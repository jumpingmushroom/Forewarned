# Forewarned: code research on boss attacks (Valheim 1.0.16 decomp)

All paths are relative to `decomp/valheim/` unless noted.
`file:line` points into those files.

---

## TL;DR

* Bosses have **no boss-specific AI**. Every boss is `MonsterAI` + `Humanoid`. Each "ability" is an
  inventory **item** (`ItemDrop.ItemData`) with its own `Attack`. Selection is generic:
  `MonsterAI.UpdateAI` → `SelectBestAttack` → `Humanoid.EquipBestWeapon` → `DoAttack` →
  `Humanoid.StartAttack` → `Attack.Start` → `ZSyncAnimation.SetTrigger(m_attackAnimation)`.
* **All of the AI, `StartAttack`, `Attack.Start/Update/OnAttackTrigger`, damage and the
  start/trigger/hit EffectLists run only on the client that owns the boss's ZDO.**
* **The one attack signal that reaches every client is the animator trigger.**
  `ZSyncAnimation.SetTrigger` is a routed RPC to `Everybody` that carries the trigger **name**
  (ZSyncAnimation.cs:148-151, 217-220). A Harmony postfix on `ZSyncAnimation.RPC_SetTrigger(long, string)`
  fires on the owner (locally, synchronously) and on every other client that has the boss
  instantiated. The name is `m_attackAnimation` (+chain level or random index).
* The **damage moment** is an animation event (`CharacterAnimEvent.OnAttackTrigger()` / `Hit()`),
  which fires on **every** client's animator. Only the owner turns it into damage
  (Humanoid.cs:553-560 `IsOwner` gate). There is **no data field for the wind-up time**. It lives
  only in the clip's event time. It can be measured at runtime from `AnimationClip.events`.
* The boss's **currently equipped attack item** is replicated through the ZDO (`ZDOVars.s_rightItem`
  = `"RightItem"`, the item prefab-name hash; VisEquipment.cs:473-478, 747). Non-owners also hold the
  same default inventory locally (Humanoid.cs:168-174), so a hash can be resolved to `ItemData`
  and its `Attack` config on every client.
* `ZSFX.Play` (the Earshot hook) only works across clients if the sound prefab is networked
  (has a `ZNetView`) or comes from an animation event (`AnimationEffect.Effect`). Attack
  `m_startEffect` and `m_triggerEffect` are created **only on the owner**. **The code alone can't
  settle this; it needs a runtime probe.**

---

## 1. How a boss picks and starts an attack

### 1.1 Tick and ownership

* `MonoUpdaters.FixedUpdate` runs the AI at a fixed **0.05 s (20 Hz)** step:
  `m_updateAITimer >= 0.05f → m_ai.UpdateAI(BaseAI.Instances, …, 0.05f)` (MonoUpdaters.cs:44-49).
* `BaseAI.UpdateAI` returns false on non-owners right away. They only copy the alert flag from
  the ZDO:
  ```csharp
  if (!m_nview.IsOwner()) { m_alerted = m_nview.GetZDO().GetBool(ZDOVars.s_alert); return false; }
  ```
  (BaseAI.cs:305-315). The owner also runs `UpdateTakeoffLanding` (BaseAI.cs:316, 354-384).
* `MonsterAI.UpdateAI` returns as soon as `base.UpdateAI` is false (MonsterAI.cs:347-352). **So the
  whole decision tree below runs only on the owner.**

### 1.2 Call chain (owner only)

```
MonoUpdaters.FixedUpdate (MonoUpdaters.cs:47)
 └ MonsterAI.UpdateAI(dt)                        MonsterAI.cs:347
    ├ UpdateTarget(...)                          MonsterAI.cs:363 → 233-345
    ├ (flee / avoid fire / circle-target / consume branches may return early: 368-459)
    ├ itemData = SelectBestAttack(humanoid, dt)  MonsterAI.cs:460 → 723-736
    │    └ every 1 s, not while InAttack: humanoid.EquipBestWeapon(target, static, hurtFriend, friend)
    │                                            MonsterAI.cs:727-733 → Humanoid.cs:670-790
    ├ flag  = Time.time - itemData.m_lastAttackTime > m_aiAttackInterval   MonsterAI.cs:461
    ├ flag2 = GetTimeSinceLastAttack() >= m_minAttackInterval               MonsterAI.cs:462
    ├ flag3 = itemData!=null && flag && flag2 && !IsTakingOff()             MonsterAI.cs:463
    ├ optional ChargeStart(m_chargeAnimationBool)                           MonsterAI.cs:472-475
    ├ optional circulate-while-charging (not ready → circle target)          MonsterAI.cs:476-481
    └ AiTarget.Enemy:
         in range (< m_aiAttackRange) && canSee && alerted → LookAt(target top point)
         if flag3 && IsLookingAt(lastKnownPos, m_aiAttackMaxAngle, m_aiInvertAngleCheck)
             DoAttack(target)                                               MonsterAI.cs:534-569
       AiTarget.FriendHurt/Friend → DoAttack(friend, isFriend:true)         MonsterAI.cs:590-617
         └ DoAttack: GetCurrentWeapon(); if (!CanUseAttack(w)) return false;
                     m_character.StartAttack(target, charge:false)          MonsterAI.cs:738-755
              └ Humanoid.StartAttack(Character target, bool secondaryAttack) Humanoid.cs:280-316
                   └ Attack.Start(...)                                      Attack.cs:356-454
                        └ m_zanim.SetTrigger(name)                          Attack.cs:411-431
```

Note: `DoAttack` passes `charge:false` into the `secondaryAttack` parameter, so AI always uses
the **primary** `m_attack` of the equipped item (MonsterAI.cs:749, Humanoid.cs:305).

### 1.3 Weapon selection: `Humanoid.EquipBestWeapon` (Humanoid.cs:670-790)

