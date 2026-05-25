# Game Design

## Основной игровой цикл

Игрок начинает в бункере, подготавливает снаряжение и инвентарь, выбирает локацию, отправляется в рейд, выполняет задачи, ищет лут, активирует эвакуацию, возвращается в бункер, получает награды и сохраняет прогресс.

## Бункер

The bunker is the safe base. It is used for storage, sorting items, preparing loadouts, interacting with terminals, and later placing or upgrading stations. In the current MVP, the bunker needs only a manager, a storage container, and a terminal that opens location selection.

## Raids

## Награды

Опыт рассчитывается в конце рейда, а не напрямую при смерти зомби.

Базовая формула награждает игрока за:

- убийства;
- нанесённый урон;
- выполненные задачи;
- возрождения союзников;
- успешную эвакуацию.

Objectives are configured through `MissionDefinition`. The first MVP objective is `KillZombies`: kill a target number of zombies, then activate extraction. Other planned objective types are loot, interact, survive, and extract.

## Опыт

`CharacterProgression` — единственное место, отвечающее за добавление опыта, проверку повышения уровня, выдачу очков характеристик и их трату.

`CharacterStats` хранит текущие значения персонажа и пересчитывает производные характеристики.

## Инвентарь

Предметы представлены через `ItemSO`.

В данных сохранения хранятся id предметов и количество в стаке, а не прямые ссылки на ScriptableObject.

Лут, собранный во время рейда, остаётся в инвентаре игрока после возвращения в бункер.

## Inventory

MVP сначала реализуется в одиночном режиме.

Новые системы рейдов должны предоставлять события для будущей синхронизации через `CoopGameplaySync`:

- начало рейда;
- завершение рейда;
- активация эвакуации;
- прогресс задач;
- завершение задач;
- статистика и награды по каждому игроку.
