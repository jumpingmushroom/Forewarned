# Forewarned M1 · Plan 2: Game-Side Capture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The plugin sees boss animator triggers and hit events in game, reads each boss's live
attack numbers, feeds the pure `Engine` from plan 1, exposes every option in F1, and prints what it
saw with the `forewarned` console command. Adds the Eikthyr module (the first boss tested in game).

**Architecture:** Thin Unity/game adapters under `src/Forewarned/Core/` and `src/Forewarned/Patches/`
turn game objects into the model's plain types (PLAN.md §11.3). All decisions stay in the pure model
(`Core/Model`), which only gains the Eikthyr module and its strings. The HUD comes in plan 3; until
then the console and the log are the only output.

**Tech Stack:** C# net472, BepInEx 5 (`ConfigFile`, `ConfigEntry<T>`), HarmonyLib 2, Unity
(`Time`, `Vector3`, `Component`), Valheim (publicized `assembly_valheim`).

**Spec:** `PLAN.md` §9 (decisions, incl. "Eikthyr joins milestone 1"), §10 (ability table), §11.3
(game side), §11.5 (config, translations). Plan 1 is done: the model API below exists.

## Global Constraints

- Repo root `/workspace/gamemods/Valheim/Forewarned`; dotnet needs `export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 PATH="$HOME/.dotnet:$PATH"`.
- `src/Forewarned/Core/Model/**` stays pure: no UnityEngine/BepInEx/HarmonyLib/game types; net472-safe (no Math.Clamp, MathF, init, records). Game-side code lives in `src/Forewarned/Core/*.cs` (namespace `Forewarned.Core`) and `src/Forewarned/Patches/` (namespace `Forewarned.Patches`). Never name a namespace `Forewarned.Game` (it would shadow Valheim's `Game` class).
- Every Harmony patch body and every per-frame tick is wrapped in try/catch → `ForewarnedPlugin.WarnOnce(key, e)`.
- Nothing runs without `Player.m_localPlayer`; all state is cleared when the local player goes away (logout).
- Config file `com.jumpingmushroom.forewarned.cfg`; sections `00 General`, then one per boss named `NN DisplayName` with NN = `BossModule.Order` two-digit (`03 Eikthyr`, `06 Moder`, `09 Fader`). Per-ability keys `<Name>: warning`, `<Name>: sound`, `<Name>: visual`, advanced `<Name>: level`, `<Name>: always warn`. Defaults from the spec (`DefaultOn`, `DefaultSettings.DefaultSound/DefaultVisual`, `DefaultLevel`).
- Boss tracking range 120 m; boss fight active = a tracked boss within 100 m that is alerted; scan every 0.2 s.
- Commit and push after every task. No AI attribution. Never write the rig address into tracked files.
- Game-side code can't run on the build box: verification is `dotnet build` with 0 errors and 0 warnings, plus the model tests. In-game checks happen in the user's Eikthyr session after plan 3.

## Model API from plan 1 (for reference)

`Engine(ModuleRegistry, Translations, Func<string,string> localize, IAbilitySettings)`;
`Engine.OnTrigger(TriggerEvent e, Scene scene, bool fightActive) → TriggerKind`; `OnHit(long bossId, float time)`;
`OnHealth(long bossId, string prefab, float hp, float now)`; `OnState(long bossId, string prefab, string state, bool value, float now)`;
`OnPull(long bossId, string prefab, float now)`; `Forget(long bossId)`; `Tick(float now)`; `Clear()`;
`SetLiveNumbers(string prefab, string abilityId, AbilityNumbers n)`; `LiveNumbers(prefab, abilityId)`;
`Registry` (`Tracks`, `ModuleFor`, `Classify`, `Modules`), `Tracker` (`LearnedWindUp`, `SampleCount`, `Health`),
`Board`, `Announcer`, `Recent` (`TriggerRecord { Time, Prefab, Trigger, Outcome }`), `BossName(BossModule)`.
`TriggerEvent { BossId, Prefab, Trigger, Time }`; `Scene { BossPos, BossFacing, Me, Others }`; `Vec2(x, z)`;
`AbilityNumbers` nullable `Cooldown, HpMin, HpMax, AiRange, MaxAngle, Range, Angle, Width, Radius, Offset`;
`AbilitySpec` (`Id, Name, ItemPrefab, Triggers, DefaultLevel, DefaultOn, Shape { Source, ... }, WindUp, Cooldown, ...`);
`ShapeSource { Fixed, AttackCone, AttackSphere, SpawnAbility, Aoe }`; `IAbilitySettings`; `DefaultSettings.DefaultSound/DefaultVisual`;
`BossList.All`; `Translations.Parse/WithFallback/Get`; `TextCheck.IsClean`.

---

### Task 1: The Eikthyr module

**Files:**
- Create: `src/Forewarned/Core/Model/Bosses/EikthyrModule.cs`
- Modify: `src/Forewarned/Core/Model/Bosses/BossList.cs`, `src/Forewarned/translations/English.txt`, `tests/Forewarned.Tests/DataFilesTests.cs` (module count 2 → 3)
- Test: `tests/Forewarned.Tests/EikthyrTests.cs`

**Interfaces:**
- Produces: `EikthyrModule` (Key `eikthyr`, DisplayName `Eikthyr`, NameToken `$enemy_eikthyr`, Order 3, prefab `Eikthyr`), `BossList.All` = Eikthyr, Moder, Fader. New translation keys `action.sidestep_line`, `action.block_step_aside`, `eikthyr.*.name`.

- [ ] **Step 1: Write the failing test** — `tests/Forewarned.Tests/EikthyrTests.cs`

```csharp
using System.Linq;
using Forewarned.Core.Model;
using Forewarned.Core.Model.Bosses;
using Xunit;

namespace Forewarned.Tests
{
    public class EikthyrTests
    {
        private static Engine Make() =>
            new Engine(new ModuleRegistry(BossList.All), DataFilesTests.English(), t => t == "$enemy_eikthyr" ? "Eikthyr" : t, new DefaultSettings());

        private static Scene At(float x, float z) =>
            new Scene { BossPos = new Vec2(0f, 0f), BossFacing = new Vec2(0f, 1f), Me = new Vec2(x, z) };

        private static TriggerEvent E(string trigger, float time) =>
            new TriggerEvent { BossId = 1, Prefab = "Eikthyr", Trigger = trigger, Time = time };

        [Fact]
        public void StompTellsYouToRunTenMetres()
        {
            Engine e = Make();
            e.OnTrigger(E("attack_stomp", 10f), At(0f, 8f), true);
            Warning w = Assert.Single(e.Board.Active);
            Assert.Equal("STOMP", w.Title);
            Assert.Equal("Run out, 10 m", w.Action);
            Assert.Equal(12.89f, w.HitAt, 3);
            Assert.Equal(Level.Danger, w.Level);
        }

        [Fact]
        public void ChargeOnlyWarnsThoseInFront()
        {
            Engine inFront = Make();
            inFront.OnTrigger(E("attack2", 10f), At(0f, 12f), true);
            Assert.Equal("Sidestep out of the line", Assert.Single(inFront.Board.Active).Action);

            Engine behind = Make();
            behind.OnTrigger(E("attack2", 10f), At(0f, -12f), true);
            Assert.Empty(behind.Board.Active);
            Assert.Equal("Eikthyr: Charge", Assert.Single(behind.Announcer.Lines).Text);
        }

        [Fact]
        public void AntlerIsCautionAndOffByDefault()
        {
            AbilitySpec antler = new EikthyrModule().Abilities.Single(a => a.Id == "eikthyr.antler");
            Assert.Equal(Level.Caution, antler.DefaultLevel);
            Assert.False(antler.DefaultOn);
            Engine e = Make();
            e.OnTrigger(E("attack1", 10f), At(0f, 2f), true);
            Assert.Empty(e.Board.Active);
        }
    }
}
```

- [ ] **Step 2: Run to see it fail**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: build error, `'EikthyrModule' could not be found`.

- [ ] **Step 3: Write `src/Forewarned/Core/Model/Bosses/EikthyrModule.cs`**

Numbers from PLAN.md §4 (Eikthyr) and docs/research/data.md §7.

```csharp
using System.Collections.Generic;

namespace Forewarned.Core.Model.Bosses
{
    /// <summary>Eikthyr, Meadows. PLAN.md §4 and §10. His stomp has no wind-up sound in vanilla.</summary>
    public sealed class EikthyrModule : BossModule
    {
        private static readonly string[] PrefabList = { "Eikthyr" };

        private static readonly AbilitySpec[] AbilityList =
        {
            new AbilitySpec
            {
                Id = "eikthyr.stomp", Name = "Stomp", ItemPrefab = "Eikthyr_stomp",
                Triggers = new[] { "attack_stomp" }, DefaultLevel = Level.Danger,
                ActionKey = "action.run_out_m", Response = Response.LeaveArea,
                Shape = Shape.Circle(10f, 3f, Anchor.Boss, ShapeSource.AttackSphere),
                Cooldown = 40f, WindUp = 2.89f, AiRange = 6f, MaxAngle = 60f
            },
            new AbilitySpec
            {
                Id = "eikthyr.charge", Name = "Charge", ItemPrefab = "Eikthyr_charge",
                Triggers = new[] { "attack2" }, DefaultLevel = Level.Danger,
                ActionKey = "action.sidestep_line", Response = Response.LeaveLine,
                Shape = Shape.Cone(20f, 45f), Cooldown = 25f, WindUp = 1.56f, AiRange = 15f, MaxAngle = 15f
            },
            new AbilitySpec
            {
                Id = "eikthyr.antler", Name = "Antler", ItemPrefab = "Eikthyr_antler",
                Triggers = new[] { "attack1" }, DefaultLevel = Level.Caution, DefaultOn = false,
                ActionKey = "action.block_step_aside", Response = Response.Parry,
                Shape = Shape.Cone(4.5f, 25f), Cooldown = 5f, WindUp = 0.6f, AiRange = 4f, MaxAngle = 30f
            }
        };

        public override string Key => "eikthyr";
        public override string DisplayName => "Eikthyr";
        public override string NameToken => "$enemy_eikthyr";
        public override int Order => 3;
        public override string[] Prefabs => PrefabList;
        public override IReadOnlyList<AbilitySpec> Abilities => AbilityList;
    }
}
```

- [ ] **Step 4: Register it** — in `BossList.cs` make the array:

```csharp
        public static readonly IReadOnlyList<BossModule> All = new BossModule[]
        {
            new EikthyrModule(),
            new ModerModule(),
            new FaderModule()
        };
```

and update the summary comment to "Every boss module, in game order. Milestone 2 adds the other five."

- [ ] **Step 5: Strings** — in `English.txt`, add after `action.parry_step_back = Parry or step back`:

```
action.sidestep_line = Sidestep out of the line
action.block_step_aside = Block or step aside
```

and before the `moder.` block:

```
eikthyr.stomp.name = Stomp
eikthyr.charge.name = Charge
eikthyr.antler.name = Antler

```

- [ ] **Step 6: Update the module count** in `DataFilesTests.TheRegistryBuildsFromEveryModule`: `Assert.Equal(3, r.Modules.Count);`

- [ ] **Step 7: Run the tests**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: `Passed: 109` (106 + 3), 0 failed.

- [ ] **Step 8: Commit and push**

```bash
git add src tests
git commit -m "Model: the Eikthyr module"
git push
```

---

### Task 2: Config binding and settings

**Files:**
- Create: `src/Forewarned/PluginConfig.cs`, `src/Forewarned/Core/ConfigSettings.cs`
- Modify: `src/Forewarned/Plugin.cs` (bind in `Awake`)

**Interfaces:**
- Consumes: `BossModule`, `AbilitySpec`, `Level`, `DefaultSettings`, `IAbilitySettings`, `BossList.All`.
- Produces: `static class PluginConfig` (namespace `Forewarned`): `ConfigEntry<bool> Enabled, OnlyDuringBossFight, LogUnmappedTriggers, Verbose`; `static void Bind(ConfigFile cfg, IEnumerable<BossModule> modules)`; lookups `BossEnabled(BossModule)`, `Announces(BossModule)`, `Warn(AbilitySpec)`, `Sound(AbilitySpec)`, `Visual(AbilitySpec)`, `LevelOf(AbilitySpec)`, `AlwaysWarn(AbilitySpec)`. `sealed class ConfigSettings : IAbilitySettings` (namespace `Forewarned.Core`) delegating to it.

- [ ] **Step 1: Write `src/Forewarned/PluginConfig.cs`**

```csharp
using System.Collections.Generic;
using BepInEx.Configuration;
using Forewarned.Core.Model;

namespace Forewarned
{
    /// <summary>
    /// PLAN.md §11.5: every option in F1. "00 General", then a section per boss in game order with an
    /// Enabled switch, pull and phase announces, and three rows per ability (warning, sound, visual)
    /// plus advanced level and always-warn rows. Abilities whose default level is None (no hit) get no rows.
    /// </summary>
    public static class PluginConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> OnlyDuringBossFight;
        public static ConfigEntry<bool> LogUnmappedTriggers;
        public static ConfigEntry<bool> Verbose;

        private sealed class BossRows
        {
            public ConfigEntry<bool> Enabled;
            public ConfigEntry<bool> Announces;
        }

        private sealed class AbilityRows
        {
            public ConfigEntry<bool> Warn;
            public ConfigEntry<bool> Sound;
            public ConfigEntry<bool> Visual;
            public ConfigEntry<Level> Level;
            public ConfigEntry<bool> AlwaysWarn;
        }

        private static readonly Dictionary<BossModule, BossRows> Bosses = new Dictionary<BossModule, BossRows>();
        private static readonly Dictionary<AbilitySpec, AbilityRows> Abilities = new Dictionary<AbilitySpec, AbilityRows>();

        private static ConfigurationManagerAttributes Attr(int order, bool advanced = false) =>
            new ConfigurationManagerAttributes { Order = order, IsAdvanced = advanced };

        public static void Bind(ConfigFile cfg, IEnumerable<BossModule> modules)
        {
            const string general = "00 General";
            Enabled = cfg.Bind(general, "Enabled", true,
                new ConfigDescription("Master switch for all boss warnings.", null, Attr(100)));
            OnlyDuringBossFight = cfg.Bind(general, "OnlyDuringBossFight", true,
                new ConfigDescription("Warn only while a boss within 100 m is alerted (its health bar is up).", null, Attr(99)));
            LogUnmappedTriggers = cfg.Bind(general, "LogUnmappedTriggers", false,
                new ConfigDescription("Log boss animation triggers no module knows, once each, to catch attacks renamed by a game update.", null, Attr(10, true)));
            Verbose = cfg.Bind(general, "Verbose", false,
                new ConfigDescription("Log every boss trigger, hit and decision. Noisy.", null, Attr(9, true)));

            foreach (BossModule m in modules)
                BindBoss(cfg, m);
        }

        private static void BindBoss(ConfigFile cfg, BossModule m)
        {
            string section = m.Order.ToString("00") + " " + m.DisplayName;
            Bosses[m] = new BossRows
            {
                Enabled = cfg.Bind(section, "Enabled", true,
                    new ConfigDescription("Warnings for " + m.DisplayName + ".", null, Attr(1000))),
                Announces = cfg.Bind(section, "Pull and phase announces", true,
                    new ConfigDescription("Announce lines when the fight starts and at health thresholds.", null, Attr(999)))
            };

            int order = 990;
            foreach (AbilitySpec a in m.Abilities)
            {
                if (a.DefaultLevel == Level.None)
                    continue;
                string what = m.DisplayName + "'s " + a.Name.ToLowerInvariant();
                Abilities[a] = new AbilityRows
                {
                    Warn = cfg.Bind(section, a.Name + ": warning", a.DefaultOn,
                        new ConfigDescription("Warn when " + what + " starts (" + a.DefaultLevel + " by default).", null, Attr(order))),
                    Sound = cfg.Bind(section, a.Name + ": sound", DefaultSettings.DefaultSound(a),
                        new ConfigDescription("Play the alert sound with this warning.", null, Attr(order - 1))),
                    Visual = cfg.Bind(section, a.Name + ": visual", DefaultSettings.DefaultVisual(a),
                        new ConfigDescription("Draw the danger area and the way out on the ground.", null, Attr(order - 2))),
                    Level = cfg.Bind(section, a.Name + ": level", a.DefaultLevel,
                        new ConfigDescription("Urgency: Danger (big warning, horn, flash), Caution (smaller, chime), Info (announce line only).", null, Attr(order - 3, true))),
                    AlwaysWarn = cfg.Bind(section, a.Name + ": always warn", false,
                        new ConfigDescription("Warn even when you're clear of the attack.", null, Attr(order - 4, true)))
                };
                order -= 10;
            }
        }

        public static bool BossEnabled(BossModule m)
        {
            BossRows r;
            return !Bosses.TryGetValue(m, out r) || r.Enabled.Value;
        }

        public static bool Announces(BossModule m)
        {
            BossRows r;
            return !Bosses.TryGetValue(m, out r) || r.Announces.Value;
        }

        public static bool Warn(AbilitySpec a)
        {
            AbilityRows r;
            return Abilities.TryGetValue(a, out r) ? r.Warn.Value : a.DefaultOn;
        }

        public static bool Sound(AbilitySpec a)
        {
            AbilityRows r;
            return Abilities.TryGetValue(a, out r) ? r.Sound.Value : DefaultSettings.DefaultSound(a);
        }

        public static bool Visual(AbilitySpec a)
        {
            AbilityRows r;
            return Abilities.TryGetValue(a, out r) ? r.Visual.Value : DefaultSettings.DefaultVisual(a);
        }

        public static Level LevelOf(AbilitySpec a)
        {
            AbilityRows r;
            return Abilities.TryGetValue(a, out r) ? r.Level.Value : a.DefaultLevel;
        }

        public static bool AlwaysWarn(AbilitySpec a)
        {
            AbilityRows r;
            return Abilities.TryGetValue(a, out r) && r.AlwaysWarn.Value;
        }
    }
}
```

- [ ] **Step 2: Write `src/Forewarned/Core/ConfigSettings.cs`**

```csharp
using Forewarned.Core.Model;

namespace Forewarned.Core
{
    /// <summary>The model's view of the player's F1 choices. Reads live, so changes apply at once.</summary>
    internal sealed class ConfigSettings : IAbilitySettings
    {
        public bool Enabled => PluginConfig.Enabled.Value;
        public bool OnlyDuringBossFight => PluginConfig.OnlyDuringBossFight.Value;
        public bool BossEnabled(BossModule m) => PluginConfig.BossEnabled(m);
        public bool Announces(BossModule m) => PluginConfig.Announces(m);
        public bool Warn(AbilitySpec a) => PluginConfig.Warn(a);
        public bool Sound(AbilitySpec a) => PluginConfig.Sound(a);
        public bool Visual(AbilitySpec a) => PluginConfig.Visual(a);
        public Level LevelOf(AbilitySpec a) => PluginConfig.LevelOf(a);
        public bool AlwaysWarn(AbilitySpec a) => PluginConfig.AlwaysWarn(a);
    }
}
```

- [ ] **Step 3: Bind in `Plugin.cs`** — add `using Forewarned.Core.Model.Bosses;` and, in `Awake`, right after `Log = Logger;`:

```csharp
            PluginConfig.Bind(Config, BossList.All);
```

- [ ] **Step 4: Build and test**

```bash
dotnet build src/Forewarned/Forewarned.csproj -c Release --nologo -v minimal
dotnet test tests/Forewarned.Tests --nologo -v minimal
```
Expected: build 0 errors, 0 warnings; `Passed: 109`.

- [ ] **Step 5: Commit and push**

```bash
git add src
git commit -m "Config: general and per-boss, per-ability options"
git push
```

---

### Task 3: Runtime, boss tracking, live data and the two patches

**Files:**
- Create: `src/Forewarned/Core/Runtime.cs`, `src/Forewarned/Core/BossWatch.cs`, `src/Forewarned/Core/LiveData.cs`, `src/Forewarned/Patches/AnimTriggerPatch.cs`, `src/Forewarned/Patches/AnimHitPatch.cs`
- Modify: `src/Forewarned/Plugin.cs` (`Update` → `Runtime.Tick`), `src/Forewarned/Forewarned.csproj` (reference `UnityEngine.AnimationModule`)

**Interfaces:**
- Consumes: `Engine` and model types; `PluginConfig`; `ConfigSettings`.
- Produces:
  - `static class Runtime` (namespace `Forewarned.Core`): `Engine Engine`, `bool Ready`, `void Tick()`, `void Debug(string line)` (logs only when `Verbose`).
  - `sealed class TrackedBoss { Character Character; string Prefab; long Id; float Health; bool Alerted; bool Pulled; bool Flying; bool FlyingKnown; bool LiveRead; }`.
  - `static class BossWatch`: `TrackedBoss Find(Character c)`, `IEnumerable<TrackedBoss> All`, `bool FightActive()`, `Scene SceneFor(Character boss)`, `void Tick(float now)`, `void Clear()`.
  - `static class LiveData`: `bool Read(Humanoid boss, string prefab)` (true when the boss's inventory was readable).

- [ ] **Step 1: Reference the animation module** — `lib/UnityEngine.AnimationModule.dll` is already present (copied from the rig). Add to the Unity reference group in `Forewarned.csproj`:

```xml
    <Reference Include="UnityEngine.AnimationModule"     HintPath="$(ValheimManaged)/UnityEngine.AnimationModule.dll" Private="false" />
```

- [ ] **Step 2: Write `src/Forewarned/Core/Runtime.cs`**

```csharp
using System.IO;
using System.Reflection;
using BepInEx;
using Forewarned.Core.Model;
using Forewarned.Core.Model.Bosses;
using UnityEngine;

namespace Forewarned.Core
{
    /// <summary>Owns the engine and runs the per-frame tick. Static: one game, one engine.</summary>
    internal static class Runtime
    {
        public static Engine Engine;
        private static bool _hadPlayer;

        public static bool Ready => Engine != null;

        public static void Tick()
        {
            if (!Ready)
                TryInit();
            if (!Ready)
                return;
            if (Player.m_localPlayer == null)
            {
                if (_hadPlayer)
                {
                    Engine.Clear();
                    BossWatch.Clear();
                    _hadPlayer = false;
                }
                return;
            }
            _hadPlayer = true;
            float now = Time.time;
            BossWatch.Tick(now);
            Engine.Tick(now);
        }

        public static void Debug(string line)
        {
            if (PluginConfig.Verbose.Value)
                ForewarnedPlugin.Log.LogInfo(line);
        }

        /// <summary>Localization.instance initialises itself on first access, so this runs on the first
        /// frame and fixes the language for the session: a language change needs a restart.</summary>
        private static void TryInit()
        {
            if (Localization.instance == null)
                return;
            Translations english = Translations.Parse(ReadResource("Forewarned.English.txt"));
            string language = Localization.instance.GetSelectedLanguage();
            string custom = Path.Combine(Path.Combine(Path.Combine(Paths.ConfigPath, "Forewarned"), "translations"), language + ".txt");
            Translations tr = File.Exists(custom) ? Translations.Parse(File.ReadAllText(custom)).WithFallback(english) : english;
            Engine = new Engine(new ModuleRegistry(BossList.All), tr, token => Localization.instance.Localize(token), new ConfigSettings());
            ForewarnedPlugin.Log.LogInfo("Forewarned ready: " + Engine.Registry.Modules.Count + " boss modules, language " + language +
                (File.Exists(custom) ? " (custom strings from " + custom + ")" : ""));
        }

        private static string ReadResource(string name)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null)
                {
                    ForewarnedPlugin.Log.LogError("Missing embedded resource " + name);
                    return "";
                }
                using (var r = new StreamReader(s))
                    return r.ReadToEnd();
            }
        }
    }
}
```

- [ ] **Step 3: Write `src/Forewarned/Core/BossWatch.cs`**

```csharp
using System.Collections.Generic;
using Forewarned.Core.Model;
using UnityEngine;

namespace Forewarned.Core
{
    internal sealed class TrackedBoss
    {
        public Character Character;
        public string Prefab;
        public long Id;
        public float Health = -1f;
        public bool Alerted;
        public bool Pulled;
        public bool Flying;
        public bool FlyingKnown;
        public bool LiveRead;
    }

    /// <summary>
    /// PLAN.md §11.3: every 0.2 s, finds the bosses (and later aspects) within 120 m that a module covers,
    /// and feeds their health, alert and flying state to the engine. Everything read here comes from the
    /// boss's network data, so it works on clients that don't own the boss.
    /// </summary>
    internal static class BossWatch
    {
        private const float Interval = 0.2f;
        private const float TrackRange = 120f;
        private const float FightRange = 100f;

        /// <summary>Character's animator bool "flying", as the owner writes it to the ZDO (decomp ZSyncAnimation.cs:159-170).</summary>
        private static readonly int FlyingKey = 438569 + ZSyncAnimation.GetHash("flying");

        private static readonly Dictionary<int, TrackedBoss> Tracked = new Dictionary<int, TrackedBoss>();
        private static readonly HashSet<int> NotBosses = new HashSet<int>();
        private static readonly List<int> Stale = new List<int>();
        private static float _next;

        public static IEnumerable<TrackedBoss> All => Tracked.Values;

        public static TrackedBoss Find(Character c)
        {
            TrackedBoss t;
            return c != null && Tracked.TryGetValue(c.GetInstanceID(), out t) ? t : null;
        }

        public static bool FightActive()
        {
            Player me = Player.m_localPlayer;
            if (me == null)
                return false;
            foreach (TrackedBoss t in Tracked.Values)
                if (t.Alerted && t.Character != null &&
                    Vector3.Distance(t.Character.transform.position, me.transform.position) <= FightRange)
                    return true;
            return false;
        }

        public static Scene SceneFor(Character boss)
        {
            Vector3 p = boss.transform.position;
            Vector3 f = boss.transform.forward;
            Vector3 m = Player.m_localPlayer.transform.position;
            var scene = new Scene { BossPos = new Vec2(p.x, p.z), BossFacing = new Vec2(f.x, f.z), Me = new Vec2(m.x, m.z) };
            foreach (Player other in Player.GetAllPlayers())
                if (other != null && other != Player.m_localPlayer && !other.IsDead())
                    scene.Others.Add(new Vec2(other.transform.position.x, other.transform.position.z));
            return scene;
        }

        public static void Tick(float now)
        {
            if (now < _next)
                return;
            _next = now + Interval;
            Vector3 me = Player.m_localPlayer.transform.position;

            Stale.Clear();
            foreach (KeyValuePair<int, TrackedBoss> kv in Tracked)
            {
                Character c = kv.Value.Character;
                if (c == null || c.IsDead() || Vector3.Distance(c.transform.position, me) > TrackRange + 20f)
                    Stale.Add(kv.Key);
            }
            foreach (int key in Stale)
            {
                Runtime.Engine.Forget(Tracked[key].Id);
                Tracked.Remove(key);
            }

            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c is Player || c.IsDead())
                    continue;
                int key = c.GetInstanceID();
                if (NotBosses.Contains(key))
                    continue;
                TrackedBoss t;
                if (!Tracked.TryGetValue(key, out t))
                {
                    if (Vector3.Distance(c.transform.position, me) > TrackRange)
                        continue;
                    string prefab = Utils.GetPrefabName(c.gameObject);
                    if (!Runtime.Engine.Registry.Tracks(prefab))
                    {
                        NotBosses.Add(key);
                        continue;
                    }
                    t = new TrackedBoss { Character = c, Prefab = prefab, Id = key };
                    Tracked[key] = t;
                    Runtime.Debug("Forewarned: tracking " + prefab + " (" + key + ")");
                }
                Update(t, me, now);
            }
        }

        private static void Update(TrackedBoss t, Vector3 me, float now)
        {
            Character c = t.Character;
            if (!t.LiveRead)
                t.LiveRead = LiveData.Read(c as Humanoid, t.Prefab);

            float hp = c.GetHealthPercentage();
            if (hp != t.Health)
            {
                t.Health = hp;
                Runtime.Engine.OnHealth(t.Id, t.Prefab, hp, now);
            }

            BaseAI ai = c.GetBaseAI();
            t.Alerted = ai != null && ai.IsAlerted();
            if (!t.Alerted)
                t.Pulled = false;
            else if (!t.Pulled && Vector3.Distance(c.transform.position, me) <= FightRange)
            {
                t.Pulled = true;
                Runtime.Engine.OnPull(t.Id, t.Prefab, now);
            }

            bool flying = ReadFlying(c);
            if (!t.FlyingKnown || flying != t.Flying)
            {
                if (t.FlyingKnown)
                    Runtime.Engine.OnState(t.Id, t.Prefab, "flying", flying, now);
                t.Flying = flying;
                t.FlyingKnown = true;
            }
        }

        private static bool ReadFlying(Character c)
        {
            ZNetView nview = c.m_nview;
            ZDO zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            return zdo != null && zdo.GetInt(FlyingKey, 0) != 0;
        }

        public static void Clear()
        {
            Tracked.Clear();
            NotBosses.Clear();
            _next = 0f;
        }
    }
}
```

- [ ] **Step 4: Write `src/Forewarned/Core/LiveData.cs`**

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Forewarned.Core.Model;
using UnityEngine;

namespace Forewarned.Core
{
    /// <summary>
    /// PLAN.md §11.3: reads each ability's numbers from the boss's own inventory item, which every
    /// client holds (Humanoid.GiveDefaultItems runs everywhere). Missing items or components fall back
    /// to the offline numbers and are logged once.
    /// </summary>
    internal static class LiveData
    {
        private static readonly HashSet<string> Warned = new HashSet<string>();

        /// <returns>False while the boss's inventory isn't filled yet, so BossWatch retries on its next scan.</returns>
        public static bool Read(Humanoid boss, string prefab)
        {
            if (boss == null)
                return true;
            List<ItemDrop.ItemData> items = boss.GetInventory()?.GetAllItems();
            if (items == null || items.Count == 0)
                return false;
            BossModule module = Runtime.Engine.Registry.ModuleFor(prefab);
            var log = new StringBuilder("Forewarned: live numbers for " + prefab + ":");
            foreach (AbilitySpec a in module.Abilities)
            {
                ItemDrop.ItemData item = items.Find(i => i.m_dropPrefab != null && i.m_dropPrefab.name == a.ItemPrefab);
                if (item == null)
                {
                    if (Warned.Add(prefab + "/" + a.ItemPrefab))
                        ForewarnedPlugin.Log.LogWarning("Forewarned: " + prefab + " has no item " + a.ItemPrefab + "; using offline numbers for " + a.Id);
                    continue;
                }
                AbilityNumbers n = Numbers(item.m_shared, a);
                Runtime.Engine.SetLiveNumbers(prefab, a.Id, n);
                log.Append(" ").Append(a.Id).Append(" cd ").Append(F(n.Cooldown)).Append(" range ").Append(F(n.AiRange))
                   .Append(" hp ").Append(F(n.HpMin)).Append("-").Append(F(n.HpMax));
                if (n.Radius.HasValue) log.Append(" r ").Append(F(n.Radius));
                if (n.Range.HasValue) log.Append(" len ").Append(F(n.Range));
                if (n.Angle.HasValue) log.Append(" angle ").Append(F(n.Angle));
                if (n.Width.HasValue) log.Append(" width ").Append(F(n.Width));
                log.Append(";");
            }
            ForewarnedPlugin.Log.LogInfo(log.ToString());
            return true;
        }

        private static AbilityNumbers Numbers(ItemDrop.ItemData.SharedData s, AbilitySpec a)
        {
            var n = new AbilityNumbers
            {
                Cooldown = s.m_aiAttackInterval,
                HpMin = s.m_aiMinHealthPercentage,
                HpMax = s.m_aiMaxHealthPercentage,
                AiRange = s.m_aiAttackRange,
                MaxAngle = s.m_aiAttackMaxAngle
            };
            Attack at = s.m_attack;
            if (at == null)
                return n;
            switch (a.Shape.Source)
            {
                case ShapeSource.AttackCone:
                    n.Range = at.m_attackRange;
                    n.Angle = at.m_attackAngle;
                    break;
                case ShapeSource.AttackSphere:
                    n.Radius = at.m_attackRayWidth;
                    n.Offset = at.m_attackRange;
                    break;
                case ShapeSource.SpawnAbility:
                    SpawnShape(at.m_attackProjectile, n);
                    break;
                case ShapeSource.Aoe:
                    AoeShape(at.m_attackProjectile, n);
                    break;
            }
            return n;
        }

        /// <summary>A spread spawner (meteors, wall of fire) is sized by its spawn radius; a spawner that
        /// drops its AoEs on the target (fissure) by the AoE's own radius.</summary>
        private static void SpawnShape(GameObject projectile, AbilityNumbers n)
        {
            SpawnAbility sa = projectile != null ? projectile.GetComponent<SpawnAbility>() : null;
            if (sa == null)
                return;
            if (sa.m_spawnRadius > 0f)
            {
                n.Radius = sa.m_spawnRadius;
                return;
            }
            if (sa.m_spawnPrefab == null)
                return;
            foreach (GameObject spawned in sa.m_spawnPrefab)
            {
                Aoe aoe = spawned != null ? spawned.GetComponent<Aoe>() : null;
                if (aoe != null)
                {
                    n.Radius = aoe.m_radius;
                    return;
                }
            }
        }

        /// <summary>A trigger-box AoE (flame breath) gives a strip; a sphere AoE gives a circle.</summary>
        private static void AoeShape(GameObject projectile, AbilityNumbers n)
        {
            if (projectile == null)
                return;
            BoxCollider box = projectile.GetComponentInChildren<BoxCollider>();
            Aoe aoe = projectile.GetComponentInChildren<Aoe>();
            if (aoe != null && aoe.m_useTriggers && box != null)
            {
                n.Range = box.size.z;
                n.Width = box.size.x;
            }
            else if (aoe != null)
                n.Radius = aoe.m_radius;
        }

        private static string F(float? v) => v.HasValue ? v.Value.ToString("0.##", CultureInfo.InvariantCulture) : "-";
    }
}
```

- [ ] **Step 5: Write `src/Forewarned/Patches/AnimTriggerPatch.cs`**

```csharp
using System;
using Forewarned.Core;
using Forewarned.Core.Model;
using HarmonyLib;
using UnityEngine;

namespace Forewarned.Patches
{
    /// <summary>
    /// PLAN.md §3, §9: the warning signal. SetTrigger is a routed RPC to everybody (decomp
    /// ZSyncAnimation.cs:148-151, 217-220), so this postfix runs on every client that has the boss loaded,
    /// and synchronously inside Attack.Start on the owner.
    /// </summary>
    [HarmonyPatch(typeof(ZSyncAnimation), nameof(ZSyncAnimation.RPC_SetTrigger))]
    internal static class AnimTriggerPatch
    {
        private static void Postfix(ZSyncAnimation __instance, string name)
        {
            try
            {
                if (!Runtime.Ready || Player.m_localPlayer == null)
                    return;
                Character c = __instance.GetComponent<Character>();
                TrackedBoss t = BossWatch.Find(c);
                if (t == null)
                    return;
                var e = new TriggerEvent { BossId = t.Id, Prefab = t.Prefab, Trigger = name, Time = Time.time };
                TriggerKind kind = Runtime.Engine.OnTrigger(e, BossWatch.SceneFor(c), BossWatch.FightActive());
                if (kind == TriggerKind.Unmapped && PluginConfig.LogUnmappedTriggers.Value)
                    UnmappedLog.Note(t.Prefab, name);
                if (Runtime.Engine.Recent.Count > 0 && Runtime.Engine.Recent[0].Trigger == name)
                    Runtime.Debug("Forewarned: " + t.Prefab + " " + name + " -> " + Runtime.Engine.Recent[0].Outcome);
            }
            catch (Exception e)
            {
                ForewarnedPlugin.WarnOnce("RPC_SetTrigger", e);
            }
        }
    }

    /// <summary>Logs each unknown (prefab, trigger) once.</summary>
    internal static class UnmappedLog
    {
        private static readonly System.Collections.Generic.HashSet<string> Seen = new System.Collections.Generic.HashSet<string>();

        public static void Note(string prefab, string trigger)
        {
            if (Seen.Add(prefab + " " + trigger))
                ForewarnedPlugin.Log.LogInfo("Forewarned unmapped trigger: " + prefab + " " + trigger);
        }
    }
}
```

- [ ] **Step 6: Write `src/Forewarned/Patches/AnimHitPatch.cs`**

```csharp
using System;
using Forewarned.Core;
using HarmonyLib;
using UnityEngine;

namespace Forewarned.Patches
{
    /// <summary>
    /// The damage moment: the attack clip's Hit / OnAttackTrigger animation event, which fires on every
    /// client's animator (decomp CharacterAnimEvent.cs:248-256). Ends the warning on time and teaches the
    /// tracker the real wind-up.
    /// </summary>
    [HarmonyPatch(typeof(CharacterAnimEvent))]
    internal static class AnimHitPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CharacterAnimEvent.Hit))]
        private static void HitPostfix(CharacterAnimEvent __instance) => OnHit(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(nameof(CharacterAnimEvent.OnAttackTrigger))]
        private static void TriggerPostfix(CharacterAnimEvent __instance) => OnHit(__instance);

        private static void OnHit(CharacterAnimEvent ev)
        {
            try
            {
                if (!Runtime.Ready || Player.m_localPlayer == null)
                    return;
                TrackedBoss t = BossWatch.Find(ev.m_character);
                if (t == null)
                    return;
                Runtime.Engine.OnHit(t.Id, Time.time);
                Runtime.Debug("Forewarned: " + t.Prefab + " hit at " + Time.time.ToString("0.00"));
            }
            catch (Exception e)
            {
                ForewarnedPlugin.WarnOnce("CharacterAnimEvent hit", e);
            }
        }
    }
}
```

- [ ] **Step 7: Tick from the plugin** — in `Plugin.cs` add `using Forewarned.Core;` and this method:

```csharp
        private void Update()
        {
            try
            {
                Runtime.Tick();
            }
            catch (Exception e)
            {
                WarnOnce("Runtime.Tick", e);
            }
        }