* Skipped while `InAttack()` or if the inventory is empty (673-676).
* `num` = distance to target minus target radius (678-686).
* For each weapon item where `m_baseAI.CanUseAttack(item)` holds (695):
  * **Enemy** type (699-726):
    * `num < m_aiAttackRangeMin` → skip it entirely (701-704). **The minimum range is enforced
      here only.**
    * It goes into `allWeapons` (705).
    * No target, or still on cooldown (`time - item.m_lastAttackTime < m_aiAttackInterval`) → not
      a candidate (706-709).
    * `num > m_aiAttackRange` → `outofRangeWeapons` (710-714).
    * `m_aiPrioritizedIfAngleCheckValid` and `IsLookingAt(target, m_aiAttackMaxAngle, invert)` →
      **equip at once and return** (715-719).
    * `m_aiPrioritized` → **equip at once and return** (720-724). Inventory order decides ties.
    * Otherwise it goes into `optimalWeapons` (725).
  * **FriendHurt / Friend**: same cooldown check, prioritized → equip, else `optimalWeapons`
    (727-747).
* Pick order: a prioritized item in optimal, else **a random optimal one** (749-760), else
  prioritized or random out-of-range (761-772), else prioritized or random from all (773-784),
  else unequip (785-789).
* So "which attack comes next" is **random among ready, in-range items** unless an item is
  prioritized.

### 1.4 `BaseAI.CanUseAttack(ItemData)` (BaseAI.cs:1726-1764): the phase gates

```csharp
if (item.m_shared.m_aiInDungeonOnly && !m_character.InInterior()) return false;
if (item.m_shared.m_aiMaxHealthPercentage < 1f && m_character.GetHealthPercentage() > item.m_shared.m_aiMaxHealthPercentage) return false;
if (item.m_shared.m_aiMinHealthPercentage > 0f && m_character.GetHealthPercentage() < item.m_shared.m_aiMinHealthPercentage) return false;
bool flag = IsFlying(); bool flag2 = IsSwimming();
if (item.m_shared.m_aiWhenFlying && flag) { alt = GetAltitude(); return alt > AltMin && alt < AltMax; }
if (item.m_shared.m_aiInMistOnly && !ParticleMist.IsInMist(...)) return false;
if (item.m_shared.m_aiWhenWalking && !flag && !flag2) return true;
if (item.m_shared.m_aiWhenSwiming && flag2) return true;
return false;
```

* The field names in 1.0.16 are **`m_aiMaxHealthPercentage` / `m_aiMinHealthPercentage`**, not
  `m_aiAttackMaxHealth`/`MinHealth` (ItemDrop.cs:372-379, `[Range(0,1)]`, defaults 1 and 0).
* They are **fractions (0..1)** of max health: `GetHealthPercentage() = GetHealth()/GetMaxHealth()`
  (Character.cs:3050-3053). Health is read from the ZDO (Character.cs:3005-3008), so the value is
  the same on every client.
* Semantics: the item is usable when `hp ≤ max` (it is blocked only if `hp > max`, so **inclusive**)
  and `hp ≥ min` (blocked only if `hp < min`, so **inclusive**). A gate turns off when `max == 1`
  or `min == 0`.
* Flying: if the item allows flying and the boss is flying, the result depends **only** on the
  altitude window (exclusive bounds, 1744-1749). The mist and walk/swim checks are skipped.
  Flying with `m_aiWhenFlying == false` falls through, and the walk check fails because `flag` is
  true, so the attack is blocked.
* `m_aiInDungeon`: there is no such field. Only `m_aiInDungeonOnly` (ItemDrop.cs:367) and
  `m_aiInMistOnly` (ItemDrop.cs:370) exist. `Attack.m_cantUseInDungeon` (Attack.cs:141) only
  blocks **players** (Attack.cs:382-386).
* `CanUseAttack` runs twice: in `EquipBestWeapon` (Humanoid.cs:695) and again in `DoAttack`
  (MonsterAI.cs:745).

### 1.5 The other AI fields (ItemDrop.cs:321-379)

| Field | Default | Used at |
|---|---|---|
| `m_aiTargetType` (Enemy/FriendHurt/Friend, ItemDrop.cs:61-66) | Enemy | MonsterAI.cs:495, 590; Humanoid.cs:699, 727, 739 |
| `m_aiAttackInterval` "Time Between Uses" | 2 | MonsterAI.cs:461; Humanoid.cs:706, 729, 739 |
| `m_aiPrioritized` | false | Humanoid.cs:720, 731, 741, 753, 765, 777 |
| `m_aiPrioritizedIfAngleCheckValid` | false | Humanoid.cs:715 |
| `m_aiAttackRange` | 2 | MonsterAI.cs:500, 534, 595; Humanoid.cs:710 |
| `m_aiAttackRangeMin` | 0 | Humanoid.cs:701 (skip if closer). MonsterAI.cs:535 only checks `< 0f`: a **negative** min makes the boss keep moving and attack while moving (it is not stopped at 552) |
| `m_aiAttackMaxAngle` | 5 | MonsterAI.cs:503-507, 565; Humanoid.cs:715 |
| `m_aiInvertAngleCheck` | false | same places |
| `m_aiWhenFlying` / `…AltitudeMin/Max` | true / 0 / 999999 | BaseAI.cs:1742-1750 |
| `m_aiWhenWalking` / `m_aiWhenSwiming` | true / true | BaseAI.cs:1755-1762 |
| `m_aiInDungeonOnly` / `m_aiInMistOnly` | false | BaseAI.cs:1728, 1751 |
| `m_aiMaxHealthPercentage` / `m_aiMinHealthPercentage` | 1 / 0 | BaseAI.cs:1732-1739 |
| MonsterAI `m_minAttackInterval` (global between any attacks) | 0 | MonsterAI.cs:64, 462 |

### 1.6 Cooldown tracking (`m_lastAttackTime`)

* `ItemDrop.ItemData.m_lastAttackTime` is `[NonSerialized] public float` (ItemDrop.cs:452-453).
* It is set to `Time.time` in `Attack.Start` on success (Attack.cs:451) and in
  `StartWithoutAnimation` (Attack.cs:464).
* It is **not saved and not synced**. `Inventory.Save` writes only name, stack, durability, pos,
  equipped, quality, variant, crafter, custom data, world level, pickedUp, cheated
  (Inventory.cs:802-831). It is local `Time.time` on the **owner's** machine.
* Effect: when ownership of the boss moves to another client, that client's copies have
  `m_lastAttackTime = 0`, so **every ability is off cooldown at once** after an owner change. A
  non-owner can't see real cooldowns, so it can only estimate them by recording the attack
  triggers it observes.
