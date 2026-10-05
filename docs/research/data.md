# Boss attack data — Valheim 1.0.16 (Unity 6000.0.75f1)

Extracted on 2026-10-05 from the SoftRef bundles for Forewarned (working name Gjallarhorn). This is read-only research.

## 0. How it was extracted (and how to re-run)

Run everything from the repo root:

```
.venv/bin/python tools/dump_boss_attacks.py   # -> gamedata/boss_dump.json (everything below, raw)
.venv/bin/python tools/loc_en.py [substr..]   # -> gamedata/loc_en.json (English strings)
.venv/bin/python tools/boss_report.py > x.md  # markdown tables (sections 6, 7, 8 of this file)
.venv/bin/python tools/find_refs.py <prefab..># reverse references (which item/clip/component uses a prefab)
```

- **Decoder.** I did not hand-write the MonoBehaviour decoder. `TypeTreeGeneratorAPI` (pip, now in `.venv`) builds Unity type trees from the game's own managed DLLs, and UnityPy uses them. The DLLs were copied from the rig's `valheim_Data/Managed` to `gamedata/Managed/`. Every MonoBehaviour is read with `read_typetree(check_read=True)`, which raises unless the decode uses up exactly the object's raw byte length. All boss, item, projectile, AoE, SpawnAbility, ZSFX, OfferingBowl, Location and LocationList objects decoded with no errors (0 `DECODE_ERROR`). As a negative control, decoding Moder's Humanoid with the MonsterAI layout does raise. Strings, names and values all look sane, for example Moder `$enemy_dragon`, 7500 HP, `boss_moder`.
- **Animator.** AnimatorControllers are the runtime-compiled ones (`m_TOS` hash→name, state machine constants, AnyState transitions). AnimationClip events are read with UnityPy. Wind-up is measured from the trigger to the first `Hit`/`OnAttackTrigger` event (both call `Character.OnAttackTrigger`, CharacterAnimEvent.cs:248-256). The time is clip time divided by (state `m_Speed` × animator speed). Animator speed starts at 1 and changes at each `Speed(float)` event, which sets `Animator.speed` (CharacterAnimEvent.cs:215). Every attack transition has `m_TransitionOffset` = 0, so no offset correction was needed. Transition blend time (0.1–0.25 s) is not added.
- **Files fetched from the rig** (all under the gitignored `gamedata/`):
  - bundles `86c3d76e` (454 MonoScripts), `d59cfac` (LocationLists), `e06fccc7` (DN_Bossroom location and the gate altar), `cf482a9b` (Vegvisir_DNBoss) and `17245031` (main.unity scene: ZoneSystem, ZNetScene);
  - `gamedata/Managed/*.dll`.

## 1. Boss overview

| prefab | m_name → English | HP | bossOrder | m_bossEvent | m_defeatSetGlobalKey | MonsterAI minAttackInterval | starts asleep | drops |
|---|---|---|---|---|---|---|---|---|
| Eikthyr | $enemy_eikthyr → Eikthyr | 500 | 1 | boss_eikthyr | defeated_eikthyr | 2.0 | yes | TrophyEikthyr, HardAntler×3 |
| gd_king | $enemy_gdking → The Elder | 2500 | 2 | boss_gdking | defeated_gdking | 1.0 | yes | TrophyTheElder, CryptKey |
| Bonemass | $enemy_bonemass → Bonemass | 5000 | 3 | boss_bonemass | defeated_bonemass | 0 | yes | TrophyBonemass, Wishbone |
| Dragon | $enemy_dragon → Moder | 7500 | 4 | boss_moder | defeated_dragon | 2.0 | no (flying 1) | TrophyDragonQueen, DragonTear×10 |
| GoblinKing | $enemy_goblinking → Yagluth | 10000 | 5 | boss_goblinking | defeated_goblinking | 0 | yes | TrophyGoblinKing, YagluthDrop×3 |
| SeekerQueen | $enemy_seekerqueen → The Queen | 12500 | 6 | boss_queen | defeated_queen | 0.5 | no | TrophySeekerQueen, QueenDrop×5 |
| Fader | $enemy_fader → Fader | 25000 | 7 | boss_fader | defeated_fader | 0.5 | no | TrophyFader, FaderDrop×5 |
| FrozenKing (phase 1) | $enemy_frozenking → Kall Fimbulbringer | 10000 | 0 | boss_frozenking | defeated_frozenking | 0.5 | yes (wakeupRange 15) | none; its death spawns FrozenKing_p2 + FrozenKing_P2_Projectile_Eikthyr |
| FrozenKing_p2 | $enemy_frozenking → Kall Fimbulbringer | 7000 | 0 | boss_frozenking | (empty) | 0.5 | no; speed 0, **no attack items** | none; its death spawns FrozenKing_p3 |
| FrozenKing_p3 | $enemy_frozenking_p3 → Kall Fimbulbringer | 30000 | 8 | boss_frozenking | defeated_frozenking_p3 | 0.5 | no | FrozenKingDrop ($item_frozenking_drop "Sacrificial Blood"), CrownJewel |

Other `m_boss = true` Humanoids:

- **Hive** ($enemy_hive) and **TheHive** ($enemy_thehive) are in ZNetScene, but neither name has an English string. I did not look for anything that spawns them. They are probably unused or test content.
- **Phase-2 aspects** (Aspect_Eikthyr, Aspect_Elder, Aspect_Bonemass, Aspect_Moder, Aspect_Yagluth, Aspect_SeekerQueen, Aspect_Fader) have `m_boss = false`. They have their own localised names ("Aspect of the Lightning Stag" and so on) and reuse the original bosses' animators with `aspect_*` attack items. Section 7 has their tables.

`m_bossEvent` is the event/music key. Alert and spawn messages are in each boss's detail header in section 7. Examples: Fader alertedMessage "The Emerald Flame ignites", FrozenKing phase-1 alertedMessage "His hatred corrupts all!".

## 2. Kall Fimbulbringer (FrozenKing): reachable in 1.0.16 — yes

All the evidence points the same way: the fight is live content.

1. **World generation.**
   - `ZoneSystem.m_locationLists` in the `main.unity` scene (bundle 17245031) lists `_LocationList_DeepNorth` along with cp1, MountainCaves, Mistlands, Hildir and Ashlands.
   - That list has a `DN_Bossroom` entry:
     - enabled, quantity 3, `m_prioritized` 1, biome 64 (DeepNorth), group `dn_boss`, minAltitude 80;
     - prefab soft-ref asset id 2e59045a8e427debbbe65e21adb52dce = `Assets/world/Locations/DeepNorth/DN_Bossroom.prefab` in bundle e06fccc7.
   - `WorldGenerator` generates `Heightmap.Biome.DeepNorth` (WorldGenerator.cs:806, 1040).
2. **DN_Bossroom location contents**:
   - `Location`: discoverLabel `$hud_pin_dnboss` "Aesir Passage", interior environment "DN_Bossroom";
   - `offeraltar_FrozenKing`: OfferingBowl `$piece_offerbowl_frozenking` "Strange Bowl". 3× HatefulBlood ("Malicious Blood") sets global key `LastBossGate_Open`, which opens the ice wall and the Gateway. The Gateway is a Teleport `$location_dnbossroomnew` "The Prison" and is inactive until the key is set.
   - **A placed `FrozenKing` creature**: full Humanoid and MonsterAI, `m_sleeping` 1, wakeupRange 15, CharacterAnimEvent loop sound `sfx_frozenking_idle_chained_loop`.
   - `offeraltar_FrozenKing_bossroom`: OfferingBowl. 3× HatefulBlood spawns `m_bossPrefab = FrozenKing` after `m_spawnBossDelay` 12 s, with prespawn sound `sfx_deepnorth_gate_open_loop`.
   - `RuneTablet_FrozenKing`: `$lore_frozenking` "HALT THE INVASION".
3. **Network registration.** ZNetScene `m_prefabs` (main.unity) contains:
   - FrozenKing, FrozenKing_p2 and FrozenKing_p3;
   - FrozenKing_Summon and all 7 `FrozenKing_P2_Projectile_*`;
   - all 8 `Aspect_*`;
   - offeraltar_FrozenKing_bossroom, HatefulBlood and FrozenKingDrop.
4. **Vegvisir.** `Vegvisir_DNBoss` (bundle cf482a9b) has Vegvisir `m_locations = [DN_Bossroom, pin "$hud_pin_dnboss"]`. The NorthMemorialPlace location bundle (enabled, qty 15) and the morkhalla dungeon endcap rooms depend on that bundle.
5. **Localisation.** English strings exist for:
   - `enemy_frozenking` and `enemy_frozenking_p3`: "Kall Fimbulbringer";
   - `enemy_boss_frozenking_alertmessage` "His hatred corrupts all!" and `_deathmessage` "Peace settles over the world";
   - `item_frozenking_drop` "Sacrificial Blood";
   - `ach_boss8frozenking` (achievement);
   - `location_dnbossroom` "The First Prison" and `location_dnbossroomnew` "The Prison".

   There is **no trophy prefab**: no `TrophyFrozen*` anywhere in the manifests. The phase-3 drops are FrozenKingDrop and CrownJewel.
6. **Code.** The decompiled code has no FrozenKing-specific class. The only special case is `Player.cs:5630` for `$item_frozenking_drop`. The phase logic is entirely data:
   - Phase 1 (10 000 HP) dies. Its `m_deathEffects` spawn `FrozenKing_p2` and `FrozenKing_P2_Projectile_Eikthyr`.
   - Phase 2 (7000 HP) is stationary and has no attack items. Each `FrozenKing_P2_Projectile_X` lands and spawns `FrozenKing_P2_Spawn_X`, a SpawnAbility that spawns `Aspect_X` after `m_initialSpawnDelay` 3 s with sound `sfx_frozenking_spirit_summon` (120 m).
   - Each aspect's death effects launch the next projectile: Eikthyr → Elder + Bonemass, Elder → Moder, Bonemass → Yagluth, Moder → Queen, Yagluth → Fader. Queen and Fader launch nothing.
   - Phase 2's death spawns `FrozenKing_p3` (30 000 HP). Its animator is `FrozenKing_p3_OverrideController` over FrozenKing_animator, and every clip event is the same as phase 1's.
   - I did not determine what kills phase 2.

## 3. Wind-up summary

Each attack's wind-up, warning sounds and area are in section 6, generated by `tools/boss_report.py`. The table after these notes maps every requested sound name. Times are seconds after the attack trigger.

**Read these first:**

- **startEffect timing.** `m_startEffect` (shared or attack) fires in `Attack.Update` on the first frame `InAttack()` is true (Attack.cs:530-541). That is as soon as the animator starts moving into the `attack`-tagged state, so effectively t ≈ 0. `m_triggerEffect` fires at the hit event.
- **trailStartEffect only fires on a `TrailOn` animation event** (CharacterAnimEvent.TrailOn → Humanoid.OnWeaponTrailStart → Attack.OnTrailStart). As a result:
  - Moder's breath sounds `vfx_dragon_coldbreath` and `sfx_dragon_coldbreath_trailon` fire at 1.14 s.
  - `fx_goblinking_vo_meteors2` (trailStart of GoblinKing_Meteors, Fader_Meteors and FrozenKing_SpikeRain) and `fx_goblinking_vo_nova` (trailStart of GoblinKing_Nova) **never fire through the item**: those clips have no TrailOn event. Yagluth's clips play them as `Effect` animation events instead.
- **Item effects run only on the owner.** `Attack.Start`/`OnAttackTrigger` run only on the zone owner (Humanoid.cs:547/555). The ZSFX prefabs carry ZNetView, so they replicate. `AnimationEffect.Effect`/`Attach` events (all the Fader, Queen, Yagluth and FrozenKing pre-sounds) are instantiated locally on every client, because the animation plays everywhere.
- **Trigger names.** The animator trigger is `m_attackAnimation`, with a suffix 0..n-1 if `m_attackRandomAnimations ≥ 2` or `m_attackChainLevels > 1` (Attack.cs:411-430). Triggers are sent through `ZSyncAnimation.SetTrigger` (RPC to everybody), so a client-side hook on ZSyncAnimation's trigger RPC sees every boss attack start on all clients.
- **Queen Slap bug.** SeekerQueen_Slap has `m_attackRandomAnimations` = 4, so it fires `attack_slash0`..`attack_slash3`. SeekerQueen_animator has only `attack_slash1`..`attack_slash4` parameters and AnyState transitions, so `attack_slash0` (1 in 4 picks) has no transition, and `attack_slash4` is never fired. I have not checked what happens in-game. The Aspect_SeekerQueen uses the same item and has the same problem.
- **Unused in 1.0.16:**
  - `sfx_HiveQueen_backslam`: only in clip "Attack Back Slam", which is not in SeekerQueen_animator.
  - `sfx_Bonemass_spawn_draugr_start`: only on item `bonemass_attack_spawn`, which is not in Bonemass's m_defaultItems.
  - `sfx_frozenking_spikerain_shards`: only referenced by ZNetScene.
  - Fader's `jump_forward`, `jump_left` and `jump_right` triggers: nothing in Fader's items fires them.
- **Reused sound names.** `sfx_dragon_melee_start` is also the start sound for Fader Fissure/Roar/WallOfFire and Queen Bite/Pierce/Rush/Slap. `sfx_goblinking_beam` is also the trigger sound of SeekerQueen_Spit. Identify the boss from the source character, not from the sound name.

## 4. Requested wind-up sounds → attack

**Fader**

| sound | maxDistance m | how it plays | t (s) | hit (s) |
|---|---|---|---|---|
| sfx_fader_firebreath_in | 80 | anim Effect in "Attack Flamebreath" (Fader_Flamebreath). Caption token `$enemy_father` has no string (typo). | 0.42 | 2.34 |
| sfx_fader_bite_pre | 100 | anim Effect in "Attack Bite" (Fader_Bite); also the unused jump_forward chain | 0.16 | 1.27 |
| sfx_fader_claw_pre | 80 | anim Effect in "Attack Claw L" / "Attack Claw R" | 0.56 L / 0.38 R | 1.24 |
| sfx_fader_firewall_start | 90 | anim Effect in "Attack Wall of Fire" (Fader_WallOfFire) | 1.08 | 1.43 |
| sfx_fader_meteor_start | 140 | anim Effect in "Taunt" (Fader_Meteors / _Intense use trigger `taunt`) | 0.03 | 1.17 |
| sfx_fader_fissure | 100 | **shared triggerEffect** of Fader_Fissure(_Intense), so it plays at the hit. The pre-hit cue is `sfx_fader_fissure_footslide` (80 m) at 1.20. | 2.88 | 2.88 |
| sfx_fader_spin | 80 | anim Effect in "Attack Spin" (Fader_Spin) | 0.09 | 1.30 |
| sfx_fader_charredsummon_roar | 90 | anim Effect in "Attack Roar" (Fader_Roar / _Intense) | 0.11 | 1.53 |

**Moder**

| sound | maxDistance m | how it plays | t (s) | hit (s) |
|---|---|---|---|---|
| sfx_dragon_coldbreath_start | 50 | shared startEffect of dragon_coldbreath | ≈0 | 1.37 |
| sfx_dragon_coldball_launch | 50 | shared **triggerEffect** of dragon_spit_shotgun, at launch. The pre-cue is startEffect `sfx_dragon_coldball_start` (50 m, ≈0). | 0.89 | 0.89 |
| sfx_dragon_scream | 100 | shared triggerEffect of dragon_taunt. It is the taunt itself; nothing hits. | 1.96 | (no hit) |

**Yagluth**

| sound | maxDistance m | how it plays | t (s) | hit (s) |
|---|---|---|---|---|
| fx_goblinking_vo_meteors1 | 50 | anim Effect in "Let it Rain" (GoblinKing_Meteors, trigger `cast1`) | 0.59 | 1.91 |
| fx_goblinking_vo_meteors2 | 60 | anim Effect in "Let it Rain", after the trigger. Its trailStart use is dead (see above). | 2.98 | 1.91 |
| fx_goblinking_vo_nova | 40 | anim Effect in "MagicNova" (GoblinKing_Nova) | 0.67 | 2.81 |
| fx_goblinking_vo_beamstart | 50 | anim Effect in "LazerBreath" (GoblinKing_Beam) | 1.13 | 2.02 |
| sfx_goblinking_beam | 100 (loop) | shared triggerEffect of GoblinKing_Beam: the beam itself | 2.02 | 2.02 |

**The Elder**

| sound | maxDistance m | how it plays | t (s) | hit (s) |
|---|---|---|---|---|
| sfx_gdking_shoot_start | 100 | shared startEffect of gd_king_shoot | ≈0 | 1.30 |
| sfx_gdking_scream | 100 | shared triggerEffect of gd_king_scream (the scream itself) | 0.59 | (no hit) |

**Bonemass**

| sound | maxDistance m | how it plays | t (s) | hit (s) |
|---|---|---|---|---|
| fx_Bonemass_aoe_start | 100 | shared startEffect of bonemass_attack_aoe | ≈0 | 3.30 |
| sfx_Bonemass_throw_start | 100 | shared startEffect of bonemass_attack_throw | ≈0 | 3.11 |
| sfx_Bonemass_punch_start | 100 | shared startEffect of bonemass_attack_punch | ≈0 | 1.13 |
| sfx_Bonemass_spawn_draugr_start | 100 | **unused** (only on bonemass_attack_spawn, which is not equipped) | — | — |

**The Queen**

| sound | maxDistance m | how it plays | t (s) | hit (s) |
|---|---|---|---|---|
| sfx_HiveQueen_acitspit | 40 | anim Effect in "Attack Acid Spit" (SeekerQueen_Spit) | 0.00 | 1.48 |
| sfx_HiveQueen_rush | 40 | anim Effect in "Attack Rush" | 0.00 | 1.44 |
| sfx_HiveQueen_pierce | 40 | anim Effect in "Attack Pierce" (SeekerQueen_PierceAOE) | 0.00 | 0.86 (second hit 0.96) |
| sfx_HiveQueen_backslam | 40 | **unused** (clip not in controller) | — | — |
| sfx_HiveQueen_bite / _slash / _callout / _burrow | 40 | anim Effect at 0.00 in Bite / Slash 1-4 / Call / Teleport | 0.00 | 1.82 / ~1.05 / 2.92 / 2.54 |

**Kall Fimbulbringer**

| sound | maxDistance m | how it plays | t (s) | hit (s) |
|---|---|---|---|---|
| sfx_frozenking_spikerain_chain / _iceceiling | 120 | anim Effect in "Attack Spike Rain" (FrozenKing_SpikeRain, p3) | 0.19 / 1.02 | 3.35 |
| sfx_frozenking_spikerain_flyby | 300 | spawn effect attached to each spike (SpawnAbility spawnEffects) | after 3.35 | — |
| sfx_frozenking_spikerain_explosion | 120 | spike projectile hitEffect | impact | — |
| sfx_frozenking_spikerain_shards | 120 | **unused** (only in ZNetScene) | — | — |
| sfx_frozenking_punchaoe_chain_start / whoosh_start / punch_first | 120 | anim Effect in "Attack Punch Aoe" (FrozenKing_Punch_AOE, p3) | 0.06 / 0.33 / 1.03 | 1.24, 2.15, 3.08 |
| sfx_frozenking_punchaoe_debris / whoosh_punch / punch_second / punch_third / chain_end | 120 | same clip, between and after the hits | 1.34 / 1.77 / 1.91 / 2.96 / 3.98 | |
| sfx_frozenking_chainflurry_start | 120 | anim Effect in "Attack Chain Flurry" (p3). The item attack startEffect also attaches `_fly`, `_coldvortex` and `_chain` (150 m). | 0.02 | 2.46, 2.55, 2.64, 2.73 |
| sfx_frozenking_frozentwirl_charge | 120 | anim Effect in "Attack Chain Whirl" (ChainWhirl p1 / P3_ChainWhirl). `_tornado` plays at 1.02. | 0.15 | 1.23, 2.45 |

**Eikthyr**

