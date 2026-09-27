# Nexus upload copy

## Upload fields

- Game: V Rising
- Mod name: Hutch V Rising Trainer
- Version: 0.6.5
- Main file: Hutch-V-Rising-Trainer-0.6.5-Nexus.zip
- File description: Prebuilt F6 trainer for locally hosted private PvE worlds. Includes installation instructions, optional helper download scripts, full trainer source and third-party notices. Install the listed dependencies separately.
- Installation method: Manual. Vortex deployment has not been verified.
- Game compatibility: Built against PC/Steam v1.1.13.0-r99712-b17, BepInEx 6 IL2CPP. Other versions are unverified.

## Short description

An F6 trainer for private V Rising worlds: combat boosts, item search and grants, blood tools, free costs, travel and servant settings, plus a read-only Steam achievement checklist.

## Description (BBCode)

[b]Hutch V Rising Trainer[/b]

A draggable in-game menu for Windows / Steam players hosting their own private PvE world. Press F6 to open it and choose combat, blood, items, building, travel or servants. An achievement checklist tracks the signed-in Steam account automatically.

[b]Features[/b]
[list]
[*]God mode configured to keep bosses aggressive, plus individual combat and survival toggles.
[*]Search 1,107 item prefabs and give a selected quantity.
[*]Spawn 99% and 100% humans; configure predictable Irradiant Gruel.
[*]Free material costs through KindredSchematics.
[*]Save waygate inventory travel and servant conversion speed settings.
[*]Aim at a converting servant coffin and press F7 to finish it.
[*]Read-only checklist of 49 Steam achievements. No achievement unlock/reset button.
[/list]

[b]Requirements[/b]
Install BepInExPack for V Rising (BepInEx 6 IL2CPP), ServerLaunchFix 0.2.0, VampireCommandFramework 0.10.4, KindredCommands 2.5.8 and KindredSchematics 1.5.6. Download links and an optional PowerShell 7 helper installer are included in the README. Third-party dependency DLLs are not bundled.

[b]Installation[/b]
Close the game, install BepInEx and the server helpers, then copy the archive's BepInEx folder into the folder containing VRising.exe. Start your private world, enable the console, run adminauth, close the console, and press F6. Read the included README for exact steps, configuration, updates and uninstall instructions.

[b]Compatibility[/b]
Built against V Rising v1.1.13.0-r99712-b17. Intended for locally hosted private PvE worlds. The command bridge does not support remote or dedicated servers. Other game versions and Vortex deployment are unverified. Some controls affect everyone in the local world. Travel and servant speed changes require a world reload.

[b]Achievements[/b]
The checklist reads Steam's local achievement cache, with public profile lookup as a fallback. It does not write achievement state. Complete the actual in-game requirements, and keep Brutal difficulty for Brutal milestones. No guarantee is made for every mod combination or achievement trigger.

[b]Credits and source[/b]
Trainer by jfhutchi. Thanks to the BepInEx team, Mythic, deca, odjit and AETeam for the dependencies. Item prefab catalog derived from KindredCommands; upstream attribution, AGPL-3.0 text and full trainer source are included.
[url=https://github.com/jfhutchi/V-Rising_trainer]Source and issues[/url]

Unofficial community mod. Not affiliated with Stunlock Studios or Valve.

## Release notes

0.6.5 prepares the existing trainer for public distribution: automatic signed-in Steam account selection, preserved explicit profile overrides, manual-install archive, source, checksums and public installation documentation.