```

- [ ] **Step 8: Build and test**

```bash
dotnet build src/Forewarned/Forewarned.csproj -c Release --nologo -v minimal
dotnet test tests/Forewarned.Tests --nologo -v minimal
grep -rn "UnityEngine\|BepInEx\|HarmonyLib" src/Forewarned/Core/Model   # must print nothing
```
Expected: build 0 errors and 0 warnings; `Passed: 109`; grep empty. If the build reports a member name that differs from the decompiled code (e.g. `m_spawnPrefab` type), check `decomp/valheim/<Class>.cs` and match it; report any such change.

- [ ] **Step 9: Commit and push**

```bash
git add src
git commit -m "Game side: boss tracking, live numbers, trigger and hit capture"
git push
```

---

### Task 4: The `forewarned` console command

**Files:**
- Create: `src/Forewarned/Core/ForewarnedConsole.cs`
- Modify: `src/Forewarned/Plugin.cs` (register in `Awake`)

**Interfaces:**
- Consumes: `Runtime.Engine`, `BossWatch.All`, `BossWatch.FightActive()`, `Engine.Recent`, `Engine.LiveNumbers`, `Engine.Tracker.LearnedWindUp/SampleCount`, `Engine.Board.Active`, `Engine.Announcer.Lines`.
- Produces: console command `forewarned` (output also mirrored to the BepInEx log).

- [ ] **Step 1: Write `src/Forewarned/Core/ForewarnedConsole.cs`**

```csharp
using System;
using System.Globalization;
using Forewarned.Core.Model;
using UnityEngine;