| sound | maxDistance m | how it plays | t (s) | hit (s) |
|---|---|---|---|---|
| sfx_eikthyr_attack | 50 | shared startEffect of Eikthyr_antler and Eikthyr_charge | ≈0 | 0.60 / 1.56 |
| fx_eikthyr_forwardshockwave (contains sfx_shockwave) | 30 | shared triggerEffect of Eikthyr_charge | 1.56 | 1.56 |
| fx_eikthyr_stomp (2× sfx_shockwave) | 30 | shared triggerEffect of Eikthyr_stomp. Its startEffect `sfx_lox_attack_stomp` is **DISABLED**, so the stomp has **no** pre-hit sound during its 2.89 s wind-up. | 2.89 | 2.89 |

Eikthyr's active animator, `eikthyrnir_animator_new` on "Visual", has no Effect events. The "OLD" animator object is inactive.

## 5. Localisation (English)

**Boss names.** Attack-item `m_name` values are all literal developer strings ("Fader Bite", "dragon breath", "slap", "spawn"…), not `$` tokens. Warning text has to be the mod's own, combined with the boss-name tokens below.

| token | English |
|---|---|
| $enemy_eikthyr | Eikthyr |
| $enemy_gdking | The Elder |
| $enemy_bonemass | Bonemass |
| $enemy_dragon | Moder |
| $enemy_goblinking | Yagluth |
| $enemy_seekerqueen | The Queen |
| $enemy_fader | Fader |
| $enemy_fader_codename | The Emerald Flame |
| $enemy_frozenking / $enemy_frozenking_p3 | Kall Fimbulbringer |
| $enemy_aspect_eikthyr | Aspect of the Lightning Stag |
| $enemy_aspect_gdking | Aspect of the Living Forest |
| $enemy_aspect_bonemass | Aspect of the Writhing Dead |
| $enemy_aspect_dragon | Aspect of the Dragon Mother |
| $enemy_aspect_goblinking | Aspect of the Twisted Soul |
| $enemy_aspect_seekerqueen | Aspect of the Crawling Matriarch |
| $enemy_aspect_fader | Aspect of the Emerald Flame |
| $enemy_hive, $enemy_thehive | (no English string) |

**Summons spawned by attacks:**

| token | English |
|---|---|
| $enemy_root | Root |
| $enemy_charred_melee_Fader | Summoned Charred Warrior (used by both Charred_Melee_Fader and Charred_Archer_Fader) |
| $enemy_babyseeker | Seeker Brood |
| $enemy_skeleton | Skeleton |
| $enemy_blob | Blob |
| Tendril | literal "Tendril", no token |

**Spawn / alert / death messages:**

| token | English |
|---|---|
| $enemy_eikthyr_alertmessage | Eikthyr summons the storm |
| $enemy_gdking_alertmessage | The Elder rises from the forest |
| $enemy_boss_bonemass_spawnmessage | The Mass is moving |
| $enemy_boss_dragon_spawnmessage | Moder is enraged |
| $enemy_boss_goblinking_spawnmessage | Yagluth's twisted soul has been summoned |
| $enemy_boss_queen_alertmessage | The Queen wants it all |
| $enemy_boss_fader_alertmessage | The Emerald Flame ignites |
| $enemy_boss_frozenking_alertmessage | His hatred corrupts all! |

Death messages are in each boss's detail header in section 7.

**ZSFX caption tokens.** Several are missing from the English CSVs: `$sfx_frozenking`, `$sfx_fader_firewallstart`, `$sfx_tentaroot_attack` and `$enemy_father`. Most boss ZSFX caption tokens are just the boss-name token, for example `$enemy_fader`.

## 6. Wind-up summary tables (generated)

Wind-up = seconds from the animator trigger (Attack.Start -> SetTrigger) to the first OnAttackTrigger/Hit animation event, including Speed() events and state speed. shared/attack startEffect fires when the animator enters the attack-tagged state (about 0 s + transition); triggerEffect fires at the hit.


**Fader** (Fader)

| attack item | trigger | wind-up s | pre-hit animation-event fx (time s) | item start / trigger effects | area |
|---|---|---|---|---|---|
| Fader_Bite | attack_bite | 1.27 | fx_Fader_AttackGlint @0.10s, sfx_fader_bite_pre @0.16s, sfx_fader_bite_snarl @0.99s, fx_Fader_Bite @1.05s | start: -; trigger: - | cone 10 m / 40 deg |
| Fader_Claw_Left | attack_ClawL | 1.236 | fx_Fader_AttackGlint @0.09s, sfx_fader_claw_pre @0.56s, fx_fader_clawattack @0.58s, sfx_fader_claw_swipe @1.06s | start: -; trigger: - | cone 10 m / 65 deg |
| Fader_Claw_Right | attack_ClawR | 1.236 | fx_Fader_AttackGlint @0.10s, sfx_fader_claw_pre @0.38s, fx_fader_clawattack @0.55s, sfx_fader_claw_swipe @0.95s | start: -; trigger: - | cone 10 m / 65 deg |
| Fader_Fissure | attack_Fissure | 2.879 | fx_fader_clawattack @1.06s, sfx_fader_fissure_footslide @1.20s | start: sfx_dragon_melee_start; trigger: sfx_fader_fissure | Fader_Fissure_Spawn spawns Fader_Fissure_AOE x12-16 within r 0; Fader_Fissure_AOE: sphere r=11 |
| Fader_Fissure_Intense | attack_Fissure | 2.879 | fx_fader_clawattack @1.06s, sfx_fader_fissure_footslide @1.20s | start: sfx_dragon_melee_start; trigger: sfx_fader_fissure | Fader_Fissure_Spawn spawns Fader_Fissure_AOE x12-16 within r 0; Fader_Fissure_AOE: sphere r=11 |
| Fader_Flamebreath | attack_flamebreath | 2.341 | sfx_fader_firebreath_in @0.42s, sfx_fader_firebreath_out @2.05s, fx_fader_firebreath @2.20s | start: -; trigger: fx_fallenvalkyrie_poisonbreath [variant=0] | Fader_Flamebreath_AOE: sphere r=4 (useTriggers: trigger box 3x5x39.45) |
| Fader_Meteors | taunt | 1.165 | sfx_fader_meteor_start @0.03s | start: -; trigger: - | spawn_fader_meteors spawns projectile_meteor_fader x10-10 within r 15; projectile_meteor_fader: aoe r 5; Fader_WallOfFire_AOE: sphere r=4 (useTriggers: trigger sphere r=2) |
| Fader_Meteors_Intense | taunt | 1.165 | sfx_fader_meteor_start @0.03s | start: -; trigger: - | spawn_fader_meteors spawns projectile_meteor_fader x10-10 within r 15; projectile_meteor_fader: aoe r 5; Fader_WallOfFire_AOE: sphere r=4 (useTriggers: trigger sphere r=2) |
| Fader_Roar | attack_roar | 1.528 | sfx_fader_charredsummon_roar @0.11s | start: sfx_dragon_melee_start; trigger: - | Fader_Roar_Projectile: aoe r 3; Fader_Roar_Spawn spawns Charred_Melee_Fader, Charred_Archer_Fader x1-1 within r 0 |
| Fader_Roar_Intense | attack_roar | 1.528 | sfx_fader_charredsummon_roar @0.11s | start: sfx_dragon_melee_start; trigger: - | Fader_Roar_Projectile: aoe r 3; Fader_Roar_Spawn spawns Charred_Melee_Fader, Charred_Archer_Fader x1-1 within r 0 |
| Fader_Spin | attack_Spin | 1.3 | sfx_fader_spin @0.09s, fx_Fader_AttackGlint @0.44s | start: -; trigger: fx_Fader_Spin | sphere r 8.5 m (0 m ahead) |
| Fader_WallOfFire | attack_WallOfFire | 1.425 | fx_fader_clawattack @0.90s, sfx_fader_firewall_start @1.08s | start: sfx_dragon_melee_start; trigger: - | Fader_WallOfFire_Spawn spawns Fader_WallOfFire_AOE x12-12 within r 8; Fader_WallOfFire_AOE: sphere r=4 (useTriggers: trigger sphere r=2) |

**Dragon** (Moder)

| attack item | trigger | wind-up s | pre-hit animation-event fx (time s) | item start / trigger effects | area |
|---|---|---|---|---|---|
| dragon_bite | attack_bite | 0.989 | - | start: sfx_dragon_melee_start; trigger: - | cone 8 m / 20 deg |
| dragon_claw_left | attack_claw_left | 1.569 | - | start: sfx_dragon_melee_start; trigger: - | cone 12 m / 50 deg |
| dragon_claw_right | attack_claw_right | 1.601 | - | start: sfx_dragon_melee_start; trigger: - | cone 12 m / 50 deg |
| dragon_coldbreath | attack_breath | 1.366 | - | start: sfx_dragon_coldbreath_start; trigger: - | cone 30 m / 10 deg |
| dragon_spit_shotgun | attack_iceball | 0.89 | - | start: sfx_dragon_coldball_start; trigger: sfx_dragon_coldball_launch, vfx_ColdBall_launch | projectile |
| dragon_taunt | attack_taunt | 1.96 | - | start: -; trigger: sfx_dragon_scream | none (no hit) |

**GoblinKing** (Yagluth)

| attack item | trigger | wind-up s | pre-hit animation-event fx (time s) | item start / trigger effects | area |
|---|---|---|---|---|---|
| GoblinKing_Beam | beam | 2.021 | fx_goblinking_vo_beamstart @1.13s | start: -; trigger: sfx_goblinking_beam | projectile |
| GoblinKing_Meteors | cast1 | 1.909 | fx_goblinking_vo_meteors1 @0.59s | start: -; trigger: - | spawn_meteors spawns projectile_meteor x10-10 within r 15; projectile_meteor: aoe r 5 |
| GoblinKing_Nova | nova | 2.811 | fx_goblinking_vo_nova @0.67s, fx_goblinking_nova_hand @0.78s | start: -; trigger: fx_goblinking_nova | sphere r 8 m (4.51 m ahead) |
| GoblinKing_Taunt | taunt | 1.729 | - | start: -; trigger: sfx_goblinking_taunt | none (no hit) |

**gd_king** (The Elder)

| attack item | trigger | wind-up s | pre-hit animation-event fx (time s) | item start / trigger effects | area |
|---|---|---|---|---|---|
| gd_king_rootspawn | spawn | 1.461 | - | start: sfx_gdking_spawn; trigger: - | spawn_roots spawns TentaRoot x15-15 within r 15 |
| gd_king_scream | scream | 0.591 | - | start: -; trigger: sfx_gdking_scream | none (no hit) |
| gd_king_shoot | shoot | 1.298 | - | start: sfx_gdking_shoot_start; trigger: sfx_gdking_shoot_trigger | projectile |
| gd_king_stomp | stomp0 | 1.995 | - | start: sfx_gdking_stomp; trigger: sfx_gdking_footstep, vfx_gdking_stomp, sfx_gdking_rock_destroyed | sphere r 5 m (3 m ahead) |
| gd_king_stomp | stomp1 | 2.09 | - | start: sfx_gdking_stomp; trigger: sfx_gdking_footstep, vfx_gdking_stomp, sfx_gdking_rock_destroyed | sphere r 5 m (3 m ahead) |

**Bonemass** (Bonemass)

| attack item | trigger | wind-up s | pre-hit animation-event fx (time s) | item start / trigger effects | area |
|---|---|---|---|---|---|
| bonemass_attack_aoe | aoe | 3.3 | - | start: fx_Bonemass_aoe_start; trigger: - | bonemass_aoe: sphere r=9 |
| bonemass_attack_punch | punch0 | 1.126 | - | start: sfx_Bonemass_punch_start; trigger: - | cone 8.5 m / 66.1 deg |
| bonemass_attack_punch | punch1 | 1.137 | - | start: sfx_Bonemass_punch_start; trigger: - | cone 8.5 m / 66.1 deg |
| bonemass_attack_throw | spawn | 3.107 | - | start: sfx_Bonemass_throw_start; trigger: sfx_Bonemass_throw_trigger | bonemass_spawn spawns Skeleton, Blob x4-4 within r 2 |

**SeekerQueen** (The Queen)

| attack item | trigger | wind-up s | pre-hit animation-event fx (time s) | item start / trigger effects | area |
|---|---|---|---|---|---|
| SeekerQueen_Bite | attack_bite | 1.819 | sfx_HiveQueen_bite @0.00s | start: sfx_dragon_melee_start; trigger: - | cone 10 m / 25 deg |
| SeekerQueen_Call | attack_call | 2.918 | sfx_HiveQueen_callout @0.00s | start: -; trigger: - | projectile |
| SeekerQueen_PierceAOE | attack_pierce | 0.857 | sfx_HiveQueen_pierce @0.00s | start: sfx_dragon_melee_start; trigger: fx_QueenPierceGround [variant=0] | sphere r 4.5 m (0 m ahead) |
| SeekerQueen_Rush | attack_rush | 1.443 | sfx_HiveQueen_rush @0.00s | start: sfx_dragon_melee_start; trigger: - | cone 8 m / 120 deg |
| SeekerQueen_Slap | attack_slash0 | no animator transition |  |  | cone 10 m / 145 deg |
| SeekerQueen_Slap | attack_slash1 | 1.063 | sfx_HiveQueen_slash @0.00s | start: sfx_dragon_melee_start; trigger: - | cone 10 m / 145 deg |
| SeekerQueen_Slap | attack_slash2 | 1.069 | sfx_HiveQueen_slash @0.00s | start: sfx_dragon_melee_start; trigger: - | cone 10 m / 145 deg |
| SeekerQueen_Slap | attack_slash3 | 1.024 | sfx_HiveQueen_slash @0.00s | start: sfx_dragon_melee_start; trigger: - | cone 10 m / 145 deg |
| SeekerQueen_Spit | attack_spit | 1.479 | sfx_HiveQueen_acitspit @0.00s | start: -; trigger: sfx_goblinking_beam | SeekerQueen_projectile_spit: aoe r 1.5; SeekerQueen_SpitSpawnAbility spawns SeekerBrood x1-1 within r 1 |
| SeekerQueen_Teleport | attack_teleport | 2.535 | sfx_HiveQueen_burrow @0.00s, fx_Queen_BurrowDown @0.48s | start: -; trigger: - | projectile |

**Eikthyr** (Eikthyr)

| attack item | trigger | wind-up s | pre-hit animation-event fx (time s) | item start / trigger effects | area |
|---|---|---|---|---|---|
| Eikthyr_antler | attack1 | 0.599 | - | start: sfx_eikthyr_attack; trigger: - | cone 4.5 m / 25 deg |
| Eikthyr_charge | attack2 | 1.564 | - | start: sfx_eikthyr_attack; trigger: fx_eikthyr_forwardshockwave | cone 20 m / 45 deg |
| Eikthyr_stomp | attack_stomp | 2.889 | - | start: -; trigger: fx_eikthyr_stomp | sphere r 10 m (3 m ahead) |

**FrozenKing** (Kall Fimbulbringer)

| attack item | trigger | wind-up s | pre-hit animation-event fx (time s) | item start / trigger effects | area |
|---|---|---|---|---|---|
| FrozenKing_ChainRush | attack_rush | 2.024 | sfx_frozenking_voice_attack @0.12s, sfx_frozenking_charge_start @0.16s, sfx_frozenking_charge_doublehookup @0.90s, sfx_frozenking_charge_whoosh @1.69s | start: fx_frozenking_chain_rush [variant=0]; trigger: - | cone 10 m / 70 deg |
| FrozenKing_ChainSlam_L | attack_chain_slamL | 1.429 | sfx_frozenking_attackmelee_single_charge @0.20s, sfx_frozenking_voice_attack @0.41s, sfx_frozenking_attackmelee_single_whsh @1.13s, sfx_frozenking_attackmelee_single_impact @1.39s | start: fx_frozenking_chain_sparks [attach,variant=0,child=ChainWhip_F.L]; trigger: - | cone 10 m / 40 deg |
| FrozenKing_ChainSlam_L_double | attack_chain_slamL_double | 1.429 | sfx_frozenking_attackmelee_single_charge @0.20s, sfx_frozenking_voice_attack @0.41s, sfx_frozenking_attackmelee_single_whsh @1.13s, sfx_frozenking_attackmelee_single_impact @1.39s | start: fx_frozenking_chain_sparks [attach,variant=0,child=ChainWhip_F.L]; trigger: - | cone 10 m / 40 deg |
| FrozenKing_ChainSlam_R | attack_chain_slamR | 1.429 | sfx_frozenking_attackmelee_single_charge @0.10s, sfx_frozenking_voice_attack @0.27s, sfx_frozenking_attackmelee_single_whsh @1.04s, sfx_frozenking_attackmelee_single_impact @1.36s, sfx_frozenking_attackmelee_single_impact @1.42s | start: fx_frozenking_chain_sparks [attach,variant=0,child=ChainWhip_F.R]; trigger: - | cone 10 m / 40 deg |
| FrozenKing_ChainSlam_R_double | attack_chain_slamR_double | 1.429 | sfx_frozenking_attackmelee_single_charge @0.10s, sfx_frozenking_voice_attack @0.27s, sfx_frozenking_attackmelee_single_whsh @1.04s, sfx_frozenking_attackmelee_single_impact @1.36s, sfx_frozenking_attackmelee_single_impact @1.42s | start: fx_frozenking_chain_sparks [attach,variant=0,child=ChainWhip_F.R]; trigger: - | cone 10 m / 40 deg |
| FrozenKing_ChainWhirl | attack_chain_whirl | 1.226 | sfx_frozenking_frozentwirl_charge @0.15s, sfx_frozenking_frozentwirl_tornado @1.02s | start: fx_frozenking_chainwhirl [variant=0], fx_frozenking_chainwhirl [attach,variant=0]; trigger: - | sphere r 8.5 m (0 m ahead) |
| FrozenKing_DoubleSweep | attack_double_sweep | 0.887 | sfx_frozenking_voice_attack @0.07s, sfx_frozenking_attackmelee_double_chain @0.53s, sfx_frozenking_attackmelee_double_whoosh_lead @0.72s, sfx_frozenking_attackmelee_double_impact @0.79s | start: -; trigger: - | cone 10 m / 90 deg |

**FrozenKing_p3** (Kall Fimbulbringer)

| attack item | trigger | wind-up s | pre-hit animation-event fx (time s) | item start / trigger effects | area |
|---|---|---|---|---|---|
| FrozenKing_ChainFlurry | attack_chain_flurry | 2.457 | sfx_frozenking_chainflurry_start @0.02s | start: fx_frozenking_chain_fury_ascending [variant=0], sfx_frozenking_chainflurry_fly [attach,variant=0], sfx_frozenking_chainflurry_coldvortex [attach,variant=0], sfx_frozenking_chainflurry_chain [attach,variant=0]; trigger: fx_frozenking_chain_fury [attach,variant=0] | cone 5 m / 80 deg |
| FrozenKing_DoubleSweep | attack_double_sweep | 0.887 | sfx_frozenking_voice_attack @0.07s, sfx_frozenking_attackmelee_double_chain @0.53s, sfx_frozenking_attackmelee_double_whoosh_lead @0.72s, sfx_frozenking_attackmelee_double_impact @0.79s | start: -; trigger: - | cone 10 m / 90 deg |
| FrozenKing_P3_ChainSlam_L_double | attack_chain_slamL_double | 1.429 | sfx_frozenking_attackmelee_single_charge @0.20s, sfx_frozenking_voice_attack @0.41s, sfx_frozenking_attackmelee_single_whsh @1.13s, sfx_frozenking_attackmelee_single_impact @1.39s | start: fx_frozenking_chain_sparks [attach,variant=0,child=ChainWhip_F.L]; trigger: - | cone 10 m / 40 deg |
| FrozenKing_P3_ChainSlam_R_double | attack_chain_slamR_double | 1.429 | sfx_frozenking_attackmelee_single_charge @0.10s, sfx_frozenking_voice_attack @0.27s, sfx_frozenking_attackmelee_single_whsh @1.04s, sfx_frozenking_attackmelee_single_impact @1.36s, sfx_frozenking_attackmelee_single_impact @1.42s | start: fx_frozenking_chain_sparks [attach,variant=0,child=ChainWhip_F.R]; trigger: - | cone 10 m / 40 deg |
| FrozenKing_P3_ChainWhirl | attack_chain_whirl | 1.226 | sfx_frozenking_frozentwirl_charge @0.15s, sfx_frozenking_frozentwirl_tornado @1.02s | start: fx_frozenking_spin [variant=0], sfx_frozenking_frozentwirl_wind [attach,variant=0]; trigger: - | sphere r 8.5 m (0 m ahead) |
| FrozenKing_Punch_AOE | attack_punch_aoe | 1.239 | sfx_frozenking_punchaoe_chain_start @0.06s, sfx_frozenking_punchaoe_whoosh_start @0.33s, sfx_frozenking_punchaoe_punch_first @1.03s | start: -; trigger: fx_frozenking_punch_aoe | sphere r 8.5 m (0 m ahead) |
| FrozenKing_SpikeRain | attack_spike_rain | 3.349 | sfx_frozenking_spikerain_chain @0.19s, sfx_frozenking_spikerain_iceceiling @1.02s | start: fx_frozenking_spikerain_summoning [variant=0]; trigger: - | spawn_frozenking_spikerain spawns projectile_spikes_frozenking x20-25 within r 30; projectile_spikes_frozenking: aoe r 5 |
| FrozenKing_tendrilspawn | spawn | 1.014 | sfx_frozenking_tendril_summon @0.14s | start: sfx_gdking_spawn; trigger: - | spawn_tendril spawns Tendril x9-9 within r 15 |

