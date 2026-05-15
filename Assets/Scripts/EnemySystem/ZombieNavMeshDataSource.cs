using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-10000)]
public class ZombieNavMeshDataSource : MonoBehaviour
{
    [SerializeField] private NavMeshData navMeshData;

    private NavMeshDataInstance navMeshDataInstance;

    public NavMeshData NavMeshData
    {
        get => navMeshData;
        set => navMeshData = value;
    }

    private void Awake()
    {
        AddNavMeshData();
    }

    private void OnEnable()
    {
        AddNavMeshData();
    }

    private void OnDisable()
    {
        RemoveNavMeshData();
    }

    private void AddNavMeshData()
    {
        if (navMeshData == null || navMeshDataInstance.valid)
            return;

        navMeshDataInstance = NavMesh.AddNavMeshData(navMeshData, transform.position, transform.rotation);
    }

    private void RemoveNavMeshData()
    {
        if (!navMeshDataInstance.valid)
            return;

        navMeshDataInstance.Remove();
    }
}
