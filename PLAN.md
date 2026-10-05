# Forewarned — Technical Plan

**Idea:** boss attack warnings for Valheim in the style of World of Warcraft's Deadly Boss Mods
(DBM). When a boss starts winding up an attack you need to move for, Forewarned says what's
coming and what to do: `Fire breath: get behind Fader`, `Meteors: keep moving`.

**Target build:** Valheim 1.0.16 (Unity 6000.0.75f1). `assembly_valheim.dll` is the Sep 25 build
on the rig (byte-identical to Earshot's `lib/`), decompiled with `ilspycmd` into the gitignored
`decomp/`. Game data comes from the SoftRef bundles and `resources.assets`, copied from the rig
into the gitignored `gamedata/` on 2026-10-05 and decoded with `tools/` (§6).

**Scope (decided before research):**
1. **React first.** Warn when a boss starts an attack's wind-up. Prediction (DBM "next X in ~N s"
   timer bars) comes later; the data model and UI must allow it, but it isn't built now.
2. **Solo first, multiplayer soon after.** Every decision keeps multiplayer in mind.
3. **Text and visuals, following DBM:** special warnings (big centre text with an action, optional
   sound and screen flash), smaller announce lines, a countdown bar sized to the wind-up, and
   where it helps a ground marker or an arrow.
4. **One module per boss, in C#.** Each declares its abilities and what to warn for each. Every
   ability has its own options (warning, sound, visual) in ConfigurationManager (F1), grouped by
   boss. Warning text goes through translation keys.

**Build setup:** the same as Earshot: net472, BepInEx 5, Harmony, `BepInEx.AssemblyPublicizer`
for `assembly_valheim`, reference DLLs in a gitignored `lib/`, no Jotunn. Game-independent model
code has no UnityEngine or game types and is tested with xUnit on net8.0.

**Name:** Forewarned. The working name Gjallarhorn clashes with RAGEmedia-Gjallarhorn on
Thunderstore, a boss-defeat and death announcer (`docs/research/web.md` §1). "Forewarned" is
unused on Thunderstore and Nexus (checked 2026-10-05).

**Detailed research reports** (the sections below summarise them):
- `docs/research/code.md`: the attack pipeline and multiplayer, from the decompiled code, with
  `file:line` references into `decomp/valheim/`.
- `docs/research/data.md`: every boss and attack decoded from the bundles: AI gates, geometry,
  spawned objects, animation wind-ups, the sound cross-reference, English strings.
- `docs/research/web.md`: the name check, prior art, DBM and BigWigs conventions, wiki
  counterplay, and ConfigurationManager.

---

## 1. How a boss picks and starts an attack

There is **no boss-specific code**. Every boss is a `Humanoid` with a `MonsterAI`, and each ability
is an inventory item (`ItemDrop.ItemData`) whose `SharedData.m_attack` is an `Attack`
(code.md §4). Phases are data only: health gates on items, plus Kall's separate phase creatures.

**Call chain, owner only** (code.md §1.2):
`MonoUpdaters.FixedUpdate` at 20 Hz (MonoUpdaters.cs:44-49) → `MonsterAI.UpdateAI`
(MonsterAI.cs:347) → `SelectBestAttack` (MonsterAI.cs:723-736, which re-equips at most every 1 s
and never mid-attack) → `Humanoid.EquipBestWeapon` (Humanoid.cs:670-790) → `DoAttack`
(MonsterAI.cs:738-755) → `Humanoid.StartAttack` (Humanoid.cs:280-316) → `Attack.Start`
(Attack.cs:356-454) → `ZSyncAnimation.SetTrigger(trigger)` (Attack.cs:411-431).

- **Selection** is random among the items that are off cooldown and in range, unless one is
  `m_aiPrioritized`, which wins (Humanoid.cs:715-724, 749-784). `m_aiAttackRangeMin` is enforced
  only here (Humanoid.cs:701).
- **AI fields in 1.0.16** (ItemDrop.cs:321-379): `m_aiAttackInterval` (cooldown),
  `m_aiAttackRange`/`RangeMin`, `m_aiAttackMaxAngle`, `m_aiPrioritized`,
  `m_aiPrioritizedIfAngleCheckValid`, `m_aiTargetType`, `m_aiWhenFlying` (+ altitude window),
  `m_aiWhenWalking`, `m_aiWhenSwiming`, `m_aiInDungeonOnly`, `m_aiInMistOnly`, and the health gates
  `m_aiMaxHealthPercentage` / `m_aiMinHealthPercentage`. The research brief's
  `m_aiAttackMaxHealth/MinHealth` and `m_aiInDungeon` do not exist in this build.
- **Health gates** (BaseAI.cs:1732-1739) are fractions 0..1 of max health and both bounds are
  inclusive: usable when `min ≤ hp% ≤ max`. Health is read from the ZDO (Character.cs:3005-3008),
  so **every client can tell which gated abilities are live**.
