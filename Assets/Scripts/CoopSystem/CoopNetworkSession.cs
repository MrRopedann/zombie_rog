using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct CoopStartGameRpc : IRpcCommand
{
    public FixedString128Bytes SceneName;
}

public struct CoopRoomInfoRpc : IRpcCommand
{
    public FixedString64Bytes RoomName;
    public FixedString64Bytes RoomId;
    public FixedString64Bytes HostAddress;
    public FixedString64Bytes LocationDisplayName;
    public FixedString128Bytes SceneName;
    public int Port;
    public int MaxPlayers;
    public int CurrentPlayers;
}

public struct CoopPlayerCountRpc : IRpcCommand
{
    public int CurrentPlayers;
    public int MaxPlayers;
}

public struct CoopRoomInfoSent : IComponentData
{
}

public class CoopNetworkSession : MonoBehaviour
{
    public const int DefaultPort = 7777;

    private static CoopNetworkSession instance;

    [SerializeField] private bool enableGhostSnapshotStream = false;

    private World serverWorld;
    private World clientWorld;
    private bool ownsServerWorld;
    private bool ownsClientWorld;
    private int currentPlayers;
    private int lastBroadcastPlayers = -1;
    private bool clientConnected;
    private string lastStatus = "Готово";

    public event Action<string> StatusChanged;
    public event Action<int, int> PlayerCountChanged;
    public event Action RoomChanged;

    public bool IsHost => CoopSessionState.IsHost;
    public bool IsClient => CoopSessionState.IsClient;
    public World ServerWorld => serverWorld;
    public World ClientWorld => clientWorld;
    public int LocalNetworkId => GetFirstNetworkId(clientWorld);
    public int CurrentPlayers => Mathf.Max(currentPlayers, CoopSessionState.IsCoopSession ? 1 : 0);
    public int MaxPlayers => CoopSessionState.MaxPlayers;
    public string LastStatus => lastStatus;
    public bool CanStartGame => IsHost && CurrentPlayers >= 2;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureRuntimeForCoop()
    {
        Application.runInBackground = true;
        ClientServerBootstrap.AutoConnectPort = 0;
    }

    public static CoopNetworkSession GetOrCreate()
    {
        if (instance != null)
            return instance;

        instance = FindObjectOfType<CoopNetworkSession>();
        if (instance != null)
            return instance;

        GameObject sessionObject = new GameObject("Coop Network Session");
        instance = sessionObject.AddComponent<CoopNetworkSession>();
        DontDestroyOnLoad(sessionObject);
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
    }

    private void Update()
    {
        if (!CoopSessionState.IsCoopSession)
            return;

        if (enableGhostSnapshotStream)
        {
            MarkConnectionsInGame(serverWorld);
            MarkConnectionsInGame(clientWorld);
        }
        else
        {
            KeepConnectionsInRpcOnlyMode(serverWorld);
            KeepConnectionsInRpcOnlyMode(clientWorld);
        }

        SendRoomInfoToNewClients();
        RefreshPlayerCountFromWorlds();
        ReceiveRoomInfo();
        ReceivePlayerCount();
        ReceiveStartGame();
        CoopGameplaySync.EnsureActive(this);
    }

    public void StartHost(CoopRoomSettings settings)
    {
        StopSession(false);

        Application.runInBackground = true;

        string hostAddress = GetLocalIPv4();
        int port = settings.port <= 0 ? DefaultPort : settings.port;
        string roomId = $"{hostAddress}:{port}";

        CoopSessionState.ConfigureHost(new CoopRoomSettings(settings.roomName, settings.maxPlayers, settings.location, port), roomId, hostAddress);
        currentPlayers = 0;
        lastBroadcastPlayers = -1;
        clientConnected = false;

        serverWorld = GetOrCreateServerWorld("Coop Server World", out ownsServerWorld);
        clientWorld = GetOrCreateClientWorld("Coop Host Client World", out ownsClientWorld);

        bool listening = EnsureListening(serverWorld, port, !ownsServerWorld);
        bool connected = EnsureConnected(clientWorld, "127.0.0.1", port);

        if (!listening || !connected)
        {
            SetStatus("Не удалось открыть комнату Netcode.");
            return;
        }

        SetStatus($"Комната создана. ID: {roomId}");
        NotifyRoomChanged();
    }