## 7. Per-boss detail (generated; includes phase-2 aspects, Hive, TheHive)


### Fader

- name: $enemy_fader ("Fader"); health 25000; m_boss 1; bossEvent `boss_fader`; defeat key `defeated_fader`; flying 0
- MonsterAI: minAttackInterval 0.5 s; viewRange 30.0; alertRange 100.0; sleeping 0 (wakeupRange 5.0); spawnMessage -; alertedMessage $enemy_boss_fader_alertmessage ("The Emerald Flame ignites"); deathMessage $enemy_boss_fader_deathmessage ("The Emerald Flame is extinguished"); alertedEffects -; idleSound sfx_fader_idle
- Animator(s): Visual:Fader_animator
- m_defaultItems: Fader_Fissure, Fader_Bite, Fader_Claw_Left, Fader_Claw_Right, Fader_Spin, Fader_Flamebreath, Fader_Meteors, Fader_Roar, Fader_WallOfFire, Fader_Meteors_Intense, Fader_Fissure_Intense, Fader_Roar_Intense

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Fader_Bite | "Fader Bite" (literal) | attack_bite | Horizontal | 9 (0) | 3 | 10 | - | walking, swimming | melee sweep: range 10 m, angle 40 deg, ray width 2, height 2 | pierce 210 | attack_bite: **1.27** (Attack Bite; 1.27) |
| Fader_Claw_Left | "Fader Claw Left" (literal) | attack_ClawL | Horizontal | 9 (0) | 3 | 10 | - | walking, swimming | melee sweep: range 10 m, angle 65 deg, ray width 1.33, height 2 | pierce 200 | attack_ClawL: **1.236** (Attack Claw L; 1.236) |
| Fader_Claw_Right | "Fader Claw Right" (literal) | attack_ClawR | Horizontal | 9 (0) | 3 | 10 | - | walking, swimming | melee sweep: range 10 m, angle 65 deg, ray width 1.33, height 2 | pierce 200 | attack_ClawR: **1.236** (Attack Claw R; 1.236) |
| Fader_Fissure | "Fader Fissure" (literal) | attack_Fissure | Projectile | 40 (0) | 30 | 180 | 35-85% | walking, swimming | projectile Fader_Fissure_Spawn x1, vel 0 (min 0), spread 30 | pierce 120 | attack_Fissure: **2.879** (Attack Fissure; 2.879) |
| Fader_Fissure_Intense | "Fader Fissure" (literal) | attack_Fissure | Projectile | 40 (0) | 20 | 180 | 0-35% | walking, swimming | projectile Fader_Fissure_Spawn x1, vel 0 (min 0), spread 30 | pierce 120 | attack_Fissure: **2.879** (Attack Fissure; 2.879) |
| Fader_Flamebreath | "Fader Firebreath" (literal) | attack_flamebreath | Projectile | 20 (2) | 25 | 15 | 5-85% | walking, swimming | projectile Fader_Flamebreath_AOE x1, vel 10 (min 2), spread 10 | fire 60, spirit 60 | attack_flamebreath: **2.341** (Attack Flamebreath; 2.341) |
| Fader_Meteors | "spawn" (literal) | taunt | Projectile | 30 (0) | 25 | 20 | 25-100% | flying(alt 0-999999), walking, swimming | projectile spawn_fader_meteors x1, vel 0 (min 2), spread 10 | - | taunt: **1.165** (Taunt; 1.165) |
| Fader_Meteors_Intense | "spawn" (literal) | taunt | Projectile | 30 (0) | 18 | 20 | 0-25% | flying(alt 0-999999), walking, swimming | projectile spawn_fader_meteors x1, vel 0 (min 2), spread 10 | - | taunt: **1.165** (Taunt; 1.165) |
| Fader_Roar | "Fader Roar" (literal) | attack_roar | Projectile | 100 (0) | 45 | 45 | 35-55% | walking, swimming | projectile Fader_Roar_Projectile x2 x 4 bursts every 0.5s, vel 50 (min 20), spread 60 | - | attack_roar: **1.528** (Attack Roar; 1.528) |
| Fader_Roar_Intense | "Fader Roar" (literal) | attack_roar | Projectile | 100 (0) | 26 | 45 | 0-35% | walking, swimming | projectile Fader_Roar_Projectile x2 x 4 bursts every 0.5s, vel 50 (min 20), spread 60 | - | attack_roar: **1.528** (Attack Roar; 1.528) |
| Fader_Spin | "Fader Spin" (literal) | attack_Spin | Area | 8 (0) | 20 | 360 | - | walking, swimming | sphere r=8.5 m at 0 m forward, 2 m up | pierce 140 | attack_Spin: **1.3** (Attack Spin; 1.3) |
| Fader_WallOfFire | "Fader Wall of Fire" (literal) | attack_WallOfFire | Projectile | 40 (0) | 60 | 180 | 15-90% | walking, swimming | projectile Fader_WallOfFire_Spawn x1, vel 0 (min 0), spread 0 | pierce 120 | attack_WallOfFire: **1.425** (Attack Wall of Fire; 1.425) |

- **Fader_Bite**: shared.hit: vfx_HitSparks; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **Fader_Claw_Left**: shared.hit: vfx_HitSparks; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **Fader_Claw_Right**: shared.hit: vfx_HitSparks; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **Fader_Fissure**: shared.start: sfx_dragon_melee_start; shared.trigger: sfx_fader_fissure; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
  - Fader_Fissure_Spawn [SpawnAbility] spawns Fader_Fissure_AOE x12-16 (maxSpawned 0), radius 0, circle 1, atTarget 1, groundOffset 0.2, initialDelay 0, delay 0.45 each, targetType RandomEnemy, maxTargetRange 120, projVel 10 acc 10
    - Fader_Fissure_AOE [Aoe] sphere r=11, useAttackSettings 0, activationDelay 4, ttl 15, hitInterval 0.5, dmg fire 120, spirit 80
- **Fader_Fissure_Intense**: shared.start: sfx_dragon_melee_start; shared.trigger: sfx_fader_fissure; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
  - Fader_Fissure_Spawn [SpawnAbility] spawns Fader_Fissure_AOE x12-16 (maxSpawned 0), radius 0, circle 1, atTarget 1, groundOffset 0.2, initialDelay 0, delay 0.45 each, targetType RandomEnemy, maxTargetRange 120, projVel 10 acc 10
    - Fader_Fissure_AOE [Aoe] sphere r=11, useAttackSettings 0, activationDelay 4, ttl 15, hitInterval 0.5, dmg fire 120, spirit 80
- **Fader_Flamebreath**: shared.trigger: fx_fallenvalkyrie_poisonbreath [variant=0]; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; useCharacterFacing=1, hitThroughWalls=1, speedFactorRotation=0.1
  - Fader_Flamebreath_AOE [Aoe] sphere r=4 (useTriggers: trigger box 3x5x39.45), useAttackSettings 0, activationDelay 1, ttl 14, hitInterval 0.5, dmg fire 60
- **Fader_Meteors**: shared.trailStart: fx_goblinking_vo_meteors2; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; useCharacterFacing=1
  - spawn_fader_meteors [SpawnAbility] spawns projectile_meteor_fader x10-10 (maxSpawned 0), radius 15, circle 0, atTarget 0, groundOffset 90, initialDelay 0, delay 0.5 each, targetType ClosestEnemy, maxTargetRange 40, projVel 30 acc 3
    - projectile_meteor_fader [Projectile] type Physical, ttl 8, gravity 0, aoe r=5, dmg blunt 40, fire 120, spawnOnHit Fader_WallOfFire_AOE (x1), hitEffects fx_fader_meteor_hit
      - Fader_WallOfFire_AOE [Aoe] sphere r=4 (useTriggers: trigger sphere r=2), useAttackSettings 0, activationDelay 0, ttl 20, hitInterval 0.5, dmg fire 80, spirit 80
- **Fader_Meteors_Intense**: shared.trailStart: fx_goblinking_vo_meteors2; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; useCharacterFacing=1
  - spawn_fader_meteors [SpawnAbility] spawns projectile_meteor_fader x10-10 (maxSpawned 0), radius 15, circle 0, atTarget 0, groundOffset 90, initialDelay 0, delay 0.5 each, targetType ClosestEnemy, maxTargetRange 40, projVel 30 acc 3
    - projectile_meteor_fader [Projectile] type Physical, ttl 8, gravity 0, aoe r=5, dmg blunt 40, fire 120, spawnOnHit Fader_WallOfFire_AOE (x1), hitEffects fx_fader_meteor_hit
      - Fader_WallOfFire_AOE [Aoe] sphere r=4 (useTriggers: trigger sphere r=2), useAttackSettings 0, activationDelay 0, ttl 20, hitInterval 0.5, dmg fire 80, spirit 80
- **Fader_Roar**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; attack.burst: fx_Fader_Roar; launchAngle=-75.0, hitThroughWalls=1
  - Fader_Roar_Projectile [Projectile] type Magic, ttl 20, gravity 9, aoe r=3, dmg -, spawnOnHit Fader_Roar_Spawn (x1), hitEffects fx_Fader_Roar_Projectile_Hit
    - Fader_Roar_Spawn [SpawnAbility] spawns Charred_Melee_Fader, Charred_Archer_Fader x1-1 (maxSpawned 7), radius 0, circle 0, atTarget 1, groundOffset 0.2, initialDelay 0, delay 0 each, targetType Position, maxTargetRange 40, projVel 10 acc 10
      - Charred_Melee_Fader: creature $enemy_charred_melee_Fader ("Summoned Charred Warrior"), hp 50
      - Charred_Archer_Fader: creature $enemy_charred_melee_Fader ("Summoned Charred Warrior"), hp 50
- **Fader_Roar_Intense**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; attack.burst: fx_Fader_Roar; launchAngle=-75.0, hitThroughWalls=1
  - Fader_Roar_Projectile [Projectile] type Magic, ttl 20, gravity 9, aoe r=3, dmg -, spawnOnHit Fader_Roar_Spawn (x1), hitEffects fx_Fader_Roar_Projectile_Hit
    - Fader_Roar_Spawn [SpawnAbility] spawns Charred_Melee_Fader, Charred_Archer_Fader x1-1 (maxSpawned 7), radius 0, circle 0, atTarget 1, groundOffset 0.2, initialDelay 0, delay 0 each, targetType Position, maxTargetRange 40, projVel 10 acc 10
      - Charred_Melee_Fader: creature $enemy_charred_melee_Fader ("Summoned Charred Warrior"), hp 50
      - Charred_Archer_Fader: creature $enemy_charred_melee_Fader ("Summoned Charred Warrior"), hp 50
- **Fader_Spin**: shared.trigger: fx_Fader_Spin; shared.hit: vfx_HitSparks; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **Fader_WallOfFire**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
  - Fader_WallOfFire_Spawn [SpawnAbility] spawns Fader_WallOfFire_AOE x12-12 (maxSpawned 0), radius 8, circle 1, atTarget 1, groundOffset 0.2, initialDelay 0, delay 0.25 each, targetType ClosestEnemy, maxTargetRange 40, projVel 10 acc 10, spawnEffects sfx_fader_firewall_fireburst
    - Fader_WallOfFire_AOE [Aoe] sphere r=4 (useTriggers: trigger sphere r=2), useAttackSettings 0, activationDelay 0, ttl 20, hitInterval 0.5, dmg fire 80, spirit 80
- other animator triggers (not from attack items): jump_forward -> Jump Forward (hit at 2.603; fx sfx_fader_bite_snarl@2.32s, fx_Fader_Bite@2.39s); jump_left -> Jump Left (hit at 1.082; fx -); jump_right -> Jump Right (hit at 1.055; fx -)

### Dragon

- name: $enemy_dragon ("Moder"); health 7500; m_boss 1; bossEvent `boss_moder`; defeat key `defeated_dragon`; flying 1
- MonsterAI: minAttackInterval 2.0 s; viewRange 60.0; alertRange 30.0; sleeping 0 (wakeupRange 5.0); spawnMessage $enemy_boss_dragon_spawnmessage ("Moder is enraged"); alertedMessage -; deathMessage $enemy_boss_dragon_deathmessage ("Moder is in tears"); alertedEffects sfx_dragon_alerted; idleSound sfx_dragon_idle
- Animator(s): Visual:dragon_animator
- m_defaultItems: dragon_taunt, dragon_bite, dragon_claw_left, dragon_claw_right, dragon_spit_shotgun, dragon_coldbreath

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| dragon_bite | "Dragon claw left" (literal) | attack_bite | Horizontal | 7 (0) | 30 | 10 | - | walking, swimming | melee sweep: range 8 m, angle 20 deg, ray width 2, height 2 | pierce 120 | attack_bite: **0.989** (bite; 0.989) |
| dragon_claw_left | "Dragon claw left" (literal) | attack_claw_left | Horizontal | 10 (0) | 30 | 30 | - | walking, swimming | melee sweep: range 12 m, angle 50 deg, ray width 2, height 2 | slash 110 | attack_claw_left: **1.569** (claw_left; 1.569) |
| dragon_claw_right | "Dragon claw left" (literal) | attack_claw_right | Horizontal | 10 (0) | 30 | 30 | - | walking, swimming | melee sweep: range 12 m, angle 50 deg, ray width 2, height 2 | slash 110 | attack_claw_right: **1.601** (claw_right; 1.601) |
| dragon_coldbreath | "dragon breath" (literal) | attack_breath | Horizontal | 20 (5) | 8 | 5 | - | walking | melee sweep: range 30 m, angle 10 deg, ray width 2, height 1, maxYAngle 25 | frost 200 | attack_breath: **1.366** (cold breath; 1.366) |
| dragon_spit_shotgun | "cold ball" (literal) | attack_iceball | Projectile | 25 (5) | 8 | 5 | - | flying(alt 0-999999) | projectile dragon_ice_projectile x1 x 16 bursts every 0.05s, vel 25 (min 2), spread 13 | pierce 30, frost 200 | attack_iceball: **0.89** (attack_iceball; 0.89) |
| dragon_taunt | "scream" (literal) | attack_taunt | None | 50 (10) | 30 | 5 | - | walking | no hit (None) | - | attack_taunt: **1.96** (taunt; 1.96) |

- **dragon_bite**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **dragon_claw_left**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=1.0
- **dragon_claw_right**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=1.0
- **dragon_coldbreath**: shared.start: sfx_dragon_coldbreath_start, vfx_greydwarf_shaman_pray [DISABLED,attach]; shared.trailStart: vfx_dragon_coldbreath, sfx_dragon_coldbreath_trailon; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; attackOriginJoint=Jaw, useCharacterFacing=1, hitThroughWalls=1, speedFactorRotation=1.0
- **dragon_spit_shotgun**: shared.start: sfx_dragon_coldball_start; shared.trigger: sfx_dragon_coldball_launch, vfx_ColdBall_launch; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; attackOriginJoint=Jaw, speedFactor=0.2, speedFactorRotation=1.0
  - dragon_ice_projectile [Projectile] type Magic|Frost, ttl 10, gravity 0, aoe r=0, dmg -, spawnOnHit IceBlocker (x1), hitEffects vfx_dragon_ice_hit, sfx_dragon_coldball_explode
    - IceBlocker [Destructible] destructibleType=1, health=10.0, ttl=30.0, destroyedEffect=[vfx_iceblocker_destroyed, sfx_ice_destroyed], hitEffect=[vfx_ice_hit, sfx_ice_hit]
      - vfx_iceblocker_destroyed (no Projectile/Aoe/SpawnAbility component)
      - sfx_ice_destroyed (no Projectile/Aoe/SpawnAbility component)
      - vfx_ice_hit (no Projectile/Aoe/SpawnAbility component)
      - sfx_ice_hit (no Projectile/Aoe/SpawnAbility component)
- **dragon_taunt**: shared.trigger: sfx_dragon_scream; speedFactor=0.2, speedFactorRotation=0.2
- other animator triggers (not from attack items): fly_land -> Land (hit at None; fx -); fly_takeoff -> Takeoff (hit at None; fx sfx_dragon_flap@1.27s, sfx_dragon_flap@2.99s)

### GoblinKing

- name: $enemy_goblinking ("Yagluth"); health 10000; m_boss 1; bossEvent `boss_goblinking`; defeat key `defeated_goblinking`; flying 0
- MonsterAI: minAttackInterval 0.0 s; viewRange 30.0; alertRange 20.0; sleeping 1 (wakeupRange 5.0); spawnMessage $enemy_boss_goblinking_spawnmessage ("Yagluth's twisted soul has been summoned"); alertedMessage -; deathMessage $enemy_boss_goblinking_deathmessage ("Yagluth's soul has been defeated"); alertedEffects -; idleSound -
- Animator(s): Visual:GoblinKingAnimator
- m_defaultItems: GoblinKing_Beam, GoblinKing_Meteors, GoblinKing_Nova, GoblinKing_Taunt

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| GoblinKing_Beam | "dragon breath" (literal) | beam | Projectile | 40 (10) | 15 | 20 | - | flying(alt 0-999999), walking, swimming | projectile projectile_beam x1 x 20 bursts every 0.1s, vel 40 (min 30), spread 1 | fire 40, lightning 20 | beam: **2.021** (beam; 2.021) |
| GoblinKing_Meteors | "spawn" (literal) | cast1 | Projectile | 30 (0) | 25 | 20 | - | flying(alt 0-999999), walking, swimming | projectile spawn_meteors x1, vel 0 (min 2), spread 10 | - | cast1: **1.909** (cast1; 1.909) |
| GoblinKing_Nova | "slap" (literal) | nova | Area | 10 (0) | 20 | 90 | - | flying(alt 0-999999), walking, swimming | sphere r=8 m at 4.51 m forward, 0.36 m up | fire 65, lightning 65 | nova: **2.811** (nova; 2.811) |
| GoblinKing_Taunt | "scream" (literal) | taunt | None | 50 (10) | 60 | 5 | - | prioritized, flying(alt 0-999999), walking, swimming | no hit (None) | - | taunt: **1.729** (Taunt; 1.729) |

- **GoblinKing_Beam**: shared.trigger: sfx_goblinking_beam; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; attackOriginJoint=BeamRoot, useCharacterFacing=1, speedFactorRotation=2.0
  - projectile_beam [Projectile] type Magic, ttl 3, gravity 0, aoe r=0, dmg -, hitEffects fx_goblinking_beam_hit
- **GoblinKing_Meteors**: shared.trailStart: fx_goblinking_vo_meteors2; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; useCharacterFacing=1
  - spawn_meteors [SpawnAbility] spawns projectile_meteor x10-10 (maxSpawned 0), radius 15, circle 0, atTarget 0, groundOffset 50, initialDelay 0, delay 0.5 each, targetType ClosestEnemy, maxTargetRange 40, projVel 30 acc 3
    - projectile_meteor [Projectile] type Physical, ttl 8, gravity 0, aoe r=5, dmg blunt 40, fire 120, hitEffects fx_goblinking_meteor_hit
