# Forewarned M1 · Plan 1: Scaffold and Pure Model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A building BepInEx plugin skeleton plus the complete, unit-tested pure model (geometry,
area tests, relevance, tracker, warning board, announcer, phases, registry, engine) with the
Fader and Moder modules and their English strings.

**Architecture:** Everything under `src/Forewarned/Core/Model/` is plain C# with no UnityEngine
or game types (PLAN.md §11.2). It is compiled into the net472 plugin and, by file link, into a
net8.0 xUnit test project. Game adapters and the HUD (plans 2 and 3) will only feed the `Engine`
and draw its `WarningBoard` and `Announcer`.

**Tech Stack:** C# (LangVersion latest, net472 and net8.0), BepInEx 5, HarmonyLib,
BepInEx.AssemblyPublicizer.MSBuild, xUnit 2.9.

**Spec:** `PLAN.md` §9 (decisions), §10 (ability table), §11 (design). Read §11.2 before starting.

## Global Constraints

- Repo root: `/workspace/gamemods/Valheim/Forewarned`. Run every command from there.
- Every dotnet command needs `export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 PATH="$HOME/.dotnet:$PATH"`.
- Files under `src/Forewarned/Core/Model/` must not reference `UnityEngine`, `BepInEx`, `HarmonyLib` or any game type. They must compile on net472: no `Math.Clamp`, no `MathF`, no `init` accessors, no records.
- Namespace root `Forewarned`; model namespace `Forewarned.Core.Model`. GUID `com.jumpingmushroom.forewarned`. Plugin version `0.0.1` until the 0.1.0 release.
- Commit after every task **and push** (`git push`). Never add AI attribution (no `Co-Authored-By`, no "Generated with") to commits.
- Never write the rig's address, user or paths into tracked files; write `<rig>`. The rig scripts (`build/deploy.sh`, `logs.sh`, `shot.sh`, `crop.sh`) are gitignored.
- Timing constants (PLAN.md §9): warning lives until hit + 0.5 s, minimum 1.2 s, fade 0.3 s; two slots; sound gap 1 s. Announces: three lines, 4 s each. Area margin 2 m.

---

## File map

| File | Responsibility |
|---|---|
| `Directory.Build.props`, `Forewarned.sln` | Shared build settings, solution |
| `src/Forewarned/Forewarned.csproj` | Plugin project (net472) |
| `src/Forewarned/Plugin.cs` | BepInEx entry point, `WarnOnce` |
| `src/Forewarned/ConfigurationManagerAttributes.cs` | F1 attribute shim (used in plan 2) |
| `src/Forewarned/translations/English.txt` | Embedded English strings |
| `src/Forewarned/Core/Model/Vec2.cs` | 2-D ground-plane vector |
| `src/Forewarned/Core/Model/Geometry.cs` | Angles, clamps |
| `src/Forewarned/Core/Model/Shape.cs` | Attack area shapes |
| `src/Forewarned/Core/Model/AreaTest.cs` | Inside / Near / Outside |
| `src/Forewarned/Core/Model/SafeDirection.cs` | Arrow direction per response |
| `src/Forewarned/Core/Model/AbilitySpec.cs` | `Level`, `Response`, `AbilitySpec`, `AbilityNumbers`, `ResolvedAbility` |
| `src/Forewarned/Core/Model/BossModule.cs` | `Phase`, `BossModule` base |
| `src/Forewarned/Core/Model/ModuleRegistry.cs` | Prefab and trigger lookup |
| `src/Forewarned/Core/Model/Tracker.cs` | Triggers, hits, learned wind-ups, health |
| `src/Forewarned/Core/Model/Relevance.cs` | `Scene`, aim point, verdict → outcome |
| `src/Forewarned/Core/Model/WarningBoard.cs` | Special-warning slots and timing |
| `src/Forewarned/Core/Model/Announcer.cs` | Announce lines |
| `src/Forewarned/Core/Model/PhaseTracker.cs` | Health-threshold crossings |
| `src/Forewarned/Core/Model/Settings.cs` | `IAbilitySettings`, `DefaultSettings` |
| `src/Forewarned/Core/Model/Translations.cs`, `TextCheck.cs` | Strings (from Earshot) |
| `src/Forewarned/Core/Model/Engine.cs` | The façade the game side calls |
| `src/Forewarned/Core/Model/Bosses/FaderModule.cs`, `ModerModule.cs`, `BossList.cs` | Boss modules |
| `tests/Forewarned.Tests/*` | xUnit tests, one file per model file |
| `build/package.sh`, `build/publish.sh` | Tracked release scripts |
| `build/deploy.sh`, `logs.sh`, `shot.sh`, `crop.sh` | Local rig scripts (gitignored) |
| `LICENSE`, `CHANGELOG.md`, `README.md`, `thunderstore/manifest.json` | Repo metadata |

---

### Task 1: Scaffold the solution, plugin and test project

**Files:**
- Create: `Directory.Build.props`, `Forewarned.sln`, `src/Forewarned/Forewarned.csproj`, `src/Forewarned/Plugin.cs`, `src/Forewarned/ConfigurationManagerAttributes.cs`, `src/Forewarned/translations/English.txt`, `src/Forewarned/Core/Model/TextCheck.cs`, `tests/Forewarned.Tests/Forewarned.Tests.csproj`, `tests/Forewarned.Tests/TextCheckTests.cs`, `LICENSE`, `CHANGELOG.md`, `README.md`, `thunderstore/manifest.json`, `build/package.sh`, `build/publish.sh`, `build/make_icon.py`, local `build/deploy.sh`, `build/logs.sh`, `build/shot.sh`, `build/crop.sh`

**Interfaces:**
- Produces: `ForewarnedPlugin.Log` (`ManualLogSource`), `ForewarnedPlugin.WarnOnce(string key, Exception e)`, `TextCheck.IsClean(string)`; a test project that compiles every `src/Forewarned/Core/Model/**/*.cs` and embeds `English.txt` as resource `Forewarned.English.txt`.

- [ ] **Step 1: Copy the build settings from Earshot**

```bash
cp ../Earshot/Directory.Build.props .
```

- [ ] **Step 2: Write `src/Forewarned/Forewarned.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <AssemblyName>Forewarned</AssemblyName>
    <RootNamespace>Forewarned</RootNamespace>
    <Version>0.0.1</Version>
    <Description>Boss attack warnings for Valheim, in the style of Deadly Boss Mods.</Description>
  </PropertyGroup>

  <ItemGroup>
    <!-- .NET Framework 4.7.2 reference assemblies; required to build net472 off-Windows. -->
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
    <PackageReference Include="BepInEx.AssemblyPublicizer.MSBuild" Version="0.4.3" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <!-- Publicized at build time only; the shipped DLL binds to the real members
         and runs against an unmodified game install. -->
    <Reference Include="assembly_valheim"  HintPath="$(ValheimManaged)/assembly_valheim.dll"  Publicize="true" Private="false" />
    <Reference Include="assembly_utils"    HintPath="$(ValheimManaged)/assembly_utils.dll"    Private="false" />
    <Reference Include="assembly_guiutils" HintPath="$(ValheimManaged)/assembly_guiutils.dll" Private="false" />

    <Reference Include="UnityEngine"                     HintPath="$(ValheimManaged)/UnityEngine.dll" Private="false" />
    <Reference Include="UnityEngine.CoreModule"          HintPath="$(ValheimManaged)/UnityEngine.CoreModule.dll" Private="false" />
    <Reference Include="UnityEngine.AudioModule"         HintPath="$(ValheimManaged)/UnityEngine.AudioModule.dll" Private="false" />
    <Reference Include="UnityEngine.PhysicsModule"       HintPath="$(ValheimManaged)/UnityEngine.PhysicsModule.dll" Private="false" />
    <Reference Include="UnityEngine.UI"                  HintPath="$(ValheimManaged)/UnityEngine.UI.dll" Private="false" />
    <Reference Include="UnityEngine.UIModule"            HintPath="$(ValheimManaged)/UnityEngine.UIModule.dll" Private="false" />
    <Reference Include="UnityEngine.TextRenderingModule" HintPath="$(ValheimManaged)/UnityEngine.TextRenderingModule.dll" Private="false" />
    <Reference Include="UnityEngine.IMGUIModule"         HintPath="$(ValheimManaged)/UnityEngine.IMGUIModule.dll" Private="false" />
    <Reference Include="UnityEngine.InputLegacyModule"   HintPath="$(ValheimManaged)/UnityEngine.InputLegacyModule.dll" Private="false" />
    <Reference Include="Unity.TextMeshPro"               HintPath="$(ValheimManaged)/Unity.TextMeshPro.dll" Private="false" />

    <Reference Include="BepInEx"  HintPath="$(BepInExPath)/BepInEx.dll" Private="false" />
    <Reference Include="0Harmony" HintPath="$(BepInExPath)/0Harmony.dll" Private="false" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="translations/English.txt" LogicalName="Forewarned.English.txt" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Write `src/Forewarned/Plugin.cs`**

```csharp
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Forewarned
{
    /// <summary>
    /// Boss attack warnings in the style of Deadly Boss Mods. Reads the boss's animator triggers as
    /// the game sends them and draws warnings; never changes game state. Purely client-side.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("valheim.exe")]
    [BepInProcess("valheim.x86_64")]
    public sealed class ForewarnedPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.jumpingmushroom.forewarned";
        public const string PluginName = "Forewarned";
        public const string PluginVersion = "0.0.1";

        internal static ManualLogSource Log;

        private static readonly HashSet<string> Warned = new HashSet<string>();
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(ForewarnedPlugin).Assembly);
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
                _harmony.UnpatchSelf();
        }

        /// <summary>Log an exception once per key, so a broken hook can't flood the log every frame.</summary>
        internal static void WarnOnce(string key, Exception e)
        {
            if (Warned.Add(key))
                Log.LogWarning(key + ": " + e);
        }
    }
}
```

- [ ] **Step 4: Write `src/Forewarned/ConfigurationManagerAttributes.cs`**

```csharp
using System;
using BepInEx.Configuration;

// Read by ConfigurationManager through reflection, matched by type name only. Declaring it
// here means no dependency on any particular ConfigurationManager build, and nothing breaks
// if no config manager is installed.
#pragma warning disable 0649
internal sealed class ConfigurationManagerAttributes
{
    public int? Order;
    public bool? IsAdvanced;
    public bool? Browsable;
    public bool? ReadOnly;
    public bool? HideDefaultButton;
    public string DispName;
    public string Category;
    public Action<ConfigEntryBase> CustomDrawer;
}
```

- [ ] **Step 5: Write `src/Forewarned/Core/Model/TextCheck.cs`** (from Earshot)

```csharp
namespace Forewarned.Core.Model
{
    public static class TextCheck
    {
        /// <summary>
        /// A localised string is shown only if it is non-blank and carries no unresolved-token marks.
        /// Valheim renders a missing key as "[key]", so '[' or ']' means a broken token, and '$'
        /// means one that was never localised.
        /// </summary>
        public static bool IsClean(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            return text.IndexOf('[') < 0 && text.IndexOf(']') < 0 && text.IndexOf('$') < 0;
        }
    }
}
```

- [ ] **Step 6: Write a placeholder `src/Forewarned/translations/English.txt`** (Task 11 fills it)

```
# Forewarned strings: "key = text", '#' comments. {boss} is the boss's localised name,
# {what} an ability name, {m} a distance in metres.
```

- [ ] **Step 7: Write `tests/Forewarned.Tests/Forewarned.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Overrides Directory.Build.props (net472): the model is plain C#, so it is tested on the
       net8 runtime the build box has. Mono is not installed, so the plugin itself can't run here. -->
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>disable</Nullable>
    <!-- The invariant-globalization SDK flags the test host's localized resources; harmless. -->
    <NoWarn>$(NoWarn);NETSDK1188</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="../../src/Forewarned/Core/Model/**/*.cs" Link="Model/%(RecursiveDir)%(Filename)%(Extension)" />
    <EmbeddedResource Include="../../src/Forewarned/translations/English.txt" LogicalName="Forewarned.English.txt" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

- [ ] **Step 8: Write `tests/Forewarned.Tests/TextCheckTests.cs`**

```csharp
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class TextCheckTests
    {
        [Theory]
        [InlineData("Get behind Fader", true)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData(null, false)]
        [InlineData("[enemy_fader]", false)]
        [InlineData("$enemy_fader", false)]
        public void IsClean(string text, bool expected)
        {
            Assert.Equal(expected, TextCheck.IsClean(text));
        }
    }
}
```

- [ ] **Step 9: Create the solution**

```bash
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 PATH="$HOME/.dotnet:$PATH"
dotnet new sln -n Forewarned
dotnet sln Forewarned.sln add --solution-folder src src/Forewarned/Forewarned.csproj
dotnet sln Forewarned.sln add --solution-folder tests tests/Forewarned.Tests/Forewarned.Tests.csproj
```

- [ ] **Step 10: Build the plugin and run the tests**

```bash
dotnet build src/Forewarned/Forewarned.csproj -c Release --nologo -v minimal
dotnet test tests/Forewarned.Tests --nologo -v minimal
```
Expected: build succeeds with `src/Forewarned/bin/Release/net472/Forewarned.dll`; tests report `Passed: 6`.

- [ ] **Step 11: Repo metadata**

`LICENSE`: copy Earshot's MIT licence (`cp ../Earshot/LICENSE .`; it already names Johnny Dalen, 2026).

`CHANGELOG.md`:
```markdown
# Changelog

## Unreleased

- Boss attack warnings for Fader and Moder (work in progress).
```

`README.md`:
```markdown
# Forewarned

Boss attack warnings for Valheim, in the style of World of Warcraft's Deadly Boss Mods. When a
boss starts winding up an attack you need to move for, Forewarned tells you what's coming and what
to do: **FLAME BREATH** · *Get behind Fader*.

Work in progress; nothing is released yet. Design and research: [PLAN.md](PLAN.md).
```

`thunderstore/manifest.json`:
```json
{
  "name": "Forewarned",
  "version_number": "0.0.1",
  "website_url": "https://github.com/jumpingmushroom/Forewarned",
  "dependencies": [
    "denikson-BepInExPack_Valheim-5.4.2350"
  ],
  "description": "Boss attack warnings in the style of Deadly Boss Mods: what's coming, what to do, a countdown to the hit and the danger drawn on the ground. Client-side."
}
```

- [ ] **Step 12: Build scripts**

Tracked, renamed from Earshot (the rig address is not in these two):
```bash
mkdir -p build
for f in package.sh publish.sh make_icon.py; do sed 's/Earshot/Forewarned/g; s/earshot/forewarned/g' ../Earshot/build/$f > build/$f; done
chmod +x build/package.sh build/publish.sh
```
Local and gitignored (they carry the rig's address from Earshot's local copies):
```bash
for f in deploy.sh logs.sh shot.sh crop.sh; do sed 's/Earshot/Forewarned/g; s/earshot/forewarned/g' ../Earshot/build/$f > build/$f; done
chmod +x build/deploy.sh build/logs.sh build/shot.sh build/crop.sh
git status --short build/   # must list only package.sh, publish.sh, make_icon.py
grep -n "192\.\|equ@\|/home/" build/package.sh build/publish.sh build/make_icon.py   # must print nothing
```

- [ ] **Step 13: Deploy once to the rig to prove the plugin loads**

```bash
./build/deploy.sh
```
Expected: `==> done: Forewarned.dll ... bytes`. On the next game launch the BepInEx log shows `Forewarned 0.0.1 loaded.` (`./build/logs.sh`). If the game isn't launched during this task, note it and move on; plan 2 checks the log.

- [ ] **Step 14: Commit and push**

