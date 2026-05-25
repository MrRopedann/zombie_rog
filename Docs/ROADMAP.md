# Roadmap

## 1. Стабилизация проекта

Audit existing systems, avoid duplicates, keep scene and prefab references intact, and isolate risky large classes for later refactoring.

## 2. Сохранения

Extend the existing Easy Save 3 based `GameSaveManager` with bunker and location progress while keeping item saves based on `itemID`.

## 3. Бункер

Add bunker manager, storage, terminal, and minimal state data. Later expand into buildable stations and upgrades.

## 4. Выбор локации

Create `LocationDefinition` assets and a temporary UGUI location selection panel opened by the bunker terminal.

## 5. Рейд

Add raid start/completion flow, selected location state, active mission state, raid statistics, and reward calculation.

## 6. Задачи

Implement the objective framework and the first required objective: kill N zombies.

## 7. Эвакуация

Activate extraction after required objectives are complete. Complete the raid when the player enters the extraction trigger.

## 8. Экран результатов

Show raid stats, success state, earned experience, current level, possible new level, and a continue button.

## 9. Прогрессия

Route all experience through `CharacterProgression`, award stat points on level up, and update character HUD through existing events.

## 10. Крафт

Add basic recipes and workbench behavior after the vertical slice is stable.

## 11. Строительство бункера

Add station placement, station upgrades, and bunker persistence.

## 12. Кооператив

Connect raid state, objectives, extraction, stats, inventory deltas, and rewards to the existing co-op sync layer.

## 13. Полировка

Improve UI, balance, audio feedback, spawn tuning, mission variety, visual clarity, and persistence edge cases.
