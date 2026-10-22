using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed class LightCustomizePanel : MonoBehaviour
{
    public BodyParts[] categories;
    public Image[] categoryImages, categoryIcons;
    public Text[] categoryLabels;
    public GameObject[] categoryBadges;
    public Sprite normalCard, selectedCard;
    public Button[] itemButtons;
    public Image[] itemImages;
    public GameObject[] itemSelections;
    public Button previousButton, nextButton, colorButton, partButton, clearButton;
    public Text partLabel;
    public Button[] partTabs;
    public Text[] partTabLabels;
    public Image[] partTabImages;
    public Image[] pageDots;
    public RectTransform colorHost;
    public Text colorLabel;
    public Image colorIcon, colorChevron;
    public Sprite colorWheelIcon, backIcon;
    public GameObject[] wardrobeElements;
    [Header("Color availability")]
    [Tooltip("Parts where the Color button is disabled. Add or remove parts to control recoloring per section.")]
    public List<ChildrenParts> disabledColorParts = new List<ChildrenParts> { ChildrenParts.Eyes };
    private bool editingColor;
    private readonly List<GameObject> visibleWardrobe = new List<GameObject>();

    private BodyParts currentCategory;
    private List<ChildrenParts> parts = new List<ChildrenParts>();
    private Sprite[] items = Array.Empty<Sprite>();
    private Dictionary<string, Sprite> thumbnails = new Dictionary<string, Sprite>();
    private Sprite noneSprite;
    private int partIndex, page, lastPlayer = -1;
    private bool bound;
    public ChildrenParts CurrentPart => parts.Count > 0 ? parts[partIndex] : ChildrenParts.TShirts;

    private void Awake()
    {
        // Older scene overrides may retain a second copy after a prefab update.
        if (GetComponent<LightCustomizePanel>() != this) enabled = false;
    }

    private void Start()
    {
        Bind();
        if (categories != null && categories.Length > 1) ShowCategory(categories[1]);
    }

    private void Bind()
    {
        if (bound) return;
        bound = true;
        ResolvePartTabs();
        for (int i = 0; i < itemButtons.Length; i++)
        {
            int slot = i;
            itemButtons[i].onClick.AddListener(() => ChooseItem(slot));
        }
        previousButton.onClick.AddListener(() => ChangePage(-1));
        nextButton.onClick.AddListener(() => ChangePage(1));
        partButton.onClick.AddListener(NextPart);
        if (partTabs != null)
            for (int i = 0; i < partTabs.Length; i++)
            {
                int index = i;
                partTabs[i].onClick.AddListener(() => SelectPart(index));
            }
        clearButton.onClick.AddListener(ClearPart);
        colorButton.onClick.AddListener(OpenColor);
    }

    private void ResolvePartTabs()
    {
        // Scene overrides can leave an older row beside the inherited prefab row.
        Transform row = transform.Cast<Transform>().FirstOrDefault(t => t.name == "Light Wardrobe Parts" && t.gameObject.activeSelf)
            ?? transform.Find("Light Wardrobe Parts");
        if (row == null) return;
        foreach (Transform sibling in transform)
            if (sibling.name == row.name) sibling.gameObject.SetActive(sibling == row);
        var buttons = new List<Button>();
        var labels = new List<Text>();
        var images = new List<Image>();
        for (int i = 0; ; i++)
        {
            Transform tab = row.Find("Part Tab " + i);
            if (tab == null) break;
            foreach (Transform sibling in row)
                if (sibling.name == tab.name) sibling.gameObject.SetActive(sibling == tab);
            Transform label = tab.Find("Label");
            if (label == null) continue;
            foreach (Transform sibling in tab)
                if (sibling.name == "Label") sibling.gameObject.SetActive(sibling == label);
            buttons.Add(tab.GetComponent<Button>());
            labels.Add(label.GetComponent<Text>());
            images.Add(tab.GetComponent<Image>());
        }
        partTabs = buttons.ToArray(); partTabLabels = labels.ToArray(); partTabImages = images.ToArray();
        wardrobeElements = transform.Cast<Transform>().Where(t => t.gameObject.activeSelf &&
            (t.name.StartsWith("Light Wardrobe") || t.name == "Light Clear Part")).Select(t => t.gameObject).ToArray();
    }

    private void LateUpdate()
    {
        if (DressUpScript.instance != null && lastPlayer != DressUpScript.instance.ActivePlayerIndex && parts.Count > 0)
        {
            if (editingColor) CloseColorView();
            lastPlayer = DressUpScript.instance.ActivePlayerIndex;
            MatchCurrentItem();
            Refresh();
        }
    }

    public void ShowCategory(BodyParts category)
    {
        if (category == null) return;
        currentCategory = category;
        parts = category.CategoryParts != null ? new List<ChildrenParts>(category.CategoryParts) : new List<ChildrenParts>();
        partIndex = 0;
        LoadPart();
        RefreshCategories();
    }

    private void NextPart()
    {
        if (parts.Count < 2) return;
        partIndex = (partIndex + 1) % parts.Count;
        LoadPart();
    }

    public void SelectPart(int index)
    {
        if (index < 0 || index >= parts.Count) return;
        if (editingColor) CloseColorView();
        partIndex = index;
        LoadPart();
    }

    private void LoadPart()
    {
        if (parts.Count == 0) return;
        string path = "Sprites/" + CurrentPart;
        var loaded = Resources.LoadAll<Sprite>(path);
        noneSprite = loaded.FirstOrDefault(sprite => sprite.name == "0");
        items = loaded.Where(sprite => sprite.name != "0").OrderBy(sprite => Number(sprite.name)).ThenBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
        thumbnails = Resources.LoadAll<Sprite>(path + "Icons").GroupBy(sprite => sprite.name).ToDictionary(group => group.Key, group => group.First());
        MatchCurrentItem();
        Refresh();
    }

    private static int Number(string name) { return int.TryParse(name, out int number) ? number : int.MaxValue; }

    private Image PartImage()
    {
        if (DressUpScript.instance == null || DressUpScript.instance.ActivdPlayer == null) return null;
        Transform part = DressUpScript.instance.ActivdPlayer.transform.Find(CurrentPart.ToString());
        return part != null ? part.GetComponent<Image>() : null;
    }

    private void MatchCurrentItem()
    {
        var image = PartImage();
        int index = image != null ? Array.FindIndex(items, sprite => sprite == image.sprite) : -1;
        page = index >= 0 ? index / 3 : 0;
    }

    public void ChangePage(int delta)
    {
        page = Mathf.Clamp(page + delta, 0, Math.Max(0, (items.Length - 1) / 3));
        Refresh();
    }

    private void ChooseItem(int slot)
    {
        int index = page * 3 + slot;
        if (index >= 0 && index < items.Length) ApplyItem(items[index]);
    }

    private void ApplyItem(Sprite sprite)
    {
        var dress = DressUpScript.instance;
        if (dress == null || sprite == null) return;
        dress.ActualPart = CurrentPart;
        dress.DressUp(sprite, CurrentPart.ToString());
        if (SavePanel.instance != null) SavePanel.instance.SaveCharacter(dress.ActivePlayerIndex, CurrentPart, sprite.name);
        Refresh();
    }

    private void ClearPart() { ApplyItem(noneSprite); }

    public void OpenColor()
    {
        if (editingColor) { CloseColorView(); return; }
        if (!CanColorCurrentPart()) return;
        var dress = DressUpScript.instance;
        var manager = PanelChoicesManager.Instance;
        var current = PartImage();
        if (dress == null || manager == null || current == null || current.sprite == null || current.sprite.name == "0") return;
        dress.ActualPart = CurrentPart;
        dress.ChangeColor(CurrentPart.ToString(), out Image character, out Image portrait, out Image icon);
        manager.SetCharacterImages(character, portrait, icon);
        var picker = manager.ActivePanel().colorPicker.GetComponent<ColorPicker>();
        picker.InlineLayout = true;
        var rect = (RectTransform)picker.transform;
        rect.SetParent(colorHost, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        picker.Closed = CloseColorView;
        editingColor = true;
        SetColorView(true);
        manager.ShowColorPicker();
        picker.InitializeView();
    }

    private void CloseColorView()
    {
        if (!editingColor) return;
        editingColor = false;
        var manager = PanelChoicesManager.Instance;
        if (manager != null) manager.ActivePanel().colorPicker.gameObject.SetActive(false);
        SetColorView(false);
        Refresh();
    }

    private void SetColorView(bool visible)
    {
        colorHost.gameObject.SetActive(visible);
        foreach (var category in categories) category.gameObject.SetActive(!visible);
        if (visible)
        {
            visibleWardrobe.Clear();
            foreach (var element in wardrobeElements)
            {
                if (element.activeSelf) visibleWardrobe.Add(element);
                element.SetActive(false);
            }
        }
        else
        {
            foreach (var element in visibleWardrobe) element.SetActive(true);
            visibleWardrobe.Clear();
        }
        colorLabel.text = visible ? "Back" : "Color";
        colorIcon.sprite = visible ? backIcon : colorWheelIcon;
        colorIcon.color = visible ? new Color(.55f, .42f, .91f) : Color.white;
        colorChevron.gameObject.SetActive(!visible);
    }

    private void OnDisable() { CloseColorView(); }

    private bool CanColorCurrentPart()
    {
        return parts.Count > 0 && (disabledColorParts == null || !disabledColorParts.Contains(CurrentPart));
    }

    private void RefreshCategories()
    {
        for (int i = 0; i < categories.Length; i++)
        {
            bool selected = categories[i] == currentCategory;
            categoryImages[i].sprite = selected ? selectedCard : normalCard;
            categoryIcons[i].color = selected ? Color.white : new Color(.25f, .18f, .38f);
            categoryLabels[i].color = selected ? Color.white : new Color(.208f, .157f, .31f);
            categoryBadges[i].SetActive(selected);
        }
    }

    private void Refresh()
    {
        var current = PartImage();
        for (int slot = 0; slot < itemButtons.Length; slot++)
        {
            int index = page * 3 + slot;
            bool available = index < items.Length;
            itemButtons[slot].interactable = available;
            itemImages[slot].enabled = available;
            if (available)
            {
                Sprite item = items[index];
                itemImages[slot].sprite = thumbnails.TryGetValue(item.name, out Sprite thumb) ? thumb : item;
                itemImages[slot].color = Color.white;
            }
            itemSelections[slot].SetActive(available && current != null && current.sprite == items[index]);
        }
        previousButton.interactable = page > 0;
        nextButton.interactable = (page + 1) * 3 < items.Length;
        clearButton.interactable = noneSprite != null;
        colorButton.interactable = editingColor || CanColorCurrentPart() && current != null && current.sprite != null && current.sprite.name != "0";
        partButton.interactable = parts.Count > 1;
        partLabel.text = Friendly(CurrentPart) + (parts.Count > 1 ? "  ›" : "");
        if (partTabs != null && partTabs.Length > 0)
        {
            var row = (RectTransform)partTabs[0].transform.parent;
            Vector2 min = row.anchorMin;
            min.y = parts.Count > 4 ? .382f : .397f;
            row.anchorMin = min;
        }
        if (partTabs != null)
            for (int i = 0; i < partTabs.Length; i++)
            {
                bool available = i < parts.Count;
                partTabs[i].gameObject.SetActive(available);
                if (!available) continue;
                int count = Mathf.Min(parts.Count, partTabs.Length);
                int columns = count > 4 ? Mathf.CeilToInt(count / 2f) : count;
                int rows = Mathf.CeilToInt(count / (float)columns);
                float width = 1f / columns;
                float height = 1f / rows;
                var tabRect = (RectTransform)partTabs[i].transform;
                int column = i % columns, rowIndex = i / columns;
                tabRect.anchorMin = new Vector2(column * width + .008f, 1f - (rowIndex + 1) * height + .02f);
                tabRect.anchorMax = new Vector2((column + 1) * width - .008f, 1f - rowIndex * height - .02f);
                tabRect.offsetMin = tabRect.offsetMax = Vector2.zero;
                partTabLabels[i].text = Friendly(parts[i]);
                partTabLabels[i].color = i == partIndex ? Color.white : new Color(.208f, .157f, .31f);
                partTabImages[i].sprite = i == partIndex ? selectedCard : normalCard;
            }
        for (int i = 0; i < pageDots.Length; i++)
            pageDots[i].color = i == 1 ? new Color(.94f, .65f, .79f) : new Color(.70f, .59f, .89f, i == 0 && page == 0 || i == 2 && !nextButton.interactable ? .4f : 1f);
    }

    private static string Friendly(ChildrenParts part)
    {
        switch (part)
        {
            case ChildrenParts.TShirts: return "Tops";
            case ChildrenParts.HairInFront: return "Front hair";
            case ChildrenParts.atTheBack: return "Back hair";
            case ChildrenParts.Ear: return "Ears";
            default: return part.ToString();
        }
    }

    // Editor preview and validation use actual project sprites without changing saves.
    public void PreparePreview() { ShowCategory(categories[1]); }
    public void InitializeForValidation() { Bind(); PreparePreview(); }
    public int ItemCount => items.Length;
    public int CurrentPage => page;
}
