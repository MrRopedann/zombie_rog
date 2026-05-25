using UnityEngine;
using UnityEngine.UI;

public class CraftingIngredientRowUI : MonoBehaviour
{
    [SerializeField] private Text label;

    public void Bind(ItemAmount ingredient, int availableAmount, Font font)
    {
        if (label == null)
            label = CreateLabel(font);

        string name = ResolveName(ingredient);
        int required = ingredient != null ? Mathf.Max(1, ingredient.amount) : 1;
        label.text = $"{name}: {availableAmount}/{required}";
        label.color = availableAmount >= required ? new Color(0.75f, 1f, 0.78f) : new Color(1f, 0.58f, 0.52f);
    }

    private static string ResolveName(ItemAmount ingredient)
    {
        if (ingredient == null)
            return "Item";

        if (ingredient.item != null)
            return !string.IsNullOrWhiteSpace(ingredient.item.itemName) ? ingredient.item.itemName : ingredient.item.name;

        return !string.IsNullOrWhiteSpace(ingredient.itemId) ? ingredient.itemId : "Item";
    }

    private Text CreateLabel(Font font)
    {
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(transform, false);
        Text text = labelObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleLeft;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return text;
    }
}
