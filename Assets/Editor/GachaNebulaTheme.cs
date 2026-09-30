using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Recolors the existing hierarchy so scene layout overrides and animation bindings survive.
public static class GachaNebulaTheme
{
    const string PrefabPath = "Assets/TMP_RM/Prefabs/BaseScene.prefab";
    const string ScenePath = "Assets/_Game/Scenes/StartScene.unity";
    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }

    [MenuItem("Tools/Gacha Nebula/Apply Nebula Theme %#&n")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("Open StartScene first.");
        AssetDatabase.Refresh();
        var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ApplyToFlow(contents.GetComponentInChildren<GachaOnboardingController>(true));
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        foreach (var flow in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GachaOnboardingController>(true)))
            ApplyToFlow(flow);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Redesign/Nebula");
        File.WriteAllText("Redesign/Nebula/theme-applied.txt", "Nebula theme applied to BaseScene and StartScene, preserving layout and callbacks.\n" + DateTime.Now);
        Debug.Log("Gacha Nebula: theme saved in BaseScene and StartScene.");
    }

    public static void ApplyToFlow(GachaOnboardingController flow)
    {
        if (flow == null) throw new InvalidOperationException("Missing onboarding flow.");
        foreach (var label in flow.GetComponentsInChildren<TMP_Text>(true))
        {
            bool muted = label.name == "Description" || label.name == "Choice Description" || label.name == "Your Choices" || label.name == "Footer" || label.name == "Eyebrow";
            label.color = Hex(muted ? "BFA8DC" : "F5ECFF");
            if (label.name == "Gacha Nox Logo" || label.name == "Gacha Nebula Logo")
                label.text = "<color=#D5B6FF>GACHA</color> <color=#83E8FA>NEBULA</color>";
            if (label.name == "Eyebrow") label.text = "CREATE YOUR OWN LITTLE UNIVERSE";
            if (label.name == "Footer") label.text = "Your story, among the stars";
            Dirty(label);
        }
        foreach (var image in flow.GetComponentsInChildren<Image>(true))
        {
            switch (image.name)
            {
                case "Shadow": image.color = Hex("0C061B88"); break;
                case "Frame": image.color = Hex("704AB0"); break;
                case "Face": image.color = Hex("281841"); break;
                case "Artwork Well": image.color = Hex("180F2D"); break;
                case "Selected Edge": case "Check Badge": image.color = Hex("78E8F7"); break;
                case "Check": image.color = Hex("241238"); break;
                case "Progress Track": image.color = Hex("493163"); break;
                case "Artwork Dim": image.color = Hex("140C22AD"); break;
                case "Lock Badge": image.color = Hex("64438E"); break;
                case "Padlock": image.color = Color.white; break;
                case "Back": image.color = Hex("3B2558"); break;
                case "Next": image.color = Hex("A26BF6"); break;
                case "Pastel Wash": image.color = new Color(.09f, .035f, .18f, .65f); break;
                default: continue;
            }
            Dirty(image);
        }
        for (int i = 0; i < flow.stepMarkers.Length; i++)
        {
            flow.stepMarkers[i].color = Hex(i == 0 ? "A66FF2" : "412A64");
            flow.stepNumbers[i].color = Hex(i == 0 ? "FFFFFF" : "DDCCFA");
            Dirty(flow.stepMarkers[i]); Dirty(flow.stepNumbers[i]);
        }
        foreach (var button in flow.GetComponentsInChildren<Button>(true))
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .93f, 1f);
            colors.pressedColor = new Color(.76f, .63f, .93f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.62f, .55f, .72f, .85f);
            button.colors = colors;
            Dirty(button);
        }
        foreach (var shadow in flow.GetComponentsInChildren<Shadow>(true))
        { shadow.effectColor = Hex("140823"); Dirty(shadow); }
        string[] names = { "Violet Star", "Nebula DJ", "Nova Bunny" };
        for (int i = 0; i < 3; i++) SetChoiceName(flow.pages[0].choices[i], names[i]);
        SetChoiceName(flow.pages[1].choices[0], "Celestial Chic");
        var caption = flow.pages[1].choices[0].button.transform.Find("Choice Description").GetComponent<TMP_Text>();
        caption.text = "Lilac dreams and silver stars"; Dirty(caption);
        SetChoiceName(flow.pages[3].choices[1], "Gacha Nebula");
        flow.pages[3].choices[1].label = "Gacha Nebula (Dressing)";
        var description = flow.pages[3].panel.transform.Find("Description").GetComponent<TMP_Text>();
        description.text = "Dress up with Gacha Nebula. More games coming soon!"; Dirty(description);
        Dirty(flow);
    }

    static void SetChoiceName(GachaOnboardingController.Choice choice, string name)
    {
        choice.label = name;
        var label = choice.button.transform.Find("Choice Name").GetComponent<TMP_Text>();
        label.text = name;
        Dirty(label);
    }
    static void Dirty(UnityEngine.Object value)
    {
        EditorUtility.SetDirty(value);
        if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }
}