- **Global gap:** `MonsterAI.m_minAttackInterval` (MonsterAI.cs:64, 462) sets a minimum time
  between any two attacks (0 to 2 s per boss).
- **Cooldowns:** `ItemData.m_lastAttackTime` is `[NonSerialized]` (ItemDrop.cs:452), set in
  `Attack.Start` (Attack.cs:451), never saved or synced. When ownership moves, every ability comes
  off cooldown at once. A non-owner can only estimate cooldowns from the triggers it sees.
- **AI uses only the primary attack.** `DoAttack` passes `false` as `secondaryAttack`
  (MonsterAI.cs:749).
- **Trigger name** = `m_attackAnimation`, plus a chain level (`m_attackChainLevels > 1`) or a
  random index 0..n-1 (`m_attackRandomAnimations ≥ 2`) (Attack.cs:411-431). For example, the Elder
  sends `stomp0`/`stomp1`, Bonemass `punch0`/`punch1`, the Queen `attack_slash0..3`.
- Equipping is only a weak "next attack" hint: an item can sit equipped for seconds while the boss
  walks into range and turns (code.md §1.7).

## 2. When the hit lands

- **`Attack.Start` only fires the animator trigger.** Nothing else visible happens there
  (Attack.cs:356-454).
- **`m_startEffect`** (item and attack) is created on the first frame the animator is in an
  `attack`-tagged state (Attack.cs:530-541), so t ≈ 0 plus the transition blend. Owner only.
- **Damage** comes from an animation event, `CharacterAnimEvent.Hit()` or `OnAttackTrigger()`
  (CharacterAnimEvent.cs:248-256). Those run on every client's animator, but
  `Humanoid.OnAttackTrigger` applies damage only on the owner (Humanoid.cs:553-560), which then
  runs `Attack.OnAttackTrigger` per attack type (Attack.cs:607-662). `TriggerProjectile` is
  declared but unhandled, so it does nothing.
- **No data field holds the wind-up.** It exists only as the event time in the clip, divided by
  state speed × animator speed (with `Speed()` events). `docs/research/data.md` §6 computes it for
  every boss attack offline. It excludes the 0.1–0.25 s transition blend and, for non-owners,
  network latency.
- **Some attacks hurt later than the hit event:** `SpawnAbility` delays (fissures 0.45 s apart,
  each arming after `Aoe.m_activationDelay` 4 s), `MeteorSmash.m_timeToLand`, projectile flight,
  and lingering AoEs (ttl 10–20 s).
- **Earliest reliable "attack X is starting" moment: `SetTrigger`.** On every client that is the
  `RPC_SetTrigger` call (§3).
- **Runtime check:** the offline wind-ups can be confirmed in game by reading
  `AnimationClip.events` for the clip the boss enters after the trigger (code.md §2.6).

## 3. Multiplayer: which signal reaches every player

A boss's AI runs only on the client that owns its ZDO (BaseAI.cs:305-315). What a non-owner
gets (code.md §3):

| Signal | Owner | Non-owner | Timing |
|---|---|---|---|
| `Humanoid.StartAttack` / `Attack.Start` postfix (full `ItemData`) | yes | **no** | t = 0 |
| **`ZSyncAnimation.RPC_SetTrigger(long, string)` postfix** (trigger name) | yes, synchronous inside `Attack.Start` | **yes** | t = 0 (+ latency) |
| ZDO `RightItem` hash → the local copy of the item (same default inventory everywhere) | yes | yes, may lag the trigger ≤ 0.05 s | ≤ t = 0 |
| `CharacterAnimEvent.Hit/OnAttackTrigger` (damage moment) | yes | yes | at the hit |
| Item start/trigger effects → `ZSFX.Play` | yes | only if the sound prefab has a `ZNetView` (data.md says the boss ZSFX prefabs do; to confirm in game) | ≈ 0 |
| `AnimationEffect` sounds (animation events) → `ZSFX.Play` | yes | yes | clip event time |
| Projectiles and networked spawns | yes | yes, after ZDO replication | after the hit |

- `SetTrigger` is a routed RPC to `ZNetView.Everybody` carrying the trigger name
  (ZSyncAnimation.cs:148-151, 217-220; ZRoutedRpc.cs:130-137, 155-163). The sender runs it locally
  at once. Every other client with the boss loaded runs it on arrival.
- The RPC also carries non-attack triggers (`attack_abort`, `detach`, staggers), so it is filtered
  by name. Takeoff and landing use the raw `Animator.SetTrigger` and are **not** networked
  (Character.cs:4394-4405). Moder's flying state is the ZDO animator bool `flying`.
- **"Is the boss targeting me?"** is owner-only (`MonsterAI.GetTargetCreature`,
  MonsterAI.cs:807-810). Non-owners have only `Player.IsTargeted()` ("something targets me",
  Player.cs:6768-6810), ZDO `LookTarget` (the target's eye point, only if the rig has a head bone,
  CharacterAnimEvent.cs:465-504), and the boss's facing.
