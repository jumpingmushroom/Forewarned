# Forewarned: web research (2026-10-05)

Read-only research. Raw sources were saved in a session scratchpad at the time (not kept in the repo): `ts.json` (full Thunderstore Valheim package list, 12,631 packages), `wiki/*.json` (Fandom wikitext), `wiki/kall_wg.json` (weirdgloop wiki), and shallow clones `dbm/` (DeadlyBossMods @61a1048, 2026-10-02), `bw/` (BigWigs, 2026-10-03), and `cm/` (BepInEx.ConfigurationManager v19.0).

> **Outcome (2026-10-05):** the working name Gjallarhorn clashed with RAGEmedia-Gjallarhorn (§1.1), so the mod was renamed **Forewarned**. No Thunderstore package uses that name or "forewarn" in its description (checked against the same 12,631-package dump).

---

## 1. Name clash and prior art

### 1.1 "Gjallarhorn" / "Gjallar" on Thunderstore: there is a clash
I searched all Valheim package names and latest-version descriptions (case-insensitive) using the API at https://thunderstore.io/c/valheim/api/v1/package/. Two packages match:

| Package | Created / updated | Downloads | What it is |
|---|---|---|---|
| **RAGEmedia-Gjallarhorn** v0.3.0, https://thunderstore.io/c/valheim/p/RAGEmedia/Gjallarhorn/ | 2026-09-12 / 2026-09-22 | ~767 | "Server-side Viking death announcements, boss-defeat chronicles, and join/leave notices." Its README says "boss defeats get the centre-screen banner" and "Named for Heimdall's horn". Categories: Server-side, Client-side, AI Generated. |
| **GBV-Gjallar** v0.1.1, https://thunderstore.io/c/valheim/p/GBV/Gjallar/ | 2026-09-25 / 2026-09-28 | ~58 | "Restart warnings for dedicated servers… countdown, on screen and in chat." |

- Thunderstore namespaces packages by team (`Team-Name`), so a second `Gjallarhorn` can be published. Even so, an active mod with the **exact same name** that also does **centre-screen boss announcements** is very likely to confuse users. `Gjallar` is a second near-collision, and its job (on-screen countdown warnings) is similar too. **I recommend picking a different name**, or at least a distinctive one such as "Gjallarhorn Boss Warnings", though that still collides on search.
- Other horn-named packages exist but don't clash: Rodo-SuperHornMod and Rodogor-Horn_of_Heimdall (craftable Horn of Heimdall), Mushroom_Vikings-HornOfCalling, hitman_cool-SignalHorn.

