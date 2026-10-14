using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed class LightCharacterPanel : MonoBehaviour
{
    public Sprite panelSprite, normalTile, selectedOutline, lockSprite, silhouetteSprite, selectionBadge;
    private CharacterButtonScrpt[] slots;
    private int lastSelection = -2;

    private void OnEnable() { Rebuild(); }
    private void OnRectTransformDimensionsChange() { if (slots != null) LayoutSlots(); }
    private void LateUpdate()
    {
        if (DressUpScript.instance == null) return;
        int selected = DressUpScript.instance.ActivePlayerIndex;
        if (selected != lastSelection || slots.Any(slot =>
            slot.transform.Find("Image") != null && slot.transform.Find("Image").gameObject.activeSelf !=
            (slot.Index == selected && (slot.Lock == null || !slot.Lock.activeSelf)))) RefreshSelection();
    }

    public void Rebuild()
    {
        slots = GetComponentsInChildren<CharacterButtonScrpt>(true).OrderBy(s => s.Index).ToArray();
        var grid = GetComponent<GridLayoutGroup>();
        if (grid != null) grid.enabled = false;
        var panel = GetComponent<Image>();
        if (panel != null && panelSprite != null) { panel.sprite = panelSprite; panel.type = Image.Type.Sliced; panel.color = Color.white; }
        LayoutSlots();
        foreach (var slot in slots) StyleSlot(slot);
        RefreshSelection();
    }

    private void LayoutSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var rect = (RectTransform)slots[i].transform;
            int row = i / 2, column = i % 2;
            float left = column == 0 ? 0.075f : 0.525f;
            float top = 0.895f - row * 0.166f;
            rect.anchorMin = new Vector2(left, top - 0.151f);
            rect.anchorMax = new Vector2(left + 0.40f, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }

    private void StyleSlot(CharacterButtonScrpt slot)
    {
        var baseImage = slot.GetComponent<Image>();
        if (baseImage != null) { baseImage.sprite = normalTile; baseImage.color = Color.white; baseImage.type = Image.Type.Sliced; }
        Transform border = slot.transform.Find("Image");
        if (border != null && border.GetComponent<Image>() != null)
        {
            var image = border.GetComponent<Image>();
            image.sprite = selectedOutline; image.type = Image.Type.Sliced; image.raycastTarget = false;
        }
        // Enlarge the existing layered character, retaining all sprite bindings.
        foreach (Transform child in slot.transform)
        {
            if (child == border || child.gameObject == slot.Lock || child.name == "Light Selection Badge") continue;
            bool layered = child.GetComponentsInChildren<Image>(true).Any(image => image.name == "HairInFront" || image.name == "Eyes");
            if (!layered && child.name != "Character") continue;
            var rect = child as RectTransform;
            if (rect == null) continue;
            rect.anchorMin = new Vector2(-0.40f, -0.75f);
            rect.anchorMax = new Vector2(1.40f, 1.05f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
        if (slot.Lock != null)
        {
            var cover = slot.Lock.GetComponent<Image>();
            if (cover != null) { cover.sprite = normalTile; cover.type = Image.Type.Sliced; cover.color = new Color(0.89f, 0.85f, 0.96f, 1); }
            var silhouette = Graphic(slot.Lock.transform, "Light Locked Silhouette", silhouetteSprite);
            silhouette.rectTransform.anchorMin = new Vector2(0.17f, 0.04f);
            silhouette.rectTransform.anchorMax = new Vector2(0.83f, 0.94f);
            silhouette.rectTransform.offsetMin = silhouette.rectTransform.offsetMax = Vector2.zero;
            silhouette.transform.SetAsFirstSibling();
            Transform oldLock = slot.Lock.transform.Find("Image (2)");
            var lockImage = oldLock != null ? oldLock.GetComponent<Image>() : null;
            if (lockImage != null)
            {
                lockImage.sprite = lockSprite; lockImage.color = Color.white; lockImage.preserveAspect = true;
                lockImage.rectTransform.anchorMin = new Vector2(0.30f, 0.12f);
                lockImage.rectTransform.anchorMax = new Vector2(0.70f, 0.60f);
                lockImage.rectTransform.offsetMin = lockImage.rectTransform.offsetMax = Vector2.zero;
                lockImage.transform.SetAsLastSibling();
                lockImage.raycastTarget = false;
            }
        }
        var badge = Graphic(slot.transform, "Light Selection Badge", selectionBadge);
        badge.maskable = false;
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = Vector2.one;
        badge.rectTransform.sizeDelta = new Vector2(30, 30);
        badge.rectTransform.anchoredPosition = new Vector2(-5, 2);
    }

    public void RefreshSelection()
    {
        if (slots == null) return;
        lastSelection = DressUpScript.instance != null ? DressUpScript.instance.ActivePlayerIndex : 0;
        foreach (var slot in slots)
        {
            bool selected = slot.Index == lastSelection && (slot.Lock == null || !slot.Lock.activeSelf);
            Transform border = slot.transform.Find("Image");
            if (border != null) border.gameObject.SetActive(selected);
            Transform badge = slot.transform.Find("Light Selection Badge");
            if (badge != null) badge.gameObject.SetActive(selected);
        }
    }

    private static Image Graphic(Transform parent, string name, Sprite sprite)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false); obj.layer = parent.gameObject.layer;
        var image = obj.GetComponent<Image>();
        image.sprite = sprite; image.color = Color.white; image.preserveAspect = true; image.raycastTarget = false;
        return image;
    }
}