- **"Boss fight active":** `EnemyHud.instance.ShowingBossHud()` / `GetActiveBoss()`
  (EnemyHud.cs:256-278). That is a boss within 100 m that is alerted (EnemyHud.cs:56, 101-114),
  and it works on every client.

**Research conclusion:** `RPC_SetTrigger` is the one attack signal that is the same in solo and
multiplayer. Keyed on (boss prefab, trigger name), it covers every attack of every boss. The
offline data maps each pair to the item, so the item's ranges, cooldown, gates and geometry are
known on any client. The decision is still open (§9).

## 4. Boss attack catalogue

Times are seconds after the trigger. **Hit** is the first damage event; a list means several hits.
**Cue** is the boss's own earliest wind-up sound (A = animation event, heard by every client;
S = item start effect, created on the owner, ≈ 0 s). Range, CD and HP gate are the AI's.
**Area** is what the visuals would draw. **Response** is the counterplay from the wikis
(web.md §3), still to be agreed (§9). Full per-item data: data.md §7.

### Eikthyr (`Eikthyr`, 500 HP)

| Trigger | Attack | Hit | Cue | Range / CD | Area | Response |
|---|---|---|---|---|---|---|
| `attack1` | Antler | 0.60 | S `sfx_eikthyr_attack` | ≤4 / 5 s | cone 4.5 m, 25° | block / step aside |
| `attack2` | Charge (lightning) | 1.56 | S `sfx_eikthyr_attack` | ≤15 / 25 s, prioritized | cone 20 m, 45° | sidestep out of the cone |
| `attack_stomp` | Stomp | 2.89 | **none** (start sound disabled) | ≤6 / 40 s | circle r 10 m, 3 m ahead | run out |

### The Elder (`gd_king`, 2,500 HP)

| Trigger | Attack | Hit | Cue | Range / CD | Area | Response |
|---|---|---|---|---|---|---|
| `shoot` | Vine volley (25 shots) | 1.30 | S `sfx_gdking_shoot_start` | 15–50 / 6 s | line at target | break line of sight (pillars) |
| `stomp0`/`stomp1` | Stomp | 2.00 / 2.09 | S `sfx_gdking_stomp` | ≤3 / 5 s | circle r 5 m, 3 m ahead | back off / block |
| `spawn` | Roots (15 within 15 m of target) | 1.46 | S `sfx_gdking_spawn` | ≤30 / 25 s | circle r 15 m at target | move away (adds) |
| `scream` | Scream | 0.59 | — | 10–50 / 30 s | none | (nothing to do) |

### Bonemass (`Bonemass`, 5,000 HP)

| Trigger | Attack | Hit | Cue | Range / CD | Area | Response |
|---|---|---|---|---|---|---|
| `aoe` | Poison cloud (15 s) | 3.30 | S `fx_Bonemass_aoe_start` | ≤15 / 30 s | circle r 9 m in front | get behind / run out (unblockable) |
| `punch0`/`punch1` | Punch | 1.13 | S `sfx_Bonemass_punch_start` | ≤8 / 7 s | cone 8.5 m, 66° | parry / roll |
| `spawn` | Throw (4 Skeletons/Blobs) | 3.11 | S `sfx_Bonemass_throw_start` | ≤30 / 50 s | landing point | kill adds |

`sfx_Bonemass_spawn_draugr_start` is unused in 1.0.16: its item isn't in his inventory.

### Moder (`Dragon`, 7,500 HP)

| Trigger | Attack | Hit | Cue | Range / CD | Area | Response |
|---|---|---|---|---|---|---|
| `attack_breath` | Cold breath (grounded) | 1.37 | S `sfx_dragon_coldbreath_start` | 5–20 / 8 s | line 30 m, 10° | sidestep out of the line |
| `attack_iceball` | Ice barrage (flying, 16 shots) | 0.89 | S `sfx_dragon_coldball_start` | 5–25 / 8 s | at target | keep moving / cover |
| `attack_bite` | Bite | 0.99 | S `sfx_dragon_melee_start` | ≤7 / 30 s | cone 8 m, 20° | block / sidestep |
| `attack_claw_left`/`_right` | Claw | 1.57 / 1.60 | S `sfx_dragon_melee_start` | ≤10 / 30 s | cone 12 m, 50° | parry / step back |
| `attack_taunt` | Scream | 1.96 (no hit) | — | 10–50 / 30 s | none | (nothing to do) |

Takeoff and landing are local, un-networked triggers; the `flying` ZDO bool shows the state.
`sfx_dragon_coldball_launch` and `sfx_dragon_scream` play at the hit, not before.

### Yagluth (`GoblinKing`, 10,000 HP)