* The global gap `GetTimeSinceLastAttack()` is `Humanoid.m_timeSinceLastAttack`
  (Humanoid.cs:115, 356-358). It resets each tick while `InAttack()` and only updates on the owner
  (`UpdateAttack`, Humanoid.cs:501-516, called from `CustomFixedUpdate` only when `IsOwner`,
  Humanoid.cs:256-265).

### 1.7 Delay between selection and `StartAttack`

* Equip and attack can happen **in the same 0.05 s tick**. `SelectBestAttack` equips, then
  `GetCurrentWeapon()` returns the new item right away (MonsterAI.cs:735), then `DoAttack` runs
  later in the same `UpdateAI`. Equipping has no delay for monsters.
* An equipped item can also sit for a long time before it fires: the boss walks into range, turns
  until `IsLookingAt(... m_aiAttackMaxAngle)`, waits for `m_minAttackInterval`, and so on.
  Re-selection happens only every 1 s (`m_updateWeaponInterval = 1f`, MonsterAI.cs:21, 727-731)
  and never during an attack.
* So **equip is a weak "next attack" hint, not a wind-up signal.** `StartAttack`, and with it
  `SetTrigger`, is the moment the wind-up begins.

---

## 2. `Humanoid.StartAttack` and `Attack.Start`, step by step

### 2.1 `Humanoid.StartAttack(Character target, bool secondaryAttack)` (Humanoid.cs:280-316)

1. It fails if `(InAttack() && !HaveQueuedChain()) || InDodge() || !CanMove() || IsKnockedBack() ||
   IsStaggering() || InMinorAction()` (282-285). `HaveQueuedChain` is false for non-players (540-543).
2. It needs `GetCurrentWeapon()` (right item, else left non-torch, else `m_unarmedWeapon`;
   Humanoid.cs:475-490) and a primary or secondary attack (286-298).
3. It stops the previous `m_currentAttack` and keeps it as `m_previousAttack` (299-304).
4. `attack = weapon.m_shared.m_attack.Clone()` (305). **`target` is never used.** The aim comes
   later from `BaseAI.GetTargetCreature()` (Attack.cs:827-835).
5. If `attack.Start(...)` succeeds: `ClearActionQueue`, `StartAttackGroundCheck`,
   `m_currentAttack = attack`, `m_lastCombatTimer = 0`, and it returns true (306-314).

### 2.2 `Attack.Start(Humanoid, Rigidbody, ZSyncAnimation, CharacterAnimEvent, VisEquipment, ItemData weapon, Attack previousAttack, float timeSinceLastAttack, float attackDrawPercentage)` (Attack.cs:356-454)

1. If `m_attackAnimation == ""` it returns false (358-361).
2. Reload, dungeon, stamina, eitr, health, and ammo checks (378-409). Monsters normally pass.
3. **Animation trigger**, sent once:
   * `m_attackChainLevels > 1`: chain level carries on if the previous attack used the same
     animation and `timeSinceLastAttack <= 0.2`. Trigger = `m_attackAnimation + level`, for
     example `"attack0"`/`"attack1"` (411-422).
   * Else if `m_attackRandomAnimations >= 2`: trigger = `m_attackAnimation + Random(0..n-1)`
     (423-427).
   * Else trigger = `m_attackAnimation` (428-431).
   * This goes through `m_zanim.SetTrigger(text)`, which is ZSyncAnimation's routed RPC (see 3b).
4. Player-only rotation handling (432-450).
5. `weapon.m_lastAttackTime = Time.time` (451), `m_animEvent.ResetChain()` (452), returns true.

**Nothing visible or audible happens in `Start` apart from the trigger.**

### 2.3 `Attack.Update(dt)`, called every FixedUpdate on the owner (Attack.cs:516-560)

From `Humanoid.UpdateAttack` (Humanoid.cs:501-507), which only runs on the owner (Humanoid.cs:260-263).

* On the **first frame `m_character.InAttack()` is true**, meaning the animator is in or entering a
  state tagged `"attack"` (Humanoid.cs:271-278, `s_animatorTagAttack`, Humanoid.cs:147):
  * `BaseAI.ChargeStop()` (532)
  * stamina, eitr, health costs (533-538)
  * **`weapon.m_shared.m_startEffect.Create(...)` and `attack.m_startEffect.Create(...)` at the
    attack-origin joint** (539-541). This is the "attack start" sound/VFX, on the **owner only**.
  * `AddNoise(m_attackStartNoise)` (542). This is AI hearing noise, not audio.
  * sets the next chain level (543-547)
* `UpdateProjectile(dt)`: projectile bursts after the first (555, 798-810).
* When the animator leaves the attack tag, or on abort, it calls `Stop()` (556-559). For a
  `m_loopingAttack`, `Stop` sends `"attack_abort"` (573-576). If attached it sends `"detach"`
  (577-582). `m_attackKillsSelf` kills the creature (592-599).
* `IsStaggering()` during an attack calls `Abort()` (526-529).

### 2.4 How the animation event reaches `Attack.OnAttackTrigger`

1. The attack clip has an **AnimationEvent** with function `OnAttackTrigger` or `Hit`.
2. Unity calls it on the `CharacterAnimEvent` component on the Animator object:
   * `public void Hit() { m_character.OnAttackTrigger(); }` (CharacterAnimEvent.cs:248-251)
   * `public void OnAttackTrigger() { m_character.OnAttackTrigger(); }` (CharacterAnimEvent.cs:253-256)
   * Neither has an owner check, so **they run on every client** whose animator plays the clip.
3. `Character.OnAttackTrigger()` is an empty virtual (Character.cs:3184-3186). The override
   `Humanoid.OnAttackTrigger()` (Humanoid.cs:553-560) is **owner-gated**:
   ```csharp
   if (m_nview.IsValid() && m_nview.IsOwner() && m_currentAttack != null && GetCurrentWeapon() != null)
   { StartCoroutine(EndAttackGroundCheck()); m_currentAttack.OnAttackTrigger(); }
   ```