- **GoblinKing_Nova**: shared.trigger: fx_goblinking_nova; shared.trailStart: fx_goblinking_vo_nova; shared.hit: vfx_troll_attack_hit, sfx_troll_attack_hit; hitThroughWalls=1
  - aoe_nova [Aoe] sphere r=10, useAttackSettings 0, activationDelay 0, ttl 10, hitInterval 1, dmg fire 100
- **GoblinKing_Taunt**: shared.trigger: sfx_goblinking_taunt; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit

### gd_king

- name: $enemy_gdking ("The Elder"); health 2500; m_boss 1; bossEvent `boss_gdking`; defeat key `defeated_gdking`; flying 0
- MonsterAI: minAttackInterval 1.0 s; viewRange 50.0; alertRange 9999.0; sleeping 1 (wakeupRange 5.0); spawnMessage $enemy_gdking_alertmessage ("The Elder rises from the forest"); alertedMessage -; deathMessage $enemy_gdking_deathmessage ("The Elder has been felled"); alertedEffects sfx_gdking_alert; idleSound sfx_gdking_idle
- Animator(s): Visual:gd_king_animator
- m_defaultItems: gd_king_rootspawn, gd_king_scream, gd_king_shoot, gd_king_stomp

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| gd_king_rootspawn | "spawn" (literal) | spawn | Projectile | 30 (0) | 25 | 5 | - | flying(alt 0-999999), walking, swimming | projectile spawn_roots x1, vel 10 (min 2), spread 10 | - | spawn: **1.461** (spawn; 1.461) |
| gd_king_scream | "scream" (literal) | scream | None | 50 (10) | 30 | 5 | - | flying(alt 0-999999), walking, swimming | no hit (None) | - | scream: **0.591** (scream; 0.591) |
| gd_king_shoot | "shaman attack" (literal) | shoot | Projectile | 50 (15) | 6 | 5 | - | flying(alt 0-999999), walking, swimming | projectile gdking_root_projectile x1 x 25 bursts every 0.1s, vel 30 (min 2), spread 10 | pierce 35 | shoot: **1.298** (shoot; 1.298) |
| gd_king_stomp | "jaws" (literal) | stomp0, stomp1 | Area | 3 (0) | 5 | 40 | - | flying(alt 0-999999), walking, swimming | sphere r=5 m at 3 m forward, 0 m up | blunt 60 | stomp0: **1.995** (stomp_right; 1.995); stomp1: **2.09** (stomp_left; 2.09) |

- **gd_king_rootspawn**: shared.start: sfx_gdking_spawn; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; useCharacterFacing=1
  - spawn_roots [SpawnAbility] spawns TentaRoot x15-15 (maxSpawned 30), radius 15, circle 0, atTarget 1, groundOffset 0.2, initialDelay 0, delay 0.1 each, targetType ClosestEnemy, maxTargetRange 40, projVel 10 acc 10, spawnEffects fx_gdking_rootspawn
    - TentaRoot: creature $enemy_root ("Root"), hp 20
- **gd_king_scream**: shared.trigger: sfx_gdking_scream; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit
- **gd_king_shoot**: shared.start: sfx_gdking_shoot_start; shared.trigger: sfx_gdking_shoot_trigger; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; useCharacterFacing=1, speedFactorRotation=0.5
  - gdking_root_projectile [Projectile] type Magic|Nature, ttl 6, gravity 0, aoe r=0, dmg -, hitEffects vfx_gdking_projectile_hit, sfx_gdking_projectile_hit
- **gd_king_stomp**: shared.start: sfx_gdking_stomp; shared.trigger: sfx_gdking_footstep, vfx_gdking_stomp, sfx_gdking_rock_destroyed; hitThroughWalls=1, speedFactorRotation=1.0
- other animator triggers (not from attack items): punch -> punch (hit at 3.424; fx -)

### Bonemass

- name: $enemy_bonemass ("Bonemass"); health 5000; m_boss 1; bossEvent `boss_bonemass`; defeat key `defeated_bonemass`; flying 0
- MonsterAI: minAttackInterval 0.0 s; viewRange 50.0; alertRange 9999.0; sleeping 1 (wakeupRange 5.0); spawnMessage $enemy_boss_bonemass_spawnmessage ("The Mass is moving"); alertedMessage -; deathMessage $enemy_boss_bonemass_deathmessage ("The Mass ceases its writhing"); alertedEffects sfx_Bonemass_alert; idleSound sfx_Bonemass_idle
- Animator(s): model:bonemass_animator
- m_defaultItems: bonemass_attack_aoe, bonemass_attack_punch, bonemass_attack_throw

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| bonemass_attack_aoe | "heal" (literal) | aoe | Projectile | 15 (0) | 30 | 45 | - | flying(alt 0-999999), walking, swimming | projectile bonemass_aoe x1, vel 10 (min 2), spread 10 | poison 130 | aoe: **3.3** (aoe; 3.3) |
| bonemass_attack_punch | "slap" (literal) | punch0, punch1 | Horizontal | 8 (0) | 7 | 40 | - | flying(alt 0-999999), walking, swimming | melee sweep: range 8.5 m, angle 66.1 deg, ray width 2.5, height 2.5 | blunt 80, poison 50 | punch0: **1.126** (punch_left; 1.126); punch1: **1.137** (punch_right; 1.137) |
| bonemass_attack_throw | "slime throw" (literal) | spawn | Projectile | 30 (0) | 50 | 20 | - | flying(alt 0-999999), walking, swimming | projectile bonemass_throw_projectile x1, vel 20 (min 2), spread 5 | - | spawn: **3.107** (spawn; 3.107) |

- **bonemass_attack_aoe**: shared.start: fx_Bonemass_aoe_start; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; useCharacterFacing=1, hitThroughWalls=1
  - bonemass_aoe [Aoe] sphere r=9, useAttackSettings 1, activationDelay 0, ttl 15, hitInterval 1, dmg -
- **bonemass_attack_punch**: shared.start: sfx_Bonemass_punch_start; shared.hit: sfx_MudDestroyed, vfx_MudDestroyed [DISABLED]; hitThroughWalls=1, speedFactor=0.5, speedFactorRotation=1.0
- **bonemass_attack_throw**: shared.start: sfx_Bonemass_throw_start; shared.trigger: sfx_Bonemass_throw_trigger; shared.hit: vfx_HitSparks [DISABLED], sfx_greydwarf_attack_hit [DISABLED]; useCharacterFacing=1
  - bonemass_throw_projectile [Projectile] type Physical, ttl 4, gravity 10, aoe r=0, dmg -, spawnOnHit bonemass_spawn (x1), hitEffects sfx_ProjectileHit, vfx_ProjectileHit, sfx_troll_attack_hit
    - bonemass_spawn [SpawnAbility] spawns Skeleton, Blob x4-4 (maxSpawned 8), radius 2, circle 0, atTarget 1, groundOffset 0.2, initialDelay 0, delay 0 each, targetType Position, maxTargetRange 40, projVel 10 acc 10, spawnEffects sfx_DraugrSpawn, vfx_DraugrSpawn
      - Skeleton: creature $enemy_skeleton ("Skeleton"), hp 40
      - Blob: creature $enemy_blob ("Blob"), hp 50

### SeekerQueen

- name: $enemy_seekerqueen ("The Queen"); health 12500; m_boss 1; bossEvent `boss_queen`; defeat key `defeated_queen`; flying 0
- MonsterAI: minAttackInterval 0.5 s; viewRange 30.0; alertRange 30.0; sleeping 0 (wakeupRange 5.0); spawnMessage -; alertedMessage $enemy_boss_queen_alertmessage ("The Queen wants it all"); deathMessage $enemy_boss_queen_deathmessage ("Long live the Queen"); alertedEffects sfx_HiveQueen_alerted; idleSound sfx_HiveQueen_idle
- Animator(s): Visual:SeekerQueen_animator
- m_defaultItems: SeekerQueen_Teleport, SeekerQueen_Rush, SeekerQueen_Bite, SeekerQueen_Call, SeekerQueen_Spit, SeekerQueen_Slap, SeekerQueen_PierceAOE

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| SeekerQueen_Bite | "slap" (literal) | attack_bite | Horizontal | 10 (0) | 10 | 45 | 0-70% | walking, swimming | melee sweep: range 10 m, angle 25 deg, ray width 2, height 1 | pierce 140, poison 100 | attack_bite: **1.819** (Attack Bite; 1.819) |
| SeekerQueen_Call | "Brute taunt" (literal) | attack_call | Projectile | 25 (0) | 60 | 360 | 0-99% | prioritized, flying(alt 0-999999), walking, dungeonOnly | projectile SeekerQueen_triggerspawn_ability x1, vel 0 (min 0), spread 0 | slash 80 | attack_call: **2.918** (Attack Call; 2.918) |
| SeekerQueen_PierceAOE | "slap" (literal) | attack_pierce | Area | 4 (0) | 4 | 90 | - | walking, swimming | sphere r=4.5 m at 0 m forward, 0.24 m up | pierce 150 | attack_pierce: **0.857** (Attack Pierce; 0.857, 0.96) |
| SeekerQueen_Rush | "slap" (literal) | attack_rush | Horizontal | 25 (5) | 25 | 40 | 0-60% | walking, swimming | melee sweep: range 8 m, angle 120 deg, ray width 2.5, height 1 | slash 100 | attack_rush: **1.443** (Attack Rush; 1.443) |
| SeekerQueen_Slap | "slap" (literal) | attack_slash0, attack_slash1, attack_slash2, attack_slash3 | Horizontal | 9 (0) | 4 | 60 | - | walking, swimming | melee sweep: range 10 m, angle 145 deg, ray width 2.5, height 1 | slash 130 | attack_slash1: **1.063** (Attack slash 1; 1.063); attack_slash2: **1.069** (Attack slash 2; 1.069); attack_slash3: **1.024** (Attack slash 3; 1.024) |
| SeekerQueen_Spit | "dragon breath" (literal) | attack_spit | Projectile | 25 (5) | 20 | 25 | 0-80% | flying(alt 0-999999), walking, swimming | projectile SeekerQueen_projectile_spit x1 x 20 bursts every 0.05s, vel 15 (min 0), spread 5 | blunt 40, poison 40 | attack_spit: **1.479** (Attack Spit; 1.479) |
| SeekerQueen_Teleport | "Brute taunt" (literal) | attack_teleport | Projectile | 400 (0) | 60 | 360 | 0-90% | flying(alt 0-999999), walking, dungeonOnly | projectile SeekerQueen_projectile_teleport x1, vel 0 (min 0), spread 0 | slash 80 | attack_teleport: **2.535** (Teleport; 2.535) |

- **SeekerQueen_Bite**: shared.start: sfx_dragon_melee_start; hitThroughWalls=1, speedFactorRotation=0.5
- **SeekerQueen_Call**: no effects
  - SeekerQueen_triggerspawn_ability [TriggerSpawnAbility] range=30.0
- **SeekerQueen_PierceAOE**: shared.start: sfx_dragon_melee_start; shared.trigger: fx_QueenPierceGround [variant=0]; hitThroughWalls=1, speedFactorRotation=0.5
- **SeekerQueen_Rush**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; hitThroughWalls=1, speedFactorRotation=0.5
- **SeekerQueen_Slap**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit [DISABLED]; hitThroughWalls=1, speedFactorRotation=0.5
- **SeekerQueen_Spit**: shared.trigger: sfx_goblinking_beam; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; attackOriginJoint=PukePoint, speedFactorRotation=2.0
  - SeekerQueen_projectile_spit [Projectile] type Physical, ttl 4, gravity 10, aoe r=1.5, dmg -, spawnOnHit SeekerQueen_SpitSpawnAbility (x1), hitEffects SeekerQueen_spithit
    - SeekerQueen_SpitSpawnAbility [SpawnAbility] spawns SeekerBrood x1-1 (maxSpawned 30), radius 1, circle 0, atTarget 0, groundOffset 0.2, initialDelay 0, delay 0 each, targetType Position, maxTargetRange 40, projVel 10 acc 10
      - SeekerBrood: creature $enemy_babyseeker ("Seeker Brood"), hp 20
- **SeekerQueen_Teleport**: no effects
  - SeekerQueen_projectile_teleport [TeleportAbility] targetTag=SeekerQueenTeleportTarget, maxTeleportRange=200.0
- other animator triggers (not from attack items): attack_slash4 -> Attack slash 4 (hit at 1.027; fx sfx_HiveQueen_slash@0.00s); taunt -> Taunt (hit at None; fx sfx_seeker_brute_taunt@0.00s, sfx_HiveQueen_move@2.42s)

### Eikthyr

- name: $enemy_eikthyr ("Eikthyr"); health 500; m_boss 1; bossEvent `boss_eikthyr`; defeat key `defeated_eikthyr`; flying 0
- MonsterAI: minAttackInterval 2.0 s; viewRange 40.0; alertRange 9999.0; sleeping 1 (wakeupRange 5.0); spawnMessage $enemy_eikthyr_alertmessage ("Eikthyr summons the storm"); alertedMessage -; deathMessage $enemy_eikthyr_deathmessage ("Eikthyr is slain"); alertedEffects sfx_eikthyr_alert; idleSound sfx_eikthyr_idle
- Animator(s): OLD:eikthyrnir_animator (inactive), Visual:eikthyrnir_animator_new
- m_defaultItems: Eikthyr_antler, Eikthyr_charge, Eikthyr_stomp

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Eikthyr_antler | "StagAttack1" (literal) | attack1 | Horizontal | 4 (0) | 5 | 30 | - | flying(alt 0-999999), walking, swimming | melee sweep: range 4.5 m, angle 25 deg, ray width 2, height 2.4 | pierce 20 | attack1: **0.599** (Attack1 Medium; 0.599) |
| Eikthyr_charge | "StagAttack2" (literal) | attack2 | Horizontal | 15 (0) | 25 | 15 | - | prioritized, flying(alt 0-999999), walking | melee sweep: range 20 m, angle 45 deg, ray width 2, height 2 | lightning 15 | attack2: **1.564** (Attack2 Hard; 1.564) |
| Eikthyr_stomp | "slap" (literal) | attack_stomp | Area | 6 (0) | 40 | 60 | - | flying(alt 0-999999), walking, swimming | sphere r=10 m at 3 m forward, 0 m up | lightning 15 | attack_stomp: **2.889** (attack_stomp; 2.889) |

- **Eikthyr_antler**: shared.start: sfx_eikthyr_attack; shared.hit: vfx_HitSparks, sfx_wolf_attack_hit; hitThroughWalls=1, speedFactorRotation=1.0
- **Eikthyr_charge**: shared.start: sfx_eikthyr_attack; shared.trigger: fx_eikthyr_forwardshockwave; shared.hit: vfx_HitSparks, sfx_wolf_attack_hit; hitThroughWalls=1
- **Eikthyr_stomp**: shared.start: sfx_lox_attack_stomp [DISABLED]; shared.trigger: fx_eikthyr_stomp; hitThroughWalls=1

### FrozenKing

- name: $enemy_frozenking ("Kall Fimbulbringer"); health 10000; m_boss 1; bossEvent `boss_frozenking`; defeat key `defeated_frozenking`; flying 0
- MonsterAI: minAttackInterval 0.5 s; viewRange 30.0; alertRange 100.0; sleeping 1 (wakeupRange 15.0); spawnMessage -; alertedMessage $enemy_boss_frozenking_alertmessage ("His hatred corrupts all!"); deathMessage -; alertedEffects -; idleSound sfx_frozenking_idle_breath [attach]
- Animator(s): Visual:FrozenKing_animator
- m_defaultItems: FrozenKing_ChainSlam_L, FrozenKing_ChainSlam_L_double, FrozenKing_ChainSlam_R, FrozenKing_ChainSlam_R_double, FrozenKing_ChainWhirl, FrozenKing_ChainRush, FrozenKing_DoubleSweep

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| FrozenKing_ChainRush | "FrozenKing ChainRush" (literal) | attack_rush | Horizontal | 12 (1) | 10 | 10 | - | walking, swimming | melee sweep: range 10 m, angle 70 deg, ray width 2, height 2 | blunt 125, pierce 125 | attack_rush: **2.024** (attack Chain Rush; 2.024) |
| FrozenKing_ChainSlam_L | "FrozenKing ChainSlam L" (literal) | attack_chain_slamL | Vertical | 12 (0) | 3 | 10 | 50-100% | walking, swimming | melee sweep: range 10 m, angle 40 deg, ray width 2, height 2 | blunt 160 | attack_chain_slamL: **1.429** (attack Chain Slam L; 1.429) |
| FrozenKing_ChainSlam_L_double | "FrozenKing ChainSlam L double" (literal) | attack_chain_slamL_double | Vertical | 12 (1) | 3 | 10 | 0-75% | walking, swimming | melee sweep: range 10 m, angle 40 deg, ray width 2, height 2 | blunt 160 | attack_chain_slamL_double: **1.429** (attack Chain Slam L Double First; 1.429, 2.763) |
| FrozenKing_ChainSlam_R | "FrozenKing ChainSlam R" (literal) | attack_chain_slamR | Vertical | 12 (0) | 3 | 10 | 50-100% | walking, swimming | melee sweep: range 10 m, angle 40 deg, ray width 2, height 2 | blunt 160 | attack_chain_slamR: **1.429** (attack Chain Slam R; 1.429) |
| FrozenKing_ChainSlam_R_double | "FrozenKing ChainSlam R double" (literal) | attack_chain_slamR_double | Vertical | 12 (1) | 3 | 10 | 0-50% | walking, swimming | melee sweep: range 10 m, angle 40 deg, ray width 2, height 2 | blunt 160 | attack_chain_slamR_double: **1.429** (attack Chain Slam R Double First; 1.429, 2.763) |
| FrozenKing_ChainWhirl | "FrozenKing ChainWhirl" (literal) | attack_chain_whirl | Area | 8 (0) | 10 | 360 | - | walking, swimming | sphere r=8.5 m at 0 m forward, 2 m up | blunt 150, frost 20 | attack_chain_whirl: **1.226** (attack Chain Whirl; 1.226, 2.449) |
| FrozenKing_DoubleSweep | "FrozenKing DoubleSweep" (literal) | attack_double_sweep | Horizontal | 12 (2) | 3 | 10 | - | walking, swimming | melee sweep: range 10 m, angle 90 deg, ray width 3, height 2 | blunt 150 | attack_double_sweep: **0.887** (attack Double Sweep; 0.887, 2.033) |

- **FrozenKing_ChainRush**: shared.start: fx_frozenking_chain_rush [variant=0]; shared.hit: fx_frozenking_chain_rush_impact [variant=0,child=Root]; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **FrozenKing_ChainSlam_L**: shared.start: fx_frozenking_chain_sparks [attach,variant=0,child=ChainWhip_F.L]; shared.hit: fx_frozenking_chain_ground_impact_1; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **FrozenKing_ChainSlam_L_double**: shared.start: fx_frozenking_chain_sparks [attach,variant=0,child=ChainWhip_F.L]; shared.hit: fx_frozenking_chain_ground_impact_1; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **FrozenKing_ChainSlam_R**: shared.start: fx_frozenking_chain_sparks [attach,variant=0,child=ChainWhip_F.R]; shared.hit: fx_frozenking_chain_ground_impact_1; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **FrozenKing_ChainSlam_R_double**: shared.start: fx_frozenking_chain_sparks [attach,variant=0,child=ChainWhip_F.R]; shared.hit: fx_frozenking_chain_ground_impact_1; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **FrozenKing_ChainWhirl**: shared.start: fx_frozenking_chainwhirl [variant=0]; attack.start: fx_frozenking_chainwhirl [attach,variant=0]; shared.hit: vfx_HitSparks; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **FrozenKing_DoubleSweep**: shared.hit: fx_frozenking_chain_ground_impact_1; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- other animator triggers (not from attack items): attack_chain_flurry -> attack Chain Flurry (hit at 2.457; fx sfx_frozenking_chainflurry_start@0.02s); attack_chain_sweepL -> attack Chain Sweep L (hit at 2.006; fx sfx_frozenking_attackmelee_single_charge@0.28s, sfx_frozenking_attackmelee_single_whsh@1.06s, sfx_frozenking_attackmelee_single_impact@1.46s, sfx_frozenking_charge_whoosh@1.49s); attack_chain_sweepR -> attack Chain Sweep R (hit at 2.006; fx sfx_frozenking_attackmelee_single_charge@0.24s, sfx_frozenking_attackmelee_single_whsh@1.02s, sfx_frozenking_attackmelee_single_impact@1.42s, sfx_frozenking_charge_whoosh@1.49s); attack_punch_aoe -> attack Punch AoE (hit at 1.239; fx sfx_frozenking_punchaoe_chain_start@0.06s, sfx_frozenking_punchaoe_whoosh_start@0.33s, sfx_frozenking_punchaoe_punch_first@1.03s); attack_spike_rain -> attack Spike Rain (hit at 3.349; fx sfx_frozenking_spikerain_chain@0.19s, sfx_frozenking_spikerain_iceceiling@1.02s); jump -> Jump (hit at None; fx sfx_frozenking_turn@0.85s, sfx_frozenking_turn@1.93s); spawn -> attack Tentacles (hit at 1.014; fx sfx_frozenking_tendril_summon@0.14s)