| Trigger | Attack | Hit | Cue | Range / CD | Area | Response |
|---|---|---|---|---|---|---|
| `beam` | Fire beam (20 shots, tracks) | 2.02 | A `fx_goblinking_vo_beamstart` @1.13 | 10–40 / 15 s | line 40 m | sidestep / break line of sight |
| `cast1` | Meteors (10, r 5 m each, within 15 m) | 1.91 | A `fx_goblinking_vo_meteors1` @0.59 | ≤30 / 25 s | circle r 15 m | keep moving |
| `nova` | Nova + burning ground (10 s) | 2.81 | A `fx_goblinking_vo_nova` @0.67 | ≤10 / 20 s | circle r 8 m 4.5 m ahead; ground r 10 m | run out |
| `taunt` | Taunt | 1.73 (no hit) | — | 10–50 / 60 s | none | (nothing to do) |

### The Queen (`SeekerQueen`, 12,500 HP)

| Trigger | Attack | Hit | Cue | Range / CD | HP gate | Area | Response |
|---|---|---|---|---|---|---|---|
| `attack_pierce` | Pierce AoE | 0.86, 0.96 | A `sfx_HiveQueen_pierce` @0 | ≤4 / 4 s | — | circle r 4.5 m | back out |
| `attack_slash1..3` | Slap | ≈1.05 | A `sfx_HiveQueen_slash` @0 | ≤9 / 4 s | — | cone 10 m, 145° | get behind / roll |
| `attack_bite` | Bite | 1.82 | A `sfx_HiveQueen_bite` @0 | ≤10 / 10 s | ≤70% | cone 10 m, 25° | roll / move to the side |
| `attack_rush` | Rush | 1.44 | A `sfx_HiveQueen_rush` @0 | 5–25 / 25 s | ≤60% | cone 8 m, 120° along her path | sidestep |
| `attack_spit` | Acid spit (20 shots, broods) | 1.48 | A `sfx_HiveQueen_acitspit` @0 | 5–25 / 20 s | ≤80% | at target | move sideways |
| `attack_call` | Call (adds from holes within 30 m) | 2.92 | A `sfx_HiveQueen_callout` @0 | ≤25 / 60 s, prioritized | ≤99% | r 30 m | kill adds |
| `attack_teleport` | Teleport (up to 200 m) | 2.54 | A `sfx_HiveQueen_burrow` @0 | / 60 s | ≤90% | — | find her |

Possible vanilla bug: Slap sends `attack_slash0..3`, but her animator only has `slash1..4`, so 1
in 4 slaps has no transition (data.md §3). `sfx_HiveQueen_backslam` is unused.

### Fader (`Fader`, 25,000 HP)

| Trigger | Attack | Hit | Cue | Range / CD | HP gate | Area | Response |
|---|---|---|---|---|---|---|---|
| `attack_flamebreath` | Flame breath (burning 14 s) | 2.34 | A `sfx_fader_firebreath_in` @0.42 | 2–20 / 25 s | 5–85% | box 3 × 39 m ahead | get behind / out of the line |
| `attack_Fissure` | Fissure (12–16 AoEs r 11 m on the target, 0.45 s apart, each arms after 4 s) | 2.88 | A `sfx_fader_fissure_footslide` @1.20 (S `sfx_dragon_melee_start` ≈0) | ≤40 / 30 s; 20 s below 35% | 35–85%; Intense 0–35% | trail on the player | keep moving |
| `attack_WallOfFire` | Wall of fire (12 fires in a ring r 8 m around the target) | 1.43 | A `sfx_fader_firewall_start` @1.08 | ≤40 / 60 s | 15–90% | ring r 8 m | leave through the gap |
| `taunt` | Meteors (10, r 5 m, burning 20 s) | 1.17 | A `sfx_fader_meteor_start` @0.03 | ≤30 / 25 s; 18 s below 25% | 25–100%; Intense 0–25% | circle r 15 m | keep moving |
| `attack_roar` | Roar (8 orbs → Charred adds) | 1.53 | A `sfx_fader_charredsummon_roar` @0.11 | ≤100 / 45 s; 26 s below 35% | 35–55%; Intense 0–35% | — | kill adds |
| `attack_Spin` | Spin | 1.30 | A `sfx_fader_spin` @0.09 | ≤8 / 20 s | — | circle r 8.5 m | back out |
| `attack_bite` | Bite | 1.27 | A `sfx_fader_bite_pre` @0.16 | ≤9 / 3 s | — | cone 10 m, 40° | roll / parry |
| `attack_ClawL`/`ClawR` | Claw | 1.24 | A `sfx_fader_claw_pre` @0.56 / 0.38 | ≤9 / 3 s | — | cone 10 m, 65° | roll / parry |

Natural phase callouts are at 85%, 55%, 35% and 25%, where abilities unlock or speed up.
`sfx_fader_fissure` plays at the hit; the earlier cue is `sfx_fader_fissure_footslide`.

### Kall Fimbulbringer (`FrozenKing` → `FrozenKing_p2` → `FrozenKing_p3`, 10,000 / 7,000 / 30,000 HP)