```bash
git add Directory.Build.props Forewarned.sln src tests LICENSE CHANGELOG.md README.md thunderstore build/package.sh build/publish.sh build/make_icon.py
git commit -m "Scaffold: plugin, test project, build scripts"
git push
```

---

### Task 2: Ground-plane geometry and area tests

**Files:**
- Create: `src/Forewarned/Core/Model/Vec2.cs`, `Geometry.cs`, `Shape.cs`, `AreaTest.cs`
- Test: `tests/Forewarned.Tests/AreaTestTests.cs`

**Interfaces:**
- Produces:
  - `struct Vec2(float x, float z)` with `X`, `Z`, `+`, `-`, `* float`, `Length`, `Normalized`, `Left`, `static Dot`, `static Cross`, `static Distance`, `static Zero`.
  - `static class Geometry`: `float AngleDeg(Vec2 a, Vec2 b)`, `float Clamp(float v, float lo, float hi)`, `float Clamp01(float v)`.
  - `enum ShapeKind { None, Cone, Circle, Line, Ring }`, `enum Anchor { Boss, Target }`, `enum ShapeSource { Fixed, AttackCone, AttackSphere, SpawnAbility, Aoe }`.
  - `sealed class Shape` with fields `Kind, Anchor, Source, Range, Angle, Width, Radius, Offset`, factories `Shape.Cone(range, angle, src)`, `Shape.Circle(radius, offset, anchor, src)`, `Shape.Line(length, width, src)`, `Shape.Ring(radius, width, src)`, `Shape.None`, `Copy()`, `int SafeMetres`.
  - `enum Verdict { Outside, Near, Inside }`; `static class AreaTest`: `const float Margin = 2f`, `Verdict Classify(Shape s, Vec2 origin, Vec2 facing, Vec2 p, float margin = Margin)`.

- [ ] **Step 1: Write the failing tests** — `tests/Forewarned.Tests/AreaTestTests.cs`

```csharp
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class AreaTestTests
    {
        private static readonly Vec2 Boss = new Vec2(0f, 0f);
        private static readonly Vec2 North = new Vec2(0f, 1f);

        private static Verdict At(Shape s, float x, float z) => AreaTest.Classify(s, Boss, North, new Vec2(x, z));

        [Fact]
        public void Vec2Basics()
        {
            Assert.Equal(-1f, North.Left.X, 3);
            Assert.Equal(0f, North.Left.Z, 3);
            Assert.True(Vec2.Cross(North, North.Left) > 0f);
            Assert.Equal(5f, new Vec2(3f, 4f).Length, 3);
            Assert.Equal(0.6f, new Vec2(3f, 4f).Normalized.X, 3);
            Assert.Equal(0f, Vec2.Zero.Normalized.Length, 3);
            Assert.Equal(90f, Geometry.AngleDeg(North, new Vec2(1f, 0f)), 1);
            Assert.Equal(180f, Geometry.AngleDeg(North, new Vec2(0f, -1f)), 1);
        }

        [Fact]
        public void CircleAroundTheBoss()
        {
            Shape spin = Shape.Circle(8.5f, 0f, Anchor.Boss, ShapeSource.AttackSphere);
            Assert.Equal(Verdict.Inside, At(spin, 0f, 8f));
            Assert.Equal(Verdict.Near, At(spin, 0f, 9.5f));
            Assert.Equal(Verdict.Outside, At(spin, 0f, 11f));
        }

        [Fact]
        public void CircleAheadOfTheBoss()
        {
            Shape stomp = Shape.Circle(10f, 3f, Anchor.Boss, ShapeSource.AttackSphere);
            Assert.Equal(Verdict.Inside, At(stomp, 0f, 12.9f));   // 9.9 m from the centre at (0,3)
            Assert.Equal(Verdict.Near, At(stomp, 0f, -7.5f));     // 10.5 m
            Assert.Equal(Verdict.Outside, At(stomp, 0f, -12.5f)); // 15.5 m
        }

        [Fact]
        public void Cone()
        {
            Shape bite = Shape.Cone(10f, 40f, ShapeSource.AttackCone);
            Assert.Equal(Verdict.Inside, At(bite, 0f, 9f));
            Assert.Equal(Verdict.Inside, At(bite, 0f, 0.3f));     // under the boss
            Assert.Equal(Verdict.Near, At(bite, 3f, 5f));         // 11° outside the edge, 1.1 m from it
            Assert.Equal(Verdict.Near, At(bite, 0f, 11.5f));      // just beyond the range
            Assert.Equal(Verdict.Outside, At(bite, 8f, 1f));
            Assert.Equal(Verdict.Outside, At(bite, 0f, -3f));     // behind
        }

        [Fact]
        public void Line()
        {
            Shape breath = Shape.Line(39.45f, 3f, ShapeSource.Aoe);
            Assert.Equal(Verdict.Inside, At(breath, 1f, 20f));
            Assert.Equal(Verdict.Near, At(breath, 2.5f, 20f));
            Assert.Equal(Verdict.Outside, At(breath, 4f, 20f));
            Assert.Equal(Verdict.Near, At(breath, 0f, -1f));
            Assert.Equal(Verdict.Outside, At(breath, 0f, -3f));
        }

        [Fact]
        public void RingCountsItsWholeDisc()
        {
            Shape wall = Shape.Ring(8f, 4f, ShapeSource.SpawnAbility);
            Assert.Equal(Anchor.Target, wall.Anchor);
            Assert.Equal(Verdict.Inside, At(wall, 1f, 1f));
            Assert.Equal(Verdict.Near, At(wall, 11f, 0f));
            Assert.Equal(Verdict.Outside, At(wall, 13f, 0f));
        }

        [Fact]
        public void NoShapeIsAlwaysOutside()
        {
            Assert.Equal(Verdict.Outside, At(Shape.None, 0f, 1f));
        }

        [Fact]
        public void SafeMetresRoundsUp()
        {
            Assert.Equal(9, Shape.Circle(8.5f, 0f, Anchor.Boss, ShapeSource.Fixed).SafeMetres);
            Assert.Equal(10, Shape.Ring(8f, 4f, ShapeSource.Fixed).SafeMetres);
            Assert.Equal(10, Shape.Cone(10f, 40f, ShapeSource.Fixed).SafeMetres);
            Assert.Equal(0, Shape.None.SafeMetres);
        }

        [Fact]
        public void CopyIsIndependent()
        {
            Shape a = Shape.Circle(5f, 0f, Anchor.Boss, ShapeSource.Fixed);
            Shape b = a.Copy();
            b.Radius = 7f;
            Assert.Equal(5f, a.Radius);
        }
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: build error, `The type or namespace name 'Vec2' could not be found`.

- [ ] **Step 3: Write `Vec2.cs`**

```csharp
using System;
using System.Globalization;

namespace Forewarned.Core.Model
{
    /// <summary>A point or direction on the ground plane (Unity x and z). The model works in 2-D:
    /// height doesn't change who a boss attack reaches.</summary>
    public struct Vec2
    {
        public readonly float X;
        public readonly float Z;

        public Vec2(float x, float z)
        {
            X = x;
            Z = z;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Z + b.Z);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Z - b.Z);
        public static Vec2 operator *(Vec2 a, float k) => new Vec2(a.X * k, a.Z * k);

        public float Length => (float)Math.Sqrt(X * X + Z * Z);

        /// <summary>Unit vector, or Zero for a (near) zero vector.</summary>
        public Vec2 Normalized
        {
            get
            {
                float l = Length;
                return l < 1e-5f ? Zero : new Vec2(X / l, Z / l);
            }
        }

        /// <summary>This direction turned 90° to the left, seen from above.</summary>
        public Vec2 Left => new Vec2(-Z, X);

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Z * b.Z;

        /// <summary>Positive when b points to the left of a. With a unit a, |Cross| is b's sideways distance from a's line.</summary>
        public static float Cross(Vec2 a, Vec2 b) => a.X * b.Z - a.Z * b.X;

        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;

        public override string ToString() =>
            X.ToString("0.0", CultureInfo.InvariantCulture) + "," + Z.ToString("0.0", CultureInfo.InvariantCulture);
    }
}
```

- [ ] **Step 4: Write `Geometry.cs`**

```csharp
using System;

namespace Forewarned.Core.Model
{
    public static class Geometry
    {
        /// <summary>Unsigned angle between two directions, 0 to 180 degrees. 0 if either is zero.</summary>
        public static float AngleDeg(Vec2 a, Vec2 b)
        {
            Vec2 na = a.Normalized, nb = b.Normalized;
            if (na.Length == 0f || nb.Length == 0f)
                return 0f;
            double dot = Clamp(Vec2.Dot(na, nb), -1f, 1f);
            return (float)(Math.Acos(dot) * 180.0 / Math.PI);
        }

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
    }
}
```

- [ ] **Step 5: Write `Shape.cs`**

```csharp
using System;

namespace Forewarned.Core.Model
{
    public enum ShapeKind { None, Cone, Circle, Line, Ring }

    /// <summary>Boss: the shape starts at the boss and turns with it. Target: it lands where the boss aims.</summary>
    public enum Anchor { Boss, Target }

    /// <summary>Where LiveData reads the shape's size from in game (PLAN.md §11.3). Fixed: the spec's own numbers.</summary>
    public enum ShapeSource { Fixed, AttackCone, AttackSphere, SpawnAbility, Aoe }

    /// <summary>
    /// An attack's danger area. Cone: Range and Angle (full width, degrees). Circle: Radius, centred
    /// Offset metres ahead of the anchor. Line: a strip Range long and Width wide ahead of the anchor.
    /// Ring: a band of Radius ± Width/2 around the target; everything inside it counts as danger,
    /// since the target is trapped in it.
    /// </summary>
    public sealed class Shape
    {
        public ShapeKind Kind;
        public Anchor Anchor;
        public ShapeSource Source;
        public float Range;
        public float Angle;
        public float Width;
        public float Radius;
        public float Offset;

        public static Shape None => new Shape { Kind = ShapeKind.None };

        public static Shape Cone(float range, float angle, ShapeSource source = ShapeSource.AttackCone) =>
            new Shape { Kind = ShapeKind.Cone, Anchor = Anchor.Boss, Source = source, Range = range, Angle = angle };

        public static Shape Circle(float radius, float offset = 0f, Anchor anchor = Anchor.Boss, ShapeSource source = ShapeSource.Fixed) =>
            new Shape { Kind = ShapeKind.Circle, Anchor = anchor, Source = source, Radius = radius, Offset = offset };

        public static Shape Line(float length, float width, ShapeSource source = ShapeSource.Fixed) =>
            new Shape { Kind = ShapeKind.Line, Anchor = Anchor.Boss, Source = source, Range = length, Width = width };

        public static Shape Ring(float radius, float width, ShapeSource source = ShapeSource.Fixed) =>
            new Shape { Kind = ShapeKind.Ring, Anchor = Anchor.Target, Source = source, Radius = radius, Width = width };

        public Shape Copy() => (Shape)MemberwiseClone();

        /// <summary>How far from the shape's centre (the anchor, for cones and lines) safety starts, rounded
        /// up: the {m} in "Run out, {m} m".</summary>
        public int SafeMetres
        {
            get
            {
                switch (Kind)
                {
                    case ShapeKind.Circle: return (int)Math.Ceiling(Radius - 1e-4);
                    case ShapeKind.Ring: return (int)Math.Ceiling(Radius + Width * 0.5f - 1e-4);
                    case ShapeKind.Cone:
                    case ShapeKind.Line: return (int)Math.Ceiling(Range - 1e-4);
                    default: return 0;
                }
            }
        }
    }
}
```

- [ ] **Step 6: Write `AreaTest.cs`**

```csharp
using System;

namespace Forewarned.Core.Model
{
    public enum Verdict { Outside, Near, Inside }

    /// <summary>PLAN.md §11.2: is a point inside an attack's area, near it (within the margin), or clear?</summary>
    public static class AreaTest
    {
        /// <summary>Metres around a shape that still count as "near": a step or a turn of the boss away.</summary>
        public const float Margin = 2f;

        /// <param name="origin">The boss, for Boss-anchored shapes; where the attack lands, for Target-anchored ones.</param>
        /// <param name="facing">The boss's facing, a unit vector.</param>
        public static Verdict Classify(Shape s, Vec2 origin, Vec2 facing, Vec2 p, float margin = Margin)
        {
            switch (s.Kind)
            {
                case ShapeKind.Circle: return Disc(origin + facing * s.Offset, s.Radius, p, margin);
                case ShapeKind.Ring: return Disc(origin, s.Radius + s.Width * 0.5f, p, margin);
                case ShapeKind.Line: return Strip(origin, facing, s.Range, s.Width, p, margin);
                case ShapeKind.Cone: return Cone(origin, facing, s.Range, s.Angle, p, margin);
                default: return Verdict.Outside;
            }
        }

        private static Verdict Disc(Vec2 centre, float radius, Vec2 p, float margin)
        {
            float d = Vec2.Distance(centre, p);
            return d <= radius ? Verdict.Inside : d <= radius + margin ? Verdict.Near : Verdict.Outside;
        }

        private static Verdict Strip(Vec2 origin, Vec2 facing, float length, float width, Vec2 p, float margin)
        {
            Vec2 rel = p - origin;
            float along = Vec2.Dot(rel, facing);
            float side = Math.Abs(Vec2.Cross(facing, rel));
            float half = width * 0.5f;
            if (along >= 0f && along <= length && side <= half)
                return Verdict.Inside;
            if (along >= -margin && along <= length + margin && side <= half + margin)
                return Verdict.Near;
            return Verdict.Outside;
        }

