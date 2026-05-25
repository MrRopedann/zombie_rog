# Roadmap

## 1. Project Stabilization

Status: MVP implemented / needs testing.

Audit existing systems, avoid duplicates, keep scene and prefab references intact, and isolate risky large classes for later refactoring.

## 2. Saves

Status: MVP implemented / needs testing.

Extend the existing Easy Save 3 based `GameSaveManager` with bunker and location progress while keeping item saves based on `itemID`.

## 3. Bunker

Status: MVP implemented / needs testing.

Add bunker manager, storage, terminal, and minimal state data. Later expand into buildable stations and upgrades.

## 4. Location Selection

Status: MVP implemented / needs testing.

Create `LocationDefinition` assets and a temporary UGUI location selection panel opened by the bunker terminal.

## 5. Raid

Status: MVP implemented / needs testing.

Add raid start/completion flow, selected location state, active mission state, raid statistics, and reward calculation.

## 6. Objectives

Status: MVP implemented / needs testing.

Implement the objective framework and the first required objective: kill N zombies.

## 7. Extraction

Status: MVP implemented / needs testing.

Activate extraction after required objectives are complete. Complete the raid when the player enters the extraction trigger.

## 8. Result Screen

Status: MVP implemented / needs testing.

Show raid stats, success state, earned experience, current level, possible new level, and a continue button.

## 9. Progression

Status: MVP implemented / needs testing.

Route all experience through `CharacterProgression`, award stat points on level up, and update character HUD through existing events.

## 10. Crafting

Status: next / started.

Add basic recipes and workbench behavior after the vertical slice is stable. Current scope includes instant crafting, recipes, inventory/storage ingredient checks, temporary UGUI, and starter recipes.

## 11. Bunker Building

Status: next after crafting / started as station scaffolding.

Add station placement, station upgrades, and bunker persistence. Current scope is station definitions, station levels/unlock state, upgrade recipes, and station save data. Full grid/base-building is not in scope yet.

## 12. Co-op

Status: future co-op integration.

Connect raid state, objectives, extraction, stats, inventory deltas, and rewards to the existing co-op sync layer.

## 13. Polish

Status: ongoing polish.

Improve UI, balance, audio feedback, spawn tuning, mission variety, visual clarity, and persistence edge cases.
