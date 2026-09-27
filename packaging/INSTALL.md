# Hutch V Rising Trainer 0.6.5

An in-game F6 trainer and read-only Steam achievement checklist for Windows / Steam V Rising private, locally hosted PvE worlds. Built against V Rising v1.1.13.0-r99712-b17 and BepInEx 6 IL2CPP. Dedicated and remote servers are not supported by this trainer's command bridge.

## Requirements

Install these separately. Their DLLs are not included in this download.

| Dependency | Verified helper version | Purpose |
| --- | --- | --- |
| [BepInExPack for V Rising](https://thunderstore.io/c/v-rising/p/BepInEx/BepInExPack_V_Rising/) | BepInEx 6 IL2CPP | Loads the trainer; use the V Rising pack, not a generic Mono pack |
| [ServerLaunchFix](https://thunderstore.io/c/v-rising/p/Mythic/ServerLaunchFix/) | 0.2.0 | Loads server plugins in a locally hosted world |
| [VampireCommandFramework](https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/) | 0.10.4 | Runs the helper chat commands |
| [KindredCommands](https://thunderstore.io/c/v-rising/p/odjit/KindredCommands/) | 2.5.8 | Combat, items, blood and servant commands |
| [KindredSchematics](https://thunderstore.io/c/v-rising/p/odjit/KindredSchematics/) | 1.5.6 | Free building, crafting, research and repair costs |

PowerShell 7 is needed only for the optional dependency installer scripts. No .NET SDK is needed to install the prebuilt trainer DLL.

## Install

1. Close V Rising. Back up any world you intend to use with mods.
2. Install BepInExPack using its own instructions. Launch the game once so BepInEx creates its folders and interop assemblies, then close it.
3. Install the four server helpers listed above, once each, following their instructions. Alternatively, open PowerShell 7 in the extracted download folder and run the command below. Substitute the actual game directory; Steam's Manage > Browse local files opens it.

   ```powershell
   ./tools/Install-BloodAssist.ps1 -Install -GameDir 'C:\Program Files (x86)\Steam\steamapps\common\VRising'
   ```

   The script downloads the pinned helper versions and checks SHA-256 hashes before installing them. It refuses conflicting helper copies. Do not install the same DLL again through another mod manager.
4. Copy this download's `BepInEx` folder into the game directory containing `VRising.exe`. The final trainer path must be `BepInEx/plugins/HutchVRisingTrainer/HutchVRisingTrainer.dll`.
5. Start V Rising and load your private world. The first local server start can take longer while server interop files are generated.
6. Enable the console in game options, open it with the console key (normally `~`), and enter `adminauth`. Close the console and press **F6**. Wait for the local command link to report ready.

Use manual installation for this archive. Vortex deployment has not been verified. The `source`, `tools`, and documentation folders do not need to be copied into the game.

## Controls and features

- **F6:** open or close the draggable trainer. Tabs cover combat, blood, items, building, travel and servants.
- **F7:** with the menu closed, aim at a converting servant coffin to finish its conversion. Requires admin authentication.
- **Combat:** god mode that keeps bosses aggressive, invulnerability, blood drain, cooldown, sun protection, damage boost, gear repair and a clear-boosts button.
- **Items:** searchable catalog of 1,107 item prefabs, a quantity field and item grants. Read the server's chat response; some internal prefabs cannot be granted.
- **Blood:** spawn 99% or 100% humans and configure Irradiant Gruel to add 1% without mutation.
- **Build:** toggle free material costs through KindredSchematics. Recipes, unlocks and placement rules still apply. The setting affects everyone on the local server.
- **Travel and servants:** save unrestricted waygate inventory travel or 1x/3x/6x servant conversion speed. Leave and reload the world to apply. The original world settings are backed up once beside their file.
- **Achievements:** read all 49 Steam achievement states from the signed-in account's local cache where available. Public profile lookup is the fallback. The trainer does not unlock or reset Steam achievements.

God mode includes no cooldown. Individual boost buttons toggle their effects, so pressing no cooldown after enabling god mode switches that effect off. Use **Show boost state** to check. By default the trainer sends `.mortal` once on joining a local world to clear boosts left from a previous session; authentication is still required and chat confirms success.

For achievement runs, perform the required actions in game. Keep Brutal difficulty for the Brutal milestones; this package does not guarantee every mod combination or command will satisfy an achievement's trigger.

## Configuration and upgrades

After the first launch, close the game and edit `BepInEx/config/hutch.vrising.achievementtrainer.cfg` if needed:

- `Achievements.SteamId64`: leave the value blank to detect the signed-in account. Set a 17-digit ID to track a different public profile.
- `Interface.ToggleKey`: defaults to F6.
- `Interface.InstantConvertKey`: defaults to F7.
- `Safety.ClearBoostsOnJoin`: defaults to true.

To upgrade, close the game and replace only the trainer DLL. Existing configuration is preserved. If upgrading from the personal v0.6.4 build, clear the existing `SteamId64` value to opt into automatic account selection.

## Optional stack and crafting speed mods

The included `tools/Install-QualityOfLife.ps1` can separately download [VStacks 1.0.1](https://thunderstore.io/c/v-rising/p/AETeam/VStacks/) and [VRates 1.0.0](https://thunderstore.io/c/v-rising/p/AETeam/VRates/). These are not required by the trainer. Close the game and run:

```powershell
./tools/Install-QualityOfLife.ps1 -Install -GameDir 'C:\Program Files (x86)\Steam\steamapps\common\VRising' -StackMultiplier 10 -MachineSpeedMultiplier 10
```

This writes client and local-server configs. Use `-Configure` instead of `-Install` to change multipliers later. Before removing these mods, split stacks larger than 4,095 items; vanilla inventory counts cannot represent them.

## Uninstall and troubleshooting

Close the game and remove `BepInEx/plugins/HutchVRisingTrainer/HutchVRisingTrainer.dll` to uninstall the trainer. Config files can be kept. Saved travel and servant settings persist independently: restore them using the trainer controls before removal, or your world-settings backup. Use **Mortal / clear boosts** before removing the trainer if boosts are active.

If installed through the included helper scripts, `-Remove -GameDir 'YOUR GAME DIRECTORY'` removes only those scripts' managed helper DLLs. Keep helpers used by other mods.

If F6 does nothing, check the DLL path, BepInEx installation, game version and key conflicts. If buttons are unavailable, confirm this is a locally hosted world and `VRisingServer.exe` is running. If commands are rejected, run `adminauth` and read chat. Check `BepInEx/LogOutput.log` and the local server's BepInEx log for errors. For an unavailable checklist, start Steam and use **Refresh Steam**; the local cache can lag until Steam saves it.

## Source and credits

Complete trainer source is included under `source/`, with build requirements in its README. Source and issue tracker: https://github.com/jfhutchi/V-Rising_trainer

See `THIRD-PARTY-NOTICES.md` and `licenses/` for the item catalog's upstream attribution and license. This is an unofficial community mod, not affiliated with Stunlock Studios or Valve.
