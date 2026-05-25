# zombie_rog

`zombie_rog` is a Unity extraction roguelite prototype about singleplayer and future co-op raids from a bunker into infected zombie locations.

## Genre

Singleplayer/co-op extraction roguelite with survival, looting, shooting, character progression, bunker preparation, and raid extraction.

## Idea

The player creates a survivor, prepares in a safe bunker, chooses a dangerous location, completes raid objectives, extracts with loot, receives experience from raid results, and returns to the bunker to save progress and prepare again.

## Current Status

The project is in active development. Roadmap items 1-9 are implemented at MVP level and still need regular Unity playtesting:

Bunker -> location selection -> raid -> kill zombies objective -> extraction -> result screen -> experience reward -> save -> return to bunker.

The next active milestone is basic crafting and bunker stations. Full base-building and full co-op raid sync remain future work.

## Implemented Systems

- Character stats, needs, stamina, health, level fields, stat points, and HUD.
- Character creation and selected profile persistence.
- Inventory, world items, loot containers, sorting, inventory UI, and loot container UI.
- Zombie AI, health, hitboxes, spawning, noise reactions, ragdoll/death support.
- Shooting with weapon definitions, ammo, hitscan/projectile support, shot effects, and IK hooks.
- Early co-op layer with session state, gameplay sync, network identity, transforms, menu, and scoreboard.
- Save system based on Easy Save 3 for players, inventories, containers, world items, zombies, weapons, and saveable scene objects.
- MVP bunker, location selection, raid manager, objectives, extraction, result UI, reward calculation, and progression save/return.
- Basic crafting runtime, temporary UGUI crafting UI, starter recipes, and station upgrade scaffolding.

## Opening the Project

1. Install a Unity Editor version compatible with URP 14 and the package manifest.
2. Open the repository folder in Unity Hub.
3. Let Unity restore packages from `Packages/manifest.json`.
4. Open `Assets/_Project/Scenes/Main/MainScene.unity` or `Assets/_Project/Scenes/Bunker/Bunker.unity`.
5. Ensure build settings include `MainScene`, `Bunker`, and the raid scene such as `City`.

## Build Settings Scenes

- `Assets/_Project/Scenes/Main/MainScene.unity`
- `Assets/_Project/Scenes/Bunker/Bunker.unity`
- `Assets/_Project/Scenes/Locations/City/City.unity`

## Vertical Slice Check

1. Open `Bunker`.
2. Run `Zombie Rogue/MVP/Create Or Refresh Test Assets` if MVP assets are missing.
3. Run `Zombie Rogue/MVP/Setup Open Bunker Scene` in the bunker scene.
4. Open `City` and run `Zombie Rogue/MVP/Setup Open Raid Scene`.
5. Start from `Bunker`, interact with the terminal, choose `MVP_CityLocation`, start the raid, kill the required zombies, enter extraction, continue from the result screen, and confirm the player returns to `Bunker`.

## Required ScriptableObjects

- `MVP_CityLocation` and `MVP_KillZombies` in `Assets/_Project/Resources/RuntimeLoadedOnly/Data/Raid/`.
- Starter test locations such as `City Easy`, `City Medium`, `Medical Raid`, and `Test Location` in the same folder.
- Item assets in `Assets/_Project/Resources/RuntimeLoadedOnly/Data/Item/`.
- Crafting recipes in `Assets/_Project/Resources/RuntimeLoadedOnly/Data/Crafting/`.
- Station definitions in `Assets/_Project/Resources/RuntimeLoadedOnly/Data/Bunker/`.

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

## In Progress

- Stabilize the implemented MVP loop in Unity Editor.
- Wire workbench scene objects to `CraftingStation` and `CraftingUI`.
- Wire buildable bunker station objects to `BuildableStation`, `StationDefinition`, and `StationUpgradeSystem`.
- Keep co-op integration limited to events and TODO hooks until singleplayer raid/crafting flow is stable.

## Known Risks

- Unity must be opened/refreshed to verify scene references after script moves.
- The active Unity instance prevents a second batchmode compile on the same project.
- Runtime-generated UGUI is intentionally temporary.
- New station definitions have no final world prefabs assigned yet.

## Development Warning

This project is a work in progress. Scenes, prefabs, balance, UI, and co-op behavior may change while the singleplayer vertical slice is being stabilized.