### FrozenKing_p2

- name: $enemy_frozenking ("Kall Fimbulbringer"); health 7000; m_boss 1; bossEvent `boss_frozenking`; defeat key ``; flying 0
- MonsterAI: minAttackInterval 0.5 s; viewRange 100.0; alertRange 100.0; sleeping 0 (wakeupRange 15.0); spawnMessage -; alertedMessage -; deathMessage -; alertedEffects -; idleSound sfx_fader_idle [DISABLED]
- Animator(s): Visual:FrozenKing_p2_animator, Visual:Fader_animator (inactive)
- m_defaultItems: 

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|

- other animator triggers (not from attack items): attack_summon -> Summon (hit at 0.017; fx -)

### FrozenKing_p3

- name: $enemy_frozenking_p3 ("Kall Fimbulbringer"); health 30000; m_boss 1; bossEvent `boss_frozenking`; defeat key `defeated_frozenking_p3`; flying 0
- MonsterAI: minAttackInterval 0.5 s; viewRange 30.0; alertRange 100.0; sleeping 0 (wakeupRange 15.0); spawnMessage -; alertedMessage -; deathMessage $enemy_boss_frozenking_deathmessage ("Peace settles over the world"); alertedEffects -; idleSound sfx_frozenking_idle_breath [attach]
- Animator(s): Visual:FrozenKing_p3_OverrideController, Tentaroots:tendril_back_animator_3, Tentaroots:tendril_back_animator_2, Tentaroots:tendril_back_animator_1, Tentaroots:tendril_back_animator_3, Tentaroots:tendril_back_animator_1, Tentaroots:tendril_back_animator_2, Tentaroots:tendril_back_animator
- m_defaultItems: FrozenKing_P3_ChainSlam_L_double, FrozenKing_P3_ChainSlam_R_double, FrozenKing_Punch_AOE, FrozenKing_ChainFlurry, FrozenKing_DoubleSweep, FrozenKing_SpikeRain, FrozenKing_tendrilspawn, FrozenKing_P3_ChainWhirl

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| FrozenKing_ChainFlurry | "FrozenKing ChainFlurry" (literal) | attack_chain_flurry | Horizontal | 12 (2) | 3 | 10 | 0-50% | walking, swimming | melee sweep: range 5 m, angle 80 deg, ray width 2, height 1 | blunt 250 | attack_chain_flurry: **2.457** (attack Chain Flurry; 2.457, 2.55, 2.637, 2.731) |
| FrozenKing_DoubleSweep | "FrozenKing DoubleSweep" (literal) | attack_double_sweep | Horizontal | 12 (2) | 3 | 10 | - | walking, swimming | melee sweep: range 10 m, angle 90 deg, ray width 3, height 2 | blunt 150 | attack_double_sweep: **0.887** (attack Double Sweep; 0.887, 2.033) |
| FrozenKing_P3_ChainSlam_L_double | "FrozenKing ChainSlam L double" (literal) | attack_chain_slamL_double | Vertical | 12 (1) | 3 | 10 | - | walking, swimming | melee sweep: range 10 m, angle 40 deg, ray width 2, height 2 | blunt 120, fire 50, frost 50 | attack_chain_slamL_double: **1.429** (attack Chain Slam L Double First; 1.429, 2.763) |
| FrozenKing_P3_ChainSlam_R_double | "FrozenKing ChainSlam R double" (literal) | attack_chain_slamR_double | Vertical | 12 (1) | 3 | 10 | - | walking, swimming | melee sweep: range 10 m, angle 40 deg, ray width 2, height 2 | blunt 120, fire 50, frost 50 | attack_chain_slamR_double: **1.429** (attack Chain Slam R Double First; 1.429, 2.763) |
| FrozenKing_P3_ChainWhirl | "FrozenKing ChainWhirl" (literal) | attack_chain_whirl | Area | 8 (0) | 20 | 360 | 75-100% | walking, swimming | sphere r=8.5 m at 0 m forward, 2 m up | blunt 120, fire 50, frost 50 | attack_chain_whirl: **1.226** (attack Chain Whirl; 1.226, 2.449) |
| FrozenKing_Punch_AOE | "FrozenKing Punch AOE" (literal) | attack_punch_aoe | Area | 8 (0) | 8 | 360 | - | walking, swimming | sphere r=8.5 m at 0 m forward, 2 m up | blunt 150 | attack_punch_aoe: **1.239** (attack Punch AoE; 1.239, 2.145, 3.08) |
| FrozenKing_SpikeRain | "spawn" (literal) | attack_spike_rain | Projectile | 20 (0) | 25 | 20 | 0-35% | flying(alt 0-999999), walking, swimming | projectile spawn_frozenking_spikerain x1, vel 0 (min 2), spread 10 | - | attack_spike_rain: **3.349** (attack Spike Rain; 3.349) |
| FrozenKing_tendrilspawn | "spawn" (literal) | spawn | Projectile | 30 (0) | 45 | 5 | - | flying(alt 0-999999), walking, swimming | projectile spawn_tendril x1, vel 5 (min 2), spread 10 | - | spawn: **1.014** (attack Tentacles; 1.014) |

- **FrozenKing_ChainFlurry**: shared.start: fx_frozenking_chain_fury_ascending [variant=0]; attack.start: sfx_frozenking_chainflurry_fly [attach,variant=0], sfx_frozenking_chainflurry_coldvortex [attach,variant=0], sfx_frozenking_chainflurry_chain [attach,variant=0]; shared.trigger: fx_frozenking_chain_fury [attach,variant=0]; shared.hit: vfx_HitSparks; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **FrozenKing_DoubleSweep**: shared.hit: fx_frozenking_chain_ground_impact_1; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **FrozenKing_P3_ChainSlam_L_double**: shared.start: fx_frozenking_chain_sparks [attach,variant=0,child=ChainWhip_F.L]; shared.hit: fx_frozenking_chain_ground_impact_1, sfx_frozenking_doubleslam_explosion; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **FrozenKing_P3_ChainSlam_R_double**: shared.start: fx_frozenking_chain_sparks [attach,variant=0,child=ChainWhip_F.R]; shared.hit: fx_frozenking_chain_ground_impact_1, sfx_frozenking_doubleslam_explosion; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **FrozenKing_P3_ChainWhirl**: shared.start: fx_frozenking_spin [variant=0], sfx_frozenking_frozentwirl_wind [attach,variant=0]; shared.hit: vfx_HitSparks; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **FrozenKing_Punch_AOE**: shared.trigger: fx_frozenking_punch_aoe; shared.hit: vfx_HitSparks; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **FrozenKing_SpikeRain**: shared.start: fx_frozenking_spikerain_summoning [variant=0]; shared.trailStart: fx_goblinking_vo_meteors2; shared.hit: vfx_HitSparks
  - spawn_frozenking_spikerain [SpawnAbility] spawns projectile_spikes_frozenking x20-25 (maxSpawned 0), radius 30, circle 0, atTarget 0, groundOffset 15, initialDelay 0, delay 0 each, targetType ClosestEnemy, maxTargetRange 20, projVel 15 acc 3, spawnEffects sfx_frozenking_spikerain_flyby [attach,variant=0]
    - projectile_spikes_frozenking [Projectile] type Physical, ttl 8, gravity 15, aoe r=5, dmg blunt 100, frost 100, hitEffects fx_frozenking_spikerain_hit, sfx_frozenking_spikerain_explosion
- **FrozenKing_tendrilspawn**: shared.start: sfx_gdking_spawn; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; useCharacterFacing=1, launchAngle=70.0
  - spawn_tendril [SpawnAbility] spawns Tendril x9-9 (maxSpawned 30), radius 15, circle 1, atTarget 1, groundOffset 0.2, initialDelay 0, delay 0.1 each, targetType ClosestEnemy, maxTargetRange 40, projVel 10 acc 10, spawnEffects fx_frozenking_tendrilspawn
    - Tendril: creature "Tendril" (literal), hp 80
- other animator triggers (not from attack items): attack -> punch (hit at 1.351; fx -); attack_chain_slamL -> attack Chain Slam L (hit at 1.429; fx sfx_frozenking_attackmelee_single_charge@0.20s, sfx_frozenking_voice_attack@0.41s, sfx_frozenking_attackmelee_single_whsh@1.13s, sfx_frozenking_attackmelee_single_impact@1.39s); attack_chain_slamR -> attack Chain Slam R (hit at 1.429; fx sfx_frozenking_attackmelee_single_charge@0.10s, sfx_frozenking_voice_attack@0.27s, sfx_frozenking_attackmelee_single_whsh@1.04s, sfx_frozenking_attackmelee_single_impact@1.36s, sfx_frozenking_attackmelee_single_impact@1.42s); attack_chain_sweepL -> attack Chain Sweep L (hit at 2.006; fx sfx_frozenking_attackmelee_single_charge@0.28s, sfx_frozenking_attackmelee_single_whsh@1.06s, sfx_frozenking_attackmelee_single_impact@1.46s, sfx_frozenking_charge_whoosh@1.49s); attack_chain_sweepR -> attack Chain Sweep R (hit at 2.006; fx sfx_frozenking_attackmelee_single_charge@0.24s, sfx_frozenking_attackmelee_single_whsh@1.02s, sfx_frozenking_attackmelee_single_impact@1.42s, sfx_frozenking_charge_whoosh@1.49s); attack_rush -> attack Chain Rush (hit at 2.024; fx sfx_frozenking_voice_attack@0.12s, sfx_frozenking_charge_start@0.16s, sfx_frozenking_charge_doublehookup@0.90s, sfx_frozenking_charge_whoosh@1.69s); jump -> Jump (hit at None; fx sfx_frozenking_turn@0.85s, sfx_frozenking_turn@1.93s)

### Aspect_Eikthyr

- name: $enemy_aspect_eikthyr ("Aspect of the Lightning Stag"); health 3000; m_boss 0; bossEvent ``; defeat key ``; flying 0
- MonsterAI: minAttackInterval 2.0 s; viewRange 40.0; alertRange 9999.0; sleeping 1 (wakeupRange 5.0); spawnMessage -; alertedMessage -; deathMessage -; alertedEffects sfx_eikthyr_alert; idleSound sfx_eikthyr_idle
- Animator(s): OLD:eikthyrnir_animator (inactive), Visual:eikthyrnir_animator_new
- m_defaultItems: aspect_Eikthyr_antler, aspect_Eikthyr_charge, aspect_Eikthyr_stomp

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| aspect_Eikthyr_antler | "StagAttack1" (literal) | attack1 | Horizontal | 4 (0) | 5 | 30 | - | flying(alt 0-999999), walking, swimming | melee sweep: range 4.5 m, angle 25 deg, ray width 2, height 2.4 | pierce 150 | attack1: **0.599** (Attack1 Medium; 0.599) |
| aspect_Eikthyr_charge | "StagAttack2" (literal) | attack2 | Horizontal | 15 (0) | 25 | 15 | - | prioritized, flying(alt 0-999999), walking | melee sweep: range 20 m, angle 45 deg, ray width 2, height 2 | lightning 150 | attack2: **1.564** (Attack2 Hard; 1.564) |
| aspect_Eikthyr_stomp | "slap" (literal) | attack_stomp | Area | 6 (0) | 40 | 60 | - | flying(alt 0-999999), walking, swimming | sphere r=10 m at 3 m forward, 0 m up | lightning 150 | attack_stomp: **2.889** (attack_stomp; 2.889) |

- **aspect_Eikthyr_antler**: shared.start: sfx_eikthyr_attack; shared.hit: vfx_HitSparks, sfx_wolf_attack_hit; hitThroughWalls=1, speedFactorRotation=1.0
- **aspect_Eikthyr_charge**: shared.start: sfx_eikthyr_attack; shared.trigger: fx_eikthyr_forwardshockwave; shared.hit: vfx_HitSparks, sfx_wolf_attack_hit; hitThroughWalls=1
- **aspect_Eikthyr_stomp**: shared.start: sfx_lox_attack_stomp [DISABLED]; shared.trigger: fx_eikthyr_stomp; hitThroughWalls=1

### Aspect_Elder

- name: $enemy_aspect_gdking ("Aspect of the Living Forest"); health 1600; m_boss 0; bossEvent ``; defeat key ``; flying 0
- MonsterAI: minAttackInterval 1.0 s; viewRange 50.0; alertRange 9999.0; sleeping 1 (wakeupRange 5.0); spawnMessage -; alertedMessage -; deathMessage -; alertedEffects sfx_gdking_alert; idleSound sfx_gdking_idle
- Animator(s): Visual:Aspect_Elder_animator
- m_defaultItems: aspect_gd_king_rootspawn, aspect_gd_king_scream, aspect_gd_king_shoot, aspect_gd_king_stomp

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| aspect_gd_king_rootspawn | "spawn" (literal) | spawn | Projectile | 30 (0) | 25 | 5 | - | flying(alt 0-999999), walking, swimming | projectile aspect_spawn_roots x1, vel 10 (min 2), spread 10 | - | spawn: **1.461** (spawn; 1.461) |
| aspect_gd_king_scream | "scream" (literal) | scream | None | 50 (10) | 30 | 5 | - | flying(alt 0-999999), walking, swimming | no hit (None) | - | scream: **0.591** (scream; 0.591) |
| aspect_gd_king_shoot | "shaman attack" (literal) | shoot | Projectile | 50 (15) | 6 | 5 | - | flying(alt 0-999999), walking, swimming | projectile aspect_gdking_root_projectile x1 x 25 bursts every 0.1s, vel 30 (min 2), spread 10 | pierce 80 | shoot: **1.298** (shoot; 1.298) |
| aspect_gd_king_stomp | "jaws" (literal) | stomp0, stomp1 | Area | 3 (0) | 5 | 40 | - | flying(alt 0-999999), walking, swimming | sphere r=5 m at 3 m forward, 0 m up | blunt 150 | stomp0: **1.995** (stomp_right; 1.995); stomp1: **2.09** (stomp_left; 2.09) |

- **aspect_gd_king_rootspawn**: shared.start: sfx_gdking_spawn; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; useCharacterFacing=1
  - aspect_spawn_roots [SpawnAbility] spawns Aspect_TentaRoot x15-15 (maxSpawned 30), radius 15, circle 0, atTarget 1, groundOffset 0.2, initialDelay 0, delay 0.1 each, targetType ClosestEnemy, maxTargetRange 40, projVel 10 acc 10, spawnEffects fx_gdking_rootspawn
    - Aspect_TentaRoot (not dumped)
- **aspect_gd_king_scream**: shared.trigger: sfx_gdking_scream; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit
- **aspect_gd_king_shoot**: shared.start: sfx_gdking_shoot_start; shared.trigger: sfx_gdking_shoot_trigger; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; useCharacterFacing=1, speedFactorRotation=0.5
  - aspect_gdking_root_projectile [Projectile] type Magic|Nature, ttl 6, gravity 0, aoe r=0, dmg -, hitEffects vfx_gdking_projectile_hit, sfx_gdking_projectile_hit
- **aspect_gd_king_stomp**: shared.start: sfx_gdking_stomp; shared.trigger: sfx_gdking_footstep, vfx_gdking_stomp, sfx_gdking_rock_destroyed; hitThroughWalls=1, speedFactorRotation=1.0
- other animator triggers (not from attack items): punch -> punch (hit at 3.424; fx -)

### Aspect_Bonemass

- name: $enemy_aspect_bonemass ("Aspect of the Writhing Dead"); health 1600; m_boss 0; bossEvent ``; defeat key ``; flying 0
- MonsterAI: minAttackInterval 0.0 s; viewRange 50.0; alertRange 9999.0; sleeping 1 (wakeupRange 5.0); spawnMessage -; alertedMessage -; deathMessage -; alertedEffects sfx_Bonemass_alert; idleSound sfx_Bonemass_idle
- Animator(s): model:bonemass_animator
- m_defaultItems: aspect_bonemass_attack_aoe, aspect_bonemass_attack_punch, aspect_bonemass_attack_throw

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| aspect_bonemass_attack_aoe | "heal" (literal) | aoe | Projectile | 15 (0) | 30 | 45 | - | flying(alt 0-999999), walking, swimming | projectile aspect_bonemass_aoe x1, vel 10 (min 2), spread 10 | poison 200 | aoe: **3.3** (aoe; 3.3) |
| aspect_bonemass_attack_punch | "slap" (literal) | punch0, punch1 | Horizontal | 8 (0) | 7 | 40 | - | flying(alt 0-999999), walking, swimming | melee sweep: range 8.5 m, angle 66.1 deg, ray width 2.5, height 2.5 | blunt 100, poison 70 | punch0: **1.126** (punch_left; 1.126); punch1: **1.137** (punch_right; 1.137) |
| aspect_bonemass_attack_throw | "slime throw" (literal) | spawn | Projectile | 30 (0) | 50 | 20 | - | flying(alt 0-999999), walking, swimming | projectile aspect_bonemass_throw_projectile x1, vel 20 (min 2), spread 5 | - | spawn: **3.107** (spawn; 3.107) |

- **aspect_bonemass_attack_aoe**: shared.start: fx_Bonemass_aoe_start; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; useCharacterFacing=1, hitThroughWalls=1
  - aspect_bonemass_aoe [Aoe] sphere r=9, useAttackSettings 1, activationDelay 0, ttl 15, hitInterval 1, dmg -
- **aspect_bonemass_attack_punch**: shared.start: sfx_Bonemass_punch_start; shared.hit: sfx_MudDestroyed, vfx_MudDestroyed [DISABLED]; hitThroughWalls=1, speedFactor=0.5, speedFactorRotation=1.0
- **aspect_bonemass_attack_throw**: shared.start: sfx_Bonemass_throw_start; shared.trigger: sfx_Bonemass_throw_trigger; shared.hit: vfx_HitSparks [DISABLED], sfx_greydwarf_attack_hit [DISABLED]; useCharacterFacing=1
  - aspect_bonemass_throw_projectile [Projectile] type Physical, ttl 4, gravity 10, aoe r=0, dmg -, spawnOnHit aspect_bonemass_spawn (x1), hitEffects sfx_ProjectileHit, vfx_ProjectileHit, sfx_troll_attack_hit
    - aspect_bonemass_spawn [SpawnAbility] spawns Skeleton_aspect, BlobAspect x4-4 (maxSpawned 8), radius 2, circle 0, atTarget 1, groundOffset 0.2, initialDelay 0, delay 0 each, targetType Position, maxTargetRange 40, projVel 10 acc 10, spawnEffects sfx_DraugrSpawn, vfx_DraugrSpawn
      - Skeleton_aspect: creature $enemy_skeleton ("Skeleton"), hp 40
      - BlobAspect: creature $enemy_blob ("Blob"), hp 50

### Aspect_Moder