**Live in 1.0.16:** `DN_Bossroom` is an enabled Deep North location (3 per world), with a
sleeping `FrozenKing` and an altar that respawns him 12 s after 3 Malicious Blood; all phases are
network prefabs; English strings exist (data.md §2).

- **Phase 1** (`FrozenKing`)

  | Trigger | Attack | Hit | Range / CD | HP gate | Area | Response |
  |---|---|---|---|---|---|---|
  | `attack_chain_slamL`/`R` | Chain slam | 1.43 | ≤12 / 3 s | 50–100% | cone 10 m, 40° | sidestep / roll |
  | `attack_chain_slamL_double` / `R_double` | Double slam | 1.43, 2.76 | 1–12 / 3 s | L ≤75%, R ≤50% | cone 10 m, 40° | sidestep / roll |
  | `attack_chain_whirl` | Chain whirl | 1.23, 2.45 | ≤8 / 10 s | — | circle r 8.5 m | back out |
  | `attack_rush` | Chain rush | 2.02 | 1–12 / 10 s | — | cone 10 m, 70° | sidestep |
  | `attack_double_sweep` | Double sweep | 0.89, 2.03 | 2–12 / 3 s | — | cone 10 m, 90° | sidestep / roll |

- **Phase 2** (`FrozenKing_p2`) has no attacks. Seven boss aspects spawn in a chain, with the
  original animators, triggers and wind-ups, so the earlier bosses' warnings can cover them.
- **Phase 3** (`FrozenKing_p3`)

  | Trigger | Attack | Hit | Range / CD | HP gate | Area | Response |
  |---|---|---|---|---|---|---|
  | `attack_spike_rain` | Spike rain (20–25 spikes r 5 m within 30 m) | 3.35 | ≤20 / 25 s | ≤35% | circle r 30 m | keep moving |
  | `attack_punch_aoe` | Punch AoE | 1.24, 2.15, 3.08 | ≤8 / 8 s | — | circle r 8.5 m | back out |
  | `attack_chain_flurry` | Chain flurry | 2.46 (×4) | 2–12 / 3 s | ≤50% | cone 5 m, 80° | step back |
  | `attack_chain_whirl` | Chain whirl | 1.23, 2.45 | ≤8 / 20 s | ≥75% | circle r 8.5 m | back out |
  | `spawn` | Tendrils (9 within 15 m) | 1.01 | ≤30 / 45 s | — | — | kill adds / break line of sight |
  | `attack_chain_slamL/R_double`, `attack_double_sweep` | as phase 1 | | | | | |

- Phase 1 and 3 cues are animation events at 0.02–0.20 s (every client). For example,
  `sfx_frozenking_frozentwirl_charge` @0.15 and `sfx_frozenking_spikerain_chain` @0.19.

**Sounds shared between bosses:** `sfx_dragon_melee_start` is the start sound for Moder, Fader
and Queen melee. `sfx_goblinking_beam` is also the Queen's spit. Identify the boss from the
character, not the sound name.

## 5. Vanilla HUD and messages

- `MessageHud.ShowMessage(type, text, …)` (MessageHud.cs:156). Center messages share one slot
  (a new one overwrites it), fade over 4 s, and are used by boss alert and death messages. There is
  no message sound.
- The boss health bar's position comes from its prefab (EnemyHud.cs:130-135, 234); its rect must
  be read in game so warnings stay clear of it.
- Vanilla sounds can be played by instantiating a ZSFX prefab without a `ZNetView`
  (code.md §6).

## 6. Offline data tools

`tools/` (Python, gitignored `.venv` with `UnityPy` and `TypeTreeGeneratorAPI`):
- `dump_boss_attacks.py` → `gamedata/boss_dump.json`: bosses, items, attacks, spawned objects,
  animator triggers and clip events. Type trees are generated from the game's own DLLs
  (`gamedata/Managed/`), and every object is decoded in a mode that fails unless it consumes
  exactly its raw byte length.
- `boss_report.py` (markdown tables), `loc_en.py` (English strings), `find_refs.py` (what uses a
  prefab), helpers `vhassets.py` and `anim_timing.py`.
- Inputs in `gamedata/`, copied from the rig: SoftRef `manifest_extended`, bundles `c4210710`
  (characters), `86c3d76e` (scripts), `d59cfac`, `e06fccc7`, `cf482a9b` (locations),
  `17245031` (main scene), `resources.assets` and `Managed/*.dll`.

## 7. Prior art and conventions

- **No Valheim mod warns about boss attacks.** The nearest are j1gA-ExtendedBosses and
  coemt-EpicBossFights, which add phases or attacks and could change triggers, plus spawn and
  kill announcers (web.md §1.4).
