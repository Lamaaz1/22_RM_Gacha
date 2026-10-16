using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using UnityEditor.U2D;

/// <summary>Applies the reusable light UI kit without changing gameplay events.</summary>
public static class GachaLightThemeTools
{
    private const string Kit = "Assets/_Game/UI/LightTheme/";
    private const string GameScene = "Assets/_Game/Scenes/_Game.unity";
    private static readonly Color Ink = new Color(0.208f, 0.157f, 0.310f);
    private static readonly Color Paper = new Color(0.953f, 0.933f, 1f);
    private static readonly HashSet<string> Panels = new HashSet<string>
    {
        "Asset 2", "Asset 13", "Asset 14", "Asset 20", "Asset 21",
        "Asset 26", "Asset 27", "Asset 29", "Asset 30", "Asset 31", "cadre"
    };
    private static readonly HashSet<string> ThemeSprites = new HashSet<string>(Panels)
    {
        "Asset 3", "Asset 4", "Asset 5", "Asset 6", "Asset 7", "Asset 8",
        "Asset 10", "Asset 11", "Asset 12", "Asset 15", "Asset 16",
        "Asset 22", "Asset 23", "Asset 24", "Asset 25", "lock icon",
        "check icon", "x icon", "gacha phone", "gacha phone ui", "screenshot bouton", "gacha club bouton (1)", "Asset 58", "Asset 59"
    };
    private static readonly Dictionary<string, string> ReplacementSprites = new Dictionary<string, string>
    {
        {"Asset 2", "panel"}, {"Asset 13", "panel"}, {"Asset 14", "panel"}, {"Asset 20", "panel"}, {"Asset 21", "panel"},
        {"Asset 3", "category-hair"}, {"Asset 4", "category-props"}, {"Asset 5", "category-pants"},
        {"Asset 6", "category-hat"}, {"Asset 7", "category-clothes"}, {"Asset 8", "category-face"},
        {"Asset 10", "arrow-next-button"}, {"Asset 11", "arrow-previous-button"}, {"Asset 12", "close-button"},
        {"Asset 15", "icon-next"}, {"Asset 16", "icon-previous"},
        {"Asset 22", "nav-studio"}, {"Asset 23", "nav-background"}, {"Asset 24", "nav-view"}, {"Asset 25", "nav-settings"},
        {"Asset 26", "navigation"}, {"Asset 27", "panel"}, {"Asset 29", "character-tile"}, {"Asset 30", "panel"}, {"Asset 31", "navigation"},
        {"cadre", "character-outline"}, {"lock icon", "icon-lock"}, {"check icon", "icon-check"}, {"x icon", "icon-close"},
        {"screenshot bouton", "nav-camera"}, {"gacha phone", "phone-frame"}, {"gacha phone ui", "phone-frame"},
        {"gacha club bouton (1)", "icon-color"}, {"Asset 58", "icon-noads"}, {"Asset 59", "nav-noads"}
    };

