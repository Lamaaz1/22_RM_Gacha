using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ColorPicker : MonoBehaviour
{
    public Transform Content;
    public List<ColorBtn> Colors;
    public List<Color> LegacyColors = new List<Color>();
    public int PaletteCount => LegacyColors != null && LegacyColors.Count > 0 ? LegacyColors.Count : Colors != null ? Colors.Count : 0;
    public bool HasColorAtIndex(int index) => index >= 0 && index < PaletteCount;
    public bool InlineLayout;
    public System.Action Closed;
    private RectTransform wheel, wheelCursor, valueCursor, alphaCursor;
    private Image preview;
    private Text hexLabel;
    private Texture2D wheelTexture, valueTexture, alphaTexture, cursorTexture;
    private float hue, saturation, brightness = 1f, alpha = 1f;

    private void OnEnable()
    {
        InitializeView();
    }

    public void InitializeView()
    {
        if (wheel == null) BuildPicker();
        var manager = PanelChoicesManager.Instance;
        var panel = manager != null ? manager.ActivePanel() : null;
        Color initial = panel != null && panel.CharacterSelectedImg != null
            ? panel.CharacterSelectedImg.color : Color.white;
        Color.RGBToHSV(initial, out hue, out saturation, out brightness);
        alpha = initial.a;
        Refresh();
    }
    public Color ColoratItem(int i)
    {
        if (LegacyColors != null && i >= 0 && i < LegacyColors.Count) return LegacyColors[i];
        // Retain the palette references to restore old saved colors.
        return Colors != null && i >= 0 && i < Colors.Count && Colors[i] != null
            ? Colors[i].GetComponent<Image>().color : Color.white;
    }

    private void BuildPicker()
    {
        if (InlineLayout)
        {
            foreach (Transform child in transform) child.gameObject.SetActive(false);
            var oldBackground = GetComponent<Image>();
            if (oldBackground != null) oldBackground.enabled = false;
        }
        if (Colors != null)
            for (int i = 0; i < Colors.Count; i++)
                if (Colors[i] != null) Colors[i].i = i;

        // Hide the old scroll view while retaining the title and close button.
        if (Content != null)
        {
            Transform oldBody = Content;
            while (oldBody.parent != null && oldBody.parent != transform)
                oldBody = oldBody.parent;
            if (oldBody != transform) oldBody.gameObject.SetActive(false);
        }
        RectTransform body = Rect("Continuous Colors", transform, Vector2.zero, Vector2.one);
        body.offsetMin = new Vector2(18, 16);
        body.offsetMax = new Vector2(-18, InlineLayout ? -52 : -112);
        var background = body.gameObject.AddComponent<Image>();
        background.color = new Color(0.953f, 0.933f, 1f);

        RectTransform area = Rect("Wheel Area", body, new Vector2(0.04f, 0.34f), new Vector2(0.96f, 0.98f));
        wheel = Rect("Hue Saturation Wheel", area, Vector2.zero, Vector2.one);
        var aspect = wheel.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 1;
        wheelTexture = MakeWheel();
        wheel.gameObject.AddComponent<RawImage>().texture = wheelTexture;
        wheel.gameObject.AddComponent<ColorPickerPointer>().Changed = PickWheel;

        cursorTexture = MakeCursor();
        wheelCursor = Cursor("Wheel Selection", wheel);
        valueTexture = MakeStrip();
        alphaTexture = MakeStrip();
        valueCursor = Strip(body, "Brightness", 0.22f, valueTexture, point =>
        {
            brightness = Mathf.Clamp01(point.x);
            Apply();
        });
        alphaCursor = Strip(body, "Opacity", 0.11f, alphaTexture, point =>
        {
            alpha = Mathf.Clamp01(point.x);
            Apply();
        });
        RectTransform swatch = Rect("Selected Color", body, new Vector2(0.04f, 0.01f), new Vector2(0.20f, 0.08f));
        preview = swatch.gameObject.AddComponent<Image>();
        preview.raycastTarget = false;
        hexLabel = Label("Color Code", body, new Vector2(0.24f, 0.01f), new Vector2(0.96f, 0.08f));
        if (InlineLayout)
        {
            var title = Label("Inline Color Title", transform, new Vector2(.05f, .88f), new Vector2(.95f, 1f));
            title.text = "Select Color";
            title.alignment = TextAnchor.MiddleCenter;
            title.fontStyle = FontStyle.Bold;
        }
    }

    private RectTransform Strip(Transform parent, string name, float y, Texture2D texture, System.Action<Vector2> changed)
    {
        var label = Label(name + " Label", parent, new Vector2(0.04f, y), new Vector2(0.27f, y + 0.08f));
        label.text = name;
        RectTransform strip = Rect(name, parent, new Vector2(0.29f, y), new Vector2(0.96f, y + 0.08f));
        strip.gameObject.AddComponent<RawImage>().texture = texture;
        strip.gameObject.AddComponent<ColorPickerPointer>().Changed = changed;
        return Cursor(name + " Selection", strip);
    }

    private static Text Label(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var label = Rect(name, parent, min, max).gameObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 26;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 10;
        label.resizeTextMaxSize = 26;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = new Color(0.208f, 0.157f, 0.310f);
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private RectTransform Cursor(string name, Transform parent)
    {
        RectTransform rect = Rect(name, parent, Vector2.one * 0.5f, Vector2.one * 0.5f);
        rect.sizeDelta = Vector2.one * 24;
        var image = rect.gameObject.AddComponent<RawImage>();
        image.texture = cursorTexture;
        image.raycastTarget = false;
        return rect;
    }

    private void PickWheel(Vector2 point)
    {
        Vector2 direction = (point - Vector2.one * 0.5f) * 2f;
        saturation = Mathf.Clamp01(direction.magnitude);
        if (saturation > 0.0001f)
            hue = Mathf.Repeat(Mathf.Atan2(direction.y, direction.x) / (2f * Mathf.PI), 1f);
        Apply();
    }

    private Color SelectedColor()
    {
        Color selected = Color.HSVToRGB(hue, saturation, brightness);
        selected.a = alpha;
        return selected;
    }

    private void Apply()
    {
        Color selected = SelectedColor();
        var manager = PanelChoicesManager.Instance;
        var dress = DressUpScript.instance;
        var panel = manager != null ? manager.ActivePanel() : null;
        if (panel != null && panel.CharacterSelectedImg != null &&
            panel.CharacterSelectedImg.sprite != null && panel.CharacterSelectedImg.sprite.name != "0")
        {
            manager.ChangeColor(selected);
            if (dress != null && SavePanel.instance != null)
                SavePanel.instance.SaveColorPart(dress.ActivePlayerIndex, dress.ActualPart, selected);
        }
        Refresh();
    }

    private void Refresh()
    {
        Color selected = SelectedColor();
        preview.color = selected;
        hexLabel.text = "#" + ColorUtility.ToHtmlStringRGBA(selected);
        Vector2 position = Vector2.one * 0.5f + new Vector2(
            Mathf.Cos(hue * Mathf.PI * 2f), Mathf.Sin(hue * Mathf.PI * 2f)) * saturation * 0.5f;
        wheelCursor.anchorMin = wheelCursor.anchorMax = position;
        valueCursor.anchorMin = valueCursor.anchorMax = new Vector2(brightness, 0.5f);
        alphaCursor.anchorMin = alphaCursor.anchorMax = new Vector2(alpha, 0.5f);
        Color full = Color.HSVToRGB(hue, saturation, 1);
        for (int x = 0; x < 256; x++)
        {
            // Composite over a checkerboard to make opacity visible.
            for (int y = 0; y < 16; y++)
            {
                valueTexture.SetPixel(x, y, Color.Lerp(Color.black, full, x / 255f));
                Color checker = ((x / 8 + y / 8) % 2 == 0) ? Color.gray : Color.white;
                Color opaque = selected;
                opaque.a = 1;
                alphaTexture.SetPixel(x, y, Color.Lerp(checker, opaque, x / 255f));
            }
        }
        valueTexture.Apply();
        alphaTexture.Apply();
    }

    private static Texture2D MakeStrip()
    {
        var texture = new Texture2D(256, 16, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private static Texture2D MakeWheel()
    {
        const int size = 512;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x / (size - 1f), y / (size - 1f)) * 2f - Vector2.one;
                Color c = Color.HSVToRGB(Mathf.Repeat(Mathf.Atan2(p.y, p.x) / (2f * Mathf.PI), 1), Mathf.Clamp01(p.magnitude), 1);
                c.a = Mathf.Clamp01((1f - p.magnitude) * size * 0.5f);
                pixels[y * size + x] = c;
            }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Texture2D MakeCursor()
    {
        var texture = new Texture2D(48, 48, TextureFormat.RGBA32, false);
        var pixels = new Color[48 * 48];
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
            {
                float radius = Vector2.Distance(new Vector2(x, y), new Vector2(23.5f, 23.5f));
                pixels[y * 48 + x] = radius > 22 || radius < 15 ? Color.clear
                    : radius > 20 || radius < 17 ? Color.black : Color.white;
            }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private void OnDisable()
    {
        // Flush once per selection session instead of writing to disk every drag frame.
        if (wheel != null) PlayerPrefs.Save();
        Closed?.Invoke();
    }

    private void OnDestroy()
    {
        if (wheelTexture != null) Destroy(wheelTexture);
        if (valueTexture != null) Destroy(valueTexture);
        if (alphaTexture != null) Destroy(alphaTexture);
        if (cursorTexture != null) Destroy(cursorTexture);
    }
}

public class ColorPickerPointer : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public System.Action<Vector2> Changed;
    public void OnPointerDown(PointerEventData eventData) { Select(eventData); }
    public void OnDrag(PointerEventData eventData) { Select(eventData); }

    private void Select(PointerEventData eventData)
    {
        var rect = (RectTransform)transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position,
            eventData.pressEventCamera, out Vector2 local))
            Changed?.Invoke(new Vector2((local.x - rect.rect.xMin) / rect.rect.width,
                (local.y - rect.rect.yMin) / rect.rect.height));
    }
}