- **DBM conventions** to mirror (web.md §2):
  - **Special warnings:** 35 pt, outlined, `#FFB300`, 75 px above centre. Shown for 1.5 s plus a
    fade, two lines at most. Five levels, each with its own sound and a full-screen tint flash.
  - **Announces:** 20 pt, 260 px above centre, 3 lines. Turquoise, yellow, orange or red by kind.
  - **Timer bars:** small at the top right, enlarging below centre under 10 s, coloured by type
    (add, AoE, targeted, interrupt, role, phase, important).
  - **Per-ability options:** announce, special warning + sound, timer + colour + countdown voice.
- **BigWigs** names its sound slots by severity (Info, Alert, Alarm, Warning, Long), which is
  simpler than DBM's numbered levels.
- **ConfigurationManager** (web.md §4): one level of grouping (category → rows), ordered by
  `Order`, `IsAdvanced` to hide detail. Zero-padded category prefixes keep bosses in game order.
  Most players run shudnal's fork, which honours the same attributes.

## 8. Still to check in game (runtime probes)

1. Offline wind-ups against in-game timing (trigger → `Hit` event), per boss.
2. On a non-owner: `RPC_SetTrigger` arrival and its lag, and `RightItem` order.
3. Whether the boss start-effect ZSFX prefabs carry a `ZNetView` at runtime (non-owner sound cue).
4. The boss health bar's on-screen rect.
5. Whether boss rigs write `LookTarget` (who the boss is targeting, for non-owners).
6. What vanilla does with the Queen's `attack_slash0`.

## 9. Decisions

Agreed so far:
- **Name:** Forewarned (2026-10-05).
- **Rig:** the same SSH target and r2modman Default profile as Earshot, kept in gitignored
  scripts and written as `<rig>` in tracked files.
- **Signal (2026-10-05): the animator trigger.** A Harmony postfix on
  `ZSyncAnimation.RPC_SetTrigger(long, string)` drives every warning, keyed on (boss prefab,
  trigger name). The same code runs solo and in multiplayer and fires at the start of the wind-up.
  Modules also claim Kall's phase-2 aspect prefabs. No sound hook and no `StartAttack` hook. A debug
  option logs triggers seen on bosses that no module maps, to catch renamed animations.
- **Who gets a warning (2026-10-05): tiered by where you stand.** When the trigger fires, the
  local player is tested against the attack's area (cone, circle, line, ring) from the boss's
  position and facing, with a margin. Inside or near it: the special warning. Outside: an announce
  line, or nothing for minor attacks. Attacks that pick a target (fissure, meteors, wall of fire,
  roots) count as aimed at you if you're within their range and the boss faces you, or you're the
  only player in range. Each ability has an "always warn" option that skips the test. Solo, you're
  always the target, so the facing guess matters only in multiplayer.
- **Urgency (2026-10-05): three levels, named by severity.**
  - **Danger:** special warning, alarm sound, screen-edge flash. Default for positional and
    unblockable attacks (fissure, flame breath, poison cloud, nova, stomp, spike rain…).
  - **Caution:** a smaller special warning, a soft sound, no flash. Default for routine melee
    (bites, claws, slaps, slams), with the warning **off** by default.
  - **Info:** an announce line, no sound. Default for adds, phase changes, teleport, takeoff and
    landing.

  Each ability has a default level that can be changed in F1.
- **Layout (2026-10-05): DBM classic.** The special warning sits just above the crosshair with its
  countdown bar directly under it; announce lines sit under the boss health bar. This keeps clear
  of the crosshair, the character, vanilla's centre message (MessageHud) and Earshot's
  bottom-centre captions. Every element has position and scale options in F1.
- **Colours (2026-10-05): a severity ramp.** Danger red-orange `#FF5A36`, Caution yellow
  `#FFD23F`, Info sky blue `#7EC8FF`. They differ in brightness as well as hue, so they stay apart
  under deuteranopia, protanopia and tritanopia (checked against the Machado 2009 simulation).
  Colour is never the only signal: Danger also has a ⚠ sprite, larger text and the edge flash.
  All colours are editable in F1 as advanced settings.
- **Timing and stacking (2026-10-05): tied to the wind-up.** A special warning shows from the
  trigger until the hit lands plus 0.5 s, then fades over 0.3 s, with a 1.2 s minimum. At most two
  special-warning lines, newest on top. A Caution never evicts a Danger (a new Caution is dropped
  if both slots hold Danger). The same ability firing again replaces its own line. A Danger
  starts with one 0.4 s screen-edge pulse. Lingering ground hazards are not kept as text.
- **World visuals (2026-10-05): ground outline plus a path arrow.** During the wind-up the
  attack's area is outlined on the ground from the decoded data: cone (range, angle), circle
  (radius, forward offset), line (box), ring (radius around the target). Attacks that land on the
  player rather than near the boss (meteors, fissure, spike rain) get a circle at the player. A
  white arrow from the player's feet shows the way out. Both clear when the hit lands. Lingering
  hazards (burning ground, fissure pools, poison cloud) are not outlined. This is the per-ability
  "visual" toggle, on by default for Danger only.