4. `Attack.OnAttackTrigger()` (Attack.cs:607-662): `UseAmmo`, skip if staggering, then switch on
   `m_attackType`:
   * `Horizontal`/`Vertical` → `DoMeleeAttack()` (1240-…): creates `m_triggerEffect` (1250-1251),
     runs the sweep, creates `m_hitEffect` on hits (1372-1373), `m_spawnOnTrigger` (1502-1511).
   * `Area` → `DoAreaAttack()` (1049-1094): `m_triggerEffect` (1054-1055), `OverlapSphere` at
     `origin = attackOrigin + up*m_attackHeight + fwd*m_attackRange + right*m_attackOffset` with
     radius `m_attackRayWidth` (1053, 1063), plus character spheres at `m_attackHeightChar1/2`
     with `+m_attackRayWidthCharExtra` (1064-1071), `m_hitEffect` (1075-1076), and
     `m_spawnOnTrigger` Instantiate + `IProjectile.Setup` (1091-1094).
   * `Projectile` → `ProjectileAttackTriggered()` (775-796): `m_triggerEffect` at the spawn point
     (778-779). If `m_projectileBursts == 1` it fires now, otherwise later bursts come from
     `UpdateProjectile` every `m_burstInterval` (784-791, 798-810). `FireProjectileBurst`
     (847-…) creates `m_burstEffect` (927-930) and instantiates `m_projectiles` copies of
     `m_attackProjectile` with velocity between `m_projectileVelMin` and `m_projectileVel`
     (955-998).
   * `None` → `DoNonAttack()` (1028-1042): `m_triggerEffect` and the consume status effect only.
   * **`TriggerProjectile` is declared (Attack.cs:35) but has no case in the switch and nothing
     else references it**, so it does nothing in this build.
   * Then `m_toggleFlying` (Land/TakeOff), recoil, self-damage, consume, reload (633-661).
5. Weapon trails: the animation event `TrailOn()` (CharacterAnimEvent.cs:293-300) turns trails
   on locally, then `Humanoid.OnWeaponTrailStart` (owner-gated, Humanoid.cs:545-551) →
   `Attack.OnTrailStart` creates `m_trailStartEffect` (Attack.cs:1738-1753). **The trail-start
   effect is owner only.**

### 2.5 Relevant `Attack` fields (Attack.cs)

`m_attackType` 54; `m_attackAnimation` 56; `m_chargeAnimationBool` 58; `m_attackRandomAnimations` 60;
`m_attackChainLevels` 62; `m_loopingAttack` 64; `m_speedFactor` 95 / `m_speedFactorRotation` 97
(movement and turn multipliers during the attack, read by Humanoid.cs:518-538; the animation
event `Stop` sets both to 0, Humanoid.cs:562-569); `m_attackStartNoise` 99; `m_attackOriginJoint` 123;
`m_attackRange` 125; `m_attackHeight` 127; `m_attackHeightChar1/2` 129/131; `m_attackOffset` 133;
`m_spawnOnTrigger` 135; `m_toggleFlying` 137; `m_attach` 139; `m_attackAngle` 170; `m_attackRayWidth` 172;
`m_attackRayWidthCharExtra` 174; `m_maxYAngle` 176; `m_attackProjectile` 210; `m_projectileVel` 212;
`m_projectileVelMin` 214; `m_projectileAccuracy` 219; `m_projectiles` 236; `m_projectileBursts` 238;
`m_burstInterval` 240; EffectLists `m_hitEffect` 267, `m_hitTerrainEffect` 269, `m_startEffect` 271,
`m_triggerEffect` 273, `m_trailStartEffect` 275, `m_burstEffect` 277. The weapon-level equivalents
are in `SharedData` (ItemDrop.cs:382-398).

### 2.6 Wind-up duration

* **No data field holds the wind-up or "time to hit".** `Attack` has no delay or timing field for
  melee, area or projectile hits. The only timers are `m_burstInterval` (bursts after the first),
  `m_drawDurationMin` (player bows) and `m_reloadTime`.
* Time to damage = (network delay of the trigger, non-owners only) + animator transition time +
  **the clip's `OnAttackTrigger`/`Hit` event time** ÷ (state speed × `animator.speed`).
  `animator.speed` is synced (ZSyncAnimation.cs:105-110, 143-144) and freeze-frames can change it
  (CharacterAnimEvent.cs:179-213).
* After the trigger there can be **extra delay built into the spawned objects**:
  * `SpawnAbility.m_initialSpawnDelay`, `m_preSpawnDelay` (with `m_preSpawnEffects` as a
    telegraph) and `m_spawnDelay` (SpawnAbility.cs:61-65, 85-87, 131-134, 201-205, 292-296).
  * `Aoe.m_activationDelay`, `m_chainStartDelay` (Aoe.cs:147, 91, 236, 241).
  * `MeteorSmash.m_timeToLand` (MeteorSmash.cs:17).
  * Projectile flight time.
* **Earliest reliable "attack X is starting" moment: `Attack.Start` → `SetTrigger`.** On the owner
  that's a postfix on `Humanoid.StartAttack` or `Attack.Start`. On every client it's
  `ZSyncAnimation.RPC_SetTrigger`.
* Runtime measurement idea: after the trigger, read `animator.GetNextAnimatorClipInfo(0)` or
  `GetCurrentAnimatorClipInfo(0)` once the attack-tagged state is entered (`Character.InAttack()`
  works everywhere because it reads animator tag hashes, Character.cs:3724-3741). Then scan
  `clip.events` for `functionName == "OnAttackTrigger" || "Hit"` and use `evt.time`. Cache this
  per (creature prefab, trigger name). A one-off dump of
  `animator.runtimeAnimatorController.animationClips[*].events` gives every boss clip's hit times.

---

## 3. Multiplayer: what a non-owner sees

Ownership: `ZNetView.IsOwner()` → `m_zdo.IsOwner()` (ZNetView.cs:227-234).

### (a) `Humanoid.StartAttack` / `Attack.Start`: **owner only**

They are called only from `MonsterAI.DoAttack` (MonsterAI.cs:749), which is behind
`BaseAI.UpdateAI`'s owner check (BaseAI.cs:311-315). `Humanoid.UpdateAttack` (so `Attack.Update`
and the start effects) is owner only (Humanoid.cs:260-263). `Humanoid.OnAttackTrigger` is owner
only (Humanoid.cs:555). On a non-owner, `Humanoid.m_currentAttack` stays null for monsters.

