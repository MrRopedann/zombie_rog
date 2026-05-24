# zombie_rog

`zombie_rog` is a Unity extraction roguelite prototype about singleplayer and future co-op raids from a bunker into infected zombie locations.

## Genre

Singleplayer/co-op extraction roguelite with survival, looting, shooting, character progression, bunker preparation, and raid extraction.

## Idea

The player creates a survivor, prepares in a safe bunker, chooses a dangerous location, completes raid objectives, extracts with loot, receives experience from raid results, and returns to the bunker to save progress and prepare again.

## Current Status

The project is in active development. The current milestone is a singleplayer vertical slice:

Bunker -> location selection -> raid -> kill zombies objective -> extraction -> result screen -> experience reward -> save -> return to bunker.

## Implemented Systems

- Character stats, needs, stamina, health, level fields, stat points, and HUD.
- Character creation and selected profile persistence.
- Inventory, world items, loot containers, sorting, inventory UI, and loot container UI.
- Zombie AI, health, hitboxes, spawning, noise reactions, ragdoll/death support.
- Shooting with weapon definitions, ammo, hitscan/projectile support, shot effects, and IK hooks.
- Early co-op layer with session state, gameplay sync, network identity, transforms, menu, and scoreboard.
- Save system based on Easy Save 3 for players, inventories, containers, world items, zombies, weapons, and saveable scene objects.

## Opening the Project

1. Install a Unity Editor version compatible with URP 14 and the package manifest.
2. Open the repository folder in Unity Hub.
3. Let Unity restore packages from `Packages/manifest.json`.
4. Open `Assets/_Project/Scenes/Main/MainScene.unity` or `Assets/_Project/Scenes/Bunker/Bunker.unity`.
5. Ensure build settings include `MainScene`, `Bunker`, and the raid scene such as `City`.

## Unity Packages

- Cinemachine
- Input System
- Netcode
- AI Navigation
- Animation Rigging
- URP
- TextMeshPro
- UGUI
- Unity Test Framework
- Unity MCP
- Easy Save 3 plugin in `Assets/Plugins`

## Nearest MVP

- Bunker manager and terminal.
- Location and mission ScriptableObject configs.
- Raid manager with objective tracking.
- Kill zombies objective.
- Extraction point activation.
- Raid result UI.
- Experience reward through `CharacterProgression`.
- Progress save after returning to the bunker.

## Development Warning

This project is a work in progress. Scenes, prefabs, balance, UI, and co-op behavior may change while the singleplayer vertical slice is being stabilized.
