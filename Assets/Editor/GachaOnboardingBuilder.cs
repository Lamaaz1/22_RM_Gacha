using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GachaOnboardingBuilder
{
    const string Art = "Assets/Scenes/ui/Onboarding/";
    const string PrefabPath = "Assets/TMP_RM/Prefabs/BaseScene.prefab";
    const string StartPath = "Assets/_Game/Scenes/StartScene.unity";
    static readonly Color Ink = new Color32(66, 42, 91, 255);
    static readonly Color Pink = new Color32(236, 116, 185, 255);
    static readonly Color Cyan = new Color32(151, 232, 246, 255);
    static readonly Color Paper = new Color32(251, 246, 255, 255);
    static Sprite rounded, circle;
    static TMP_FontAsset font, brandFont;

    [MenuItem("Tools/Gacha Nox/Update Dressing and Locked Games %#&g")]
    public static void UpdateActivities()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before updating the activity cards.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != StartPath)
            throw new InvalidOperationException("Open StartScene before updating the activity cards.");
        Directory.CreateDirectory("Redesign/Onboarding/backups");
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(PrefabPath, "Redesign/Onboarding/backups/BaseScene-before-dressing-" + stamp + ".prefab");
        File.Copy(StartPath, "Redesign/Onboarding/backups/StartScene-before-dressing-" + stamp + ".unity");
        ImportArt(new[] { "activity-dressing.png", "ui-lock.png" });
        rounded = SpriteAt("ui-rounded.png");
        circle = SpriteAt("ui-circle.png");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF.asset");
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ConfigureActivities(contents.GetComponentInChildren<GachaOnboardingController>(true));
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Redesign/Onboarding/dressing-update.txt", "Dressing is available. Coloring Book and Jigsaw Puzzle are locked.\n" + DateTime.Now);
        Debug.Log("Gacha Nox: Dressing is ready; the other two activities are locked.");
    }

    [MenuItem("Tools/Gacha Nox/Build Choice Panels")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before building the choice panels.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != StartPath)
            throw new InvalidOperationException("Open StartScene before building the choice panels.");
        Directory.CreateDirectory("Redesign/Onboarding/backups");
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Copy(PrefabPath, "Redesign/Onboarding/backups/BaseScene-" + stamp + ".prefab");
        File.Copy(StartPath, "Redesign/Onboarding/backups/StartScene-" + stamp + ".unity");
        ImportArt();
        rounded = SpriteAt("ui-rounded.png");
        circle = SpriteAt("ui-circle.png");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Roboto-Bold SDF.asset");
        brandFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset");
        if (font == null || brandFont == null) throw new InvalidOperationException("Required TMP fonts are missing.");
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform previous = contents.transform.Find("Gacha Onboarding");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            contents.GetComponent<Canvas>().sortingOrder = 10;
            BuildFlow(contents.transform);
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        AssetDatabase.SaveAssets();

        GameObject baseScene = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "BaseScene");
        if (baseScene == null)
            baseScene = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
        var flow = baseScene.GetComponentInChildren<GachaOnboardingController>(true);
        if (flow == null) throw new InvalidOperationException("The saved prefab did not expose the onboarding controller.");
        var startGame = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StartGame>(true)).FirstOrDefault();
        if (startGame == null) throw new InvalidOperationException("StartScene needs its existing StartGame component.");
        while (flow.onCompleted.GetPersistentEventCount() > 0) UnityEventTools.RemovePersistentListener(flow.onCompleted, 0);
        UnityEventTools.AddPersistentListener(flow.onCompleted, startGame.Play);
        PrefabUtility.RecordPrefabInstancePropertyModifications(flow);
        EditorUtility.SetDirty(flow);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = flow.gameObject;
        File.WriteAllText("Redesign/Onboarding/build-result.txt", "Built four panels and 12 choices inside BaseScene. Completion is connected to StartGame.Play.\n" + DateTime.Now);
        Debug.Log("Gacha Nox: four choice panels saved inside BaseScene in StartScene.");
    }

    static void ImportArt(string[] filenames = null)
    {
        AssetDatabase.Refresh();
        foreach (string path in Directory.GetFiles(Art, "*.png"))
        {
            if (filenames != null && !filenames.Contains(Path.GetFileName(path))) continue;
            string asset = path.Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Could not import " + asset);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = asset.EndsWith("ui-rounded.png") || asset.EndsWith("ui-outline.png") ? new Vector4(32, 32, 32, 32) : Vector4.zero;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }

    static void BuildFlow(Transform parent)
    {
        RectTransform root = Full("Gacha Onboarding", parent);
        var flow = root.gameObject.AddComponent<GachaOnboardingController>();
        flow.defaultBackground = SpriteAt("world-dream-galaxy.png");
        flow.background = Full("Backdrop", root).gameObject.AddComponent<Image>();
        flow.background.sprite = flow.defaultBackground;
        flow.background.raycastTarget = true;
        flow.backgroundWash = Full("Pastel Wash", root).gameObject.AddComponent<Image>();
        flow.backgroundWash.color = new Color(0.94f, 0.90f, 1f, 0.64f);
        flow.backgroundWash.raycastTarget = false;
        flow.safeArea = Full("Safe Area", root);
        flow.designSurface = Rect("Design 2560x1440", flow.safeArea, 0, 0, 2560, 1440);
        Transform content = flow.designSurface;
        Label("Eyebrow", content, "MAKE A LITTLE WORLD OF YOUR OWN", 0, 657, 1900, 40, 26, Ink);
        TMP_Text logo = Label("Gacha Nox Logo", content, "<color=#D95CA8>GACHA</color> <color=#668DC9>NOX</color>", 0, 558, 1500, 155, 140, Ink);
        logo.font = brandFont;
        logo.characterSpacing = 3;
        var shadow = logo.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Color.white;
        shadow.effectDistance = new Vector2(4, -5);

        Panel("Progress Track", content, 0, 436, 1390, 7, new Color32(198, 181, 224, 255));
        flow.stepMarkers = new Image[4]; flow.stepNumbers = new TMP_Text[4];
        string[] steps = { "CHARACTER", "STYLE", "WORLD", "ACTIVITY" };
        for (int i = 0; i < 4; i++)
        {
            float x = -675 + i * 450;
            flow.stepMarkers[i] = Panel("Step " + (i + 1), content, x, 436, 58, 58, i == 0 ? Pink : new Color32(201, 189, 227, 255), circle);
            flow.stepNumbers[i] = Label("Number", flow.stepMarkers[i].transform, (i + 1).ToString(), 0, 0, 50, 50, 30, i == 0 ? Color.white : Ink);
            Label("Step Label " + i, content, steps[i], x, 386, 350, 36, 23, Ink);
        }
        flow.progressLabel = Label("Page Count", content, "1 / 4", 1060, 437, 200, 60, 32, Ink);
        flow.pages = new GachaOnboardingController.Page[4];
        string[] titles = { "Choose Your Character", "Choose Your Style", "Choose Your World", "What Do You Want to Play?" };
        string[] descriptions = { "Who will join your Gacha adventure?", "Find the look that feels like you.", "A new story starts with a new place.", "Pick your favorite kind of fun." };
        string[][] labels = {
            new[] { "Lavender Star", "Gacha DJ", "Nox Bunny" },
            new[] { "Cute Pastel", "Dark & Gothic", "Neon Pop" },
            new[] { "Neon City", "Dream Galaxy", "Music Studio" },
            new[] { "Coloring Book", "Jigsaw Puzzle", "Memory Match" }
        };
        string[][] captions = {
            new[] { "A little magic, a lot of sparkle", "Turn up your imagination", "Your sweet little sidekick" },
            new[] { "Soft pinks and dreamy blues", "Moonlight and violet charm", "Bright colors, big energy" },
            new[] { "Explore the lights after dark", "Dream among the stars", "Make your own kind of music" },
            new[] { "Bring your imagination to life", "Put a little world together", "Find the pairs, follow the fun" }
        };
        string[][] ids = {
            new[] { "lavender-star", "gacha-dj", "nox-bunny" },
            new[] { "cute-pastel", "dark-gothic", "neon-pop" },
            new[] { "neon-city", "dream-galaxy", "music-studio" },
            new[] { "coloring", "jigsaw", "memory" }
        };
        string[][] images = {
            new[] { "character-lavender.png", "character-gacha-dj.png", "character-nox-bunny.png" },
            new[] { "style-cute-pastel.png", "style-dark-gothic.png", "style-neon-pop.png" },
            new[] { "world-neon-city.png", "world-dream-galaxy.png", "world-music-studio.png" },
            new[] { "activity-coloring.png", "activity-jigsaw.png", "activity-memory.png" }
        };
        for (int p = 0; p < 4; p++)
        {
            RectTransform page = Full("Panel " + (p + 1) + " - " + steps[p], content);
            Label("Title", page, titles[p], 0, 292, 2200, 90, 64, Ink);
            Label("Description", page, descriptions[p], 0, 226, 2000, 50, 29, new Color32(111, 87, 139, 255));
            var choices = new GachaOnboardingController.Choice[3];
            for (int c = 0; c < 3; c++) choices[c] = Card(page, -720 + c * 720, ids[p][c], labels[p][c], captions[p][c], SpriteAt(images[p][c]));
            flow.pages[p] = new GachaOnboardingController.Page { panel = page.gameObject, choices = choices };
            page.gameObject.SetActive(p == 0);
        }
        flow.selectionLabel = Label("Selection Hint", content, "Pick one card to continue", 0, -475, 1500, 48, 31, Ink);
        flow.summaryLabel = Label("Your Choices", content, "Your character. Your style. Your world.", 0, -525, 1950, 42, 26, Ink);
        flow.backButton = Nav("Back", content, -920, -623, "<  Back", new Color32(235, 224, 247, 255), out _);
        flow.backButton.interactable = false;
        flow.nextButton = Nav("Next", content, 920, -623, "Next  >", Pink, out var nextText);
        flow.nextButton.interactable = false;
        flow.nextLabel = nextText;
        Label("Footer", content, "A world made by you", 0, -630, 1200, 45, 26, Ink);
        flow.worldBackgrounds = new[] { SpriteAt("world-neon-city.png"), SpriteAt("world-dream-galaxy.png"), SpriteAt("world-music-studio.png") };
        ConfigureActivities(flow);
    }

    static void ConfigureActivities(GachaOnboardingController flow)
    {
        if (flow == null || flow.pages.Length != 4 || flow.pages[3].choices.Length != 3)
            throw new InvalidOperationException("Expected three activity cards in the fourth panel.");
        string[] ids = { "coloring", "dressing", "jigsaw" };
        string[] names = { "Coloring Book", "Gacha Nox", "Jigsaw Puzzle" };
        string[] captions = { "Coming soon", "Dressing - Create your look", "Coming soon" };
        string[] sprites = { "activity-coloring.png", "activity-dressing.png", "activity-jigsaw.png" };
        flow.pages[3].panel.transform.Find("Description").GetComponent<TMP_Text>().text = "Dress up with Gacha Nox. More games coming soon!";
        for (int c = 0; c < 3; c++)
        {
            var choice = flow.pages[3].choices[c];
            Transform card = choice.button.transform;
            choice.id = ids[c]; choice.label = c == 1 ? "Gacha Nox (Dressing)" : names[c]; choice.locked = c != 1;
            card.name = names[c];
            card.Find("Choice Name").GetComponent<TMP_Text>().text = names[c];
            card.Find("Choice Description").GetComponent<TMP_Text>().text = captions[c];
            card.Find("Artwork Well/Artwork").GetComponent<Image>().sprite = SpriteAt(sprites[c]);
            choice.button.interactable = !choice.locked;
            choice.selectedVisual.SetActive(false);
            Transform existing = card.Find("Locked");
            if (existing == null)
            {
                RectTransform locked = Full("Locked", card);
                Panel("Artwork Dim", locked, 0, 43, 610, 430, new Color(0.83f, 0.79f, 0.88f, 0.74f));
                var badge = Panel("Lock Badge", locked, 0, 73, 168, 168, new Color32(103, 80, 128, 255), circle);
                var icon = Rect("Padlock", badge.transform, 0, 5, 90, 90).gameObject.AddComponent<Image>();
                icon.sprite = SpriteAt("ui-lock.png"); icon.raycastTarget = false;
                Label("Locked Label", locked, "LOCKED", 0, -43, 400, 60, 34, Ink);
                existing = locked;
            }
            choice.lockedVisual = existing.gameObject;
            choice.lockedVisual.SetActive(choice.locked);
        }
    }

    static GachaOnboardingController.Choice Card(Transform page, float x, string id, string label, string caption, Sprite art)
    {
        RectTransform root = Rect(label, page, x, -126, 650, 590);
        Panel("Shadow", root, 0, -9, 662, 602, new Color(0.22f, 0.12f, 0.34f, 0.15f));
        Panel("Frame", root, 0, 0, 658, 598, new Color32(170, 143, 202, 255));
        var face = Panel("Face", root, 0, 0, 642, 582, Paper);
        face.raycastTarget = true;
        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.93f, 0.98f);
        colors.pressedColor = new Color(0.93f, 0.82f, 0.96f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        var artArea = Panel("Artwork Well", root, 0, 43, 610, 430, new Color32(235, 225, 248, 255));
        artArea.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var image = Rect("Artwork", artArea.transform, 0, 0, 588, 414).gameObject.AddComponent<Image>();
        image.sprite = art; image.preserveAspect = true; image.raycastTarget = false;
        Label("Choice Name", root, label, 0, -200, 604, 65, 45, Ink);
        Label("Choice Description", root, caption, 0, -256, 604, 42, 25, new Color32(121, 99, 145, 255));
        RectTransform selection = Full("Selected", root);
        Panel("Selected Edge", selection, 0, 0, 668, 608, Pink, SpriteAt("ui-outline.png"));
        var badge = Panel("Check Badge", selection, 295, 265, 72, 72, Pink, circle);
        var check = Rect("Check", badge.transform, 0, 0, 44, 44).gameObject.AddComponent<Image>();
        check.sprite = SpriteAt("ui-check.png"); check.raycastTarget = false;
        selection.gameObject.SetActive(false);
        return new GachaOnboardingController.Choice { id = id, label = label, button = button, selectedVisual = selection.gameObject };
    }

    static Button Nav(string name, Transform parent, float x, float y, string text, Color color, out TMP_Text label)
    {
        var image = Panel(name, parent, x, y, 360, 112, color);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var cb = button.colors; cb.disabledColor = new Color(0.72f, 0.65f, 0.79f, 0.62f); cb.highlightedColor = new Color(1f, 0.91f, 0.97f); button.colors = cb;
        label = Label("Label", image.transform, text, 0, 0, 325, 95, 43, Ink);
        return button;
    }

    static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height); rt.anchoredPosition = new Vector2(x, y);
        return rt;
    }
    static RectTransform Full(string name, Transform parent)
    {
        var rt = Rect(name, parent, 0, 0, 0, 0);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }
    static Image Panel(string name, Transform parent, float x, float y, float w, float h, Color color, Sprite sprite = null)
    {
        var image = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
        image.sprite = sprite != null ? sprite : rounded; image.type = sprite == circle ? Image.Type.Simple : Image.Type.Sliced;
        image.color = color; image.raycastTarget = false; return image;
    }
    static TMP_Text Label(string name, Transform parent, string value, float x, float y, float w, float h, float size, Color color)
    {
        var text = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = size; text.color = color;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.enableWordWrapping = false; text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
    static Sprite SpriteAt(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + name);
        if (sprite == null) throw new InvalidOperationException("Missing sprite: " + name);
        return sprite;
    }
}
