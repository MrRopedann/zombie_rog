# Рефакторинг структуры проекта

Дата: 2026-05-24

## Цель

Структура `Assets` была приведена к разделению собственного проекта, сторонних ассетов и плагинов без переписывания игровой логики. Перемещения выполнялись через `git mv`, чтобы сохранить Unity GUID у ассетов и соответствующих `.meta`.

## Итоговая верхнеуровневая структура

```text
Assets/
  _Project/
    Art/
    Audio/
    Prefabs/
    Resources/
    Scenes/
    ScriptableObjects/
    Scripts/
    Tests/
    VFX/
  _External/
    NewAnimations/
    ParrelSync/
    PolygonApocalypse/
    PolygonBattleRoyale/
    PolygonGeneric/
    Synty/
    TextMesh Pro/
  Plugins/
    Easy Save 3/
Созданные проектные папки
Assets/_Project/Scenes/Main
Assets/_Project/Scenes/Bunker
Assets/_Project/Scenes/Locations
Assets/_Project/Scenes/Test

Assets/_Project/Scripts/Runtime/Core/Stats
Assets/_Project/Scripts/Runtime/Core/Cameras
Assets/_Project/Scripts/Runtime/Core/Utils
Assets/_Project/Scripts/Runtime/Player/Movement
Assets/_Project/Scripts/Runtime/Player/CharacterCreation
Assets/_Project/Scripts/Runtime/Player/Progression
Assets/_Project/Scripts/Runtime/Player/HUD
Assets/_Project/Scripts/Runtime/Combat/Damage
Assets/_Project/Scripts/Runtime/Combat/Weapons
Assets/_Project/Scripts/Runtime/Combat/Projectiles
Assets/_Project/Scripts/Runtime/Combat/Hitscan
Assets/_Project/Scripts/Runtime/Combat/IK
Assets/_Project/Scripts/Runtime/Enemies/Zombie/AI
Assets/_Project/Scripts/Runtime/Enemies/Zombie/Health
Assets/_Project/Scripts/Runtime/Enemies/Zombie/Hitboxes
Assets/_Project/Scripts/Runtime/Enemies/Zombie/Spawning
Assets/_Project/Scripts/Runtime/Inventory/Core
Assets/_Project/Scripts/Runtime/Inventory/Items
Assets/_Project/Scripts/Runtime/Inventory/Loot
Assets/_Project/Scripts/Runtime/Inventory/World
Assets/_Project/Scripts/Runtime/Inventory/UI
Assets/_Project/Scripts/Runtime/Input
Assets/_Project/Scripts/Runtime/Save
Assets/_Project/Scripts/Runtime/Audio/Footsteps
Assets/_Project/Scripts/Runtime/UI/HUD
Assets/_Project/Scripts/Runtime/UI/Menus
Assets/_Project/Scripts/Runtime/UI/Menus/CharacterCreation
Assets/_Project/Scripts/Runtime/UI/Crosshair
Assets/_Project/Scripts/Runtime/Networking/Coop

Assets/_Project/Scripts/Editor/SceneTools
Assets/_Project/Scripts/Editor/CharacterTools
Assets/_Project/Scripts/Editor/ItemTools
Assets/_Project/Scripts/Editor/ZombieTools
Assets/_Project/Scripts/Editor/CoopTools

Assets/_Project/Prefabs/Player
Assets/_Project/Prefabs/Enemies
Assets/_Project/Prefabs/Weapons
Assets/_Project/Prefabs/Items
Assets/_Project/Prefabs/UI
Assets/_Project/Prefabs/Systems
Assets/_Project/Prefabs/Environment

Assets/_Project/ScriptableObjects/Items
Assets/_Project/ScriptableObjects/Weapons
Assets/_Project/ScriptableObjects/Audio
Assets/_Project/ScriptableObjects/Enemies

Assets/_Project/Art/Models
Assets/_Project/Art/Materials
Assets/_Project/Art/Textures
Assets/_Project/Art/Animations
Assets/_Project/Art/Terrain

Assets/_Project/Audio/SFX
Assets/_Project/Audio/Music
Assets/_Project/VFX/Shaders
Assets/_Project/VFX/Effects
Assets/_Project/Resources/RuntimeLoadedOnly
Assets/_Project/Tests/EditMode
Assets/_Project/Tests/PlayMode
Таблица перемещений
Старый путь	Новый путь	Причина
Assets/Scripts/CharacterSystem/Movement	Assets/_Project/Scripts/Runtime/Player/Movement	Runtime-код движения игрока отделён от остальных систем.
Assets/Scripts/CharacterSystem/Progression	Assets/_Project/Scripts/Runtime/Player/Progression	Прогрессия и характеристики игрока собраны в Player.
Assets/Scripts/CharacterSystem/CharacterCreation	Assets/_Project/Scripts/Runtime/Player/CharacterCreation	Runtime-часть создания персонажа отделена от UI.
Assets/Scripts/CharacterSystem/UI	Assets/_Project/Scripts/Runtime/UI/Menus/CharacterCreation и Assets/_Project/Scripts/Runtime/Player/HUD	Меню создания персонажа и HUD разнесены по смыслу.
Assets/Scripts/ShootingSystem/Weapon	Assets/_Project/Scripts/Runtime/Combat/Weapons	Оружейная логика перенесена в Combat.
Assets/Scripts/ShootingSystem/Projectiles	Assets/_Project/Scripts/Runtime/Combat/Projectiles	Projectile-код отделён от hitscan и weapon-кода.
Assets/Scripts/ShootingSystem/Hitscan	Assets/_Project/Scripts/Runtime/Combat/Hitscan	Hitscan-стрельба выделена отдельно.
Assets/Scripts/ShootingSystem/IK	Assets/_Project/Scripts/Runtime/Combat/IK	IK для оружия оставлен в Combat/IK.
Assets/Scripts/ShootingSystem/Damage	Assets/_Project/Scripts/Runtime/Combat/Damage	Damage-контракты и damageable-объекты собраны вместе.
Assets/Scripts/EnemySystem/AI	Assets/_Project/Scripts/Runtime/Enemies/Zombie/AI	Zombie AI отделён от здоровья, hitbox и spawning.
Assets/Scripts/EnemySystem/Health	Assets/_Project/Scripts/Runtime/Enemies/Zombie/Health	Здоровье и ragdoll zombie-сущностей собраны отдельно.
Assets/Scripts/EnemySystem/Hitboxes	Assets/_Project/Scripts/Runtime/Enemies/Zombie/Hitboxes	Hitbox-компоненты перенесены в zombie-подсистему.
Assets/Scripts/EnemySystem/Spawning	Assets/_Project/Scripts/Runtime/Enemies/Zombie/Spawning	Spawner zombie перенесён в отдельный блок.
Assets/Scripts/InventorySystem/Core	Assets/_Project/Scripts/Runtime/Inventory/Core	Базовая логика inventory отделена от UI и world-представлений.
Assets/Scripts/InventorySystem/Items	Assets/_Project/Scripts/Runtime/Inventory/Items	ItemSO и item-код собраны в Inventory/Items.
Assets/Scripts/InventorySystem/Loot	Assets/_Project/Scripts/Runtime/Inventory/Loot	Loot-контейнеры и interaction-логика отделены.
Assets/Scripts/InventorySystem/World	Assets/_Project/Scripts/Runtime/Inventory/World	World item/pickup-компоненты вынесены из core inventory.
Assets/Scripts/InventorySystem/UI	Assets/_Project/Scripts/Runtime/Inventory/UI	Inventory UI оставлен рядом с inventory-системой.
Assets/Scripts/CoopSystem	Assets/_Project/Scripts/Runtime/Networking/Coop	Coop-код перенесён в Networking/Coop.
Assets/Scripts/SaveSystem	Assets/_Project/Scripts/Runtime/Save	Save-код вынесен в Runtime/Save.
Assets/Scripts/InputSystem	Assets/_Project/Scripts/Runtime/Input	InputActions и input-настройки собраны в Runtime/Input.
Assets/Scripts/FloorSoundSystem	Assets/_Project/Scripts/Runtime/Audio/Footsteps	Footstep-звуки перенесены в Audio/Footsteps.
Assets/Scripts/UI/HUD	Assets/_Project/Scripts/Runtime/UI/HUD	HUD-скрипты отделены от меню.
Assets/Scripts/UI/Menus	Assets/_Project/Scripts/Runtime/UI/Menus	Menu runtime-код собран в UI/Menus.
Assets/Scripts/UI/Crosshair	Assets/_Project/Scripts/Runtime/UI/Crosshair	Crosshair-код вынесен в отдельный UI-блок.
Assets/Scripts/Core/Stats	Assets/_Project/Scripts/Runtime/Core/Stats	Общие stats-компоненты оставлены в Core.
Assets/Scripts/Core/Cameras	Assets/_Project/Scripts/Runtime/Core/Cameras	Общие camera helpers оставлены в Core.
Assets/Scripts/Core/Utils	Assets/_Project/Scripts/Runtime/Core/Utils	Небольшие runtime utilities вынесены отдельно.
Assets/Scripts/Editor/SceneTools	Assets/_Project/Scripts/Editor/SceneTools	Editor-утилиты сцен отделены от runtime-кода.
Assets/Scripts/Editor/CharacterTools	Assets/_Project/Scripts/Editor/CharacterTools	Editor-утилиты персонажей отделены от runtime-кода.
Assets/Scripts/Editor/ItemTools	Assets/_Project/Scripts/Editor/ItemTools	Editor-утилиты предметов отделены от runtime-кода.
Assets/Scripts/Editor/ZombieTools	Assets/_Project/Scripts/Editor/ZombieTools	Editor-утилиты zombie-системы отделены от runtime-кода.
Assets/Scripts/Editor/CoopTools	Assets/_Project/Scripts/Editor/CoopTools	Editor-утилиты coop-системы отделены от runtime-кода.
Assets/_Scenes/MainScene.unity	Assets/_Project/Scenes/Main/MainScene.unity	Main-сцена перенесена в проектный блок сцен.
Assets/_Scenes/Bunker.unity	Assets/_Project/Scenes/Bunker/Bunker.unity	Bunker-сцена перенесена в отдельную папку.
Assets/_Scenes/City.unity	Assets/_Project/Scenes/Locations/City/City.unity	Location-сцена City отделена от test-сцен.
Assets/_Scenes/City	Assets/_Project/Scenes/Locations/City/Profiles	Профили/настройки сцены оставлены рядом со сценой City.
Assets/_Scenes/Poligon.unity	Assets/_Project/Scenes/Test/Poligon/Poligon.unity	Test/poligon-сцена перенесена в test-scenes.
Assets/_Scenes/Poligon	Assets/_Project/Scenes/Test/Poligon/Profiles	Профили/настройки test-сцены оставлены рядом со сценой.
Assets/_Scenes/TestScene.unity	Assets/_Project/Scenes/Test/TestScene.unity	Test-сцена перенесена в проектный test-блок.
Assets/Test	Assets/_Project/Tests/PlayMode/LegacyAnimationTest	Тестовые ассеты перенесены в _Project/Tests.
Assets/Resources/Data/Item/*	Assets/_Project/Resources/RuntimeLoadedOnly/Data/Item/*	Эти ItemSO реально загружаются через Resources.LoadAll<ItemSO>.
Assets/Resources/Prefabs/Character/Player.prefab	Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/Character/Player.prefab	Player prefab реально загружается через Resources.Load.
Assets/Resources/Prefabs/UI/Menu/CharacterCreationPanel.prefab	Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/UI/Menu/CharacterCreationPanel.prefab	Character creation panel реально загружается через Resources.Load.
Assets/Resources/Prefabs/UI/Menu/CoopMenu.prefab	Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/UI/Menu/CoopMenu.prefab	Coop menu реально загружается через Resources.Load.
Assets/Resources/Prefabs/Zombie/*	Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/Zombie/*	Zombie prefabs реально загружаются через Resources.LoadAll<GameObject>.
Assets/Resources/Data/Weapons/*	Assets/_Project/ScriptableObjects/Weapons/*	WeaponDefinition-ассеты не загружаются через Resources.Load; перенесены в ScriptableObjects.
Assets/Resources/Data/Prefabs/Items/*	Assets/_Project/Prefabs/Items/*	Item prefabs не должны лежать в Resources без необходимости runtime-загрузки.
Assets/Resources/Data/Prefabs/Weapons/*	Assets/_Project/Prefabs/Weapons/*	Weapon prefabs вынесены в обычную prefab-структуру.
Assets/Resources/Materials/*	Assets/_Project/Art/Materials/*	Materials вынесены из Resources.
Assets/Resources/Models/*	Assets/_Project/Art/Models/*	Models вынесены из Resources.
Assets/Resources/Textures/*	Assets/_Project/Art/Textures/*	Textures вынесены из Resources.
Assets/Resources/Animations/*	Assets/_Project/Art/Animations/*	Animations вынесены из Resources.
Assets/Resources/Sounds/*	Assets/_Project/Audio/SFX/*	SFX вынесены из Resources.
Assets/Resources/Shaders/*	Assets/_Project/VFX/Shaders/*	Shaders вынесены из Resources.
Assets/Resources/VFX/*	Assets/_Project/VFX/Effects/*	VFX-ассеты вынесены из Resources.
Assets/Resources/Prefabs/UI/*	Assets/_Project/Prefabs/UI/*	UI prefabs без Resources.Load перенесены в обычные prefab-папки.
Assets/Resources/Prefabs/Environment/*	Assets/_Project/Prefabs/Environment/*	Environment prefabs вынесены из Resources.
Assets/Resources/PolygonBattleRoyale	Assets/_External/PolygonBattleRoyale/ResourcesLegacy	Сторонний пакет вынесен из проектного Resources.
Assets/Resources/PolygonGeneric	Assets/_External/PolygonGeneric/ResourcesLegacy	Сторонний пакет вынесен из проектного Resources.
Assets/Terrain	Assets/_Project/Art/Terrain	Terrain-ассеты перенесены в Art/Terrain.
Assets/Tools/Generated	Assets/_Project/Art/Terrain/Generated	Generated terrain/art-ассеты вынесены в проектный Art.
Assets/CharacterPreviewRT.renderTexture	Assets/_Project/Art/Textures/RenderTextures/CharacterPreviewRT.renderTexture	RenderTexture вынесен из корня Assets.
Assets/EventSystem.prefab	Assets/_Project/Prefabs/Systems/EventSystem.prefab	System prefab вынесен из корня Assets.
Assets/SM_Wep_Ammo_BulletLarge_01.prefab	Assets/_Project/Prefabs/Weapons/SM_Wep_Ammo_BulletLarge_01.prefab	Weapon prefab вынесен из корня Assets.
Assets/PolygonApocalypse	Assets/_External/PolygonApocalypse	Сторонний Synty package отделён от проекта.
Assets/Synty/PolygonGeneric	Assets/_External/PolygonGeneric	Сторонний Synty package отделён от проекта.
Assets/Synty/PolygonBattleRoyale	Assets/_External/PolygonBattleRoyale	Сторонний Synty package отделён от проекта.
Assets/NewAnimations	Assets/_External/NewAnimations	Внешний animation pack отделён от проектных ассетов.
Assets/Tools/TextMesh Pro	Assets/_External/TextMesh Pro	TextMesh Pro examples/package content оставлен как внешний код.
Assets/Tools/ParrelSync	Assets/_External/ParrelSync	ParrelSync вынесен в _External.
Assets/Tools/Plugins/ParrelSync/ScriptableObjects	Assets/_External/ParrelSync/ScriptableObjects	ParrelSync ScriptableObjects оставлены рядом с пакетом.
Assets/Tools/Plugins/Easy Save 3	Assets/Plugins/Easy Save 3	Easy Save 3 оставлен как Unity plugin.
Assets/Tools/SyntyPackageHelper	Assets/_External/Synty/SyntyPackageHelper	Synty helper оставлен с внешними Synty-ассетами.
Assets/Tools/IKHelperTool	Assets/_External/Synty/IKHelperTool	Внешний helper tool оставлен в _External/Synty.
Что осталось в Resources

В проектном Resources оставлен только namespace RuntimeLoadedOnly. Текущий список ассетов:

Assets/_Project/Resources/RuntimeLoadedOnly/Data/Item/Coin.asset
Assets/_Project/Resources/RuntimeLoadedOnly/Data/Item/RiffleAmmo.asset
Assets/_Project/Resources/RuntimeLoadedOnly/Data/Item/Water.asset
Assets/_Project/Resources/RuntimeLoadedOnly/Data/Item/Wood.asset
Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/Character/Player.prefab
Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/UI/Menu/CharacterCreationPanel.prefab
Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/UI/Menu/CoopMenu.prefab
Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/Zombie/SM_Chr_Zombie_Female_01.prefab
Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/Zombie/SM_Chr_Zombie_Female_02.prefab
Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/Zombie/SM_Chr_Zombie_Male_01.prefab
Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/Zombie/SM_Chr_Zombie_Male_02.prefab
Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/Zombie/SM_Prop_DeadBody_Laying_Female_01.prefab
Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/Zombie/SM_Prop_DeadBody_Laying_Male_01.prefab

Причина: эти ассеты используются проектными вызовами Resources.Load или Resources.LoadAll:

RuntimeLoadedOnly/Data
RuntimeLoadedOnly/Data/Item
RuntimeLoadedOnly/Prefabs/Character/Player
RuntimeLoadedOnly/Prefabs/UI/Menu/CharacterCreationPanel
RuntimeLoadedOnly/Prefabs/UI/Menu/CoopMenu
RuntimeLoadedOnly/Prefabs/Zombie

Отдельно оставлен совместимый fallback Resources.Load<GameObject>("SM_Prop_Cross_02"), потому что соответствующий ресурс отсутствовал и в старом проектном Assets/Resources. Добавление нового prefab в Resources изменило бы поведение, поэтому это оставлено как TODO.

Изменённые строковые пути

Пути были обновлены только там, где они ссылались на перемещённые ассеты:

ProjectSettings/EditorBuildSettings.asset
Assets/_Project/Scripts/Runtime/Save/GameSaveManager.cs
Assets/_Project/Scripts/Runtime/Enemies/Zombie/Spawning/ZombieSpawner.cs
Assets/_Project/Scripts/Runtime/UI/Menus/MenuController.cs
Assets/_Project/Scripts/Runtime/UI/Menus/CharacterCreation/PlayerCharacterMenuController.cs
Assets/_Project/Scripts/Runtime/Player/CharacterCreation/CharacterSkinSelector.cs
Assets/_Project/Scripts/Runtime/Networking/Coop/CoopGameplaySync.cs
Assets/_Project/Scripts/Editor/SceneTools/SceneNavMeshConfigurator.cs
Assets/_Project/Scripts/Editor/SceneTools/BunkerSceneBuilder.cs
Assets/_Project/Scripts/Editor/CharacterTools/CharacterAnimatorConfigurator.cs
Assets/_Project/Scripts/Editor/ItemTools/ItemIconGeneratorTool.cs
Assets/_Project/Scripts/Editor/ZombieTools/ZombieSystemConfigurator.cs
Assets/_Project/Scripts/Editor/ZombieTools/ZombieSpawnerEditorTools.cs
Assets/_Project/Scripts/Editor/CoopTools/CoopMenuSetupTool.cs
Assets/_Project/Scripts/Runtime/Input/InputActions.cs
Файлы и папки, оставленные на месте
Путь	Причина
ProjectSettings/*	Не перемещались по условию задачи. Обновлён только EditorBuildSettings.asset, потому что пути сцен изменились.
Packages/*	Не перемещались по условию задачи.
.gitignore, .gitattributes, .git/*	Не относятся к Assets и не трогались.
Assets/Plugins/Easy Save 3/Resources/*	Это внутренние resources плагина Easy Save 3; проектная чистка Resources не должна ломать плагин.
Assets/_External/TextMesh Pro/Resources/* и examples	Это внутренние resources/examples TextMesh Pro; оставлены как внешний пакет.
Assets/_External/ParrelSync/LegacyPluginContainer	Пустой legacy-контейнер оставлен ради сохранения исходного .meta; его можно удалить отдельным осознанным cleanup-коммитом, если команда подтвердит.
Resources.Load после рефакторинга

Проектный код теперь использует RuntimeLoadedOnly/... для осознанных runtime-загрузок. Внешние вызовы Resources.Load внутри Easy Save 3 и TextMesh Pro не изменялись.

Остались два риска, которые не исправлялись автоматически:

CharacterSkinSelector ищет RuntimeLoadedOnly/Prefabs/Character/Skin, но такого project-owned prefab в Resources не было найдено. Существует похожий внешний prefab Assets/_External/PolygonApocalypse/Prefabs/Characters/Skin.prefab, но переносить его в Resources без отдельного решения рискованно.
CoopGameplaySync ищет RuntimeLoadedOnly/Prefabs/Coop/SM_Prop_Cross_02, затем старый fallback SM_Prop_Cross_02. В старом проектном Resources этот prefab также не был найден; внешний prefab есть в Assets/_External/PolygonApocalypse/Prefabs/Props/SM_Prop_Cross_02.prefab.
Проверки

Выполнены проверки после крупных блоков перемещений:

rg старых путей в проектных скриптах и ProjectSettings: совпадений нет
Assets/_Project/Resources: содержит только RuntimeLoadedOnly asset-set
Orphan .meta: 0
Папки без .meta: 0
Unity Editor.log: error CS / Compilation failed не найдены; последние записи script compilation есть

Примечание: Unity-проект был открыт во время файлового рефакторинга. После перемещений .csproj/solution могут оставаться со старыми путями до refresh/regenerate project files в Unity, поэтому MSBuild-проверка по текущим solution-файлам не является надёжной финальной проверкой структуры. Надёжная следующая проверка: дождаться завершения import/refresh в Unity, выполнить Assets > Open C# Project или regenerate project files, затем запустить EditMode/PlayMode tests.

Оставшиеся риски
В проекте остаётся широкий вызов Resources.LoadAll<ItemSO>(string.Empty). После сокращения project Resources это менее опасно, но технически он всё ещё сканирует все Resources в проекте и плагинах.
Два runtime-пути указывают на prefab, которые не были найдены в старом project Resources: Prefabs/Character/Skin и SM_Prop_Cross_02.
Сторонние ассеты физически перенесены в _External, GUID сохранены, но отдельные пакеты могут иметь editor tooling со строковыми путями, которые проявятся только при ручном запуске этих tools.
Большой объём renames требует внимательного review в Unity: сцены, prefabs и serialized references должны сохраниться по GUID, но инспекторная проверка основных сцен всё равно обязательна.
Рекомендуемые следующие шаги
Открыть Unity и дождаться полного import/refresh.
Regenerate project files, затем выполнить компиляцию из Unity.
Запустить основные сцены: MainScene, Bunker, City, Poligon, TestScene.
Проверить в Play Mode: загрузку меню coop/character creation, zombie spawning, save/load item registry, inventory UI, shooting, coop bootstrap.
Принять отдельное решение по CharacterSkinSelector и SM_Prop_Cross_02: либо заменить Resources.Load на serialized references/addressables, либо добавить project-owned runtime-loaded wrappers в RuntimeLoadedOnly.
Отдельным cleanup-коммитом можно удалить пустые legacy-контейнеры только после подтверждения, что их .meta не используется командой.