        private static Verdict Cone(Vec2 origin, Vec2 facing, float range, float angle, Vec2 p, float margin)
        {
            Vec2 rel = p - origin;
            float d = rel.Length;
            if (d <= 0.5f)
                return Verdict.Inside; // under the boss: every sweep passes through here
            float half = angle * 0.5f;
            float off = Geometry.AngleDeg(facing, rel);
            if (d <= range && off <= half)
                return Verdict.Inside;
            if (d > range + margin)
                return Verdict.Outside;
            if (off <= half)
                return Verdict.Near; // just past the tip
            float beyond = off - half;
            if (beyond >= 90f)
                return d <= margin ? Verdict.Near : Verdict.Outside;
            float sideways = d * (float)Math.Sin(beyond * Math.PI / 180.0);
            return sideways <= margin ? Verdict.Near : Verdict.Outside;
        }
    }
}
```

- [ ] **Step 7: Run the tests**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: `Passed: 15` (6 + 9), 0 failed.

- [ ] **Step 8: Commit and push**

```bash
git add src/Forewarned/Core/Model tests/Forewarned.Tests
git commit -m "Model: ground-plane geometry, shapes and area tests"
git push
```

---

### Task 3: Safe direction for the path arrow

**Files:**
- Create: `src/Forewarned/Core/Model/SafeDirection.cs`, and `Response` in a new `src/Forewarned/Core/Model/Response.cs`
- Test: `tests/Forewarned.Tests/SafeDirectionTests.cs`

**Interfaces:**
- Consumes: `Vec2`, `Shape`, `ShapeKind` (Task 2).
- Produces: `enum Response { None, GetBehind, LeaveArea, LeaveLine, KeepMoving, ExitRing, BreakLos, Parry, KillAdds, Find }`; `static class SafeDirection`: `const float BehindDistance = 4f`, `Vec2 Toward(Response r, Shape s, Vec2 bossPos, Vec2 facing, Vec2 origin, Vec2 me)` returning a unit vector, or `Vec2.Zero` for "no arrow".

- [ ] **Step 1: Write the failing tests** — `tests/Forewarned.Tests/SafeDirectionTests.cs`

```csharp
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class SafeDirectionTests
    {
        private static readonly Vec2 Boss = new Vec2(0f, 0f);
        private static readonly Vec2 North = new Vec2(0f, 1f);

        private static void Near(Vec2 expected, Vec2 actual)
        {
            Assert.Equal(expected.X, actual.X, 3);
            Assert.Equal(expected.Z, actual.Z, 3);
        }

        [Fact]
        public void GetBehindHeadsForThePointBehindTheBoss()
        {
            Vec2 me = new Vec2(0f, 10f);
            Near(new Vec2(0f, -1f), SafeDirection.Toward(Response.GetBehind, Shape.Line(39f, 3f), Boss, North, Boss, me));
        }

        [Fact]
        public void LeaveAreaPointsAwayFromTheCentre()
        {
            Shape spin = Shape.Circle(8.5f);
            Near(new Vec2(0.6f, 0.8f), SafeDirection.Toward(Response.LeaveArea, spin, Boss, North, Boss, new Vec2(3f, 4f)));
        }

        [Fact]
        public void LeaveAreaFromTheVeryCentreBacksAwayFromTheBoss()
        {
            Near(new Vec2(0f, -1f), SafeDirection.Toward(Response.LeaveArea, Shape.Circle(8.5f), Boss, North, Boss, Boss));
        }

        [Fact]
        public void LeaveLineSteppsToTheNearerSide()
        {
            Shape breath = Shape.Cone(30f, 10f);
            Near(new Vec2(1f, 0f), SafeDirection.Toward(Response.LeaveLine, breath, Boss, North, Boss, new Vec2(1f, 10f)));
            Near(new Vec2(-1f, 0f), SafeDirection.Toward(Response.LeaveLine, breath, Boss, North, Boss, new Vec2(-1f, 10f)));
            Near(new Vec2(-1f, 0f), SafeDirection.Toward(Response.LeaveLine, breath, Boss, North, Boss, new Vec2(0f, 10f)));
        }

        [Fact]
        public void KeepMovingStrafesAroundTheBoss()
        {
            Near(new Vec2(-1f, 0f), SafeDirection.Toward(Response.KeepMoving, Shape.Circle(15f, 0f, Anchor.Target), Boss, North, new Vec2(0f, 10f), new Vec2(0f, 10f)));
        }

        [Fact]
        public void ExitRingLeavesAwayFromTheBoss()
        {
            Near(new Vec2(0f, 1f), SafeDirection.Toward(Response.ExitRing, Shape.Ring(8f, 4f), Boss, North, new Vec2(0f, 10f), new Vec2(0f, 10f)));
        }

        [Theory]
        [InlineData(Response.None)]
        [InlineData(Response.BreakLos)]
        [InlineData(Response.Parry)]
        [InlineData(Response.KillAdds)]
        [InlineData(Response.Find)]
        public void NoArrow(Response r)
        {
            Assert.Equal(0f, SafeDirection.Toward(r, Shape.Circle(5f), Boss, North, Boss, new Vec2(1f, 1f)).Length);
        }
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: build error, `'SafeDirection' could not be found`.

- [ ] **Step 3: Write `Response.cs`**

```csharp
namespace Forewarned.Core.Model
{
    /// <summary>What the player should do about an attack. Drives the path arrow (SafeDirection).</summary>
    public enum Response { None, GetBehind, LeaveArea, LeaveLine, KeepMoving, ExitRing, BreakLos, Parry, KillAdds, Find }
}
```

- [ ] **Step 4: Write `SafeDirection.cs`**

```csharp
namespace Forewarned.Core.Model
{
    /// <summary>PLAN.md §11.2: which way the path arrow points for each response.</summary>
    public static class SafeDirection
    {
        /// <summary>How far behind the boss "get behind" aims.</summary>
        public const float BehindDistance = 4f;

        /// <param name="origin">The shape's origin: the boss for Boss-anchored shapes, the landing point for Target-anchored ones.</param>
        /// <returns>A unit direction for the player to move in, or Vec2.Zero when there is no useful arrow.</returns>
        public static Vec2 Toward(Response r, Shape s, Vec2 bossPos, Vec2 facing, Vec2 origin, Vec2 me)
        {
            switch (r)
            {
                case Response.GetBehind:
                    return (bossPos - facing * BehindDistance - me).Normalized;
                case Response.LeaveArea:
                {
                    Vec2 centre = s.Kind == ShapeKind.Circle ? origin + facing * s.Offset : origin;
                    Vec2 away = (me - centre).Normalized;
                    return away.Length > 0f ? away : (facing * -1f).Normalized;
                }
                case Response.LeaveLine:
                    // Step out sideways, to whichever side of the boss's line you already stand on.
                    return Vec2.Cross(facing, me - origin) < 0f ? (facing.Left * -1f).Normalized : facing.Left.Normalized;
                case Response.KeepMoving:
                {
                    Vec2 fromBoss = (me - bossPos).Normalized;
                    return fromBoss.Length > 0f ? fromBoss.Left : facing.Left.Normalized;
                }
                case Response.ExitRing:
                {
                    // The wall of fire starts on the far side and closes towards the boss; leave away from the boss.
                    Vec2 away = (me - bossPos).Normalized;
                    return away.Length > 0f ? away : facing.Normalized;
                }
                default:
                    return Vec2.Zero;
            }
        }
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: `Passed: 26`, 0 failed.

- [ ] **Step 6: Commit and push**

```bash
git add src/Forewarned/Core/Model tests/Forewarned.Tests
git commit -m "Model: safe direction for the path arrow"
git push
```

---

### Task 4: Ability specs and live-number resolution

**Files:**
- Create: `src/Forewarned/Core/Model/AbilitySpec.cs`
- Test: `tests/Forewarned.Tests/AbilitySpecTests.cs`

**Interfaces:**
- Consumes: `Shape`, `Response` (Tasks 2–3).
- Produces:
  - `enum Level { None, Info, Caution, Danger }` (ordered: higher is more urgent).
  - `sealed class AbilitySpec`: fields `Id, Name, ItemPrefab, Triggers (string[]), DefaultLevel, DefaultOn (=true), ActionKey, Response, Shape (=Shape.None), Cooldown, HpMin (=0), HpMax (=1), WindUp, Hits (=1), AiRange, MaxAngle`; property `NameKey` (`Id + ".name"`).
  - `sealed class AbilityNumbers`: nullable floats `Cooldown, HpMin, HpMax, AiRange, MaxAngle, Range, Angle, Width, Radius, Offset`.
  - `sealed class ResolvedAbility`: `Spec, Shape, Cooldown, HpMin, HpMax, AiRange, MaxAngle, WindUp, WindUpLearned`; `static ResolvedAbility Resolve(AbilitySpec spec, AbilityNumbers live, float? learnedWindUp)`; `bool UsableAt(float hp)`.

- [ ] **Step 1: Write the failing tests** — `tests/Forewarned.Tests/AbilitySpecTests.cs`

```csharp
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class AbilitySpecTests
    {
        private static AbilitySpec Breath() => new AbilitySpec
        {
            Id = "fader.flamebreath", Name = "Flame breath", ItemPrefab = "Fader_Flamebreath",
            Triggers = new[] { "attack_flamebreath" }, DefaultLevel = Level.Danger,
            ActionKey = "action.get_behind", Response = Response.GetBehind,
            Shape = Shape.Line(39.45f, 3f, ShapeSource.Aoe),
            Cooldown = 25f, HpMin = 0.05f, HpMax = 0.85f, WindUp = 2.34f, AiRange = 20f, MaxAngle = 15f
        };

        [Fact]
        public void NameKeyFollowsTheId()
        {
            Assert.Equal("fader.flamebreath.name", Breath().NameKey);
        }

        [Fact]
        public void WithoutLiveDataTheSpecNumbersApply()
        {
            AbilitySpec spec = Breath();
            ResolvedAbility a = ResolvedAbility.Resolve(spec, null, null);
            Assert.Equal(25f, a.Cooldown);
            Assert.Equal(2.34f, a.WindUp);
            Assert.False(a.WindUpLearned);
            Assert.Equal(39.45f, a.Shape.Range);
            Assert.NotSame(spec.Shape, a.Shape);
        }

        [Fact]
        public void LiveNumbersOverride()
        {
            var live = new AbilityNumbers { Cooldown = 20f, HpMax = 0.8f, Range = 40f, Width = 4f, AiRange = 22f };
            ResolvedAbility a = ResolvedAbility.Resolve(Breath(), live, null);
            Assert.Equal(20f, a.Cooldown);
            Assert.Equal(0.8f, a.HpMax);
            Assert.Equal(0.05f, a.HpMin);
            Assert.Equal(22f, a.AiRange);
            Assert.Equal(40f, a.Shape.Range);
            Assert.Equal(4f, a.Shape.Width);
        }

        [Fact]
        public void FixedShapesIgnoreLiveSizes()
        {
            AbilitySpec spec = Breath();
            spec.Shape = Shape.Circle(5f, 0f, Anchor.Target, ShapeSource.Fixed);
            ResolvedAbility a = ResolvedAbility.Resolve(spec, new AbilityNumbers { Radius = 9f }, null);
            Assert.Equal(5f, a.Shape.Radius);
        }

        [Fact]
        public void LearnedWindUpWins()
        {
            ResolvedAbility a = ResolvedAbility.Resolve(Breath(), null, 2.1f);
            Assert.Equal(2.1f, a.WindUp);
            Assert.True(a.WindUpLearned);
        }

        [Theory]
        [InlineData(0.85f, true)]
        [InlineData(0.86f, false)]
        [InlineData(0.05f, true)]
        [InlineData(0.04f, false)]
        public void HealthGatesAreInclusive(float hp, bool usable)
        {
            Assert.Equal(usable, ResolvedAbility.Resolve(Breath(), null, null).UsableAt(hp));
        }
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: build error, `'AbilitySpec' could not be found`.

- [ ] **Step 3: Write `AbilitySpec.cs`**

```csharp
namespace Forewarned.Core.Model
{
    /// <summary>Urgency (PLAN.md §9). Ordered: a higher value is more urgent.</summary>
    public enum Level { None, Info, Caution, Danger }

    /// <summary>
    /// One boss ability: our decisions (level, wording, response, shape kind) plus fallback numbers
    /// from the offline research (PLAN.md §4, §10). Live values from the game override the numbers.
    /// </summary>
    public sealed class AbilitySpec
    {
        /// <summary>Stable id, also the translation-key prefix: "fader.flamebreath".</summary>
        public string Id;
        /// <summary>English display name for config rows; the shown name comes from NameKey.</summary>
        public string Name;
        /// <summary>The boss inventory item that carries this attack: "Fader_Flamebreath".</summary>
        public string ItemPrefab;
        /// <summary>Animator triggers that start it, including any index the game appends.</summary>
        public string[] Triggers = new string[0];
        public Level DefaultLevel;
        public bool DefaultOn = true;
        /// <summary>Translation key of the action line ("action.get_behind") or announce text. Null for Level.None.</summary>
        public string ActionKey;
        public Response Response;
        public Shape Shape = Shape.None;
        public float Cooldown;
        public float HpMin;
        public float HpMax = 1f;
        /// <summary>Seconds from trigger to the first hit.</summary>
        public float WindUp;
        public int Hits = 1;
        public float AiRange;
        public float MaxAngle;

        public string NameKey => Id + ".name";
    }

    /// <summary>Numbers read from the boss's own item in game (PLAN.md §11.3). Null: not available.</summary>
    public sealed class AbilityNumbers
    {
        public float? Cooldown;
        public float? HpMin;
        public float? HpMax;
        public float? AiRange;
        public float? MaxAngle;
        public float? Range;
        public float? Angle;
        public float? Width;
        public float? Radius;
        public float? Offset;
    }

    /// <summary>A spec with live numbers and the learned wind-up applied: what a warning is built from.</summary>
    public sealed class ResolvedAbility
    {
        public AbilitySpec Spec;
        public Shape Shape;
        public float Cooldown;
        public float HpMin;
        public float HpMax;
        public float AiRange;
        public float MaxAngle;
        public float WindUp;
        public bool WindUpLearned;

        public static ResolvedAbility Resolve(AbilitySpec spec, AbilityNumbers live, float? learnedWindUp)
        {
            live = live ?? new AbilityNumbers();
            Shape shape = spec.Shape.Copy();
            if (shape.Source != ShapeSource.Fixed)
            {
                shape.Range = live.Range ?? shape.Range;
                shape.Angle = live.Angle ?? shape.Angle;
                shape.Width = live.Width ?? shape.Width;
                shape.Radius = live.Radius ?? shape.Radius;
                shape.Offset = live.Offset ?? shape.Offset;
            }
            return new ResolvedAbility
            {
                Spec = spec,
                Shape = shape,
                Cooldown = live.Cooldown ?? spec.Cooldown,
                HpMin = live.HpMin ?? spec.HpMin,
                HpMax = live.HpMax ?? spec.HpMax,
                AiRange = live.AiRange ?? spec.AiRange,
                MaxAngle = live.MaxAngle ?? spec.MaxAngle,
                WindUp = learnedWindUp ?? spec.WindUp,
                WindUpLearned = learnedWindUp.HasValue
            };
        }

        /// <summary>BaseAI.CanUseAttack's health gates: both bounds inclusive (PLAN.md §1).</summary>
        public bool UsableAt(float hp) => hp >= HpMin - 1e-4f && hp <= HpMax + 1e-4f;
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: `Passed: 35`, 0 failed.

- [ ] **Step 5: Commit and push**

```bash
git add src/Forewarned/Core/Model tests/Forewarned.Tests
git commit -m "Model: ability specs and live-number resolution"
git push
```

---

### Task 5: Boss modules and the registry

**Files:**
- Create: `src/Forewarned/Core/Model/BossModule.cs`, `src/Forewarned/Core/Model/ModuleRegistry.cs`
- Test: `tests/Forewarned.Tests/ModuleRegistryTests.cs`

**Interfaces:**
- Consumes: `AbilitySpec` (Task 4).
- Produces:
  - `sealed class Phase { float At; string Key; }`.
  - `abstract class BossModule`: abstract `string Key`, `string DisplayName`, `string NameToken`, `int Order`, `string[] Prefabs`, `IReadOnlyList<AbilitySpec> Abilities`; virtual `IReadOnlyList<Phase> Phases` (default empty); virtual `string OnStateChanged(string state, bool value)` (default null); `protected static readonly IReadOnlyList<Phase> NoPhases`.
  - `enum TriggerKind { NotTracked, Ignored, Unmapped, Mapped }`.
  - `sealed class ModuleRegistry(IEnumerable<BossModule>)`: `static readonly HashSet<string> IgnoredTriggers`, `IReadOnlyList<BossModule> Modules`, `bool Tracks(string prefab)`, `BossModule ModuleFor(string prefab)` (null if unknown), `TriggerKind Classify(string prefab, string trigger, out AbilitySpec ability)`. The constructor throws `ArgumentException` on a prefab claimed twice or a (prefab, trigger) mapped twice.

- [ ] **Step 1: Write the failing tests** — `tests/Forewarned.Tests/ModuleRegistryTests.cs`

```csharp
using System;
using System.Collections.Generic;
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class ModuleRegistryTests
    {
        private sealed class TestModule : BossModule
        {
            private readonly string[] _prefabs;
            private readonly AbilitySpec[] _abilities;

            public TestModule(string[] prefabs, params AbilitySpec[] abilities)
            {
                _prefabs = prefabs;
                _abilities = abilities;
            }

            public override string Key => "test";
            public override string DisplayName => "Test";
            public override string NameToken => "$enemy_test";
            public override int Order => 3;
            public override string[] Prefabs => _prefabs;
            public override IReadOnlyList<AbilitySpec> Abilities => _abilities;
        }

        private static AbilitySpec Spec(string id, params string[] triggers) =>
            new AbilitySpec { Id = id, Name = id, ItemPrefab = id, Triggers = triggers, DefaultLevel = Level.Danger };

        private static ModuleRegistry Registry() =>
            new ModuleRegistry(new BossModule[]
            {
                new TestModule(new[] { "Fader", "Aspect_Fader" }, Spec("fader.claw", "attack_ClawL", "attack_ClawR"), Spec("fader.meteors", "taunt"))
            });

        [Fact]
        public void MapsTriggersOfEveryPrefabTheModuleClaims()
        {
            ModuleRegistry r = Registry();
            AbilitySpec a;
            Assert.Equal(TriggerKind.Mapped, r.Classify("Fader", "attack_ClawR", out a));
            Assert.Equal("fader.claw", a.Id);
            Assert.Equal(TriggerKind.Mapped, r.Classify("Aspect_Fader", "taunt", out a));
            Assert.Equal("fader.meteors", a.Id);
            Assert.Same(r.ModuleFor("Fader"), r.ModuleFor("Aspect_Fader"));
            Assert.True(r.Tracks("Aspect_Fader"));
        }

        [Fact]
        public void SortsOutTheRest()
        {
            ModuleRegistry r = Registry();
            AbilitySpec a;
            Assert.Equal(TriggerKind.Ignored, r.Classify("Fader", "attack_abort", out a));
            Assert.Null(a);
            Assert.Equal(TriggerKind.Unmapped, r.Classify("Fader", "jump_forward", out a));
            Assert.Equal(TriggerKind.NotTracked, r.Classify("Greydwarf", "attack", out a));
            Assert.Null(r.ModuleFor("Greydwarf"));
            Assert.False(r.Tracks("Greydwarf"));
        }

        [Fact]
        public void RejectsDuplicates()
        {
            Assert.Throws<ArgumentException>(() => new ModuleRegistry(new BossModule[]
            {
                new TestModule(new[] { "Fader" }, Spec("a", "taunt"), Spec("b", "taunt"))
            }));
            Assert.Throws<ArgumentException>(() => new ModuleRegistry(new BossModule[]
            {
                new TestModule(new[] { "Fader" }, Spec("a", "x")),
                new TestModule(new[] { "Fader" }, Spec("b", "y"))
            }));
        }

        [Fact]
        public void DefaultsAreEmpty()
        {
            var m = new TestModule(new[] { "X" });
            Assert.Empty(m.Phases);
            Assert.Null(m.OnStateChanged("flying", true));
        }
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: build error, `'BossModule' could not be found`.

- [ ] **Step 3: Write `BossModule.cs`**

```csharp
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    /// <summary>A health threshold (0..1) whose downward crossing is announced with Key.</summary>
    public sealed class Phase
    {
        public float At;
        public string Key;
    }

    /// <summary>
    /// One boss (PLAN.md §11.2): the prefabs it covers, its abilities and phase thresholds, and
    /// optional boss-specific reactions. Pure data and logic; no game types.
    /// </summary>
    public abstract class BossModule
    {
        protected static readonly IReadOnlyList<Phase> NoPhases = new Phase[0];

        /// <summary>Config and translation prefix: "fader".</summary>
        public abstract string Key { get; }
        /// <summary>English name for the config section: "Fader".</summary>
        public abstract string DisplayName { get; }
        /// <summary>The game's name token, localised by the game: "$enemy_fader".</summary>
        public abstract string NameToken { get; }
        /// <summary>Game order, for the config section number: Eikthyr 3 … Kall 10.</summary>
        public abstract int Order { get; }
        public abstract string[] Prefabs { get; }
        public abstract IReadOnlyList<AbilitySpec> Abilities { get; }

        public virtual IReadOnlyList<Phase> Phases => NoPhases;

        /// <summary>An announce key for a change in a tracked state (Moder: "flying"), or null for none.</summary>
        public virtual string OnStateChanged(string state, bool value) => null;
    }
}
```

- [ ] **Step 4: Write `ModuleRegistry.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    public enum TriggerKind { NotTracked, Ignored, Unmapped, Mapped }

    /// <summary>PLAN.md §11.2: prefab → module, (prefab, trigger) → ability.</summary>
    public sealed class ModuleRegistry
    {
        /// <summary>Non-attack triggers the game sends through the same RPC (decomp: SetTrigger call sites).</summary>
        public static readonly HashSet<string> IgnoredTriggers = new HashSet<string>(StringComparer.Ordinal)
        {
            "attack_abort", "detach", "attach", "stagger", "consume", "jump", "dodge", "eat",
            "interact", "equip_hip", "emote_stop", "gpower", "teleportin", "flyin"
        };

        private readonly List<BossModule> _modules = new List<BossModule>();
        private readonly Dictionary<string, BossModule> _byPrefab = new Dictionary<string, BossModule>(StringComparer.Ordinal);
        private readonly Dictionary<string, AbilitySpec> _byTrigger = new Dictionary<string, AbilitySpec>(StringComparer.Ordinal);

        public ModuleRegistry(IEnumerable<BossModule> modules)
        {
            foreach (BossModule m in modules)
            {
                _modules.Add(m);
                foreach (string prefab in m.Prefabs)
                {
                    if (_byPrefab.ContainsKey(prefab))
                        throw new ArgumentException("Prefab claimed twice: " + prefab);
                    _byPrefab[prefab] = m;
                    foreach (AbilitySpec a in m.Abilities)
                        foreach (string trigger in a.Triggers)
                        {
                            string key = Key(prefab, trigger);
                            if (_byTrigger.ContainsKey(key))
                                throw new ArgumentException("Trigger mapped twice: " + prefab + " " + trigger);
                            _byTrigger[key] = a;
                        }
                }
            }
        }

        public IReadOnlyList<BossModule> Modules => _modules;

        public bool Tracks(string prefab) => prefab != null && _byPrefab.ContainsKey(prefab);

        public BossModule ModuleFor(string prefab)
        {
            BossModule m;
            return prefab != null && _byPrefab.TryGetValue(prefab, out m) ? m : null;
        }

        public TriggerKind Classify(string prefab, string trigger, out AbilitySpec ability)
        {
            ability = null;
            if (!Tracks(prefab))
                return TriggerKind.NotTracked;
            if (_byTrigger.TryGetValue(Key(prefab, trigger), out ability))
                return TriggerKind.Mapped;
            return IgnoredTriggers.Contains(trigger) ? TriggerKind.Ignored : TriggerKind.Unmapped;
        }

        private static string Key(string prefab, string trigger) => prefab + "\n" + trigger;
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: `Passed: 39`, 0 failed.

- [ ] **Step 6: Commit and push**

```bash
git add src/Forewarned/Core/Model tests/Forewarned.Tests
git commit -m "Model: boss modules and the trigger registry"
git push
```

---

### Task 6: Tracker: triggers, hits and learned wind-ups

**Files:**
- Create: `src/Forewarned/Core/Model/Tracker.cs`
- Test: `tests/Forewarned.Tests/TrackerTests.cs`

**Interfaces:**
- Produces: `sealed class Tracker`: consts `MinWindUp = 0.2f`, `MaxWindUp = 6f`, `MaxSamples = 9`; `void OnTrigger(long bossId, string prefab, string trigger, string abilityId, float time)`; `string OnHit(long bossId, float time)` (the ability id whose wind-up the hit ended, or null); `float? LearnedWindUp(string prefab, string trigger)`; `int SampleCount(string prefab, string trigger)`; `float? LastUse(long bossId, string abilityId)`; `void SetHealth(long bossId, float hp)`; `float? Health(long bossId)`; `void Forget(long bossId)`; `void Clear()`.

- [ ] **Step 1: Write the failing tests** — `tests/Forewarned.Tests/TrackerTests.cs`

```csharp
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class TrackerTests
    {
        private const string Fader = "Fader";
        private const string Breath = "attack_flamebreath";

        [Fact]
        public void AHitEndsThePendingTriggerAndTeachesTheWindUp()
        {
            var t = new Tracker();
            t.OnTrigger(1, Fader, Breath, "fader.flamebreath", 10f);
            Assert.Equal("fader.flamebreath", t.OnHit(1, 12.3f));
            Assert.Equal(2.3f, t.LearnedWindUp(Fader, Breath).Value, 3);
            Assert.Equal(10f, t.LastUse(1, "fader.flamebreath"));
        }

        [Fact]
        public void HitsWithoutATriggerOrAfterTheFirstAreIgnored()
        {
            var t = new Tracker();
            Assert.Null(t.OnHit(1, 5f));
            t.OnTrigger(1, Fader, Breath, "fader.flamebreath", 10f);
            t.OnHit(1, 12f);
            Assert.Null(t.OnHit(1, 13f)); // second hit of a multi-hit attack
            Assert.Equal(1, t.SampleCount(Fader, Breath));
        }

        [Fact]
        public void TooSoonKeepsWaitingTooLateIsDropped()
        {
            var t = new Tracker();
            t.OnTrigger(1, Fader, Breath, "fader.flamebreath", 10f);
            Assert.Null(t.OnHit(1, 10.1f));
            Assert.Equal("fader.flamebreath", t.OnHit(1, 12f));
            Assert.Equal(2f, t.LearnedWindUp(Fader, Breath).Value, 3);

            t.OnTrigger(1, Fader, Breath, "fader.flamebreath", 20f);
            Assert.Null(t.OnHit(1, 27f));
            Assert.Equal(1, t.SampleCount(Fader, Breath));
        }

        [Fact]
        public void OutliersDoNotMoveTheEstimate()
        {
            var t = new Tracker();
            float[] samples = { 2.3f, 2.35f, 2.25f, 5f };
            for (int i = 0; i < samples.Length; i++)
            {
                t.OnTrigger(1, Fader, Breath, "fader.flamebreath", i * 10f);
                t.OnHit(1, i * 10f + samples[i]);
            }
            Assert.Equal(2.3f, t.LearnedWindUp(Fader, Breath).Value, 3);
        }

        [Fact]
        public void KeepsOnlyTheLatestSamples()
        {
            var t = new Tracker();
            for (int i = 0; i < 12; i++)
            {
                t.OnTrigger(1, Fader, Breath, "fader.flamebreath", i * 10f);
                t.OnHit(1, i * 10f + (i < 3 ? 1f : 2f));
            }
            Assert.Equal(Tracker.MaxSamples, t.SampleCount(Fader, Breath));
            Assert.Equal(2f, t.LearnedWindUp(Fader, Breath).Value, 3);
        }

        [Fact]
        public void BossesAreTrackedSeparately()
        {
            var t = new Tracker();
            t.OnTrigger(1, Fader, Breath, "fader.flamebreath", 10f);
            Assert.Null(t.OnHit(2, 12f));
            Assert.Equal("fader.flamebreath", t.OnHit(1, 12f));
            t.SetHealth(1, 0.5f);
            Assert.Equal(0.5f, t.Health(1));
            Assert.Null(t.Health(2));
        }

        [Fact]
        public void ForgetClearsOneBossButKeepsWhatWasLearned()
        {
            var t = new Tracker();
            t.OnTrigger(1, Fader, Breath, "fader.flamebreath", 10f);
            t.OnHit(1, 12f);
            t.SetHealth(1, 0.5f);
            t.Forget(1);
            Assert.Null(t.Health(1));
            Assert.Null(t.LastUse(1, "fader.flamebreath"));
            Assert.Equal(1, t.SampleCount(Fader, Breath));
            t.Clear();
            Assert.Null(t.LearnedWindUp(Fader, Breath));
        }
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: build error, `'Tracker' could not be found`.

- [ ] **Step 3: Write `Tracker.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    /// <summary>
    /// PLAN.md §11.2: what each boss has done. Pairs every trigger with the next hit event to learn
    /// the real wind-up per (prefab, trigger), and keeps each ability's last use and the boss's
    /// health for later "next X in ~N s" timers.
    /// </summary>
    public sealed class Tracker
    {
        /// <summary>A hit sooner than this after a trigger belongs to the previous attack.</summary>
        public const float MinWindUp = 0.2f;
        /// <summary>A hit later than this means the trigger's own hit was missed (aborted, out of range).</summary>
        public const float MaxWindUp = 6f;
        public const int MaxSamples = 9;

        private sealed class Pending
        {
            public string Prefab;
            public string Trigger;
            public string AbilityId;
            public float Time;
        }

        private readonly Dictionary<long, Pending> _pending = new Dictionary<long, Pending>();
        private readonly Dictionary<string, List<float>> _samples = new Dictionary<string, List<float>>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _lastUse = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<long, float> _health = new Dictionary<long, float>();

        public void OnTrigger(long bossId, string prefab, string trigger, string abilityId, float time)
        {
            _pending[bossId] = new Pending { Prefab = prefab, Trigger = trigger, AbilityId = abilityId, Time = time };
            _lastUse[UseKey(bossId, abilityId)] = time;
        }

        public string OnHit(long bossId, float time)
        {
            Pending p;
            if (!_pending.TryGetValue(bossId, out p))
                return null;
            float dt = time - p.Time;
            if (dt < MinWindUp)
                return null;
            _pending.Remove(bossId);
            if (dt > MaxWindUp)
                return null;
            string key = SampleKey(p.Prefab, p.Trigger);
            List<float> list;
            if (!_samples.TryGetValue(key, out list))
                _samples[key] = list = new List<float>();
            list.Add(dt);
            if (list.Count > MaxSamples)
                list.RemoveAt(0);
            return p.AbilityId;
        }

        /// <summary>Mean of the samples within 25% of their median, or null with no samples.</summary>
        public float? LearnedWindUp(string prefab, string trigger)
        {
            List<float> list;
            if (!_samples.TryGetValue(SampleKey(prefab, trigger), out list) || list.Count == 0)
                return null;
            var sorted = new List<float>(list);
            sorted.Sort();
            int n = sorted.Count;
            float median = n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5f;
            float sum = 0f;
            int kept = 0;
            foreach (float s in sorted)
                if (Math.Abs(s - median) <= median * 0.25f)
                {
                    sum += s;
                    kept++;
                }
            return kept > 0 ? sum / kept : median;
        }

        public int SampleCount(string prefab, string trigger)
        {
            List<float> list;
            return _samples.TryGetValue(SampleKey(prefab, trigger), out list) ? list.Count : 0;
        }

        public float? LastUse(long bossId, string abilityId)
        {
            float t;
            return _lastUse.TryGetValue(UseKey(bossId, abilityId), out t) ? t : (float?)null;
        }

        public void SetHealth(long bossId, float hp) => _health[bossId] = hp;

        public float? Health(long bossId)
        {
            float hp;
            return _health.TryGetValue(bossId, out hp) ? hp : (float?)null;
        }

        /// <summary>The boss is gone. What was learned about its prefab stays.</summary>
        public void Forget(long bossId)
        {
            _pending.Remove(bossId);
            _health.Remove(bossId);
            string prefix = bossId + "\n";
            var stale = new List<string>();
            foreach (string k in _lastUse.Keys)
                if (k.StartsWith(prefix, StringComparison.Ordinal))
                    stale.Add(k);
            foreach (string k in stale)
                _lastUse.Remove(k);
        }

        public void Clear()
        {
            _pending.Clear();
            _samples.Clear();
            _lastUse.Clear();
            _health.Clear();
        }

        private static string SampleKey(string prefab, string trigger) => prefab + "\n" + trigger;
        private static string UseKey(long bossId, string abilityId) => bossId + "\n" + abilityId;
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: `Passed: 46`, 0 failed.

- [ ] **Step 5: Commit and push**

```bash
git add src/Forewarned/Core/Model tests/Forewarned.Tests
git commit -m "Model: tracker pairs triggers with hits and learns wind-ups"
git push
```

---

### Task 7: Relevance: who gets the warning

**Files:**
- Create: `src/Forewarned/Core/Model/Relevance.cs`
- Test: `tests/Forewarned.Tests/RelevanceTests.cs`

**Interfaces:**
- Consumes: `Vec2`, `Shape`, `AreaTest`, `Verdict`, `Level`.
- Produces:
  - `sealed class Scene { Vec2 BossPos; Vec2 BossFacing; Vec2 Me; List<Vec2> Others = new List<Vec2>(); }`.
  - `enum Outcome { Nothing, Special, Announce }`.
  - `static class Relevance`: `Vec2? AimPoint(Scene s, float aiRange)`; `Verdict Judge(Shape shape, float aiRange, Scene s, out Vec2 origin)`; `Outcome Decide(Level level, Verdict v, bool alwaysWarn)`.

- [ ] **Step 1: Write the failing tests** — `tests/Forewarned.Tests/RelevanceTests.cs`

```csharp
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class RelevanceTests
    {
        private static Scene Solo(float x, float z) =>
            new Scene { BossPos = new Vec2(0f, 0f), BossFacing = new Vec2(0f, 1f), Me = new Vec2(x, z) };

        [Fact]
        public void BossAnchoredShapesStartAtTheBoss()
        {
            Vec2 origin;
            Assert.Equal(Verdict.Inside, Relevance.Judge(Shape.Line(39f, 3f), 20f, Solo(0f, 10f), out origin));
            Assert.Equal(0f, origin.Length);
            Assert.Equal(Verdict.Outside, Relevance.Judge(Shape.Line(39f, 3f), 20f, Solo(0f, -10f), out origin));
        }

        [Fact]
        public void SoloYouAreTheTargetWhenInRange()
        {
            Vec2 origin;
            Shape meteors = Shape.Circle(15f, 0f, Anchor.Target);
            Assert.Equal(Verdict.Inside, Relevance.Judge(meteors, 30f, Solo(5f, 20f), out origin));
            Assert.Equal(5f, origin.X);
            Assert.Equal(Verdict.Outside, Relevance.Judge(meteors, 30f, Solo(0f, 40f), out origin));
        }

        [Fact]
        public void InAGroupTheBossAimsAtWhoeverItFaces()
        {
            Shape meteors = Shape.Circle(15f, 0f, Anchor.Target);
            var s = Solo(10f, 0f);
            s.Others.Add(new Vec2(0f, 10f)); // straight ahead of the boss
            Vec2? aim = Relevance.AimPoint(s, 30f);
            Assert.Equal(10f, aim.Value.Z);
            Vec2 origin;
            Assert.Equal(Verdict.Inside, Relevance.Judge(meteors, 30f, s, out origin)); // 14.1 m from the other player
            s.Me = new Vec2(20f, 0f);
            Assert.Equal(Verdict.Outside, Relevance.Judge(meteors, 30f, s, out origin)); // 22.4 m
        }

        [Fact]
        public void AShapelessAttackReachesEveryoneInItsRange()
        {
            Vec2 origin;
            Assert.Equal(Verdict.Inside, Relevance.Judge(Shape.None, 100f, Solo(0f, 50f), out origin));
            Assert.Equal(Verdict.Outside, Relevance.Judge(Shape.None, 30f, Solo(0f, 50f), out origin));
        }

        [Theory]
        [InlineData(Level.Danger, Verdict.Inside, false, Outcome.Special)]
        [InlineData(Level.Danger, Verdict.Near, false, Outcome.Special)]
        [InlineData(Level.Danger, Verdict.Outside, false, Outcome.Announce)]
        [InlineData(Level.Caution, Verdict.Inside, false, Outcome.Special)]
        [InlineData(Level.Caution, Verdict.Outside, false, Outcome.Nothing)]
        [InlineData(Level.Caution, Verdict.Outside, true, Outcome.Special)]
        [InlineData(Level.Info, Verdict.Inside, false, Outcome.Announce)]
        [InlineData(Level.Info, Verdict.Outside, true, Outcome.Announce)]
        [InlineData(Level.None, Verdict.Inside, true, Outcome.Nothing)]
        public void Decide(Level level, Verdict v, bool always, Outcome expected)
        {
            Assert.Equal(expected, Relevance.Decide(level, v, always));
        }
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: build error, `'Scene' could not be found`.

- [ ] **Step 3: Write `Relevance.cs`**

```csharp
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    /// <summary>Where everyone stands when a trigger arrives. Others: the other players' positions.</summary>
    public sealed class Scene
    {
        public Vec2 BossPos;
        public Vec2 BossFacing;
        public Vec2 Me;
        public List<Vec2> Others = new List<Vec2>();
    }

    public enum Outcome { Nothing, Special, Announce }

    /// <summary>PLAN.md §9 "who gets a warning" and §11.2 Relevance.</summary>
    public static class Relevance
    {
        /// <summary>
        /// Where a target-anchored attack lands: among the players within aiRange (+ margin) of the
        /// boss, the one it faces most directly. Solo that's you whenever you're in range. Null when
        /// nobody is in range.
        /// </summary>
        public static Vec2? AimPoint(Scene s, float aiRange)
        {
            float reach = aiRange + AreaTest.Margin;
            Vec2? best = null;
            float bestAngle = float.MaxValue;
            Consider(s, s.Me, reach, ref best, ref bestAngle);
            foreach (Vec2 p in s.Others)
                Consider(s, p, reach, ref best, ref bestAngle);
            return best;
        }

        private static void Consider(Scene s, Vec2 p, float reach, ref Vec2? best, ref float bestAngle)
        {
            if (Vec2.Distance(s.BossPos, p) > reach)
                return;
            float angle = Geometry.AngleDeg(s.BossFacing, p - s.BossPos);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = p;
            }
        }

        /// <param name="origin">Set to where the shape is anchored: the boss, or the aim point.</param>
        public static Verdict Judge(Shape shape, float aiRange, Scene s, out Vec2 origin)
        {
            origin = s.BossPos;
            if (shape.Kind == ShapeKind.None)
                return Vec2.Distance(s.BossPos, s.Me) <= aiRange + AreaTest.Margin ? Verdict.Inside : Verdict.Outside;
            if (shape.Anchor == Anchor.Boss)
                return AreaTest.Classify(shape, s.BossPos, s.BossFacing, s.Me);
            Vec2? aim = AimPoint(s, aiRange);
            if (aim == null)
            {
                origin = s.Me;
                return Verdict.Outside;
            }
            origin = aim.Value;
            return AreaTest.Classify(shape, origin, s.BossFacing, s.Me);
        }

        /// <summary>Inside or near: the special warning. Outside: an announce for Danger, nothing for Caution.
        /// Info is always an announce; always-warn skips the area test.</summary>
        public static Outcome Decide(Level level, Verdict v, bool alwaysWarn)
        {
            switch (level)
            {
                case Level.None: return Outcome.Nothing;
                case Level.Info: return Outcome.Announce;
            }
            if (alwaysWarn || v != Verdict.Outside)
                return Outcome.Special;
            return level == Level.Danger ? Outcome.Announce : Outcome.Nothing;
        }
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: `Passed: 59`, 0 failed.

- [ ] **Step 5: Commit and push**

```bash
git add src/Forewarned/Core/Model tests/Forewarned.Tests
git commit -m "Model: relevance decides who gets each warning"
git push
```

---

### Task 8: Warning board: slots, stacking, timing, sound gating

**Files:**
- Create: `src/Forewarned/Core/Model/WarningBoard.cs`
- Test: `tests/Forewarned.Tests/WarningBoardTests.cs`

**Interfaces:**
- Consumes: `Level`, `Shape`, `Response`, `Vec2`, `Geometry`.
- Produces:
  - `sealed class Warning`: `AbilityId, BossId (long), Level, Title, Action, Start, HitAt, HitSeen, Sound, Visual, Shape, Response, Origin (Vec2), PlaySound`.
  - `enum OfferResult { Added, Replaced, Evicted, Dropped }`.
  - `sealed class WarningBoard`: consts `Slots = 2`, `Linger = 0.5f`, `MinShow = 1.2f`, `FadeTime = 0.3f`, `SoundGap = 1f`; `IReadOnlyList<Warning> Active` (index 0 = newest, drawn on top); `OfferResult Offer(Warning w, float now)`; `void Hit(string abilityId, long bossId, float time)`; `void Tick(float now)`; `void Clear()`; statics `float EndAt(Warning w)`, `float Alpha(Warning w, float now)`, `float Progress(Warning w, float now)`, `float Remaining(Warning w, float now)`.

- [ ] **Step 1: Write the failing tests** — `tests/Forewarned.Tests/WarningBoardTests.cs`

```csharp
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class WarningBoardTests
    {
        private static Warning W(string id, Level level, float start, float windUp = 2f, bool sound = true) =>
            new Warning { AbilityId = id, BossId = 1, Level = level, Title = id, Start = start, HitAt = start + windUp, Sound = sound };

        [Fact]
        public void NewestGoesOnTop()
        {
            var b = new WarningBoard();
            Assert.Equal(OfferResult.Added, b.Offer(W("a", Level.Danger, 10f), 10f));
            Assert.Equal(OfferResult.Added, b.Offer(W("b", Level.Danger, 10.5f), 10.5f));
            Assert.Equal("b", b.Active[0].AbilityId);
            Assert.Equal("a", b.Active[1].AbilityId);
        }

        [Fact]
        public void TheSameAbilityReplacesItsOwnLine()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f), 10f);
            b.Offer(W("b", Level.Danger, 10.2f), 10.2f);
            Assert.Equal(OfferResult.Replaced, b.Offer(W("a", Level.Danger, 10.4f), 10.4f));
            Assert.Equal(2, b.Active.Count);
            Assert.Equal("a", b.Active[0].AbilityId);
            Assert.Equal(10.4f, b.Active[0].Start);
        }

        [Fact]
        public void AThirdDangerEvictsTheOldest()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f), 10f);
            b.Offer(W("b", Level.Danger, 10.2f), 10.2f);
            Assert.Equal(OfferResult.Evicted, b.Offer(W("c", Level.Danger, 10.4f), 10.4f));
            Assert.Equal("c", b.Active[0].AbilityId);
            Assert.Equal("b", b.Active[1].AbilityId);
        }

        [Fact]
        public void CautionNeverEvictsDanger()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f), 10f);
            b.Offer(W("b", Level.Danger, 10.2f), 10.2f);
            Assert.Equal(OfferResult.Dropped, b.Offer(W("c", Level.Caution, 10.4f), 10.4f));
            Assert.Equal(2, b.Active.Count);
        }

        [Fact]
        public void DangerEvictsCautionFirst()
        {
            var b = new WarningBoard();
            b.Offer(W("caution", Level.Caution, 10f), 10f);
            b.Offer(W("old", Level.Danger, 10.2f), 10.2f);
            b.Offer(W("new", Level.Danger, 10.4f), 10.4f);
            Assert.Equal("new", b.Active[0].AbilityId);
            Assert.Equal("old", b.Active[1].AbilityId);
        }

        [Fact]
        public void FadingLinesMakeRoom()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f, 1f), 10f);     // ends 11.5, gone 11.8
            b.Offer(W("b", Level.Danger, 10.1f, 3f), 10.1f);
            Assert.Equal(OfferResult.Added, b.Offer(W("c", Level.Caution, 11.6f), 11.6f)); // a is fading
            Assert.Equal(2, b.Active.Count);
        }

        [Fact]
        public void LivesUntilTheHitPlusHalfASecondButAtLeast1_2Seconds()
        {
            Assert.Equal(12.84f, WarningBoard.EndAt(W("a", Level.Danger, 10f, 2.34f)), 3);
            Assert.Equal(11.2f, WarningBoard.EndAt(W("a", Level.Danger, 10f, 0.6f)), 3);
        }

        [Fact]
        public void ARealHitMovesTheEnd()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f, 2.34f), 10f);
            b.Hit("a", 1, 11.5f);
            Assert.True(b.Active[0].HitSeen);
            Assert.Equal(12f, WarningBoard.EndAt(b.Active[0]), 3);
            b.Hit("a", 1, 11.9f); // a second hit of the same attack doesn't move it again
            Assert.Equal(11.5f, b.Active[0].HitAt, 3);
            b.Hit("a", 2, 11.9f); // another boss's hit doesn't touch it
            Assert.Equal(11.5f, b.Active[0].HitAt, 3);
        }

        [Fact]
        public void FadesOutThenLeaves()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f, 2f), 10f); // ends 12.5
            Assert.Equal(1f, WarningBoard.Alpha(b.Active[0], 12.5f), 3);
            Assert.Equal(0.5f, WarningBoard.Alpha(b.Active[0], 12.65f), 3);
            b.Tick(12.79f);
            Assert.Single(b.Active);
            b.Tick(12.81f);
            Assert.Empty(b.Active);
        }

        [Fact]
        public void ProgressAndRemaining()
        {
            Warning w = W("a", Level.Danger, 10f, 2f);
            Assert.Equal(0f, WarningBoard.Progress(w, 10f), 3);
            Assert.Equal(0.5f, WarningBoard.Progress(w, 11f), 3);
            Assert.Equal(1f, WarningBoard.Progress(w, 13f), 3);
            Assert.Equal(1.5f, WarningBoard.Remaining(w, 10.5f), 3);
            Assert.Equal(0f, WarningBoard.Remaining(w, 13f), 3);
        }

        [Fact]
        public void SoundPlaysAtMostOncePerSecondPerAbility()
        {
            var b = new WarningBoard();
            Warning first = W("a", Level.Danger, 10f);
            b.Offer(first, 10f);
            Assert.True(first.PlaySound);
            Warning again = W("a", Level.Danger, 10.5f);
            b.Offer(again, 10.5f);
            Assert.False(again.PlaySound);
            Warning later = W("a", Level.Danger, 11f);
            b.Offer(later, 11f);
            Assert.True(later.PlaySound);
            Warning muted = W("b", Level.Danger, 12f, 2f, false);
            b.Offer(muted, 12f);
            Assert.False(muted.PlaySound);
        }

        [Fact]
        public void ClearEmptiesTheBoard()
        {
            var b = new WarningBoard();
            b.Offer(W("a", Level.Danger, 10f), 10f);
            b.Clear();
            Assert.Empty(b.Active);
        }
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: build error, `'Warning' could not be found`.

