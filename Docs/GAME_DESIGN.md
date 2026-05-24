# Game Design

## Core Loop

The player starts in the bunker, prepares equipment and inventory, chooses a location, enters a raid, completes objectives, searches for loot, activates extraction, returns to the bunker, receives rewards, and saves progress.

## Bunker

The bunker is the safe base. It is used for storage, sorting items, preparing loadouts, interacting with terminals, and later placing or upgrading stations. In the current MVP, the bunker needs only a manager, a storage container, and a terminal that opens location selection.

## Raids

A raid is a temporary dangerous session in a selected location. The player enters with current character stats and inventory, completes a required mission, optionally gathers loot, and must extract to keep progress.

## Locations

Locations are configured as `LocationDefinition` ScriptableObjects. Each location has an id, display name, description, scene name, difficulty, recommended level, base experience reward, unlock state, and available missions.

## Objectives

Objectives are configured through `MissionDefinition`. The first MVP objective is `KillZombies`: kill a target number of zombies, then activate extraction. Other planned objective types are loot, interact, survive, and extract.

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

## Inventory

Items are represented by `ItemSO`. Save data stores item ids and stack amounts, not direct ScriptableObject references. Loot taken during a raid remains in the player inventory when returning to the bunker.

## Future Co-op

The MVP is singleplayer first. New raid systems expose events for future sync through `CoopGameplaySync`: raid start/completion, extraction activation, objective progress/completion, and per-player stats/rewards.
