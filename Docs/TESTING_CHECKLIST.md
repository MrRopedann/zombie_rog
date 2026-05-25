# Testing Checklist

## Bunker to Raid to Result to Bunker

1. Open `Assets/_Project/Scenes/Bunker/Bunker.unity`.
2. Confirm Build Settings contain `MainScene`, `Bunker`, and `City`.
3. Run `Zombie Rogue/MVP/Create Or Refresh Test Assets`.
4. Run `Zombie Rogue/MVP/Setup Open Bunker Scene`.
5. Open `Assets/_Project/Scenes/Locations/City/City.unity`.
6. Run `Zombie Rogue/MVP/Setup Open Raid Scene`.
7. Start Play Mode from `Bunker`.
8. Interact with the raid terminal and select `MVP_CityLocation`.
9. Start the raid and confirm `City` loads.
10. Kill the required zombies and confirm the objective UI updates.
11. Enter the extraction point after it activates.
12. Confirm the result screen appears.
13. Press Continue and confirm the player returns to `Bunker`.

## Save and Load

1. Complete a raid extraction.
2. Confirm `GameSaveManager.SaveExtractedRaidReturnAndQueueLoad` saves progress before returning to `Bunker`.
3. Stop Play Mode, start again, and run load from the existing save UI or `GameSaveManager.LoadCurrentGame`.
4. Confirm player level, experience, stat points, inventory stacks, bunker storage, unlocked locations, and station levels are restored.

## Crafting

1. In the bunker, add or select a world object for the workbench.
2. Add `BuildableStation`, set its definition to `WorkbenchStation`, and add `CraftingStation`.
3. Ensure the scene has a `CraftingSystem` or allow `CraftingStation` to create one.
4. Put required items such as `Wood`, `Coin`, and `Water` in `PlayerInventory` or `BunkerStorage`.
5. Open the crafting UI.
6. Select `Bandage`, `Rifle Ammo`, `Simple Ration`, or `Upgrade Component`.
7. Confirm missing resources show an error.
8. Confirm successful crafting removes ingredients and adds the result.

## Station Upgrade

1. Add `StationUpgradeSystem` to a bunker runtime object.
2. Add `BuildableStation` to a station object and assign a `StationDefinition`.
3. Add `StationUpgradeUI` if a temporary UI is needed.
4. Put `Upgrade Component` in inventory or storage.
5. Trigger `StationUpgradeSystem.Upgrade`.
6. Confirm the station level increases and the save data stores the new level.

## Objectives

1. Test `KillObjective` with `MVP_KillZombies`.
2. Test `LootObjective` with `Medical_Raid_Loot` by picking up an item with `itemID = water`.
3. Test `InteractObjective` by adding `ObjectiveInteractable` and matching `targetInteractableId`.
4. Test `ExtractObjective` by using an extract-only mission and confirming extraction is active immediately.
5. Test `SurviveObjective` with a survive mission and confirm seconds advance in `RaidObjectiveUI`.

## Extraction

1. Confirm extraction visuals and trigger are disabled at raid start for kill/loot/interact/survive objectives.
2. Complete all required objectives.
3. Confirm `RaidManager.OnExtractionActivated` fires and extraction visuals/trigger enable.
4. Enter the trigger with the player and confirm `RaidManager.CompleteRaid(true)` runs.

## UI

1. Confirm location selection opens and closes without cursor lock issues.
2. Confirm raid objective UI does not overlap other HUD elements at common resolutions.
3. Confirm raid result UI applies experience only once.
4. Confirm crafting UI refreshes ingredient counts after crafting.
5. Confirm station upgrade UI closes and releases the cursor.

## Inventory Loot Persistence

1. Loot an item during a raid.
2. Extract successfully.
3. Confirm the item remains in player inventory after returning to `Bunker`.
4. Save and reload.
5. Confirm item ids and amounts match the pre-reload inventory.