- **Alert sounds (2026-10-05): our own, synthesised in code at startup.** Danger: a short low horn
  blast (~0.4 s). Caution: a soft two-note chime. Info: silent. No audio files, no licensing.
  Default volume 0.7 with its own F1 slider, scaled by the game's master volume but not the SFX
  slider. Sound is on by default for Danger and Caution (Caution warnings themselves are off by
  default). Per-ability sound toggles, a test button per level in F1, and the same ability never
  replays its sound within 1 s.
- **Abilities, levels and wording (2026-10-05): as proposed; full table in §10.**
- **F1 layout (2026-10-05): a section per boss, plain rows.** Sections in game order: `00 General`
  (master switch, warn only during a boss fight, debug log of unmapped triggers), `01 Display`
  (positions, scale, durations, edge flash; colours as advanced), `02 Sounds` (volume, a test
  button per level), then `03 Eikthyr` … `10 Kall Fimbulbringer`. Each boss section starts with an
  `Enabled` switch, then three rows per ability kept together with `Order`
  (`Flame breath: warning` / `: sound` / `: visual`). The level and the always-warn override are
  advanced rows. Plain entries work in F1, r2modman's config editor and a hand-edited `.cfg`.
- **Modules and data (2026-10-05): the module declares, the game supplies the numbers.** A boss
  module names each ability's item prefab (`Fader_Flamebreath`) and triggers, plus our choices
  (level, wording keys, response, shape kind). Cooldown, health gates, range and AoE geometry are
  read from the boss's own inventory items when it appears (every client has them); the wind-up
  is read once from the clip's `Hit`/`OnAttackTrigger` event and cached. The offline research
  values are the fallback and the test fixtures. A pure-model **tracker** records every trigger
  seen per boss and ability, plus the boss's health percentage, so later "next X in ~N s" timers
  only add a reader. Boss-specific logic (phase announces, Moder's flying state) lives in small
  optional overrides on the module class.
- **Earshot (2026-10-05): independent for now.** No coupling: each mod works alone and both run
  together; the agreed layout keeps their HUDs apart. Earshot may still caption boss sounds that
  Forewarned has turned into warnings. A later, optional duplicate-suppression check (found by
  plugin GUID, no hard dependency) is to be reconsidered after multiplayer, once both have been
  seen in a real fight.

Open, to settle in the brainstorm before any design is written:
- The first milestone's boss list and its definition of done.

## 10. Ability defaults and wording (agreed 2026-10-05)

Title = the attack's plain name in capitals; the action starts with a verb and says where to go;
a distance only where the safe distance is a fixed number. Every string is a translation key.
**Danger:** warning, sound and ground outline on. **Caution (off):** defined, off until enabled in
F1. **Info:** announce line only. **None:** no warning. Wind-up: seconds to the first hit (§4).

**Eikthyr**

| Attack | Level | Wind-up | Warning | Area drawn | Notes |
|---|---|---|---|---|---|
| Antler swipe | Caution (off) | 0.60 | **ANTLER** / Block or step aside |  |  |
| Lightning charge | Danger | 1.56 | **CHARGE** / Sidestep out of the line | cone 20 m × 45° |  |
| Stomp | Danger | 2.89 | **STOMP** / Run out, 10 m | circle 10 m | No vanilla sound during this 2.9 s wind-up: the warning is the only cue. |

**The Elder**

| Attack | Level | Wind-up | Warning | Area drawn | Notes |
|---|---|---|---|---|---|
| Vine volley | Danger | 1.30 | **VINE VOLLEY** / Hide behind a pillar | line to you | Only used at 15–50 m. |
| Stomp | Caution (off) | 2.00 | **STOMP** / Back away | circle 5 m |  |
| Roots | Info | 1.46 | announce: "Roots rising: move away" | circle 15 m on you |  |
| Scream | None | — | — |  | No hit. |

**Bonemass**

| Attack | Level | Wind-up | Warning | Area drawn | Notes |
|---|---|---|---|---|---|
| Poison cloud | Danger | 3.30 | **POISON CLOUD** / Get behind Bonemass | circle 9 m in front | Unblockable; lasts 15 s. |
| Punch | Caution (off) | 1.13 | **PUNCH** / Parry or roll | cone 8.5 m × 66° |  |
| Throw | Info | 3.11 | announce: "Skeletons and Blobs incoming" |  |  |

**Moder**

| Attack | Level | Wind-up | Warning | Area drawn | Notes |
|---|---|---|---|---|---|
| Cold breath | Danger | 1.37 | **COLD BREATH** / Get out of the line | line 30 m | Grounded only. |
| Ice barrage | Danger | 0.89 | **ICE BARRAGE** / Keep moving | circle on you | Flying only. |
| Bite | Caution (off) | 0.99 | **BITE** / Block or sidestep | cone 8 m × 20° |  |
| Claw left / right | Caution (off) | 1.57 | **CLAW** / Parry or step back | cone 12 m × 50° |  |
| Takes off / lands | Info | — | announce: "Moder takes off · Moder lands" |  | From the networked "flying" state, not a trigger. |
| Scream | None | — | — |  | No hit. |

**Yagluth**

| Attack | Level | Wind-up | Warning | Area drawn | Notes |
|---|---|---|---|---|---|
| Fire beam | Danger | 2.02 | **FIRE BEAM** / Sidestep or hide behind a pillar | line 40 m | Only used at 10–40 m; tracks you. |
| Meteors | Danger | 1.91 | **METEORS** / Keep moving | circle 15 m on you |  |
| Nova | Danger | 2.81 | **NOVA** / Run out, 10 m | circle 10 m | Burning ground for 10 s. |
| Taunt | None | — | — |  | No hit. |

**The Queen**

| Attack | Level | Wind-up | Warning | Area drawn | Notes |
|---|---|---|---|---|---|
| Rush | Danger | 1.44 | **RUSH** / Sidestep | cone 8 m × 120° | Below 60%. |
| Acid spit | Danger | 1.48 | **ACID SPIT** / Move sideways | line to you | Below 80%; spawns broods. |
| Slap | Caution (off) | 1.05 | **SLAP** / Get behind her or roll | cone 10 m × 145° |  |
| Pierce | Caution (off) | 0.86 | **PIERCE** / Back out, 5 m | circle 4.5 m |  |
| Bite | Caution (off) | 1.82 | **BITE** / Roll to the side | cone 10 m × 25° | Below 70%. |
| Call | Info | 2.92 | announce: "Seekers incoming: kill the adds" |  | Below 99%. |
| Teleport | Info | 2.54 | announce: "The Queen burrows: find her" |  | Below 90%. |

**Fader**

| Attack | Level | Wind-up | Warning | Area drawn | Notes |
|---|---|---|---|---|---|
| Flame breath | Danger | 2.34 | **FLAME BREATH** / Get behind Fader | line 39 m | 5–85%. |
| Fissure | Danger | 2.88 | **FISSURE** / Keep moving | circle 11 m on you | Below 85%; every 20 s below 35%. |
| Wall of fire | Danger | 1.43 | **WALL OF FIRE** / Leave through the gap | ring 8 m around you | 15–90%. |
| Meteors | Danger | 1.17 | **METEORS** / Keep moving | circle 15 m on you | Every 18 s below 25%. |
| Spin | Danger | 1.30 | **SPIN** / Back out, 9 m | circle 8.5 m |  |
| Roar | Info | 1.53 | announce: "Charred Warriors incoming" |  | Below 55%. |
| Bite | Caution (off) | 1.27 | **BITE** / Parry or roll | cone 10 m × 40° |  |
| Claw left / right | Caution (off) | 1.24 | **CLAW** / Parry or roll | cone 10 m × 65° |  |

**Kall Fimbulbringer**

| Attack | Level | Wind-up | Warning | Area drawn | Notes |
|---|---|---|---|---|---|
| Chain whirl | Danger | 1.23 | **CHAIN WHIRL** / Back out, 9 m | circle 8.5 m | Phase 1; phase 3 above 75%. Hits twice. |
| Chain rush | Danger | 2.02 | **CHAIN RUSH** / Sidestep | cone 10 m × 70° | Phase 1. |
| Punch AoE | Danger | 1.24 | **GROUND PUNCH** / Back out, 9 m | circle 8.5 m | Phase 3. Hits three times. |
| Spike rain | Danger | 3.35 | **SPIKE RAIN** / Keep moving | circle 30 m on you | Phase 3, below 35%. |
| Chain flurry | Danger | 2.46 | **CHAIN FLURRY** / Step back | cone 5 m × 80° | Phase 3, below 50%. Four hits. |
| Chain slam / double slam | Caution (off) | 1.43 | **CHAIN SLAM** / Sidestep | cone 10 m × 40° | Double slams hit again at 2.76. |
| Double sweep | Caution (off) | 0.89 | **DOUBLE SWEEP** / Sidestep or roll | cone 10 m × 90° |  |
| Tendrils | Info | 1.01 | announce: "Tendrils: kill them or break line of sight" |  | Phase 3. |

**Phase and pull announces (Info)**

| Boss | When | Announce |
|---|---|---|
| All bosses | Pull | “{Boss} engaged” when the boss bar appears. |
| Fader | 85%, 55%, 35%, 25% | “Fader 85%: Fissure and Flame breath”, “55%: adds”, “35%: faster fissures and adds”, “25%: faster meteors” |
| The Queen | 99%, 90%, 80%, 70%, 60% | “The Queen 90%: Teleport”, and so on as each ability unlocks |
| Kall Fimbulbringer | 75%, 50% (phase 1); phase 2; 75%, 50%, 35% (phase 3) | “Phase 2: kill the aspects”, “Kall 35%: Spike rain”. Each aspect is announced as it spawns and uses its original boss’s warnings. |
