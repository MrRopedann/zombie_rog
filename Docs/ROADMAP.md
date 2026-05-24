# Roadmap

## 1. Project Stabilization

Audit existing systems, avoid duplicates, keep scene and prefab references intact, and isolate risky large classes for later refactoring.

## 2. Saves

Extend the existing Easy Save 3 based `GameSaveManager` with bunker and location progress while keeping item saves based on `itemID`.

## 3. Bunker

Add bunker manager, storage, terminal, and minimal state data. Later expand into buildable stations and upgrades.

## 4. Location Selection

Create `LocationDefinition` assets and a temporary UGUI location selection panel opened by the bunker terminal.

## 5. Raid

Add raid start/completion flow, selected location state, active mission state, raid statistics, and reward calculation.

## 6. Objectives

Implement the objective framework and the first required objective: kill N zombies.

## 7. Extraction

Activate extraction after required objectives are complete. Complete the raid when the player enters the extraction trigger.

## 8. Result Screen

Show raid stats, success state, earned experience, current level, possible new level, and a continue button.

## 9. Progression

Route all experience through `CharacterProgression`, award stat points on level up, and update character HUD through existing events.

## 10. Crafting

Add basic recipes and workbench behavior after the vertical slice is stable.

## 11. Bunker Building

Add station placement, station upgrades, and bunker persistence.

## 12. Co-op

Connect raid state, objectives, extraction, stats, inventory deltas, and rewards to the existing co-op sync layer.

## 13. Polish

Improve UI, balance, audio feedback, spawn tuning, mission variety, visual clarity, and persistence edge cases.