- name: $enemy_aspect_dragon ("Aspect of the Dragon Mother"); health 1500; m_boss 0; bossEvent ``; defeat key ``; flying 1
- MonsterAI: minAttackInterval 2.0 s; viewRange 60.0; alertRange 30.0; sleeping 0 (wakeupRange 5.0); spawnMessage -; alertedMessage -; deathMessage -; alertedEffects sfx_dragon_alerted; idleSound sfx_dragon_idle
- Animator(s): Visual:dragon_animator
- m_defaultItems: aspect_dragon_taunt, aspect_dragon_bite, aspect_dragon_claw_left, aspect_dragon_claw_right, aspect_dragon_spit_shotgun, aspect_dragon_coldbreath

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| aspect_dragon_bite | "Dragon claw left" (literal) | attack_bite | Horizontal | 7 (0) | 30 | 10 | - | walking, swimming | melee sweep: range 8 m, angle 20 deg, ray width 2, height 2 | pierce 150 | attack_bite: **0.989** (bite; 0.989) |
| aspect_dragon_claw_left | "Dragon claw left" (literal) | attack_claw_left | Horizontal | 10 (0) | 30 | 30 | - | walking, swimming | melee sweep: range 12 m, angle 50 deg, ray width 2, height 2 | slash 160 | attack_claw_left: **1.569** (claw_left; 1.569) |
| aspect_dragon_claw_right | "Dragon claw left" (literal) | attack_claw_right | Horizontal | 10 (0) | 30 | 30 | - | walking, swimming | melee sweep: range 12 m, angle 50 deg, ray width 2, height 2 | slash 160 | attack_claw_right: **1.601** (claw_right; 1.601) |
| aspect_dragon_coldbreath | "dragon breath" (literal) | attack_breath | Horizontal | 20 (5) | 8 | 5 | - | walking | melee sweep: range 30 m, angle 10 deg, ray width 2, height 1, maxYAngle 25 | frost 180 | attack_breath: **1.366** (cold breath; 1.366) |
| aspect_dragon_spit_shotgun | "cold ball" (literal) | attack_iceball | Projectile | 25 (5) | 8 | 5 | - | flying(alt 0-999999) | projectile dragon_ice_projectile x1 x 16 bursts every 0.05s, vel 25 (min 2), spread 13 | pierce 50, frost 100 | attack_iceball: **0.89** (attack_iceball; 0.89) |
| aspect_dragon_taunt | "scream" (literal) | attack_taunt | None | 50 (10) | 30 | 5 | - | walking | no hit (None) | - | attack_taunt: **1.96** (taunt; 1.96) |

- **aspect_dragon_bite**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **aspect_dragon_claw_left**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=1.0
- **aspect_dragon_claw_right**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=1.0
- **aspect_dragon_coldbreath**: shared.start: sfx_dragon_coldbreath_start, vfx_greydwarf_shaman_pray [DISABLED,attach]; shared.trailStart: vfx_dragon_coldbreath, sfx_dragon_coldbreath_trailon; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; attackOriginJoint=Jaw, useCharacterFacing=1, hitThroughWalls=1, speedFactorRotation=1.0
- **aspect_dragon_spit_shotgun**: shared.start: sfx_dragon_coldball_start; shared.trigger: sfx_dragon_coldball_launch, vfx_ColdBall_launch; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; attackOriginJoint=Jaw, speedFactor=0.2, speedFactorRotation=1.0
  - dragon_ice_projectile [Projectile] type Magic|Frost, ttl 10, gravity 0, aoe r=0, dmg -, spawnOnHit IceBlocker (x1), hitEffects vfx_dragon_ice_hit, sfx_dragon_coldball_explode
    - IceBlocker [Destructible] destructibleType=1, health=10.0, ttl=30.0, destroyedEffect=[vfx_iceblocker_destroyed, sfx_ice_destroyed], hitEffect=[vfx_ice_hit, sfx_ice_hit]
      - vfx_iceblocker_destroyed (no Projectile/Aoe/SpawnAbility component)
      - sfx_ice_destroyed (no Projectile/Aoe/SpawnAbility component)
      - vfx_ice_hit (no Projectile/Aoe/SpawnAbility component)
      - sfx_ice_hit (no Projectile/Aoe/SpawnAbility component)
- **aspect_dragon_taunt**: shared.trigger: sfx_dragon_scream; speedFactor=0.2, speedFactorRotation=0.2
- other animator triggers (not from attack items): fly_land -> Land (hit at None; fx -); fly_takeoff -> Takeoff (hit at None; fx sfx_dragon_flap@1.27s, sfx_dragon_flap@2.99s)

### Aspect_Yagluth

- name: $enemy_aspect_goblinking ("Aspect of the Twisted Soul"); health 1700; m_boss 0; bossEvent ``; defeat key ``; flying 0
- MonsterAI: minAttackInterval 0.0 s; viewRange 30.0; alertRange 20.0; sleeping 1 (wakeupRange 5.0); spawnMessage -; alertedMessage -; deathMessage -; alertedEffects -; idleSound -
- Animator(s): Visual:Aspect_Yagluth_Animator
- m_defaultItems: aspect_GoblinKing_Beam, aspect_GoblinKing_Nova, aspect_GoblinKing_Taunt

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| aspect_GoblinKing_Beam | "dragon breath" (literal) | beam | Projectile | 40 (10) | 15 | 20 | - | flying(alt 0-999999), walking, swimming | projectile aspect_projectile_beam x1 x 20 bursts every 0.1s, vel 40 (min 30), spread 1 | fire 40, lightning 40 | beam: **2.021** (beam; 2.021) |
| aspect_GoblinKing_Nova | "slap" (literal) | nova | Area | 10 (0) | 20 | 90 | - | flying(alt 0-999999), walking, swimming | sphere r=8 m at 4.51 m forward, 0.36 m up | fire 75, lightning 90 | nova: **2.811** (nova; 2.811) |
| aspect_GoblinKing_Taunt | "scream" (literal) | taunt | None | 50 (10) | 60 | 5 | - | prioritized, flying(alt 0-999999), walking, swimming | no hit (None) | - | taunt: **1.729** (Taunt; 1.729) |

- **aspect_GoblinKing_Beam**: shared.trigger: sfx_goblinking_beam; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; attackOriginJoint=BeamRoot, useCharacterFacing=1, speedFactorRotation=2.0
  - aspect_projectile_beam [Projectile] type Magic, ttl 3, gravity 0, aoe r=0, dmg -, hitEffects fx_goblinking_beam_hit
- **aspect_GoblinKing_Nova**: shared.trigger: fx_goblinking_nova; shared.trailStart: fx_goblinking_vo_nova; shared.hit: vfx_troll_attack_hit, sfx_troll_attack_hit; hitThroughWalls=1
  - aspect_aoe_nova [Aoe] sphere r=10, useAttackSettings 0, activationDelay 0, ttl 10, hitInterval 1, dmg fire 250
- **aspect_GoblinKing_Taunt**: shared.trigger: sfx_goblinking_taunt; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit
- other animator triggers (not from attack items): cast1 -> cast1 (hit at 1.909; fx fx_goblinking_vo_meteors1@0.59s)

### Aspect_SeekerQueen

- name: $enemy_aspect_seekerqueen ("Aspect of the Crawling Matriarch"); health 1700; m_boss 0; bossEvent ``; defeat key ``; flying 0
- MonsterAI: minAttackInterval 0.5 s; viewRange 30.0; alertRange 30.0; sleeping 0 (wakeupRange 5.0); spawnMessage -; alertedMessage -; deathMessage -; alertedEffects sfx_HiveQueen_alerted; idleSound sfx_HiveQueen_idle
- Animator(s): Visual:SeekerQueen_animator
- m_defaultItems: aspect_SeekerQueen_Rush, aspect_SeekerQueen_Bite, aspect_SeekerQueen_Spit, aspect_SeekerQueen_Slap, aspect_SeekerQueen_PierceAOE

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| aspect_SeekerQueen_Bite | "slap" (literal) | attack_bite | Horizontal | 10 (0) | 10 | 45 | 0-70% | walking, swimming | melee sweep: range 10 m, angle 25 deg, ray width 2, height 1 | pierce 100, poison 100 | attack_bite: **1.819** (Attack Bite; 1.819) |
| aspect_SeekerQueen_PierceAOE | "slap" (literal) | attack_pierce | Area | 4 (0) | 4 | 90 | - | walking, swimming | sphere r=4.5 m at 0 m forward, 0.24 m up | pierce 150 | attack_pierce: **0.857** (Attack Pierce; 0.857, 0.96) |
| aspect_SeekerQueen_Rush | "slap" (literal) | attack_rush | Horizontal | 25 (5) | 25 | 40 | 0-60% | walking, swimming | melee sweep: range 8 m, angle 120 deg, ray width 2.5, height 1 | slash 100 | attack_rush: **1.443** (Attack Rush; 1.443) |
| aspect_SeekerQueen_Slap | "slap" (literal) | attack_slash0, attack_slash1, attack_slash2, attack_slash3 | Horizontal | 9 (0) | 4 | 60 | - | walking, swimming | melee sweep: range 10 m, angle 145 deg, ray width 2.5, height 1 | slash 130 | attack_slash1: **1.063** (Attack slash 1; 1.063); attack_slash2: **1.069** (Attack slash 2; 1.069); attack_slash3: **1.024** (Attack slash 3; 1.024) |
| aspect_SeekerQueen_Spit | "dragon breath" (literal) | attack_spit | Projectile | 25 (5) | 20 | 25 | 0-80% | flying(alt 0-999999), walking, swimming | projectile aspect_SeekerQueen_projectile_spit x1 x 20 bursts every 0.05s, vel 15 (min 0), spread 5 | blunt 40, poison 40 | attack_spit: **1.479** (Attack Spit; 1.479) |

- **aspect_SeekerQueen_Bite**: shared.start: sfx_dragon_melee_start; hitThroughWalls=1, speedFactorRotation=0.5
- **aspect_SeekerQueen_PierceAOE**: shared.start: sfx_dragon_melee_start; shared.trigger: fx_QueenPierceGround [variant=0]; hitThroughWalls=1, speedFactorRotation=0.5
- **aspect_SeekerQueen_Rush**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; hitThroughWalls=1, speedFactorRotation=0.5
- **aspect_SeekerQueen_Slap**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit [DISABLED]; hitThroughWalls=1, speedFactorRotation=0.5
- **aspect_SeekerQueen_Spit**: shared.trigger: sfx_goblinking_beam; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; attackOriginJoint=PukePoint, speedFactorRotation=2.0
  - aspect_SeekerQueen_projectile_spit [Projectile] type Physical, ttl 4, gravity 10, aoe r=1.5, dmg -, spawnOnHit aspect_SeekerQueen_SpitSpawnAbility (x1), hitEffects SeekerQueen_spithit
    - aspect_SeekerQueen_SpitSpawnAbility [SpawnAbility] spawns SeekerBrood x1-1 (maxSpawned 30), radius 1, circle 0, atTarget 0, groundOffset 0.2, initialDelay 0, delay 0 each, targetType Position, maxTargetRange 40, projVel 10 acc 10
      - SeekerBrood: creature $enemy_babyseeker ("Seeker Brood"), hp 20
- other animator triggers (not from attack items): attack_call -> Attack Call (hit at 2.918; fx sfx_HiveQueen_callout@0.00s); attack_slash4 -> Attack slash 4 (hit at 1.027; fx sfx_HiveQueen_slash@0.00s); attack_teleport -> Teleport (hit at 2.535; fx sfx_HiveQueen_burrow@0.00s, fx_Queen_BurrowDown@0.48s); taunt -> Taunt (hit at None; fx sfx_seeker_brute_taunt@0.00s, sfx_HiveQueen_move@2.42s)

### Aspect_Fader

- name: $enemy_aspect_fader ("Aspect of the Emerald Flame"); health 1700; m_boss 0; bossEvent ``; defeat key ``; flying 0
- MonsterAI: minAttackInterval 0.5 s; viewRange 30.0; alertRange 100.0; sleeping 0 (wakeupRange 5.0); spawnMessage -; alertedMessage -; deathMessage -; alertedEffects -; idleSound sfx_fader_idle
- Animator(s): Visual:Fader_animator
- m_defaultItems: aspect_Fader_Fissure, aspect_Fader_Bite, aspect_Fader_Claw_Left, aspect_Fader_Claw_Right, aspect_Fader_Spin, aspect_Fader_Flamebreath, aspect_Fader_WallOfFire

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| aspect_Fader_Bite | "Fader Bite" (literal) | attack_bite | Horizontal | 9 (0) | 3 | 10 | - | walking, swimming | melee sweep: range 10 m, angle 40 deg, ray width 2, height 2 | pierce 180 | attack_bite: **1.27** (Attack Bite; 1.27) |
| aspect_Fader_Claw_Left | "Fader Claw Left" (literal) | attack_ClawL | Horizontal | 9 (0) | 3 | 10 | - | walking, swimming | melee sweep: range 10 m, angle 65 deg, ray width 1.33, height 2 | pierce 170 | attack_ClawL: **1.236** (Attack Claw L; 1.236) |
| aspect_Fader_Claw_Right | "Fader Claw Right" (literal) | attack_ClawR | Horizontal | 9 (0) | 3 | 10 | - | walking, swimming | melee sweep: range 10 m, angle 65 deg, ray width 1.33, height 2 | pierce 170 | attack_ClawR: **1.236** (Attack Claw R; 1.236) |
| aspect_Fader_Fissure | "Fader Fissure" (literal) | attack_Fissure | Projectile | 40 (0) | 30 | 180 | 0-85% | walking, swimming | projectile aspect_Fader_Fissure_Spawn x1, vel 0 (min 0), spread 30 | pierce 100 | attack_Fissure: **2.879** (Attack Fissure; 2.879) |
| aspect_Fader_Flamebreath | "Fader Firebreath" (literal) | attack_flamebreath | Projectile | 20 (2) | 25 | 15 | 0-85% | walking, swimming | projectile aspect_Fader_Flamebreath_AOE x1, vel 10 (min 2), spread 10 | fire 50, spirit 50 | attack_flamebreath: **2.341** (Attack Flamebreath; 2.341) |
| aspect_Fader_Spin | "Fader Spin" (literal) | attack_Spin | Area | 8 (0) | 20 | 360 | - | walking, swimming | sphere r=8.5 m at 0 m forward, 2 m up | pierce 140 | attack_Spin: **1.3** (Attack Spin; 1.3) |
| aspect_Fader_WallOfFire | "Fader Wall of Fire" (literal) | attack_WallOfFire | Projectile | 40 (0) | 60 | 180 | 0-90% | walking, swimming | projectile aspect_Fader_WallOfFire_Spawn x1, vel 0 (min 0), spread 0 | pierce 120 | attack_WallOfFire: **1.425** (Attack Wall of Fire; 1.425) |

- **aspect_Fader_Bite**: shared.hit: vfx_HitSparks; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **aspect_Fader_Claw_Left**: shared.hit: vfx_HitSparks; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **aspect_Fader_Claw_Right**: shared.hit: vfx_HitSparks; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **aspect_Fader_Fissure**: shared.start: sfx_dragon_melee_start; shared.trigger: sfx_fader_fissure; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
  - aspect_Fader_Fissure_Spawn [SpawnAbility] spawns aspect_Fader_Fissure_AOE x12-16 (maxSpawned 0), radius 0, circle 1, atTarget 1, groundOffset 0.2, initialDelay 0, delay 0.45 each, targetType RandomEnemy, maxTargetRange 120, projVel 10 acc 10
    - aspect_Fader_Fissure_AOE [Aoe] sphere r=11, useAttackSettings 0, activationDelay 4, ttl 15, hitInterval 0.5, dmg fire 80, spirit 40
- **aspect_Fader_Flamebreath**: shared.trigger: fx_fallenvalkyrie_poisonbreath [variant=0]; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; useCharacterFacing=1, hitThroughWalls=1, speedFactorRotation=0.1
  - aspect_Fader_Flamebreath_AOE [Aoe] sphere r=4 (useTriggers: trigger box 3x5x39.45), useAttackSettings 0, activationDelay 1, ttl 14, hitInterval 0.5, dmg fire 60
- **aspect_Fader_Spin**: shared.trigger: fx_Fader_Spin; shared.hit: vfx_HitSparks; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
- **aspect_Fader_WallOfFire**: shared.start: sfx_dragon_melee_start; shared.hit: vfx_HitSparks, sfx_dragon_melee_hit; hitThroughWalls=1, speedFactor=0.2, speedFactorRotation=0.5
  - aspect_Fader_WallOfFire_Spawn [SpawnAbility] spawns aspect_Fader_WallOfFire_AOE x12-12 (maxSpawned 0), radius 8, circle 1, atTarget 1, groundOffset 0.2, initialDelay 0, delay 0.25 each, targetType ClosestEnemy, maxTargetRange 40, projVel 10 acc 10, spawnEffects sfx_fader_firewall_fireburst
    - aspect_Fader_WallOfFire_AOE [Aoe] sphere r=4 (useTriggers: trigger sphere r=2), useAttackSettings 0, activationDelay 0, ttl 20, hitInterval 0.5, dmg fire 80, spirit 80
- other animator triggers (not from attack items): attack_roar -> Attack Roar (hit at 1.528; fx sfx_fader_charredsummon_roar@0.11s); jump_forward -> Jump Forward (hit at 2.603; fx sfx_fader_bite_snarl@2.32s, fx_Fader_Bite@2.39s); jump_left -> Jump Left (hit at 1.082; fx -); jump_right -> Jump Right (hit at 1.055; fx -); taunt -> Taunt (hit at 1.165; fx sfx_fader_meteor_start@0.03s)

### Aspect_TentaRoot

- name: $enemy_root ("Root"); health 20; m_boss 0; bossEvent ``; defeat key ``; flying 0
- MonsterAI: minAttackInterval 0.0 s; viewRange 30.0; alertRange 20.0; sleeping 0 (wakeupRange 5.0); spawnMessage -; alertedMessage -; deathMessage -; alertedEffects -; idleSound -
- Animator(s): Tentaroots:tentaroot
- m_defaultItems: ; m_randomWeapon: tentaroot_attack

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| tentaroot_attack | "Dragur axe" (literal) | attack | Vertical | 4 (0) | 3 | 5 | - | flying(alt 0-999999), walking, swimming | melee sweep: range 4 m, angle 90 deg, ray width 1, height 1.5 | blunt 55 | attack: **1.351** (punch; 1.351) |

- **tentaroot_attack**: shared.start: sfx_tentaroot_attack; shared.trigger: sfx_gdking_projectile_hit, vfx_tentaroot_hit; shared.hit: vfx_greydwarf_hit, sfx_greydwarf_attack_hit; speedFactorRotation=2.0

### Hive

- name: $enemy_hive (no English string); health 10000; m_boss 1; bossEvent `boss_hive`; defeat key `defeated_hive`; flying 0
- MonsterAI: minAttackInterval 0.0 s; viewRange 50.0; alertRange 9999.0; sleeping 1 (wakeupRange 5.0); spawnMessage $boss_hive_start ("We have gathered."); alertedMessage -; deathMessage $boss_hive_end ("We are dispersed."); alertedEffects sfx_Bonemass_alert; idleSound sfx_Bonemass_idle
- Animator(s): model:hive_animator
- m_defaultItems: hive_attack_aoe, hive_attack_punch, hive_attack_throw, hive_attack_ranged

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| hive_attack_aoe | "heal" (literal) | aoe | Projectile | 15 (0) | 30 | 45 | - | flying(alt 0-999999), walking, swimming | projectile bonemass_aoe x1, vel 10 (min 2), spread 10 | spirit 200 | aoe: **3.3** (aoe; 3.3) |
| hive_attack_punch | "slap" (literal) | punch0, punch1 | Horizontal | 8 (0) | 7 | 40 | - | flying(alt 0-999999), walking, swimming | melee sweep: range 8.5 m, angle 66.1 deg, ray width 2.5, height 2.5 | blunt 100, spirit 90 | punch0: **1.126** (punch_left; 1.126); punch1: **1.137** (punch_right; 1.137) |
| hive_attack_ranged | "dragon breath" (literal) | ranged | Horizontal | 25 (5) | 6 | 20 | - | walking | melee sweep: range 30 m, angle 10 deg, ray width 2, height 1, maxYAngle 25 | spirit 200 | ranged: **3.107** (ranged; 3.107) |
| hive_attack_throw | "slime throw" (literal) | spawn | Projectile | 30 (0) | 50 | 20 | - | flying(alt 0-999999), walking, swimming | projectile hive_throw_projectile x1, vel 20 (min 2), spread 5 | - | spawn: **3.107** (spawn; 3.107) |

