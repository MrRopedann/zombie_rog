using UnityEngine;

public class SurfaceDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float raycastDistance = 2f;
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private Vector3 raycastOffset = new Vector3(0, 0.2f, 0);

    [Header("Debug")]
    [SerializeField] private bool showDebugRay = true;

    [SerializeField] private string currentSurfaceTag = "Ground";
    [SerializeField] private PhysicMaterial currentPhysicMaterial;

    public string CurrentSurfaceTag => currentSurfaceTag;
    public PhysicMaterial CurrentPhysicMaterial => currentPhysicMaterial;

    private void FixedUpdate()
    {
        DetectSurface();
    }

    private void DetectSurface()
    {
        Vector3 rayOrigin = transform.position + raycastOffset;

        if (showDebugRay)
            Debug.DrawRay(rayOrigin, Vector3.down * raycastDistance, Color.red);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer))
        {
            // TERRAIN
            if (hit.collider is TerrainCollider terrainCollider)
            {
                Terrain terrain = terrainCollider.GetComponent<Terrain>();

                if (terrain != null)
                {
                    int textureIndex = GetMainTerrainTexture(hit.point, terrain);

                    // Название слоя Terrain используем как surfaceTag
                    TerrainLayer layer = terrain.terrainData.terrainLayers[textureIndex];

                    currentSurfaceTag = layer.name;
                    currentPhysicMaterial = null;

                    return;
                }
            }

            // ОБЫЧНЫЕ ОБЪЕКТЫ
            currentSurfaceTag = hit.collider.tag;
            currentPhysicMaterial = hit.collider.sharedMaterial;
        }
        else
        {
            currentSurfaceTag = "Ground";
            currentPhysicMaterial = null;
        }
    }

    private int GetMainTerrainTexture(Vector3 worldPos, Terrain terrain)
    {
        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        int mapX = Mathf.RoundToInt(
            ((worldPos.x - terrainPos.x) / terrainData.size.x) * terrainData.alphamapWidth);

        int mapZ = Mathf.RoundToInt(
            ((worldPos.z - terrainPos.z) / terrainData.size.z) * terrainData.alphamapHeight);

        float[,,] splatmapData =
            terrainData.GetAlphamaps(mapX, mapZ, 1, 1);

        int maxIndex = 0;
        float maxMix = 0;

        for (int i = 0; i < splatmapData.GetLength(2); i++)
        {
            if (splatmapData[0, 0, i] > maxMix)
            {
                maxMix = splatmapData[0, 0, i];
                maxIndex = i;
            }
        }

        return maxIndex;
    }

    public (string, PhysicMaterial) GetCurrentSurfaceInfo()
    {
        return (currentSurfaceTag, currentPhysicMaterial);
    }
}