- [ ] **Step 3: Write `WarningBoard.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    /// <summary>One special warning: what the HUD draws above the crosshair, its bar and its ground marker.</summary>
    public sealed class Warning
    {
        public string AbilityId;
        public long BossId;
        public Level Level;
        public string Title;
        public string Action;
        public float Start;
        /// <summary>Predicted hit until HitSeen, then the real one.</summary>
        public float HitAt;
        public bool HitSeen;
        public bool Sound;
        public bool Visual;
        public Shape Shape;
        public Response Response;
        /// <summary>Where a Target-anchored shape lands; the boss's position at the trigger otherwise.</summary>
        public Vec2 Origin;
        /// <summary>Set by the board: Sound, unless the same ability sounded less than SoundGap ago.</summary>
        public bool PlaySound;
    }

    public enum OfferResult { Added, Replaced, Evicted, Dropped }

    /// <summary>PLAN.md §9 timing and stacking: two slots, newest on top, Caution never evicts Danger,
    /// an ability replaces its own line; hit + 0.5 s (at least 1.2 s), then a 0.3 s fade.</summary>
    public sealed class WarningBoard
    {
        public const int Slots = 2;
        public const float Linger = 0.5f;
        public const float MinShow = 1.2f;
        public const float FadeTime = 0.3f;
        public const float SoundGap = 1f;

        private readonly List<Warning> _active = new List<Warning>();
        private readonly Dictionary<string, float> _lastSound = new Dictionary<string, float>(StringComparer.Ordinal);

        /// <summary>Index 0 is the newest, drawn on top.</summary>
        public IReadOnlyList<Warning> Active => _active;

        public OfferResult Offer(Warning w, float now)
        {
            _active.RemoveAll(x => now >= EndAt(x)); // fading lines give up their slot
            OfferResult result = OfferResult.Added;
            int same = _active.FindIndex(x => x.AbilityId == w.AbilityId && x.BossId == w.BossId);
            if (same >= 0)
            {
                _active.RemoveAt(same);
                result = OfferResult.Replaced;
            }
            else if (_active.Count >= Slots)
            {
                // Lowest level first, the oldest among equals; never a line more urgent than w.
                int victim = -1;
                for (int i = _active.Count - 1; i >= 0; i--)
                {
                    if (_active[i].Level > w.Level)
                        continue;
                    if (victim < 0 || _active[i].Level < _active[victim].Level)
                        victim = i;
                }
                if (victim < 0)
                    return OfferResult.Dropped;
                _active.RemoveAt(victim);
                result = OfferResult.Evicted;
            }

            float last;
            w.PlaySound = w.Sound && (!_lastSound.TryGetValue(w.AbilityId, out last) || now - last >= SoundGap);
            if (w.PlaySound)
                _lastSound[w.AbilityId] = now;
            _active.Insert(0, w);
            return result;
        }

        /// <summary>The boss's attack actually landed: the bar and the line end from here.</summary>
        public void Hit(string abilityId, long bossId, float time)
        {
            foreach (Warning w in _active)
                if (!w.HitSeen && w.AbilityId == abilityId && w.BossId == bossId)
                {
                    w.HitAt = time;
                    w.HitSeen = true;
                }
        }

        public void Tick(float now) => _active.RemoveAll(w => now >= EndAt(w) + FadeTime);

        public void Clear()
        {
            _active.Clear();
            _lastSound.Clear();
        }

        public static float EndAt(Warning w) => Math.Max(w.HitAt + Linger, w.Start + MinShow);

        public static float Alpha(Warning w, float now)
        {
            float end = EndAt(w);
            return now <= end ? 1f : Geometry.Clamp01(1f - (now - end) / FadeTime);
        }

        /// <summary>0 at the trigger, 1 at the hit: how far the countdown bar has drained.</summary>
        public static float Progress(Warning w, float now)
        {
            float span = w.HitAt - w.Start;
            return span <= 0f ? 1f : Geometry.Clamp01((now - w.Start) / span);
        }

        public static float Remaining(Warning w, float now) => Math.Max(0f, w.HitAt - now);
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: `Passed: 71`, 0 failed.

- [ ] **Step 5: Commit and push**

```bash
git add src/Forewarned/Core/Model tests/Forewarned.Tests
git commit -m "Model: warning board with slots, timing and sound gating"
git push
```

---

### Task 9: Announcer and phase tracking

**Files:**
- Create: `src/Forewarned/Core/Model/Announcer.cs`, `src/Forewarned/Core/Model/PhaseTracker.cs`
- Test: `tests/Forewarned.Tests/AnnouncerTests.cs`, `tests/Forewarned.Tests/PhaseTrackerTests.cs`

**Interfaces:**
- Consumes: `Phase` (Task 5), `Geometry`.
- Produces:
  - `sealed class Announce { string Key; string Text; float Start; }`; `sealed class Announcer`: consts `MaxLines = 3`, `Life = 4f`, `FadeTime = 0.3f`; `IReadOnlyList<Announce> Lines` (oldest first, newest last = bottom); `void Add(string key, string text, float now)`; `void Tick(float now)`; `void Clear()`; `static float Alpha(Announce a, float now)`.
  - `sealed class PhaseTracker`: `IList<Phase> Observe(long bossId, IReadOnlyList<Phase> phases, float hp)` (newly crossed, highest first); `void Forget(long bossId)`; `void Clear()`.

- [ ] **Step 1: Write the failing tests** — `tests/Forewarned.Tests/AnnouncerTests.cs`

```csharp
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class AnnouncerTests
    {
        [Fact]
        public void NewestAtTheBottomAndAtMostThree()
        {
            var a = new Announcer();
            a.Add("1", "one", 0f);
            a.Add("2", "two", 1f);
            a.Add("3", "three", 2f);
            a.Add("4", "four", 3f);
            Assert.Equal(3, a.Lines.Count);
            Assert.Equal("two", a.Lines[0].Text);
            Assert.Equal("four", a.Lines[2].Text);
        }

        [Fact]
        public void TheSameKeyRefreshesItsLine()
        {
            var a = new Announcer();
            a.Add("adds", "Charred Warriors incoming", 0f);
            a.Add("other", "x", 1f);
            a.Add("adds", "Charred Warriors incoming", 2f);
            Assert.Equal(2, a.Lines.Count);
            Assert.Equal("adds", a.Lines[1].Key);
            Assert.Equal(2f, a.Lines[1].Start);
        }

        [Fact]
        public void LastsFourSecondsThenFades()
        {
            var a = new Announcer();
            a.Add("k", "t", 10f);
            Assert.Equal(1f, Announcer.Alpha(a.Lines[0], 14f), 3);
            Assert.Equal(0.5f, Announcer.Alpha(a.Lines[0], 14.15f), 3);
            a.Tick(14.29f);
            Assert.Single(a.Lines);
            a.Tick(14.31f);
            Assert.Empty(a.Lines);
        }
    }
}
```

`tests/Forewarned.Tests/PhaseTrackerTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class PhaseTrackerTests
    {
        private static readonly IReadOnlyList<Phase> Fader = new[]
        {
            new Phase { At = 0.85f, Key = "85" }, new Phase { At = 0.55f, Key = "55" },
            new Phase { At = 0.35f, Key = "35" }, new Phase { At = 0.25f, Key = "25" }
        };

        private static string Keys(IList<Phase> p) => string.Join(",", p.Select(x => x.Key));

        [Fact]
        public void AnnouncesEachThresholdOnceOnTheWayDown()
        {
            var t = new PhaseTracker();
            Assert.Equal("", Keys(t.Observe(1, Fader, 0.9f)));
            Assert.Equal("85", Keys(t.Observe(1, Fader, 0.84f)));
            Assert.Equal("", Keys(t.Observe(1, Fader, 0.86f))); // healed back over
            Assert.Equal("", Keys(t.Observe(1, Fader, 0.84f))); // jitter: no repeat
            Assert.Equal("55,35", Keys(t.Observe(1, Fader, 0.3f)));
        }

        [Fact]
        public void JoiningMidFightStaysQuietAboutThePast()
        {
            var t = new PhaseTracker();
            Assert.Equal("", Keys(t.Observe(1, Fader, 0.3f)));
            Assert.Equal("25", Keys(t.Observe(1, Fader, 0.2f)));
        }

        [Fact]
        public void ForgetStartsOver()
        {
            var t = new PhaseTracker();
            t.Observe(1, Fader, 0.9f);
            t.Observe(1, Fader, 0.5f);
            t.Forget(1);
            Assert.Equal("", Keys(t.Observe(1, Fader, 0.9f)));
            Assert.Equal("85", Keys(t.Observe(1, Fader, 0.8f)));
        }
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: build error, `'Announcer' could not be found`.

- [ ] **Step 3: Write `Announcer.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    public sealed class Announce
    {
        public string Key;
        public string Text;
        public float Start;
    }

    /// <summary>PLAN.md §11.2: up to three announce lines, 4 s each, newest at the bottom.</summary>
    public sealed class Announcer
    {
        public const int MaxLines = 3;
        public const float Life = 4f;
        public const float FadeTime = 0.3f;

        private readonly List<Announce> _lines = new List<Announce>();

        /// <summary>Oldest first; the last line is the newest, drawn at the bottom.</summary>
        public IReadOnlyList<Announce> Lines => _lines;

        public void Add(string key, string text, float now)
        {
            _lines.RemoveAll(l => l.Key == key);
            _lines.Add(new Announce { Key = key, Text = text, Start = now });
            while (_lines.Count > MaxLines)
                _lines.RemoveAt(0);
        }

        public void Tick(float now) => _lines.RemoveAll(l => now >= l.Start + Life + FadeTime);

        public void Clear() => _lines.Clear();

        public static float Alpha(Announce a, float now)
        {
            float end = a.Start + Life;
            return now <= end ? 1f : Geometry.Clamp01(1f - (now - end) / FadeTime);
        }
    }
}
```

- [ ] **Step 4: Write `PhaseTracker.cs`**

```csharp
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    /// <summary>
    /// Which health thresholds each boss has crossed this fight. A threshold is announced once, on the
    /// way down; healing back over it and jitter around it stay quiet. The first observation of a boss
    /// marks thresholds it is already below as crossed without announcing them.
    /// </summary>
    public sealed class PhaseTracker
    {
        private readonly Dictionary<long, HashSet<string>> _crossed = new Dictionary<long, HashSet<string>>();

        public IList<Phase> Observe(long bossId, IReadOnlyList<Phase> phases, float hp)
        {
            var result = new List<Phase>();
            HashSet<string> crossed;
            bool first = !_crossed.TryGetValue(bossId, out crossed);
            if (first)
                _crossed[bossId] = crossed = new HashSet<string>();
            foreach (Phase p in phases)
            {
                if (hp > p.At || crossed.Contains(p.Key))
                    continue;
                crossed.Add(p.Key);
                if (!first)
                    result.Add(p);
            }
            result.Sort((a, b) => b.At.CompareTo(a.At));
            return result;
        }

        public void Forget(long bossId) => _crossed.Remove(bossId);

        public void Clear() => _crossed.Clear();
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: `Passed: 77`, 0 failed.

- [ ] **Step 6: Commit and push**

```bash
git add src/Forewarned/Core/Model tests/Forewarned.Tests
git commit -m "Model: announcer and phase thresholds"
git push
```

---

### Task 10: Strings and the Fader and Moder modules

**Files:**
- Create: `src/Forewarned/Core/Model/Translations.cs`, `src/Forewarned/Core/Model/Bosses/FaderModule.cs`, `src/Forewarned/Core/Model/Bosses/ModerModule.cs`, `src/Forewarned/Core/Model/Bosses/BossList.cs`
- Modify: `src/Forewarned/translations/English.txt` (replace the placeholder)
- Test: `tests/Forewarned.Tests/TranslationsTests.cs`, `tests/Forewarned.Tests/DataFilesTests.cs`

**Interfaces:**
- Consumes: `AbilitySpec`, `Shape`, `Response`, `Level`, `BossModule`, `Phase`, `ModuleRegistry`, `TextCheck`.
- Produces:
  - `sealed class Translations`: `static Translations Parse(string text)`, `Translations WithFallback(Translations fallback)`, `string Get(string key)` (null if missing), `IEnumerable<string> Keys`.
  - `FaderModule`, `ModerModule` (namespace `Forewarned.Core.Model.Bosses`); `static class BossList { static IReadOnlyList<BossModule> All }` (not named `Bosses`: that is its namespace).
  - Shared translation keys used by the engine: `announce.pull`, `announce.elsewhere`.

- [ ] **Step 1: Write the failing tests** — `tests/Forewarned.Tests/TranslationsTests.cs`

```csharp
using System.Linq;
using Forewarned.Core.Model;
using Xunit;

namespace Forewarned.Tests
{
    public class TranslationsTests
    {
        [Fact]
        public void ParsesKeysAndSkipsComments()
        {
            Translations t = Translations.Parse("# comment\nfader.spin.name = Spin\n\nbroken line\nempty =\n");
            Assert.Equal("Spin", t.Get("fader.spin.name"));
            Assert.Null(t.Get("empty"));
            Assert.Null(t.Get("missing"));
            Assert.Single(t.Keys);
        }

        [Fact]
        public void FallsBackToEnglish()
        {
            Translations english = Translations.Parse("a = A\nb = B");
            Translations german = Translations.Parse("a = Ä").WithFallback(english);
            Assert.Equal("Ä", german.Get("a"));
            Assert.Equal("B", german.Get("b"));
        }
    }
}
```

`tests/Forewarned.Tests/DataFilesTests.cs`:

```csharp
using System.IO;
using System.Linq;
using System.Reflection;
using Forewarned.Core.Model;
using Forewarned.Core.Model.Bosses;
using Xunit;

namespace Forewarned.Tests
{
    public class DataFilesTests
    {
        internal static Translations English()
        {
            using (Stream s = typeof(DataFilesTests).Assembly.GetManifestResourceStream("Forewarned.English.txt"))
            using (var r = new StreamReader(s))
                return Translations.Parse(r.ReadToEnd());
        }

        private static void Clean(Translations tr, string key)
        {
            string text = tr.Get(key);
            Assert.True(text != null, "missing key " + key);
            Assert.True(TextCheck.IsClean(text.Replace("{boss}", "B").Replace("{what}", "W").Replace("{m}", "1")), "unclean " + key + " = " + text);
        }

        [Fact]
        public void TheRegistryBuildsFromEveryModule()
        {
            var r = new ModuleRegistry(BossList.All);
            Assert.Equal(2, r.Modules.Count);
        }

        [Fact]
        public void EveryAbilityIsComplete()
        {
            foreach (BossModule m in BossList.All)
                foreach (AbilitySpec a in m.Abilities)
                {
                    Assert.False(string.IsNullOrEmpty(a.ItemPrefab), a.Id);
                    Assert.NotEmpty(a.Triggers);
                    Assert.StartsWith(m.Key + ".", a.Id);
                    Assert.True(a.WindUp > 0f, a.Id);
                    if (a.DefaultLevel != Level.None)
                        Assert.False(string.IsNullOrEmpty(a.ActionKey), a.Id);
                    if (a.DefaultLevel == Level.Danger)
                        Assert.True(a.Shape.Kind != ShapeKind.None, a.Id + " is Danger but has no shape");
                    if (a.DefaultLevel == Level.Caution)
                        Assert.False(a.DefaultOn, a.Id + ": Caution is off by default (PLAN.md §9)");
                }
        }

        [Fact]
        public void EveryKeyHasCleanEnglish()
        {
            Translations tr = English();
            Clean(tr, "announce.pull");
            Clean(tr, "announce.elsewhere");
            foreach (BossModule m in BossList.All)
            {
                foreach (AbilitySpec a in m.Abilities)
                {
                    Clean(tr, a.NameKey);
                    if (a.ActionKey != null)
                        Clean(tr, a.ActionKey);
                }
                foreach (Phase p in m.Phases)
                    Clean(tr, p.Key);
                foreach (string key in new[] { m.OnStateChanged("flying", true), m.OnStateChanged("flying", false) })
                    if (key != null)
                        Clean(tr, key);
            }
        }

        [Fact]
        public void FaderMatchesTheResearch()
        {
            AbilitySpec breath = BossList.All.OfType<FaderModule>().Single().Abilities.Single(a => a.Id == "fader.flamebreath");
            Assert.Equal(2.34f, breath.WindUp);
            Assert.Equal(25f, breath.Cooldown);
            Assert.Equal(0.05f, breath.HpMin);
            Assert.Equal(0.85f, breath.HpMax);
            Assert.Equal(new[] { 0.85f, 0.55f, 0.35f, 0.25f }, new FaderModule().Phases.Select(p => p.At).ToArray());
        }

        [Fact]
        public void ModerAnnouncesFlying()
        {
            var moder = new ModerModule();
            Assert.Equal("moder.takeoff", moder.OnStateChanged("flying", true));
            Assert.Equal("moder.land", moder.OnStateChanged("flying", false));
            Assert.Null(moder.OnStateChanged("other", true));
        }
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: build error, `'Translations' could not be found`.

- [ ] **Step 3: Write `Translations.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Forewarned.Core.Model
{
    /// <summary>Forewarned's own strings: "key = text" lines, '#' comments. Missing keys fall back to English.</summary>
    public sealed class Translations
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Translations _fallback;

        public static Translations Parse(string text)
        {
            var t = new Translations();
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (key.Length > 0 && value.Length > 0)
                    t._map[key] = value;
            }
            return t;
        }

        public Translations WithFallback(Translations fallback)
        {
            _fallback = fallback;
            return this;
        }

        public IEnumerable<string> Keys => _map.Keys;

        public string Get(string key)
        {
            if (key == null)
                return null;
            string value;
            if (_map.TryGetValue(key, out value))
                return value;
            return _fallback != null ? _fallback.Get(key) : null;
        }
    }
}
```

- [ ] **Step 4: Write `src/Forewarned/translations/English.txt`**

```
# Forewarned strings: "key = text", '#' comments. {boss} is the boss's localised name,
# {what} an ability name, {m} a distance in metres (rounded up from the attack's size).
# Titles are the ".name" strings in capitals.

announce.pull = {boss} engaged
announce.elsewhere = {boss}: {what}

action.get_behind = Get behind {boss}
action.keep_moving = Keep moving
action.leave_gap = Leave through the gap
action.back_out_m = Back out, {m} m
action.run_out_m = Run out, {m} m
action.leave_line = Get out of the line
action.parry_roll = Parry or roll
action.block_sidestep = Block or sidestep
action.parry_step_back = Parry or step back

fader.flamebreath.name = Flame breath
fader.fissure.name = Fissure
fader.walloffire.name = Wall of fire
fader.meteors.name = Meteors
fader.spin.name = Spin
fader.roar.name = Roar
fader.roar.action = Charred Warriors incoming
fader.bite.name = Bite
fader.claw.name = Claw
fader.phase.85 = {boss} 85%: Fissure and Flame breath
fader.phase.55 = {boss} 55%: Charred Warriors
fader.phase.35 = {boss} 35%: faster Fissure and adds
fader.phase.25 = {boss} 25%: faster Meteors

moder.breath.name = Cold breath
moder.icebarrage.name = Ice barrage
moder.bite.name = Bite
moder.claw.name = Claw
moder.scream.name = Scream
moder.takeoff = {boss} takes off
moder.land = {boss} lands
```

- [ ] **Step 5: Write `src/Forewarned/Core/Model/Bosses/FaderModule.cs`**

Numbers from PLAN.md §4 (Fader) and docs/research/data.md §7. Two items share a trigger where the game has "Intense" variants; the module names the base item and covers the union of their health gates.

```csharp
using System.Collections.Generic;

namespace Forewarned.Core.Model.Bosses
{
    /// <summary>Fader, Ashlands. PLAN.md §4 and §10.</summary>
    public sealed class FaderModule : BossModule
    {
        private static readonly string[] PrefabList = { "Fader" };

        private static readonly AbilitySpec[] AbilityList =
        {
            new AbilitySpec
            {
                Id = "fader.flamebreath", Name = "Flame breath", ItemPrefab = "Fader_Flamebreath",
                Triggers = new[] { "attack_flamebreath" }, DefaultLevel = Level.Danger,
                ActionKey = "action.get_behind", Response = Response.GetBehind,
                Shape = Shape.Line(39.45f, 3f, ShapeSource.Aoe),
                Cooldown = 25f, HpMin = 0.05f, HpMax = 0.85f, WindUp = 2.34f, AiRange = 20f, MaxAngle = 15f
            },
            new AbilitySpec
            {
                // Fader_Fissure_Intense (below 35%, every 20 s) shares the trigger.
                Id = "fader.fissure", Name = "Fissure", ItemPrefab = "Fader_Fissure",
                Triggers = new[] { "attack_Fissure" }, DefaultLevel = Level.Danger,
                ActionKey = "action.keep_moving", Response = Response.KeepMoving,
                Shape = Shape.Circle(11f, 0f, Anchor.Target, ShapeSource.SpawnAbility),
                Cooldown = 30f, HpMax = 0.85f, WindUp = 2.88f, AiRange = 40f, MaxAngle = 180f
            },
            new AbilitySpec
            {
                Id = "fader.walloffire", Name = "Wall of fire", ItemPrefab = "Fader_WallOfFire",
                Triggers = new[] { "attack_WallOfFire" }, DefaultLevel = Level.Danger,
                ActionKey = "action.leave_gap", Response = Response.ExitRing,
                Shape = Shape.Ring(8f, 4f, ShapeSource.SpawnAbility),
                Cooldown = 60f, HpMin = 0.15f, HpMax = 0.9f, WindUp = 1.43f, AiRange = 40f, MaxAngle = 180f
            },
            new AbilitySpec
            {
                // Fader_Meteors_Intense (below 25%, every 18 s) shares the trigger.
                Id = "fader.meteors", Name = "Meteors", ItemPrefab = "Fader_Meteors",
                Triggers = new[] { "taunt" }, DefaultLevel = Level.Danger,
                ActionKey = "action.keep_moving", Response = Response.KeepMoving,
                Shape = Shape.Circle(15f, 0f, Anchor.Target, ShapeSource.SpawnAbility),
                Cooldown = 25f, WindUp = 1.17f, AiRange = 30f, MaxAngle = 20f
            },
            new AbilitySpec
            {
                Id = "fader.spin", Name = "Spin", ItemPrefab = "Fader_Spin",
                Triggers = new[] { "attack_Spin" }, DefaultLevel = Level.Danger,
                ActionKey = "action.back_out_m", Response = Response.LeaveArea,
                Shape = Shape.Circle(8.5f, 0f, Anchor.Boss, ShapeSource.AttackSphere),
                Cooldown = 20f, WindUp = 1.3f, AiRange = 8f, MaxAngle = 360f
            },
            new AbilitySpec
            {
                // Fader_Roar_Intense (below 35%, every 26 s) shares the trigger.
                Id = "fader.roar", Name = "Roar", ItemPrefab = "Fader_Roar",
                Triggers = new[] { "attack_roar" }, DefaultLevel = Level.Info,
                ActionKey = "fader.roar.action", Response = Response.KillAdds,
                Cooldown = 45f, HpMax = 0.55f, WindUp = 1.53f, AiRange = 100f, MaxAngle = 45f
            },
            new AbilitySpec
            {
                Id = "fader.bite", Name = "Bite", ItemPrefab = "Fader_Bite",
                Triggers = new[] { "attack_bite" }, DefaultLevel = Level.Caution, DefaultOn = false,
                ActionKey = "action.parry_roll", Response = Response.Parry,
                Shape = Shape.Cone(10f, 40f), Cooldown = 3f, WindUp = 1.27f, AiRange = 9f, MaxAngle = 10f
            },
            new AbilitySpec
            {
                Id = "fader.claw", Name = "Claw", ItemPrefab = "Fader_Claw_Left",
                Triggers = new[] { "attack_ClawL", "attack_ClawR" }, DefaultLevel = Level.Caution, DefaultOn = false,
                ActionKey = "action.parry_roll", Response = Response.Parry,
                Shape = Shape.Cone(10f, 65f), Cooldown = 3f, WindUp = 1.24f, AiRange = 9f, MaxAngle = 10f
            }
        };

        private static readonly Phase[] PhaseList =
        {
            new Phase { At = 0.85f, Key = "fader.phase.85" },
            new Phase { At = 0.55f, Key = "fader.phase.55" },
            new Phase { At = 0.35f, Key = "fader.phase.35" },
            new Phase { At = 0.25f, Key = "fader.phase.25" }
        };

        public override string Key => "fader";
        public override string DisplayName => "Fader";
        public override string NameToken => "$enemy_fader";
        public override int Order => 9;
        public override string[] Prefabs => PrefabList;
        public override IReadOnlyList<AbilitySpec> Abilities => AbilityList;
        public override IReadOnlyList<Phase> Phases => PhaseList;
    }
}
```

- [ ] **Step 6: Write `src/Forewarned/Core/Model/Bosses/ModerModule.cs`**

```csharp
using System.Collections.Generic;

namespace Forewarned.Core.Model.Bosses
{
    /// <summary>Moder (prefab "Dragon"), Mountains. PLAN.md §4 and §10.</summary>
    public sealed class ModerModule : BossModule
    {
        private static readonly string[] PrefabList = { "Dragon" };

        private static readonly AbilitySpec[] AbilityList =
        {
            new AbilitySpec
            {
                Id = "moder.breath", Name = "Cold breath", ItemPrefab = "dragon_coldbreath",
                Triggers = new[] { "attack_breath" }, DefaultLevel = Level.Danger,
                ActionKey = "action.leave_line", Response = Response.LeaveLine,
                Shape = Shape.Cone(30f, 10f), Cooldown = 8f, WindUp = 1.37f, AiRange = 20f, MaxAngle = 5f
            },
            new AbilitySpec
            {
                // Flying only. The ice shards spread around whoever she aims at; 5 m is a fixed estimate.
                Id = "moder.icebarrage", Name = "Ice barrage", ItemPrefab = "dragon_spit_shotgun",
                Triggers = new[] { "attack_iceball" }, DefaultLevel = Level.Danger,
                ActionKey = "action.keep_moving", Response = Response.KeepMoving,
                Shape = Shape.Circle(5f, 0f, Anchor.Target, ShapeSource.Fixed),
                Cooldown = 8f, WindUp = 0.89f, AiRange = 25f, MaxAngle = 5f
            },
            new AbilitySpec
            {
                Id = "moder.bite", Name = "Bite", ItemPrefab = "dragon_bite",
                Triggers = new[] { "attack_bite" }, DefaultLevel = Level.Caution, DefaultOn = false,
                ActionKey = "action.block_sidestep", Response = Response.Parry,
                Shape = Shape.Cone(8f, 20f), Cooldown = 30f, WindUp = 0.99f, AiRange = 7f, MaxAngle = 10f
            },
            new AbilitySpec
            {
                Id = "moder.claw", Name = "Claw", ItemPrefab = "dragon_claw_left",
                Triggers = new[] { "attack_claw_left", "attack_claw_right" }, DefaultLevel = Level.Caution, DefaultOn = false,
                ActionKey = "action.parry_step_back", Response = Response.Parry,
                Shape = Shape.Cone(12f, 50f), Cooldown = 30f, WindUp = 1.57f, AiRange = 10f, MaxAngle = 30f
            },
            new AbilitySpec
            {
                // No hit: mapped so it isn't reported as unknown, but never warned.
                Id = "moder.scream", Name = "Scream", ItemPrefab = "dragon_taunt",
                Triggers = new[] { "attack_taunt" }, DefaultLevel = Level.None, DefaultOn = false,
                Cooldown = 30f, WindUp = 1.96f, AiRange = 50f, MaxAngle = 5f
            }
        };

        public override string Key => "moder";
        public override string DisplayName => "Moder";
        public override string NameToken => "$enemy_dragon";
        public override int Order => 6;
        public override string[] Prefabs => PrefabList;
        public override IReadOnlyList<AbilitySpec> Abilities => AbilityList;

        public override string OnStateChanged(string state, bool value)
        {
            if (state != "flying")
                return null;
            return value ? "moder.takeoff" : "moder.land";
        }
    }
}
```

- [ ] **Step 7: Write `src/Forewarned/Core/Model/Bosses/BossList.cs`**

```csharp
using System.Collections.Generic;

namespace Forewarned.Core.Model.Bosses
{
    /// <summary>Every boss module, in game order. Milestone 2 adds the other six.</summary>
    public static class BossList
    {
        public static readonly IReadOnlyList<BossModule> All = new BossModule[]
        {
            new ModerModule(),
            new FaderModule()
        };
    }
}
```

- [ ] **Step 8: Run the tests**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: `Passed: 84`, 0 failed.

- [ ] **Step 9: Commit and push**

```bash
git add src tests
git commit -m "Model: English strings and the Fader and Moder modules"
git push
```

---

### Task 11: Settings and the Engine façade

**Files:**
- Create: `src/Forewarned/Core/Model/Settings.cs`, `src/Forewarned/Core/Model/Engine.cs`
- Test: `tests/Forewarned.Tests/EngineTests.cs`

**Interfaces:**
- Consumes: everything above; `DataFilesTests.English()` (internal test helper from Task 10).
- Produces:
  - `interface IAbilitySettings`: `bool Enabled { get; }`, `bool OnlyDuringBossFight { get; }`, `bool BossEnabled(BossModule m)`, `bool Announces(BossModule m)`, `bool Warn(AbilitySpec a)`, `bool Sound(AbilitySpec a)`, `bool Visual(AbilitySpec a)`, `Level LevelOf(AbilitySpec a)`, `bool AlwaysWarn(AbilitySpec a)`.
  - `sealed class DefaultSettings : IAbilitySettings`, with statics `bool DefaultSound(AbilitySpec a)` (Danger or Caution) and `bool DefaultVisual(AbilitySpec a)` (Danger), which plan 2's config binding reuses for its defaults.
  - `sealed class TriggerEvent { long BossId; string Prefab; string Trigger; float Time; }`, `sealed class TriggerRecord { float Time; string Prefab; string Trigger; string Outcome; }`.
  - `sealed class Engine(ModuleRegistry registry, Translations tr, Func<string,string> localize, IAbilitySettings settings)`: fields `Registry, Tracker, Board, Announcer, Phases`; `IReadOnlyList<TriggerRecord> Recent` (newest first, max 20); `TriggerKind OnTrigger(TriggerEvent e, Scene scene, AbilityNumbers live, bool fightActive)`; `void OnHit(long bossId, float time)`; `void OnHealth(long bossId, string prefab, float hp, float now)`; `void OnState(long bossId, string prefab, string state, bool value, float now)`; `void OnPull(string prefab, float now)`; `void Forget(long bossId)`; `void Tick(float now)`; `void Clear()`; `string BossName(BossModule m)`.

- [ ] **Step 1: Write the failing tests** — `tests/Forewarned.Tests/EngineTests.cs`

```csharp
using System.Collections.Generic;
using Forewarned.Core.Model;
using Forewarned.Core.Model.Bosses;
using Xunit;

namespace Forewarned.Tests
{
    public class EngineTests
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "$enemy_fader", "Fader" }, { "$enemy_dragon", "Moder" }
        };

        private sealed class Settings : IAbilitySettings
        {
            private readonly DefaultSettings _d = new DefaultSettings();
            public HashSet<string> On = new HashSet<string>();
            public bool Enabled => true;
            public bool OnlyDuringBossFight => true;
            public bool BossEnabled(BossModule m) => true;
            public bool Announces(BossModule m) => true;
            public bool Warn(AbilitySpec a) => On.Contains(a.Id) || _d.Warn(a);
            public bool Sound(AbilitySpec a) => _d.Sound(a);
            public bool Visual(AbilitySpec a) => _d.Visual(a);
            public Level LevelOf(AbilitySpec a) => _d.LevelOf(a);
            public bool AlwaysWarn(AbilitySpec a) => false;
        }

        private static Engine Make(Settings s = null) =>
            new Engine(new ModuleRegistry(BossList.All), DataFilesTests.English(), t => Names.TryGetValue(t, out var n) ? n : t, s ?? new Settings());

        private static Scene At(float x, float z) =>
            new Scene { BossPos = new Vec2(0f, 0f), BossFacing = new Vec2(0f, 1f), Me = new Vec2(x, z) };

        private static TriggerEvent Fader(string trigger, float time) =>
            new TriggerEvent { BossId = 7, Prefab = "Fader", Trigger = trigger, Time = time };

        [Fact]
        public void InFrontOfTheFlameBreathYouGetTheSpecialWarning()
        {
            Engine e = Make();
            Assert.Equal(TriggerKind.Mapped, e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, 10f), null, true));
            Warning w = Assert.Single(e.Board.Active);
            Assert.Equal("FLAME BREATH", w.Title);
            Assert.Equal("Get behind Fader", w.Action);
            Assert.Equal(Level.Danger, w.Level);
            Assert.Equal(12.34f, w.HitAt, 3);
            Assert.True(w.Sound);
            Assert.True(w.Visual);
            Assert.True(w.PlaySound);
            Assert.Equal(Response.GetBehind, w.Response);
            Assert.StartsWith("special added", e.Recent[0].Outcome);
        }

        [Fact]
        public void BehindTheBossItIsOnlyAnAnnounce()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, -10f), null, true);
            Assert.Empty(e.Board.Active);
            Assert.Equal("Fader: Flame breath", Assert.Single(e.Announcer.Lines).Text);
        }

        [Fact]
        public void NothingOutsideABossFight()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, 10f), null, false);
            Assert.Empty(e.Board.Active);
            Assert.Equal("no fight", e.Recent[0].Outcome);
        }

        [Fact]
        public void CautionIsOffByDefaultButCanBeTurnedOn()
        {
            Engine off = Make();
            off.OnTrigger(Fader("attack_bite", 10f), At(0f, 5f), null, true);
            Assert.Empty(off.Board.Active);
            Assert.Equal("off", off.Recent[0].Outcome);

            var s = new Settings();
            s.On.Add("fader.bite");
            Engine on = Make(s);
            on.OnTrigger(Fader("attack_bite", 10f), At(0f, 5f), null, true);
            Assert.Equal("BITE", Assert.Single(on.Board.Active).Title);
        }

        [Fact]
        public void InfoAbilitiesAnnounceTheirAction()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_roar", 10f), At(0f, 50f), null, true);
            Assert.Equal("Charred Warriors incoming", Assert.Single(e.Announcer.Lines).Text);
        }

        [Fact]
        public void DistancesComeFromTheShape()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_Spin", 10f), At(0f, 5f), null, true);
            Assert.Equal("Back out, 9 m", Assert.Single(e.Board.Active).Action);
            Engine live = Make();
            live.OnTrigger(Fader("attack_Spin", 10f), At(0f, 5f), new AbilityNumbers { Radius = 10f }, true);
            Assert.Equal("Back out, 10 m", Assert.Single(live.Board.Active).Action);
        }

        [Fact]
        public void UnknownTriggersAreRecordedAndOthersIgnored()
        {
            Engine e = Make();
            Assert.Equal(TriggerKind.Unmapped, e.OnTrigger(Fader("jump_forward", 10f), At(0f, 5f), null, true));
            Assert.Equal("unmapped", e.Recent[0].Outcome);
            Assert.Equal(TriggerKind.Ignored, e.OnTrigger(Fader("attack_abort", 11f), At(0f, 5f), null, true));
            Assert.Equal(TriggerKind.NotTracked, e.OnTrigger(new TriggerEvent { BossId = 9, Prefab = "Greydwarf", Trigger = "attack", Time = 12f }, At(0f, 5f), null, true));
            Assert.Equal(2, e.Recent.Count);
        }

        [Fact]
        public void TheRealHitEndsTheWarningAndTeachesTheNextOne()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, 10f), null, true);
            e.OnHit(7, 12f);
            Assert.Equal(12f, e.Board.Active[0].HitAt, 3);
            Assert.True(e.Board.Active[0].HitSeen);
            e.Board.Clear();
            e.OnTrigger(Fader("attack_flamebreath", 40f), At(0f, 10f), null, true);
            Assert.Equal(42f, e.Board.Active[0].HitAt, 3);
        }

        [Fact]
        public void PhasesAreAnnounced()
        {
            Engine e = Make();
            e.OnHealth(7, "Fader", 0.9f, 1f);
            Assert.Empty(e.Announcer.Lines);
            e.OnHealth(7, "Fader", 0.84f, 2f);
            Assert.Equal("Fader 85%: Fissure and Flame breath", Assert.Single(e.Announcer.Lines).Text);
            Assert.Equal(0.84f, e.Tracker.Health(7));
        }

        [Fact]
        public void ModerAnnouncesTakeoffAndThePull()
        {
            Engine e = Make();
            e.OnPull("Dragon", 1f);
            e.OnState(3, "Dragon", "flying", true, 2f);
            Assert.Equal("Moder engaged", e.Announcer.Lines[0].Text);
            Assert.Equal("Moder takes off", e.Announcer.Lines[1].Text);
        }

        [Fact]
        public void LevelNoneIsSilent()
        {
            Engine e = Make();
            e.OnTrigger(new TriggerEvent { BossId = 3, Prefab = "Dragon", Trigger = "attack_taunt", Time = 1f }, At(0f, 5f), null, true);
            Assert.Empty(e.Board.Active);
            Assert.Empty(e.Announcer.Lines);
            Assert.Equal("none", e.Recent[0].Outcome);
        }

        [Fact]
        public void RecentKeepsTheNewestTwenty()
        {
            Engine e = Make();
            for (int i = 0; i < 25; i++)
                e.OnTrigger(Fader("jump_forward", i), At(0f, 5f), null, true);
            Assert.Equal(Engine.RecentSize, e.Recent.Count);
            Assert.Equal(24f, e.Recent[0].Time);
        }

        [Fact]
        public void ForgetAndClear()
        {
            Engine e = Make();
            e.OnTrigger(Fader("attack_flamebreath", 10f), At(0f, 10f), null, true);
            e.OnHealth(7, "Fader", 0.5f, 10f);
            e.Forget(7);
            Assert.Null(e.Tracker.Health(7));
            e.Clear();
            Assert.Empty(e.Board.Active);
            Assert.Empty(e.Recent);
        }
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: build error, `'IAbilitySettings' could not be found`.

- [ ] **Step 3: Write `Settings.cs`**

```csharp
namespace Forewarned.Core.Model
{
    /// <summary>The player's choices, as the model sees them. The plugin implements this over its
    /// BepInEx config (plan 2); tests use DefaultSettings.</summary>
    public interface IAbilitySettings
    {
        bool Enabled { get; }
        bool OnlyDuringBossFight { get; }
        bool BossEnabled(BossModule m);
        /// <summary>Pull and phase announces for this boss.</summary>
        bool Announces(BossModule m);
        bool Warn(AbilitySpec a);
        bool Sound(AbilitySpec a);
        bool Visual(AbilitySpec a);
        Level LevelOf(AbilitySpec a);
        bool AlwaysWarn(AbilitySpec a);
    }

    /// <summary>The out-of-the-box choices (PLAN.md §9): the spec's level and on/off; sound for Danger
    /// and Caution; ground visuals for Danger only.</summary>
    public sealed class DefaultSettings : IAbilitySettings
    {
        public static bool DefaultSound(AbilitySpec a) => a.DefaultLevel == Level.Danger || a.DefaultLevel == Level.Caution;
        public static bool DefaultVisual(AbilitySpec a) => a.DefaultLevel == Level.Danger;

        public bool Enabled => true;
        public bool OnlyDuringBossFight => true;
        public bool BossEnabled(BossModule m) => true;
        public bool Announces(BossModule m) => true;
        public bool Warn(AbilitySpec a) => a.DefaultOn;
        public bool Sound(AbilitySpec a) => DefaultSound(a);
        public bool Visual(AbilitySpec a) => DefaultVisual(a);
        public Level LevelOf(AbilitySpec a) => a.DefaultLevel;
        public bool AlwaysWarn(AbilitySpec a) => false;
    }
}
```

- [ ] **Step 4: Write `Engine.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Forewarned.Core.Model
{
    public sealed class TriggerEvent
    {
        public long BossId;
        public string Prefab;
        public string Trigger;
        public float Time;
    }

    /// <summary>One line of the console's trigger history.</summary>
    public sealed class TriggerRecord
    {
        public float Time;
        public string Prefab;
        public string Trigger;
        public string Outcome;
    }

    /// <summary>
    /// PLAN.md §11.1: the pure core. Game adapters feed it triggers, hits, health and state; it fills
    /// the warning board and the announcer, which the HUD draws. No Unity and no game types.
    /// </summary>
    public sealed class Engine
    {
        public const int RecentSize = 20;

        public readonly ModuleRegistry Registry;
        public readonly Tracker Tracker = new Tracker();
        public readonly WarningBoard Board = new WarningBoard();
        public readonly Announcer Announcer = new Announcer();
        public readonly PhaseTracker Phases = new PhaseTracker();

        private readonly Translations _tr;
        private readonly Func<string, string> _localize;
        private readonly IAbilitySettings _settings;
        private readonly List<TriggerRecord> _recent = new List<TriggerRecord>();

        /// <param name="localize">Turns a game token ("$enemy_fader") into the player's language.</param>
        public Engine(ModuleRegistry registry, Translations tr, Func<string, string> localize, IAbilitySettings settings)
        {
            Registry = registry;
            _tr = tr ?? new Translations();
            _localize = localize;
            _settings = settings;
        }

        /// <summary>Newest first.</summary>
        public IReadOnlyList<TriggerRecord> Recent => _recent;

        public TriggerKind OnTrigger(TriggerEvent e, Scene scene, AbilityNumbers live, bool fightActive)
        {
            AbilitySpec spec;
            TriggerKind kind = Registry.Classify(e.Prefab, e.Trigger, out spec);
            if (kind == TriggerKind.NotTracked)
                return kind;
            if (kind != TriggerKind.Mapped)
            {
                Record(e, kind == TriggerKind.Ignored ? "ignored" : "unmapped");
                return kind;
            }
            Tracker.OnTrigger(e.BossId, e.Prefab, e.Trigger, spec.Id, e.Time);
            Record(e, Warn(e, Registry.ModuleFor(e.Prefab), spec, scene, live, fightActive));
            return kind;
        }

        private string Warn(TriggerEvent e, BossModule module, AbilitySpec spec, Scene scene, AbilityNumbers live, bool fightActive)
        {
            if (!_settings.Enabled || !_settings.BossEnabled(module))
                return "off";
            if (_settings.OnlyDuringBossFight && !fightActive)
                return "no fight";
            Level level = _settings.LevelOf(spec);
            if (level == Level.None)
                return "none";
            if (!_settings.Warn(spec))
                return "off";

            ResolvedAbility a = ResolvedAbility.Resolve(spec, live, Tracker.LearnedWindUp(e.Prefab, e.Trigger));
            Vec2 origin;
            Verdict verdict = Relevance.Judge(a.Shape, a.AiRange, scene, out origin);
            Outcome outcome = Relevance.Decide(level, verdict, _settings.AlwaysWarn(spec));
            string where = verdict.ToString().ToLowerInvariant();
            string boss = BossName(module);

            if (outcome == Outcome.Nothing)
                return "nothing, " + where;
            if (outcome == Outcome.Announce)
            {
                string text = level == Level.Info
                    ? Fill(_tr.Get(spec.ActionKey), boss, null, a.Shape.SafeMetres)
                    : Fill(_tr.Get("announce.elsewhere"), boss, AbilityName(spec), 0);
                Announcer.Add(spec.Id, text, e.Time);
                return "announce, " + where;
            }

            var w = new Warning
            {
                AbilityId = spec.Id,
                BossId = e.BossId,
                Level = level,
                Title = AbilityName(spec).ToUpperInvariant(),
                Action = Fill(_tr.Get(spec.ActionKey), boss, null, a.Shape.SafeMetres),
                Start = e.Time,
                HitAt = e.Time + a.WindUp,
                Sound = _settings.Sound(spec),
                Visual = _settings.Visual(spec),
                Shape = a.Shape,
                Response = spec.Response,
                Origin = origin
            };
            return "special " + Board.Offer(w, e.Time).ToString().ToLowerInvariant() + ", " + where;
        }

        public void OnHit(long bossId, float time)
        {
            string abilityId = Tracker.OnHit(bossId, time);
            if (abilityId != null)
                Board.Hit(abilityId, bossId, time);
        }

        public void OnHealth(long bossId, string prefab, float hp, float now)
        {
            BossModule m = Registry.ModuleFor(prefab);
            if (m == null)
                return;
            Tracker.SetHealth(bossId, hp);
            IList<Phase> crossed = Phases.Observe(bossId, m.Phases, hp);
            if (!AnnouncesFor(m))
                return;
            foreach (Phase p in crossed)
                Announcer.Add(p.Key, Fill(_tr.Get(p.Key), BossName(m), null, 0), now);
        }

        public void OnState(long bossId, string prefab, string state, bool value, float now)
        {
            BossModule m = Registry.ModuleFor(prefab);
            string key = m != null ? m.OnStateChanged(state, value) : null;
            if (key != null && AnnouncesFor(m))
                Announcer.Add(key, Fill(_tr.Get(key), BossName(m), null, 0), now);
        }

        public void OnPull(string prefab, float now)
        {
            BossModule m = Registry.ModuleFor(prefab);
            if (m != null && AnnouncesFor(m))
                Announcer.Add("pull." + m.Key, Fill(_tr.Get("announce.pull"), BossName(m), null, 0), now);
        }

        public void Forget(long bossId)
        {
            Tracker.Forget(bossId);
            Phases.Forget(bossId);
        }

        public void Tick(float now)
        {
            Board.Tick(now);
            Announcer.Tick(now);
        }

        public void Clear()
        {
            Tracker.Clear();
            Board.Clear();
            Announcer.Clear();
            Phases.Clear();
            _recent.Clear();
        }

        /// <summary>The boss's name in the player's language, or the module's English name if the game has none.</summary>
        public string BossName(BossModule m)
        {
            string name = _localize != null ? _localize(m.NameToken) : null;
            return TextCheck.IsClean(name) ? name : m.DisplayName;
        }

        private bool AnnouncesFor(BossModule m) => _settings.Enabled && _settings.BossEnabled(m) && _settings.Announces(m);

        private string AbilityName(AbilitySpec spec)
        {
            string name = _tr.Get(spec.NameKey);
            return TextCheck.IsClean(name) ? name : spec.Name;
        }

        private static string Fill(string template, string boss, string what, int metres)
        {
            if (template == null)
                return "";
            return template
                .Replace("{boss}", boss ?? "")
                .Replace("{what}", what ?? "")
                .Replace("{m}", metres.ToString(CultureInfo.InvariantCulture));
        }

        private void Record(TriggerEvent e, string outcome)
        {
            _recent.Insert(0, new TriggerRecord { Time = e.Time, Prefab = e.Prefab, Trigger = e.Trigger, Outcome = outcome });
            if (_recent.Count > RecentSize)
                _recent.RemoveAt(_recent.Count - 1);
        }
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test tests/Forewarned.Tests --nologo -v minimal`
Expected: `Passed: 97`, 0 failed.

- [ ] **Step 6: Build the plugin too** (the model must still compile on net472)

Run: `dotnet build src/Forewarned/Forewarned.csproj -c Release --nologo -v minimal`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 7: Commit and push**

```bash
git add src tests
git commit -m "Model: settings and the engine façade"
git push
```

---

### Task 12: Close out plan 1

**Files:**
- Modify: `PLAN.md` (§9 decisions: note plan 1 done), `CHANGELOG.md`

- [ ] **Step 1: Full verification**

```bash
dotnet build Forewarned.sln -c Release --nologo -v minimal
dotnet test tests/Forewarned.Tests --nologo -v minimal
grep -rn "UnityEngine\|BepInEx\|HarmonyLib" src/Forewarned/Core/Model   # must print nothing
```
Expected: build succeeded, `Passed: 97`, grep empty.

- [ ] **Step 2: Note progress in PLAN.md**

Append to the end of PLAN.md §9's milestone bullet list (after "7. Probes settled: …"):

```markdown
- **Progress:** milestone 1 plan 1 (scaffold and pure model, `docs/superpowers/plans/2026-10-05-m1-plan1-scaffold-and-model.md`) is done: 97 model tests green. Next: plan 2 (game-side capture, live data, config, console).
```

- [ ] **Step 3: Commit and push**

```bash
git add PLAN.md
git commit -m "PLAN: milestone 1 plan 1 done"
git push
```