### (b) Animation trigger: **reaches everyone, with the name**

```csharp
public void SetTrigger(string name) { m_nview.InvokeRPC(ZNetView.Everybody, "SetTrigger", name); } // ZSyncAnimation.cs:148-151
private void RPC_SetTrigger(long sender, string name) { m_animator.SetTrigger(name); }              // ZSyncAnimation.cs:217-220
// registered in Awake: m_nview.Register<string>("SetTrigger", RPC_SetTrigger);                     // ZSyncAnimation.cs:45
```

* `ZNetView.InvokeRPC(long target, …)` → `ZRoutedRpc.InvokeRoutedRPC(target, m_zdo.m_uid, …)`
  (ZNetView.cs:326-329).
* With target `0` (Everybody), the sender **handles it locally right away** (ZRoutedRpc.cs:130-133)
  and also routes it (134-137). The server forwards it to every other ready peer
  (ZRoutedRpc.cs:155-163, 179-186).
* Receivers resolve the ZDO to their local `ZNetView` instance and drop the call if the object
  isn't instantiated there (ZRoutedRpc.cs:189-207). Any client that has the boss loaded gets
  `RPC_SetTrigger(sender, "<trigger>")`.
* Triggers are **not** stored in the ZDO. Bools, floats and ints are (salted `438569 + hash`,
  ZSyncAnimation.cs:153-215). Non-owners apply only the parameters listed in
  `m_syncBools/Floats/Ints` (118-142), but any client can read the raw ZDO keys. So a late
  joiner never sees a trigger that already fired.
* **Hook: Harmony postfix on `ZSyncAnimation.RPC_SetTrigger(long sender, string name)`** (private).
  Get the boss with `__instance.GetComponent<Character>()`. On the owner it runs synchronously
  inside `Attack.Start`. Elsewhere it arrives after about one-way latency (×2 when relayed through
  a dedicated server).
* Patching the public `SetTrigger` sees only the caller, which is the owner.
* Caveat: other calls also go through this RPC, for example `"attack_abort"`, `"detach"`
  (Attack.cs:575, 579), Leviathan `"shake"`/`"dive"` (Leviathan.cs:98, 109), staggers and so on.
  Filter by name. `Character.TakeOff/Land` use the raw `Animator.SetTrigger("fly_takeoff"/"fly_land")`,
  which is **not** networked (Character.cs:4394-4405, `m_animator` is a plain `Animator`,
  Character.cs:346, 667).

### (c) EffectLists and sounds

* `EffectList.Create` is a plain local `Object.Instantiate` of each prefab (EffectList.cs:37-131).
  If a prefab has a `ZNetView`, its `Awake` creates a new ZDO owned locally (ZNetView.cs:88-106)
  that replicates to others, and `Create` also tags it with the creator (EffectList.cs:72-79).
  This proves some effect prefabs are networked.
* Attack `m_startEffect`/`weapon.m_startEffect` (Attack.cs:540-541), `m_triggerEffect`
  (778-779, 1035-1036, 1054-1055, 1250-1251), `m_hitEffect`, `m_burstEffect` (927-930) and
  `m_trailStartEffect` (1743-1751) are all created **only on the owner**.
  * If the sfx/vfx prefab has **no** ZNetView, **non-owners never get it**, and Earshot's
    `ZSFX.Play` postfix won't fire for them.
  * If it **has** one, non-owners create it when the ZDO arrives. That takes up to about 0.05 s
    send tick (ZDOMan.cs:903-927) + latency + ZNetScene create tick at 1/30 s
    (ZNetScene.cs:349-357). Then `ZSFX.Play` fires there.
  * **The code can't settle which prefabs are networked. Runtime probe:** for each boss item,
    check `ZNetScene.instance.GetPrefab(name)` and `effectPrefab.GetComponent<ZNetView>() != null`
    for every `EffectData.m_prefab` in `m_shared.m_attack.m_startEffect/m_triggerEffect` and in
    `m_shared.m_startEffect/m_triggerEffect`.
* **Animation-event-driven effects run on every client.** `AnimationEffect.Effect(AnimationEvent e)`
  instantiates `e.objectReferenceParameter` at a joint (AnimationEffect.cs:19-36), and `Attach`
  does the same parented (38-89). These run wherever the animator plays the clip, with no owner
  check, at the clip-event time (so after the trigger). If a boss's wind-up roar or vfx is an
  `AnimationEffect` event, **`ZSFX.Play` sees it on every client**, but only once the clip
  reaches that event, which is not instant.
* Other EffectLists: `BaseAI.m_alertedEffects` (BaseAI.cs:1566). Non-owners also call
  `SetAlerted` via the ZDO, but only when `m_alerted` changes through the local path. Non-owners
  set `m_alerted` directly (BaseAI.cs:313) and so **don't** create alert effects. Also
  `MonsterAI.m_wakeupEffects` (owner, MonsterAI.cs:885).

### (d) Projectiles, Aoe, SpawnAbility

* `Projectile` requires a `ZNetView` (Projectile.cs:179, 235). It's instantiated on the owner
  (Attack.cs:955), so non-owners get it through ZDO replication with the delay from (c).
  Non-owners only rotate it visually. Movement and hits are owner only (Projectile.cs:255-265).
  `Setup` (owner, Projectile.cs:408-) sets `m_owner`, so on non-owners `m_owner` is unknown.
* `Aoe` looks up the `ZNetView` in its parents (Aoe.cs:210). Damage logic returns early on
  non-owners (Aoe.cs:308-313). Without a ZNetView it runs wherever it exists. `m_activationDelay`
  (Aoe.cs:147, 236) is a built-in "telegraph" window.
* `SpawnAbility` has no ZNetView handling at all (SpawnAbility.cs). It runs where it's
  instantiated and `Setup` is called, which is the owner (Attack.cs:1093, 1504-1505).
  Its `m_preSpawnEffects` / `m_spawnEffects` are local EffectLists (SpawnAbility.cs:201, 292).
  The spawned prefabs (minions, AoEs) replicate if networked. **Runtime probe:** whether each boss
  `m_spawnOnTrigger` prefab has a ZNetView.
