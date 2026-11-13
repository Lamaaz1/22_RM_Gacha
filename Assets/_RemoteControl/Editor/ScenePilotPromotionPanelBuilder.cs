using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ScenePilotPromotionPanelBuilder
{
    private const string PrefabFolder = "Assets/_RemoteControl/Prefabs";
    private const string DefaultsFolder = PrefabFolder + "/PromotionDefaults";
    private const string MoreGamesPath = PrefabFolder + "/MoreGamesPanel.prefab";
    private const string PlayOtherPath = PrefabFolder + "/PlayOtherGamePanel.prefab";
    private const string MasterPrefabPath = "Assets/_RemoteControl/ScenePilot/TestJson.prefab";
    private const float DesignWidth = 628f;
    private const float DesignHeight = 350f;

    private static readonly Color[] GameColors =
    {
        new Color32(109, 78, 246, 255),
        new Color32(24, 169, 153, 255),
        new Color32(242, 115, 72, 255),
        new Color32(34, 132, 222, 255),
        new Color32(222, 66, 139, 255),
        new Color32(237, 171, 41, 255)
    };

    static ScenePilotPromotionPanelBuilder()
    {
        EditorApplication.delayCall += AutoBuildWhenReady;
    }

    private static void AutoBuildWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += AutoBuildWhenReady;
            return;
        }

        if (PromotionPanelsAreInstalled())
            return;

        try
        {
            BuildAll();
            Debug.Log("[ScenePilot] Created MoreGamesPanel and PlayOtherGamePanel and added them to TestJson.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static bool PromotionPanelsAreInstalled()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MoreGamesPath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(PlayOtherPath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(MasterPrefabPath) == null)
            return false;

        GameObject root = PrefabUtility.LoadPrefabContents(MasterPrefabPath);
        try
        {
            bool moreGames = false;
            bool playOther = false;
            foreach (RemoteStaticPanel panel in root.GetComponentsInChildren<RemoteStaticPanel>(true))
            {
                if (panel == null)
                    continue;
                moreGames |= string.Equals(panel.panelId, "more_games", StringComparison.OrdinalIgnoreCase);
                playOther |= string.Equals(panel.panelId, "play_other_game", StringComparison.OrdinalIgnoreCase);
            }
            return moreGames && playOther;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("ScenePilot/Rebuild Promotion Panel Prefabs")]
    public static void BuildFromMenu()
    {
        BuildAll();
        EditorUtility.DisplayDialog(
            "ScenePilot",
            "MoreGamesPanel and PlayOtherGamePanel were created and added to TestJson.",
            "OK");
    }

    public static void BuildFromCommandLine()
    {
        try
        {
            BuildAll();
            Debug.Log("[ScenePilot] Promotion panels built successfully.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
            throw;
        }
    }

    private static void BuildAll()
    {
        EnsureFolder("Assets/_RemoteControl", "Prefabs");
        EnsureFolder(PrefabFolder, "PromotionDefaults");

        Sprite background = CreateGradientSprite(
            DefaultsFolder + "/promotion_background.png",
            640,
            360,
            new Color32(19, 18, 42, 255),
            new Color32(52, 31, 100, 255));
        Sprite panel = CreateGradientSprite(
            DefaultsFolder + "/promotion_panel.png",
            640,
            360,
            new Color32(43, 41, 78, 255),
            new Color32(26, 25, 54, 255));
        Sprite title = CreateGradientSprite(
            DefaultsFolder + "/promotion_title.png",
            512,
            128,
            new Color32(126, 91, 255, 255),
            new Color32(75, 55, 190, 255));
        Sprite play = CreateGradientSprite(
            DefaultsFolder + "/promotion_play_button.png",
            512,
            160,
            new Color32(43, 207, 151, 255),
            new Color32(18, 142, 120, 255));
        Sprite featured = CreateGradientSprite(
            DefaultsFolder + "/featured_game.png",
            640,
            384,
            new Color32(89, 61, 222, 255),
            new Color32(24, 161, 175, 255));

        Sprite[] gameSprites = new Sprite[6];
        for (int i = 0; i < gameSprites.Length; i++)
        {
            Color top = GameColors[i];
            Color bottom = Color.Lerp(top, new Color(0.08f, 0.07f, 0.16f, 1f), 0.58f);
            gameSprites[i] = CreateGradientSprite(
                DefaultsFolder + "/more_game_" + (i + 1) + ".png",
                512,
                300,
                top,
                bottom);
        }

        GameObject moreGames = BuildMoreGames(background, panel, title, gameSprites);
        GameObject playOther = BuildPlayOtherGame(background, panel, featured, play);

        GameObject morePrefab = PrefabUtility.SaveAsPrefabAsset(moreGames, MoreGamesPath);
        GameObject playPrefab = PrefabUtility.SaveAsPrefabAsset(playOther, PlayOtherPath);
        UnityEngine.Object.DestroyImmediate(moreGames);
        UnityEngine.Object.DestroyImmediate(playOther);

        if (morePrefab == null || playPrefab == null)
            throw new InvalidOperationException("Unity did not create the promotion panel prefabs.");

        AddPanelsToMasterPrefab(morePrefab, playPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static GameObject BuildMoreGames(Sprite background, Sprite panelSprite, Sprite titleSprite, Sprite[] gameSprites)
    {
        GameObject root = CreateRoot("MoreGamesPanel");
        Image bg = CreateBackground(root.transform, background);
        Image panel = CreateImage("Panel", bg.transform, panelSprite, Color.white);
        SetBox(panel.rectTransform, 48, 28, 532, 294);

        Image title = CreateImage("Title", bg.transform, titleSprite, Color.white);
        SetBox(title.rectTransform, 185, 40, 258, 48);
        TextMeshProUGUI titleLabel = CreateLabel("Fallback Label", title.transform, "MORE GAMES", 34, FontStyles.Bold);
        AddFallbackLabel(title, titleLabel.gameObject);

        List<RectTransform> entrance = new List<RectTransform>();
        List<RemoteStaticPanel.Element> elements = new List<RemoteStaticPanel.Element>
        {
            Binding("bg", bg.gameObject, bg, null, bg.rectTransform, false),
            Binding("panel", panel.gameObject, panel, null, panel.rectTransform, false),
            Binding("title", title.gameObject, title, titleLabel, title.rectTransform, false)
        };

        float[] x = { 76f, 244f, 412f, 76f, 244f, 412f };
        float[] y = { 104f, 104f, 104f, 205f, 205f, 205f };
        for (int i = 0; i < 6; i++)
        {
            Image artwork = CreateImage("Game " + (i + 1), bg.transform, gameSprites[i], Color.white);
            SetBox(artwork.rectTransform, x[i], y[i], 140, 82);
            Button button = artwork.gameObject.AddComponent<Button>();
            button.targetGraphic = artwork;
            button.transition = Selectable.Transition.ColorTint;
            TextMeshProUGUI label = CreateLabel("Fallback Label", artwork.transform, "GAME " + (i + 1), 23, FontStyles.Bold);
            label.color = Color.white;
            AddFallbackLabel(artwork, label.gameObject);
            entrance.Add(artwork.rectTransform);
            elements.Add(Binding("game_" + (i + 1), artwork.gameObject, artwork, label, artwork.rectTransform, true));
        }

        Button close = CreateCloseButton(bg.transform, 543, 39, 28, 28);
        entrance.Add(close.GetComponent<RectTransform>());
        elements.Add(Binding("close_button", close.gameObject, close.targetGraphic as Image,
            close.GetComponentInChildren<TextMeshProUGUI>(true), close.GetComponent<RectTransform>(), false));

        FinishPanel(root, bg.gameObject, "more_games", elements, close, entrance.ToArray());
        return root;
    }

    private static GameObject BuildPlayOtherGame(Sprite background, Sprite panelSprite, Sprite featured, Sprite playSprite)
    {
        GameObject root = CreateRoot("PlayOtherGamePanel");
        Image bg = CreateBackground(root.transform, background);
        Image panel = CreateImage("Panel", bg.transform, panelSprite, Color.white);
        SetBox(panel.rectTransform, 150, 35, 328, 280);

        Image gameImage = CreateImage("Game Image", bg.transform, featured, Color.white);
        SetBox(gameImage.rectTransform, 187, 68, 254, 150);
        TextMeshProUGUI gameLabel = CreateLabel("Fallback Label", gameImage.transform, "FEATURED GAME", 30, FontStyles.Bold);
        AddFallbackLabel(gameImage, gameLabel.gameObject);

        Image playImage = CreateImage("Play Button", bg.transform, playSprite, Color.white);
        SetBox(playImage.rectTransform, 235, 238, 158, 52);
        Button playButton = playImage.gameObject.AddComponent<Button>();
        playButton.targetGraphic = playImage;
        playButton.transition = Selectable.Transition.ColorTint;
        TextMeshProUGUI playLabel = CreateLabel("Fallback Label", playImage.transform, "PLAY", 28, FontStyles.Bold);
        AddFallbackLabel(playImage, playLabel.gameObject);

        Button close = CreateCloseButton(bg.transform, 440, 47, 28, 28);
        List<RemoteStaticPanel.Element> elements = new List<RemoteStaticPanel.Element>
        {
            Binding("bg", bg.gameObject, bg, null, bg.rectTransform, false),
            Binding("panel", panel.gameObject, panel, null, panel.rectTransform, false),
            Binding("game_image", gameImage.gameObject, gameImage, gameLabel, gameImage.rectTransform, false),
            Binding("play_button", playImage.gameObject, playImage, playLabel, playImage.rectTransform, true),
            Binding("close_button", close.gameObject, close.targetGraphic as Image,
                close.GetComponentInChildren<TextMeshProUGUI>(true), close.GetComponent<RectTransform>(), false)
        };

        FinishPanel(root, bg.gameObject, "play_other_game", elements, close,
            new[] { gameImage.rectTransform, playImage.rectTransform, close.GetComponent<RectTransform>() });
        return root;
    }

    private static GameObject CreateRoot(string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        SetUiLayer(root);
        RectTransform rect = root.GetComponent<RectTransform>();
        Stretch(rect);
        return root;
    }

    private static Image CreateBackground(Transform parent, Sprite background)
    {
        Image bg = CreateImage("BG", parent, background, Color.white);
        Stretch(bg.rectTransform);
        bg.raycastTarget = true;
        bg.type = Image.Type.Simple;
        bg.gameObject.AddComponent<CanvasGroup>();
        return bg;
    }

    private static void FinishPanel(
        GameObject root,
        GameObject panelRoot,
        string panelId,
        List<RemoteStaticPanel.Element> elements,
        Button close,
        RectTransform[] entrance)
    {
        CanvasGroup canvasGroup = panelRoot.GetComponent<CanvasGroup>();
        RemoteStaticPanel panel = root.AddComponent<RemoteStaticPanel>();
        panel.panelId = panelId;
        panel.panelTemplateId = panelId;
        panel.useRemoteContent = true;
        panel.deferWhileVisible = false;
        panel.allowRemotePanelActivation = true;
        panel.panelRoot = panelRoot;
        panel.elements = elements;
        panel.nextButton = close;

        // The controller looks for and replays the visual animator on panelRoot.
        // Keep the data/registration component on the stable host, but put all
        // visual and interaction state on BG with the rest of the panel UI.
        RemoteStaticPanelAnimator animator = panelRoot.AddComponent<RemoteStaticPanelAnimator>();
        animator.panelCanvasGroup = canvasGroup;
        animator.panelFadeDuration = 0.32f;
        animator.panelStartScale = 0.97f;
        animator.animateButtons = true;
        animator.animateToggles = false;
        animator.entranceItems = entrance;
    }

    private static RemoteStaticPanel.Element Binding(
        string key,
        GameObject target,
        Image image,
        TextMeshProUGUI text,
        RectTransform rect,
        bool allowUrl)
    {
        return new RemoteStaticPanel.Element
        {
            key = key,
            targetImage = image,
            fallbackSprite = image != null ? image.sprite : null,
            targetObject = target,
            targetText = text,
            targetRect = rect,
            allowLayout = rect != null,
            layoutParent = rect != null ? rect.parent as RectTransform : null,
            allowText = text != null,
            allowImageProperties = image != null,
            allowImage = image != null,
            requireSpriteForPanel = false,
            allowColor = image != null,
            allowEnabled = true,
            allowAdAction = target != null && target.GetComponent<Selectable>() != null,
            allowUrlAction = allowUrl
        };
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        SetUiLayer(go);
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = true;
        image.preserveAspect = false;
        image.type = Image.Type.Simple;
        return image;
    }

    private static Button CreateCloseButton(Transform parent, float x, float y, float width, float height)
    {
        Sprite builtin = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Image image = CreateImage("Close Button", parent, builtin, new Color32(235, 77, 101, 255));
        SetBox(image.rectTransform, x, y, width, height);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        TextMeshProUGUI label = CreateLabel("Fallback Label", image.transform, "×", 27, FontStyles.Bold);
        label.color = Color.white;
        return button;
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, string value, float fontSize, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        SetUiLayer(go);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        Stretch(rect);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static void AddFallbackLabel(Image artwork, GameObject label)
    {
        RemoteImageFallbackLabel fallback = artwork.gameObject.AddComponent<RemoteImageFallbackLabel>();
        fallback.artwork = artwork;
        fallback.fallbackLabel = label;
    }

    private static void AddPanelsToMasterPrefab(GameObject morePrefab, GameObject playPrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MasterPrefabPath);
        try
        {
            RemoteStaticPanel[] panels = root.GetComponentsInChildren<RemoteStaticPanel>(true);
            Transform parent = null;
            foreach (RemoteStaticPanel panel in panels)
            {
                if (panel != null && string.Equals(panel.panelId, "rate_us", StringComparison.OrdinalIgnoreCase))
                {
                    parent = panel.transform.parent;
                    break;
                }
            }
            if (parent == null && panels.Length > 0)
                parent = panels[0].transform.parent;
            if (parent == null)
            {
                Canvas canvas = root.GetComponentInChildren<Canvas>(true);
                parent = canvas != null ? canvas.transform : root.transform;
            }

            RemovePanel(panels, "more_games");
            RemovePanel(panels, "play_other_game");
            AddPanelInstance(morePrefab, parent, "MoreGamesPanel");
            AddPanelInstance(playPrefab, parent, "PlayOtherGamePanel");

            PrefabUtility.SaveAsPrefabAsset(root, MasterPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemovePanel(RemoteStaticPanel[] panels, string panelId)
    {
        foreach (RemoteStaticPanel panel in panels)
            if (panel != null && string.Equals(panel.panelId, panelId, StringComparison.OrdinalIgnoreCase))
                UnityEngine.Object.DestroyImmediate(panel.gameObject);
    }

    private static void AddPanelInstance(GameObject prefab, Transform parent, string name)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null)
            throw new InvalidOperationException("Could not add " + name + " to TestJson.");
        instance.name = name;
        instance.SetActive(false);
    }

    private static Sprite CreateGradientSprite(string assetPath, int width, int height, Color top, Color bottom)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float vertical = height <= 1 ? 0f : (float)y / (height - 1);
            Color row = Color.Lerp(bottom, top, vertical);
            for (int x = 0; x < width; x++)
            {
                float glow = Mathf.Clamp01(1f - Vector2.Distance(
                    new Vector2((float)x / width, vertical),
                    new Vector2(0.18f, 0.82f)) * 1.3f);
                pixels[y * width + x] = Color.Lerp(row, Color.white, glow * 0.08f);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, false);

        string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("Could not import generated sprite: " + assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
            throw new InvalidOperationException("Generated sprite is missing: " + assetPath);
        return sprite;
    }

    private static void SetBox(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(x / DesignWidth, 1f - (y + height) / DesignHeight);
        rect.anchorMax = new Vector2((x + width) / DesignWidth, 1f - y / DesignHeight);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.localPosition = Vector3.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.localPosition = Vector3.zero;
    }

    private static void SetUiLayer(GameObject go)
    {
        int layer = LayerMask.NameToLayer("UI");
        go.layer = layer >= 0 ? layer : 0;
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
