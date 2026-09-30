using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class GachaOnboardingValidation
{
    [MenuItem("Tools/Gacha Nox/Validate and Export Panel Previews %#&v")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode first.");
        string directory = "Redesign/Onboarding";
        Directory.CreateDirectory(directory);
        var prior = new Dictionary<string, string>();
        var existed = new HashSet<string>();
        foreach (string key in GachaOnboardingController.PreferenceKeys)
        {
            string full = GachaOnboardingController.PreferencePrefix + key;
            if (PlayerPrefs.HasKey(full)) existed.Add(full);
            prior[full] = PlayerPrefs.GetString(full, "");
        }
        string completeKey = GachaOnboardingController.PreferencePrefix + "Completed";
        bool hadCompleted = PlayerPrefs.HasKey(completeKey);
        int oldCompleted = PlayerPrefs.GetInt(completeKey);
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture rt = null;
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TMP_RM/Prefabs/BaseScene.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            var flow = root.GetComponentInChildren<GachaOnboardingController>(true);
            Require(flow != null && flow.pages.Length == 4, "Four serialized panels exist");
            foreach (Transform child in root.transform) child.gameObject.SetActive(child == flow.transform);
            Require(flow.pages.All(p => p.choices.Length == 3), "Each panel has three choices");
            Require(root.GetComponentsInChildren<MonoBehaviour>(true).All(m => m != null), "No missing scripts");
            int completions = 0;
            flow.onCompleted = new UnityEvent();
            flow.onCompleted.AddListener(() => completions++);
            flow.Begin(false);
            Require(!flow.nextButton.interactable && !flow.backButton.interactable, "Initial navigation is gated");
            flow.nextButton.onClick.Invoke();
            Require(flow.CurrentPage == 0, "Cannot skip an unselected panel");
            flow.pages[0].choices[1].button.onClick.Invoke();
            Require(flow.nextButton.interactable && flow.GetSelection(0) == 1, "Clicking a card selects it");
            flow.nextButton.onClick.Invoke();
            Require(flow.CurrentPage == 1 && !flow.nextButton.interactable, "Next opens the style panel");
            flow.backButton.onClick.Invoke();
            Require(flow.CurrentPage == 0 && flow.GetSelection(0) == 1 && flow.pages[0].choices[1].selectedVisual.activeSelf, "Back preserves the selected card");
            flow.nextButton.onClick.Invoke();
            flow.pages[1].choices[2].button.onClick.Invoke();
            flow.nextButton.onClick.Invoke();
            flow.pages[2].choices[2].button.onClick.Invoke();
            Require(flow.background.sprite == flow.worldBackgrounds[2], "The selected world updates the backdrop");
            flow.nextButton.onClick.Invoke();
            flow.pages[3].choices[1].button.onClick.Invoke();
            Require(flow.nextLabel.text == "Let's Play!", "Last page shows the play action");
            flow.nextButton.onClick.Invoke(); flow.nextButton.onClick.Invoke();
            Require(completions == 1 && flow.IsComplete, "Completion runs once");
            flow.Begin();
            Require(flow.GetSelection(0) == 1 && flow.GetSelection(1) == 2 && flow.GetSelection(2) == 2 && flow.GetSelection(3) == 1, "All four preferences survive reopening");

            var cameraObject = new GameObject("Preview Camera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = 720;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.93f, 0.87f, 0.98f);
            camera.cullingMask = 1 << 5; camera.nearClipPlane = 0.1f; camera.farClipPlane = 100;
            camera.transform.position = new Vector3(0, 0, -10);
            rt = new RenderTexture(1920, 1080, 24); rt.Create(); camera.targetTexture = rt;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            canvas.enabled = true;
            flow.Begin(false);
            for (int p = 0; p < 4; p++)
            {
                flow.pages[p].choices[0].button.onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                flow.FitToSafeArea();
                foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                Require(flow.pages.Count(page => page.panel.activeSelf) == 1, "Only one panel is visible");
                camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = rt;
                var capture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); capture.Apply();
                File.WriteAllBytes(directory + "/panel-" + (p + 1) + ".png", capture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(capture); RenderTexture.active = previous;
                if (p < 3) flow.nextButton.onClick.Invoke();
            }
            File.WriteAllText(directory + "/validation.txt", "PASS: 4 panels, 12 choices, no missing scripts, selection gating, Next/Back, retained selections, world preview, single completion, preference persistence.\nFour previews rendered by Unity at 1920x1080.\n" + DateTime.Now);
            Debug.Log("Gacha Nox: flow checks passed; four Unity panel previews exported.");
        }
        finally
        {
            foreach (var kv in prior)
                if (existed.Contains(kv.Key)) PlayerPrefs.SetString(kv.Key, kv.Value); else PlayerPrefs.DeleteKey(kv.Key);
            if (hadCompleted) PlayerPrefs.SetInt(completeKey, oldCompleted); else PlayerPrefs.DeleteKey(completeKey);
            PlayerPrefs.Save();
            EditorSceneManager.ClosePreviewScene(scene);
            if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Gacha onboarding validation failed: " + message);
    }
}