* `TeleportAbility.Setup` (owner) moves the boss and can `Player.MessageAllInRange(..., Center, m_message)`
  (TeleportAbility.cs:14-32). That message reaches all clients in range.

### (e) Is there a ZDO field for the current attack?

* **No "current attack" or "attacking" ZDO var for characters.** `ZDOVars.s_lastAttack` is
  only used by `Turret` (ZDOVars.cs:155; Turret.cs:340, 384).
* Replicated pieces that help:
  * **`ZDOVars.s_rightItem` ("RightItem")**: hash of `m_rightItem.m_dropPrefab.name`
    (Humanoid.cs:1408 → VisEquipment.cs:463-478). Every client reads it in
    `VisEquipment.UpdateEquipmentVisuals` (VisEquipment.cs:743-747). `s_leftItem` covers
    Bow/Shield/TwoHandedWeaponLeft items (Humanoid.cs:1167-1196).
  * Non-owners hold the **same default inventory**: `Humanoid.Start` → `GiveDefaultItems()` for
    all non-players on every client (Humanoid.cs:168-174, 181-245, seeded by the ZDO `s_seed`,
    Humanoid.cs:157-165). Resolve the hash by matching
    `inv.GetAllItems().First(i => i.m_dropPrefab.name.GetStableHashCode() == hash)` and you get
    that item's `Attack` (animation name, type, ranges) on any client.
  * Ordering caveat: the equip ZDO write goes out in the next `ZDOMan` send (≤0.05 s), while the
    trigger RPC goes out immediately. A non-owner may get `RPC_SetTrigger` **before** the new
    `RightItem` value. Map by trigger name, or re-read `RightItem` 0.1-0.2 s later.
    **Runtime probe needed.**
  * `s_alert` (BaseAI.cs:1562), `s_haveTargetHash` (a bool: does the boss have a target;
    BaseAI.cs:1657-1669), `s_health`/`s_maxHealth` (Character.cs:3007, 3064),
    `s_animationSpeed`, and the animator sync ints/bools (for example the `m_chargeAnimationBool`
    set by `ChargeStart`, BaseAI.cs:1801-1808 → ZSyncAnimation.SetBool → ZDO key `438569+hash`).
  * `s_lookTarget` ("LookTarget", a Vector3): the owner writes **the target creature's eye point**
    every 0.2 s for MonsterAI characters with `m_headRotation` and a humanoid head bone
    (CharacterAnimEvent.cs:465-501). Non-owners read it (504). This is the only replicated "who is
    the boss targeting" signal. It gives a position, not an id, and only exists if `m_head` was
    found (CharacterAnimEvent.cs:132, 467). **Runtime probe:** whether boss rigs have it.

### (f) `m_currentAttack` on non-owners

It's always null for AI creatures on non-owners. It's assigned only in `Humanoid.StartAttack`
(Humanoid.cs:310), which non-owners never call. **`Character.InAttack()` does work on non-owners**
because it uses the local animator's state tags (Humanoid.cs:271-278; Character.cs:3704-3741), and
the animator runs everywhere and receives the trigger.

### Conclusion

| Signal | Owner (solo / host-owner) | Non-owner | Timing |
|---|---|---|---|
| `Humanoid.StartAttack` / `Attack.Start` postfix | yes, with full `ItemData` and `Attack` | **no** | t=0 |
| `ZSyncAnimation.RPC_SetTrigger` postfix | yes (synchronous, inside `Attack.Start`) | **yes** (name only) | t=0 (+latency) |
| `RightItem` ZDO → local `ItemData` | yes | yes (may lag the trigger) | ≤ t=0 |
| `Character.InAttack()` false→true poll | yes | yes | trigger + transition |
| `CharacterAnimEvent.OnAttackTrigger/Hit` prefix | yes | **yes** (event fires; owner applies damage) | the damage moment |
| Attack start/trigger EffectLists → `ZSFX.Play` | yes | **only if the prefab is networked** (probe) | start effects at the attack-state entry |
| `AnimationEffect.Effect` → `ZSFX.Play` | yes | yes | clip event time |
| Projectile / spawned networked objects | yes | yes, after ZDO replication | after hit/trigger |

**Recommendation: use `RPC_SetTrigger` as the main signal for every player.** Key on
(boss prefab name, trigger name) and confirm or enrich with the `RightItem` hash → local
`ItemData`. Use the `StartAttack` postfix only as an optional owner-side shortcut. A solo
`StartAttack` hook does **not** carry over to non-owners. A sound hook carries over only for
networked or animation-event sounds. The trigger hook behaves the same in solo and multiplayer,
because the owner also goes through `RPC_SetTrigger` locally.

---

## 4. Boss-specific logic

* **No boss-specific C# classes.** A grep across `decomp/` for
  `Fader|FrozenKing|SeekerQueen|Dragon|GoblinKing|gd_king|Bonemass|Eikthyr|aspect` finds only:
  global keys `defeated_eikthyr, defeated_dragon, defeated_goblinking, defeated_gdking, defeated_bonemass, activeBosses`
  (GlobalKeys.cs:45-50); `Pathfinding.AgentType.SeekerQueen` (Pathfinding.cs:41, 228);
  guardian-power stat names (ItemStand.cs:256-263; Player.cs:6314-6321); tutorial hooks
  (`$item_frozenking_drop` at Player.cs:5630; `$item_trophy_eikthyr` at Player.cs:5634).
  **There's no "aspect" or BossAspects code.** Prefabs named `aspect_*` would just be item or
  effect prefabs, data only.
* Character name tokens (`$enemy_eikthyr` and so on) **do not appear in code**. They're prefab
  data in `Character.m_name` (Character.cs:79), shown through `GetHoverName()` →
  `Localization.Localize(m_name)` (Character.cs:3876-3888). Identify bosses by
  `Character.IsBoss()` (`m_boss`, Character.cs:88, 3116-3119) plus the prefab name
  (`Utils.GetPrefabName(gameObject)` or the ZDO prefab hash). Get the names from a runtime dump.
