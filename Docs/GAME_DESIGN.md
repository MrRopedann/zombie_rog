# Game Design

## Core Loop

The player starts in the bunker, prepares equipment and inventory, chooses a location, enters a raid, completes objectives, searches for loot, activates extraction, returns to the bunker, receives rewards, and saves progress.

## Bunker

The bunker is the safe base. It is used for storage, sorting items, preparing loadouts, interacting with terminals, crafting, and later placing or upgrading stations. In the current MVP, the bunker needs a manager, a storage container, a terminal that opens location selection, and early station objects.

## Bunker Stations

Stations are represented by `BuildableStation` and configured with `StationDefinition`. The first station set is Storage, Workbench, Medical Station, Weapon Bench, Generator, and Radio Terminal. The workbench is the first station expected to open `CraftingUI`; the rest are scaffolding for later specialization.

Station save data stores ids, station type text, level, position, rotation, unlock state, and built state. It does not store direct `StationDefinition` references.

## Raids

A raid is a temporary dangerous session in a selected location. The player enters with current character stats and inventory, completes a required mission, optionally gathers loot, and must extract to keep progress.

## Locations

Locations are configured as `LocationDefinition` ScriptableObjects. Each location has an id, display name, description, scene name, difficulty, recommended level, base experience reward, unlock state, and available missions.

## Objectives

Objectives are configured through `MissionDefinition`. The first MVP objective is `KillZombies`: kill a target number of zombies, then activate extraction. The objective framework now also has hooks for loot, interact, survive, and extract objectives.

## Statistics

Raid statistics track:

- Zombie kills.
- Damage dealt.
- Damage taken.
- Items looted.
- Required and optional objectives completed.
- Allies revived.
- Raid time.
- Extraction success.

## Rewards

Experience is calculated at the end of a raid, not directly when a zombie dies. The base formula rewards kills, damage dealt, completed objectives, revives, successful extraction, and applies a location difficulty multiplier.

## Experience

`CharacterProgression` is the single place responsible for adding experience, checking level ups, awarding stat points, and spending stat points. `CharacterStats` stores current character values and recalculates derived stats.

## Crafting

Crafting is configured through `CraftingRecipe` ScriptableObjects. A recipe has a station type requirement, station level requirement, item ingredients, result item, result amount, craft time, and default unlock flag.

The first implementation is instant crafting. `CraftingSystem` checks resources across `PlayerInventory` and `BunkerStorage`, consumes ingredients, adds the result to player inventory or storage, and returns a `CraftingResult` with a failure reason when crafting cannot happen.

## Inventory

Items are represented by `ItemSO`. Save data stores item ids and stack amounts, not direct ScriptableObject references. Loot taken during a raid remains in the player inventory when returning to the bunker.

## Future Co-op

The MVP is singleplayer first. New raid systems expose events for future sync through `CoopGameplaySync`: raid start/completion, extraction activation, objective progress/completion, and per-player stats/rewards.