    public void Join(string addressOrRoomId, int fallbackPort)
    {
        StopSession(false);

        if (!TryParseEndpoint(addressOrRoomId, fallbackPort, out string host, out int port))
        {
            SetStatus("Не удалось прочитать ID или IP комнаты.");
            return;
        }

        Application.runInBackground = true;
        CoopSessionState.ConfigureClient("Подключение", $"{host}:{port}", host, port, 2, "Ожидание", "Demo_City_Universal_RenderPipeline");
        currentPlayers = 1;
        clientConnected = false;
        lastBroadcastPlayers = -1;

        clientWorld = GetOrCreateClientWorld("Coop Client World", out ownsClientWorld);
        bool connected = EnsureConnected(clientWorld, host, port);

        SetStatus($"Подключение к {host}:{port}...");

        if (!connected)
            SetStatus("Не удалось начать подключение Netcode.");
    }

    public void StartGameAsHost()
    {
        if (!IsHost)
        {
            SetStatus("Запуск доступен только создателю комнаты.");
            return;
        }

        if (!CanStartGame)
        {
            SetStatus("Нужен минимум второй игрок.");
            return;
        }

        string sceneName = CoopSessionState.SceneName;
        BroadcastStartGame(sceneName);
        SetStatus("Запуск игры...");
        LoadSceneIfNeeded(sceneName);
    }

    public void LeaveRoom()
    {
        StopSession(true);
        SetStatus("Комната закрыта.");
    }

    public void StopSession(bool clearState)
    {
        DisposeWorld(ref clientWorld, ref ownsClientWorld);
        DisposeWorld(ref serverWorld, ref ownsServerWorld);

        currentPlayers = 0;
        lastBroadcastPlayers = -1;
        clientConnected = false;

        if (clearState)
            CoopSessionState.Clear();

        NotifyRoomChanged();
    }

    private static bool Listen(World world, int port)
    {
        if (world == null || !world.IsCreated)
            return false;

        using EntityQuery query = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
        return query.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(NetworkEndpoint.AnyIpv4.WithPort((ushort)port));
    }

    private static bool Connect(World world, string host, int port)
    {
        if (world == null || !world.IsCreated)
            return false;

        NetworkEndpoint endpoint = NetworkEndpoint.Parse(host, (ushort)port);
        if (!endpoint.IsValid)
            return false;

        using EntityQuery query = world.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
        Entity connection = query.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(world.EntityManager, endpoint);
        return connection != Entity.Null;
    }

    private static World GetOrCreateServerWorld(string worldName, out bool ownsWorld)
    {
        World existingWorld = FindExistingWorld(world => world.IsServer());
        if (existingWorld != null)
        {
            ownsWorld = false;
            return existingWorld;
        }

        ownsWorld = true;
        return ClientServerBootstrap.CreateServerWorld(worldName);
    }

    private static World GetOrCreateClientWorld(string worldName, out bool ownsWorld)
    {
        World existingWorld = FindExistingWorld(world => world.IsClient() && !world.IsThinClient());
        if (existingWorld != null)
        {
            ownsWorld = false;
            return existingWorld;
        }

        ownsWorld = true;
        return ClientServerBootstrap.CreateClientWorld(worldName);
    }

    private static World FindExistingWorld(Func<World, bool> predicate)
    {
        foreach (World world in World.All)
        {
            if (world == null || !world.IsCreated)
                continue;

            if (predicate(world))
                return world;
        }

        return null;
    }

    private static bool EnsureListening(World world, int port, bool allowAlreadyConfiguredWorld)
    {
        try
        {
            bool listening = Listen(world, port);
            return listening || allowAlreadyConfiguredWorld || HasNetworkStreamConnection(world);
        }
        catch (Exception exception)
        {
            if (allowAlreadyConfiguredWorld || HasNetworkStreamConnection(world))
            {
                Debug.LogWarning($"Coop reused existing Netcode server world '{world?.Name}' and skipped Listen({port}): {exception.Message}");
                return true;
            }

            Debug.LogError($"Coop could not start Netcode server world '{world?.Name}' on port {port}: {exception.Message}");
            return false;
        }
    }

    private static bool EnsureConnected(World world, string host, int port)
    {
        if (HasNetworkStreamConnection(world))
            return true;

        try
        {
            return Connect(world, host, port);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Coop could not create Netcode client connection to {host}:{port}: {exception.Message}");
            return HasNetworkStreamConnection(world);
        }
    }

    private static bool HasNetworkStreamConnection(World world)
    {
        if (world == null || !world.IsCreated)
            return false;

        using EntityQuery query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
        return query.CalculateEntityCount() > 0;
    }