* Boss fields on `Character`: `m_boss` 88, `m_bossOrder` 92, `m_dontHideBossHud` 96,
  `m_bossEvent` 99 (used by RandEventSystem.cs:213-234 and Location.cs:183 via
  `EnemyHud.GetActiveBoss()`), `m_defeatSetGlobalKey` 103, `m_dreamCinematic` 106. There's also
  `Faction.Boss` (Character.cs enum, line 8-24), which matters for crown-fear immunity
  (MonsterAI.cs:387).
* Generic MonsterAI knobs bosses use: `m_enableHuntPlayer` → `SetHuntPlayer(true)` in Awake
  (MonsterAI.cs:52, 164-167; stored in ZDO `s_huntPlayer`, BaseAI.cs:1485-1491).
  `m_circulateWhileCharging`/`…Flying` (MonsterAI.cs:48-50, 476-481) circles the target while
  every item is on cooldown. `m_circleTargetInterval/Duration/Distance` (MonsterAI.cs:67-71,
  447-459). `m_minAttackInterval` (64). `m_maxChaseDistance` (62, 336). `m_interceptTime*` (58-60).
  `m_alertedMessage`/`m_spawnMessage`/`m_deathMessage` broadcast as Center messages
  (BaseAI.cs:118-122, 232-235, 711-713, 1574-1578).
* **Flying (Moder):** `BaseAI.m_randomFly, m_chanceToTakeoff, m_chanceToLand, m_groundDuration,
  m_airDuration, m_maxLandAltitude, m_takeoffTime, m_flyAltitudeMin/Max, m_flyAbsMinAltitude`
  (BaseAI.cs:81-99). `UpdateTakeoffLanding` (owner only, not during attack or stagger) takes off
  after `m_groundDuration` and lands after `m_airDuration` if altitude < `m_maxLandAltitude`
  (BaseAI.cs:354-384). `IsTakingOff()` blocks attacks for `m_takeoffTime` (BaseAI.cs:345-352;
  MonsterAI.cs:463). Attacks can toggle flight (`Attack.m_toggleFlying`, Attack.cs:633-643), as can
  the animation events `TakeOff()`/`Land()` (CharacterAnimEvent.cs:263-277). Items pick their
  flight state through `m_aiWhenFlying`, the altitude window and `m_aiWhenWalking`.
  `Character.m_flying` isn't directly replicated. The owner writes animator bool `"flying"`
  (Character.cs:502, 1601, 1560, 1847) to the ZDO.
* **Phases:** the only phase mechanism is the per-item health window
  `m_aiMaxHealthPercentage/m_aiMinHealthPercentage` (section 1.4). Health is in the ZDO, so
  phase-gated attacks can be predicted on every client.
* Related ability components: `SpawnAbility` (minions or AoE spawns with pre-spawn telegraphs),
  `TriggerSpawnAbility` (TriggerSpawnAbility.cs:10-14 → `TriggerSpawner.TriggerAllInRange`),
  `TeleportAbility` (teleports to a tagged object and can show a message), `MeteorSmash` (a visual
  meteor with `m_timeToLand`, MeteorSmash.cs:17-58), `Aoe` (activation delay, ttl, chain),
  `AnimSetTrigger` (a StateMachineBehaviour that sets local animator triggers on state
  enter/exit, AnimSetTrigger.cs:13-41; local only and not seen by `RPC_SetTrigger`).
* `BossStone` (BossStone.cs) is the trophy altar at the start temple. It just toggles visuals and
  global keys and has nothing to do with the fight.
* **"Boss fight active" detection:**
  * `EnemyHud.TestShow`: a boss HUD shows if the boss is within **`m_maxShowDistanceBoss = 100f`**
    of the local player **and** `BaseAI.IsAlerted()` (or `m_dontHideBossHud` while already shown)
    (EnemyHud.cs:56, 101-114). `IsAlerted` comes from the ZDO on non-owners (BaseAI.cs:313).
  * `EnemyHud.instance.ShowingBossHud()` / `GetActiveBoss()` (EnemyHud.cs:256-278) are the
    vanilla answers and work on every client. Use them.
  * Global key `activeBosses` (incremented on alert, BaseAI.cs:1568-1573; decremented on death,
    Character.cs:2996-2999) is world-wide and coarse.

---

## 5. Hook candidates

