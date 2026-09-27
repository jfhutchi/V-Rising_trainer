# Hutch V Rising Trainer

An in-game trainer for Windows / Steam private, locally hosted PvE worlds. Press **F6** to open the draggable trainer window. The Trainer tab sends live commands to V Rising's locally hosted server and can save travel and servant conversion settings in the active world's `ServerGameSettings.json`. The Achievements tab tracks all 49 achievements on the signed-in Steam account, with an optional profile override.

This build targets V Rising `v1.1.13.0-r99712-b17` and BepInEx 6 IL2CPP. The trainer uses [KindredCommands](https://thunderstore.io/c/v-rising/p/odjit/KindredCommands/) for the server-side actions, [KindredSchematics](https://thunderstore.io/c/v-rising/p/odjit/KindredSchematics/) for free building, and [ServerLaunchFix](https://thunderstore.io/c/v-rising/p/Mythic/ServerLaunchFix/) so those actions work in a locally hosted world.

## Download and install

Download the prebuilt ZIP from [GitHub Releases](https://github.com/jfhutchi/V-Rising_trainer/releases). Follow the [public installation guide](packaging/INSTALL.md) for dependencies, manual installation, configuration, upgrades and removal. The binary download does not require the .NET SDK. Remote and dedicated servers are not supported by the trainer's command bridge.

## Build from source

Requirements: V Rising with BepInEx 6 IL2CPP and generated `BepInEx/interop` assemblies, PowerShell 7, and .NET SDK 8 or newer. Close V Rising before installing.

```powershell
./Build.ps1 -Install
./tools/Install-BloodAssist.ps1 -Install
./tools/Install-QualityOfLife.ps1 -Install -StackMultiplier 10 -MachineSpeedMultiplier 10
```

`Build.ps1` installs `HutchVRisingTrainer.dll` in `BepInEx/plugins/HutchVRisingTrainer`. The helper installer downloads pinned versions of ServerLaunchFix, VampireCommandFramework, KindredCommands, and KindredSchematics, verifies the SHA-256 of each package, and installs their DLLs in `BepInEx/plugins/HutchVRisingServerHelpers`. If the first three helpers are already installed by the previous version of this script, `-Install` verifies them and adds KindredSchematics. It leaves the existing BepInEx runtime alone. Set `-GameDir` on either script if the game is installed somewhere other than `D:\SteamLibrary\steamapps\common\VRising`.

The optional quality-of-life installer adds pinned [VStacks](https://thunderstore.io/c/v-rising/p/AETeam/VStacks/) and [VRates](https://thunderstore.io/c/v-rising/p/AETeam/VRates/) DLLs to the shared client/server plugin directory. It writes matching stack multipliers to the client and local server configs, plus crafting and refinement speed multipliers to both VRates configs. All other VRates multipliers are set to 1. These are startup settings; close and relaunch V Rising to apply a change. The installer does not edit a world save or `ServerGameSettings.json`.

To change those multipliers after closing the game:

```powershell
./tools/Install-QualityOfLife.ps1 -Configure -StackMultiplier 100 -MachineSpeedMultiplier 20
```

The two stack DLLs must remain installed on both the local server and client while using enlarged stacks. Before removing them, split or store stacks above 4,095 items so vanilla inventory counts can represent them. `-Remove` removes the DLLs and keeps the config files.

To remove the server helpers after closing the game:

```powershell
./tools/Install-BloodAssist.ps1 -Remove
```

## Use in game

1. Create or load a **private PvE world that started on Brutal**. Keep its difficulty set to Brutal for the entire run.
2. Enable the console in V Rising's options. In the world, open the console and run `adminauth`. If authentication is denied, follow [Stunlock's administrator instructions](https://github.com/StunlockStudios/vrising-dedicated-server-instructions/blob/master/1.1.x-pc/INSTRUCTIONS.md) to add your Steam ID to that server's admin list.
3. Press **F6**. Wait until the Trainer tab says its local command link is ready, then use the buttons. Read the server's chat reply after each command.

Live controls include a boss-safe god mode and a clear-boosts button, invulnerability, no blood drain, damage boost, no cooldown, sun protection, gear repair, an item browser with grants, and buttons to spawn 99% and 100% humans. The God button runs the upstream `.god` preset, then turns off No Aggro, Immaterial, and Shrouded. The upstream No Aggro flag makes V Bloods disengage and refill their health; the trainer removes it so bosses keep targeting you. The individual boost buttons are toggles. **God mode ON** already includes No Cooldown; clicking **Toggle no cooldown** after God mode turns it off. Read the chat reply or use **Show boost state** to check.

By default, the trainer sends `.mortal` once after joining a local world to clear character boosts that survived an exit or crash. This requires admin rights; the chat reply confirms it. You can turn it off with `Safety.ClearBoostsOnJoin` in the BepInEx config. Saving and exiting cannot reliably clear boosts after a crash, so the next join is the recovery point. World travel, servant speed, and quality-of-life multipliers remain as saved.

On the Travel page, **Allow travel with all items** changes `TeleportBoundItems` to `false` in the active private world's `ServerGameSettings.json`. Leave and reload that world to apply it. The trainer saves the original settings once as `ServerGameSettings.json.hutch-backup` beside the world file. The button can restore the inventory restriction later. This applies only to the active local world and does not change bat travel or unlock undiscovered waygates.

On the Servants page, select **Save normal 1x**, **Save faster 3x**, or **Save fastest 6x** for the active private world's `ServantConvertRateModifier`. The page shows the active server rate and the saved rate separately. The saved setting takes effect after leaving and reloading the world; at 6x, the game's 20-minute base conversion takes about 3 minutes 20 seconds. The same settings backup protects the original world file. To finish one conversion immediately, close the F6 menu, aim directly at a coffin that is already converting a servant, and press **F7**. This sends KindredCommands' `.servant convert` command while the aim remains on the coffin and requires local server admin authentication.

For V Blood navigation, use the game's V Blood menu to select and track a target. Its direction and distance update as the boss moves; the Guide tab now points to this flow. The trainer does not add persistent pins for every V Blood because static pins would become inaccurate for roaming targets.

The gruel button makes Irradiant Gruel add exactly 1% with no mutation. Spawn a 100% human to feed from and another to charm; charm and imprison a 99% human before giving it gruel. These actions still happen in game and can trigger the corresponding achievements.

V Rising 1.1 already lets **Dominate** charm almost any ordinary unit with an active blood type, including many nonhumans. Activate the Dominate vampire power and channel its spell on the target; the spell lowers the target's health and takes control below 30%. Take the charmed unit to an empty Prison Cell, or use a Dusk Caller to send it there. Servant Coffins still accept only humans and some draculin units. V Blood targets are excluded. If the trainer's damage boost kills a target before it can be charmed, remove the boost first. The 100% and 99% human buttons are specifically for the listed achievements, not a restriction on Dominate.

On the Build page, **Toggle free costs** sends `.build free`. The server chat confirms whether it is enabled or disabled. When enabled, building, crafting, research, and repairs cost no materials for everyone on the private local server. Recipes and objects still require their normal unlocks, and placement and Castle Heart rules still apply. The trainer intentionally does not use `.build restrictions`, which disables respawns while active.

On the Items page, click **Search items**, type a word such as `sword`, and choose a result from the on-screen list. The list filters immediately and shows six results per page. Click **Amount** to type a quantity, then **Give selected**. The trainer sends the selected item's numeric prefab ID to the local server, so you do not need to know a prefab name. Press Enter to stop editing. The Achievements page uses the same click-to-type control for its search. Giving research materials speeds up station completion; you still unlock each technology and interact with each fully researched station.

The bundled browser contains 1,107 `Item_` prefab IDs from the [KindredCommands prefab catalog](https://github.com/Odjit/KindredCommands/blob/d956d65/Data/Prefabs.cs), for the V Rising 1.1 game family. Some internal or template prefabs may not be grantable; the server's chat reply is authoritative. The on-screen search does not send a command. Only **Give selected** sends `.give`.

The commands are only sent while a `VRisingServer.exe` process is running locally and the client character is loaded. They require admin rights in that world. There is no button that marks Steam achievements complete or skips V Blood progression. Fight and drink the blood of every V Blood for Master of All; use the five Brutal milestones on the same world. By default, the checklist detects the signed-in Steam account and reads its local achievement cache. The menu shows the cache's last-saved time. Public profile lookup is used when the local cache is unavailable or an explicit different Steam ID is configured. Change the profile, F6 menu key, or F7 conversion key in `BepInEx/config/hutch.vrising.achievementtrainer.cfg` after the first launch. Leave `Achievements.SteamId64` blank for automatic detection. Existing configuration is preserved on upgrade; clear an old profile override to enable automatic detection.

## Verification and limits

The project builds against the installed game's generated interop DLLs. The package download check, isolated helper install/upgrade/remove test, isolated quality-of-life install/configure/remove/reinstall test, and Steam catalog comparison pass. The F6 menu renders in the private world; **Show boost state**, **God mode ON**, and **Mortal / clear boosts** returned the expected results in earlier live tests. With God mode active, manually removing No Aggro let the player defeat a V Blood without the reported health reset. The updated button's automatic No Aggro removal still needs an in-game test after installation. If a command sends but no server chat reply appears, check `BepInEx/LogOutput.log` and the generated server BepInEx log. ServerLaunchFix notes that its first local-server launch can take longer while it builds interop files.

## Release packaging

Run `./Package.ps1` (PowerShell 7) to build the DLL and create a versioned Nexus-ready manual-install ZIP and SHA-256 file in `dist/releases/`. Pass `-GameDir` for a different game installation. The packager checks project, plugin and assembly versions and verifies every ZIP entry. It refuses to overwrite an existing release. The ZIP includes the trainer DLL, installation guide, helper download scripts, full source and third-party notices. It excludes local settings, saves, logs, symbols, game assemblies and third-party dependency DLLs.

[Nexus listing copy](packaging/NEXUS-LISTING.md) provides upload fields and a BBCode description. See [CHANGELOG.md](CHANGELOG.md) for releases and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for upstream item-catalog attribution and license text.

Verification commands:

```powershell
dotnet run --project tests/SteamCacheTests.csproj -- --current
dotnet run --project tests/WorldSettingsTests.csproj
./tests/Test-ItemCatalog.ps1
./tests/Test-BloodAssist.ps1
./tests/Test-QualityOfLife.ps1
```

The live Steam check needs a signed-in account with a V Rising cache; omit `-- --current` for fixture-only tests. Helper tests download pinned packages into isolated fixtures. Close V Rising before running the server-helper test.