### 1.2 Nexus Mods (Valheim)
- I queried the Nexus v2 GraphQL API (`https://api.nexusmods.com/v2/graphql`, mod-name wildcard "gjallar"): **0 results**. A web search also turned up no Gjallarhorn mod on Nexus.
- Name searches for "warning", "telegraph", "alert" and "indicator" found nothing relevant. The only hit was "Simple Pickup Warnings" (https://www.nexusmods.com/valheim/mods/3828, about inventory pickup).
- Boss-related Nexus mods are all difficulty, trophy or altar tweaks, for example Enhanced Bosses (https://www.nexusmods.com/valheim/mods/1880), Hard Bosses (/877), M182 Boss Challenge (/3717) and Boss Directions (/2692). **None of them warn about attacks.**

### 1.3 Vanilla Valheim usage of "Gjallarhorn"
- There is **no vanilla item, location or creature called Gjallarhorn.** I searched the full text of both wikis: the only hit is the trivia note on the **Gjall** page, which says the name refers to the Gjallarhorn (https://valheim.weirdgloop.org/w/Gjall).
- The **Gjall** is a Mistlands floating creature that fires fireballs and drops Ticks, and it makes a horn-like sound when it spots you (https://game8.co/games/Valheim/archives/619520). There is also a vanilla raid event, "What's up, Gjall?!".
- Players already link "Gjall" and "horn", so the confusion risk is low to moderate. One upside is that the in-game horn sound could be thematically appropriate.
- There is an old Steam suggestion thread asking for a "Gjallarhorn aka viking horn" item: https://steamcommunity.com/app/892970/discussions/2/3073117690269721957/

### 1.4 Prior art: boss warnings, telegraphs, timers
I found **no Valheim mod (Thunderstore or Nexus) that does DBM-style per-ability boss warnings, cast timers or ground telegraphs.** The closest packages are adjacent:

| Package | One-liner |
|---|---|
| j1gA-ExtendedBosses, https://thunderstore.io/c/valheim/p/j1gA/ExtendedBosses/ | "Raid-style boss fights from vanilla parts only: HP phases, add waves, nests, lieutenants, shields, burn windows, threat." This **adds** mechanics. Worth checking compatibility, and a possible integration target. |
| coemt-EpicBossFights (~41k downloads) | "adds new attacks to some of the bosses". This could add unknown attacks that Forewarned won't recognise. |
| Bagr-DamageMeter, https://thunderstore.io/c/valheim/p/Bagr/DamageMeter/ | A boss-fight stats panel (damage, blocks, parries, deaths per player). The Details!/Recount equivalent, not DBM. |
| TrueNativeStudio-Skald | Achievements and death announcements, plus "boss and raid call-outs" (event or kill announcements, not attack warnings). |
| RustyLaserGaming-RustyHeathensMessenger / Runeborn | Server messaging with "boss alerts" and "raid warnings" (spawn/kill and raid-event messages). |
| Zeitsurfer-MultiBossSpawner | Spawns several bosses and gives a "Viking HUD retreat warning" when a participant dies. |
| Ains-PingWheel | Ping wheel with an "attack / danger" ping, edge arrows and sounds. Its manual ping UX is a good reference for arrows. |
| Spazz-ParryAndDodgeConfig, 159-AutoParry, localcc-ConfigurableParrying | Parry/dodge timing tweaks. These are not warnings but are relevant to "block/parry now" callouts. |
| cjayride-SeneaLHudLayout | A HUD layout mod that keeps the boss bars readable. Consider HUD-overlap compatibility. |

---

## 2. DBM conventions to mirror
Sources: the DBM repo at https://github.com/DeadlyBossMods/DeadlyBossMods (master, 2026-10-02) and the CurseForge page at https://www.curseforge.com/wow/addons/deadly-boss-mods. File paths below are relative to the repo.

Terminology note: DBM's GUI now calls special warnings **"Special Announcements" (SA)** (`DBM-GUI/localization.en.lua`: `L.Panel_SpecWarnFrame = "Special Announcements"`). The code still uses `SpecialWarning`. Also, DBM retail for WoW 12.x ("Midnight") partly relies on Blizzard's encounter-event and secret-value APIs (`QueueBlizzTargetSpecialWarning` and similar), which is why parts of the code reference "secrets". That is not relevant to us.

### 2.1 Special warnings / special announcements (big centre text)
From `DBM-Core/modules/objects/CoreOptions.lua` (defaults) and `DBM-Core/modules/objects/SpecialWarning.lua` (behaviour). Links:
- https://github.com/DeadlyBossMods/DeadlyBossMods/blob/master/DBM-Core/modules/objects/CoreOptions.lua
- https://github.com/DeadlyBossMods/DeadlyBossMods/blob/master/DBM-Core/modules/objects/SpecialWarning.lua

**Position and font**
- `SpecialWarningPoint = "CENTER"`, X = 0, **Y = +75** (just above screen centre).
- `SpecialWarningFontSize2 = 35`, `SpecialWarningFontStyle = "THICKOUTLINE"`, no shadow.
- Text colour `SpecialWarningFontCol = {1.0, 0.7, 0.0}` (#FFB300, "yellow with a tint of orange"). Spell icon shown on both sides (`SpecialWarningIcon = true`).

**Duration and fade**
- `SpecialWarningDuration2 = 1.5` s. The GUI slider allows 1–10 in 0.5 steps.
- Behaviour: full alpha for `duration`, then a linear fade over `0.3 × duration`, so the text is gone at 1.3 × duration (about 1.95 s by default). The fade runs on a 0.05 s ticker.

**Stacking**
- There are **only two lines** (font1 and font2 below it).
- A new warning goes to line 1 if it's free, else line 2.
- If both are busy, line 2's text and timer move up to line 1 and the new warning replaces line 2. So it is a two-deep FIFO and the oldest warning drops off.

**The five SA levels**
Each level has its own sound, flash colour, flash duration, alpha, repeat count and controller vibration (GUI headers, `L.SpecialWarnHeader1..5`):

| SA | Meaning | Default sound | Flash colour | Flash dur (s) | Flash alpha | Flash count | Vibrate |
|---|---|---|---|---|---|---|---|
| 1 | Normal priority, affects **you** | "PvP Flag" (569200, PVPFlagTaken.ogg) | Yellow {1,1,0} | 0.3 | 0.3 | 1 | no |
| 2 | Normal priority, affects **everyone** | "Algalon: Beware!" (543587) | Orange {1,0.5,0} | 0.4 | 0.3 | 1 | no |
| 3 | **HIGH** priority | `DBM-Core/sounds/AirHorn.ogg` ("AirHorn (DBM)") | Red {1,0,0} | 1.0 | 0.4 | 3 | yes |
| 4 | **HIGH priority run away** | "BB Wolf: Run Away" (552035, the Big Bad Wolf "Run away little girl" line) | Purple {1,0,1} | 0.7 | 0.4 | 2 | yes |
| 5 | Note contains your name | "Loatheb: I see you" (554236) | Teal {0.2,1,1} | 1.0 | 0.5 | 3 | yes |

- Code comments in `ValidUIDs.lua` describe the sound levels as "1: Normal Personal Alert, 2: Normal raid/aoe alert, 3: High priority Personal Alert, 4: Highest Priority run away alert".
- Other selectable sounds (`DBM-GUI/modules/options/alerts/SpecialAnnouncements.lua`): Headless Horseman Laugh, Illidan "Not Prepared", Kil'Jaeden "Destruction", Lady Malande "Flee", Kaz'rogal "Marked", Milhouse "Light You Up", Void Reaver "Marked", Yogg-Saron Laugh, Night Elf Bell, and Blizzard Low/Medium/Critical. Plus any LibSharedMedia sounds.

**Screen flash** (`DBM-Core/DBM-Flash.lua`)
- A **full-screen** tinted backdrop on BACKGROUND strata, so it sits behind the UI. It is **not** an edge vignette.
- Alpha follows a parabola, `1-(t/(d/2)-1)^2`: it rises to peak at half the duration and falls back to 0.
- It repeats N times. Peak alpha is the per-level value (0.3–0.5).

**Text templates** (`DBM-Core/localization.en.lua`, `L.AUTO_SPEC_WARN_TEXTS`)
- "%s - move away", "%s - run away", "%s - dodge attack", "%s - keep moving", "%s - stop moving", "%s on you", "%s - interrupt >%s<!", "%s - defensive", "%s - switch targets", "Incoming Adds - switch targets", "%s damage - move away" (GTFO), "%s - look away", "%s - jump", "%s soon", "%s in %s", "%s - soak it", "%s - move away from others".
- The `>name<` marker is class-coloured in-game.

**Global switches**: don't show text, don't flash, don't vibrate, don't play sound, and "Do not play special announce sounds or show flash for trivial content".

### 2.2 Announces (regular "raid-warning-style" messages)
- Frame: `WarningPoint = CENTER`, Y = **+260** (above the special warning), `WarningFontSize = 20`, shadow on, no outline.
- `WarningDuration2 = 1.5` s, with the same fade rule (gone at 1.3×).
- **Three lines** (`DBM-Core/modules/objects/Announce.lua`).
- Spell icon on the left and right, mirrored to chat by default (`ShowWarningsInChat = true`).
- Default sound: Night Elf Bell toll (566558).

**Announce colours** (`WarningColors`)
| # | Colour | Use |
|---|---|---|
| 1 | #69CCF0 turquoise | "positive" |
| 2 | #F2F200 yellow | "low priority, e.g. phase change / info" |
| 3 | #FF8000 orange | "high priority, personal danger" |
| 4 | #FF1A1A red | "highest priority, whole raid" |

**Templates** (`L.AUTO_ANNOUNCE_TEXTS`): "%s on >%s<" (target), "%s on YOU", "Casting %s: %.1f sec", "%s soon", "%s in %s" (prewarn), "%s ended", "%s faded", "%s (%s)" (count), "%s on >%s< (%d)" (stacks). The spell-only form is just the spell name.

### 2.3 Timer bars: DBT (`DBM-StatusBarTimers/DBT.lua`)
https://github.com/DeadlyBossMods/DeadlyBossMods/blob/master/DBM-StatusBarTimers/DBT.lua

**Small bars**
- Size 183×20, scale 0.9, anchored TOPRIGHT at (−223, −260).
- Bars fill up (`FillUpBars = true`), are sorted by remaining time, and have the icon on the left.
- Spark is on. Font size 10. The decimal is shown below 11 s (`TDecimal = 11`). "Flash bar about to expire" is off by default.

**Enlarge**
- A bar moves to the **"huge" bar anchor at CENTER (0, −120)** when below `EnlargeBarTime = 9.9` s.
- Huge bars are 200×20, scale 1.03.
- Two styles: "Classic (existing small bar slides to Enlarged anchor)" or "Simple (small bar disappears and new large bar created)".

**Colouring**
- `ColorByType = true`, `DynamicColor = true`: the bar fades from its start colour to its end colour as it runs down. With `NoBarFade` it switches colour at enlarge instead.
- Long bars can be hidden until under 60 s (`HiddenBarTime = 60`, off by default).
- "Keep timer active until ability cast" (`KeepBars`): the bar sits at 0 or goes negative until the cast actually happens. There are variance-window options ("Text hits zero at start of CD window then goes negative" is the default).

**Default colours by type** (start → end, RGB 0–1 converted to hex). GUI names come from `L.CBT*`:

| Type id | GUI name | Start | End |
|---|---|---|---|
| 0 | Generic | (1, 0.7, 0) #FFB300 | (1, 0, 0) #FF0000 |
| 1 | Adds Incoming | (0.375, 0.545, 1) #608BFF | (0.15, 0.385, 1) #2662FF |
| 2 | AOE Spell | (1, 0.466, 0.459) #FF7775 | (1, 0.043, 0.247) #FF0B3F |
| 3 | Targeted Spell | (0.9, 0.3, 1) #E64DFF | (1, 0, 1) #FF00FF |
| 4 | Interruptable Spell | (0.47, 0.97, 1) #78F7FF | (0.047, 0.88, 1) #0CE0FF |
| 5 | Specific Role Spell | (0.5, 1, 0.5) #80FF80 | (0.11, 1, 0.3) #1CFF4D |
| 6 | Phase Change | (1, 0.776, 0.42) #FFC66B | (0.5, 0.41, 0.285) #806949 |
| 7 | User Important 1 | (1, 1, 0.063) #FFFF10 | (1, 0.92, 0.012) #FFEB03 |
| 8 | User Important 2 | (1, 0.675, 0) #FFAC00 | (1, 0.506, 0) #FF8100 |

- Important bars are always shown large by default (`Bar7ForceLarge = true`) and get a custom "!" inline icon.
- Timer kinds (`L.AUTO_TIMER_TEXTS` / `L.AUTO_TIMER_OPTIONS`):
  - **cd/next**: "Show timer for X cooldown"
  - **cast**: "Show timer for X cast"
  - **target**: debuff on a player
  - **active**: "X ends"
  - **fades**
  - **ai**: an auto-learned "X AI" timer, which is interesting for Valheim since we will often need to learn cooldowns
  - **count**: variants with a count

### 2.4 Countdown voice
- Per-timer countdown voice: 0 = None, 1/2/3 = "Global Countdown 1/2/3".
- Defaults (enUS): Voice1 "Corsica", Voice2 "Kolt", Voice3 "Smooth". Packs provide up to 10 numbers.
- If a mod doesn't specify a length, it counts down from **4** (`self.countdownMax or 4`, Timer.lua).
- Can be disabled globally (`DontPlayCountdowns`).

### 2.5 Voice packs
- These replace or augment the SA sound with a spoken instruction.
- Default enUS pack: `ChosenVoicePack2 = "VEM"`.
- The GUI sound list starts with "Voice Pack / SA 1..4 fallback": it plays the voice line if a pack is installed, otherwise that SA level's sound.
- Voice keys are short instruction clips (`DBM-Core/VoicePackSounds.lua`), e.g. `runaway`, `keepmove`, `dontmove`, `watchstep`, `justrun`, `behindboss`, `breaklos`, `defensive`, `killmob`, `mobsoon`, `phasechange`, `aesoon`, `breathsoon`, `frontal`, `meteorrun`, `firecircle`, `firewall`, `kite`, `findshelter`, `movetopillar`, `dodge`/`dodgecount`, `left`/`right`, `enrage`, `bigmob`.
- **This vocabulary maps very well to Valheim responses**, for example `breaklos` = hide behind a pillar from Yagluth's breath, `findshelter`, `movetopillar` = The Elder's vines, `meteorrun`, `firecircle` = Fader's Wall of Fire.

### 2.6 Arrows, range frame, HUD, yells
**Arrow** (`DBM-Core/DBM-Arrow.lua`)
- An arrow at TOP (0, −150) that points **to** a player or location, or **away** from it ("ShowRunAway").
- The run-away arrow hides itself at 100 yd distance by default; the normal arrow hides at 3 yd.

**Range frame** (`DBM-RangeCheck.lua`)
- A text list or "radar" of players within N yards.
- Default "radar" at CENTER (100, −100), with optional sounds.

**HudMap** (`DBM-Core/DBM-HudMap.lua`): draws world-space circles and markers on a mini-map overlay. Relevant to ground telegraphs.

**Yells** (`L.AUTO_YELL_OPTION_TEXT`)
- "Yell when you are affected by X", with variants: with player name, with count, with countdown when fading ("X fading in 3… 2… 1"), and with position and raid icon.
- Valheim analogue: auto-send a chat "shout" or ping when *you* are the Elder vine or Fader Wall-of-Fire target.

### 2.7 Per-ability option layout in DBM-GUI
From `DBM-GUI/modules/PanelPrototype.lua` → `CreateAbility`, `CreateCheckButton`:
- Each boss panel lists **ability groups**: a bordered box titled with the spell icon and name, collapsible (`AutoExpandSpellGroups2`), with a "test" button that fires a 5 s test timer and shows the announce.
- Inside each group is **one checkbox per object** for that spell:
  - **Announce**: checkbox.
  - **Special announce**: checkbox plus an **"Announce Sound"** dropdown (None / SA1–4 fallback / any named sound).
  - **Timer**: checkbox plus a **"Bar Color"** dropdown (Generic / Add / AOE / Targeted / Interrupt / Role / Phase / Important 1 / Important 2, each label drawn in its colour) plus a **"Countdown Voice"** dropdown (None / Global 1–3 / specific voice).
  - **Yell**, **Set icon**, **range frame** and **info frame**: checkboxes.
- Option labels are auto-generated from templates, e.g. "Show special announce to move away from $spell:X", "Show timer for $spell:X cooldown".

### 2.8 BigWigs, briefly (https://github.com/BigWigsMods/BigWigs)
- Each ability has toggle flags (`Core/Constants.lua`): BAR, MESSAGE, ICON, PULSE (icon pulse in the middle of the screen), SOUND, SAY, PROXIMITY, FLASH, ME_ONLY, EMPHASIZE, COUNTDOWN, CASTBAR, VOICE, INFOBOX, NAMEPLATE, plus role flags (TANK, HEALER, DISPEL).
- **Messages** (`Plugins/Messages.lua`): font size 20, display 2 s plus a 1.2 s fade. An **Emphasized** message is 44 pt, THICKOUTLINE and uppercase.
- **Bars** (`Plugins/Bars.lua`): **emphasize** (move to the big bar anchor) at **11 s**, size multiplier 1.1. Normal bar colour (0.25, 0.33, 0.68); emphasized bar colour red.
- **Countdown** (`Plugins/Countdown.lua`): **5** s, large 48 pt numbers plus voice.
- **Message colours** (`Plugins/Colors.lua`, descriptions from `Locales/enUS.lua`):
  - red (1, 0.2, 0.2): "General encounter warnings"
  - blue (0.2, 0.4, 1): "things that affect you directly"
  - orange (1, 0.5, 0.1)
  - yellow (1, 1, 0.1)
  - green (0.2, 1, 0.2): "good things"
  - cyan (0.2, 1, 1): "stage changes"
  - purple (0.7, 0, 0.7): "tank specific"
- **Sounds** (`Plugins/Sound.lua`): five semantic slots, **Long, Info, Alert, Alarm, Warning**, plus "underyou". Sounds are tied to meaning, not to specific audio.
- A design takeaway worth borrowing: BigWigs names sounds by *severity class* (Info/Alert/Alarm/Warning), which is simpler for users than DBM's SA1–5.

---

## 3. Valheim boss attacks and counterplay
**Wiki sources**
- Fandom wiki through its MediaWiki API (the page HTML returns 402 to fetchers; `api.php?action=parse&prop=wikitext` works). Boss pages are https://valheim.fandom.com/wiki/<Boss> and strategies are https://valheim.fandom.com/wiki/Boss_strategies.
- Fandom has **no Kall page**. Kall data comes from the **Weird Gloop Valheim wiki**, https://valheim.weirdgloop.org/w/Kall_Fimbulbringer.

Values below are 0-star, single-player. "CD" means cooldown, "range" means the player distance at which the AI may pick the attack, and HP% are the AI's health gates. Prefab/IDs are given for implementation.

### 3.1 Eikthyr (`Eikthyr`, 500 HP, Meadows)
Aura: forces darkness. All attacks can be blocked with a wood shield, and also parried, though he isn't staggered.
- **Antler swipe**: 20 Pierce. CD 5 s, range ≤4 m, 4.5 m long, 25° arc in front. Response: block/parry or step aside.
- **Charge (lightning beam)**: 15 Lightning plus the Lightning status. A 45° horizontal fan, 20 m long, in front. CD 25 s, range ≤15 m. Response: sidestep out of the cone or get behind him, or block. Use the altar rocks as cover against the beam.
- **Stomp**: 15 Lightning, a **10 m radius AoE expanding from him**. CD 40 s, range ≤6 m. Response: run out of 10 m, or block.
- No phases.

### 3.2 The Elder (`gd_king`, 2,500 HP, Black Forest, weak to Fire)
- **Shoot (vine volley)**: 25 vines, each 35 Pierce. CD 6 s. Used only at **15–50 m**, never in melee. Tracks the target; 30 m/s, 10–20° spread.
  - Response: **break line of sight behind the four indestructible altar pillars**, or close to melee.
  - In groups, spread out so only one person is targeted.
- **Stomp**: 60 Blunt, 5 m radius at the impact point. CD 5 s, range ≤3 m. Response: block (a buckler blocks most), parry, roll, or back off.
- **Root spawn**: he raises an arm and summons **15 Roots within 15 m of the player**. Roots are immobile and despawn after 18–20 s. CD 25 s, range ≤30 m. Response: **move away from the roots** rather than fight them, since they expire.
- No phases.

### 3.3 Bonemass (`Bonemass`, 5,000 HP, Swamp, weak to Blunt/Frost, immune to Poison)
Aura: forces rain.
- **Poison AoE (vomit)**: 130 Poison in a **9 m radius**; the cloud lasts 15 s and keeps refreshing poison. **Cannot be blocked or dodged.**
  - Tell: **he leans backwards** and charges a green cloud. CD 30 s, range ≤15 m.
  - Response: **start running out immediately at the lean-back.** The cloud is centred on the vomit impact point in front of him, so it barely reaches **behind** him: get behind him.
  - Running beats rolling here. Use poison resistance mead.
- **Punch**: 80 Blunt plus 50 Poison, an 8.5 m, 66° frontal arc. CD 7 s, range ≤8 m. Response: parry, roll, or sprint out. A normal block likely staggers you.
- **Throw (summon adds)**: reaches into his armpit and lobs a goop glob that spawns **4 Skeletons and/or Blobs** (cap 8 of each). CD **50 s**, range ≤30 m. Slow animation.
  - Response: **kill adds as they land** (group: assign add-killers), using blunt weapons. You get free hits on him during the animation.
- No phases.

### 3.4 Moder (`Dragon` / "dragonqueen", 7,500 HP, Mountains, weak to Fire, immune to Frost)
Aura: Freezing. **She alternates between flying and landing.** There is **no HP-threshold phase**; she switches between airborne and grounded at her own discretion. A Valheim mod could detect this from the AI's flying state.
- **Ice Barrage (airborne only)**: 16 icicles, 30 Pierce plus 200 Frost each, 13–20° spread, 25 m/s. Leaves 10-HP crystals that last 30 s. CD 8 s, range 5–25 m. Response: keep running perpendicular, roll, block, or **use terrain or a raised earth pillar as cover**.
- **Cold Breath (grounded)**: 200 Frost, a **30 m line** that tracks you a little (10° arc). CD 8 s, range 5–20 m measured from her body centre. Response: **move sideways out of the line**, use cover, or stand very close (under her head) where it can't reach.
- **Bite (grounded)**: 120 Pierce, 8 m, 20° arc. CD 30 s, range ≤7 m. Response: block, parry, or sidestep.
- **Claw L/R (grounded)**: 110 Slash, a 12 m, 50° arc. CD 30 s per claw, range ≤10 m. Response: parry (easy with a silver shield) or step back.
- General strategy: melee when she lands, ranged while she flies. Clear drakes and golems first.

### 3.5 Yagluth (`GoblinKing`, 10,000 HP, Plains, resists Fire, very resistant to Pierce)
Aura: darkness, no rain. You get free hits while he is emerging from the ground.
- **Fire Beam (breath)**: 20 projectiles of 40 Fire plus 20 Lightning, **a 40 m straight line**, with no spread (perfectly accurate). It rotates slowly to follow you. CD 15 s. Used only at **10–40 m**, never in melee.
  - Response: **strafe sideways**, **break LOS behind the stone pillars** (they erode), or get into melee. Blocking is a stamina sink.
- **Meteors**: Tell: **raises his LEFT fist, which glows RED.** A few seconds later, 10 meteors with 5 m explosions (40 Blunt plus 120 Fire, Burning) fall from 30 m behind him across a 15 m sphere. CD 25 s, range ≤30 m.
  - Response: **keep moving or outrun them**, use pillar cover, or stay in front of his face, since his head blocks most meteors.
- **Nova**: Tell: **raises his RIGHT fist, which glows BLUE**, then slams. A 10 m radius explosion expands from the fist (65 Fire plus 65 Lightning) and leaves lingering blue fire (100 Fire DoT). CD 20 s, range ≤10 m.
  - Response: **roll through it with i-frames as he slams**, or run out of 10 m. The initial blast can be blocked; the lingering fire cannot, so leave it.
- No phases. Fire resistance barley wine is expected.

### 3.6 The Queen / Seeker Queen (`SeekerQueen`, 12,500 HP, Mistlands, Infested Citadel, resists Pierce)
**HP-gated ability unlocks**, which act as de-facto phases:

| HP gate | Ability |
|---|---|
| Below **99%** | **Call** unlocks |
| Below **90%** | **Teleport** unlocks |
| Below **80%** | **Spit** unlocks |
| Below **70%** | **Bite** unlocks |
| Below **60%** | **Rush** unlocks |
| Always | Slap and Pierce AoE |

**Attacks**
- **Slap**: 130 Slash, a **10 m, 145° wide arc** in front. CD 4 s, range ≤9 m. Response: dodge-roll timed to impact, parry (heavy knockback still applies), or get behind her.
- **Pierce AoE**: stabs the ground with all four arms; 150 Pierce in a 4.5 m radius around her. CD 4 s, range ≤4 m. Response: don't hug her; back out of 4.5 m.
- **Bite** (<70%): lunge, 140 Pierce plus 100 Poison, reaching 10 m from her centre in a 25° arc. The hitbox is huge and hits well to the side. Response: roll or move far to the side. Use poison resistance.
- **Rush** (<60%): crawls 16 m in a line slashing a 120° arc (100 Slash). CD 25 s, range 5–25 m. Response: **sidestep out of her path.**
- **Spit** (<80%): 20 projectiles, 40 Blunt plus 40 Poison plus Slimed (the slow is unique to her). Each has a 30% chance to spawn a Seeker Brood. CD 20 s, range 5–25 m. Response: move laterally, block, or kill the broods.
- **Call** (<99%): **stops and roars**, and holes within 30 m spawn **Seekers and Seeker Broods**. CD 60 s. Response: **kill adds**. Polearm spin, Demolisher and Staff of Embers work well. The roar gives a free-hit window.
- **Teleport** (<90%): **burrows and re-emerges up to 200 m away**. CD 60 s. Response: reposition and find her (a "where is she" arrow is a good UI idea).

**Notes**
- Her knockback is huge, even when parried, so a **Feather cape** is advised against fall damage.
- The arena entrance is a safe zone.

### 3.7 Fader (`Fader`, 25,000 HP, Ashlands, resists Pierce, immune to Fire/Spirit)
"Casts more ranged attacks as health gets lower." **HP-gated cooldowns form the phases:**

| HP band | Fissure CD | Meteors CD | Roar (adds) | Flamebreath | Wall of Fire |
|---|---|---|---|---|---|
| 90–100% | – | 25 s | – | – | – |
| 85–90% | – | 25 s | – | – | 60 s |
| 55–85% | **30 s** | 25 s | – | 25 s | 60 s |
| 35–55% | 30 s | 25 s | **45 s** | 25 s | 60 s |
| 25–35% | **20 s** | 25 s | **26 s** | 25 s | 60 s |
| 15–25% | 20 s | **18 s** | 26 s | 25 s | 60 s |
| 5–15% | 20 s | 18 s | 26 s | 25 s | – |
| <5% | 20 s | 18 s | 26 s | – | – |

Natural callouts for "phase" announces are **85%, 55%, 35% and 25%**.

**Attacks**
- **Fissure** (signature): Tell: he **draws a line with his LEFT paw, raises it and slams**, with a high-pitched sound cue.
  - 12–16 rings of **small green flames, 11 m radius, spawn around/under the player one after another, every 0.45 s**. They follow you slightly faster than walking speed.
  - **4 s after each appears**, spikes erupt and leave a **flame pool lasting 15 s** (120 Fire plus 80 Spirit). It can kill full Flametal armour in seconds and **cannot be blocked or dodged**.
  - Range ≤40 m.
  - Response: **keep moving in a straight line with short sprints. Never stand in green.**
- **Wall of Fire** (15–90%): he **draws a line with his RIGHT paw** and swipes or rotates. 12 fire circles of 4 m radius form a ring **8 m from the player**, placed clockwise 0.25 s apart from the far side. Each lasts 20 s (80 Fire plus 80 Spirit). CD 60 s, range ≤40 m.
  - Response: **escape through the gap where it started**, keep moving to stretch it, or jump over (feather cape).
- **Meteors**: Tell: **rears on his hind legs and stomps / roars.** 10 meteors fall centred on the player (40 Blunt plus 120 Fire) and leave 5 m fire pools for 20 s. Range ≤30 m.
  - Response: **look up and keep moving**; they have a big random spread.
- **Flamebreath** (5–85%): Tell: **raises his head**, then releases a **17 m line** of fire and leaves 4 m burning ground for 14 s. **Unblockable.** Range 2–20 m.
  - Response: **walk perpendicular**, or stand right under his chin. You get a free combo during the long wind-up.
- **Roar / summon** (<55%): Tell: **tilts his head at the ground, then a long roar.** He shoots 8 green orbs that each spawn a Summoned Charred Warrior or Marksman (50 HP, half damage, cap 7 of each). Range ≤100 m.
  - Response: **kill adds**, or lure them into his own fire (friendly fire applies).
- **Bite**: 210 Pierce, 10 m, 40° in front. CD 3 s, range ≤9 m. Tell: pulls his head back. Response: roll or parry.
- **Claw L/R**: 200 Pierce, 10 m, 65°. CD 3 s per variant. Response: roll or parry.
- **Spin**: Tell: **leans slightly to his right**, then spins. 140 Pierce in an **8.5 m radius**. CD 20 s, range ≤8 m. Response: back out of 8.5 m, or roll.

### 3.8 Kall Fimbulbringer: **yes, he is in the released game**
- Valheim left Early Access with **1.0 on 9 September 2026**, which added the Deep North biome and Kall as the 8th and final boss (https://beebom.com/valheim-1-0-patch-notes/, https://www.bisecthosting.com/blog/valheim-1-0-patch-notes-deep-north-biome-kall-fimbulbringer-boss-new-dungeons-armors-foods).
- **Fandom wiki status** (as of the 2026-10 fetch): it has no Kall page. Its Deep North page is still marked "Unfinished" with an empty boss field.
- **Weird Gloop Valheim wiki** (https://valheim.weirdgloop.org/w/Kall_Fimbulbringer) documents him fully. Its Boss strategies section for Kall is still empty ("...?").
- **Summon**: 3× Malicious Blood (from Jotun invasions) at the Strange Bowl in **The Prison**, reached via the **Aesir Passage**. He appears **12 s after the offering**, which is a good "pull timer" for a DBM-style mod.
- **IDs**: `FrozenKing`, `FrozenKing_p2`, `FrozenKing_p3`. **Each phase is a separate creature with its own HP pool.** When one dies, the next replaces it.
- **Health**: 10,000 + 7,000 + 30,000 = 47,000.
- **Resistances**: resists Pierce/Fire/Frost/Lightning, immune to Spirit and Stagger.
- **Messages**: "His hatred corrupts all!" on first notice, and "Peace settles over the world" on the kill.
- **World keys**: `defeated_frozenking` after phase 1, `defeated_frozenking_p3`.

**Phase 1** (`FrozenKing`)
- **Chain Slam**: 160 Blunt.
  - Left and right single slams, used only at ≥50% HP.
  - **Double-left** at ≤75% HP and **double-right** at ≤50% HP.
  - CD 3 s each, range ≤12 m.
  - Response (third-party guides): block or roll, or sidestep away from the slam side.
- **Chain Whirl**: an AoE around him, 150 Blunt plus 20 Frost. CD 10 s, range ≤8 m. Response: back out past 8 m.
- **Chain Rush**: gap closer, 125 Blunt plus 125 Pierce. CD 10 s, range 1–12 m. Response: keep spacing and sidestep.
- **Double Sweep**: 150 Blunt. CD 3 s, range 2–12 m. Response: sidestep or roll.
- HP callouts: **75% and 50%** (new slam variants).

**Phase 2** (`FrozenKing_p2`): Kall is encased in invulnerable ice and immune to everything. **Aspects** (spirit versions of earlier bosses) spawn in a tree order:
1. **Lightning Stag** (Eikthyr, 3,000 HP) comes first.
2. When it dies, **Living Forest** (Elder, 1,600) and **Writhing Dead** (Bonemass, 1,600) appear together.
3. Living Forest is followed by **Dragon Mother** (Moder, 1,500), then **Crawling Matriarch** (Queen, 1,700).
4. Writhing Dead is followed by **Twisted Soul** (Yagluth, 1,700), then **Emerald Flame** (Fader, 1,700).

- Each aspect's death explosion deals 1,000 damage to Kall, so 7 × 1,000 = phase 2 HP.
- Aspects have **most of the original bosses' abilities**, so the earlier warning modules can be **reused for the aspects**.
- Response: kill the aspects.

**Phase 3** (`FrozenKing_p3`)
- **Chain Slam (double L/R)**: 120 Blunt plus 50 Fire plus 50 Frost. CD 3 s, range 1–12 m.
- **Chain Whirl**: same mix of Fire and Frost. Only at **≥75%** HP, CD 20 s, range ≤8 m.
- **Punch**: AoE, 150 Blunt. CD 8 s, range ≤8 m.
- **Chain Flurry**: 250 Blunt. Only at **≤50%** HP, CD 3 s, range 2–12 m.
- **Double Sweep**: 150 Blunt, CD 3 s.
- **Spike Rain**: 22 spikes of 100 Blunt plus 100 Frost. Only at **≤35%** HP, CD 25 s, range ≤20 m. Response: keep moving.
- **Tendril spawn**: CD **45 s**, range ≤30 m. Tendrils have 80 HP, live 9–12 s, are immobile, are weak to Fire, and fire Hexen lightning bolts (55 Blunt every 5 s, range ≤40 m). Response: **kill adds or break line of sight.**
- HP callouts: **75%** (Whirl stops), **50%** (Flurry starts), **35%** (Spike Rain starts).
- Third-party guides add little beyond this: timesaver.gg (https://timesaver.gg/blog/valheim-kall-fimbulbringer-final-boss-how-to-beat) and mobalytics (https://mobalytics.gg/gamebase/guides/valheim-kall-fimbulbringer-boss-guide). Their attack names largely agree; timesaver says exact HP was "unconfirmed", but Weird Gloop has the numbers above.
- **Caveat**: 1.0 is only about 4 weeks old and patches are ongoing (1.0.14 on 2026-09-17 per timesaver). Treat Kall numbers as provisional and data-drive them from the game's `CharacterAttack`/`ItemData` (cooldowns, `m_attackHealthPercentage` etc.) at runtime rather than hard-coding.

### 3.9 Summary: response-type mapping (for warning templates)
| Response | Examples |
|---|---|
| Leave AoE / run out | Bonemass Poison (lean-back), Eikthyr Stomp, Yagluth Nova, Fader Spin, Kall Whirl/Punch, Queen Pierce AoE |
| Keep moving | Fader Fissure, Fader/Yagluth Meteors, Kall Spike Rain, Moder Ice Barrage |
| Sidestep / leave line | Yagluth Beam, Moder Breath, Fader Flamebreath, Eikthyr Charge, Queen Rush, Kall Chain Rush |
| Break LOS / cover | Elder Vines (pillars), Yagluth Beam (pillars), Moder (earth pillar), Kall Tendrils |
| Escape ring | Fader Wall of Fire (exit through the gap where it started) |
| Block / parry / roll | Eikthyr all attacks, Elder Stomp, Bonemass Punch, Moder Claw/Bite, Queen Slap/Bite, Fader Bite/Claw, Kall Slam/Sweep |
| Kill adds | Bonemass Throw, Elder Roots (or avoid them), Queen Call, Fader Roar, Kall Tendrils, Kall P2 aspects |
| Get behind | Bonemass vomit, Queen front-arc slaps |
| Track / relocate | Queen Teleport, Moder take-off and landing |

---

## 4. BepInEx ConfigurationManager for per-ability options
**Sources**
- Upstream repo: https://github.com/BepInEx/BepInEx.ConfigurationManager (v19.0, cloned at commit dated 2026-06-30)
- README: https://github.com/BepInEx/BepInEx.ConfigurationManager/blob/master/README.md
- Attribute template: https://github.com/BepInEx/BepInEx.ConfigurationManager/blob/master/ConfigurationManagerAttributes.cs
- BepInEx ConfigFile: https://github.com/BepInEx/BepInEx/blob/v5-lts/BepInEx/Configuration/ConfigFile.cs

**What Valheim players actually run**
- **shudnal-ConfigurationManager** v1.1.23, about 525k downloads, updated 2026-10-02, targets Valheim 1.0.7: https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/
  - It is a fork of upstream that adds Azumatt's colour drawer and aedenthorn's colouring and localisation.
  - It defaults to **Split View** (a tree of plugins and categories on the left).
  - It highlights changed values, has a file editor and a per-setting edit window, and shows ServerSync/Jotunn/CCS sync indicators.
  - Dynamic `ReadOnly`/`Browsable` are refreshed live while the window is open.
  - It can hide settings via `shudnal.ConfigurationManager.hiddensettings.json`.
  - It requires ConditionalConfigSync and is incompatible with the original CM.
- Alternatives: cjayride-ConfigurationManager (an aedenthorn fork, about 51k downloads) and treextr-Hugins_Desk_Configuration_Manager.
- **Design for the upstream attribute contract**; all the forks honour it.

**`ConfigurationManagerAttributes` fields**
- Copy the class into your project and keep it **internal**. You don't need to reference the CM dll. It is ignored if CM is absent.
- Pass it as the tag in `new ConfigDescription(desc, acceptableValues, new ConfigurationManagerAttributes{...})`.

| Field | Effect |
|---|---|
| `Order` (int?) | Sort within a category. **Higher = higher on the list**; default 0. Ties sort by `DispName`. |
| `Category` (string) | Overrides the section shown as the category header. Null = the config Section (the default comes from `entry.Definition.Section`). |
| `DispName` (string) | Display name instead of the config key. |
| `Description` (string) | Tooltip override (prefer the ConfigDescription text). |
| `Browsable` (bool?) | false = hidden entirely. |
| `IsAdvanced` (bool?) | Hidden unless the user ticks "Advanced settings" or searches for it. |
| `ReadOnly` (bool?) | Shows the value without allowing edits. |
| `DefaultValue` (object) / `HideDefaultButton` (bool?) | Controls the "Reset" button. |
| `HideSettingName` (bool?) | Use with `CustomDrawer` to get the full row width. |
| `ShowRangeAsPercent` (bool?) | For range sliders. |
| `CustomDrawer` (`Action<ConfigEntryBase>`) | Your own IMGUI (GUILayout) for the value cell. Disables most of the other fields. |
| `CustomHotkeyDrawer` (delegate with `ref bool isEditing`) | Like `CustomDrawer` but lets you poll `Input`. |
| `ObjToStr` / `StrToObj` | Custom text-box converters. |

**Built-in editors**
- bool → toggle.
- Enum → dropdown. `[Description("...")]` on enum members renames the displayed values, so it suits sound choice and bar colour type.
- `AcceptableValueList` → dropdown.
- `AcceptableValueRange` → slider; 0–1 or 0–100 is shown as %.
- `KeyboardShortcut` → key binder.
- `Color` → colour editor (the shudnal fork has a richer one).
- `Vector2`/`Vector3`/`Vector4`.
- A global per-type drawer is possible via `ConfigurationManager.RegisterCustomSettingDrawer`, but it needs a hard dll reference, so it isn't recommended.

**How categories display** (upstream `ConfigurationManager.cs` around lines 236–258 and 600–615)
- Plugins are listed alphabetically by plugin name, and each plugin is collapsible ("Plugin collapsed default" = true, "Expand All / Collapse All").
- Inside a plugin, settings are grouped by Category (the Section). **Category order = order of first appearance when enumerating the plugin's ConfigFile, then by name.**
  - BepInEx 5's `ConfigFile` is backed by a `Dictionary`, so this is effectively bind order but not guaranteed.
  - The saved `.cfg` file sorts sections **alphabetically** (`ConfigFile.Save`: `GroupBy(Section).OrderBy(Key)`).
- **Common Valheim convention: number-prefix the sections**, e.g. `1 - General`, `2 - Display`, `3 - Eikthyr`, …, `10 - Kall`. This makes both the cfg file and CM order deterministic. Beware that "10" sorts before "2" as a string, so use zero-padded `01 - …`.
- A category header is a centred 14 pt label. It is **not collapsible in upstream**; there is a single collapse level, per plugin. The shudnal fork's Split View does give a category tree.
- "Hide single sections" hides the header when a plugin has only one section.
- Search filters across all settings and shows advanced ones that match.

**Limits and implications for per-ability options**
- **No nested groups**: one level (Section/Category), then flat rows. A DBM-style "ability box" with announce, special plus sound, and timer plus colour plus countdown has two possible representations:
  - (a) **Section per boss**, with keys like `Fader: Fissure – Special warning`, `Fader: Fissure – Sound`, `Fader: Fissure – Timer`, using `Order` to keep each ability's rows together (descending Order values per ability block).
  - (b) **One row per ability with a `CustomDrawer`** that draws several toggles and dropdowns inline. The backing value would be a string, flags enum or small serialised struct (`HideSettingName = true` for width).
  - Option (a) is plain and robust. Option (b) is closer to DBM but the custom IMGUI must work in every CM fork.
- Volume: 8 bosses × about 5–8 abilities × 3–5 settings is roughly 150–300 entries. This is workable but long. Mitigations:
  - Use `IsAdvanced` for colours and countdown voice.
  - Collapse per-ability toggles into a single **flags enum** (e.g. `Announce | Special | Timer | Countdown`). Upstream CM renders `[Flags]` enums as multiple toggles, so that needs checking in the shudnal fork.
  - Put global defaults in `01 - General`.
- CM is IMGUI (F1 by default) and has **no live preview hook**. Provide a "Test warning" **`KeyboardShortcut`**, or a `CustomDrawer` button that fires a sample warning (the DBM test-button analogue).
- With ServerSync or CCS (common on Valheim servers), consider whether any settings should be server-locked. Probably none should, since warnings are client-side.

---

## 5. Key URLs (consolidated)
**Thunderstore**
- API: https://thunderstore.io/c/valheim/api/v1/package/
- https://thunderstore.io/c/valheim/p/RAGEmedia/Forewarned/
- https://thunderstore.io/c/valheim/p/GBV/Gjallar/
- https://thunderstore.io/c/valheim/p/j1gA/ExtendedBosses/
- https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/

**Nexus**
- GraphQL API: https://api.nexusmods.com/v2/graphql
- Enhanced Bosses: https://www.nexusmods.com/valheim/mods/1880

**Vanilla name check**
- https://valheim.weirdgloop.org/w/Gjall
- https://game8.co/games/Valheim/archives/619520
- https://steamcommunity.com/app/892970/discussions/2/3073117690269721957/

**DBM**
- https://github.com/DeadlyBossMods/DeadlyBossMods, specifically:
  - `DBM-Core/modules/objects/CoreOptions.lua`
  - `DBM-Core/modules/objects/SpecialWarning.lua`
  - `DBM-Core/modules/objects/Announce.lua`
  - `DBM-Core/modules/objects/Timer.lua`
  - `DBM-Core/DBM-Flash.lua`
  - `DBM-Core/DBM-Arrow.lua`
  - `DBM-Core/VoicePackSounds.lua`
  - `DBM-Core/localization.en.lua`
  - `DBM-StatusBarTimers/DBT.lua`
  - `DBM-GUI/modules/PanelPrototype.lua`
  - `DBM-GUI/modules/options/alerts/SpecialAnnouncements.lua`
  - `DBM-GUI/localization.en.lua`
- https://www.curseforge.com/wow/addons/deadly-boss-mods

**BigWigs**
- https://github.com/BigWigsMods/BigWigs: `Core/Constants.lua`, `Plugins/Messages.lua`, `Plugins/Bars.lua`, `Plugins/Countdown.lua`, `Plugins/Colors.lua`, `Plugins/Sound.lua`

**Valheim wikis**
- Fandom: https://valheim.fandom.com/wiki/Eikthyr, /The_Elder, /Bonemass, /Moder, /Yagluth, /The_Queen, /Fader, /Boss_strategies, /Deep_North
- Weird Gloop: https://valheim.weirdgloop.org/w/Kall_Fimbulbringer

**Valheim 1.0 / Kall**
- https://beebom.com/valheim-1-0-patch-notes/
- https://www.bisecthosting.com/blog/valheim-1-0-patch-notes-deep-north-biome-kall-fimbulbringer-boss-new-dungeons-armors-foods
- https://timesaver.gg/blog/valheim-kall-fimbulbringer-final-boss-how-to-beat
- https://mobalytics.gg/gamebase/guides/valheim-kall-fimbulbringer-boss-guide

**ConfigurationManager**
- https://github.com/BepInEx/BepInEx.ConfigurationManager (README, `ConfigurationManagerAttributes.cs`, `ConfigurationManager.Shared/ConfigurationManager.cs`)
- https://github.com/BepInEx/BepInEx/blob/v5-lts/BepInEx/Configuration/ConfigFile.cs