- **hive_attack_aoe**: shared.start: fx_Bonemass_aoe_start; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; useCharacterFacing=1, hitThroughWalls=1
  - bonemass_aoe [Aoe] sphere r=9, useAttackSettings 1, activationDelay 0, ttl 15, hitInterval 1, dmg -
- **hive_attack_punch**: shared.start: sfx_Bonemass_punch_start; shared.hit: sfx_MudDestroyed, vfx_MudDestroyed [DISABLED]; hitThroughWalls=1, speedFactor=0.5, speedFactorRotation=1.0
- **hive_attack_ranged**: shared.start: sfx_dragon_coldbreath_start, vfx_greydwarf_shaman_pray [DISABLED,attach]; shared.trailStart: vfx_dragon_coldbreath, sfx_dragon_coldbreath_trailon; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; attackOriginJoint=l_hand, useCharacterFacing=1, hitThroughWalls=1, speedFactorRotation=1.0
- **hive_attack_throw**: shared.start: sfx_Bonemass_throw_start; shared.trigger: sfx_Bonemass_throw_trigger; shared.hit: vfx_HitSparks [DISABLED], sfx_greydwarf_attack_hit [DISABLED]; useCharacterFacing=1
  - hive_throw_projectile [Projectile] type Physical, ttl 4, gravity 10, aoe r=0, dmg -, spawnOnHit hive_spawn (x1), hitEffects sfx_ProjectileHit, vfx_ProjectileHit, sfx_troll_attack_hit
    - hive_spawn [SpawnAbility] spawns Seeker, Tick x4-4 (maxSpawned 8), radius 2, circle 0, atTarget 1, groundOffset 0.2, initialDelay 0, delay 0 each, targetType Position, maxTargetRange 40, projVel 10 acc 10, spawnEffects sfx_DraugrSpawn, vfx_DraugrSpawn
      - Seeker: creature $enemy_seeker ("Seeker"), hp 200
      - Tick: creature $enemy_tick ("Tick"), hp 50

### TheHive

- name: $enemy_thehive (no English string); health 5000; m_boss 1; bossEvent ``; defeat key ``; flying 1
- MonsterAI: minAttackInterval 0.0 s; viewRange 100.0; alertRange 50.0; sleeping 0 (wakeupRange 5.0); spawnMessage -; alertedMessage -; deathMessage -; alertedEffects sfx_hatchling_alerted; idleSound sfx_hatchling_idle
- Animator(s): Hatchling_mountain:hatchling_animator
- m_defaultItems: gjall_attack_spit, gjall_attack_egg

| item prefab | m_name | anim trigger(s) | type | AI range (min) | AI interval | AI max angle | HP gate | flags | geometry | damage | wind-up s (state; all hit times) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| gjall_attack_egg | "egg drop" (literal) | attack_eggs | Projectile | 20 (10) | 50 | 180 | - | flying(alt 0-999999), walking, swimming | projectile gjall_egg_projectile x1 x 3 bursts every 0.5s, vel 2 (min 0), spread 45 | - | n/a |
| gjall_attack_spit | "gjall spit" (literal) | attack_spit | Projectile | 30 (6) | 6 | 30 | - | flying(alt 0-999999), swimming | projectile gjall_spit_projectile x1 x 2 bursts every 0.7s, vel 15 (min 2), spread 10 | blunt 50, fire 80 | n/a |

- **gjall_attack_egg**: shared.start: sfx_Bonemass_throw_start; shared.trigger: sfx_Bonemass_throw_trigger; shared.hit: vfx_HitSparks [DISABLED], sfx_greydwarf_attack_hit [DISABLED]; useCharacterFacing=1, launchAngle=90.0
  - gjall_egg_projectile [Projectile] type Physical, ttl 4, gravity 10, aoe r=0, dmg -, spawnOnHit gjall_egg_spawn (x1), hitEffects fx_gjall_egg_splat
    - gjall_egg_spawn [SpawnAbility] spawns Tick x1-1 (maxSpawned 8), radius 2, circle 0, atTarget 0, groundOffset 0.2, initialDelay 0, delay 0 each, targetType Position, maxTargetRange 40, projVel 10 acc 10, spawnEffects sfx_DraugrSpawn [DISABLED], vfx_DraugrSpawn [DISABLED]
      - Tick: creature $enemy_tick ("Tick"), hp 50
- **gjall_attack_spit**: shared.start: sfx_hatchling_coldball_start [DISABLED]; shared.trigger: sfx_hatchling_coldball_launch [DISABLED], vfx_gjall_spit; shared.hit: vfx_HitSparks, sfx_greydwarf_attack_hit; speedFactorRotation=1.0
  - gjall_spit_projectile [Projectile] type Physical, ttl 10, gravity 1, aoe r=4, dmg -, hitEffects vfx_hjall_spit_hit
- other animator triggers (not from attack items): attack -> attack (hit at 1.103; fx -); fly_land -> Land (hit at None; fx -); fly_takeoff -> Takeoff (hit at None; fx sfx_hatchling_flap@2.37s, sfx_hatchling_flap@3.31s)

## 8. Sound cross-reference (generated; every ZSFX-bearing prefab used by a boss attack or attack animation)

