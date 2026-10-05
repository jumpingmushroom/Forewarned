# Forewarned — notes for Claude

Boss attack warnings for Valheim in the style of Deadly Boss Mods. Research, decisions and design:
`PLAN.md` (§9 decisions, §10 ability table, §11 design). Detailed research: `docs/research/`.

## Commits

- **Never add AI attribution** (`Co-Authored-By`, "Generated with", footers) to commits, PRs, the
  README, the changelog or release notes. The user is the only author.
- Commit and push after every change.
- Version bumps touch three places together: `PluginVersion` in `src/Forewarned/Plugin.cs`,
  `<Version>` in the csproj, and `version_number` in `thunderstore/manifest.json`.
  `build/package.sh` refuses to package if `Plugin.cs` and `manifest.json` disagree.
- Never commit the rig's address, user or paths. Write `<rig>` in tracked files; the real target
  lives only in the gitignored `build/deploy.sh`, `logs.sh`, `shot.sh`, `crop.sh`.

## Building and testing

- `dotnet test tests/Forewarned.Tests` runs the model tests (pure C#, net8.0). Everything under
  `src/Forewarned/Core/Model/` (including `Bosses/`) must stay free of UnityEngine and game types.
- The build box's dotnet SDK needs `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`; the scripts set it.
  `ilspycmd` also needs `DOTNET_ROOT=$HOME/.dotnet`.
- Reference assemblies live in `lib/` (gitignored), pulled from the rig's `valheim_Data/Managed`.
  Decompiled game code is in `decomp/` (gitignored). No Jotunn.
- `./build/deploy.sh` builds Release and swaps the DLL atomically into the r2modman **Default**
  profile on the rig over SSH. A running game keeps the old DLL until relaunch.
- The rig's login shell is fish: wrap anything non-trivial in `bash -c '...'`. Brace expansion in
  scp paths fails there; use `tar` over ssh.
- `./build/logs.sh` shows Forewarned lines from the rig's BepInEx log. `./build/shot.sh <name>`
  captures the game window into `docs/images/`.
- In game: devcommands (`spawn Fader`, `god`, `ghost`) and the `forewarned` console command
  (tracked bosses, recent triggers and verdicts, live vs offline numbers).
- **Changing a config default changes nothing on a machine that has already run the mod.** Delete
  `BepInEx/config/com.jumpingmushroom.forewarned.cfg` in the rig's profile after changing one.

## Game data

- Warnings are driven by the animator trigger (`ZSyncAnimation.RPC_SetTrigger`), keyed on (boss
  prefab, trigger name). It reaches every client; `StartAttack` and attack effects do not.
- Offline data: `tools/` (Python in a gitignored `.venv` with `UnityPy` and
  `TypeTreeGeneratorAPI`) reads the bundles and `resources.assets` copied into the gitignored
  `gamedata/`. `tools/dump_boss_attacks.py` → `gamedata/boss_dump.json`; `boss_report.py` makes
  the tables in `docs/research/data.md`. Recreate the venv with
  `python3 -m venv .venv && .venv/bin/pip install UnityPy==1.25.4 TypeTreeGeneratorAPI==0.0.10`.
- After a game update: re-copy `lib/` and the bundles, re-run the dump, and check the
  `LogUnmappedTriggers` output for renamed animations.

## Releasing

Nothing is released until every boss works solo (0.1.0); 0.2.0 adds multiplayer.
`./build/package.sh` → `dist/Forewarned-X.Y.Z.zip`. Tag `vX.Y.Z`, push, `gh release create`, then
copy the zip to `~/Downloads` on the rig (`scp dist/Forewarned-X.Y.Z.zip <rig>:Downloads/`). The
user uploads it to Thunderstore themselves.
