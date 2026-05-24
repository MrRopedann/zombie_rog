using System.Collections.Generic;
using UnityEngine;

public class CharacterSkinSelector : MonoBehaviour
{
    [Header("Groups")]
    [SerializeField] private GameObject skinPrefab;
    [SerializeField] private GameObject charactersMale;
    [SerializeField] private GameObject charactersFemale;

    private GameObject[] maleSkins;
    private GameObject[] femaleSkins;

    private int currentSkinIndex;
    private bool isMale = true;

    public PlayerCharacterGender CurrentGender => isMale ? PlayerCharacterGender.Male : PlayerCharacterGender.Female;
    public int CurrentSkinIndex => currentSkinIndex;
    public string CurrentModelId => $"{(isMale ? "male" : "female")}_{currentSkinIndex}";

    private void Awake()
    {
        EnsureSkinPrefabInstance();
        ResolveMissingGroups();
        maleSkins = GetChildren(charactersMale);
        femaleSkins = GetChildren(charactersFemale);

        SelectMale();
    }

    public void SelectMale()
    {
        isMale = true;
        currentSkinIndex = 0;

        if (charactersMale != null)
            charactersMale.SetActive(true);

        if (charactersFemale != null)
            charactersFemale.SetActive(false);

        ShowCurrentSkin();
    }

    public void SelectFemale()
    {
        isMale = false;
        currentSkinIndex = 0;

        if (charactersMale != null)
            charactersMale.SetActive(false);

        if (charactersFemale != null)
            charactersFemale.SetActive(true);

        ShowCurrentSkin();
    }

    public void SelectGender(PlayerCharacterGender gender)
    {
        if (gender == PlayerCharacterGender.Female)
            SelectFemale();
        else
            SelectMale();
    }

    public void SetSkinIndex(int index)
    {
        GameObject[] skins = GetCurrentSkins();
        if (skins.Length == 0)
            return;

        currentSkinIndex = Mathf.Clamp(index, 0, skins.Length - 1);
        ShowCurrentSkin();
    }

    public void NextSkin()
    {
        GameObject[] skins = GetCurrentSkins();

        if (skins.Length == 0)
            return;

        currentSkinIndex++;

        if (currentSkinIndex >= skins.Length)
            currentSkinIndex = 0;

        ShowCurrentSkin();
    }

    public void PreviousSkin()
    {
        GameObject[] skins = GetCurrentSkins();

        if (skins.Length == 0)
            return;

        currentSkinIndex--;

        if (currentSkinIndex < 0)
            currentSkinIndex = skins.Length - 1;

        ShowCurrentSkin();
    }

    private void ShowCurrentSkin()
    {
        currentSkinIndex = ApplySkin(transform, CurrentGender, currentSkinIndex);
    }

    private GameObject[] GetCurrentSkins()
    {
        return isMale ? maleSkins : femaleSkins;
    }

    private void EnsureSkinPrefabInstance()
    {
        if (FindChildByName(transform, "Male") != null && FindChildByName(transform, "Female") != null)
            return;

        if (skinPrefab == null)
            skinPrefab = Resources.Load<GameObject>("Prefabs/Character/Skin");

        if (skinPrefab == null)
            return;

        GameObject instance = Instantiate(skinPrefab, transform);
        instance.name = skinPrefab.name;

        Transform instanceTransform = instance.transform;
        instanceTransform.localPosition = Vector3.zero;
        instanceTransform.localRotation = Quaternion.identity;
        instanceTransform.localScale = Vector3.one;
    }

    private void ResolveMissingGroups()
    {
        if (charactersMale == null)
            charactersMale = FindChildByName(transform, "Male");

        if (charactersFemale == null)
            charactersFemale = FindChildByName(transform, "Female");
    }

    private GameObject FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child.gameObject;

