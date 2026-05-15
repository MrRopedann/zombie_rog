using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class CoopGameplaySync : MonoBehaviour
{
    private const float PlayerSnapshotInterval = 0.05f;
    private const float ZombieSnapshotInterval = 0.1f;
    private const float ZombieImportantSnapshotCheckInterval = 0.03f;
    private const float ZombieImportantSnapshotMinInterval = 0.045f;
    private const float ZombieImportantMoveDistance = 0.45f;
    private const float ZombieImportantRotationAngle = 25f;
    private const float PredictedZombieDamageHoldSeconds = 1.25f;
    private const float ZombieHealthSyncTolerance = 0.01f;
    private const float ZombieRewindHistorySeconds = 1.0f;
    private const float ZombieRewindRecordInterval = 1f / 30f;
    private const float DefaultZombieShotRewindSeconds = 0.15f;
    private const float MaxServerShotOriginDistance = 8f;
    private const int MaxProcessedServerShotKeys = 1024;
    private const float AuthorityCheckInterval = 0.75f;
    private const float LootContainerScanInterval = 1.0f;
    private const float PlayerDeathCheckInterval = 0.2f;
    private const float ReviveHoldSeconds = 3f;
    private const float ReviveMarkerDelaySeconds = 5f;
    private const float ReviveHealthPercent = 0.5f;
    private const float SceneReloadDeathStateIgnoreSeconds = 1.5f;
    private const string MainMenuSceneName = "MainScene";
    private const int WorldItemIdStride = 100000;
    private const byte PlayerFlagAim = 1 << 0;
    private const byte PlayerFlagFire = 1 << 1;
    private const byte PlayerFlagDead = 1 << 2;
    private const byte GameOverChoiceRestart = 1;
    private const byte GameOverChoiceLobby = 2;
    private const float RemoteAimDistance = 80f;

    private static CoopGameplaySync instance;
    private static int networkDamageScopeDepth;
    private static int networkLootScopeDepth;
    private static int networkWorldItemScopeDepth;

    private readonly Dictionary<int, CoopNetworkIdentity> remotePlayers = new();
    private readonly Dictionary<int, CoopPlayerSnapshotRpc> lastPlayerSnapshots = new();
    private readonly Dictionary<int, uint> serverPlayerSnapshotSequences = new();
    private readonly Dictionary<int, uint> clientPlayerSnapshotSequences = new();
    private readonly Dictionary<int, uint> clientZombieStateSequences = new();
    private readonly Dictionary<int, float> clientPredictedZombieHealth = new();
    private readonly Dictionary<int, float> clientPredictedZombieHoldUntil = new();
    private readonly Dictionary<int, float> serverClientClockOffsets = new();
    private readonly Dictionary<int, List<ZombieRewindFrame>> zombieRewindHistory = new();
    private readonly Dictionary<int, ZombieHitbox[]> hostZombieHitboxCache = new();
    private readonly Dictionary<int, ZombieSnapshotState> lastZombieSnapshotStates = new();
    private readonly HashSet<ulong> processedServerShotKeys = new();
    private readonly Queue<ulong> processedServerShotKeyOrder = new();
    private readonly Dictionary<int, CoopNetworkIdentity> networkZombies = new();
    private readonly Dictionary<ZombieHealth, int> hostZombieIds = new();
    private readonly Dictionary<int, ZombieHealth> hostZombiesById = new();
    private readonly Dictionary<LootContainer, string> lootContainerIds = new();
    private readonly Dictionary<string, LootContainer> lootContainersById = new();
    private readonly Dictionary<string, uint> receivedLootSequences = new();
    private readonly Dictionary<string, ItemSO> itemsById = new();
    private readonly Dictionary<int, WorldItem> networkWorldItems = new();
    private readonly Dictionary<int, Dictionary<string, int>> serverPlayerInventoryItems = new();
    private readonly HashSet<int> pendingWorldItemPickups = new();
    private readonly Dictionary<ulong, Projectile> remoteProjectiles = new();
    private readonly Dictionary<int, CoopReviveMarker> reviveMarkers = new();
    private readonly Dictionary<int, Coroutine> reviveMarkerDelayCoroutines = new();
    private readonly Dictionary<int, byte> gameOverVotes = new();
    private readonly HashSet<int> deadPlayerIds = new();
    private readonly HashSet<int> notifiedLocalDeathIds = new();
    private readonly HashSet<int> spawnedZombieIds = new();
    private readonly HashSet<Weapon> subscribedWeapons = new();
    private readonly List<CoopAllyHud.AllyInfo> allyInfoBuffer = new();
    private readonly List<int> ownerIdBuffer = new();

    private CoopNetworkSession session;
    private GameObject playerPrefab;
    private GameObject reviveMarkerPrefab;
    private GameObject[] zombiePrefabs;
    private CoopNetworkIdentity localPlayerIdentity;
    private Transform localPlayerMotionTransform;
    private PlayerWeaponController localWeaponController;
    private CharacterStats localCharacterStats;
    private uint localSequence;
    private uint localShotSequence;
    private uint localProjectileSequence;
    private uint zombieSequence;
    private uint lootSequence;
    private int lastLocalZombieDamageRequestId;
    private int lastLocalZombieDamageRequestFrame = -1;
    private int nextZombieId = 1;
    private int nextWorldItemSequence = 1;
    private float nextPlayerSnapshotTime;
    private float nextZombieSnapshotTime;
    private float nextImportantZombieSnapshotCheckTime;
    private float nextZombieRewindRecordTime;
    private float nextAuthorityCheckTime;
    private float nextLootContainerScanTime;
    private float nextPlayerDeathCheckTime;
    private float ignoreDeathStateUntilTime;
    private bool gameOverVoteStarted;
    private bool gameOverResultApplied;

    public static bool IsApplyingNetworkDamage => networkDamageScopeDepth > 0;
    public static bool IsApplyingNetworkLootState => networkLootScopeDepth > 0;
    public static bool IsApplyingNetworkWorldItemState => networkWorldItemScopeDepth > 0;

    private sealed class ZombieRewindFrame
    {
        public float time;
        public ZombieRewindHitbox[] hitboxes;
    }

    private struct ZombieRewindHitbox
    {
        public ZombieHealth owner;
        public ZombieHitboxBodyPart bodyPart;
        public float damageMultiplier;
        public bool fallbackHitbox;
        public bool isSphere;
        public Vector3 start;
        public Vector3 end;
        public float radius;
    }

    private struct ZombieRewindHit
    {
        public ZombieHealth owner;
        public ZombieHitboxBodyPart bodyPart;
        public float damageMultiplier;
        public bool fallbackHitbox;
        public Vector3 point;
        public Vector3 normal;
        public float distance;
    }

    private struct ZombieSnapshotState
    {
        public Vector3 position;
        public Quaternion rotation;
        public float health;
        public int state;
        public byte flags;
        public float time;
    }

    public static void EnsureActive(CoopNetworkSession networkSession)
    {
        if (networkSession == null || !CoopSessionState.IsCoopSession)
            return;

        if (instance == null)
        {
            GameObject syncObject = new GameObject("Coop Gameplay Sync");
            instance = syncObject.AddComponent<CoopGameplaySync>();
            DontDestroyOnLoad(syncObject);
        }

        instance.session = networkSession;
    }

    public static bool TryRequestZombieDamage(ZombieHealth health, float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (instance == null || IsApplyingNetworkDamage || health == null || damage <= 0f)
            return false;

        if (!CoopSessionState.IsCoopSession || instance.session == null || instance.session.IsHost)
            return false;

        CoopNetworkIdentity identity = health.GetComponentInParent<CoopNetworkIdentity>();
        if (identity == null || identity.Kind != CoopNetworkObjectKind.Zombie || !identity.IsRemoteProxy)
            return false;

        instance.lastLocalZombieDamageRequestId = identity.NetworkId;
        instance.lastLocalZombieDamageRequestFrame = Time.frameCount;
        instance.SendDamageRequest(CoopNetworkObjectKind.Zombie, identity.NetworkId, damage, hitPoint, hitNormal);
        instance.ApplyPredictedZombieDamage(identity, health, damage, hitPoint, hitNormal);
        return true;
    }

    public static void RegisterLocalProjectileShot(Weapon weapon, Projectile projectile, ProjectileShotInfo shotInfo)
    {
        if (instance == null || weapon == null || projectile == null || !shotInfo.IsValid)
            return;

        if (!CoopSessionState.IsCoopSession || instance.session == null || instance.session.ClientWorld == null || !instance.session.ClientWorld.IsCreated)
            return;

        int ownerId = instance.session.LocalNetworkId;
        if (ownerId <= 0)
            return;

        int projectileId = (int)++instance.localProjectileSequence;
        projectile.ConfigureNetwork(ownerId, projectileId, true);

        instance.SendClientRpc(new CoopProjectileSpawnRpc
        {
            OwnerId = ownerId,
            ProjectileId = projectileId,
            WeaponIndex = instance.ResolveLocalWeaponIndex(weapon),
            Position = shotInfo.Position,
            Direction = shotInfo.Direction.sqrMagnitude > 0.001f ? shotInfo.Direction.normalized : Vector3.forward,
            Speed = shotInfo.Speed,
            Damage = shotInfo.Damage,
            Lifetime = shotInfo.Lifetime,
            Range = shotInfo.Range,
            HitMask = shotInfo.HitMask,
            UseGravity = shotInfo.UseGravity ? (byte)1 : (byte)0,
            AlignToVelocity = shotInfo.AlignToVelocity ? (byte)1 : (byte)0,
            ClientTime = Time.time,
            Sequence = instance.localProjectileSequence
        });
    }

    public static void NotifyProjectileImpact(Projectile projectile, Vector3 hitPoint, Vector3 hitNormal, bool suppressEffect)
    {
        if (instance == null || projectile == null || !projectile.NetworkLocalAuthority)
            return;

        if (!CoopSessionState.IsCoopSession || instance.session == null || instance.session.ClientWorld == null || !instance.session.ClientWorld.IsCreated)
            return;

        if (projectile.NetworkOwnerId <= 0 || projectile.NetworkProjectileId <= 0)
            return;

        instance.SendClientRpc(new CoopProjectileImpactRpc
        {
            OwnerId = projectile.NetworkOwnerId,
            ProjectileId = projectile.NetworkProjectileId,
            Position = hitPoint,
            Normal = hitNormal.sqrMagnitude > 0.001f ? hitNormal.normalized : Vector3.up,
            SuppressEffect = suppressEffect ? (byte)1 : (byte)0
        });
    }

    public static void NotifyPlayerHealthChanging(CharacterStats stats, float amount)
    {
        if (instance == null || IsApplyingNetworkDamage || stats == null || amount >= 0f)
            return;

        if (!CoopSessionState.IsCoopSession || instance.session == null || !instance.session.IsHost)
            return;

        CoopNetworkIdentity identity = stats.GetComponentInParent<CoopNetworkIdentity>();
        if (identity == null || identity.Kind != CoopNetworkObjectKind.Player || !identity.IsRemoteProxy || identity.OwnerId <= 0)
            return;

        float damage = -amount;
        float predictedHealth = Mathf.Max(0f, stats.currentHealth - damage);
        instance.SendPlayerDamage(identity.OwnerId, damage, predictedHealth, predictedHealth <= 0f);
    }

    public static void BeginNetworkDamageScope()
    {
        networkDamageScopeDepth++;
    }

    public static void EndNetworkDamageScope()
    {
        networkDamageScopeDepth = Mathf.Max(0, networkDamageScopeDepth - 1);
    }

    public static bool TryOpenRemoteLootContainer(LootContainer container, PlayerInventory inventory, CharacterStats stats)
    {
        if (instance == null || container == null || inventory == null || stats == null)
            return false;

        if (!CoopSessionState.IsCoopSession || instance.session == null || instance.session.IsHost)
            return false;

        if (!container.CanOpen(stats))
            return false;

        instance.RegisterLootContainer(container);
        instance.SendClientRpc(new CoopLootContainerRequestRpc
        {
            ContainerId = container.NetworkId
        });

        return container.OpenExistingFor(inventory, stats);
    }

    public static bool TryRequestLootTransfer(
        LootContainer container,
        PlayerInventory inventory,
        bool fromContainer,
        ItemSO item,
        int amount)
    {
        if (instance == null || container == null || inventory == null || item == null || amount <= 0)
            return false;

        if (!CoopSessionState.IsCoopSession || instance.session == null)
            return false;

        CharacterStats stats = inventory.GetComponentInParent<CharacterStats>();
        if (!container.CanOpen(stats))
            return true;

        int ownerId = instance.session.LocalNetworkId;
        if (ownerId <= 0)
            return true;

        instance.RegisterLootContainer(container);

        if (instance.session.IsHost)
        {
            instance.ApplyAuthoritativeLootTransfer(ownerId, container, inventory, fromContainer, item, amount, true);
            return true;
        }

        instance.SendClientRpc(new CoopLootTransferRequestRpc
        {
            ContainerId = container.NetworkId,
            ItemId = GetItemNetworkId(item),
            Amount = amount,
            FromContainer = fromContainer ? (byte)1 : (byte)0
        });

        return true;
    }

    public static bool TryPickupWorldItem(WorldItem worldItem, PlayerInventory inventory)
    {
        if (instance == null || worldItem == null || inventory == null || IsApplyingNetworkWorldItemState)
            return false;

        if (!CoopSessionState.IsCoopSession || instance.session == null)
            return false;

        if (worldItem.ItemData == null || worldItem.NetworkItemId <= 0)
            return false;

        int ownerId = instance.session.LocalNetworkId;
        if (ownerId <= 0 || !inventory.CanAddItem(worldItem.ItemData, worldItem.Amount))
            return true;

        if (instance.session.IsHost)
        {
            if (!inventory.AddItem(worldItem.ItemData, worldItem.Amount))
                return true;

            instance.AddServerInventoryItem(ownerId, worldItem.ItemData, worldItem.Amount);
            instance.RemoveNetworkWorldItem(worldItem.NetworkItemId);
            instance.BroadcastServerRpc(new CoopWorldItemPickupRpc
            {
                OwnerId = ownerId,
                NetworkItemId = worldItem.NetworkItemId,
                ItemId = GetItemNetworkId(worldItem.ItemData),
                Amount = worldItem.Amount,
                Approved = 1
            });
            return true;
        }

        if (!instance.pendingWorldItemPickups.Add(worldItem.NetworkItemId))
            return true;

        instance.SendClientRpc(new CoopWorldItemPickupRpc
        {
            OwnerId = ownerId,
            NetworkItemId = worldItem.NetworkItemId,
            ItemId = GetItemNetworkId(worldItem.ItemData),
            Amount = worldItem.Amount,
            Approved = 0
        });

        return true;
    }

    public static bool TryDropInventoryItem(PlayerInventory inventory, ItemSO item, int amount, Vector3 position, Quaternion rotation)
    {
        if (instance == null || inventory == null || item == null || amount <= 0 || IsApplyingNetworkWorldItemState)
            return false;

        if (!CoopSessionState.IsCoopSession || instance.session == null)
            return false;

        int ownerId = instance.session.LocalNetworkId;
        if (ownerId <= 0 || !inventory.ContainsItem(item))
            return true;

        Vector3 velocity = instance.CalculateWorldItemDropVelocity(inventory.transform, position);
        Vector3 angularVelocity = UnityEngine.Random.insideUnitSphere * 8f;

        if (instance.session.IsHost)
        {
            if (!inventory.RemoveItem(item, amount))
                return true;

            instance.RemoveServerInventoryItem(ownerId, item, amount, true);
            instance.SpawnAndBroadcastWorldItem(ownerId, item, amount, position, rotation, velocity, angularVelocity);
            return true;
        }

        instance.SendClientRpc(new CoopWorldItemSpawnRpc
        {
            OwnerId = ownerId,
            NetworkItemId = 0,
            ItemId = GetItemNetworkId(item),
            Position = position,
            Rotation = rotation,
            Velocity = velocity,
            AngularVelocity = angularVelocity,
            Amount = amount
        });

        return true;
    }

    public static void NotifyWorldItemDropped(WorldItem worldItem, ItemSO item, int amount)
    {
        if (instance == null || worldItem == null || item == null || IsApplyingNetworkWorldItemState)
            return;

        if (!CoopSessionState.IsCoopSession || instance.session == null)
            return;

        instance.RegisterDroppedWorldItem(worldItem, item, amount);
    }

    public static void NotifyWorldItemPickedUp(WorldItem worldItem)
    {
        if (instance == null || worldItem == null || IsApplyingNetworkWorldItemState)
            return;

        if (!CoopSessionState.IsCoopSession || instance.session == null || worldItem.NetworkItemId <= 0)
            return;

        instance.SendWorldItemPickup(worldItem.NetworkItemId);
    }

    public static void RequestPlayerRevive(int deadOwnerId, Vector3 revivePosition, Quaternion reviveRotation)
    {
        if (instance == null || deadOwnerId <= 0 || instance.session == null || !CoopSessionState.IsCoopSession)
            return;

        int reviverOwnerId = instance.session.LocalNetworkId;
        if (reviverOwnerId <= 0 || reviverOwnerId == deadOwnerId)
            return;

        if (instance.session.IsHost)
        {
            instance.CompletePlayerRevive(deadOwnerId, revivePosition, reviveRotation);
            return;
        }

        instance.SendClientRpc(new CoopPlayerReviveRequestRpc
        {
            DeadOwnerId = deadOwnerId,
            ReviverOwnerId = reviverOwnerId,
            Position = revivePosition,
            Rotation = reviveRotation
        });
    }

    public static void SubmitGameOverVote(int choice)
    {
        if (instance == null || instance.session == null || !CoopSessionState.IsCoopSession)
            return;

        byte resolvedChoice = choice == GameOverChoiceLobby ? GameOverChoiceLobby : GameOverChoiceRestart;
        int ownerId = instance.session.LocalNetworkId;
        if (ownerId <= 0)
            return;

        DeathChoiceMenu.SetStatus("Голос принят. Ждем остальных игроков...");

        if (instance.session.IsHost)
        {
            instance.RegisterGameOverVote(ownerId, resolvedChoice);
            return;
        }

        instance.SendClientRpc(new CoopGameOverVoteRpc
        {
            OwnerId = ownerId,
            Choice = resolvedChoice
        });
    }

    public static bool TryGetLocalReviveInteractor(
        out int ownerId,
        out CharacterStats stats,
        out Transform playerTransform,
        out Camera playerCamera,
        out InputsController inputs)
    {
        ownerId = 0;
        stats = null;
        playerTransform = null;
        playerCamera = null;
        inputs = null;

        if (instance == null || instance.session == null || !CoopSessionState.IsCoopSession)
            return false;

        ownerId = instance.session.LocalNetworkId;
        if (ownerId <= 0)
            return false;

        instance.EnsureLocalPlayer(ownerId);
        stats = instance.localCharacterStats;
        playerTransform = instance.localPlayerMotionTransform != null ? instance.localPlayerMotionTransform : stats != null ? stats.transform : null;
        inputs = playerTransform != null
            ? playerTransform.GetComponent<InputsController>() ?? playerTransform.GetComponentInChildren<InputsController>(true)
            : null;

        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                {
                    playerCamera = cameras[i];
                    break;
                }
            }
        }

        return stats != null && playerTransform != null && !stats.IsDead;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadPrefabs();
        LoadItemCache();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Update()
    {
        if (session == null || !CoopSessionState.IsCoopSession)
            return;

        ReceivePlayerSnapshotsOnServer();
        ReceiveWeaponShotsOnServer();
        ReceiveProjectileSpawnsOnServer();
        ReceiveProjectileImpactsOnServer();
        ReceiveDamageRequestsOnServer();
        ReceivePlayerDeathNoticesOnServer();
        ReceivePlayerReviveRequestsOnServer();
        ReceiveGameOverVotesOnServer();

        ReceivePlayerSnapshotsOnClient();
        ReceiveWeaponShotsOnClient();
        ReceiveProjectileSpawnsOnClient();
        ReceiveProjectileImpactsOnClient();
        ReceivePlayerDamageOnClient();
        ReceivePlayerDeathNoticesOnClient();
        ReceivePlayerRevivesOnClient();
        ReceiveGameOverVoteStartsOnClient();
        ReceiveGameOverResultsOnClient();
        ReceiveZombieSpawnsOnClient();
        ReceiveZombieSnapshotsOnClient();
        ReceiveZombieDamageEventsOnClient();
        ReceiveLootContainerRequestsOnServer();
        ReceiveLootTransferRequestsOnServer();
        ReceiveLootContainerStateOnServer();
        ReceiveLootContainerStateOnClient();
        ReceiveLootTransferResultsOnClient();
        ReceiveWorldItemSpawnsOnServer();
        ReceiveWorldItemSpawnsOnClient();
        ReceiveWorldItemPickupsOnServer();
        ReceiveWorldItemPickupsOnClient();

        int localOwnerId = session.LocalNetworkId;
        if (localOwnerId <= 0)
            return;

        EnsureLocalPlayer(localOwnerId);
        ConfigureSceneAuthority();
        RegisterLootContainers();
        UpdateAllyHud(localOwnerId);
        UpdatePlayerDeathState(localOwnerId);
        SendLocalPlayerSnapshot(localOwnerId);

        if (session.IsHost)
        {
            RegisterHostZombies();
            RecordZombieRewindHistory();
            SendImportantZombieSnapshots();
            SendZombieSnapshots();
        }
    }

    private void LoadPrefabs()
    {
        if (playerPrefab == null)
            playerPrefab = Resources.Load<GameObject>("Prefabs/Character/Player");

        if (reviveMarkerPrefab == null)
        {
            reviveMarkerPrefab = Resources.Load<GameObject>("Prefabs/Coop/SM_Prop_Cross_02")
                ?? Resources.Load<GameObject>("SM_Prop_Cross_02");

#if UNITY_EDITOR
            if (reviveMarkerPrefab == null)
            {
                reviveMarkerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/PolygonApocalypse/Prefabs/Props/SM_Prop_Cross_02.prefab");
            }
#endif
        }

        if (zombiePrefabs == null || zombiePrefabs.Length == 0)
            zombiePrefabs = Resources.LoadAll<GameObject>("Prefabs/Zombie");
    }

    private void LoadItemCache()
    {
        if (itemsById.Count > 0)
            return;

        RegisterItems(Resources.LoadAll<ItemSO>("Data"));
        RegisterItems(Resources.LoadAll<ItemSO>("Data/Item"));
    }

    private void RegisterItems(ItemSO[] items)
    {
        if (items == null)
            return;

        for (int i = 0; i < items.Length; i++)
        {
            ItemSO item = items[i];
            if (item == null)
                continue;

            RegisterItemKey(item.itemID, item);
            RegisterItemKey(item.name, item);
        }
    }

    private void RegisterItemKey(string key, ItemSO item)
    {
        if (string.IsNullOrWhiteSpace(key) || item == null)
            return;

        itemsById[key.Trim()] = item;
    }

    private void EnsureLocalPlayer(int ownerId)
    {
        if (localPlayerIdentity != null && localPlayerIdentity.OwnerId == ownerId)
            return;

        Transform playerMotionTransform = FindLocalPlayerMotionTransform();
        if (playerMotionTransform == null)
            return;

        localPlayerIdentity = CoopNetworkIdentity.GetOrAdd(playerMotionTransform.gameObject);
        localPlayerIdentity.Configure(CoopNetworkObjectKind.Player, ownerId, ownerId, 0, true, false);
        localPlayerMotionTransform = playerMotionTransform;

        Transform playerRoot = playerMotionTransform.root != null ? playerMotionTransform.root : playerMotionTransform;
        localWeaponController = playerRoot.GetComponentInChildren<PlayerWeaponController>(true);
        localCharacterStats = playerRoot.GetComponentInChildren<CharacterStats>(true);

        if (localCharacterStats != null)
            localCharacterStats.playerID = ownerId;

        SubscribeLocalWeapons();
    }

    private static Transform FindLocalPlayerMotionTransform()
    {
        ThirdPersonController[] controllers = FindObjectsOfType<ThirdPersonController>(true);
        foreach (ThirdPersonController controller in controllers)
        {
            CoopNetworkIdentity identity = controller.GetComponentInParent<CoopNetworkIdentity>();
            if (identity != null && identity.IsRemoteProxy)
                continue;

            if (identity == null || identity.HasLocalAuthority)
                return controller.transform;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer == null)
            return null;

        CoopNetworkIdentity taggedIdentity = taggedPlayer.GetComponentInParent<CoopNetworkIdentity>();
        if (taggedIdentity != null && taggedIdentity.IsRemoteProxy)
            return null;

        ThirdPersonController childController = taggedPlayer.GetComponentInChildren<ThirdPersonController>(true);
        if (childController != null)
            return childController.transform;

        CharacterController childCharacterController = taggedPlayer.GetComponentInChildren<CharacterController>(true);
        if (childCharacterController != null)
            return childCharacterController.transform;

        return taggedPlayer.transform;
    }

    private void SubscribeLocalWeapons()
    {
        if (localWeaponController == null)
            return;

        localWeaponController.CurrentWeaponChanged -= HandleCurrentWeaponChanged;
        localWeaponController.CurrentWeaponChanged += HandleCurrentWeaponChanged;

        IReadOnlyList<Weapon> weapons = localWeaponController.Weapons;
        if (weapons == null)
            return;

        for (int i = 0; i < weapons.Count; i++)
        {
            Weapon weapon = weapons[i];
            if (weapon == null || subscribedWeapons.Contains(weapon))
                continue;

            weapon.ShotFired += HandleLocalWeaponShot;
            subscribedWeapons.Add(weapon);
        }
    }

    private void HandleCurrentWeaponChanged(Weapon weapon)
    {
        SubscribeLocalWeapons();
        SendLocalPlayerSnapshot(session != null ? session.LocalNetworkId : 0, force: true);
    }

    private void HandleLocalWeaponShot(Weapon weapon)
    {
        if (session == null || session.ClientWorld == null || !session.ClientWorld.IsCreated)
            return;

        int ownerId = session.LocalNetworkId;
        if (ownerId <= 0)
            return;

        Transform muzzle = ResolveMuzzle(weapon);
        Vector3 origin = muzzle != null ? muzzle.position : weapon.transform.position;
        Vector3 direction = muzzle != null ? muzzle.forward : weapon.transform.forward;
        float damage = 0f;
        float range = 0f;
        int hitMask = 0;
        int predictedZombieId = 0;
        bool hasHitscanInfo = TryGetHitscanShotInfo(weapon, out HitscanShotInfo hitscanInfo);

        if (hasHitscanInfo)
        {
            origin = hitscanInfo.Origin;
            direction = hitscanInfo.Direction;
            damage = hitscanInfo.Damage;
            range = hitscanInfo.Range;
            hitMask = hitscanInfo.HitMask;
            predictedZombieId = hitscanInfo.HasPredictedZombieHit ? hitscanInfo.PredictedZombieId : 0;
        }

        if (predictedZombieId <= 0 && lastLocalZombieDamageRequestFrame == Time.frameCount)
            predictedZombieId = lastLocalZombieDamageRequestId;

        SendClientRpc(new CoopWeaponShotRpc
        {
            OwnerId = ownerId,
            WeaponIndex = localWeaponController != null ? localWeaponController.SelectedWeaponIndex : 0,
            Origin = origin,
            Direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward,
            Damage = damage,
            Range = range,
            HitMask = hitMask,
            PredictedZombieId = predictedZombieId,
            Hitscan = hasHitscanInfo ? (byte)1 : (byte)0,
            PredictedZombieHit = predictedZombieId > 0 ? (byte)1 : (byte)0,
            ClientTime = Time.time,
            Sequence = ++localShotSequence
        });
    }

    private static bool TryGetHitscanShotInfo(Weapon weapon, out HitscanShotInfo shotInfo)
    {
        shotInfo = default;

        if (weapon == null)
            return false;

        RaycastShooter shooter = weapon.GetComponent<RaycastShooter>() ?? weapon.GetComponentInChildren<RaycastShooter>(true);
        return shooter != null && shooter.TryGetLastShotInfo(out shotInfo);
    }

    private int ResolveLocalWeaponIndex(Weapon weapon)
    {
        if (weapon == null || localWeaponController == null)
            return 0;

        IReadOnlyList<Weapon> weapons = localWeaponController.Weapons;
        if (weapons == null)
            return localWeaponController.SelectedWeaponIndex;

        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] == weapon)
                return i;
        }

        return localWeaponController.SelectedWeaponIndex;
    }

    private static Transform ResolveMuzzle(Weapon weapon)
    {
        if (weapon == null)
            return null;

        WeaponIKGrip grip = weapon.GetComponent<WeaponIKGrip>() ?? weapon.GetComponentInChildren<WeaponIKGrip>(true);
        return grip != null && grip.Muzzle != null ? grip.Muzzle : weapon.transform;
    }

    private void SendLocalPlayerSnapshot(int ownerId, bool force = false)
    {
        if (ownerId <= 0 || localPlayerIdentity == null || session.ClientWorld == null || !session.ClientWorld.IsCreated)
            return;

        if (!force && Time.time < nextPlayerSnapshotTime)
            return;

        nextPlayerSnapshotTime = Time.time + PlayerSnapshotInterval;

        Transform playerTransform = ResolvePlayerMotionTransform(localPlayerMotionTransform != null ? localPlayerMotionTransform.gameObject : localPlayerIdentity.gameObject);
        CharacterController characterController = playerTransform.GetComponent<CharacterController>() ?? playerTransform.GetComponentInChildren<CharacterController>();
        InputsController inputs = playerTransform.GetComponent<InputsController>() ?? playerTransform.GetComponentInChildren<InputsController>(true);

        byte flags = 0;
        if (inputs != null && inputs.aim)
            flags |= PlayerFlagAim;
        if (inputs != null && inputs.fireHeld)
            flags |= PlayerFlagFire;
        if (localCharacterStats != null && localCharacterStats.IsDead)
            flags |= PlayerFlagDead;

        SendClientRpc(new CoopPlayerSnapshotRpc
        {
            OwnerId = ownerId,
            Position = playerTransform.position,
            Rotation = playerTransform.rotation,
            MoveSpeed = characterController != null ? characterController.velocity.magnitude : 0f,
            WeaponIndex = localWeaponController != null ? localWeaponController.SelectedWeaponIndex : 0,
            Flags = flags,
            Health = localCharacterStats != null ? localCharacterStats.currentHealth : 100f,
            MaxHealth = localCharacterStats != null ? Mathf.Max(1f, localCharacterStats.MaxHealth) : 100f,
            PlayerName = localCharacterStats != null && !string.IsNullOrWhiteSpace(localCharacterStats.playerName)
                ? localCharacterStats.playerName
                : $"Player {ownerId}",
            ClientTime = Time.time,
            Sequence = ++localSequence
        });
    }

    private void ReceivePlayerSnapshotsOnServer()
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopPlayerSnapshotRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopPlayerSnapshotRpc> snapshots = query.ToComponentDataArray<CoopPlayerSnapshotRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> requests = query.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopPlayerSnapshotRpc snapshot = snapshots[i];
            int ownerId = GetConnectionNetworkId(world, requests[i].SourceConnection);
            if (ownerId > 0)
                snapshot.OwnerId = ownerId;

            UpdateClientClockOffset(snapshot.OwnerId, snapshot.ClientTime);

            if (!ShouldAcceptPlayerSnapshot(snapshot, serverPlayerSnapshotSequences))
            {
                entityManager.DestroyEntity(entities[i]);
                continue;
            }

            lastPlayerSnapshots[snapshot.OwnerId] = snapshot;
            BroadcastServerRpc(snapshot);
            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceivePlayerSnapshotsOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopPlayerSnapshotRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopPlayerSnapshotRpc> snapshots = query.ToComponentDataArray<CoopPlayerSnapshotRpc>(Allocator.Temp);
        int localOwnerId = session.LocalNetworkId;

        for (int i = 0; i < entities.Length; i++)
        {
            CoopPlayerSnapshotRpc snapshot = snapshots[i];
            if (snapshot.OwnerId > 0 && snapshot.OwnerId != localOwnerId)
            {
                if (!ShouldAcceptPlayerSnapshot(snapshot, clientPlayerSnapshotSequences))
                {
                    entityManager.DestroyEntity(entities[i]);
                    continue;
                }

                lastPlayerSnapshots[snapshot.OwnerId] = snapshot;
                ApplyRemotePlayerSnapshot(snapshot);
            }

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ApplyRemotePlayerSnapshot(CoopPlayerSnapshotRpc snapshot)
    {
        bool isDead = (snapshot.Flags & PlayerFlagDead) != 0 || snapshot.Health <= 0f;
        if (ShouldIgnoreDeathStateNow() && isDead)
            return;

        CoopNetworkIdentity identity = GetOrCreateRemotePlayer(snapshot.OwnerId, snapshot.Position, snapshot.Rotation);
        if (identity == null)
            return;

        CoopNetworkTransform networkTransform = identity.GetComponent<CoopNetworkTransform>();
        if (networkTransform == null)
            networkTransform = identity.gameObject.AddComponent<CoopNetworkTransform>();

        networkTransform.SetTarget(snapshot.Position, snapshot.Rotation);

        PlayerWeaponController weaponController = GetNetworkComponent<PlayerWeaponController>(identity);
        if (weaponController != null)
            weaponController.SetSelectedWeaponIndex(snapshot.WeaponIndex);

        CharacterStats stats = GetNetworkComponent<CharacterStats>(identity);
        bool wasDeadOrRagdoll = IsRemotePlayerInDeathState(snapshot.OwnerId, identity, stats);

        if (isDead)
        {
            if (stats != null)
                stats.ApplyNetworkHealth(snapshot.Health);

            MarkPlayerDead(snapshot.OwnerId, snapshot.Position, snapshot.Rotation, false);
            ActivateRemotePlayerRagdoll(identity);
        }
        else
        {
            if (stats != null)
            {
                if (stats.IsDead)
                    stats.Revive(Mathf.Max(1f, snapshot.Health));
                else
                    stats.ApplyNetworkHealth(snapshot.Health);
            }

            if (wasDeadOrRagdoll)
                RestoreRemotePlayerFromDeathState(identity, snapshot);
        }

        ApplyRemotePlayerVisualState(identity, snapshot);
    }

    private bool ShouldAcceptPlayerSnapshot(CoopPlayerSnapshotRpc snapshot, Dictionary<int, uint> sequenceMap)
    {
        if (snapshot.OwnerId <= 0)
            return false;

        bool isDead = (snapshot.Flags & PlayerFlagDead) != 0 || snapshot.Health <= 0f;
        if (ShouldIgnoreDeathStateNow() && isDead)
            return false;

        if (sequenceMap.TryGetValue(snapshot.OwnerId, out uint lastSequence) &&
            snapshot.Sequence != 0 &&
            snapshot.Sequence <= lastSequence)
        {
            return false;
        }

        sequenceMap[snapshot.OwnerId] = snapshot.Sequence;
        return true;
    }

    private void UpdateClientClockOffset(int ownerId, float clientTime)
    {
        if (ownerId <= 0 || clientTime <= 0f)
            return;

        float rawOffset = Time.time - clientTime;
        if (serverClientClockOffsets.TryGetValue(ownerId, out float existingOffset))
            serverClientClockOffsets[ownerId] = Mathf.Lerp(existingOffset, rawOffset, 0.1f);
        else
            serverClientClockOffsets[ownerId] = rawOffset;
    }

    private bool ShouldIgnoreDeathStateNow()
    {
        return Time.time < ignoreDeathStateUntilTime;
    }

    private bool IsRemotePlayerInDeathState(int ownerId, CoopNetworkIdentity identity, CharacterStats stats)
    {
        if (deadPlayerIds.Contains(ownerId))
            return true;

        if (stats != null && stats.IsDead)
            return true;

        Animator animator = GetNetworkComponent<Animator>(identity);
        return animator != null && !animator.enabled;
    }

    private void RestoreRemotePlayerFromDeathState(CoopNetworkIdentity identity, CoopPlayerSnapshotRpc snapshot)
    {
        deadPlayerIds.Remove(snapshot.OwnerId);
        notifiedLocalDeathIds.Remove(snapshot.OwnerId);
        RemoveReviveMarker(snapshot.OwnerId);
        RestoreDeadBody(snapshot.OwnerId);

        Quaternion rotation = (Quaternion)snapshot.Rotation;
        ThirdPersonController controller = GetNetworkComponent<ThirdPersonController>(identity);
        if (controller != null)
        {
            controller.ReviveFromNetwork(snapshot.Position, rotation, false);
            return;
        }

        RagdollController ragdoll = GetNetworkComponent<RagdollController>(identity);
        if (ragdoll != null)
            ragdoll.DisableRagdoll();

        Animator animator = GetNetworkComponent<Animator>(identity);
        if (animator != null)
        {
            animator.enabled = true;
            animator.SetBool("isDead", false);
        }
    }

    private static void ApplyRemotePlayerVisualState(CoopNetworkIdentity identity, CoopPlayerSnapshotRpc snapshot)
    {
        bool isAiming = (snapshot.Flags & PlayerFlagAim) != 0;
        bool isFiring = (snapshot.Flags & PlayerFlagFire) != 0;
        bool isDead = (snapshot.Flags & PlayerFlagDead) != 0;

        InputsController inputs = GetNetworkComponent<InputsController>(identity);
        if (inputs != null)
        {
            inputs.aim = isAiming;
            inputs.fireHeld = isFiring;
        }

        Animator animator = GetNetworkComponent<Animator>(identity);
        if (animator != null)
        {
            float motionSpeed = snapshot.MoveSpeed > 0.05f ? 1f : 0f;
            animator.SetFloat("Speed", snapshot.MoveSpeed);
            animator.SetFloat("MotionSpeed", motionSpeed);
            animator.SetBool("Grounded", true);
            animator.SetBool("Jump", false);
            animator.SetBool("FreeFall", false);
            animator.SetBool("isDead", false);
        }

        if (isDead)
            ActivateRemotePlayerRagdoll(identity);

        PlayerWeaponIKController ikController = GetNetworkComponent<PlayerWeaponIKController>(identity);
        if (ikController != null)
        {
            Quaternion rotation = (Quaternion)snapshot.Rotation;
            Vector3 forward = rotation * Vector3.forward;
            if (forward.sqrMagnitude <= 0.001f)
                forward = identity.transform.forward;

            ikController.SetExternalAimPoint((Vector3)snapshot.Position + forward.normalized * RemoteAimDistance);
        }
    }

    private void UpdateAllyHud(int localOwnerId)
    {
        allyInfoBuffer.Clear();

        foreach (KeyValuePair<int, CoopPlayerSnapshotRpc> pair in lastPlayerSnapshots)
        {
            if (pair.Key <= 0 || pair.Key == localOwnerId)
                continue;

            CoopPlayerSnapshotRpc snapshot = pair.Value;
            float maxHealth = Mathf.Max(1f, snapshot.MaxHealth);
            float health = Mathf.Clamp(snapshot.Health, 0f, maxHealth);

            if (remotePlayers.TryGetValue(pair.Key, out CoopNetworkIdentity identity) && identity != null)
            {
                CharacterStats stats = GetNetworkComponent<CharacterStats>(identity);
                if (stats != null)
                {
                    maxHealth = Mathf.Max(1f, stats.MaxHealth);
                    health = stats.IsDead ? 0f : Mathf.Clamp(stats.currentHealth, 0f, maxHealth);
                }
            }

            if (deadPlayerIds.Contains(pair.Key) || (snapshot.Flags & PlayerFlagDead) != 0)
                health = 0f;

            allyInfoBuffer.Add(new CoopAllyHud.AllyInfo(
                pair.Key,
                string.IsNullOrWhiteSpace(snapshot.PlayerName.ToString()) ? $"Player {pair.Key}" : snapshot.PlayerName.ToString(),
                health,
                maxHealth));
        }

        CoopAllyHud.EnsureActive().SetAllies(allyInfoBuffer);
    }

    private void UpdateCachedPlayerHealth(int ownerId, float health, bool dead)
    {
        if (ownerId <= 0)
            return;

        bool resolvedDead = dead || health <= 0f;
        float resolvedHealth = resolvedDead ? 0f : Mathf.Max(0f, health);

        if (!lastPlayerSnapshots.TryGetValue(ownerId, out CoopPlayerSnapshotRpc snapshot))
            snapshot = CreatePlayerSnapshotPlaceholder(ownerId);

        snapshot.OwnerId = ownerId;
        snapshot.Health = resolvedHealth;

        if (snapshot.MaxHealth <= 0f)
            snapshot.MaxHealth = ResolveKnownPlayerMaxHealth(ownerId, resolvedHealth);

        if (string.IsNullOrWhiteSpace(snapshot.PlayerName.ToString()))
            snapshot.PlayerName = ResolveKnownPlayerName(ownerId);

        snapshot.Flags = resolvedDead
            ? (byte)(snapshot.Flags | PlayerFlagDead)
            : (byte)(snapshot.Flags & ~PlayerFlagDead);

        lastPlayerSnapshots[ownerId] = snapshot;
    }

    private CoopPlayerSnapshotRpc CreatePlayerSnapshotPlaceholder(int ownerId)
    {
        return new CoopPlayerSnapshotRpc
        {
            OwnerId = ownerId,
            Health = ResolveKnownPlayerMaxHealth(ownerId, 100f),
            MaxHealth = ResolveKnownPlayerMaxHealth(ownerId, 100f),
            PlayerName = ResolveKnownPlayerName(ownerId)
        };
    }

    private float ResolveKnownPlayerMaxHealth(int ownerId, float fallback)
    {
        int localOwnerId = session != null ? session.LocalNetworkId : 0;
        if (ownerId == localOwnerId && localCharacterStats != null)
            return Mathf.Max(1f, localCharacterStats.MaxHealth);

        if (remotePlayers.TryGetValue(ownerId, out CoopNetworkIdentity identity) && identity != null)
        {
            CharacterStats stats = GetNetworkComponent<CharacterStats>(identity);
            if (stats != null)
                return Mathf.Max(1f, stats.MaxHealth);
        }

        return Mathf.Max(1f, fallback, 100f);
    }

    private string ResolveKnownPlayerName(int ownerId)
    {
        int localOwnerId = session != null ? session.LocalNetworkId : 0;
        if (ownerId == localOwnerId && localCharacterStats != null && !string.IsNullOrWhiteSpace(localCharacterStats.playerName))
            return localCharacterStats.playerName;

        if (remotePlayers.TryGetValue(ownerId, out CoopNetworkIdentity identity) && identity != null)
        {
            CharacterStats stats = GetNetworkComponent<CharacterStats>(identity);
            if (stats != null && !string.IsNullOrWhiteSpace(stats.playerName))
                return stats.playerName;
        }

        if (lastPlayerSnapshots.TryGetValue(ownerId, out CoopPlayerSnapshotRpc snapshot) &&
            !string.IsNullOrWhiteSpace(snapshot.PlayerName.ToString()))
        {
            return snapshot.PlayerName.ToString();
        }

        return $"Player {ownerId}";
    }

    private void UpdatePlayerDeathState(int localOwnerId)
    {
        if (Time.time < nextPlayerDeathCheckTime)
            return;

        nextPlayerDeathCheckTime = Time.time + PlayerDeathCheckInterval;

        if (localCharacterStats != null)
        {
            if (localCharacterStats.IsDead)
            {
                Vector3 position = localPlayerMotionTransform != null ? localPlayerMotionTransform.position : localCharacterStats.transform.position;
                Quaternion rotation = localPlayerMotionTransform != null ? localPlayerMotionTransform.rotation : localCharacterStats.transform.rotation;

                if (!deadPlayerIds.Contains(localOwnerId))
                    MarkPlayerDead(localOwnerId, position, rotation, session.IsHost);

                if (!session.IsHost && notifiedLocalDeathIds.Add(localOwnerId))
                {
                    SendClientRpc(new CoopPlayerDeathNoticeRpc
                    {
                        DeadOwnerId = localOwnerId,
                        Position = position,
                        Rotation = rotation
                    });
                }
            }
            else
            {
                notifiedLocalDeathIds.Remove(localOwnerId);
            }
        }

        if (session.IsHost)
            UpdateHostGameOverState(localOwnerId);
    }

    private void UpdateHostGameOverState(int localOwnerId)
    {
        if (gameOverResultApplied || gameOverVoteStarted)
            return;

        CollectKnownOwnerIds(localOwnerId);
        int expectedPlayers = Mathf.Max(1, session != null ? session.CurrentPlayers : ownerIdBuffer.Count);
        if (ownerIdBuffer.Count < expectedPlayers)
            return;

        for (int i = 0; i < ownerIdBuffer.Count; i++)
        {
            if (!IsPlayerDead(ownerIdBuffer[i], localOwnerId))
                return;
        }

        StartGameOverVote();
    }

    private void CollectKnownOwnerIds(int localOwnerId)
    {
        ownerIdBuffer.Clear();
        AddKnownOwnerId(localOwnerId);

        foreach (int ownerId in remotePlayers.Keys)
            AddKnownOwnerId(ownerId);

        foreach (int ownerId in lastPlayerSnapshots.Keys)
            AddKnownOwnerId(ownerId);

        foreach (int ownerId in deadPlayerIds)
            AddKnownOwnerId(ownerId);
    }

    private void AddKnownOwnerId(int ownerId)
    {
        if (ownerId <= 0 || ownerIdBuffer.Contains(ownerId))
            return;

        ownerIdBuffer.Add(ownerId);
    }

    private bool IsPlayerDead(int ownerId, int localOwnerId)
    {
        if (ownerId == localOwnerId)
            return localCharacterStats != null && localCharacterStats.IsDead;

        if (lastPlayerSnapshots.TryGetValue(ownerId, out CoopPlayerSnapshotRpc snapshot))
            return (snapshot.Flags & PlayerFlagDead) != 0 || snapshot.Health <= 0f;

        return deadPlayerIds.Contains(ownerId);
    }

    private void MarkPlayerDead(int ownerId, Vector3 position, Quaternion rotation, bool broadcastFromServer)
    {
        if (ownerId <= 0)
            return;

        bool wasKnownDead = deadPlayerIds.Contains(ownerId);
        deadPlayerIds.Add(ownerId);
        UpdateCachedPlayerHealth(ownerId, 0f, true);

        if (reviveMarkers.ContainsKey(ownerId))
            RemoveDeadBodyRagdoll(ownerId);
        else
            ScheduleReviveMarkerReplacement(ownerId, position, rotation);

        if (broadcastFromServer && !wasKnownDead)
        {
            BroadcastServerRpc(new CoopPlayerDeathNoticeRpc
            {
                DeadOwnerId = ownerId,
                Position = position,
                Rotation = rotation
            });
        }
    }

    private void EnsureReviveMarker(int ownerId, Vector3 position, Quaternion rotation)
    {
        Vector3 markerPosition = ResolveGroundedMarkerPosition(position);

        if (reviveMarkers.TryGetValue(ownerId, out CoopReviveMarker existing) && existing != null)
        {
            existing.transform.SetPositionAndRotation(markerPosition, rotation);
            existing.Configure(ownerId, ReviveHoldSeconds);
            return;
        }

        LoadPrefabs();
        GameObject markerObject = reviveMarkerPrefab != null
            ? Instantiate(reviveMarkerPrefab, markerPosition, rotation)
            : CreateFallbackReviveMarker(markerPosition, rotation);

        markerObject.name = $"Revive Cross {ownerId}";
        EnsureMarkerCollider(markerObject);

        CoopReviveMarker marker = markerObject.GetComponent<CoopReviveMarker>();
        if (marker == null)
            marker = markerObject.AddComponent<CoopReviveMarker>();

        marker.Configure(ownerId, ReviveHoldSeconds);
        reviveMarkers[ownerId] = marker;
    }

    private static Vector3 ResolveGroundedMarkerPosition(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * 2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;

        return position;
    }

    private static void EnsureMarkerCollider(GameObject markerObject)
    {
        if (markerObject == null)
            return;

        Collider collider = markerObject.GetComponent<Collider>();
        if (collider == null)
        {
            BoxCollider box = markerObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1f, 0f);
            box.size = new Vector3(1.25f, 2f, 1.25f);
            collider = box;
        }

        collider.isTrigger = true;
    }

    private static GameObject CreateFallbackReviveMarker(Vector3 position, Quaternion rotation)
    {
        GameObject root = new GameObject("Revive Cross Fallback");
        root.transform.SetPositionAndRotation(position, rotation);

        GameObject vertical = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vertical.name = "Vertical";
        vertical.transform.SetParent(root.transform, false);
        vertical.transform.localPosition = new Vector3(0f, 1f, 0f);
        vertical.transform.localScale = new Vector3(0.18f, 2f, 0.18f);
        Destroy(vertical.GetComponent<Collider>());

        GameObject horizontal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        horizontal.name = "Horizontal";
        horizontal.transform.SetParent(root.transform, false);
        horizontal.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        horizontal.transform.localScale = new Vector3(1.15f, 0.16f, 0.16f);
        Destroy(horizontal.GetComponent<Collider>());

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = new Color(0.74f, 0.74f, 0.68f, 1f);

        return root;
    }

    private void RemoveReviveMarker(int ownerId)
    {
        CancelReviveMarkerDelay(ownerId);

        if (!reviveMarkers.TryGetValue(ownerId, out CoopReviveMarker marker))
            return;

        reviveMarkers.Remove(ownerId);
        if (marker != null)
            Destroy(marker.gameObject);
    }

    private static void ActivateRemotePlayerRagdoll(CoopNetworkIdentity identity)
    {
        if (identity == null)
            return;

        ThirdPersonController controller = GetNetworkComponent<ThirdPersonController>(identity);
        if (controller != null)
        {
            controller.ActivateNetworkRagdollDeath();
            return;
        }

        Animator animator = GetNetworkComponent<Animator>(identity);
        if (animator != null)
        {
            animator.SetBool("isDead", false);
            animator.enabled = false;
        }

        RagdollController ragdoll = GetNetworkComponent<RagdollController>(identity);
        if (ragdoll != null)
            ragdoll.EnableRagdoll();
    }

    private void CompletePlayerRevive(int deadOwnerId, Vector3 requestedPosition, Quaternion requestedRotation)
    {
        if (deadOwnerId <= 0 || !deadPlayerIds.Contains(deadOwnerId))
            return;

        Vector3 revivePosition = reviveMarkers.TryGetValue(deadOwnerId, out CoopReviveMarker marker) && marker != null
            ? marker.transform.position
            : requestedPosition;
        Quaternion reviveRotation = requestedRotation;
        float health = ResolveReviveHealth(deadOwnerId);

        CoopPlayerReviveRpc revive = new CoopPlayerReviveRpc
        {
            DeadOwnerId = deadOwnerId,
            Position = revivePosition + Vector3.up * 0.08f,
            Rotation = reviveRotation,
            Health = health
        };

        BroadcastServerRpc(revive);
        ApplyPlayerRevive(revive);
    }

    private float ResolveReviveHealth(int ownerId)
    {
        float maxHealth = 100f;

        if (ownerId == (session != null ? session.LocalNetworkId : 0) && localCharacterStats != null)
            maxHealth = Mathf.Max(1f, localCharacterStats.MaxHealth);
        else if (lastPlayerSnapshots.TryGetValue(ownerId, out CoopPlayerSnapshotRpc snapshot))
            maxHealth = Mathf.Max(1f, snapshot.MaxHealth);
        else if (remotePlayers.TryGetValue(ownerId, out CoopNetworkIdentity identity))
        {
            CharacterStats stats = GetNetworkComponent<CharacterStats>(identity);
            if (stats != null)
                maxHealth = Mathf.Max(1f, stats.MaxHealth);
        }

        return Mathf.Max(1f, maxHealth * ReviveHealthPercent);
    }

    private void ApplyPlayerRevive(CoopPlayerReviveRpc revive)
    {
        int ownerId = revive.DeadOwnerId;
        if (ownerId <= 0)
            return;

        Vector3 position = revive.Position;
        Quaternion rotation = revive.Rotation;

        deadPlayerIds.Remove(ownerId);
        notifiedLocalDeathIds.Remove(ownerId);
        RemoveReviveMarker(ownerId);
        RestoreDeadBody(ownerId);

        int localOwnerId = session != null ? session.LocalNetworkId : 0;
        if (ownerId == localOwnerId)
        {
            if (localCharacterStats != null)
                localCharacterStats.Revive(revive.Health);

            ThirdPersonController controller = localPlayerMotionTransform != null
                ? localPlayerMotionTransform.GetComponent<ThirdPersonController>()
                : null;
            if (controller == null && localCharacterStats != null)
                controller = localCharacterStats.GetComponentInChildren<ThirdPersonController>(true);
            if (controller != null)
                controller.ReviveFromNetwork(position, rotation, true);

            SendLocalPlayerSnapshot(localOwnerId, true);
        }
        else
        {
            CoopNetworkIdentity identity = GetOrCreateRemotePlayer(ownerId, position, rotation);
            if (identity != null)
            {
                CharacterStats stats = GetNetworkComponent<CharacterStats>(identity);
                if (stats != null)
                    stats.Revive(revive.Health);

                ThirdPersonController controller = GetNetworkComponent<ThirdPersonController>(identity);
                if (controller != null)
                    controller.ReviveFromNetwork(position, rotation, false);

                CoopNetworkTransform networkTransform = identity.GetComponent<CoopNetworkTransform>();
                if (networkTransform != null)
                    networkTransform.SetTarget(position, rotation);
            }

            if (lastPlayerSnapshots.TryGetValue(ownerId, out CoopPlayerSnapshotRpc snapshot))
            {
                snapshot.Position = position;
                snapshot.Rotation = rotation;
                snapshot.Health = revive.Health;
                snapshot.Flags = (byte)(snapshot.Flags & ~PlayerFlagDead);
                lastPlayerSnapshots[ownerId] = snapshot;
            }
        }

        if (!AllKnownPlayersDead(localOwnerId))
        {
            gameOverVoteStarted = false;
            gameOverVotes.Clear();
            DeathChoiceMenu.Hide();
        }
    }

    private bool AllKnownPlayersDead(int localOwnerId)
    {
        CollectKnownOwnerIds(localOwnerId);
        if (ownerIdBuffer.Count == 0)
            return false;

        for (int i = 0; i < ownerIdBuffer.Count; i++)
        {
            if (!IsPlayerDead(ownerIdBuffer[i], localOwnerId))
                return false;
        }

        return true;
    }

    private void StartGameOverVote()
    {
        if (gameOverVoteStarted || gameOverResultApplied)
            return;

        gameOverVoteStarted = true;
        gameOverVotes.Clear();
        DeathChoiceMenu.ShowCoopVote(SubmitGameOverVote);

        BroadcastServerRpc(new CoopGameOverVoteStartRpc { Active = 1 });
    }

    private void RegisterGameOverVote(int ownerId, byte choice)
    {
        if (ownerId <= 0 || gameOverResultApplied)
            return;

        if (!gameOverVoteStarted)
            StartGameOverVote();

        gameOverVotes[ownerId] = choice == GameOverChoiceLobby ? GameOverChoiceLobby : GameOverChoiceRestart;
        int expectedVotes = Mathf.Max(1, session != null ? session.CurrentPlayers : gameOverVotes.Count);
        DeathChoiceMenu.SetStatus($"Голосов: {gameOverVotes.Count}/{expectedVotes}");

        if (gameOverVotes.Count >= expectedVotes)
            CompleteGameOverVote(ResolveGameOverVoteChoice());
    }

    private byte ResolveGameOverVoteChoice()
    {
        int restartVotes = 0;
        int lobbyVotes = 0;

        foreach (byte choice in gameOverVotes.Values)
        {
            if (choice == GameOverChoiceLobby)
                lobbyVotes++;
            else
                restartVotes++;
        }

        if (restartVotes > lobbyVotes)
            return GameOverChoiceRestart;

        if (lobbyVotes > restartVotes)
            return GameOverChoiceLobby;

        int hostOwnerId = session != null ? session.LocalNetworkId : 0;
        if (gameOverVotes.TryGetValue(hostOwnerId, out byte hostChoice))
            return hostChoice == GameOverChoiceLobby ? GameOverChoiceLobby : GameOverChoiceRestart;

        return GameOverChoiceRestart;
    }

    private void CompleteGameOverVote(byte choice)
    {
        if (gameOverResultApplied)
            return;

        CoopGameOverResultRpc result = new CoopGameOverResultRpc
        {
            Choice = choice == GameOverChoiceLobby ? GameOverChoiceLobby : GameOverChoiceRestart
        };

        BroadcastServerRpc(result);
        ApplyGameOverResult(result);
    }

    private void ApplyGameOverResult(CoopGameOverResultRpc result)
    {
        if (gameOverResultApplied)
            return;

        gameOverResultApplied = true;
        gameOverVoteStarted = false;
        gameOverVotes.Clear();
        DeathChoiceMenu.Hide();
        CoopRevivePromptUI.Hide();

        Time.timeScale = 1f;

        if (result.Choice == GameOverChoiceLobby)
        {
            if (session != null)
                session.StopSession(true);

            SceneManager.LoadScene(MainMenuSceneName);
            return;
        }

        ClearRuntimeDeathState();
        string sceneName = CoopSessionState.SceneName;
        if (!string.IsNullOrWhiteSpace(sceneName))
            SceneManager.LoadScene(sceneName);
    }

    private void ClearRuntimeDeathState()
    {
        CancelAllReviveMarkerDelays();

        foreach (int ownerId in deadPlayerIds)
            RestoreDeadBody(ownerId);

        foreach (CoopReviveMarker marker in reviveMarkers.Values)
        {
            if (marker != null)
                Destroy(marker.gameObject);
        }

        reviveMarkers.Clear();
        deadPlayerIds.Clear();
        notifiedLocalDeathIds.Clear();
        lastPlayerSnapshots.Clear();
        remotePlayers.Clear();
    }

    private void ScheduleReviveMarkerReplacement(int ownerId, Vector3 position, Quaternion rotation)
    {
        if (reviveMarkers.ContainsKey(ownerId) || reviveMarkerDelayCoroutines.ContainsKey(ownerId))
            return;

        reviveMarkerDelayCoroutines[ownerId] = StartCoroutine(ShowReviveMarkerAfterDelay(ownerId, position, rotation));
    }

    private IEnumerator ShowReviveMarkerAfterDelay(int ownerId, Vector3 position, Quaternion rotation)
    {
        if (ReviveMarkerDelaySeconds > 0f)
            yield return new WaitForSeconds(ReviveMarkerDelaySeconds);

        reviveMarkerDelayCoroutines.Remove(ownerId);

        if (!deadPlayerIds.Contains(ownerId))
            yield break;

        EnsureReviveMarker(ownerId, position, rotation);
        RemoveDeadBodyRagdoll(ownerId);
    }

    private void CancelReviveMarkerDelay(int ownerId)
    {
        if (!reviveMarkerDelayCoroutines.TryGetValue(ownerId, out Coroutine coroutine))
            return;

        if (coroutine != null)
            StopCoroutine(coroutine);

        reviveMarkerDelayCoroutines.Remove(ownerId);
    }

    private void CancelAllReviveMarkerDelays()
    {
        foreach (Coroutine coroutine in reviveMarkerDelayCoroutines.Values)
        {
            if (coroutine != null)
                StopCoroutine(coroutine);
        }

        reviveMarkerDelayCoroutines.Clear();
    }

    private void RemoveDeadBodyRagdoll(int ownerId)
    {
        GameObject playerRoot = ResolvePlayerRoot(ownerId);
        if (playerRoot == null)
            return;

        CoopDeadBodyReplacer replacer = playerRoot.GetComponent<CoopDeadBodyReplacer>();
        if (replacer == null)
            replacer = playerRoot.AddComponent<CoopDeadBodyReplacer>();

        replacer.RemoveNow();
    }

    private void RestoreDeadBody(int ownerId)
    {
        CancelReviveMarkerDelay(ownerId);

        GameObject playerRoot = ResolvePlayerRoot(ownerId);
        if (playerRoot == null)
            return;

        CoopDeadBodyReplacer replacer = playerRoot.GetComponent<CoopDeadBodyReplacer>();
        if (replacer != null)
            replacer.ShowNow();
    }

    private GameObject ResolvePlayerRoot(int ownerId)
    {
        int localOwnerId = session != null ? session.LocalNetworkId : 0;
        if (ownerId == localOwnerId)
        {
            Transform root = localPlayerMotionTransform != null
                ? localPlayerMotionTransform.root
                : localCharacterStats != null
                    ? localCharacterStats.transform.root
                    : null;
            return root != null ? root.gameObject : null;
        }

        if (remotePlayers.TryGetValue(ownerId, out CoopNetworkIdentity identity) && identity != null)
        {
            Transform root = identity.transform.root != null ? identity.transform.root : identity.transform;
            return root.gameObject;
        }

        return null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ignoreDeathStateUntilTime = Time.time + SceneReloadDeathStateIgnoreSeconds;
        ClearRuntimeDeathState();
        gameOverVoteStarted = false;
        gameOverResultApplied = false;
        gameOverVotes.Clear();
        CoopRevivePromptUI.Hide();
        DeathChoiceMenu.Hide();
        clientZombieStateSequences.Clear();
        clientPredictedZombieHealth.Clear();
        clientPredictedZombieHoldUntil.Clear();
        serverClientClockOffsets.Clear();
        zombieRewindHistory.Clear();
        hostZombieHitboxCache.Clear();
        lastZombieSnapshotStates.Clear();
        serverPlayerInventoryItems.Clear();
        pendingWorldItemPickups.Clear();
        remoteProjectiles.Clear();
        processedServerShotKeys.Clear();
        processedServerShotKeyOrder.Clear();

        if (!CoopSessionState.IsCoopSession || scene.name == MainMenuSceneName)
        {
            serverPlayerSnapshotSequences.Clear();
            clientPlayerSnapshotSequences.Clear();
        }
    }

    private CoopNetworkIdentity GetOrCreateRemotePlayer(int ownerId, Vector3 position, Quaternion rotation)
    {
        if (ownerId <= 0)
            return null;

        if (remotePlayers.TryGetValue(ownerId, out CoopNetworkIdentity existing) && existing != null)
            return existing;

        LoadPrefabs();
        if (playerPrefab == null)
            return null;

        GameObject instanceObject = Instantiate(playerPrefab, position, rotation);
        instanceObject.name = $"Remote Player {ownerId}";

        Transform motionTransform = ResolvePlayerMotionTransform(instanceObject);
        CoopNetworkIdentity identity = CoopNetworkIdentity.GetOrAdd(motionTransform.gameObject);
        identity.Configure(CoopNetworkObjectKind.Player, ownerId, ownerId, 0, false, true);
        ConfigureRemotePlayer(instanceObject);
        remotePlayers[ownerId] = identity;
        return identity;
    }

    private static Transform ResolvePlayerMotionTransform(GameObject playerObject)
    {
        if (playerObject == null)
            return null;

        ThirdPersonController controller = playerObject.GetComponent<ThirdPersonController>() ?? playerObject.GetComponentInChildren<ThirdPersonController>(true);
        if (controller != null)
            return controller.transform;

        CharacterController characterController = playerObject.GetComponent<CharacterController>() ?? playerObject.GetComponentInChildren<CharacterController>(true);
        if (characterController != null)
            return characterController.transform;

        return playerObject.transform;
    }

    private static T GetNetworkComponent<T>(CoopNetworkIdentity identity) where T : Component
    {
        if (identity == null)
            return null;

        T component = identity.GetComponent<T>();
        if (component != null)
            return component;

        Transform root = identity.transform.root != null ? identity.transform.root : identity.transform;
        component = root.GetComponentInChildren<T>(true);
        if (component != null)
            return component;

        return identity.GetComponentInParent<T>(true);
    }

    private static void ConfigureRemotePlayer(GameObject playerObject)
    {
        if (playerObject.GetComponent<CharacterController>() == null && playerObject.GetComponentInChildren<CharacterController>(true) != null)
            playerObject.tag = "Untagged";

        foreach (Camera camera in playerObject.GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = false;
            if (camera.CompareTag("MainCamera"))
                camera.tag = "Untagged";
        }

        foreach (AudioListener listener in playerObject.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;

        foreach (Behaviour behaviour in playerObject.GetComponentsInChildren<Behaviour>(true))
        {
            if (behaviour == null || behaviour is Animator || behaviour is CoopNetworkIdentity || behaviour is CoopNetworkTransform)
                continue;

            string typeName = behaviour.GetType().Name;
            if (typeName == nameof(InputsController) ||
                typeName == nameof(ThirdPersonController) ||
                typeName == nameof(BasicRigidBodyPush) ||
                typeName == nameof(CharacterStats) ||
                typeName == nameof(PlayerWeaponController) ||
                typeName == nameof(PlayerInventory) ||
                typeName == "PlayerInput" ||
                typeName.StartsWith("Cinemachine"))
            {
                behaviour.enabled = false;
            }
        }
    }

    private void ReceiveWeaponShotsOnServer()
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopWeaponShotRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopWeaponShotRpc> shots = query.ToComponentDataArray<CoopWeaponShotRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> requests = query.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopWeaponShotRpc shot = shots[i];
            int ownerId = GetConnectionNetworkId(world, requests[i].SourceConnection);
            if (ownerId > 0)
                shot.OwnerId = ownerId;

            if (RegisterServerShot(shot.OwnerId, shot.Sequence))
                TryApplyServerHitscanFallback(shot);

            BroadcastServerRpc(shot);
            entityManager.DestroyEntity(entities[i]);
        }
    }

    private bool RegisterServerShot(int ownerId, uint sequence)
    {
        if (ownerId <= 0 || sequence == 0)
            return true;

        ulong key = ((ulong)(uint)ownerId << 32) | sequence;
        if (!processedServerShotKeys.Add(key))
            return false;

        processedServerShotKeyOrder.Enqueue(key);
        while (processedServerShotKeyOrder.Count > MaxProcessedServerShotKeys)
        {
            ulong oldKey = processedServerShotKeyOrder.Dequeue();
            processedServerShotKeys.Remove(oldKey);
        }

        return true;
    }

    private void TryApplyServerHitscanFallback(CoopWeaponShotRpc shot)
    {
        if (shot.Hitscan == 0 || shot.PredictedZombieHit != 0 || shot.PredictedZombieId > 0)
            return;

        if (shot.OwnerId <= 0 || shot.Damage <= 0f || shot.Range <= 0f)
            return;

        if (session != null && session.IsHost && shot.OwnerId == session.LocalNetworkId)
            return;

        Vector3 origin = shot.Origin;
        Vector3 direction = shot.Direction;
        if (direction.sqrMagnitude <= 0.001f)
            return;

        Transform ownerRoot = ResolveNetworkOwnerRoot(shot.OwnerId);
        if (!IsServerShotOriginReasonable(origin, ownerRoot))
            return;

        LayerMask hitMask = shot.HitMask != 0 ? shot.HitMask : ~0;
        float blockingDistance = shot.Range;

        if (ShooterAimUtility.TryRaycastIgnoringOwner(origin, direction.normalized, shot.Range, hitMask, ownerRoot, out RaycastHit hit))
        {
            BaseDamagable damageable = ShooterAimUtility.FindDamageable(hit.collider);
            ZombieHealth zombie = ResolveZombieHealthFromDamageable(damageable, hit.collider);
            if (zombie != null && !zombie.IsDead && hostZombieIds.ContainsKey(zombie))
            {
                damageable ??= zombie;
                ApplyServerShotDamage(damageable, shot.Damage, hit.point, hit.normal);
                return;
            }

            if (IsSolidShotBlocker(hit.collider))
                blockingDistance = Mathf.Max(0f, hit.distance);
        }

        float targetTime = ResolveServerShotRewindTime(shot);
        if (!TryRaycastZombieRewind(origin, direction.normalized, shot.Range, blockingDistance, targetTime, out ZombieRewindHit rewindHit))
            return;

        ApplyServerRewindShotDamage(rewindHit, shot.Damage);
    }

    private void ApplyServerShotDamage(BaseDamagable damageable, float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (damageable == null || damage <= 0f)
            return;

        BeginNetworkDamageScope();
        try
        {
            damageable.TakeDamage(damage, hitPoint, hitNormal);
        }
        finally
        {
            EndNetworkDamageScope();
        }
    }

    private void ApplyServerRewindShotDamage(ZombieRewindHit hit, float baseDamage)
    {
        if (hit.owner == null || hit.owner.IsDead || baseDamage <= 0f)
            return;

        float damage = baseDamage * Mathf.Max(0f, hit.damageMultiplier);
        if (damage <= 0f)
            return;

        BeginNetworkDamageScope();
        try
        {
            hit.owner.ApplyNetworkDamage(damage, hit.point, hit.normal);
        }
        finally
        {
            EndNetworkDamageScope();
        }
    }

    private Transform ResolveNetworkOwnerRoot(int ownerId)
    {
        int localOwnerId = session != null ? session.LocalNetworkId : 0;
        if (ownerId == localOwnerId && localPlayerMotionTransform != null)
            return localPlayerMotionTransform.root;

        if (remotePlayers.TryGetValue(ownerId, out CoopNetworkIdentity identity) && identity != null)
            return identity.transform.root != null ? identity.transform.root : identity.transform;

        return null;
    }

    private static bool IsServerShotOriginReasonable(Vector3 origin, Transform ownerRoot)
    {
        if (ownerRoot == null)
            return true;

        return (origin - ownerRoot.position).sqrMagnitude <= MaxServerShotOriginDistance * MaxServerShotOriginDistance;
    }

    private static ZombieHealth ResolveZombieHealthFromDamageable(BaseDamagable damageable, Collider hitCollider)
    {
        if (damageable is ZombieHealth zombieHealth)
            return zombieHealth;

        if (damageable is ZombieHitbox hitbox)
            return hitbox.Owner;

        if (damageable != null)
        {
            ZombieHealth parentHealth = damageable.GetComponentInParent<ZombieHealth>();
            if (parentHealth != null)
                return parentHealth;
        }

        return hitCollider != null ? hitCollider.GetComponentInParent<ZombieHealth>() : null;
    }

    private float ResolveServerShotRewindTime(CoopWeaponShotRpc shot)
    {
        float targetTime = Time.time - DefaultZombieShotRewindSeconds;

        if (shot.ClientTime > 0f && serverClientClockOffsets.TryGetValue(shot.OwnerId, out float offset))
            targetTime = shot.ClientTime + offset - DefaultZombieShotRewindSeconds;

        float oldestTime = Time.time - ZombieRewindHistorySeconds;
        return Mathf.Clamp(targetTime, oldestTime, Time.time);
    }

    private static bool IsSolidShotBlocker(Collider collider)
    {
        if (collider == null || collider.isTrigger)
            return false;

        return ResolveZombieHealthFromDamageable(ShooterAimUtility.FindDamageable(collider), collider) == null;
    }

    private bool TryRaycastZombieRewind(
        Vector3 origin,
        Vector3 direction,
        float range,
        float blockingDistance,
        float targetTime,
        out ZombieRewindHit bestHit)
    {
        bestHit = default;

        if (direction.sqrMagnitude <= 0.001f || range <= 0f || zombieRewindHistory.Count == 0)
            return false;

        direction.Normalize();
        float maxDistance = Mathf.Min(range, Mathf.Max(0f, blockingDistance - 0.01f));
        bool hasBestHit = false;
        float bestDistance = float.MaxValue;

        foreach (KeyValuePair<int, List<ZombieRewindFrame>> pair in zombieRewindHistory)
        {
            if (!TryGetZombieRewindFrame(pair.Value, targetTime, out ZombieRewindFrame frame) ||
                !TryFindBestZombieRewindHit(frame, origin, direction, maxDistance, out ZombieRewindHit zombieHit))
            {
                continue;
            }

            if (!hasBestHit || zombieHit.distance < bestDistance)
            {
                hasBestHit = true;
                bestDistance = zombieHit.distance;
                bestHit = zombieHit;
            }
        }

        return hasBestHit;
    }

    private static bool TryGetZombieRewindFrame(List<ZombieRewindFrame> frames, float targetTime, out ZombieRewindFrame frame)
    {
        frame = null;

        if (frames == null || frames.Count == 0)
            return false;

        frame = frames[0];
        float bestDelta = Mathf.Abs(frame.time - targetTime);

        for (int i = 1; i < frames.Count; i++)
        {
            float delta = Mathf.Abs(frames[i].time - targetTime);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                frame = frames[i];
            }
        }

        return frame != null && frame.hitboxes != null && frame.hitboxes.Length > 0;
    }

    private static bool TryFindBestZombieRewindHit(
        ZombieRewindFrame frame,
        Vector3 origin,
        Vector3 direction,
        float maxDistance,
        out ZombieRewindHit bestHit)
    {
        bestHit = default;

        bool hasHit = false;
        int bestPriority = int.MinValue;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < frame.hitboxes.Length; i++)
        {
            ZombieRewindHitbox hitbox = frame.hitboxes[i];
            if (hitbox.owner == null || hitbox.owner.IsDead || hitbox.radius <= 0f)
                continue;

            float distance;
            Vector3 point;
            Vector3 normal;
            bool intersects = hitbox.isSphere
                ? TryIntersectRewindSphere(origin, direction, hitbox.start, hitbox.radius, maxDistance, out distance, out point, out normal)
                : TryIntersectRewindCapsule(origin, direction, hitbox.start, hitbox.end, hitbox.radius, maxDistance, out distance, out point, out normal);

            if (!intersects)
                continue;

            int priority = GetRewindHitPriority(hitbox);
            if (hasHit && (priority < bestPriority || priority == bestPriority && distance >= bestDistance))
                continue;

            hasHit = true;
            bestPriority = priority;
            bestDistance = distance;
            bestHit = new ZombieRewindHit
            {
                owner = hitbox.owner,
                bodyPart = hitbox.bodyPart,
                damageMultiplier = hitbox.damageMultiplier,
                fallbackHitbox = hitbox.fallbackHitbox,
                point = point,
                normal = normal,
                distance = distance
            };
        }

        return hasHit;
    }

    private static int GetRewindHitPriority(ZombieRewindHitbox hitbox)
    {
        if (hitbox.fallbackHitbox)
            return 0;

        return hitbox.bodyPart switch
        {
            ZombieHitboxBodyPart.Head => 40,
            ZombieHitboxBodyPart.Arm => 30,
            ZombieHitboxBodyPart.Hand => 30,
            ZombieHitboxBodyPart.Leg => 30,
            ZombieHitboxBodyPart.Foot => 30,
            ZombieHitboxBodyPart.Torso => 10,
            _ => 0
        };
    }

    private static bool TryIntersectRewindSphere(
        Vector3 origin,
        Vector3 direction,
        Vector3 center,
        float radius,
        float maxDistance,
        out float distance,
        out Vector3 point,
        out Vector3 normal)
    {
        distance = 0f;
        point = default;
        normal = default;

        Vector3 toOrigin = origin - center;
        float b = Vector3.Dot(toOrigin, direction);
        float c = Vector3.Dot(toOrigin, toOrigin) - radius * radius;

        if (c > 0f && b > 0f)
            return false;

        float discriminant = b * b - c;
        if (discriminant < 0f)
            return false;

        distance = -b - Mathf.Sqrt(discriminant);
        if (distance < 0f)
            distance = 0f;

        if (distance > maxDistance)
            return false;

        point = origin + direction * distance;
        normal = point - center;
        if (normal.sqrMagnitude <= 0.001f)
            normal = -direction;
        else
            normal.Normalize();

        return true;
    }

    private static bool TryIntersectRewindCapsule(
        Vector3 origin,
        Vector3 direction,
        Vector3 start,
        Vector3 end,
        float radius,
        float maxDistance,
        out float distance,
        out Vector3 point,
        out Vector3 normal)
    {
        distance = float.MaxValue;
        point = default;
        normal = default;
        bool hasHit = false;

        if (TryIntersectRewindSphere(origin, direction, start, radius, maxDistance, out float startDistance, out Vector3 startPoint, out Vector3 startNormal))
            AcceptCapsuleHit(startDistance, startPoint, startNormal, ref hasHit, ref distance, ref point, ref normal);

        if (TryIntersectRewindSphere(origin, direction, end, radius, maxDistance, out float endDistance, out Vector3 endPoint, out Vector3 endNormal))
            AcceptCapsuleHit(endDistance, endPoint, endNormal, ref hasHit, ref distance, ref point, ref normal);

        Vector3 axis = end - start;
        Vector3 offset = origin - start;
        float axisLengthSquared = Vector3.Dot(axis, axis);
        if (axisLengthSquared > 0.0001f)
        {
            float axisRay = Vector3.Dot(axis, direction);
            float axisOffset = Vector3.Dot(axis, offset);
            float rayOffset = Vector3.Dot(direction, offset);
            float offsetSquared = Vector3.Dot(offset, offset);
            float a = axisLengthSquared - axisRay * axisRay;
            float b = axisLengthSquared * rayOffset - axisOffset * axisRay;
            float c = axisLengthSquared * offsetSquared - axisOffset * axisOffset - radius * radius * axisLengthSquared;
            float discriminant = b * b - a * c;

            if (Mathf.Abs(a) > 0.0001f && discriminant >= 0f)
            {
                float cylinderDistance = (-b - Mathf.Sqrt(discriminant)) / a;
                float axisPosition = axisOffset + cylinderDistance * axisRay;
                if (cylinderDistance >= 0f &&
                    cylinderDistance <= maxDistance &&
                    axisPosition > 0f &&
                    axisPosition < axisLengthSquared)
                {
                    Vector3 cylinderPoint = origin + direction * cylinderDistance;
                    Vector3 closestAxisPoint = start + axis * (axisPosition / axisLengthSquared);
                    Vector3 cylinderNormal = cylinderPoint - closestAxisPoint;
                    if (cylinderNormal.sqrMagnitude <= 0.001f)
                        cylinderNormal = -direction;
                    else
                        cylinderNormal.Normalize();

                    AcceptCapsuleHit(cylinderDistance, cylinderPoint, cylinderNormal, ref hasHit, ref distance, ref point, ref normal);
                }
            }
        }

        return hasHit;
    }

    private static void AcceptCapsuleHit(
        float candidateDistance,
        Vector3 candidatePoint,
        Vector3 candidateNormal,
        ref bool hasHit,
        ref float distance,
        ref Vector3 point,
        ref Vector3 normal)
    {
        if (hasHit && candidateDistance >= distance)
            return;

        hasHit = true;
        distance = candidateDistance;
        point = candidatePoint;
        normal = candidateNormal;
    }

    private void ReceiveWeaponShotsOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopWeaponShotRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopWeaponShotRpc> shots = query.ToComponentDataArray<CoopWeaponShotRpc>(Allocator.Temp);
        int localOwnerId = session.LocalNetworkId;

        for (int i = 0; i < entities.Length; i++)
        {
            CoopWeaponShotRpc shot = shots[i];
            if (shot.OwnerId > 0 && shot.OwnerId != localOwnerId)
                PlayRemoteShot(shot);

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void PlayRemoteShot(CoopWeaponShotRpc shot)
    {
        if (!remotePlayers.TryGetValue(shot.OwnerId, out CoopNetworkIdentity identity) || identity == null)
            return;

        PlayerWeaponController weaponController = GetNetworkComponent<PlayerWeaponController>(identity);
        if (weaponController == null)
            return;

        weaponController.SetSelectedWeaponIndex(shot.WeaponIndex);
        Weapon weapon = weaponController.GetCurrentWeapon();
        if (weapon != null)
            weapon.PlayRemoteShotFeedback();
    }

    private void ReceiveProjectileSpawnsOnServer()
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopProjectileSpawnRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopProjectileSpawnRpc> spawns = query.ToComponentDataArray<CoopProjectileSpawnRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> requests = query.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopProjectileSpawnRpc spawn = spawns[i];
            int ownerId = GetConnectionNetworkId(world, requests[i].SourceConnection);
            if (ownerId > 0)
                spawn.OwnerId = ownerId;

            BroadcastServerRpc(spawn);
            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceiveProjectileSpawnsOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopProjectileSpawnRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopProjectileSpawnRpc> spawns = query.ToComponentDataArray<CoopProjectileSpawnRpc>(Allocator.Temp);
        int localOwnerId = session.LocalNetworkId;

        for (int i = 0; i < entities.Length; i++)
        {
            CoopProjectileSpawnRpc spawn = spawns[i];
            if (spawn.OwnerId > 0 && spawn.OwnerId != localOwnerId)
                SpawnRemoteProjectileVisual(spawn);

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceiveProjectileImpactsOnServer()
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopProjectileImpactRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopProjectileImpactRpc> impacts = query.ToComponentDataArray<CoopProjectileImpactRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> requests = query.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopProjectileImpactRpc impact = impacts[i];
            int ownerId = GetConnectionNetworkId(world, requests[i].SourceConnection);
            if (ownerId > 0)
                impact.OwnerId = ownerId;

            BroadcastServerRpc(impact);
            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceiveProjectileImpactsOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopProjectileImpactRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopProjectileImpactRpc> impacts = query.ToComponentDataArray<CoopProjectileImpactRpc>(Allocator.Temp);
        int localOwnerId = session.LocalNetworkId;

        for (int i = 0; i < entities.Length; i++)
        {
            CoopProjectileImpactRpc impact = impacts[i];
            if (impact.OwnerId > 0 && impact.OwnerId != localOwnerId)
                ApplyRemoteProjectileImpact(impact);

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void SpawnRemoteProjectileVisual(CoopProjectileSpawnRpc spawn)
    {
        if (spawn.ProjectileId <= 0 || spawn.OwnerId <= 0)
            return;

        ulong key = MakeProjectileKey(spawn.OwnerId, spawn.ProjectileId);
        if (remoteProjectiles.ContainsKey(key))
            return;

        Weapon weapon = ResolveRemoteProjectileWeapon(spawn.OwnerId, spawn.WeaponIndex);
        Projectile prefab = ResolveProjectilePrefab(weapon);
        if (prefab == null)
            return;

        Vector3 direction = ((Vector3)spawn.Direction).sqrMagnitude > 0.001f ? ((Vector3)spawn.Direction).normalized : Vector3.forward;
        Projectile projectile = Instantiate(prefab, spawn.Position, Quaternion.LookRotation(direction));
        projectile.ConfigureNetwork(spawn.OwnerId, spawn.ProjectileId, false);
        projectile.StartupVisual(
            weapon,
            spawn.Position,
            direction,
            Mathf.Max(0.1f, spawn.Speed),
            Mathf.Max(0.1f, spawn.Lifetime),
            ResolveNetworkOwnerRoot(spawn.OwnerId),
            spawn.HitMask != 0 ? spawn.HitMask : ~0,
            spawn.Range > 0f ? spawn.Range : Mathf.Max(0.1f, spawn.Speed) * Mathf.Max(0.1f, spawn.Lifetime));
        projectile.ConfigureNetwork(spawn.OwnerId, spawn.ProjectileId, false);
        remoteProjectiles[key] = projectile;
    }

    private void ApplyRemoteProjectileImpact(CoopProjectileImpactRpc impact)
    {
        ulong key = MakeProjectileKey(impact.OwnerId, impact.ProjectileId);
        if (!remoteProjectiles.TryGetValue(key, out Projectile projectile) || projectile == null)
        {
            remoteProjectiles.Remove(key);
            return;
        }

        projectile.ForceVisualImpact(impact.Position, impact.Normal, impact.SuppressEffect != 0);
        remoteProjectiles.Remove(key);
    }

    private Weapon ResolveRemoteProjectileWeapon(int ownerId, int weaponIndex)
    {
        if (!remotePlayers.TryGetValue(ownerId, out CoopNetworkIdentity identity) || identity == null)
            return null;

        PlayerWeaponController weaponController = GetNetworkComponent<PlayerWeaponController>(identity);
        if (weaponController == null)
            return null;

        weaponController.SetSelectedWeaponIndex(weaponIndex);
        return weaponController.GetCurrentWeapon();
    }

    private static Projectile ResolveProjectilePrefab(Weapon weapon)
    {
        if (weapon == null)
            return null;

        if (weapon.ProjectilePrefab != null)
            return weapon.ProjectilePrefab;

        ProjectileShooter projectileShooter = weapon.GetComponent<ProjectileShooter>() ?? weapon.GetComponentInChildren<ProjectileShooter>(true);
        return projectileShooter != null ? projectileShooter.ResolveProjectilePrefab() : null;
    }

    private static ulong MakeProjectileKey(int ownerId, int projectileId)
    {
        return ((ulong)(uint)ownerId << 32) | (uint)projectileId;
    }

    private void ConfigureSceneAuthority()
    {
        if (Time.time < nextAuthorityCheckTime)
            return;

        nextAuthorityCheckTime = Time.time + AuthorityCheckInterval;

        if (session == null || session.IsHost)
            return;

        foreach (ZombieSpawner spawner in FindObjectsOfType<ZombieSpawner>(true))
            spawner.enabled = false;

        foreach (ZombieHealth zombieHealth in FindObjectsOfType<ZombieHealth>(true))
        {
            CoopNetworkIdentity identity = zombieHealth.GetComponentInParent<CoopNetworkIdentity>();
            if (identity != null && identity.Kind == CoopNetworkObjectKind.Zombie && identity.IsRemoteProxy)
                continue;

            Destroy(GetNetworkRoot(zombieHealth).gameObject);
        }
    }

    private void RegisterLootContainers()
    {
        if (Time.time < nextLootContainerScanTime)
            return;

        nextLootContainerScanTime = Time.time + LootContainerScanInterval;

        foreach (LootContainer container in FindObjectsOfType<LootContainer>(true))
            RegisterLootContainer(container);
    }

    private void RegisterLootContainer(LootContainer container)
    {
        if (container == null || lootContainerIds.ContainsKey(container))
            return;

        string id = container.NetworkId;
        if (string.IsNullOrWhiteSpace(id))
            return;

        lootContainerIds[container] = id;
        lootContainersById[id] = container;
        container.InventoryChanged += () => HandleLootContainerChanged(container);
    }

    private void HandleLootContainerChanged(LootContainer container)
    {
        if (container == null || IsApplyingNetworkLootState)
            return;

        if (!CoopSessionState.IsCoopSession || session == null)
            return;

        if (!session.IsHost)
            return;

        RegisterLootContainer(container);
        SendLootContainerState(container, true);
    }

    private void SendLootContainerState(LootContainer container, bool broadcastFromServer)
    {
        if (container == null)
            return;

        string containerId = container.NetworkId;
        uint sequence = ++lootSequence;

        CoopLootContainerClearRpc clearRpc = new CoopLootContainerClearRpc
        {
            ContainerId = containerId,
            WasSearched = container.WasSearched ? (byte)1 : (byte)0,
            Sequence = sequence
        };

        if (broadcastFromServer)
            BroadcastServerRpc(clearRpc);
        else
            SendClientRpc(clearRpc);

        IReadOnlyList<InventorySlot> slots = container.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.item == null || slot.amount <= 0)
                continue;

            CoopLootContainerSlotRpc slotRpc = new CoopLootContainerSlotRpc
            {
                ContainerId = containerId,
                ItemId = GetItemNetworkId(slot.item),
                Amount = slot.amount,
                Sequence = sequence
            };

            if (broadcastFromServer)
                BroadcastServerRpc(slotRpc);
            else
                SendClientRpc(slotRpc);
        }
    }

    private void ReceiveLootContainerRequestsOnServer()
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopLootContainerRequestRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopLootContainerRequestRpc> requests = query.ToComponentDataArray<CoopLootContainerRequestRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            LootContainer container = FindLootContainerById(requests[i].ContainerId.ToString());
            if (container != null)
            {
                container.GenerateLootForNetworkIfNeeded();
                SendLootContainerState(container, true);
            }

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceiveLootTransferRequestsOnServer()
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopLootTransferRequestRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopLootTransferRequestRpc> requests = query.ToComponentDataArray<CoopLootTransferRequestRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> rpcRequests = query.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopLootTransferRequestRpc request = requests[i];
            int ownerId = GetConnectionNetworkId(world, rpcRequests[i].SourceConnection);
            LootContainer container = FindLootContainerById(request.ContainerId.ToString());
            ItemSO item = ResolveItem(request.ItemId.ToString());
            bool approved = ownerId > 0 &&
                container != null &&
                item != null &&
                ApplyAuthoritativeLootTransfer(ownerId, container, null, request.FromContainer != 0, item, request.Amount, false);

            BroadcastServerRpc(new CoopLootTransferResultRpc
            {
                OwnerId = ownerId,
                ContainerId = request.ContainerId,
                ItemId = request.ItemId,
                Amount = request.Amount,
                FromContainer = request.FromContainer,
                Approved = approved ? (byte)1 : (byte)0
            });

            if (container != null)
                SendLootContainerState(container, true);

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceiveLootTransferResultsOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopLootTransferResultRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopLootTransferResultRpc> results = query.ToComponentDataArray<CoopLootTransferResultRpc>(Allocator.Temp);
        int localOwnerId = session.LocalNetworkId;

        for (int i = 0; i < entities.Length; i++)
        {
            CoopLootTransferResultRpc result = results[i];
            if (!session.IsHost && result.Approved != 0 && result.OwnerId == localOwnerId && localCharacterStats != null)
            {
                PlayerInventory inventory = localCharacterStats.GetComponent<PlayerInventory>() ??
                    localCharacterStats.GetComponentInChildren<PlayerInventory>(true) ??
                    localCharacterStats.GetComponentInParent<PlayerInventory>();
                ItemSO item = ResolveItem(result.ItemId.ToString());

                if (inventory != null && item != null)
                {
                    BeginNetworkLootScope();
                    if (result.FromContainer != 0)
                        inventory.AddItem(item, result.Amount);
                    else
                        inventory.RemoveItem(item, result.Amount);
                    EndNetworkLootScope();
                }
            }

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private bool ApplyAuthoritativeLootTransfer(
        int ownerId,
        LootContainer container,
        PlayerInventory localInventory,
        bool fromContainer,
        ItemSO item,
        int amount,
        bool updateLocalInventory)
    {
        if (ownerId <= 0 || container == null || item == null || amount <= 0)
            return false;

        if (fromContainer)
        {
            if (!container.RemoveItem(item, amount))
                return false;

            AddServerInventoryItem(ownerId, item, amount);
            if (updateLocalInventory && localInventory != null)
                localInventory.AddItem(item, amount);
        }
        else
        {
            if (!RemoveServerInventoryItem(ownerId, item, amount, true))
                return false;

            if (updateLocalInventory && localInventory != null && !localInventory.RemoveItem(item, amount))
                AddServerInventoryItem(ownerId, item, amount);

            if (!container.AddItem(item, amount))
            {
                AddServerInventoryItem(ownerId, item, amount);
                if (updateLocalInventory && localInventory != null)
                    localInventory.AddItem(item, amount);
                return false;
            }
        }

        SendLootContainerState(container, session != null && session.IsHost);
        return true;
    }

    private void ReceiveLootContainerStateOnServer()
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        DiscardClientLootContainerStates<CoopLootContainerClearRpc>(world);
        DiscardClientLootContainerStates<CoopLootContainerSlotRpc>(world);
    }

    private static void DiscardClientLootContainerStates<T>(World world) where T : unmanaged, IComponentData
    {
        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<T>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
            entityManager.DestroyEntity(entities[i]);
    }

    private void ReceiveLootContainerStateOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        ReceiveLootContainerClears(world, false);
        ReceiveLootContainerSlots(world, false);
    }

    private void ReceiveLootContainerClears(World world, bool rebroadcastFromServer)
    {
        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopLootContainerClearRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopLootContainerClearRpc> clears = query.ToComponentDataArray<CoopLootContainerClearRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopLootContainerClearRpc clear = clears[i];
            ApplyLootContainerClear(clear);

            if (rebroadcastFromServer)
                BroadcastServerRpc(clear);

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceiveLootContainerSlots(World world, bool rebroadcastFromServer)
    {
        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopLootContainerSlotRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopLootContainerSlotRpc> slots = query.ToComponentDataArray<CoopLootContainerSlotRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopLootContainerSlotRpc slot = slots[i];
            ApplyLootContainerSlot(slot);

            if (rebroadcastFromServer)
                BroadcastServerRpc(slot);

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ApplyLootContainerClear(CoopLootContainerClearRpc clear)
    {
        string containerId = clear.ContainerId.ToString();
        if (string.IsNullOrWhiteSpace(containerId))
            return;

        receivedLootSequences[containerId] = clear.Sequence;
        LootContainer container = FindLootContainerById(containerId);
        if (container == null)
            return;

        BeginNetworkLootScope();
        container.ClearNetworkState(clear.WasSearched != 0);
        EndNetworkLootScope();
    }

    private void ApplyLootContainerSlot(CoopLootContainerSlotRpc slot)
    {
        string containerId = slot.ContainerId.ToString();
        if (string.IsNullOrWhiteSpace(containerId) || slot.Amount <= 0)
            return;

        LootContainer container = FindLootContainerById(containerId);
        ItemSO item = ResolveItem(slot.ItemId.ToString());
        if (container == null || item == null)
            return;

        BeginNetworkLootScope();
        container.AddNetworkItem(item, slot.Amount);
        EndNetworkLootScope();
    }

    private LootContainer FindLootContainerById(string containerId)
    {
        if (string.IsNullOrWhiteSpace(containerId))
            return null;

        if (lootContainersById.TryGetValue(containerId, out LootContainer known) && known != null)
            return known;

        foreach (LootContainer container in FindObjectsOfType<LootContainer>(true))
        {
            RegisterLootContainer(container);
            if (container != null && container.NetworkId == containerId)
                return container;
        }

        return null;
    }

    private static void BeginNetworkLootScope()
    {
        networkLootScopeDepth++;
    }

    private static void EndNetworkLootScope()
    {
        networkLootScopeDepth = Mathf.Max(0, networkLootScopeDepth - 1);
    }

    private void RegisterDroppedWorldItem(WorldItem worldItem, ItemSO item, int amount)
    {
        if (worldItem == null || item == null)
            return;

        int networkItemId = worldItem.NetworkItemId > 0 ? worldItem.NetworkItemId : CreateWorldItemId();
        worldItem.SetupNetwork(networkItemId, amount, false);
        networkWorldItems[networkItemId] = worldItem;
        int ownerId = session != null ? session.LocalNetworkId : 0;

        CoopWorldItemSpawnRpc spawn = new CoopWorldItemSpawnRpc
        {
            OwnerId = ownerId,
            NetworkItemId = networkItemId,
            ItemId = GetItemNetworkId(item),
            Position = worldItem.transform.position,
            Rotation = worldItem.transform.rotation,
            Velocity = Vector3.zero,
            AngularVelocity = Vector3.zero,
            Amount = Mathf.Max(1, amount)
        };

        if (session != null && session.IsHost)
            BroadcastServerRpc(spawn);
        else
            SendClientRpc(spawn);
    }

    private int CreateWorldItemId()
    {
        int ownerId = session != null ? Mathf.Max(1, session.LocalNetworkId) : 1;
        return ownerId * WorldItemIdStride + nextWorldItemSequence++;
    }

    private void SendWorldItemPickup(int networkItemId)
    {
        CoopWorldItemPickupRpc pickup = new CoopWorldItemPickupRpc { NetworkItemId = networkItemId };

        if (session != null && session.IsHost)
            BroadcastServerRpc(pickup);
        else
            SendClientRpc(pickup);
    }

    private void ReceiveWorldItemSpawnsOnServer()
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopWorldItemSpawnRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopWorldItemSpawnRpc> spawns = query.ToComponentDataArray<CoopWorldItemSpawnRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> requests = query.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopWorldItemSpawnRpc spawn = spawns[i];
            int ownerId = GetConnectionNetworkId(world, requests[i].SourceConnection);
            if (ownerId > 0)
                spawn.OwnerId = ownerId;

            ItemSO item = ResolveItem(spawn.ItemId.ToString());
            if (item != null && spawn.Amount > 0 && RemoveServerInventoryItem(spawn.OwnerId, item, spawn.Amount, true))
            {
                spawn.NetworkItemId = CreateWorldItemId();
                SpawnNetworkWorldItem(spawn, false);
                BroadcastServerRpc(spawn);
            }

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceiveWorldItemSpawnsOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopWorldItemSpawnRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopWorldItemSpawnRpc> spawns = query.ToComponentDataArray<CoopWorldItemSpawnRpc>(Allocator.Temp);
        int localOwnerId = session.LocalNetworkId;

        for (int i = 0; i < entities.Length; i++)
        {
            if (!session.IsHost)
            {
                if (spawns[i].OwnerId == localOwnerId)
                    ApplyApprovedLocalDrop(spawns[i]);

                SpawnNetworkWorldItem(spawns[i], true);
            }

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ApplyApprovedLocalDrop(CoopWorldItemSpawnRpc spawn)
    {
        if (localCharacterStats == null)
            return;

        PlayerInventory inventory = localCharacterStats.GetComponent<PlayerInventory>() ??
            localCharacterStats.GetComponentInChildren<PlayerInventory>(true) ??
            localCharacterStats.GetComponentInParent<PlayerInventory>();
        ItemSO item = ResolveItem(spawn.ItemId.ToString());

        if (inventory == null || item == null)
            return;

        BeginNetworkWorldItemScope();
        inventory.RemoveItem(item, Mathf.Max(1, spawn.Amount));
        EndNetworkWorldItemScope();
    }

    private void SpawnNetworkWorldItem(CoopWorldItemSpawnRpc spawn, bool remoteProxy)
    {
        if (spawn.NetworkItemId <= 0 || networkWorldItems.ContainsKey(spawn.NetworkItemId))
            return;

        ItemSO item = ResolveItem(spawn.ItemId.ToString());
        if (item == null)
            return;

        BeginNetworkWorldItemScope();
        WorldItem worldItem = WorldItem.Spawn(item, (Vector3)spawn.Position, (Quaternion)spawn.Rotation);
        if (worldItem != null)
        {
            worldItem.ApplyLaunchVelocity(spawn.Velocity, spawn.AngularVelocity);
            worldItem.SetupNetwork(spawn.NetworkItemId, spawn.Amount, remoteProxy);
            networkWorldItems[spawn.NetworkItemId] = worldItem;
        }
        EndNetworkWorldItemScope();
    }

    private void ReceiveWorldItemPickupsOnServer()
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopWorldItemPickupRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopWorldItemPickupRpc> pickups = query.ToComponentDataArray<CoopWorldItemPickupRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> requests = query.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopWorldItemPickupRpc pickup = pickups[i];
            int ownerId = GetConnectionNetworkId(world, requests[i].SourceConnection);
            if (ownerId > 0)
                pickup.OwnerId = ownerId;

            bool approved = TryApproveWorldItemPickup(ref pickup);
            pickup.Approved = approved ? (byte)1 : (byte)0;
            BroadcastServerRpc(pickup);
            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceiveWorldItemPickupsOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopWorldItemPickupRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopWorldItemPickupRpc> pickups = query.ToComponentDataArray<CoopWorldItemPickupRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            if (!session.IsHost)
                ApplyWorldItemPickupResult(pickups[i]);

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private bool TryApproveWorldItemPickup(ref CoopWorldItemPickupRpc pickup)
    {
        if (pickup.OwnerId <= 0 || pickup.NetworkItemId <= 0)
            return false;

        if (!networkWorldItems.TryGetValue(pickup.NetworkItemId, out WorldItem worldItem) || worldItem == null)
        {
            networkWorldItems.Remove(pickup.NetworkItemId);
            return false;
        }

        ItemSO item = worldItem.ItemData != null ? worldItem.ItemData : ResolveItem(pickup.ItemId.ToString());
        int amount = Mathf.Max(1, worldItem.Amount);
        if (item == null)
            return false;

        pickup.ItemId = GetItemNetworkId(item);
        pickup.Amount = amount;
        AddServerInventoryItem(pickup.OwnerId, item, amount);
        RemoveNetworkWorldItem(pickup.NetworkItemId);
        return true;
    }

    private void ApplyWorldItemPickupResult(CoopWorldItemPickupRpc pickup)
    {
        pendingWorldItemPickups.Remove(pickup.NetworkItemId);

        if (pickup.Approved == 0)
            return;

        int localOwnerId = session != null ? session.LocalNetworkId : 0;
        if (pickup.OwnerId == localOwnerId && localCharacterStats != null)
        {
            PlayerInventory inventory = localCharacterStats.GetComponent<PlayerInventory>() ??
                localCharacterStats.GetComponentInChildren<PlayerInventory>(true) ??
                localCharacterStats.GetComponentInParent<PlayerInventory>();
            ItemSO item = ResolveItem(pickup.ItemId.ToString());

            if (inventory != null && item != null)
            {
                BeginNetworkWorldItemScope();
                inventory.AddItem(item, Mathf.Max(1, pickup.Amount));
                EndNetworkWorldItemScope();
            }
        }

        RemoveNetworkWorldItem(pickup.NetworkItemId);
    }

    private void SpawnAndBroadcastWorldItem(
        int ownerId,
        ItemSO item,
        int amount,
        Vector3 position,
        Quaternion rotation,
        Vector3 velocity,
        Vector3 angularVelocity)
    {
        if (item == null || amount <= 0)
            return;

        int networkItemId = CreateWorldItemId();
        CoopWorldItemSpawnRpc spawn = new CoopWorldItemSpawnRpc
        {
            OwnerId = ownerId,
            NetworkItemId = networkItemId,
            ItemId = GetItemNetworkId(item),
            Position = position,
            Rotation = rotation,
            Velocity = velocity,
            AngularVelocity = angularVelocity,
            Amount = amount
        };

        SpawnNetworkWorldItem(spawn, false);
        BroadcastServerRpc(spawn);
    }

    private Vector3 CalculateWorldItemDropVelocity(Transform dropper, Vector3 dropPosition)
    {
        Vector3 forward = dropper != null ? dropper.forward : Vector3.forward;
        if (dropper != null)
        {
            Vector3 pocketDirection = dropPosition - dropper.position;
            if (pocketDirection.sqrMagnitude > 0.001f)
                forward = Vector3.Lerp(forward, pocketDirection.normalized, 0.35f).normalized;
        }

        return forward * 4.5f + Vector3.up * 2.2f + UnityEngine.Random.insideUnitSphere * 0.45f;
    }

    private void RemoveNetworkWorldItem(int networkItemId)
    {
        if (networkItemId <= 0)
            return;

        if (!networkWorldItems.TryGetValue(networkItemId, out WorldItem worldItem) || worldItem == null)
        {
            networkWorldItems.Remove(networkItemId);
            return;
        }

        BeginNetworkWorldItemScope();
        worldItem.Pickup();
        EndNetworkWorldItemScope();
        networkWorldItems.Remove(networkItemId);
    }

    private static void BeginNetworkWorldItemScope()
    {
        networkWorldItemScopeDepth++;
    }

    private static void EndNetworkWorldItemScope()
    {
        networkWorldItemScopeDepth = Mathf.Max(0, networkWorldItemScopeDepth - 1);
    }

    private void AddServerInventoryItem(int ownerId, ItemSO item, int amount)
    {
        if (ownerId <= 0 || item == null || amount <= 0)
            return;

        string itemId = GetItemNetworkId(item);
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        Dictionary<string, int> inventory = GetServerInventory(ownerId);
        inventory.TryGetValue(itemId, out int current);
        inventory[itemId] = Mathf.Max(0, current) + amount;
    }

    private bool RemoveServerInventoryItem(int ownerId, ItemSO item, int amount, bool allowIfUnknown)
    {
        if (ownerId <= 0 || item == null || amount <= 0)
            return false;

        string itemId = GetItemNetworkId(item);
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        Dictionary<string, int> inventory = GetServerInventory(ownerId);
        inventory.TryGetValue(itemId, out int current);

        if (current < amount)
        {
            if (!allowIfUnknown)
                return false;

            inventory[itemId] = 0;
            return true;
        }

        current -= amount;
        if (current <= 0)
            inventory.Remove(itemId);
        else
            inventory[itemId] = current;

        return true;
    }

    private Dictionary<string, int> GetServerInventory(int ownerId)
    {
        if (!serverPlayerInventoryItems.TryGetValue(ownerId, out Dictionary<string, int> inventory))
        {
            inventory = new Dictionary<string, int>();
            serverPlayerInventoryItems[ownerId] = inventory;
        }

        return inventory;
    }

    private ItemSO ResolveItem(string itemId)
    {
        LoadItemCache();

        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        return itemsById.TryGetValue(itemId.Trim(), out ItemSO item) ? item : null;
    }

    private static string GetItemNetworkId(ItemSO item)
    {
        if (item == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(item.itemID) ? item.itemID.Trim() : item.name;
    }

    private void RegisterHostZombies()
    {
        if (session == null || !session.IsHost)
            return;

        foreach (ZombieHealth health in FindObjectsOfType<ZombieHealth>(true))
        {
            if (health == null || hostZombieIds.ContainsKey(health))
                continue;

            int zombieId = nextZombieId++;
            int prefabId = ResolveZombiePrefabId(health.gameObject);
            CoopNetworkIdentity identity = CoopNetworkIdentity.GetOrAdd(GetNetworkRoot(health).gameObject);
            identity.Configure(CoopNetworkObjectKind.Zombie, zombieId, 0, prefabId, true, false);

            hostZombieIds[health] = zombieId;
            hostZombiesById[zombieId] = health;
            hostZombieHitboxCache[zombieId] = GetZombieHitboxes(health);
            networkZombies[zombieId] = identity;

            health.OnDamageTaken += (damage, hitPoint, hitNormal) => HandleHostZombieDamage(health, damage, hitPoint, hitNormal);
            health.OnDeath += () => HandleHostZombieDeath(health);

            BroadcastZombieSpawn(health);
        }
    }

    private int ResolveZombiePrefabId(GameObject zombieObject)
    {
        LoadPrefabs();
        if (zombiePrefabs == null)
            return 0;

        string objectName = zombieObject.name.Replace("(Clone)", string.Empty).Trim();
        for (int i = 0; i < zombiePrefabs.Length; i++)
        {
            if (zombiePrefabs[i] != null && objectName.StartsWith(zombiePrefabs[i].name))
                return i;
        }

        return 0;
    }

    private void BroadcastZombieSpawn(ZombieHealth health)
    {
        if (health == null || !hostZombieIds.TryGetValue(health, out int zombieId) || spawnedZombieIds.Contains(zombieId))
            return;

        spawnedZombieIds.Add(zombieId);
        CoopNetworkIdentity identity = health.GetComponentInParent<CoopNetworkIdentity>();
        Transform root = GetNetworkRoot(health);

        BroadcastServerRpc(new CoopZombieSpawnRpc
        {
            ZombieId = zombieId,
            PrefabId = identity != null ? identity.PrefabId : 0,
            Position = root.position,
            Rotation = root.rotation,
            Health = health.CurrentHealth,
            Dead = health.IsDead ? (byte)1 : (byte)0
        });
    }

    private void SendZombieSnapshots()
    {
        if (Time.time < nextZombieSnapshotTime)
            return;

        nextZombieSnapshotTime = Time.time + ZombieSnapshotInterval;

        foreach (KeyValuePair<int, ZombieHealth> pair in hostZombiesById)
        {
            if (pair.Value != null)
                BroadcastZombieSnapshot(pair.Value);
        }
    }

    private void SendImportantZombieSnapshots()
    {
        if (Time.time < nextImportantZombieSnapshotCheckTime || Time.time >= nextZombieSnapshotTime)
            return;

        nextImportantZombieSnapshotCheckTime = Time.time + ZombieImportantSnapshotCheckInterval;

        foreach (KeyValuePair<int, ZombieHealth> pair in hostZombiesById)
        {
            ZombieHealth health = pair.Value;
            if (health == null || !TryBuildZombieSnapshot(health, out CoopZombieSnapshotRpc snapshot))
                continue;

            if (!ShouldSendImportantZombieSnapshot(pair.Key, snapshot))
                continue;

            BroadcastServerRpc(snapshot);
            RememberZombieSnapshot(snapshot);
        }
    }

    private bool ShouldSendImportantZombieSnapshot(int zombieId, CoopZombieSnapshotRpc snapshot)
    {
        if (!lastZombieSnapshotStates.TryGetValue(zombieId, out ZombieSnapshotState last))
            return true;

        if (Time.time - last.time < ZombieImportantSnapshotMinInterval)
            return false;

        if (snapshot.Flags != last.flags || snapshot.State != last.state)
            return true;

        if (Mathf.Abs(snapshot.Health - last.health) > ZombieHealthSyncTolerance)
            return true;

        Vector3 position = snapshot.Position;
        Quaternion rotation = snapshot.Rotation;
        if ((position - last.position).sqrMagnitude >= ZombieImportantMoveDistance * ZombieImportantMoveDistance)
            return true;

        return Quaternion.Angle(rotation, last.rotation) >= ZombieImportantRotationAngle;
    }

    private void RecordZombieRewindHistory()
    {
        if (Time.time < nextZombieRewindRecordTime)
            return;

        nextZombieRewindRecordTime = Time.time + ZombieRewindRecordInterval;
        float now = Time.time;
        float oldestAllowedTime = now - ZombieRewindHistorySeconds;

        foreach (KeyValuePair<int, ZombieHealth> pair in hostZombiesById)
        {
            if (pair.Value == null || pair.Value.IsDead)
                continue;

            ZombieRewindFrame frame = BuildZombieRewindFrame(pair.Key, pair.Value, now);
            if (frame == null)
                continue;

            if (!zombieRewindHistory.TryGetValue(pair.Key, out List<ZombieRewindFrame> frames))
            {
                frames = new List<ZombieRewindFrame>();
                zombieRewindHistory[pair.Key] = frames;
            }

            frames.Add(frame);
            for (int i = frames.Count - 1; i >= 0; i--)
            {
                if (frames[i].time < oldestAllowedTime)
                    frames.RemoveAt(i);
            }
        }
    }

    private ZombieRewindFrame BuildZombieRewindFrame(int zombieId, ZombieHealth health, float time)
    {
        ZombieHitbox[] hitboxes = GetCachedZombieHitboxes(zombieId, health);
        if (hitboxes == null || hitboxes.Length == 0)
            return null;

        ZombieHitbox.SyncOwnerHitboxes(health);
        List<ZombieRewindHitbox> samples = new(hitboxes.Length);

        for (int i = 0; i < hitboxes.Length; i++)
        {
            ZombieHitbox hitbox = hitboxes[i];
            if (hitbox == null || !hitbox.isActiveAndEnabled || hitbox.Owner != health)
                continue;

            if (!hitbox.TryGetWorldShape(out Vector3 start, out Vector3 end, out float radius, out bool isSphere))
                continue;

            samples.Add(new ZombieRewindHitbox
            {
                owner = health,
                bodyPart = hitbox.BodyPart,
                damageMultiplier = hitbox.DamageMultiplier,
                fallbackHitbox = hitbox.IsFallbackHitbox,
                isSphere = isSphere,
                start = start,
                end = end,
                radius = radius
            });
        }

        if (samples.Count == 0)
            return null;

        return new ZombieRewindFrame
        {
            time = time,
            hitboxes = samples.ToArray()
        };
    }

    private ZombieHitbox[] GetCachedZombieHitboxes(int zombieId, ZombieHealth health)
    {
        if (health == null)
            return null;

        if (hostZombieHitboxCache.TryGetValue(zombieId, out ZombieHitbox[] hitboxes) && HasValidHitboxCache(hitboxes))
            return hitboxes;

        hitboxes = GetZombieHitboxes(health);
        hostZombieHitboxCache[zombieId] = hitboxes;
        return hitboxes;
    }

    private static bool HasValidHitboxCache(ZombieHitbox[] hitboxes)
    {
        if (hitboxes == null || hitboxes.Length == 0)
            return false;

        for (int i = 0; i < hitboxes.Length; i++)
        {
            if (hitboxes[i] != null)
                return true;
        }

        return false;
    }

    private static ZombieHitbox[] GetZombieHitboxes(ZombieHealth health)
    {
        return health != null ? health.GetComponentsInChildren<ZombieHitbox>(true) : null;
    }

    private void BroadcastZombieSnapshot(ZombieHealth health)
    {
        if (!TryBuildZombieSnapshot(health, out CoopZombieSnapshotRpc snapshot))
            return;

        BroadcastServerRpc(snapshot);
        RememberZombieSnapshot(snapshot);
    }

    private bool TryBuildZombieSnapshot(ZombieHealth health, out CoopZombieSnapshotRpc snapshot)
    {
        snapshot = default;

        if (health == null || !hostZombieIds.TryGetValue(health, out int zombieId))
            return false;

        Transform root = GetNetworkRoot(health);
        ZombieAI ai = health.GetComponentInParent<ZombieAI>();
        NavMeshAgent agent = health.GetComponentInParent<NavMeshAgent>();
        CoopNetworkIdentity identity = health.GetComponentInParent<CoopNetworkIdentity>();

        snapshot = new CoopZombieSnapshotRpc
        {
            ZombieId = zombieId,
            PrefabId = identity != null ? identity.PrefabId : 0,
            Position = root.position,
            Rotation = root.rotation,
            MoveSpeed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f,
            State = ai != null ? (int)ai.state : 0,
            Health = health.CurrentHealth,
            Flags = health.IsDead ? (byte)1 : (byte)0,
            Sequence = ++zombieSequence
        };

        return true;
    }

    private void RememberZombieSnapshot(CoopZombieSnapshotRpc snapshot)
    {
        if (snapshot.ZombieId <= 0)
            return;

        lastZombieSnapshotStates[snapshot.ZombieId] = new ZombieSnapshotState
        {
            position = snapshot.Position,
            rotation = snapshot.Rotation,
            health = snapshot.Health,
            state = snapshot.State,
            flags = snapshot.Flags,
            time = Time.time
        };
    }

    private void ReceiveZombieSpawnsOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopZombieSpawnRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopZombieSpawnRpc> spawns = query.ToComponentDataArray<CoopZombieSpawnRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            if (!session.IsHost)
                GetOrCreateRemoteZombie(spawns[i]);

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceiveZombieSnapshotsOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopZombieSnapshotRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopZombieSnapshotRpc> snapshots = query.ToComponentDataArray<CoopZombieSnapshotRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            if (!session.IsHost)
                ApplyZombieSnapshot(snapshots[i]);

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private CoopNetworkIdentity GetOrCreateRemoteZombie(CoopZombieSpawnRpc spawn)
    {
        if (networkZombies.TryGetValue(spawn.ZombieId, out CoopNetworkIdentity existing) && existing != null)
            return existing;

        LoadPrefabs();
        GameObject prefab = GetZombiePrefab(spawn.PrefabId);
        if (prefab == null)
            return null;

        GameObject zombieObject = Instantiate(prefab, (Vector3)spawn.Position, (Quaternion)spawn.Rotation);
        zombieObject.name = $"{prefab.name} Remote {spawn.ZombieId}";

        CoopNetworkIdentity identity = CoopNetworkIdentity.GetOrAdd(GetNetworkRoot(zombieObject.transform).gameObject);
        identity.Configure(CoopNetworkObjectKind.Zombie, spawn.ZombieId, 0, spawn.PrefabId, false, true);
        ConfigureRemoteZombie(identity.gameObject);
        networkZombies[spawn.ZombieId] = identity;

        ZombieHealth health = GetNetworkComponent<ZombieHealth>(identity);
        if (health != null)
            health.SetNetworkHealth(spawn.Health);

        return identity;
    }

    private GameObject GetZombiePrefab(int prefabId)
    {
        if (zombiePrefabs == null || zombiePrefabs.Length == 0)
            return null;

        int index = Mathf.Clamp(prefabId, 0, zombiePrefabs.Length - 1);
        return zombiePrefabs[index];
    }

    private static void ConfigureRemoteZombie(GameObject zombieObject)
    {
        foreach (ZombieAI ai in zombieObject.GetComponentsInChildren<ZombieAI>(true))
            ai.enabled = false;

        foreach (NavMeshAgent agent in zombieObject.GetComponentsInChildren<NavMeshAgent>(true))
            agent.enabled = false;

        foreach (Rigidbody body in zombieObject.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        CoopNetworkTransform networkTransform = zombieObject.GetComponent<CoopNetworkTransform>();
        if (networkTransform == null)
            networkTransform = zombieObject.AddComponent<CoopNetworkTransform>();

        networkTransform.ConfigureInterpolation(22f, 24f, 5f, 0.12f);
    }

    private void ApplyZombieSnapshot(CoopZombieSnapshotRpc snapshot)
    {
        if (!AcceptZombieStateSequence(snapshot.ZombieId, snapshot.Sequence))
            return;

        CoopNetworkIdentity identity = networkZombies.TryGetValue(snapshot.ZombieId, out CoopNetworkIdentity existing) && existing != null
            ? existing
            : GetOrCreateRemoteZombie(new CoopZombieSpawnRpc
            {
                ZombieId = snapshot.ZombieId,
                PrefabId = snapshot.PrefabId,
                Position = snapshot.Position,
                Rotation = snapshot.Rotation,
                Health = snapshot.Health,
                Dead = (byte)(snapshot.Flags & 1)
            });

        if (identity == null)
            return;

        ZombieHealth health = GetNetworkComponent<ZombieHealth>(identity);
        bool keepPredictedHealth = health != null && ShouldKeepPredictedZombieHealth(snapshot.ZombieId, snapshot.Health);
        bool isDead = (snapshot.Flags & 1) != 0 || (keepPredictedHealth && health.IsDead);

        CoopNetworkTransform networkTransform = identity.GetComponent<CoopNetworkTransform>();
        if (networkTransform == null)
        {
            networkTransform = identity.gameObject.AddComponent<CoopNetworkTransform>();
            networkTransform.ConfigureInterpolation(22f, 24f, 5f, 0.12f);
        }

        networkTransform.SetTarget(snapshot.Position, snapshot.Rotation, isDead);

        Animator animator = GetNetworkComponent<Animator>(identity);
        if (animator != null)
        {
            animator.SetFloat("Speed", snapshot.MoveSpeed);
            animator.SetBool("isDead", isDead);
            animator.SetBool("isAttacking", !isDead && snapshot.State == (int)ZombieAI.State.Attack);
        }

        if (health != null && !keepPredictedHealth)
            health.SetNetworkHealth(snapshot.Health);
    }

    private void ApplyPredictedZombieDamage(CoopNetworkIdentity identity, ZombieHealth health, float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (identity == null || health == null || damage <= 0f || health.IsDead)
            return;

        float healthBeforeDamage = health.CurrentHealth;

        BeginNetworkDamageScope();
        try
        {
            health.ApplyNetworkDamage(damage, hitPoint, hitNormal);
        }
        finally
        {
            EndNetworkDamageScope();
        }

        if (health.CurrentHealth < healthBeforeDamage - ZombieHealthSyncTolerance || health.IsDead)
            RecordPredictedZombieHealth(identity.NetworkId, health.CurrentHealth);

        if (health.IsDead)
            ApplyZombieDeathVisual(identity);
    }

    private void ApplyAuthoritativeZombieDamageEvent(CoopNetworkIdentity identity, CoopZombieDamageEventRpc damageEvent)
    {
        if (identity == null || !AcceptZombieStateSequence(damageEvent.ZombieId, damageEvent.Sequence))
            return;

        ZombieHealth health = GetNetworkComponent<ZombieHealth>(identity);
        if (health == null)
            return;

        float authoritativeHealth = Mathf.Clamp(damageEvent.Health, 0f, health.MaxHealth);
        bool keepPredictedHealth = ShouldKeepPredictedZombieHealth(damageEvent.ZombieId, authoritativeHealth);
        float missingDamage = Mathf.Max(0f, health.CurrentHealth - authoritativeHealth);

        BeginNetworkDamageScope();
        try
        {
            if (missingDamage > ZombieHealthSyncTolerance && !health.IsDead)
                health.ApplyNetworkDamage(missingDamage, damageEvent.HitPoint, damageEvent.HitNormal);

            if (!keepPredictedHealth)
                health.SetNetworkHealth(authoritativeHealth);
        }
        finally
        {
            EndNetworkDamageScope();
        }

        bool isDead = damageEvent.Dead != 0 || (!keepPredictedHealth && authoritativeHealth <= ZombieHealthSyncTolerance) || (keepPredictedHealth && health.IsDead);
        if (isDead)
            ApplyZombieDeathVisual(identity);
    }

    private bool AcceptZombieStateSequence(int zombieId, uint sequence)
    {
        if (zombieId <= 0)
            return false;

        if (sequence == 0)
            return true;

        if (clientZombieStateSequences.TryGetValue(zombieId, out uint lastSequence) && sequence <= lastSequence)
            return false;

        clientZombieStateSequences[zombieId] = sequence;
        return true;
    }

    private void RecordPredictedZombieHealth(int zombieId, float health)
    {
        if (zombieId <= 0)
            return;

        clientPredictedZombieHealth[zombieId] = Mathf.Max(0f, health);
        clientPredictedZombieHoldUntil[zombieId] = Time.time + PredictedZombieDamageHoldSeconds;
    }

    private bool ShouldKeepPredictedZombieHealth(int zombieId, float authoritativeHealth)
    {
        if (!clientPredictedZombieHealth.TryGetValue(zombieId, out float predictedHealth))
            return false;

        if (!clientPredictedZombieHoldUntil.TryGetValue(zombieId, out float holdUntil) || Time.time > holdUntil)
        {
            ClearPredictedZombieHealth(zombieId);
            return false;
        }

        if (Mathf.Max(0f, authoritativeHealth) > predictedHealth + ZombieHealthSyncTolerance)
            return true;

        ClearPredictedZombieHealth(zombieId);
        return false;
    }

    private void ClearPredictedZombieHealth(int zombieId)
    {
        clientPredictedZombieHealth.Remove(zombieId);
        clientPredictedZombieHoldUntil.Remove(zombieId);
    }

    private static void ApplyZombieDeathVisual(CoopNetworkIdentity identity)
    {
        if (identity == null)
            return;

        Animator animator = GetNetworkComponent<Animator>(identity);
        if (animator == null)
            return;

        animator.SetFloat("Speed", 0f);
        animator.SetBool("isDead", true);
        animator.SetBool("isAttacking", false);
    }

    private void SendDamageRequest(CoopNetworkObjectKind targetKind, int targetId, float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        SendClientRpc(new CoopDamageRequestRpc
        {
            TargetKind = (byte)targetKind,
            TargetId = targetId,
            ShooterOwnerId = session != null ? session.LocalNetworkId : 0,
            Damage = damage,
            HitPoint = hitPoint,
            HitNormal = hitNormal.sqrMagnitude > 0.001f ? hitNormal.normalized : Vector3.up
        });
    }

    private void ReceiveDamageRequestsOnServer()
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopDamageRequestRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopDamageRequestRpc> requests = query.ToComponentDataArray<CoopDamageRequestRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> rpcRequests = query.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopDamageRequestRpc request = requests[i];
            int shooterOwnerId = GetConnectionNetworkId(world, rpcRequests[i].SourceConnection);
            if (shooterOwnerId > 0)
                request.ShooterOwnerId = shooterOwnerId;

            if ((CoopNetworkObjectKind)request.TargetKind == CoopNetworkObjectKind.Zombie &&
                hostZombiesById.TryGetValue(request.TargetId, out ZombieHealth health) &&
                health != null)
            {
                BeginNetworkDamageScope();
                health.ApplyNetworkDamage(request.Damage, request.HitPoint, request.HitNormal);
                EndNetworkDamageScope();
            }

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void HandleHostZombieDamage(ZombieHealth health, float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (health == null || !hostZombieIds.TryGetValue(health, out int zombieId))
            return;

        BroadcastServerRpc(new CoopZombieDamageEventRpc
        {
            ZombieId = zombieId,
            Damage = damage,
            Health = health.CurrentHealth,
            HitPoint = hitPoint,
            HitNormal = hitNormal.sqrMagnitude > 0.001f ? hitNormal.normalized : Vector3.up,
            Dead = health.CurrentHealth <= 0f ? (byte)1 : (byte)0,
            Sequence = ++zombieSequence
        });
    }

    private void HandleHostZombieDeath(ZombieHealth health)
    {
        if (health != null)
            BroadcastZombieSnapshot(health);
    }

    private void ReceiveZombieDamageEventsOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopZombieDamageEventRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopZombieDamageEventRpc> events = query.ToComponentDataArray<CoopZombieDamageEventRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            if (!session.IsHost && networkZombies.TryGetValue(events[i].ZombieId, out CoopNetworkIdentity identity) && identity != null)
                ApplyAuthoritativeZombieDamageEvent(identity, events[i]);

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void SendPlayerDamage(int targetOwnerId, float damage, float health, bool dead)
    {
        UpdateCachedPlayerHealth(targetOwnerId, health, dead);

        BroadcastServerRpc(new CoopPlayerDamageRpc
        {
            TargetOwnerId = targetOwnerId,
            Damage = damage,
            Health = health,
            Dead = dead ? (byte)1 : (byte)0
        });
    }

    private void ReceivePlayerDamageOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopPlayerDamageRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopPlayerDamageRpc> damageEvents = query.ToComponentDataArray<CoopPlayerDamageRpc>(Allocator.Temp);
        int localOwnerId = session.LocalNetworkId;

        for (int i = 0; i < entities.Length; i++)
        {
            CoopPlayerDamageRpc damageEvent = damageEvents[i];
            UpdateCachedPlayerHealth(damageEvent.TargetOwnerId, damageEvent.Health, damageEvent.Dead != 0);

            if (damageEvent.TargetOwnerId == localOwnerId && localCharacterStats != null)
            {
                BeginNetworkDamageScope();
                localCharacterStats.ChangeHealth(-damageEvent.Damage);
                localCharacterStats.ApplyNetworkHealth(damageEvent.Health);
                EndNetworkDamageScope();
                SendLocalPlayerSnapshot(localOwnerId, force: true);
            }
            else
            {
                ApplyPlayerDamageToRemoteProxy(damageEvent);
            }

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ApplyPlayerDamageToRemoteProxy(CoopPlayerDamageRpc damageEvent)
    {
        if (damageEvent.TargetOwnerId <= 0 ||
            !remotePlayers.TryGetValue(damageEvent.TargetOwnerId, out CoopNetworkIdentity identity) ||
            identity == null)
        {
            return;
        }

        bool isDead = damageEvent.Dead != 0 || damageEvent.Health <= 0f;
        CharacterStats stats = GetNetworkComponent<CharacterStats>(identity);

        if (stats != null)
        {
            if (isDead)
                stats.ApplyNetworkHealth(0f);
            else if (stats.IsDead)
                stats.Revive(Mathf.Max(1f, damageEvent.Health));
            else
                stats.ApplyNetworkHealth(damageEvent.Health);
        }

        if (isDead)
        {
            MarkPlayerDead(damageEvent.TargetOwnerId, identity.transform.position, identity.transform.rotation, false);
            ActivateRemotePlayerRagdoll(identity);
            return;
        }

        if (lastPlayerSnapshots.TryGetValue(damageEvent.TargetOwnerId, out CoopPlayerSnapshotRpc snapshot))
            RestoreRemotePlayerFromDeathState(identity, snapshot);
    }

    private void ReceivePlayerDeathNoticesOnServer()
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopPlayerDeathNoticeRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopPlayerDeathNoticeRpc> notices = query.ToComponentDataArray<CoopPlayerDeathNoticeRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> requests = query.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopPlayerDeathNoticeRpc notice = notices[i];
            int ownerId = GetConnectionNetworkId(world, requests[i].SourceConnection);
            if (ownerId > 0)
                notice.DeadOwnerId = ownerId;

            if (!ShouldIgnoreDeathStateNow())
                MarkPlayerDead(notice.DeadOwnerId, notice.Position, notice.Rotation, true);

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceivePlayerDeathNoticesOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopPlayerDeathNoticeRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopPlayerDeathNoticeRpc> notices = query.ToComponentDataArray<CoopPlayerDeathNoticeRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopPlayerDeathNoticeRpc notice = notices[i];
            if (notice.DeadOwnerId > 0 && !ShouldIgnoreDeathStateNow())
            {
                MarkPlayerDead(notice.DeadOwnerId, notice.Position, notice.Rotation, false);

                if (remotePlayers.TryGetValue(notice.DeadOwnerId, out CoopNetworkIdentity identity) && identity != null)
                    ActivateRemotePlayerRagdoll(identity);
            }

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceivePlayerReviveRequestsOnServer()
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopPlayerReviveRequestRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopPlayerReviveRequestRpc> requests = query.ToComponentDataArray<CoopPlayerReviveRequestRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> rpcRequests = query.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        int localOwnerId = session != null ? session.LocalNetworkId : 0;
        for (int i = 0; i < entities.Length; i++)
        {
            CoopPlayerReviveRequestRpc request = requests[i];
            int reviverOwnerId = GetConnectionNetworkId(world, rpcRequests[i].SourceConnection);
            if (reviverOwnerId > 0)
                request.ReviverOwnerId = reviverOwnerId;

            if (request.ReviverOwnerId > 0 &&
                request.ReviverOwnerId != request.DeadOwnerId &&
                !IsPlayerDead(request.ReviverOwnerId, localOwnerId))
            {
                CompletePlayerRevive(request.DeadOwnerId, request.Position, request.Rotation);
            }

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceivePlayerRevivesOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopPlayerReviveRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopPlayerReviveRpc> revives = query.ToComponentDataArray<CoopPlayerReviveRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            ApplyPlayerRevive(revives[i]);
            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceiveGameOverVoteStartsOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopGameOverVoteStartRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopGameOverVoteStartRpc> starts = query.ToComponentDataArray<CoopGameOverVoteStartRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            if (starts[i].Active != 0 && !gameOverResultApplied)
            {
                gameOverVoteStarted = true;
                DeathChoiceMenu.ShowCoopVote(SubmitGameOverVote);
            }

            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceiveGameOverVotesOnServer()
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopGameOverVoteRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopGameOverVoteRpc> votes = query.ToComponentDataArray<CoopGameOverVoteRpc>(Allocator.Temp);
        using NativeArray<ReceiveRpcCommandRequest> requests = query.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopGameOverVoteRpc vote = votes[i];
            int ownerId = GetConnectionNetworkId(world, requests[i].SourceConnection);
            if (ownerId > 0)
                vote.OwnerId = ownerId;

            RegisterGameOverVote(vote.OwnerId, vote.Choice);
            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceiveGameOverResultsOnClient()
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopGameOverResultRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopGameOverResultRpc> results = query.ToComponentDataArray<CoopGameOverResultRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            ApplyGameOverResult(results[i]);
            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void SendClientRpc<T>(T rpc) where T : unmanaged, IRpcCommand
    {
        World world = session != null ? session.ClientWorld : null;
        if (world == null || !world.IsCreated)
            return;

        Entity entity = world.EntityManager.CreateEntity();
        world.EntityManager.AddComponentData(entity, rpc);
        world.EntityManager.AddComponentData(entity, new SendRpcCommandRequest { TargetConnection = Entity.Null });
    }

    private void BroadcastServerRpc<T>(T rpc) where T : unmanaged, IRpcCommand
    {
        World world = session != null ? session.ServerWorld : null;
        if (world == null || !world.IsCreated)
            return;

        Entity entity = world.EntityManager.CreateEntity();
        world.EntityManager.AddComponentData(entity, rpc);
        world.EntityManager.AddComponentData(entity, new SendRpcCommandRequest { TargetConnection = Entity.Null });
    }

    private static int GetConnectionNetworkId(World world, Entity connection)
    {
        if (world == null || !world.IsCreated || connection == Entity.Null)
            return 0;

        EntityManager entityManager = world.EntityManager;
        return entityManager.HasComponent<NetworkId>(connection)
            ? entityManager.GetComponentData<NetworkId>(connection).Value
            : 0;
    }

    private static Transform GetNetworkRoot(Component component)
    {
        if (component == null)
            return null;

        CoopNetworkIdentity identity = component.GetComponentInParent<CoopNetworkIdentity>();
        if (identity != null)
            return identity.transform;

        ZombieAI zombieAI = component.GetComponentInParent<ZombieAI>();
        if (zombieAI != null)
            return zombieAI.transform;

        return component.transform.root != null ? component.transform.root : component.transform;
    }

    private void OnDestroy()
    {
        foreach (Weapon weapon in subscribedWeapons)
        {
            if (weapon != null)
                weapon.ShotFired -= HandleLocalWeaponShot;
        }

        if (localWeaponController != null)
            localWeaponController.CurrentWeaponChanged -= HandleCurrentWeaponChanged;

        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (instance == this)
            instance = null;
    }
}

[DisallowMultipleComponent]
public class CoopAllyHud : MonoBehaviour
{
    public readonly struct AllyInfo
    {
        public readonly int ownerId;
        public readonly string displayName;
        public readonly float health;
        public readonly float maxHealth;

        public AllyInfo(int ownerId, string displayName, float health, float maxHealth)
        {
            this.ownerId = ownerId;
            this.displayName = displayName;
            this.health = health;
            this.maxHealth = Mathf.Max(1f, maxHealth);
        }
    }

    private sealed class AllyRow
    {
        public GameObject root;
        public Text nameText;
        public Text healthText;
        public Image healthFill;
        public RectTransform healthFillRect;
    }

    private static CoopAllyHud instance;

    private readonly List<AllyRow> rows = new();
    private RectTransform panelRoot;
    private Font font;

    public static CoopAllyHud EnsureActive()
    {
        if (instance != null)
            return instance;

        GameObject hudObject = new GameObject("Coop Ally HUD");
        instance = hudObject.AddComponent<CoopAllyHud>();
        DontDestroyOnLoad(hudObject);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    public void SetAllies(IReadOnlyList<AllyInfo> allies)
    {
        if (panelRoot == null)
            BuildUI();

        int count = allies != null ? allies.Count : 0;
        panelRoot.gameObject.SetActive(count > 0);
        EnsureRowCount(count);

        for (int i = 0; i < rows.Count; i++)
        {
            AllyRow row = rows[i];
            bool visible = i < count;
            row.root.SetActive(visible);

            if (!visible)
                continue;

            AllyInfo ally = allies[i];
            float percent = Mathf.Clamp01(ally.health / Mathf.Max(1f, ally.maxHealth));
            row.nameText.text = string.IsNullOrWhiteSpace(ally.displayName) ? $"Player {ally.ownerId}" : ally.displayName;

            if (row.healthText != null)
                row.healthText.text = $"{Mathf.CeilToInt(Mathf.Max(0f, ally.health))} / {Mathf.CeilToInt(ally.maxHealth)}";

            if (row.healthFillRect != null)
            {
                row.healthFillRect.anchorMin = Vector2.zero;
                row.healthFillRect.anchorMax = new Vector2(percent, 1f);
                row.healthFillRect.offsetMin = Vector2.zero;
                row.healthFillRect.offsetMax = Vector2.zero;
            }

            if (row.healthFill != null)
            {
                row.healthFill.color = percent <= 0.25f
                    ? new Color(0.86f, 0.18f, 0.16f, 1f)
                    : percent <= 0.55f
                        ? new Color(0.95f, 0.72f, 0.18f, 1f)
                        : new Color(0.22f, 0.86f, 0.38f, 1f);
            }
        }
    }

    private void BuildUI()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 44;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("Allies", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelObject.transform.SetParent(canvasObject.transform, false);

        panelRoot = panelObject.GetComponent<RectTransform>();
        panelRoot.anchorMin = new Vector2(0f, 1f);
        panelRoot.anchorMax = new Vector2(0f, 1f);
        panelRoot.pivot = new Vector2(0f, 1f);
        panelRoot.anchoredPosition = new Vector2(18f, -18f);
        panelRoot.sizeDelta = new Vector2(260f, 0f);

        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        panelObject.SetActive(false);
    }

    private void EnsureRowCount(int count)
    {
        while (rows.Count < count)
            rows.Add(CreateRow(rows.Count));
    }

    private AllyRow CreateRow(int index)
    {
        GameObject rowObject = new GameObject($"Ally {index + 1}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowObject.transform.SetParent(panelRoot, false);

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(260f, 44f);

        LayoutElement layoutElement = rowObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 44f;
        layoutElement.minHeight = 44f;

        Image background = rowObject.GetComponent<Image>();
        background.color = new Color(0.03f, 0.04f, 0.045f, 0.74f);
        background.raycastTarget = false;

        Text nameText = CreateText("Name", rowObject.transform, 15, TextAnchor.MiddleLeft);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 0.45f);
        nameRect.anchorMax = Vector2.one;
        nameRect.offsetMin = new Vector2(10f, 0f);
        nameRect.offsetMax = new Vector2(-72f, -2f);

        Text healthText = CreateText("Health Text", rowObject.transform, 13, TextAnchor.MiddleRight);
        RectTransform healthTextRect = healthText.rectTransform;
        healthTextRect.anchorMin = new Vector2(1f, 0.45f);
        healthTextRect.anchorMax = Vector2.one;
        healthTextRect.offsetMin = new Vector2(-70f, 0f);
        healthTextRect.offsetMax = new Vector2(-10f, -2f);

        Image healthBack = CreateImage("Health Back", rowObject.transform, new Color(0.12f, 0.12f, 0.12f, 0.95f));
        RectTransform backRect = healthBack.rectTransform;
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(1f, 0f);
        backRect.offsetMin = new Vector2(10f, 9f);
        backRect.offsetMax = new Vector2(-10f, 19f);

        Image healthFill = CreateImage("Health Fill", healthBack.transform, new Color(0.22f, 0.86f, 0.38f, 1f));
        healthFill.type = Image.Type.Simple;

        RectTransform fillRect = healthFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        return new AllyRow
        {
            root = rowObject,
            nameText = nameText,
            healthText = healthText,
            healthFill = healthFill,
            healthFillRect = fillRect
        };
    }

    private Text CreateText(string objectName, Transform parent, int size, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}

[DisallowMultipleComponent]
public class CoopReviveMarker : MonoBehaviour
{
    [SerializeField] private int deadOwnerId;
    [SerializeField] private float holdSeconds = 3f;
    [SerializeField] private float interactionRange = 3f;

    private float holdTimer;
    private bool promptOwned;

    public int DeadOwnerId => deadOwnerId;

    public void Configure(int ownerId, float requiredHoldSeconds)
    {
        deadOwnerId = ownerId;
        holdSeconds = Mathf.Max(0.1f, requiredHoldSeconds);
    }

    private void Update()
    {
        if (!CoopGameplaySync.TryGetLocalReviveInteractor(
                out int localOwnerId,
                out CharacterStats stats,
                out Transform playerTransform,
                out Camera playerCamera,
                out InputsController inputs) ||
            stats == null ||
            stats.IsDead ||
            playerTransform == null ||
            localOwnerId <= 0 ||
            localOwnerId == deadOwnerId)
        {
            ResetInteraction();
            return;
        }

        float distance = Vector3.Distance(playerTransform.position, transform.position);
        if (distance > interactionRange)
        {
            ResetInteraction();
            return;
        }

        if (!IsMarkerInFront(playerCamera))
        {
            ResetInteraction();
            return;
        }

        bool isHeld = IsUseHeld(inputs);
        holdTimer = isHeld ? holdTimer + Time.deltaTime : 0f;
        float progress = holdTimer / Mathf.Max(0.1f, holdSeconds);
        promptOwned = true;
        CoopRevivePromptUI.Show("Удерживайте F, чтобы воскресить", progress);

        if (holdTimer < holdSeconds)
            return;

        Quaternion reviveRotation = Quaternion.LookRotation(
            Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up).sqrMagnitude > 0.001f
                ? Vector3.ProjectOnPlane(playerTransform.forward, Vector3.up).normalized
                : Vector3.forward,
            Vector3.up);

        CoopGameplaySync.RequestPlayerRevive(deadOwnerId, transform.position, reviveRotation);
        ResetInteraction();
    }

    private bool IsMarkerInFront(Camera playerCamera)
    {
        if (playerCamera == null)
            return true;

        Vector3 viewportPoint = playerCamera.WorldToViewportPoint(transform.position + Vector3.up);
        if (viewportPoint.z < 0f)
            return false;

        return viewportPoint.x >= -0.2f && viewportPoint.x <= 1.2f && viewportPoint.y >= -0.2f && viewportPoint.y <= 1.2f;
    }

    private void ResetInteraction()
    {
        holdTimer = 0f;

        if (promptOwned)
        {
            promptOwned = false;
            CoopRevivePromptUI.Hide();
        }
    }

    private static bool IsUseHeld(InputsController inputs)
    {
#if ENABLE_INPUT_SYSTEM
        if (inputs != null && inputs.InputAction != null)
            return inputs.InputAction.Player.Use.IsPressed();

        if (Keyboard.current != null)
            return Keyboard.current.fKey.isPressed;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.F);
#else
        return false;
#endif
    }

    private void OnDisable()
    {
        ResetInteraction();
    }

    private void OnDestroy()
    {
        ResetInteraction();
    }
}

public class CoopRevivePromptUI : MonoBehaviour
{
    private static CoopRevivePromptUI instance;

    private RectTransform root;
    private Text label;
    private Image progressBack;
    private Image progressFill;
    private Font font;

    public static void Show(string text, float progress)
    {
        CoopRevivePromptUI ui = EnsureActive();
        ui.root.gameObject.SetActive(true);
        ui.label.text = text;
        ui.progressBack.enabled = true;
        ui.progressFill.enabled = true;
        ui.progressFill.fillAmount = Mathf.Clamp01(progress);
    }

    public static void Hide()
    {
        if (instance == null || instance.root == null)
            return;

        instance.root.gameObject.SetActive(false);
    }

    private static CoopRevivePromptUI EnsureActive()
    {
        if (instance != null)
            return instance;

        GameObject uiObject = new GameObject("Coop Revive Prompt UI");
        instance = uiObject.AddComponent<CoopRevivePromptUI>();
        DontDestroyOnLoad(uiObject);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    private void BuildUI()
    {
        if (root != null)
            return;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Sprite circleSprite = CreateCircleSprite();

        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 68;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rootObject = new GameObject("Prompt", typeof(RectTransform));
        rootObject.transform.SetParent(canvasObject.transform, false);

        root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2(0f, -110f);
        root.sizeDelta = new Vector2(520f, 92f);

        label = CreateText("Label", rootObject.transform, 22, TextAnchor.MiddleCenter);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0.42f);
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        progressBack = CreateImage("Progress Back", rootObject.transform, circleSprite, new Color(0f, 0f, 0f, 0.55f));
        RectTransform backRect = progressBack.rectTransform;
        backRect.anchorMin = new Vector2(0.5f, 0f);
        backRect.anchorMax = new Vector2(0.5f, 0f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.anchoredPosition = new Vector2(0f, 18f);
        backRect.sizeDelta = new Vector2(40f, 40f);

        progressFill = CreateImage("Progress Fill", progressBack.transform, circleSprite, new Color(0.82f, 0.82f, 0.78f, 0.96f));
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Radial360;
        progressFill.fillOrigin = 2;
        progressFill.fillClockwise = true;

        RectTransform fillRect = progressFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        root.gameObject.SetActive(false);
    }

    private Text CreateText(string objectName, Transform parent, int size, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(string objectName, Transform parent, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = (size - 2) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}

public class CoopDeadBodyReplacer : MonoBehaviour
{
    private Renderer[] renderers;
    private bool[] originalRendererStates;
    private Collider[] colliders;
    private bool[] originalColliderStates;
    private Rigidbody[] rigidbodies;
    private bool[] originalKinematicStates;
    private bool[] originalCollisionStates;

    public void HideAfterDelay(float delay)
    {
        RemoveNow();
    }

    public void RemoveNow()
    {
        HideNow();
    }

    public void ShowNow()
    {
        CacheParts();
        enabled = false;

        if (rigidbodies != null)
        {
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody body = rigidbodies[i];
                if (body == null)
                    continue;

                body.isKinematic = originalKinematicStates == null || i >= originalKinematicStates.Length || originalKinematicStates[i];
                body.detectCollisions = originalCollisionStates == null || i >= originalCollisionStates.Length || originalCollisionStates[i];
                body.WakeUp();
            }
        }

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = originalColliderStates == null || i >= originalColliderStates.Length || originalColliderStates[i];
            }
        }

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = originalRendererStates == null || i >= originalRendererStates.Length || originalRendererStates[i];
            }
        }
    }

    private void HideNow()
    {
        CacheParts();
        enabled = false;

        if (rigidbodies != null)
        {
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody body = rigidbodies[i];
                if (body == null)
                    continue;

                if (!body.isKinematic)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }
        }

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = false;
            }
        }
    }

    private void CacheParts()
    {
        if (renderers != null && renderers.Length > 0)
            return;

        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        rigidbodies = GetComponentsInChildren<Rigidbody>(true);

        originalRendererStates = new bool[renderers.Length];
        originalColliderStates = new bool[colliders.Length];
        originalKinematicStates = new bool[rigidbodies.Length];
        originalCollisionStates = new bool[rigidbodies.Length];

        for (int i = 0; i < renderers.Length; i++)
            originalRendererStates[i] = renderers[i] != null && renderers[i].enabled;

        for (int i = 0; i < colliders.Length; i++)
            originalColliderStates[i] = colliders[i] != null && colliders[i].enabled;

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody body = rigidbodies[i];
            originalKinematicStates[i] = body == null || body.isKinematic;
            originalCollisionStates[i] = body != null && body.detectCollisions;
        }
    }
}
