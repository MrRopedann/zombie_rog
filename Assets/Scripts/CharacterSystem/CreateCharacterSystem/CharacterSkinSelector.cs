using System.Collections.Generic;
using UnityEngine;

public class CharacterSkinSelector : MonoBehaviour
{
    [Header("Groups")]
    [SerializeField] private GameObject charactersMale;
    [SerializeField] private GameObject charactersFemale;

    private GameObject[] maleSkins;
    private GameObject[] femaleSkins;

    private int currentSkinIndex;
    private bool isMale = true;

    private void Awake()
    {
        maleSkins = GetChildren(charactersMale);
        femaleSkins = GetChildren(charactersFemale);

        SelectMale();
    }

    public void SelectMale()
    {
        isMale = true;
        currentSkinIndex = 0;

        charactersMale.SetActive(true);
        charactersFemale.SetActive(false);

        ShowCurrentSkin();
    }

    public void SelectFemale()
    {
        isMale = false;
        currentSkinIndex = 0;

        charactersMale.SetActive(false);
        charactersFemale.SetActive(true);

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
        GameObject[] skins = GetCurrentSkins();

        for (int i = 0; i < maleSkins.Length; i++)
            maleSkins[i].SetActive(false);

        for (int i = 0; i < femaleSkins.Length; i++)
            femaleSkins[i].SetActive(false);

        if (skins.Length == 0)
            return;

        skins[currentSkinIndex].SetActive(true);
    }

    private GameObject[] GetCurrentSkins()
    {
        return isMale ? maleSkins : femaleSkins;
    }

    private GameObject[] GetChildren(GameObject parent)
    {
        List<GameObject> result = new List<GameObject>();

        for (int i = 0; i < parent.transform.childCount; i++)
        {
            GameObject child = parent.transform.GetChild(i).gameObject;

            // Пропускаем Root
            if (child.name == "Root")
                continue;

            result.Add(child);
        }

        return result.ToArray();
    }
}