    private void MarkConnectionsInGame(World world)
    {
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[] { ComponentType.ReadOnly<NetworkStreamConnection>() },
            None = new[] { ComponentType.ReadOnly<NetworkStreamInGame>() }
        });

        using NativeArray<Entity> connections = query.ToEntityArray(Allocator.Temp);
        foreach (Entity connection in connections)
            entityManager.AddComponent<NetworkStreamInGame>(connection);
    }

    private void KeepConnectionsInRpcOnlyMode(World world)
    {
        if (world == null || !world.IsCreated)
            return;

        EntityManager entityManager = world.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<NetworkStreamConnection>(),
            ComponentType.ReadOnly<NetworkStreamInGame>());

        if (query.CalculateEntityCount() <= 0)
            return;

        using NativeArray<Entity> connections = query.ToEntityArray(Allocator.Temp);
        foreach (Entity connection in connections)
            entityManager.RemoveComponent<NetworkStreamInGame>(connection);
    }

    private void SendRoomInfoToNewClients()
    {
        if (!IsHost || serverWorld == null || !serverWorld.IsCreated)
            return;

        EntityManager entityManager = serverWorld.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadOnly<NetworkStreamConnection>(),
                ComponentType.ReadOnly<NetworkId>()
            },
            None = new[] { ComponentType.ReadOnly<CoopRoomInfoSent>() }
        });

        using NativeArray<Entity> connections = query.ToEntityArray(Allocator.Temp);
        foreach (Entity connection in connections)
        {
            SendRoomInfo(connection);
            entityManager.AddComponent<CoopRoomInfoSent>(connection);
        }

        if (connections.Length > 0)
        {
            SetStatus(CurrentPlayers >= 2 ? "Игрок подключился. Можно запускать." : "Ожидание второго игрока.");
            NotifyRoomChanged();
        }
    }

    private void RefreshPlayerCountFromWorlds()
    {
        int nextPlayers = currentPlayers;

        if (IsHost && serverWorld != null && serverWorld.IsCreated)
        {
            nextPlayers = CountNetworkConnections(serverWorld);
            if (nextPlayers <= 0)
                nextPlayers = 1;
        }
        else if (IsClient && clientWorld != null && clientWorld.IsCreated)
        {
            int clientConnections = CountNetworkConnections(clientWorld);
            if (clientConnections > 0 && !clientConnected)
            {
                clientConnected = true;
                SetStatus("Подключено. Ожидание запуска.");
            }
        }

        if (nextPlayers != currentPlayers)
        {
            currentPlayers = nextPlayers;
            NotifyRoomChanged();
        }

        if (IsHost && currentPlayers != lastBroadcastPlayers)
        {
            BroadcastPlayerCount();
            lastBroadcastPlayers = currentPlayers;
        }
    }

    private void SendRoomInfo(Entity targetConnection)
    {
        EntityManager entityManager = serverWorld.EntityManager;
        Entity rpc = entityManager.CreateEntity();
        entityManager.AddComponentData(rpc, new CoopRoomInfoRpc
        {
            RoomName = CoopSessionState.RoomName,
            RoomId = CoopSessionState.RoomId,
            HostAddress = CoopSessionState.HostAddress,
            LocationDisplayName = CoopSessionState.LocationDisplayName,
            SceneName = CoopSessionState.SceneName,
            Port = CoopSessionState.Port,
            MaxPlayers = CoopSessionState.MaxPlayers,
            CurrentPlayers = CurrentPlayers
        });
        entityManager.AddComponentData(rpc, new SendRpcCommandRequest { TargetConnection = targetConnection });
    }

    private void BroadcastPlayerCount()
    {
        if (!IsHost || serverWorld == null || !serverWorld.IsCreated)
            return;

        EntityManager entityManager = serverWorld.EntityManager;
        Entity rpc = entityManager.CreateEntity();
        entityManager.AddComponentData(rpc, new CoopPlayerCountRpc
        {
            CurrentPlayers = CurrentPlayers,
            MaxPlayers = CoopSessionState.MaxPlayers
        });
        entityManager.AddComponentData(rpc, new SendRpcCommandRequest { TargetConnection = Entity.Null });
    }

    private void BroadcastStartGame(string sceneName)
    {
        if (!IsHost || serverWorld == null || !serverWorld.IsCreated)
            return;

        EntityManager entityManager = serverWorld.EntityManager;
        Entity rpc = entityManager.CreateEntity();
        entityManager.AddComponentData(rpc, new CoopStartGameRpc { SceneName = sceneName });
        entityManager.AddComponentData(rpc, new SendRpcCommandRequest { TargetConnection = Entity.Null });
    }

    private void ReceiveRoomInfo()
    {
        if (CoopSessionState.IsHost)
            return;

        if (clientWorld == null || !clientWorld.IsCreated)
            return;

        EntityManager entityManager = clientWorld.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopRoomInfoRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopRoomInfoRpc> roomInfos = query.ToComponentDataArray<CoopRoomInfoRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            CoopRoomInfoRpc info = roomInfos[i];
            CoopSessionState.ConfigureClient(
                info.RoomName.ToString(),
                info.RoomId.ToString(),
                info.HostAddress.ToString(),
                info.Port,
                info.MaxPlayers,
                info.LocationDisplayName.ToString(),
                info.SceneName.ToString());
            currentPlayers = info.CurrentPlayers;
            SetStatus("Подключено. Ожидание запуска.");
            NotifyRoomChanged();
            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceivePlayerCount()
    {
        if (clientWorld == null || !clientWorld.IsCreated)
            return;

        EntityManager entityManager = clientWorld.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopPlayerCountRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopPlayerCountRpc> playerCounts = query.ToComponentDataArray<CoopPlayerCountRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            currentPlayers = playerCounts[i].CurrentPlayers;
            NotifyRoomChanged();
            entityManager.DestroyEntity(entities[i]);
        }
    }

    private void ReceiveStartGame()
    {
        if (clientWorld == null || !clientWorld.IsCreated)
            return;

        EntityManager entityManager = clientWorld.EntityManager;
        using EntityQuery query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<CoopStartGameRpc>(),
            ComponentType.ReadOnly<ReceiveRpcCommandRequest>());

        using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
        using NativeArray<CoopStartGameRpc> sceneMessages = query.ToComponentDataArray<CoopStartGameRpc>(Allocator.Temp);

        for (int i = 0; i < entities.Length; i++)
        {
            string sceneName = sceneMessages[i].SceneName.ToString();
            SetStatus("Запуск игры...");
            entityManager.DestroyEntity(entities[i]);
            LoadSceneIfNeeded(sceneName);
        }
    }

    private static int CountNetworkConnections(World world)
    {
        using EntityQuery query = world.EntityManager.CreateEntityQuery(
            ComponentType.ReadOnly<NetworkStreamConnection>(),
            ComponentType.ReadOnly<NetworkId>());
        return query.CalculateEntityCount();
    }

    private static int GetFirstNetworkId(World world)
    {
        if (world == null || !world.IsCreated)
            return 0;

        using EntityQuery query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
        if (query.CalculateEntityCount() <= 0)
            return 0;

        using NativeArray<NetworkId> networkIds = query.ToComponentDataArray<NetworkId>(Allocator.Temp);
        return networkIds.Length > 0 ? networkIds[0].Value : 0;
    }

    private static bool TryParseEndpoint(string addressOrRoomId, int fallbackPort, out string host, out int port)
    {
        host = string.Empty;
        port = Mathf.Clamp(fallbackPort <= 0 ? DefaultPort : fallbackPort, 1024, 65535);

        if (string.IsNullOrWhiteSpace(addressOrRoomId))
            return false;

        string value = addressOrRoomId.Trim();
        int separator = value.LastIndexOf(':');

        if (separator > 0 && separator < value.Length - 1 && int.TryParse(value.Substring(separator + 1), out int parsedPort))
        {
            host = value.Substring(0, separator);
            port = Mathf.Clamp(parsedPort, 1024, 65535);
            return !string.IsNullOrWhiteSpace(host);
        }

        host = value;
        return true;
    }

    private static string GetLocalIPv4()
    {
        try
        {
            IPAddress address = Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip));

            if (address != null)
                return address.ToString();
        }
        catch (SocketException)
        {
        }

        return "127.0.0.1";
    }

    private void SetStatus(string status)
    {
        lastStatus = status;
        StatusChanged?.Invoke(status);
    }

    private void NotifyRoomChanged()
    {
        RoomChanged?.Invoke();
        PlayerCountChanged?.Invoke(CurrentPlayers, MaxPlayers);
    }

    private static void LoadSceneIfNeeded(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        if (SceneManager.GetActiveScene().name == sceneName)
            return;

        SceneManager.LoadScene(sceneName);
    }

    private static void DisposeWorld(ref World world, ref bool ownsWorld)
    {
        if (ownsWorld && world != null && world.IsCreated)
            world.Dispose();

        world = null;
        ownsWorld = false;
    }

    private void OnApplicationQuit()
    {
        StopSession(false);
    }
}
