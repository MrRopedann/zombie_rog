using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ItemIconGeneratorTool
{
    private const string ItemFolder = "Assets/_Project/Resources/RuntimeLoadedOnly/Data/Item";
    private const string OutputFolder = "Assets/_Project/Art/Textures/UI/GeneratedItemIcons";
    private const string PolygonApocalypsePreviewMaterialPath = "Assets/_External/PolygonApocalypse/Materials/PolygonApocalypse_Material_01_A.mat";
    private const int IconSize = 256;
    private const int IconPaddingPixels = 10;
    private const float VisibleAlphaThreshold = 0.02f;

    [MenuItem("Tools/Zombie Rogue/Inventory/Generate Missing Item Icons")]
    public static void GenerateMissingItemIcons()
    {
        GenerateIcons(onlyMissingIcons: true);
    }

    [MenuItem("Tools/Zombie Rogue/Inventory/Regenerate All Item Icons")]
    public static void RegenerateAllItemIcons()
    {
        GenerateIcons(onlyMissingIcons: false);
    }

    private static void GenerateIcons(bool onlyMissingIcons)
    {
        EnsureFolder(OutputFolder);

        string[] itemGuids = FindItemAssetGuids();
        int generatedCount = 0;
        int skippedCount = 0;

        try
        {
            for (int i = 0; i < itemGuids.Length; i++)
            {
                string itemPath = AssetDatabase.GUIDToAssetPath(itemGuids[i]);
                ItemSO item = AssetDatabase.LoadAssetAtPath<ItemSO>(itemPath);

                EditorUtility.DisplayProgressBar(
                    "Generating item icons",
                    item != null ? item.name : itemPath,
                    itemGuids.Length > 0 ? i / (float)itemGuids.Length : 1f);

                if (item == null || item.worldPrefab == null || (onlyMissingIcons && item.icon != null))
                {
                    skippedCount++;
                    continue;
                }

                Texture2D texture = RenderPrefabIcon(item.worldPrefab);

                if (texture == null)
                {
                    Debug.LogWarning($"Could not render icon for item {item.name}.", item);
                    skippedCount++;
                    continue;
                }

                string iconPath = $"{OutputFolder}/{GetSafeFileName(item)}_Icon.png";
                File.WriteAllBytes(iconPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);

                AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
                ConfigureTextureAsSprite(iconPath);

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);

                if (sprite == null)
                {
                    Debug.LogWarning($"Generated icon could not be loaded as Sprite: {iconPath}", item);
                    skippedCount++;
                    continue;
                }

                item.icon = sprite;
                EditorUtility.SetDirty(item);
                generatedCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"Item icon generation complete. Generated: {generatedCount}, skipped: {skippedCount}.");
    }

    private static string[] FindItemAssetGuids()
    {
        string[] itemGuids = AssetDatabase.FindAssets("t:ItemSO", new[] { ItemFolder });

        if (itemGuids.Length > 0)
        {
            return itemGuids;
        }

        List<string> filteredGuids = new List<string>();
        string[] scriptableObjectGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { ItemFolder });

        foreach (string guid in scriptableObjectGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (AssetDatabase.LoadAssetAtPath<ItemSO>(path) != null)
            {
                filteredGuids.Add(guid);
            }
        }

        return filteredGuids.ToArray();
    }

    private static Texture2D RenderPrefabIcon(GameObject prefab)
    {
        PreviewRenderUtility preview = new PreviewRenderUtility();
        GameObject instance = null;
        Material previewMaterial = null;

        try
        {
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            preview.camera.orthographic = true;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 1000f;

            preview.lights[0].intensity = 1.15f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 40f, 0f);
            preview.lights[1].intensity = 0.55f;

            instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(prefab);
            }

            instance.hideFlags = HideFlags.HideAndDontSave;
            DisableNonVisualComponents(instance);
            previewMaterial = CreateSafePolygonApocalypsePreviewMaterial();
            ApplyPreviewMaterial(instance, previewMaterial);
            preview.AddSingleGO(instance);

            Bounds bounds = CalculateRendererBounds(instance);
            Vector3 center = bounds.center;
            float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.35f);
            Quaternion cameraRotation = Quaternion.Euler(24f, -35f, 0f);

            preview.camera.transform.position = center + cameraRotation * new Vector3(0f, 0f, -maxExtent * 4.2f);
            preview.camera.transform.rotation = cameraRotation;
            preview.camera.orthographicSize = maxExtent * 1.55f;

            preview.BeginPreview(new Rect(0f, 0f, IconSize, IconSize), GUIStyle.none);
            preview.Render();
            Texture renderedTexture = preview.EndPreview();
            Texture2D rawTexture = CopyTexture(renderedTexture, IconSize, IconSize);
            Texture2D fittedTexture = FitVisiblePixelsToIcon(rawTexture, IconSize, IconPaddingPixels);

            if (fittedTexture != rawTexture)
            {
                UnityEngine.Object.DestroyImmediate(rawTexture);
            }

            return fittedTexture;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return null;
        }
        finally
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            if (previewMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(previewMaterial);
            }

            preview.Cleanup();
        }
    }

    private static void DisableNonVisualComponents(GameObject instance)
    {
        foreach (Canvas canvas in instance.GetComponentsInChildren<Canvas>(true))
        {
            canvas.enabled = false;
        }

        foreach (Light light in instance.GetComponentsInChildren<Light>(true))
        {
            light.enabled = false;
        }
    }

    private static Material CreateSafePolygonApocalypsePreviewMaterial()
    {
        Material sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(PolygonApocalypsePreviewMaterialPath);

        if (sourceMaterial == null)
        {
            Debug.LogWarning($"Item icon preview material not found: {PolygonApocalypsePreviewMaterialPath}");
            return null;
        }

        Shader shader = FindPreviewShader();

        if (shader == null)
        {
            Debug.LogWarning("Could not find a safe shader for item icon generation.");
            return sourceMaterial;
        }

        Material previewMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "Item Icon Preview Material"
        };

        Texture mainTexture = GetFirstExistingTexture(sourceMaterial, "_BaseMap", "_MainTex");

        if (mainTexture != null)
        {
            SetTextureIfExists(previewMaterial, "_BaseMap", mainTexture);
            SetTextureIfExists(previewMaterial, "_MainTex", mainTexture);
        }

        Color color = GetFirstExistingColor(sourceMaterial, "_BaseColor", "_Color");
        SetColorIfExists(previewMaterial, "_BaseColor", color);
        SetColorIfExists(previewMaterial, "_Color", color);
        SetFloatIfExists(previewMaterial, "_Surface", 0f);
        SetFloatIfExists(previewMaterial, "_Cull", 2f);

        return previewMaterial;
    }

    private static Shader FindPreviewShader()
    {
        string[] shaderNames =
        {
            "Unlit/Texture",
            "Sprites/Default",
            "UI/Default",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Simple Lit",
            "Standard"
        };

        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);

            if (shader != null)
            {
                return shader;
            }
        }

        return null;
    }

    private static Texture GetFirstExistingTexture(Material material, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                Texture texture = material.GetTexture(propertyName);

                if (texture != null)
                {
                    return texture;
                }
            }
        }

        return null;
    }

    private static Color GetFirstExistingColor(Material material, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (material.HasProperty(propertyName))
            {
                return material.GetColor(propertyName);
            }
        }

        return Color.white;
    }

    private static void SetTextureIfExists(Material material, string propertyName, Texture texture)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetTexture(propertyName, texture);
        }
    }

    private static void SetColorIfExists(Material material, string propertyName, Color color)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void SetFloatIfExists(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void ApplyPreviewMaterial(GameObject instance, Material previewMaterial)
    {
        if (previewMaterial == null)
        {
            return;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Material[] replacementMaterials = renderer.sharedMaterials;

            if (replacementMaterials == null || replacementMaterials.Length == 0)
            {
                replacementMaterials = new[] { previewMaterial };
            }
            else
            {
                for (int i = 0; i < replacementMaterials.Length; i++)
                {
                    replacementMaterials[i] = previewMaterial;
                }
            }

            renderer.sharedMaterials = replacementMaterials;
        }
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        if (renderers == null || renderers.Length == 0)
        {
            return new Bounds(root.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static Texture2D CopyTexture(Texture source, int width, int height)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);

        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            result.Apply();
            return result;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    private static Texture2D FitVisiblePixelsToIcon(Texture2D source, int outputSize, int padding)
    {
        if (source == null)
        {
            return null;
        }

        if (!TryFindVisibleBounds(source, out RectInt visibleBounds))
        {
            return source;
        }

        int usableSize = Mathf.Max(1, outputSize - padding * 2);
        float scale = Mathf.Min(
            usableSize / (float)visibleBounds.width,
            usableSize / (float)visibleBounds.height);

        int destinationWidth = Mathf.Max(1, Mathf.RoundToInt(visibleBounds.width * scale));
        int destinationHeight = Mathf.Max(1, Mathf.RoundToInt(visibleBounds.height * scale));
        int destinationX = Mathf.RoundToInt((outputSize - destinationWidth) * 0.5f);
        int destinationY = Mathf.RoundToInt((outputSize - destinationHeight) * 0.5f);

        Texture2D fitted = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false);
        Color32[] clearPixels = new Color32[outputSize * outputSize];

        for (int i = 0; i < clearPixels.Length; i++)
        {
            clearPixels[i] = new Color32(0, 0, 0, 0);
        }

        fitted.SetPixels32(clearPixels);

        for (int y = 0; y < destinationHeight; y++)
        {
            float sourceY = visibleBounds.yMin + ((y + 0.5f) / destinationHeight) * visibleBounds.height;

            for (int x = 0; x < destinationWidth; x++)
            {
                float sourceX = visibleBounds.xMin + ((x + 0.5f) / destinationWidth) * visibleBounds.width;
                Color color = source.GetPixelBilinear(
                    Mathf.Clamp01(sourceX / Mathf.Max(1, source.width - 1)),
                    Mathf.Clamp01(sourceY / Mathf.Max(1, source.height - 1)));

                fitted.SetPixel(destinationX + x, destinationY + y, color);
            }
        }

        fitted.Apply();
        return fitted;
    }

    private static bool TryFindVisibleBounds(Texture2D texture, out RectInt bounds)
    {
        int minX = texture.width;
        int minY = texture.height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                if (texture.GetPixel(x, y).a <= VisibleAlphaThreshold)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            bounds = default;
            return false;
        }

        bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    private static void ConfigureTextureAsSprite(string iconPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;

        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = IconSize;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string currentPath = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = $"{currentPath}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }

    private static string GetSafeFileName(ItemSO item)
    {
        string source = !string.IsNullOrWhiteSpace(item.itemID)
            ? item.itemID
            : !string.IsNullOrWhiteSpace(item.itemName)
                ? item.itemName
                : item.name;

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            source = source.Replace(invalidChar, '_');
        }

        return source.Replace(' ', '_');
    }
}
