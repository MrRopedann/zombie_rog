using System;
using UnityEngine;
using UnityEngine.UI;

public class CraftingRecipeSlotUI : MonoBehaviour
{
    [SerializeField] private Text label;
    [SerializeField] private Button button;

    private CraftingRecipe recipe;
    private Action<CraftingRecipe> onSelected;

    public void Bind(CraftingRecipe recipe, Font font, Action<CraftingRecipe> onSelected)
    {
        this.recipe = recipe;
        this.onSelected = onSelected;

        if (button == null)
            button = gameObject.GetComponent<Button>() ?? gameObject.AddComponent<Button>();

        if (label == null)
            label = CreateLabel(font);

        label.text = recipe != null ? recipe.DisplayNameOrId : "Recipe";
        button.onClick.RemoveListener(Select);
        button.onClick.AddListener(Select);
    }

    private void Select()
    {
        onSelected?.Invoke(recipe);
    }

    private Text CreateLabel(Font font)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(transform, false);
        Text text = labelObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = 15;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 0f);
        rect.offsetMax = new Vector2(-10f, 0f);
        return text;
    }
}
