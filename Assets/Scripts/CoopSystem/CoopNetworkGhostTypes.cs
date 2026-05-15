using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

public struct CoopPlayerGhostState : IComponentData
{
    [GhostField] public int OwnerId;
    [GhostField] public float3 Position;
    [GhostField] public quaternion Rotation;
    [GhostField] public float MoveSpeed;
    [GhostField] public int WeaponIndex;
    [GhostField] public byte Flags;
    [GhostField] public float Health;
    [GhostField] public float MaxHealth;
    [GhostField] public uint Sequence;
}

public struct CoopZombieGhostState : IComponentData
{
    [GhostField] public int ZombieId;
    [GhostField] public int PrefabId;
    [GhostField] public float3 Position;
    [GhostField] public quaternion Rotation;
    [GhostField] public float MoveSpeed;
    [GhostField] public int State;
    [GhostField] public float Health;
    [GhostField] public byte Flags;
    [GhostField] public uint Sequence;
}

public struct CoopPlayerSnapshotRpc : IRpcCommand
{
    public int OwnerId;
    public float3 Position;
    public quaternion Rotation;
    public float MoveSpeed;
    public int WeaponIndex;
    public byte Flags;
    public float Health;
    public float MaxHealth;
    public FixedString64Bytes PlayerName;
    public float ClientTime;
    public uint Sequence;
}

public struct CoopWeaponShotRpc : IRpcCommand
{
    public int OwnerId;
    public int WeaponIndex;
    public float3 Origin;
    public float3 Direction;
    public float Damage;
    public float Range;
    public int HitMask;
    public int PredictedZombieId;
    public byte Hitscan;
    public byte PredictedZombieHit;
    public float ClientTime;
    public uint Sequence;
}

public struct CoopZombieSpawnRpc : IRpcCommand
{
    public int ZombieId;
    public int PrefabId;
    public float3 Position;
    public quaternion Rotation;
    public float Health;
    public byte Dead;
}

public struct CoopZombieSnapshotRpc : IRpcCommand
{
    public int ZombieId;
    public int PrefabId;
    public float3 Position;
    public quaternion Rotation;
    public float MoveSpeed;
    public int State;
    public float Health;
    public byte Flags;
    public uint Sequence;
}

public struct CoopDamageRequestRpc : IRpcCommand
{
    public byte TargetKind;
    public int TargetId;
    public int ShooterOwnerId;
    public float Damage;
    public float3 HitPoint;
    public float3 HitNormal;
}

public struct CoopZombieDamageEventRpc : IRpcCommand
{
    public int ZombieId;
    public float Damage;
    public float Health;
    public float3 HitPoint;
    public float3 HitNormal;
    public byte Dead;
    public uint Sequence;
}

public struct CoopProjectileSpawnRpc : IRpcCommand
{
    public int OwnerId;
    public int ProjectileId;
    public int WeaponIndex;
    public float3 Position;
    public float3 Direction;
    public float Speed;
    public float Damage;
    public float Lifetime;
    public float Range;
    public int HitMask;
    public byte UseGravity;
    public byte AlignToVelocity;
    public float ClientTime;
    public uint Sequence;
}

public struct CoopProjectileImpactRpc : IRpcCommand
{
    public int OwnerId;
    public int ProjectileId;
    public float3 Position;
    public float3 Normal;
    public byte SuppressEffect;
}

public struct CoopPlayerDamageRpc : IRpcCommand
{
    public int TargetOwnerId;
    public float Damage;
    public float Health;
    public byte Dead;
}

public struct CoopPlayerDeathNoticeRpc : IRpcCommand
{
    public int DeadOwnerId;
    public float3 Position;
    public quaternion Rotation;
}

public struct CoopPlayerReviveRequestRpc : IRpcCommand
{
    public int DeadOwnerId;
    public int ReviverOwnerId;
    public float3 Position;
    public quaternion Rotation;
}

public struct CoopPlayerReviveRpc : IRpcCommand
{
    public int DeadOwnerId;
    public float3 Position;
    public quaternion Rotation;
    public float Health;
}

public struct CoopGameOverVoteStartRpc : IRpcCommand
{
    public byte Active;
}

public struct CoopGameOverVoteRpc : IRpcCommand
{
    public int OwnerId;
    public byte Choice;
}

public struct CoopGameOverResultRpc : IRpcCommand
{
    public byte Choice;
}

public struct CoopLootContainerRequestRpc : IRpcCommand
{
    public FixedString128Bytes ContainerId;
}

public struct CoopLootTransferRequestRpc : IRpcCommand
{
    public FixedString128Bytes ContainerId;
    public FixedString64Bytes ItemId;
    public int Amount;
    public byte FromContainer;
}

public struct CoopLootTransferResultRpc : IRpcCommand
{
    public int OwnerId;
    public FixedString128Bytes ContainerId;
    public FixedString64Bytes ItemId;
    public int Amount;
    public byte FromContainer;
    public byte Approved;
}

public struct CoopLootContainerClearRpc : IRpcCommand
{
    public FixedString128Bytes ContainerId;
    public byte WasSearched;
    public uint Sequence;
}

public struct CoopLootContainerSlotRpc : IRpcCommand
{
    public FixedString128Bytes ContainerId;
    public FixedString64Bytes ItemId;
    public int Amount;
    public uint Sequence;
}

public struct CoopWorldItemSpawnRpc : IRpcCommand
{
    public int OwnerId;
    public int NetworkItemId;
    public FixedString64Bytes ItemId;
    public float3 Position;
    public quaternion Rotation;
    public float3 Velocity;
    public float3 AngularVelocity;
    public int Amount;
}

public struct CoopWorldItemPickupRpc : IRpcCommand
{
    public int OwnerId;
    public int NetworkItemId;
    public FixedString64Bytes ItemId;
    public int Amount;
    public byte Approved;
}