namespace Forewarned.Core
{
    /// <summary>"forewarned": tracked bosses, what is showing, the last triggers and their verdicts, and each
    /// ability's live vs offline numbers with the learned wind-up. Mirrored to the BepInEx log.</summary>
    internal static class ForewarnedConsole
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand("forewarned", "Forewarned: tracked bosses, recent boss triggers and attack numbers",
                delegate (Terminal.ConsoleEventArgs args)
                {
                    try
                    {
                        Report(args.Context);
                    }
                    catch (Exception e)
                    {
                        ForewarnedPlugin.WarnOnce("forewarned console", e);
                    }
                });
        }

        private static void Say(Terminal ctx, string line)
        {
            if (ctx != null)
                ctx.AddString(line);
            ForewarnedPlugin.Log.LogInfo(line);
        }

        private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        private static string F(float? v) => v.HasValue ? F(v.Value) : "-";

        private static void Report(Terminal ctx)
        {
            Engine engine = Runtime.Engine;
            if (engine == null || Player.m_localPlayer == null)
            {
                Say(ctx, "Forewarned: load into a world first.");
                return;
            }
            float now = Time.time;
            Say(ctx, "Forewarned: enabled=" + PluginConfig.Enabled.Value + " fight=" + BossWatch.FightActive() + " t=" + F(now));

            foreach (TrackedBoss t in BossWatch.All)
            {
                float dist = t.Character != null ? Vector3.Distance(t.Character.transform.position, Player.m_localPlayer.transform.position) : -1f;
                Say(ctx, "  boss " + t.Prefab + " #" + t.Id + " hp " + F(t.Health * 100f) + "% alerted " + t.Alerted +
                    " flying " + t.Flying + " " + F(dist) + "m");
                BossModule m = engine.Registry.ModuleFor(t.Prefab);
                foreach (AbilitySpec a in m.Abilities)
                {
                    AbilityNumbers live = engine.LiveNumbers(t.Prefab, a.Id);
                    string learned = "";
                    foreach (string trigger in a.Triggers)
                    {
                        float? w = engine.Tracker.LearnedWindUp(t.Prefab, trigger);
                        if (w.HasValue)
                            learned += " " + trigger + " " + F(w) + "s x" + engine.Tracker.SampleCount(t.Prefab, trigger);
                    }
                    Say(ctx, "    " + a.Id + ": wind-up " + F(a.WindUp) + "s" + (learned.Length > 0 ? " learned" + learned : "") +
                        " | cd " + F(a.Cooldown) + "/" + F(live?.Cooldown) +
                        " range " + F(a.AiRange) + "/" + F(live?.AiRange) +
                        " hp " + F(a.HpMin) + "-" + F(a.HpMax) + "/" + F(live?.HpMin) + "-" + F(live?.HpMax) +
                        " size r " + F(a.Shape.Radius) + "/" + F(live?.Radius) + " len " + F(a.Shape.Range) + "/" + F(live?.Range) +
                        " angle " + F(a.Shape.Angle) + "/" + F(live?.Angle));
                }
            }

            foreach (Warning w in engine.Board.Active)
                Say(ctx, "  showing " + w.Level + " " + w.Title + " / " + w.Action + " hit in " + F(WarningBoard.Remaining(w, now)) + "s");
            foreach (Announce a in engine.Announcer.Lines)
                Say(ctx, "  announce " + a.Text);

            Say(ctx, "  last triggers (newest first):");
            foreach (TriggerRecord r in engine.Recent)
                Say(ctx, "    " + F(r.Time) + "s " + r.Prefab + " " + r.Trigger + " -> " + r.Outcome);
        }
    }
}
```

- [ ] **Step 2: Register** — in `Plugin.cs` `Awake`, after `PluginConfig.Bind(...)`:

```csharp
            ForewarnedConsole.Register();
```

- [ ] **Step 3: Build and test**

```bash
dotnet build src/Forewarned/Forewarned.csproj -c Release --nologo -v minimal
dotnet test tests/Forewarned.Tests --nologo -v minimal
```
Expected: 0 errors, 0 warnings; `Passed: 109`.

- [ ] **Step 4: Deploy** (approved dev loop; a running game picks it up on relaunch)

```bash
./build/deploy.sh
```

- [ ] **Step 5: Commit and push**

```bash
git add src
git commit -m "Console: the forewarned command"
git push
```

- [ ] **Step 6: Note progress in PLAN.md** — append to §9 after the plan 1 progress bullet:

```markdown
- **Progress:** milestone 1 plan 2 (game-side capture, live data, config, console, Eikthyr module) is done and deployed; in-game checks happen in the first Eikthyr session after plan 3.
```

Commit `PLAN: milestone 1 plan 2 done` and push.