            GameObject nested = FindChildByName(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    public static void ApplyProfileToCharacterRoot(Transform characterRoot, PlayerCharacterProfile profile)
    {
        if (characterRoot == null || profile == null)
            return;

        ApplySkin(characterRoot, profile.gender, profile.modelIndex);
    }

    private static int ApplySkin(Transform characterRoot, PlayerCharacterGender gender, int requestedIndex)
    {
        if (characterRoot == null)
            return 0;

        GameObject maleGroup = FindChildByNameStatic(characterRoot, "Male");
        GameObject femaleGroup = FindChildByNameStatic(characterRoot, "Female");

        GameObject[] maleSkinObjects = GetChildrenStatic(maleGroup);
        GameObject[] femaleSkinObjects = GetChildrenStatic(femaleGroup);
        GameObject[] selectedSkinObjects = gender == PlayerCharacterGender.Female ? femaleSkinObjects : maleSkinObjects;

        DisableObjects(maleSkinObjects);
        DisableObjects(femaleSkinObjects);

        if (maleGroup != null)
            maleGroup.SetActive(gender == PlayerCharacterGender.Male);

        if (femaleGroup != null)
            femaleGroup.SetActive(gender == PlayerCharacterGender.Female);

        if (selectedSkinObjects.Length == 0)
        {
            ApplyGenderAttachments(characterRoot, null, gender);
            return 0;
        }

        int clampedIndex = Mathf.Clamp(requestedIndex, 0, selectedSkinObjects.Length - 1);
        GameObject selectedSkin = selectedSkinObjects[clampedIndex];
        if (selectedSkin != null)
            selectedSkin.SetActive(true);

        ApplyGenderAttachments(characterRoot, selectedSkin != null ? selectedSkin.name : null, gender);
        return clampedIndex;
    }

    private static void DisableObjects(GameObject[] objects)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                objects[i].SetActive(false);
        }
    }

    private static void ApplyGenderAttachments(Transform characterRoot, string selectedSkinName, PlayerCharacterGender gender)
    {
        string modelToken = ResolveModelToken(selectedSkinName);
        Transform[] allTransforms = characterRoot.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < allTransforms.Length; i++)
        {
            GameObject gameObject = allTransforms[i].gameObject;
            string objectName = gameObject.name;

            if (!objectName.StartsWith("SM_Chr_Attach_"))
                continue;

            bool isMaleAttachment = objectName.Contains("_Male_");
            bool isFemaleAttachment = objectName.Contains("_Female_");
            if (!isMaleAttachment && !isFemaleAttachment)
                continue;

            bool genderMatches = gender == PlayerCharacterGender.Male ? isMaleAttachment : isFemaleAttachment;
            bool modelMatches = string.IsNullOrWhiteSpace(modelToken) || objectName.Contains(modelToken);
            gameObject.SetActive(genderMatches && modelMatches);
        }
    }

    private static string ResolveModelToken(string skinName)
    {
        if (string.IsNullOrWhiteSpace(skinName))
            return null;

        string token = skinName;
        if (token.StartsWith("SM_Chr_"))
            token = token.Substring("SM_Chr_".Length);

        int lastSeparator = token.LastIndexOf('_');
        if (lastSeparator > 0 && IsDigitsOnly(token.Substring(lastSeparator + 1)))
            token = token.Substring(0, lastSeparator);

        return token;
    }

    private static bool IsDigitsOnly(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]))
                return false;
        }

        return true;
    }

    private static GameObject FindChildByNameStatic(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child.gameObject;

            GameObject nested = FindChildByNameStatic(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private GameObject[] GetChildren(GameObject parent)
    {
        return GetChildrenStatic(parent);
    }

    private static GameObject[] GetChildrenStatic(GameObject parent)
    {
        List<GameObject> result = new List<GameObject>();

        if (parent == null)
            return result.ToArray();

        for (int i = 0; i < parent.transform.childCount; i++)
        {
            GameObject child = parent.transform.GetChild(i).gameObject;

            if (child.name == "Root")
                continue;

            result.Add(child);
        }

        return result.ToArray();
    }
}