| Patch point | Signature | Knows | Fires on non-owner? |
|---|---|---|---|
| `Humanoid.StartAttack` postfix (`__result`) | `public override bool StartAttack(Character target, bool secondaryAttack)` (Humanoid.cs:280) | attacker (`__instance`), `GetCurrentWeapon()` → `ItemData` (`m_shared.m_name`, `m_dropPrefab.name`), `m_currentAttack` (protected field: `m_attackAnimation`, `m_attackType`, ranges), `target` (for AI it's `m_targetCreature`) | **No** |
| `Attack.Start` postfix | `public bool Start(Humanoid character, Rigidbody body, ZSyncAnimation zanim, CharacterAnimEvent animEvent, VisEquipment visEquipment, ItemDrop.ItemData weapon, Attack previousAttack, float timeSinceLastAttack, float attackDrawPercentage)` (Attack.cs:356) | same, plus the attack instance (`__instance`), chain level (private) | **No** |
| `ZSyncAnimation.RPC_SetTrigger` postfix | `private void RPC_SetTrigger(long sender, string name)` (ZSyncAnimation.cs:217) | `__instance.GetComponent<Character>()` (the boss), trigger name, sender peer id (the owner) | **Yes**, for everyone with the object loaded. Owner gets it synchronously |
| `ZSyncAnimation.SetTrigger` | `public void SetTrigger(string name)` (148) | caller side only | No (owner/caller only) |
| `CharacterAnimEvent.OnAttackTrigger` / `Hit` prefix | `public void OnAttackTrigger()` (253), `public void Hit()` (248) | private `m_character`. The damage moment | **Yes** (local animator) |
| `CharacterAnimEvent.TrailOn` | `public void TrailOn()` (293) | swing start (melee) | Yes |
| `AnimationEffect.Effect` / `Attach` | `public void Effect(AnimationEvent e)` (AnimationEffect.cs:19) | effect prefab (`e.objectReferenceParameter`), joint; find the Character with `GetComponentInParent<Character>()` | Yes |
| `Attack.OnAttackTrigger` | `public void OnAttackTrigger()` (Attack.cs:607) | full attack data | No |
| `EffectList.Create` | `public GameObject[] Create(Vector3 basePos, Quaternion baseRot, Transform baseParent = null, float scale = 1f, int variant = -1, ZDOID gamepadEffectsExclusiveToPlayer = default)` (EffectList.cs:37) | prefabs; the last arg is the creator ZDOID (Attack passes `m_character.GetZDOID()`) | Where it's called (attack lists: owner only) |
| `ZSFX.Play` | `public void Play()` (ZSFX.cs:294) | sfx prefab and position | Wherever the sfx object exists (networked prefab or anim-event) |
| `BaseAI.CanUseAttack` / `Humanoid.EquipBestWeapon` | BaseAI.cs:1726 / Humanoid.cs:670 | selection internals | No |
| `MessageHud.RPC_ShowMessage` / `ShowMessage` | MessageHud.cs:151/156 | boss alert/spawn text (MessageAll) | Yes |

**"Is the boss targeting me?"**

* `MonsterAI.GetTargetCreature()` returns the private `m_targetCreature` (MonsterAI.cs:807-810)
  and is set only on the owner. On non-owners it stays null.
* Replicated options:
  1. `Player.RPC_OnTargeted`. The owner of any monster targeting a player calls
     `target.OnTargeted(sensed, alerted)` (MonsterAI.cs:319-322). That sends an RPC to the
     player's owner, which is that player's own client (Player.cs:6768-6783). The receiver sets
     `m_timeSinceTargeted` (6785-6796), and `IsTargeted()` returns true for 1 s (6807-6810). The
     RPC carries no monster id (only the sender peer), so it means "something targets me".
  2. ZDO `s_lookTarget` ≈ the target's eye point (section 3e), if the boss rig has a head bone.
  3. Projectile aim. `GetProjectileSpawnPoint` aims at `GetTargetCreature().GetCenterPoint()`
     (Attack.cs:827-835) on the owner.
  4. Fallback: boss facing (`transform.forward`, synced through ZSyncTransform) plus distance.

---

## 6. Hud and messages

* `MessageHud.instance.ShowMessage(MessageHud.MessageType type, string text, int amount = 0, Sprite icon = null, bool showDespiteHiddenHUD = false, bool log = true)`
  (MessageHud.cs:156-199). `MessageType { TopLeft = 1, Center }` (MessageHud.cs:8-11).
  * `Center`: sets `m_messageCenterText.text` (TMP, MessageHud.cs:56), fades in at once and out
    over 4 s (179-192). **A new Center message replaces the current one.** There's no queue, and
    the boss alert message uses the same slot. It's also added to the log unless `log:false`.
  * `TopLeft`: queued and shown 1 s apart, merged if the same (166-177, 213-257).
  * It's suppressed when `Hud.IsUserHidden()` unless `showDespiteHiddenHUD` (159-162).
  * `MessageAll(type, text)` sends routed RPC `"ShowMessage"` to everyone (146-149). A local
    warning should call `ShowMessage` directly.
  * `Player.Message(type, msg, …)` is also local for the local player.
* **MessageHud has no alert sound** (no `m_msgSound`). The only sound object is
  `m_biomeFoundStinger` (MessageHud.cs:68, 280-283). Usable vanilla sounds:
  * `Hud.m_selectItemEffect` / `m_selectItemCategoryEffect` EffectLists (Hud.cs:78-80), which
    are UI clicks.
  * `Player.m_localPlayer` EffectLists (Player.cs:118-282, for example `m_skillLevelupEffects`,
    `m_perfectDodgeEffects`, `m_adrenalinePopEffects`).
  * `ButtonSfx.m_sfxPrefab` (guiutils/ButtonSfx.cs:7).
  * Instantiate any sfx prefab with `ZNetScene.instance.GetPrefab("sfx_…")`. Avoid prefabs that
    have a ZNetView, or they'll replicate. **Pick names with a runtime probe.**
* `Hud` (Hud.cs): `m_rootObject` (28), `m_targetedAlert`/`m_targeted`/`m_hidden` stealth icons
  (260, 954-986), `m_eventBar`, which is hidden while `EnemyHud.ShowingBossHud()` (1702-1714),
  and `Hud.IsUserHidden()` (1765).
* **Boss health bar:** `EnemyHud.m_baseHudBoss` is instantiated under `m_hudRoot`
  (EnemyHud.cs:48, 130-135). It is **not** repositioned in code: the world-to-screen placement in
  `UpdateHuds` is skipped for bosses (`if (!IsBoss() && …)`, EnemyHud.cs:234-248). So its screen
  position (top-centre) comes from the prefab layout. **Runtime probe:** read the `RectTransform`
  of `m_baseHudBoss` (anchors and anchoredPosition, its height) so warnings can sit below it.
  Its children are `Health/health_fast`, `Health/health_slow`, `Name` (EnemyHud.cs:136-153).
* `Chat` (`Chat : Terminal`, Chat.cs:10) has `AddString(string text)` (local line,
  Terminal.cs:3094) and `SetNpcText(GameObject talker, Vector3 offset, float cullDistance, float ttl, string topic, string text, bool large)`
  (Chat.cs:687). The second puts **in-world floating text over a GameObject**, which could label
  the boss itself.

---

## Runtime probes still needed

1. A dump of each boss prefab: `Character.m_name`, prefab name, inventory item names, and per item
   `m_attack.m_attackAnimation`/`m_attackType`/`m_attackRandomAnimations`/`m_attackChainLevels`,
   AI gates, `m_spawnOnTrigger`/`m_attackProjectile` names.
2. For each attack clip: `OnAttackTrigger`/`Hit` event time (`AnimationClip.events`) and clip
   length, which gives the wind-up seconds.
3. Which attack and start effect sfx prefabs have a `ZNetView`, and which boss sounds come from
   `AnimationEffect` events. Together these decide whether the Earshot-style sound hook works
   for non-owners.
4. On a non-owner client: the order and lag of `RPC_SetTrigger` vs the `RightItem` ZDO change.
5. Whether boss rigs have `CharacterAnimEvent.m_head`, i.e. whether `LookTarget` is written.
6. Boss HUD `RectTransform` placement.
