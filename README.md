# zombie_rog

`zombie_rog` — это прототип Unity-игры в жанре extraction roguelite про одиночные и будущие кооперативные вылазки из бункера в заражённые зомби-локации.

## Жанр

Одиночный/кооперативный extraction roguelite с элементами выживания, лута, стрельбы, прокачки персонажа, подготовки в бункере и эвакуации из рейда.

## Идея

Игрок создаёт выжившего, готовится в безопасном бункере, выбирает опасную локацию, выполняет задачи вылазки, эвакуируется с лутом, получает опыт по итогам рейда и возвращается в бункер, чтобы сохранить прогресс и подготовиться к следующей вылазке.

## Текущий статус

The project is in active development. The current milestone is a singleplayer vertical slice:

Бункер -> выбор локации -> рейд -> задача на убийство зомби -> эвакуация -> экран результатов -> награда опытом -> сохранение -> возврат в бункер.

## Implemented Systems

- Character stats, needs, stamina, health, level fields, stat points, and HUD.
- Character creation and selected profile persistence.
- Inventory, world items, loot containers, sorting, inventory UI, and loot container UI.
- Zombie AI, health, hitboxes, spawning, noise reactions, ragdoll/death support.
- Shooting with weapon definitions, ammo, hitscan/projectile support, shot effects, and IK hooks.
- Early co-op layer with session state, gameplay sync, network identity, transforms, menu, and scoreboard.
- Save system based on Easy Save 3 for players, inventories, containers, world items, zombies, weapons, and saveable scene objects.

## Открытие проекта

1. Установите версию Unity Editor, совместимую с URP 14 и package manifest проекта.
2. Откройте папку репозитория через Unity Hub.
3. Дождитесь, пока Unity восстановит пакеты из `Packages/manifest.json`.
4. Откройте `Assets/_Project/Scenes/Main/MainScene.unity` или `Assets/_Project/Scenes/Bunker/Bunker.unity`.
5. Убедитесь, что в Build Settings добавлены `MainScene`, `Bunker` и сцена рейда, например `City`.

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
- Плагин Easy Save 3 в `Assets/Plugins`

## Nearest MVP

- Bunker manager and terminal.
- Location and mission ScriptableObject configs.
- Raid manager with objective tracking.
- Kill zombies objective.
- Extraction point activation.
- Raid result UI.
- Experience reward through `CharacterProgression`.
- Progress save after returning to the bunker.

## Предупреждение о разработке

Проект находится в разработке. Сцены, префабы, баланс, UI и поведение кооператива могут изменяться во время стабилизации одиночного вертикального среза.