| sound prefab | maxDistance | caption token | loop | where it plays |
|---|---|---|---|---|
| fx_Bonemass_aoe_start | 100.0 | - |  | Aspect_Bonemass aspect_bonemass_attack_aoe: shared startEffect<br>Bonemass bonemass_attack_aoe: shared startEffect<br>Hive hive_attack_aoe: shared startEffect |
| fx_QueenPierceGround | 60.0 | - |  | Aspect_SeekerQueen aspect_SeekerQueen_PierceAOE: shared triggerEffect<br>SeekerQueen SeekerQueen_PierceAOE: shared triggerEffect |
| fx_Queen_BurrowDown | 40.0 | - |  | Aspect_SeekerQueen: anim trigger attack_teleport (not an attack item) at 0.48s<br>SeekerQueen SeekerQueen_Teleport: anim event attack_teleport at 0.48s (pre-hit) |
| fx_eikthyr_forwardshockwave | 30.0 | - |  | Aspect_Eikthyr aspect_Eikthyr_charge: shared triggerEffect<br>Eikthyr Eikthyr_charge: shared triggerEffect |
| fx_eikthyr_stomp | 30.0 | - |  | Aspect_Eikthyr aspect_Eikthyr_stomp: shared triggerEffect<br>Eikthyr Eikthyr_stomp: shared triggerEffect |
| fx_fallenvalkyrie_poisonbreath | 30.0 | - | yes | Aspect_Fader aspect_Fader_Flamebreath: shared triggerEffect<br>Fader Fader_Flamebreath: shared triggerEffect |
| fx_frozenking_chain_ground_impact_1 | 120.0 | $sfx_frozenking |  | FrozenKing FrozenKing_ChainSlam_L: shared hitEffect<br>FrozenKing FrozenKing_ChainSlam_L_double: shared hitEffect<br>FrozenKing FrozenKing_ChainSlam_R: shared hitEffect<br>FrozenKing FrozenKing_ChainSlam_R_double: shared hitEffect<br>FrozenKing FrozenKing_DoubleSweep: shared hitEffect<br>FrozenKing_p3 FrozenKing_DoubleSweep: shared hitEffect<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_L_double: shared hitEffect<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_R_double: shared hitEffect |
| fx_frozenking_chain_sparks | 120.0 | $sfx_frozenking |  | FrozenKing FrozenKing_ChainSlam_L: shared startEffect<br>FrozenKing FrozenKing_ChainSlam_L_double: shared startEffect<br>FrozenKing FrozenKing_ChainSlam_R: shared startEffect<br>FrozenKing FrozenKing_ChainSlam_R_double: shared startEffect<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_L_double: shared startEffect<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_R_double: shared startEffect |
| fx_frozenking_punch_aoe | 30.0 | - |  | FrozenKing_p3 FrozenKing_Punch_AOE: shared triggerEffect |
| fx_frozenking_spin | 120.0 | $enemy_frozenking |  | FrozenKing_p3 FrozenKing_P3_ChainWhirl: shared startEffect |
| fx_goblinking_nova | 80.0 | - |  | Aspect_Yagluth aspect_GoblinKing_Nova: shared triggerEffect<br>GoblinKing GoblinKing_Nova: shared triggerEffect |
| fx_goblinking_nova_hand | 50.0 | - |  | Aspect_Yagluth aspect_GoblinKing_Nova: anim event nova at 0.78s (pre-hit)<br>GoblinKing GoblinKing_Nova: anim event nova at 0.78s (pre-hit) |
| fx_goblinking_vo_beamstart | 50.0 | - |  | Aspect_Yagluth aspect_GoblinKing_Beam: anim event beam at 1.13s (pre-hit)<br>GoblinKing GoblinKing_Beam: anim event beam at 1.13s (pre-hit) |
| fx_goblinking_vo_meteors1 | 50.0 | - |  | Aspect_Yagluth: anim trigger cast1 (not an attack item) at 0.59s<br>GoblinKing GoblinKing_Meteors: anim event cast1 at 0.59s (pre-hit) |
| fx_goblinking_vo_meteors2 | 60.0 | - |  | Fader Fader_Meteors: shared trailStartEffect<br>Fader Fader_Meteors_Intense: shared trailStartEffect<br>FrozenKing_p3 FrozenKing_SpikeRain: shared trailStartEffect<br>GoblinKing GoblinKing_Meteors: shared trailStartEffect |
| fx_goblinking_vo_nova | 40.0 | - |  | Aspect_Yagluth aspect_GoblinKing_Nova: anim event nova at 0.67s (pre-hit)<br>Aspect_Yagluth aspect_GoblinKing_Nova: shared trailStartEffect<br>GoblinKing GoblinKing_Nova: anim event nova at 0.67s (pre-hit)<br>GoblinKing GoblinKing_Nova: shared trailStartEffect |
| sfx_Bonemass_punch_start | 100.0 | $enemy_bonemass |  | Aspect_Bonemass aspect_bonemass_attack_punch: shared startEffect<br>Bonemass bonemass_attack_punch: shared startEffect<br>Hive hive_attack_punch: shared startEffect |
| sfx_Bonemass_throw_start | 100.0 | $enemy_bonemass |  | Aspect_Bonemass aspect_bonemass_attack_throw: shared startEffect<br>Bonemass bonemass_attack_throw: shared startEffect<br>Hive hive_attack_throw: shared startEffect<br>TheHive gjall_attack_egg: shared startEffect |
| sfx_Bonemass_throw_trigger | 100.0 | - |  | Aspect_Bonemass aspect_bonemass_attack_throw: shared triggerEffect<br>Bonemass bonemass_attack_throw: shared triggerEffect<br>Hive hive_attack_throw: shared triggerEffect<br>TheHive gjall_attack_egg: shared triggerEffect |
| sfx_HiveQueen_acitspit | 40.0 | $enemy_seekerqueen |  | Aspect_SeekerQueen aspect_SeekerQueen_Spit: anim event attack_spit at 0.00s (pre-hit)<br>SeekerQueen SeekerQueen_Spit: anim event attack_spit at 0.00s (pre-hit) |
| sfx_HiveQueen_bite | 40.0 | $enemy_seekerqueen |  | Aspect_SeekerQueen aspect_SeekerQueen_Bite: anim event attack_bite at 0.00s (pre-hit)<br>SeekerQueen SeekerQueen_Bite: anim event attack_bite at 0.00s (pre-hit) |
| sfx_HiveQueen_burrow | 40.0 | $enemy_seekerqueen |  | Aspect_SeekerQueen: anim trigger attack_teleport (not an attack item) at 0.00s<br>SeekerQueen SeekerQueen_Teleport: anim event attack_teleport at 0.00s (pre-hit) |
| sfx_HiveQueen_callout | 40.0 | $enemy_seekerqueen |  | Aspect_SeekerQueen: anim trigger attack_call (not an attack item) at 0.00s<br>SeekerQueen SeekerQueen_Call: anim event attack_call at 0.00s (pre-hit) |
| sfx_HiveQueen_move | 40.0 | - |  | Aspect_SeekerQueen: anim trigger taunt (not an attack item) at 2.42s<br>SeekerQueen: anim trigger taunt (not an attack item) at 2.42s |
| sfx_HiveQueen_pierce | 40.0 | - |  | Aspect_SeekerQueen aspect_SeekerQueen_PierceAOE: anim event attack_pierce at 0.00s (pre-hit)<br>SeekerQueen SeekerQueen_PierceAOE: anim event attack_pierce at 0.00s (pre-hit) |
| sfx_HiveQueen_rush | 40.0 | $enemy_seekerqueen |  | Aspect_SeekerQueen aspect_SeekerQueen_Rush: anim event attack_rush at 0.00s (pre-hit)<br>SeekerQueen SeekerQueen_Rush: anim event attack_rush at 0.00s (pre-hit) |
| sfx_HiveQueen_slash | 40.0 | $enemy_seekerqueen |  | Aspect_SeekerQueen aspect_SeekerQueen_Slap: anim event attack_slash1 at 0.00s (pre-hit)<br>Aspect_SeekerQueen aspect_SeekerQueen_Slap: anim event attack_slash2 at 0.00s (pre-hit)<br>Aspect_SeekerQueen aspect_SeekerQueen_Slap: anim event attack_slash3 at 0.00s (pre-hit)<br>Aspect_SeekerQueen: anim trigger attack_slash4 (not an attack item) at 0.00s<br>SeekerQueen SeekerQueen_Slap: anim event attack_slash1 at 0.00s (pre-hit)<br>SeekerQueen SeekerQueen_Slap: anim event attack_slash2 at 0.00s (pre-hit)<br>SeekerQueen SeekerQueen_Slap: anim event attack_slash3 at 0.00s (pre-hit)<br>SeekerQueen: anim trigger attack_slash4 (not an attack item) at 0.00s |
| sfx_MudDestroyed | 30.0 | - |  | Aspect_Bonemass aspect_bonemass_attack_punch: shared hitEffect<br>Bonemass bonemass_attack_punch: shared hitEffect<br>Hive hive_attack_punch: shared hitEffect |
| sfx_dragon_coldball_launch | 50.0 | - |  | Aspect_Moder aspect_dragon_spit_shotgun: shared triggerEffect<br>Dragon dragon_spit_shotgun: shared triggerEffect |
| sfx_dragon_coldball_start | 50.0 | $enemy_dragon |  | Aspect_Moder aspect_dragon_spit_shotgun: shared startEffect<br>Dragon dragon_spit_shotgun: shared startEffect |
| sfx_dragon_coldbreath_start | 50.0 | $enemy_dragon |  | Aspect_Moder aspect_dragon_coldbreath: shared startEffect<br>Dragon dragon_coldbreath: shared startEffect<br>Hive hive_attack_ranged: shared startEffect |
| sfx_dragon_coldbreath_trailon | 100.0 | - |  | Aspect_Moder aspect_dragon_coldbreath: shared trailStartEffect<br>Dragon dragon_coldbreath: shared trailStartEffect<br>Hive hive_attack_ranged: shared trailStartEffect |
| sfx_dragon_flap | 100.0 | $enemy_dragon |  | Aspect_Moder: anim trigger fly_takeoff (not an attack item) at 1.27s<br>Aspect_Moder: anim trigger fly_takeoff (not an attack item) at 2.99s<br>Dragon: anim trigger fly_takeoff (not an attack item) at 1.27s<br>Dragon: anim trigger fly_takeoff (not an attack item) at 2.99s |
| sfx_dragon_melee_hit | 50.0 | - |  | Aspect_Fader aspect_Fader_Fissure: shared hitEffect<br>Aspect_Fader aspect_Fader_WallOfFire: shared hitEffect<br>Aspect_Moder aspect_dragon_bite: shared hitEffect<br>Aspect_Moder aspect_dragon_claw_left: shared hitEffect<br>Aspect_Moder aspect_dragon_claw_right: shared hitEffect<br>Aspect_SeekerQueen aspect_SeekerQueen_Rush: shared hitEffect<br>Dragon dragon_bite: shared hitEffect<br>Dragon dragon_claw_left: shared hitEffect<br>Dragon dragon_claw_right: shared hitEffect<br>Fader Fader_Fissure: shared hitEffect<br>Fader Fader_Fissure_Intense: shared hitEffect<br>Fader Fader_Roar: shared hitEffect<br>Fader Fader_Roar_Intense: shared hitEffect<br>Fader Fader_WallOfFire: shared hitEffect<br>SeekerQueen SeekerQueen_Rush: shared hitEffect |
| sfx_dragon_melee_start | 50.0 | $enemy_dragon |  | Aspect_Fader aspect_Fader_Fissure: shared startEffect<br>Aspect_Fader aspect_Fader_WallOfFire: shared startEffect<br>Aspect_Moder aspect_dragon_bite: shared startEffect<br>Aspect_Moder aspect_dragon_claw_left: shared startEffect<br>Aspect_Moder aspect_dragon_claw_right: shared startEffect<br>Aspect_SeekerQueen aspect_SeekerQueen_Bite: shared startEffect<br>Aspect_SeekerQueen aspect_SeekerQueen_PierceAOE: shared startEffect<br>Aspect_SeekerQueen aspect_SeekerQueen_Rush: shared startEffect<br>Aspect_SeekerQueen aspect_SeekerQueen_Slap: shared startEffect<br>Dragon dragon_bite: shared startEffect<br>Dragon dragon_claw_left: shared startEffect<br>Dragon dragon_claw_right: shared startEffect<br>Fader Fader_Fissure: shared startEffect<br>Fader Fader_Fissure_Intense: shared startEffect<br>Fader Fader_Roar: shared startEffect<br>Fader Fader_Roar_Intense: shared startEffect<br>Fader Fader_WallOfFire: shared startEffect<br>SeekerQueen SeekerQueen_Bite: shared startEffect<br>SeekerQueen SeekerQueen_PierceAOE: shared startEffect<br>SeekerQueen SeekerQueen_Rush: shared startEffect<br>SeekerQueen SeekerQueen_Slap: shared startEffect |
| sfx_dragon_scream | 100.0 | $enemy_dragon |  | Aspect_Moder aspect_dragon_taunt: shared triggerEffect<br>Dragon dragon_taunt: shared triggerEffect |
| sfx_eikthyr_attack | 50.0 | $enemy_eikthyr |  | Aspect_Eikthyr aspect_Eikthyr_antler: shared startEffect<br>Aspect_Eikthyr aspect_Eikthyr_charge: shared startEffect<br>Eikthyr Eikthyr_antler: shared startEffect<br>Eikthyr Eikthyr_charge: shared startEffect |
| sfx_fader_bite_pre | 100.0 | - |  | Aspect_Fader aspect_Fader_Bite: anim event attack_bite at 0.16s (pre-hit)<br>Fader Fader_Bite: anim event attack_bite at 0.16s (pre-hit) |
| sfx_fader_bite_snarl | 100.0 | $enemy_fader |  | Aspect_Fader aspect_Fader_Bite: anim event attack_bite at 0.99s (pre-hit)<br>Aspect_Fader: anim trigger jump_forward (not an attack item) at 2.32s<br>Fader Fader_Bite: anim event attack_bite at 0.99s (pre-hit)<br>Fader: anim trigger jump_forward (not an attack item) at 2.32s |
| sfx_fader_charredsummon_roar | 90.0 | $enemy_fader |  | Aspect_Fader: anim trigger attack_roar (not an attack item) at 0.11s<br>Fader Fader_Roar: anim event attack_roar at 0.11s (pre-hit)<br>Fader Fader_Roar_Intense: anim event attack_roar at 0.11s (pre-hit) |
| sfx_fader_claw_pre | 80.0 | - |  | Aspect_Fader aspect_Fader_Claw_Left: anim event attack_ClawL at 0.56s (pre-hit)<br>Aspect_Fader aspect_Fader_Claw_Right: anim event attack_ClawR at 0.38s (pre-hit)<br>Fader Fader_Claw_Left: anim event attack_ClawL at 0.56s (pre-hit)<br>Fader Fader_Claw_Right: anim event attack_ClawR at 0.38s (pre-hit) |
| sfx_fader_claw_swipe | 80.0 | $enemy_fader |  | Aspect_Fader aspect_Fader_Claw_Left: anim event attack_ClawL at 1.06s (pre-hit)<br>Aspect_Fader aspect_Fader_Claw_Right: anim event attack_ClawR at 0.95s (pre-hit)<br>Fader Fader_Claw_Left: anim event attack_ClawL at 1.06s (pre-hit)<br>Fader Fader_Claw_Right: anim event attack_ClawR at 0.95s (pre-hit) |
| sfx_fader_firebreath_in | 80.0 | $enemy_father |  | Aspect_Fader aspect_Fader_Flamebreath: anim event attack_flamebreath at 0.42s (pre-hit)<br>Fader Fader_Flamebreath: anim event attack_flamebreath at 0.42s (pre-hit) |
| sfx_fader_firebreath_out | 80.0 | $enemy_fader |  | Aspect_Fader aspect_Fader_Flamebreath: anim event attack_flamebreath at 2.05s (pre-hit)<br>Fader Fader_Flamebreath: anim event attack_flamebreath at 2.05s (pre-hit) |
| sfx_fader_firewall_start | 90.0 | $sfx_fader_firewallstart |  | Aspect_Fader aspect_Fader_WallOfFire: anim event attack_WallOfFire at 1.08s (pre-hit)<br>Fader Fader_WallOfFire: anim event attack_WallOfFire at 1.08s (pre-hit) |
| sfx_fader_fissure | 100.0 | $enemy_fader |  | Aspect_Fader aspect_Fader_Fissure: shared triggerEffect<br>Fader Fader_Fissure: shared triggerEffect<br>Fader Fader_Fissure_Intense: shared triggerEffect |
| sfx_fader_fissure_footslide | 80.0 | - |  | Aspect_Fader aspect_Fader_Fissure: anim event attack_Fissure at 1.20s (pre-hit)<br>Fader Fader_Fissure: anim event attack_Fissure at 1.20s (pre-hit)<br>Fader Fader_Fissure_Intense: anim event attack_Fissure at 1.20s (pre-hit) |
| sfx_fader_meteor_start | 140.0 | $enemy_fader |  | Aspect_Fader: anim trigger taunt (not an attack item) at 0.03s<br>Fader Fader_Meteors: anim event taunt at 0.03s (pre-hit)<br>Fader Fader_Meteors_Intense: anim event taunt at 0.03s (pre-hit) |
| sfx_fader_spin | 80.0 | $enemy_fader |  | Aspect_Fader aspect_Fader_Spin: anim event attack_Spin at 0.09s (pre-hit)<br>Fader Fader_Spin: anim event attack_Spin at 0.09s (pre-hit) |
| sfx_frozenking_attackmelee_double_chain | 50.0 | $enemy_frozenking |  | FrozenKing FrozenKing_DoubleSweep: anim event attack_double_sweep at 0.53s (pre-hit)<br>FrozenKing_p3 FrozenKing_DoubleSweep: anim event attack_double_sweep at 0.53s (pre-hit) |
| sfx_frozenking_attackmelee_double_impact | 80.0 | $enemy_frozenking |  | FrozenKing FrozenKing_DoubleSweep: anim event attack_double_sweep at 0.79s (pre-hit)<br>FrozenKing_p3 FrozenKing_DoubleSweep: anim event attack_double_sweep at 0.79s (pre-hit) |
| sfx_frozenking_attackmelee_double_whoosh_lead | 60.0 | $enemy_frozenking |  | FrozenKing FrozenKing_DoubleSweep: anim event attack_double_sweep at 0.72s (pre-hit)<br>FrozenKing_p3 FrozenKing_DoubleSweep: anim event attack_double_sweep at 0.72s (pre-hit) |
| sfx_frozenking_attackmelee_single_charge | 120.0 | $enemy_frozenking |  | FrozenKing FrozenKing_ChainSlam_L: anim event attack_chain_slamL at 0.20s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_L_double: anim event attack_chain_slamL_double at 0.20s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_R: anim event attack_chain_slamR at 0.10s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_R_double: anim event attack_chain_slamR_double at 0.10s (pre-hit)<br>FrozenKing: anim trigger attack_chain_sweepL (not an attack item) at 0.28s<br>FrozenKing: anim trigger attack_chain_sweepR (not an attack item) at 0.24s<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_L_double: anim event attack_chain_slamL_double at 0.20s (pre-hit)<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_R_double: anim event attack_chain_slamR_double at 0.10s (pre-hit)<br>FrozenKing_p3: anim trigger attack_chain_slamL (not an attack item) at 0.20s<br>FrozenKing_p3: anim trigger attack_chain_slamR (not an attack item) at 0.10s<br>FrozenKing_p3: anim trigger attack_chain_sweepL (not an attack item) at 0.28s<br>FrozenKing_p3: anim trigger attack_chain_sweepR (not an attack item) at 0.24s |
| sfx_frozenking_attackmelee_single_impact | 120.0 | $enemy_frozenking |  | FrozenKing FrozenKing_ChainSlam_L: anim event attack_chain_slamL at 1.39s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_L_double: anim event attack_chain_slamL_double at 1.39s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_R: anim event attack_chain_slamR at 1.36s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_R: anim event attack_chain_slamR at 1.42s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_R_double: anim event attack_chain_slamR_double at 1.36s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_R_double: anim event attack_chain_slamR_double at 1.42s (pre-hit)<br>FrozenKing: anim trigger attack_chain_sweepL (not an attack item) at 1.46s<br>FrozenKing: anim trigger attack_chain_sweepR (not an attack item) at 1.42s<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_L_double: anim event attack_chain_slamL_double at 1.39s (pre-hit)<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_R_double: anim event attack_chain_slamR_double at 1.36s (pre-hit)<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_R_double: anim event attack_chain_slamR_double at 1.42s (pre-hit)<br>FrozenKing_p3: anim trigger attack_chain_slamL (not an attack item) at 1.39s<br>FrozenKing_p3: anim trigger attack_chain_slamR (not an attack item) at 1.36s<br>FrozenKing_p3: anim trigger attack_chain_slamR (not an attack item) at 1.42s<br>FrozenKing_p3: anim trigger attack_chain_sweepL (not an attack item) at 1.46s<br>FrozenKing_p3: anim trigger attack_chain_sweepR (not an attack item) at 1.42s |
| sfx_frozenking_attackmelee_single_whsh | 70.0 | $enemy_frozenking |  | FrozenKing FrozenKing_ChainSlam_L: anim event attack_chain_slamL at 1.13s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_L_double: anim event attack_chain_slamL_double at 1.13s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_R: anim event attack_chain_slamR at 1.04s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_R_double: anim event attack_chain_slamR_double at 1.04s (pre-hit)<br>FrozenKing: anim trigger attack_chain_sweepL (not an attack item) at 1.06s<br>FrozenKing: anim trigger attack_chain_sweepR (not an attack item) at 1.02s<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_L_double: anim event attack_chain_slamL_double at 1.13s (pre-hit)<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_R_double: anim event attack_chain_slamR_double at 1.04s (pre-hit)<br>FrozenKing_p3: anim trigger attack_chain_slamL (not an attack item) at 1.13s<br>FrozenKing_p3: anim trigger attack_chain_slamR (not an attack item) at 1.04s<br>FrozenKing_p3: anim trigger attack_chain_sweepL (not an attack item) at 1.06s<br>FrozenKing_p3: anim trigger attack_chain_sweepR (not an attack item) at 1.02s |
| sfx_frozenking_chainflurry_chain | 150.0 | $enemy_frozenking |  | FrozenKing_p3 FrozenKing_ChainFlurry: attack startEffect |
| sfx_frozenking_chainflurry_coldvortex | 150.0 | $enemy_frozenking |  | FrozenKing_p3 FrozenKing_ChainFlurry: attack startEffect |
| sfx_frozenking_chainflurry_fly | 150.0 | $enemy_frozenking |  | FrozenKing_p3 FrozenKing_ChainFlurry: attack startEffect |
| sfx_frozenking_chainflurry_start | 120.0 | $enemy_frozenking |  | FrozenKing: anim trigger attack_chain_flurry (not an attack item) at 0.02s<br>FrozenKing_p3 FrozenKing_ChainFlurry: anim event attack_chain_flurry at 0.02s (pre-hit) |
| sfx_frozenking_charge_doublehookup | 120.0 | $sfx_frozenking |  | FrozenKing FrozenKing_ChainRush: anim event attack_rush at 0.90s (pre-hit)<br>FrozenKing_p3: anim trigger attack_rush (not an attack item) at 0.90s |
| sfx_frozenking_charge_start | 120.0 | $sfx_frozenking |  | FrozenKing FrozenKing_ChainRush: anim event attack_rush at 0.16s (pre-hit)<br>FrozenKing_p3: anim trigger attack_rush (not an attack item) at 0.16s |
| sfx_frozenking_charge_whoosh | 120.0 | $sfx_frozenking |  | FrozenKing FrozenKing_ChainRush: anim event attack_rush at 1.69s (pre-hit)<br>FrozenKing: anim trigger attack_chain_sweepL (not an attack item) at 1.49s<br>FrozenKing: anim trigger attack_chain_sweepR (not an attack item) at 1.49s<br>FrozenKing_p3: anim trigger attack_chain_sweepL (not an attack item) at 1.49s<br>FrozenKing_p3: anim trigger attack_chain_sweepR (not an attack item) at 1.49s<br>FrozenKing_p3: anim trigger attack_rush (not an attack item) at 1.69s |
| sfx_frozenking_doubleslam_explosion | 120.0 | $sfx_frozenking |  | FrozenKing_p3 FrozenKing_P3_ChainSlam_L_double: shared hitEffect<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_R_double: shared hitEffect |
| sfx_frozenking_frozentwirl_charge | 120.0 | $enemy_frozenking |  | FrozenKing FrozenKing_ChainWhirl: anim event attack_chain_whirl at 0.15s (pre-hit)<br>FrozenKing_p3 FrozenKing_P3_ChainWhirl: anim event attack_chain_whirl at 0.15s (pre-hit) |
| sfx_frozenking_frozentwirl_tornado | 120.0 | $enemy_frozenking |  | FrozenKing FrozenKing_ChainWhirl: anim event attack_chain_whirl at 1.02s (pre-hit)<br>FrozenKing_p3 FrozenKing_P3_ChainWhirl: anim event attack_chain_whirl at 1.02s (pre-hit) |
| sfx_frozenking_frozentwirl_wind | 120.0 | $enemy_frozenking |  | FrozenKing_p3 FrozenKing_P3_ChainWhirl: shared startEffect |
| sfx_frozenking_punchaoe_chain_start | 120.0 | $sfx_frozenking |  | FrozenKing: anim trigger attack_punch_aoe (not an attack item) at 0.06s<br>FrozenKing_p3 FrozenKing_Punch_AOE: anim event attack_punch_aoe at 0.06s (pre-hit) |
| sfx_frozenking_punchaoe_punch_first | 120.0 | $sfx_frozenking |  | FrozenKing: anim trigger attack_punch_aoe (not an attack item) at 1.03s<br>FrozenKing_p3 FrozenKing_Punch_AOE: anim event attack_punch_aoe at 1.03s (pre-hit) |
| sfx_frozenking_punchaoe_whoosh_start | 120.0 | $sfx_frozenking |  | FrozenKing: anim trigger attack_punch_aoe (not an attack item) at 0.33s<br>FrozenKing_p3 FrozenKing_Punch_AOE: anim event attack_punch_aoe at 0.33s (pre-hit) |
| sfx_frozenking_spikerain_chain | 120.0 | $sfx_frozenking |  | FrozenKing: anim trigger attack_spike_rain (not an attack item) at 0.19s<br>FrozenKing_p3 FrozenKing_SpikeRain: anim event attack_spike_rain at 0.19s (pre-hit) |
| sfx_frozenking_spikerain_iceceiling | 120.0 | $sfx_frozenking |  | FrozenKing: anim trigger attack_spike_rain (not an attack item) at 1.02s<br>FrozenKing_p3 FrozenKing_SpikeRain: anim event attack_spike_rain at 1.02s (pre-hit) |
| sfx_frozenking_tendril_summon | 120.0 | $sfx_frozenking |  | FrozenKing: anim trigger spawn (not an attack item) at 0.14s<br>FrozenKing_p3 FrozenKing_tendrilspawn: anim event spawn at 0.14s (pre-hit) |
| sfx_frozenking_turn | 50.0 | $sfx_frozenking |  | FrozenKing: anim trigger jump (not an attack item) at 0.85s<br>FrozenKing: anim trigger jump (not an attack item) at 1.93s<br>FrozenKing_p3: anim trigger jump (not an attack item) at 0.85s<br>FrozenKing_p3: anim trigger jump (not an attack item) at 1.93s |
| sfx_frozenking_voice_attack | 120.0 | $sfx_frozenking |  | FrozenKing FrozenKing_ChainRush: anim event attack_rush at 0.12s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_L: anim event attack_chain_slamL at 0.41s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_L_double: anim event attack_chain_slamL_double at 0.41s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_R: anim event attack_chain_slamR at 0.27s (pre-hit)<br>FrozenKing FrozenKing_ChainSlam_R_double: anim event attack_chain_slamR_double at 0.27s (pre-hit)<br>FrozenKing FrozenKing_DoubleSweep: anim event attack_double_sweep at 0.07s (pre-hit)<br>FrozenKing_p3 FrozenKing_DoubleSweep: anim event attack_double_sweep at 0.07s (pre-hit)<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_L_double: anim event attack_chain_slamL_double at 0.41s (pre-hit)<br>FrozenKing_p3 FrozenKing_P3_ChainSlam_R_double: anim event attack_chain_slamR_double at 0.27s (pre-hit)<br>FrozenKing_p3: anim trigger attack_chain_slamL (not an attack item) at 0.41s<br>FrozenKing_p3: anim trigger attack_chain_slamR (not an attack item) at 0.27s<br>FrozenKing_p3: anim trigger attack_rush (not an attack item) at 0.12s |
| sfx_gdking_footstep | 100.0 | - |  | Aspect_Elder aspect_gd_king_stomp: shared triggerEffect<br>gd_king gd_king_stomp: shared triggerEffect |
| sfx_gdking_projectile_hit | 20.0 | - |  | Aspect_TentaRoot tentaroot_attack: shared triggerEffect |
| sfx_gdking_rock_destroyed | 40.0 | - |  | Aspect_Elder aspect_gd_king_stomp: shared triggerEffect<br>gd_king gd_king_stomp: shared triggerEffect |
| sfx_gdking_scream | 100.0 | $enemy_gdking |  | Aspect_Elder aspect_gd_king_scream: shared triggerEffect<br>gd_king gd_king_scream: shared triggerEffect |
| sfx_gdking_shoot_start | 100.0 | - |  | Aspect_Elder aspect_gd_king_shoot: shared startEffect<br>gd_king gd_king_shoot: shared startEffect |
| sfx_gdking_shoot_trigger | 100.0 | $enemy_gdking |  | Aspect_Elder aspect_gd_king_shoot: shared triggerEffect<br>gd_king gd_king_shoot: shared triggerEffect |
| sfx_gdking_spawn | 100.0 | $enemy_gdking |  | Aspect_Elder aspect_gd_king_rootspawn: shared startEffect<br>FrozenKing_p3 FrozenKing_tendrilspawn: shared startEffect<br>gd_king gd_king_rootspawn: shared startEffect |
| sfx_gdking_stomp | 100.0 | $enemy_gdking |  | Aspect_Elder aspect_gd_king_stomp: shared startEffect<br>gd_king gd_king_stomp: shared startEffect |
| sfx_goblinking_beam | 100.0 | $enemy_goblinking | yes | Aspect_SeekerQueen aspect_SeekerQueen_Spit: shared triggerEffect<br>Aspect_Yagluth aspect_GoblinKing_Beam: shared triggerEffect<br>GoblinKing GoblinKing_Beam: shared triggerEffect<br>SeekerQueen SeekerQueen_Spit: shared triggerEffect |
| sfx_goblinking_taunt | 100.0 | $enemy_goblinking |  | Aspect_Yagluth aspect_GoblinKing_Taunt: shared triggerEffect<br>GoblinKing GoblinKing_Taunt: shared triggerEffect |
| sfx_greydwarf_attack_hit | 30.0 | - |  | Aspect_Bonemass aspect_bonemass_attack_aoe: shared hitEffect<br>Aspect_Elder aspect_gd_king_rootspawn: shared hitEffect<br>Aspect_Elder aspect_gd_king_scream: shared hitEffect<br>Aspect_Elder aspect_gd_king_shoot: shared hitEffect<br>Aspect_Fader aspect_Fader_Flamebreath: shared hitEffect<br>Aspect_Moder aspect_dragon_coldbreath: shared hitEffect<br>Aspect_Moder aspect_dragon_spit_shotgun: shared hitEffect<br>Aspect_SeekerQueen aspect_SeekerQueen_Spit: shared hitEffect<br>Aspect_TentaRoot tentaroot_attack: shared hitEffect<br>Aspect_Yagluth aspect_GoblinKing_Beam: shared hitEffect<br>Aspect_Yagluth aspect_GoblinKing_Taunt: shared hitEffect<br>Bonemass bonemass_attack_aoe: shared hitEffect<br>Dragon dragon_coldbreath: shared hitEffect<br>Dragon dragon_spit_shotgun: shared hitEffect<br>Fader Fader_Flamebreath: shared hitEffect<br>Fader Fader_Meteors: shared hitEffect<br>Fader Fader_Meteors_Intense: shared hitEffect<br>FrozenKing_p3 FrozenKing_tendrilspawn: shared hitEffect<br>GoblinKing GoblinKing_Beam: shared hitEffect<br>GoblinKing GoblinKing_Meteors: shared hitEffect<br>GoblinKing GoblinKing_Taunt: shared hitEffect<br>Hive hive_attack_aoe: shared hitEffect<br>Hive hive_attack_ranged: shared hitEffect<br>SeekerQueen SeekerQueen_Spit: shared hitEffect<br>TheHive gjall_attack_spit: shared hitEffect<br>gd_king gd_king_rootspawn: shared hitEffect<br>gd_king gd_king_scream: shared hitEffect<br>gd_king gd_king_shoot: shared hitEffect |
| sfx_hatchling_flap | 30.0 | - |  | TheHive: anim trigger fly_takeoff (not an attack item) at 2.37s<br>TheHive: anim trigger fly_takeoff (not an attack item) at 3.31s |
| sfx_seeker_brute_taunt | 35.0 | $enemy_seekerbrute |  | Aspect_SeekerQueen: anim trigger taunt (not an attack item) at 0.00s<br>SeekerQueen: anim trigger taunt (not an attack item) at 0.00s |
| sfx_tentaroot_attack | 40.0 | $sfx_tentaroot_attack |  | Aspect_TentaRoot tentaroot_attack: shared startEffect |
| sfx_troll_attack_hit | 50.0 | - |  | Aspect_Yagluth aspect_GoblinKing_Nova: shared hitEffect<br>GoblinKing GoblinKing_Nova: shared hitEffect |
| sfx_wolf_attack_hit | 30.0 | - |  | Aspect_Eikthyr aspect_Eikthyr_antler: shared hitEffect<br>Aspect_Eikthyr aspect_Eikthyr_charge: shared hitEffect<br>Eikthyr Eikthyr_antler: shared hitEffect<br>Eikthyr Eikthyr_charge: shared hitEffect |
| vfx_gjall_spit | 50.0 | - |  | TheHive gjall_attack_spit: shared triggerEffect |
