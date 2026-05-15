using UnityEngine;

public class ZombieHitboxAnimatorSync : MonoBehaviour
{
    [SerializeField] private ZombieHealth owner;

    public void Configure(ZombieHealth newOwner)
    {
        owner = newOwner;
        Sync();
    }

    private void OnAnimatorMove()
    {
        Sync();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        Sync();
    }

    private void LateUpdate()
    {
        Sync();
    }

    private void FixedUpdate()
    {
        Sync();
    }

    private void Sync()
    {
        if (owner != null)
            ZombieHitbox.SyncOwnerHitboxes(owner);
    }
}