    [MenuItem("Tools/Gacha Nox/Apply Light Dress-Up Theme")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before applying the theme.");
        AssetDatabase.Refresh();
        ConfigureImports();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string events = ButtonEvents(root);
                Style(root);
                if (ButtonEvents(root) != events) throw new Exception("Button events changed: " + path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            string events = ButtonEvents(root);
            Style(root);
            if (ButtonEvents(root) != events) throw new Exception("Button events changed: " + root.name);
        }
        var roots = scene.GetRootGameObjects();
        var manager = roots.SelectMany(r => r.GetComponentsInChildren<UiMnager>(true)).FirstOrDefault();
        if (manager != null)
        {
            foreach (var item in manager.BackgroundsP.GetComponentsInChildren<BackgroundBtn>(true))
                if (!manager.BackgrounButtons.Contains(item)) manager.BackgrounButtons.Add(item);
            foreach (var item in manager.BackgroundsPLSCP.GetComponentsInChildren<BackgroundBtn>(true))
                if (!manager.BackgrounButtonsLSCP.Contains(item)) manager.BackgrounButtonsLSCP.Add(item);
        }
        var orientation = roots.SelectMany(r => r.GetComponentsInChildren<OrientationUI>(true)).FirstOrDefault();
        if (manager != null && orientation != null)
        {
            if (manager.downpLSCP != null) LayoutNavigation(manager.downpLSCP.transform, orientation.landscapeLayout.transform);
            if (manager.downp != null) LayoutNavigation(manager.downp.transform, orientation.portraitLayout.transform);
        }
        var choices = roots.SelectMany(r => r.GetComponentsInChildren<PanelChoices>(true));
        var choicesManager = roots.SelectMany(r => r.GetComponentsInChildren<PanelChoicesManager>(true)).FirstOrDefault();
        foreach (var choice in choices)
        {
            if (choice.colorPicker == null) continue;
            var canvas = choice.GetComponentInParent<Canvas>();
            if (canvas == null) continue;
            var sourcePicker = choice.colorPicker.GetComponent<ColorPicker>();
            var legacyColors = sourcePicker != null ? Enumerable.Range(0, sourcePicker.PaletteCount).Select(sourcePicker.ColoratItem).ToList() : new List<Color>();
            bool landscape = choicesManager != null && choice == choicesManager.landscapePanel;
            string modalName = landscape ? "Light Color Picker Landscape" : "Light Color Picker Portrait";
            Transform existing = canvas.rootCanvas.transform.Find(modalName);
            GameObject modal;
            if (existing != null) modal = existing.gameObject;
            else
            {
                choice.colorPicker.gameObject.SetActive(false);
                modal = UnityEngine.Object.Instantiate(choice.colorPicker.gameObject, canvas.rootCanvas.transform, false);
                modal.name = modalName;
            }
            choice.colorPicker = modal.GetComponent<ColorBtn>();
            var compactPicker = modal.GetComponent<ColorPicker>();
            compactPicker.LegacyColors = legacyColors;
            if (compactPicker.Content != null)
            {
                Transform oldGrid = compactPicker.Content;
                while (oldGrid.parent != null && oldGrid.parent != modal.transform) oldGrid = oldGrid.parent;
                if (oldGrid != modal.transform) UnityEngine.Object.DestroyImmediate(oldGrid.gameObject);
                compactPicker.Content = null;
            }
            compactPicker.Colors = new List<ColorBtn>();
            if (manager != null)
            {
                if (landscape) manager.colorPickerLSCP = modal.GetComponent<ColorPicker>();
                else manager.colorPicker = modal.GetComponent<ColorPicker>();
            }
            var pickerRect = modal.GetComponent<RectTransform>();
            pickerRect.anchorMin = new Vector2(.32f, .17f); pickerRect.anchorMax = new Vector2(.70f, .84f);
            pickerRect.offsetMin = pickerRect.offsetMax = Vector2.zero;
            modal.SetActive(false);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        ConfigureAtlas();
        Debug.Log("Light dress-up theme applied. Gameplay button events retained.");
    }

    private static void ConfigureImports()
    {
        const string backgroundsFolder = "Assets/_Game/Art/Backgrounds/EmptyFloor";
        if (AssetDatabase.IsValidFolder(backgroundsFolder))
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { backgroundsFolder }))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false; importer.isReadable = false; importer.maxTextureSize = 2048;
                importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Compressed;
                var platform = importer.GetPlatformTextureSettings("Android"); platform.name = "Android"; platform.overridden = true;
                platform.maxTextureSize = 2048; platform.format = TextureImporterFormat.ASTC_6x6; importer.SetPlatformTextureSettings(platform);
                importer.SaveAndReimport();
            }
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Kit.TrimEnd('/'),
            "Assets/drive-download-20251021T154352Z-1-001", "Assets/drive-download-20251022T085704Z-1-001" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith(Kit) && !ThemeSprites.Contains(Path.GetFileNameWithoutExtension(path))) continue;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            var mobile = importer.GetPlatformTextureSettings("Android");
            mobile.name = "Android"; mobile.overridden = true;
            mobile.maxTextureSize = importer.maxTextureSize;
            mobile.format = TextureImporterFormat.ASTC_6x6;
            mobile.compressionQuality = 80;
            importer.SetPlatformTextureSettings(mobile);
            importer.SaveAndReimport();
        }
    }

    private static void ConfigureAtlas()
    {
        const string atlasPath = Kit + "LightTheme.spriteatlas";
        SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
        if (atlas == null)
        {
            atlas = new SpriteAtlas();
            AssetDatabase.CreateAsset(atlas, atlasPath);
        }
        var packing = atlas.GetPackingSettings();
        packing.padding = 2; packing.enableRotation = false; packing.enableTightPacking = false;
        atlas.SetPackingSettings(packing);
        var texture = atlas.GetTextureSettings();
        texture.generateMipMaps = false; texture.readable = false;
        texture.filterMode = FilterMode.Bilinear;
        atlas.SetTextureSettings(texture);
        var mobile = atlas.GetPlatformSettings("Android");
        mobile.name = "Android"; mobile.overridden = true; mobile.maxTextureSize = 2048;
        mobile.format = TextureImporterFormat.ASTC_6x6;
        atlas.SetPlatformSettings(mobile);
        atlas.SetIncludeInBuild(true);
        atlas.Remove(atlas.GetPackables());
        var images = new List<UnityEngine.Object>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Kit.TrimEnd('/'),
            "Assets/drive-download-20251021T154352Z-1-001", "Assets/drive-download-20251022T085704Z-1-001" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".jpg")) continue;
            if (path.StartsWith(Kit))
                images.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }
        atlas.Add(images.ToArray());
        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();
    }

    private static Sprite Sprite(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(Kit + name + (name == "courtyard-background" ? ".jpg" : ".png"));
    }

    private static void Style(GameObject root)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            string spriteName = image.sprite != null ? image.sprite.name : "";
            bool kitSprite = image.sprite != null && AssetDatabase.GetAssetPath(image.sprite).StartsWith(Kit);
            if (ThemeSprites.Contains(spriteName) || kitSprite)
            {
                if (ReplacementSprites.TryGetValue(spriteName, out string replacement)) image.sprite = Sprite(replacement);
                Color tint = image.color;
                image.color = new Color(1, 1, 1, tint.a);
                if (image.sprite != null) image.type = image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                if ((image.name == "Background" || image.name == "BackGround") &&
                    (spriteName == "Asset 20" || spriteName == "Asset 13" || spriteName == "panel" || spriteName == "courtyard-background"))
                {
                    image.sprite = Sprite("courtyard-background");
                    image.type = Image.Type.Simple;
                    image.color = Color.white;
                    StageTitle(image.transform);
                }
            }
            // Character slots used a black tint on the old frame.
            if (image.name.StartsWith("CharacterButton") && (ThemeSprites.Contains(spriteName) || kitSprite))
                image.color = Color.white;
            if (image.name == "Down" && image.color.a > 0)
            {
                image.sprite = Sprite("navigation");
                image.color = Color.white;
                image.type = Image.Type.Sliced;
            }
            if (image.name == "ColorPicker") image.color = Paper;
            if (image.name == "Lock" && image.color.a > 0)
                image.color = new Color(0.78f, 0.72f, 0.88f, 0.55f);

            string caption = spriteName == "Asset 22" || spriteName == "nav-studio" ? "Studio" : spriteName == "Asset 23" || spriteName == "nav-background" ? "Background" :
                spriteName == "Asset 24" || spriteName == "nav-view" ? "View" : spriteName == "Asset 25" || spriteName == "nav-settings" ? "Settings" :
                spriteName == "screenshot bouton" || spriteName == "nav-camera" ? "Screenshot" : null;
            if (caption != null)
                Label(image.transform, "Light Theme Caption", caption, new Vector2(0.36f, 0.12f), new Vector2(0.90f, 0.88f), 32,
                    spriteName == "Asset 24" || spriteName == "nav-view" ? Color.white : Ink);
        }

        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            if (text.name == "Light Theme Caption") continue;
            Color current = text.color;
            text.color = new Color(Ink.r, Ink.g, Ink.b, current.a);
        }

        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform.name == "Down") LayoutNavigation(transform);
            if (transform.name == "LeftPanel")
            {
                ConfigureCharacters(transform);
            }
            if (transform.name == "RightPanel")
            {
                ConfigureCustomize(transform);
            }
            if (transform.name == "BackGround" && transform.Find("Grid") != null)
                ConfigureBackgrounds(transform);
        }
        foreach (Text music in root.GetComponentsInChildren<Text>(true).Where(t => t.text == "Music").ToArray())
        {
            if (music.transform.parent.GetComponentsInChildren<Text>(true).Any(t => t.text == "Sounds"))
                ConfigureSettings(music.transform.parent, music);
        }
    }

    private static void ConfigureBackgrounds(Transform panel)
    {
        var rect = (RectTransform)panel;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1100, 1000);
        var background = panel.GetComponent<Image>(); background.sprite = Sprite("panel"); background.type = Image.Type.Sliced; background.color = Color.white;
        var oldTitle = panel.Find("Light Theme Stage Title"); if (oldTitle != null) oldTitle.gameObject.SetActive(false);
        var header = panel.Find("Panel");
        var headerRect = (RectTransform)header; headerRect.anchorMin = new Vector2(.03f, .80f); headerRect.anchorMax = new Vector2(.97f, .98f); headerRect.offsetMin = headerRect.offsetMax = Vector2.zero;
        var headerImage = header.GetComponent<Image>(); if (headerImage != null) headerImage.enabled = false;
        foreach (Text old in header.GetComponentsInChildren<Text>(true)) if (!old.name.StartsWith("Light Background")) old.enabled = false;
        TextWidget(header, "Light Background Heading", "Backgrounds", new Vector2(.18f, .39f), new Vector2(.80f, .95f), 48).alignment = TextAnchor.MiddleLeft;
        var hint = TextWidget(header, "Light Background Hint", "Choose your scene", new Vector2(.18f, .04f), new Vector2(.82f, .39f), 25); hint.fontStyle = FontStyle.Normal; hint.alignment = TextAnchor.MiddleLeft;
        Widget(header, "Light Background Icon", new Vector2(.04f, .40f), new Vector2(.15f, .93f), "icon-background");
        var close = header.GetComponentInChildren<Button>(true); var closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = new Vector2(.865f, .45f); closeRect.anchorMax = new Vector2(.98f, .96f); closeRect.offsetMin = closeRect.offsetMax = Vector2.zero;
        var grid = panel.Find("Grid"); var layout = grid.GetComponent<GridLayoutGroup>(); if (layout != null) layout.enabled = false;
        var gridRect = (RectTransform)grid; gridRect.anchorMin = new Vector2(.055f, .17f); gridRect.anchorMax = new Vector2(.945f, .785f); gridRect.offsetMin = gridRect.offsetMax = Vector2.zero;
        var gridImage = grid.GetComponent<Image>(); if (gridImage != null) gridImage.enabled = false;
        var controller = panel.GetComponent<LightBackgroundPanel>(); if (controller == null) controller = panel.gameObject.AddComponent<LightBackgroundPanel>();
        const string scenesFolder = "Assets/_Game/Art/Backgrounds/EmptyFloor";
        var scenePaths = AssetDatabase.IsValidFolder(scenesFolder) ? AssetDatabase.FindAssets("t:Sprite", new[] { scenesFolder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
        for (int i = 0; i < scenePaths.Length; i++)
        {
            string itemName = "Light Scene " + Path.GetFileNameWithoutExtension(scenePaths[i]);
            var existing = grid.Find(itemName);
            var obj = existing != null ? existing.gameObject : new GameObject(itemName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(BackgroundBtn));
            obj.transform.SetParent(grid, false); obj.layer = panel.gameObject.layer;
            obj.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(scenePaths[i]);
            var item = obj.GetComponent<BackgroundBtn>(); item.i = 100 + i; item.btn = obj.GetComponent<Button>();
        }
        var allItems = grid.GetComponentsInChildren<BackgroundBtn>(true);
        if (scenePaths.Length > 0)
            foreach (var legacy in allItems.Where(b => !b.name.StartsWith("Light Scene "))) legacy.gameObject.SetActive(false);
        controller.items = allItems.Where(b => scenePaths.Length == 0 || b.name.StartsWith("Light Scene ")).OrderBy(b => b.i).ToArray();
        controller.cards = new Image[controller.items.Length]; controller.badges = new GameObject[controller.items.Length];
        controller.normalCard = Sprite("button"); controller.selectedCard = Sprite("button-selected");
        for (int i = 0; i < controller.items.Length; i++)
        {
            var item = controller.items[i]; var tile = (RectTransform)item.transform;
            float x = (i % 4) * .255f, top = 1f - ((i % 12) / 4) * .34f;
            tile.anchorMin = new Vector2(x, top - .31f); tile.anchorMax = new Vector2(x + .235f, top); tile.offsetMin = tile.offsetMax = Vector2.zero;
            var source = item.GetComponent<Image>(); source.enabled = false;
            var card = Widget(tile, "Light Background Card", Vector2.zero, Vector2.one, "button");
            card.raycastTarget = true; item.GetComponent<Button>().targetGraphic = card; controller.cards[i] = card;
            var preview = Widget(tile, "Light Background Preview", new Vector2(.055f, .10f), new Vector2(.945f, .90f), null);
            preview.sprite = source.sprite; preview.color = Color.white; preview.type = Image.Type.Simple; preview.preserveAspect = true;
            var badge = Widget(tile, "Light Background Selected", new Vector2(.75f, .71f), new Vector2(.99f, .98f), "selection-dot");
            Widget(badge.transform, "Light Background Check", new Vector2(.18f, .18f), new Vector2(.82f, .82f), "icon-check");
            controller.badges[i] = badge.gameObject;
        }
        var footer = panel.Find("Down"); var footerRect = (RectTransform)footer; footerRect.anchorMin = new Vector2(.07f, .03f); footerRect.anchorMax = new Vector2(.93f, .14f); footerRect.offsetMin = footerRect.offsetMax = Vector2.zero;
        footer.GetComponent<Image>().enabled = false;
        controller.previousButton = footer.Find("Prev").GetComponent<Button>(); controller.nextButton = footer.Find("Next").GetComponent<Button>();
        foreach (var button in new[] { controller.previousButton, controller.nextButton })
        {
            bool next = button == controller.nextButton; var buttonRect = (RectTransform)button.transform;
            buttonRect.anchorMin = new Vector2(next ? .80f : .02f, .12f); buttonRect.anchorMax = new Vector2(next ? .98f : .20f, .88f); buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
            var image = button.GetComponent<Image>(); image.sprite = Sprite("button"); image.color = Color.white; image.type = Image.Type.Sliced;
            Widget(button.transform, "Light Background Arrow", new Vector2(.30f, .20f), new Vector2(.70f, .80f), next ? "mask-next" : "mask-previous").color = Ink;
        }
        foreach (var old in footer.GetComponentsInChildren<Text>(true)) old.enabled = false;
        controller.pageLabel = TextWidget(footer, "Light Background Page", "1 / 1", new Vector2(.32f, .1f), new Vector2(.68f, .9f), 32); controller.pageLabel.enabled = true;
        controller.Initialize();
    }

    private static void ConfigureSettings(Transform panel, Text music)
    {
        var sounds = panel.GetComponentsInChildren<Text>(true).First(t => t.text == "Sounds");
        var close = panel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.onClick.GetPersistentEventCount() > 0);
        var container = panel.parent as RectTransform;
        if (container != null)
        {
            container.anchorMin = container.anchorMax = new Vector2(.5f, .5f);
            container.anchoredPosition = Vector2.zero;
            container.sizeDelta = new Vector2(1050, 850);
        }
        var background = panel.GetComponent<Image>();
        if (background != null) { background.sprite = Sprite("panel"); background.type = Image.Type.Sliced; background.color = Color.white; }
        var header = close != null ? close.transform.parent : null;
        if (header != null && header != panel)
        {
            var rect = (RectTransform)header; rect.anchorMin = new Vector2(.035f, .77f); rect.anchorMax = new Vector2(.965f, .965f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = header.GetComponent<Image>(); if (image != null) image.enabled = false;
            TextWidget(header, "Light Settings Heading", "Settings", new Vector2(.18f, .35f), new Vector2(.78f, .95f), 52).alignment = TextAnchor.MiddleLeft;
            var icon = Widget(header, "Light Settings Icon", new Vector2(.035f, .4f), new Vector2(.155f, .92f), "icon-settings");
            var subtitle = TextWidget(header, "Light Settings Subtitle", "Make yourself comfortable", new Vector2(.18f, .01f), new Vector2(.88f, .39f), 25); subtitle.fontStyle = FontStyle.Normal; subtitle.alignment = TextAnchor.MiddleLeft;
            var closeRect = (RectTransform)close.transform; closeRect.anchorMin = new Vector2(.86f, .43f); closeRect.anchorMax = new Vector2(.97f, .94f); closeRect.offsetMin = closeRect.offsetMax = Vector2.zero;
        }
        ConfigureSettingsRow(music, .47f, "Background music", "♪");
        ConfigureSettingsRow(sounds, .20f, "Clicks & game effects", "♫");
        TextWidget(panel, "Light Settings Footer", "Your preferences are saved automatically", new Vector2(.07f, .05f), new Vector2(.93f, .15f), 24).fontStyle = FontStyle.Normal;
    }

    private static void ConfigureSettingsRow(Text label, float bottom, string description, string symbol)
    {
        var row = label.rectTransform; row.anchorMin = new Vector2(.065f, bottom); row.anchorMax = new Vector2(.935f, bottom + .235f);
        row.offsetMin = row.offsetMax = Vector2.zero;
        label.enabled = false;
        var card = Widget(row, "Light Audio Card", Vector2.zero, Vector2.one, "button");
        card.transform.SetAsFirstSibling();
        TextWidget(row, "Light Audio Symbol", symbol, new Vector2(.035f, .22f), new Vector2(.17f, .82f), 65).color = new Color(.58f, .40f, .84f);
        TextWidget(row, "Light Audio Name", label.text, new Vector2(.21f, .47f), new Vector2(.70f, .86f), 38).alignment = TextAnchor.MiddleLeft;
        var hint = TextWidget(row, "Light Audio Hint", description, new Vector2(.21f, .16f), new Vector2(.72f, .47f), 23); hint.alignment = TextAnchor.MiddleLeft; hint.fontStyle = FontStyle.Normal;
        var button = row.GetComponentsInChildren<Button>(true).First();
        var rect = (RectTransform)button.transform; rect.anchorMin = new Vector2(.75f, .33f); rect.anchorMax = new Vector2(.955f, .67f); rect.offsetMin = rect.offsetMax = Vector2.zero;
        var indicators = button.GetComponentsInChildren<Image>(true).Where(i => i.transform != button.transform && !i.name.StartsWith("Light ")).ToArray();
        var active = indicators.FirstOrDefault(i => i.sprite != null && (i.sprite.name == "icon-check" || i.sprite.name == "check icon"));
        if (active == null) throw new Exception("Settings enabled indicator missing: " + label.text);
        foreach (var image in indicators) image.enabled = false;
        var switchView = button.GetComponent<LightSettingsSwitch>(); if (switchView == null) switchView = button.gameObject.AddComponent<LightSettingsSwitch>();
        switchView.enabledIndicator = active.gameObject; switchView.track = button.GetComponent<Image>();
        switchView.track.enabled = true; switchView.track.color = Color.white; switchView.track.type = Image.Type.Sliced;
        switchView.onSprite = Sprite("button-selected"); switchView.offSprite = Sprite("button");
        switchView.thumb = Widget(button.transform, "Light Switch Thumb", Vector2.zero, Vector2.one, "mask-dot").rectTransform;
        switchView.status = TextWidget(button.transform, "Light Switch Status", "ON", Vector2.zero, Vector2.one, 24);
        switchView.Refresh();
    }

    private static void ConfigureCustomize(Transform parent)
    {
        var grid = parent.GetComponent<GridLayoutGroup>(); if (grid != null) grid.enabled = false;
        var controller = parent.GetComponent<LightCustomizePanel>();
        if (controller == null) controller = parent.gameObject.AddComponent<LightCustomizePanel>();
        foreach (var duplicate in parent.GetComponents<LightCustomizePanel>().Where(c => c != controller).ToArray())
            UnityEngine.Object.DestroyImmediate(duplicate);
        string[] keys = { "face", "clothes", "hair", "hat", "pants", "props" };
        string[] captions = { "Face", "Clothes", "Hair", "Hat", "Pants", "Props" };
        var parts = parent.GetComponentsInChildren<BodyParts>(true);
        controller.categories = new BodyParts[6]; controller.categoryImages = new Image[6];
        controller.categoryIcons = new Image[6]; controller.categoryLabels = new Text[6]; controller.categoryBadges = new GameObject[6];
        for (int i = 0; i < keys.Length; i++)
        {
            string partName = new[] { "FacePart", "ClothsPart", "HairPart", "HatPart", "TrousersPart", "ToysPart" }[i];
            var part = parts.FirstOrDefault(p => p.name == partName);
            if (part == null) part = parts.FirstOrDefault(p => p.GetComponent<Image>() != null && p.GetComponent<Image>().sprite != null && p.GetComponent<Image>().sprite.name == "category-" + keys[i]);
            if (part == null) throw new Exception("Category button missing: " + partName);
            controller.categories[i] = part;
            var rect = (RectTransform)part.transform;
            float x = .085f + (i % 3) * .285f, top = i < 3 ? .845f : .645f;
            rect.anchorMin = new Vector2(x, top - .18f); rect.anchorMax = new Vector2(x + .25f, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
            var image = part.GetComponent<Image>(); image.sprite = Sprite("button"); image.color = Color.white; image.type = Image.Type.Sliced;
            controller.categoryImages[i] = image;
            controller.categoryIcons[i] = Widget(part.transform, "Light Category Icon", new Vector2(.20f, .31f), new Vector2(.80f, .89f), "mask-" + keys[i]);
            controller.categoryLabels[i] = TextWidget(part.transform, "Light Category Label", captions[i], new Vector2(.04f, .04f), new Vector2(.96f, .29f), 22);
            var badge = Widget(part.transform, "Light Category Selection", new Vector2(.83f, .83f), new Vector2(1.04f, 1.04f), "selection-dot");
            badge.maskable = false; controller.categoryBadges[i] = badge.gameObject;
        }
        controller.normalCard = Sprite("button"); controller.selectedCard = Sprite("button-selected");
        Label(parent, "Light Theme Heading", "Customize", new Vector2(.18f, .875f), new Vector2(.82f, .95f), 36, Ink);
        for (int i = 0; i < 2; i++) Widget(parent, "Light Customize Sparkle " + i, new Vector2(i == 0 ? .12f : .82f, .895f), new Vector2(i == 0 ? .18f : .88f, .925f), "mask-sparkle").color = new Color(.73f, .60f, .91f);
        Widget(parent, "Light Wardrobe Divider", new Vector2(.09f, .395f), new Vector2(.91f, .435f), "divider").gameObject.SetActive(false);
        controller.partButton = Widget(parent, "Light Wardrobe Part", new Vector2(.085f, .392f), new Vector2(.40f, .438f), null, true).GetComponent<Button>();
        controller.partLabel = TextWidget(controller.partButton.transform, "Label", "Tops ›", Vector2.zero, Vector2.one, 17);
        controller.partButton.gameObject.SetActive(false);
        var tabs = Widget(parent, "Light Wardrobe Parts", new Vector2(.08f, .397f), new Vector2(.85f, .452f), null);
        controller.partTabs = new Button[4]; controller.partTabLabels = new Text[4]; controller.partTabImages = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var tab = Widget(tabs.transform, "Part Tab " + i, Vector2.zero, Vector2.one, "button", true);
            controller.partTabs[i] = tab.GetComponent<Button>(); controller.partTabImages[i] = tab;
            controller.partTabLabels[i] = TextWidget(tab.transform, "Label", "Part", new Vector2(.03f, .04f), new Vector2(.97f, .96f), 22);
        }
        controller.clearButton = Widget(parent, "Light Clear Part", new Vector2(.865f, .395f), new Vector2(.935f, .435f), "icon-close", true).GetComponent<Button>();
        controller.itemButtons = new Button[3]; controller.itemImages = new Image[3]; controller.itemSelections = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            float x = .085f + i * .285f;
            var card = Widget(parent, "Light Wardrobe Card " + i, new Vector2(x, .205f), new Vector2(x + .25f, .375f), "wardrobe-tile", true);
            controller.itemButtons[i] = card.GetComponent<Button>();
            controller.itemImages[i] = Widget(card.transform, "Outfit", new Vector2(.08f, .08f), new Vector2(.92f, .92f), null);
            controller.itemImages[i].preserveAspect = true;
            var outline = Widget(card.transform, "Selection", Vector2.zero, Vector2.one, "character-outline");
            controller.itemSelections[i] = outline.gameObject;
        }
        controller.previousButton = Widget(parent, "Light Wardrobe Previous", new Vector2(.21f, .145f), new Vector2(.28f, .19f), "mask-previous", true).GetComponent<Button>();
        controller.nextButton = Widget(parent, "Light Wardrobe Next", new Vector2(.72f, .145f), new Vector2(.79f, .19f), "mask-next", true).GetComponent<Button>();
        controller.previousButton.image.color = controller.nextButton.image.color = new Color(.55f, .42f, .91f);
        controller.pageDots = new Image[3];
        for (int i = 0; i < 3; i++) controller.pageDots[i] = Widget(parent, "Light Wardrobe Page " + i, new Vector2(.415f + i * .07f, .157f), new Vector2(.44f + i * .07f, .18f), "mask-dot");
        var color = Widget(parent, "Light Color Button", new Vector2(.085f, .055f), new Vector2(.935f, .135f), "button", true);
        controller.colorButton = color.GetComponent<Button>();
        controller.colorIcon = Widget(color.transform, "Color Wheel", new Vector2(.045f, .15f), new Vector2(.20f, .85f), "icon-color");
        controller.colorLabel = TextWidget(color.transform, "Label", "Color", new Vector2(.24f, .05f), new Vector2(.78f, .95f), 30);
        controller.colorLabel.alignment = TextAnchor.MiddleLeft;
        controller.colorChevron = Widget(color.transform, "Chevron", new Vector2(.86f, .25f), new Vector2(.94f, .75f), "mask-next");
        controller.colorChevron.color = new Color(.55f, .42f, .91f);
        controller.colorWheelIcon = Sprite("icon-color"); controller.backIcon = Sprite("mask-previous");
        controller.colorHost = Widget(parent, "Light Inline Color Host", new Vector2(.085f, .145f), new Vector2(.935f, .855f), "panel").rectTransform;
        controller.colorHost.gameObject.SetActive(false);
        controller.wardrobeElements = parent.Cast<Transform>().Where(child => child.gameObject.activeSelf && (child.name.StartsWith("Light Wardrobe") || child.name == "Light Clear Part")).Select(child => child.gameObject).ToArray();
        controller.PreparePreview();
    }

    private static Image Widget(Transform parent, string name, Vector2 min, Vector2 max, string spriteName, bool button = false)
    {
        Transform existing = parent.Find(name);
        var obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        HideDuplicates(parent, name, obj.transform);
        obj.transform.SetParent(parent, false); obj.layer = parent.gameObject.layer;
        var rect = (RectTransform)obj.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        obj.GetComponent<LayoutElement>().ignoreLayout = true;
        var image = obj.GetComponent<Image>(); image.sprite = spriteName != null ? Sprite(spriteName) : null;
        image.color = spriteName != null ? Color.white : Color.clear;
        image.type = image.sprite != null && image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.preserveAspect = image.type != Image.Type.Sliced;
        image.raycastTarget = button;
        if (button)
        {
            var control = obj.GetComponent<Button>(); if (control == null) control = obj.AddComponent<Button>();
            control.targetGraphic = image;
        }
        return image;
    }

    private static Text TextWidget(Transform parent, string name, string value, Vector2 min, Vector2 max, int size)
    {
        Label(parent, name, value, min, max, size, Ink);
        return parent.Find(name).GetComponent<Text>();
    }

    private static void HideDuplicates(Transform parent, string name, Transform keep)
    {
        foreach (Transform sibling in parent)
            if (sibling.name == name) sibling.gameObject.SetActive(sibling == keep);
    }

    private static void ConfigureCharacters(Transform parent)
    {
        var rect = (RectTransform)parent;
        if (parent.parent != null)
        {
            rect.anchorMin = new Vector2(0.014f, 0.176f);
            rect.anchorMax = new Vector2(0.204f, 0.957f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        var controller = parent.GetComponent<LightCharacterPanel>();
        if (controller == null) controller = parent.gameObject.AddComponent<LightCharacterPanel>();
        controller.panelSprite = Sprite("characters-panel"); controller.normalTile = Sprite("character-tile");
        controller.selectedOutline = Sprite("character-outline"); controller.lockSprite = Sprite("character-lock");
        controller.silhouetteSprite = Sprite("locked-avatar"); controller.selectionBadge = Sprite("character-selection-badge");
        Label(parent, "Light Theme Heading", "Characters", new Vector2(0.18f, 0.91f), new Vector2(0.82f, 0.987f), 31, Ink);
        for (int i = 0; i < 2; i++)
        {
            string name = "Light Header Sparkle " + i;
            Transform existing = parent.Find(name);
            GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            obj.transform.SetParent(parent, false); obj.layer = parent.gameObject.layer;
            var sparkle = obj.GetComponent<Image>(); sparkle.sprite = Sprite("sparkle"); sparkle.color = new Color(.73f, .60f, .91f); sparkle.raycastTarget = false;
            var sparkleRect = (RectTransform)obj.transform;
            sparkleRect.anchorMin = sparkleRect.anchorMax = new Vector2(i == 0 ? .13f : .87f, .95f);
            sparkleRect.sizeDelta = new Vector2(22, 22); sparkleRect.anchoredPosition = Vector2.zero;
            obj.GetComponent<LayoutElement>().ignoreLayout = true;
        }
        controller.Rebuild();
    }

    private static void LayoutNavigation(Transform bar, Transform scope = null)
    {
        string[] names = { "nav-view", "nav-studio", "nav-background", "nav-settings" };
        var images = (scope != null ? scope : bar).GetComponentsInChildren<Image>(true);
        if (scope != null) images = images.Where(image => IsActiveWithin(image.transform, scope)).ToArray();
        var nav = names.Select(name => images.FirstOrDefault(image => image.sprite != null && image.sprite.name == name)).ToArray();
        if (nav.Any(image => image == null)) return;
        var layout = bar.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;
        for (int i = 0; i < nav.Length; i++)
        {
            var button = nav[i].GetComponentInParent<Button>();
            var rect = (RectTransform)(button != null ? button.transform : nav[i].transform);
            rect.SetParent(bar, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0.04f + i * 0.234f, 0.15f);
            rect.anchorMax = new Vector2(0.26f + i * 0.234f, 0.86f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (nav[i].transform != rect)
            {
                var imageRect = nav[i].rectTransform;
                imageRect.anchorMin = Vector2.zero; imageRect.anchorMax = Vector2.one;
                imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
            }
        }
    }

    private static bool IsActiveWithin(Transform item, Transform scope)
    {
        for (Transform current = item; current != null && current != scope; current = current.parent)
            if (!current.gameObject.activeSelf) return false;
        return true;
    }

    private static string ButtonEvents(GameObject root)
    {
        var events = new List<string>();
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                UnityEngine.Object target = button.onClick.GetPersistentTarget(i);
                events.Add(button.name + "|" + (target != null ? target.GetType().FullName + ":" + target.name : "null") + "|" + button.onClick.GetPersistentMethodName(i));
            }
        events.Sort(StringComparer.Ordinal);
        return string.Join("\n", events);
    }

    private static void StageTitle(Transform parent)
    {
        Transform existing = parent.Find("Light Theme Stage Title");
        GameObject obj = existing != null ? existing.gameObject : new GameObject("Light Theme Stage Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.45f, 0.94f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(360, 80);
        obj.GetComponent<LayoutElement>().ignoreLayout = true;
        var image = obj.GetComponent<Image>();
        image.sprite = Sprite("title-plaque"); image.color = Color.white; image.raycastTarget = false;
        Label(obj.transform, "Light Theme Heading", "Dress Up", new Vector2(0.14f, 0.1f), new Vector2(0.86f, 0.9f), 42, Ink);
    }

    private static void Label(Transform parent, string name, string value, Vector2 min, Vector2 max, int size, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
        HideDuplicates(parent, name, obj.transform);
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        obj.GetComponent<LayoutElement>().ignoreLayout = true;
        var text = obj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value; text.fontSize = size; text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = true; text.resizeTextMinSize = 10; text.resizeTextMaxSize = size;
        text.color = color; text.raycastTarget = false;
    }

    // Run against a disposable copy while the user's main project stays open.
    public static void ApplyAndValidate()
    {
        var baselineScene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        foreach (var panel in baselineScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LightCustomizePanel>(true)))
            Debug.Log("CUSTOMIZE_CONTROLLER_COUNT " + panel.name + ": " + panel.GetComponents<LightCustomizePanel>().Length);
        int existingMissing = baselineScene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<MonoBehaviour>(true).Count(m => m == null));
        Apply();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        if (roots.SelectMany(r => r.GetComponentsInChildren<LightCustomizePanel>(true)).Any(c => c.GetComponents<LightCustomizePanel>().Length != 1))
            throw new Exception("Customize panel has duplicate controllers.");
        int missing = roots.Sum(r => r.GetComponentsInChildren<MonoBehaviour>(true).Count(m => m == null));
        if (missing != existingMissing) throw new Exception("The theme introduced a missing script.");
        Debug.Log("Existing missing scripts unchanged: " + existingMissing);
        foreach (var root in roots)
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.type == Image.Type.Sliced && image.sprite != null && image.sprite.border == Vector4.zero)
                    Debug.LogWarning("Existing sliced image without border: " + image.name);
        RenderPreview(roots);
        // Export only reusable assets, without depending on this game's character content.
        AssetDatabase.ExportPackage(Kit.TrimEnd('/'), "Redesign/DressUpLight/GachaDressUpLight.unitypackage", ExportPackageOptions.Recurse);
    }

    private static void RenderPreview(GameObject[] roots)
    {
        var ui = roots.SelectMany(r => r.GetComponentsInChildren<UiMnager>(true)).FirstOrDefault();
        if (ui == null) throw new Exception("Game UI manager not found.");
        var orientation = roots.SelectMany(r => r.GetComponentsInChildren<OrientationUI>(true)).FirstOrDefault();
        if (orientation != null)
        {
            orientation.portraitLayout.SetActive(false);
            orientation.landscapeLayout.SetActive(true);
        }
        ui.LeftPanelLSCP.SetActive(true); ui.RightPanelLSCP.SetActive(true); ui.downpLSCP.SetActive(true);
        ui.SettingsPanelLSCP.SetActive(false); ui.BackgroundsPLSCP.SetActive(false);
        ui.ScreenShotBtnLSCP.SetActive(false); ui.SelectModBtnLSCP.SetActive(false);
        if (ui.colorPickerLSCP != null) ui.colorPickerLSCP.gameObject.SetActive(false);
        var dress = roots.SelectMany(r => r.GetComponentsInChildren<DressUpScript>(true)).FirstOrDefault();
        if (dress != null && dress.PlayerTransform != null)
        {
            DressUpScript.instance = dress;
            dress.ActivePlayerIndex = 0;
            dress.ActivdPlayer = dress.PlayerTransform.transform.GetChild(0).gameObject;
            dress.BtnActivdPlayer = dress.PlatersButtons[0];
            dress.PlayerVdIcon = dress.IconsVdButtons[0];
            dress.LandscapSwitch();
            for (int i = 0; i < dress.PlayerTransform.transform.childCount; i++)
                dress.PlayerTransform.transform.GetChild(i).gameObject.SetActive(i == 0);
            // A preview-only outfit from existing game sprites; saved game data is untouched.
            foreach (Transform part in dress.PlayerTransform.transform.GetChild(0))
            {
                var image = part.GetComponent<Image>();
                if (image == null || part.name == "0" || part.name == "Hand") continue;
                if (part.name == "Caps" || part.name == "Glasses" || part.name == "Jackets" || part.name == "Tail" || part.name == "Toys") continue;
                var sample = Resources.Load<Sprite>("Sprites/" + part.name + "/1");
                if (sample != null) { image.sprite = sample; image.color = Color.white; }
            }
        }
        ValidateCustomize(roots, ui, dress);
        foreach (var panel in ui.LeftPanelLSCP.GetComponentsInChildren<LightCharacterPanel>(true))
        {
            var slots = panel.GetComponentsInChildren<CharacterButtonScrpt>(true).OrderBy(s => s.Index).ToArray();
            for (int i = 0; i < Math.Min(2, slots.Length); i++)
            {
                if (slots[i].Lock != null) slots[i].Lock.SetActive(false);
                foreach (var image in slots[i].GetComponentsInChildren<Image>(true))
                {
                    if (!Enum.TryParse(image.name, out ChildrenParts part)) continue;
                    if (part == ChildrenParts.Caps || part == ChildrenParts.Glasses || part == ChildrenParts.Jackets || part == ChildrenParts.Tail || part == ChildrenParts.Toys) continue;
                    var sample = Resources.Load<Sprite>("Sprites/" + image.name + "/" + (i + 1));
                    if (sample != null) { image.sprite = sample; image.color = Color.white; }
                }
            }
            panel.RefreshSelection();
        }
        var cameraObject = new GameObject("Light Theme Preview Camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 540;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Paper;
        camera.cullingMask = 1 << 5; camera.nearClipPlane = 0.1f; camera.farClipPlane = 100;
        var rt = new RenderTexture(1280, 720, 24); rt.Create(); camera.targetTexture = rt;
        foreach (var canvas in roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas))
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            canvas.enabled = true;
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = rt;
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
        Directory.CreateDirectory("Redesign/DressUpLight");
        File.WriteAllBytes("Redesign/DressUpLight/unity-preview.png", texture.EncodeToPNG());
        var customize = ui.RightPanelLSCP.GetComponent<LightCustomizePanel>();
        customize.categories[2].GetComponent<Button>().onClick.Invoke();
        Canvas.ForceUpdateCanvases(); camera.Render();
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
        File.WriteAllBytes("Redesign/DressUpLight/unity-hair-preview.png", texture.EncodeToPNG());
        customize.PreparePreview();
        customize.categories[0].GetComponent<Button>().onClick.Invoke();
        Canvas.ForceUpdateCanvases(); camera.Render();
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
        File.WriteAllBytes("Redesign/DressUpLight/unity-face-preview.png", texture.EncodeToPNG());
        customize.PreparePreview();
        customize.colorButton.onClick.Invoke();
        Canvas.ForceUpdateCanvases();
        camera.Render();
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
        File.WriteAllBytes("Redesign/DressUpLight/unity-color-preview.png", texture.EncodeToPNG());
        customize.colorButton.onClick.Invoke();
        ui.ShowSettings();
        foreach (var view in ui.SettingsPanelLSCP.GetComponentsInChildren<LightSettingsSwitch>(true))
        {
            bool previous = view.enabledIndicator.activeSelf;
            view.enabledIndicator.SetActive(true); view.Refresh();
            if (view.status.text != "ON" || view.track.sprite != view.onSprite) throw new Exception("Settings ON state failed.");
            view.enabledIndicator.SetActive(false); view.Refresh();
            if (view.status.text != "OFF" || view.track.sprite != view.offSprite) throw new Exception("Settings OFF state failed.");
            view.enabledIndicator.SetActive(previous); view.Refresh();
        }
        Canvas.ForceUpdateCanvases(); camera.Render();
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
        File.WriteAllBytes("Redesign/DressUpLight/unity-settings-preview.png", texture.EncodeToPNG());
        Debug.Log("SETTINGS_STATES_OK");
        ui.ChooseBackGroundP();
        var backgrounds = ui.BackgroundsPLSCP.GetComponent<LightBackgroundPanel>();
        if (backgrounds == null || backgrounds.items.Length == 0) throw new Exception("Background thumbnails missing.");
        backgrounds.Initialize(); backgrounds.ChangePage(-100);
        if (backgrounds.Page != 0 || backgrounds.previousButton.interactable) throw new Exception("Background first page failed.");
        backgrounds.ChangePage(100);
        if (backgrounds.Page != backgrounds.PageCount - 1 || backgrounds.nextButton.interactable) throw new Exception("Background last page failed.");
        backgrounds.ChangePage(-100);
        bool hadBackground = PlayerPrefs.HasKey("Background"); int savedBackground = PlayerPrefs.GetInt("Background");
        var portraitBackground = ui.BackgroundImage.GetComponent<Image>(); var landscapeBackground = ui.BackgroundImageLSCP.GetComponent<Image>();
        var originalPortrait = portraitBackground.sprite; var originalLandscape = landscapeBackground.sprite;
        try
        {
            foreach (var sample in backgrounds.items)
            {
                sample.OnclickM(); backgrounds.Refresh();
                if (PlayerPrefs.GetInt("Background") != sample.i || portraitBackground.sprite != sample.GetComponent<Image>().sprite || landscapeBackground.sprite != sample.GetComponent<Image>().sprite)
                    throw new Exception("Background selection/save failed.");
                if (!backgrounds.badges[Array.IndexOf(backgrounds.items, sample)].activeSelf) throw new Exception("Background selection marker failed.");
                ui.LoadBackground();
                if (landscapeBackground.sprite != sample.GetComponent<Image>().sprite) throw new Exception("Background reload failed: " + sample.i);
            }
        }
        finally
        {
            if (hadBackground) PlayerPrefs.SetInt("Background", savedBackground); else PlayerPrefs.DeleteKey("Background"); PlayerPrefs.Save();
            portraitBackground.sprite = originalPortrait; landscapeBackground.sprite = originalLandscape; backgrounds.Refresh();
        }
        Canvas.ForceUpdateCanvases(); camera.Render();
        texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
        File.WriteAllBytes("Redesign/DressUpLight/unity-backgrounds-preview.png", texture.EncodeToPNG());
        if (backgrounds.PageCount > 1)
        {
            backgrounds.ChangePage(1);
            Canvas.ForceUpdateCanvases(); camera.Render();
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            File.WriteAllBytes("Redesign/DressUpLight/unity-backgrounds-page2-preview.png", texture.EncodeToPNG());
        }
        Debug.Log("BACKGROUNDS_SELECTION_PAGING_OK");
        RenderTexture.active = null; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        Debug.Log("LIGHT_THEME_VALIDATION_OK");
    }

    private static void ValidateCustomize(GameObject[] roots, UiMnager ui, DressUpScript dress)
    {
        var controller = ui.RightPanelLSCP.GetComponent<LightCustomizePanel>();
        if (controller == null || dress == null) throw new Exception("Customize panel controller missing.");
        var manager = roots.SelectMany(r => r.GetComponentsInChildren<PanelChoicesManager>(true)).First();
        var save = roots.SelectMany(r => r.GetComponentsInChildren<SavePanel>(true)).First();
        PanelChoicesManager.Instance = manager; SavePanel.instance = save; UiMnager.instance = ui;
        controller.InitializeForValidation();
        var reachableParts = new HashSet<ChildrenParts>();
        foreach (BodyParts category in controller.categories)
        {
            category.SendMessage("Start");
            category.GetComponent<Button>().onClick.Invoke();
            if (controller.CurrentPart != category.CategoryParts[0]) throw new Exception("Category click targets another controller: " + category.name);
            if (controller.ItemCount == 0) throw new Exception("No items loaded for " + category.name);
            for (int part = 0; part < category.CategoryParts.Count; part++)
            {
                if (!controller.partTabs[part].gameObject.activeSelf) throw new Exception("Hidden part tab: " + category.CategoryParts[part]);
                controller.partTabs[part].onClick.Invoke();
                if (controller.CurrentPart != category.CategoryParts[part] || controller.ItemCount == 0)
                    throw new Exception("Part tab failed: " + category.CategoryParts[part]);
                if (!IsActiveWithin(controller.partTabs[part].transform, controller.transform)) throw new Exception("Visible part row is not the bound row.");
                for (int tab = 0; tab < category.CategoryParts.Count; tab++)
                {
                    string expected = category.CategoryParts[tab] == ChildrenParts.TShirts ? "Tops" : category.CategoryParts[tab] == ChildrenParts.Ear ? "Ears" : category.CategoryParts[tab] == ChildrenParts.HairInFront ? "Front hair" : category.CategoryParts[tab] == ChildrenParts.atTheBack ? "Back hair" : category.CategoryParts[tab].ToString();
                    if (controller.partTabs[tab].GetComponentInChildren<Text>().text != expected) throw new Exception("Visible tab label is stale: " + expected);
                }
                reachableParts.Add(controller.CurrentPart);
            }
            string expectedPath = "Sprites/" + controller.CurrentPart;
            foreach (var preview in controller.itemImages.Where(image => image.enabled))
                if (!AssetDatabase.GetAssetPath(preview.sprite).Contains("/" + expectedPath + "/") && !AssetDatabase.GetAssetPath(preview.sprite).Contains("/" + expectedPath + "Icons/"))
                    throw new Exception("Wrong thumbnail for " + category.name + ": " + AssetDatabase.GetAssetPath(preview.sprite));
            controller.ChangePage(-100);
            if (controller.CurrentPage != 0) throw new Exception("Paging underflow.");
            controller.ChangePage(100);
            if (controller.CurrentPage != (controller.ItemCount - 1) / 3) throw new Exception("Paging overflow.");
        }
        if (reachableParts.Count != Enum.GetValues(typeof(ChildrenParts)).Length) throw new Exception("Some body parts are inaccessible.");
        controller.PreparePreview();
        string key = "Player0" + controller.CurrentPart;
        bool existed = PlayerPrefs.HasKey(key); string prior = PlayerPrefs.GetString(key);
        var image = dress.ActivdPlayer.transform.Find(controller.CurrentPart.ToString()).GetComponent<Image>();
        Sprite original = image.sprite;
        try
        {
            controller.itemButtons[1].onClick.Invoke();
            if (image.sprite == null || PlayerPrefs.GetString(key) != image.sprite.name) throw new Exception("Item selection did not save the chosen sprite.");
            controller.colorButton.onClick.Invoke();
            if (manager.ActivePanel().CharacterSelectedImg != image || dress.ActualPart != controller.CurrentPart) throw new Exception("Color picker targets the wrong body part.");
            if (!manager.ActivePanel().colorPicker.gameObject.activeSelf || manager.ActivePanel().colorPicker.transform.parent != controller.colorHost) throw new Exception("Color picker did not open inside the phone.");
            if (controller.categories.Any(category => category.gameObject.activeSelf) || controller.colorLabel.text != "Back") throw new Exception("The inline color view did not replace the wardrobe.");
            controller.colorButton.onClick.Invoke();
            if (manager.ActivePanel().colorPicker.gameObject.activeSelf || controller.categories.Any(category => !category.gameObject.activeSelf) || controller.colorLabel.text != "Color") throw new Exception("Back did not restore the wardrobe.");
            foreach (string name in new[] { "Light Wardrobe Card 0", "Light Wardrobe Card 1", "Light Wardrobe Card 2", "Light Wardrobe Parts" })
                if (controller.transform.Cast<Transform>().Count(child => child.name == name && child.gameObject.activeSelf) != 1)
                    throw new Exception("Back reactivated duplicate wardrobe elements: " + name);
            controller.clearButton.onClick.Invoke();
            if (image.sprite.name != "0" || controller.colorButton.interactable) throw new Exception("Clearing a part did not disable color editing.");
            controller.partTabs[1].onClick.Invoke();
            if (controller.CurrentPart != ChildrenParts.Jackets) throw new Exception("The secondary clothes part is inaccessible.");
            Debug.Log("CUSTOMIZE_INTERACTIONS_OK: six categories, paging, applying/saving clothing, color target, clear, secondary part.");
        }
        finally
        {
            if (existed) PlayerPrefs.SetString(key, prior); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            dress.DressUp(original, ChildrenParts.TShirts.ToString());
            controller.PreparePreview();
        }
    }
}
