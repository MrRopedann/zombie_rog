using UnityEngine;

public enum CoopNetworkObjectKind : byte
{
    None = 0,
    Player = 1,
    Zombie = 2
}

[DisallowMultipleComponent]
public class CoopNetworkIdentity : MonoBehaviour
{
    [SerializeField] private CoopNetworkObjectKind kind;
    [SerializeField] private int networkId;
    [SerializeField] private int ownerId;
    [SerializeField] private int prefabId;
    [SerializeField] private bool localAuthority;
    [SerializeField] private bool remoteProxy;

    public CoopNetworkObjectKind Kind => kind;
    public int NetworkId => networkId;
    public int OwnerId => ownerId;
    public int PrefabId => prefabId;
    public bool HasLocalAuthority => localAuthority;
    public bool IsRemoteProxy => remoteProxy;

    public void Configure(
        CoopNetworkObjectKind newKind,
        int newNetworkId,
        int newOwnerId,
        int newPrefabId,
        bool hasLocalAuthority,
        bool isRemoteProxy)
    {
        kind = newKind;
        networkId = newNetworkId;
        ownerId = newOwnerId;
        prefabId = newPrefabId;
        localAuthority = hasLocalAuthority;
        remoteProxy = isRemoteProxy;
    }

    public static CoopNetworkIdentity GetOrAdd(GameObject target)
    {
        if (target == null)
            return null;

        CoopNetworkIdentity identity = target.GetComponent<CoopNetworkIdentity>();
        return identity != null ? identity : target.AddComponent<CoopNetworkIdentity>();
    }
}
