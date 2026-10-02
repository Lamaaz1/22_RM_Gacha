#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class GameScreenshotTool : EditorWindow
{
    [SerializeField] private string outputFolder = "";
    [SerializeField] private string filenamePrefix = "Screenshot";
    [SerializeField] private int scaleIndex;

    private static readonly string[] ScaleLabels = { "1×", "2×", "4×" };
    private static readonly int[] Scales = { 1, 2, 4 };

    private string status = "";
    private MessageType statusType = MessageType.Info;

    [MenuItem("Tools/Game Screenshot")]
    private static void Open()
    {
        var window = GetWindow<GameScreenshotTool>("Game Screenshot");
        window.minSize = new Vector2(400, 280);
    }

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            outputFolder = Path.GetFullPath(
                Path.Combine(Application.dataPath, "../Screenshots"));
        }
    }

    private void OnGUI()
    {
        GUILayout.Space(12);
        GUILayout.Label("Game Screenshot", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Captures the Game view, including UI. Set the desired " +
            "resolution in the Game view before capturing.",
            MessageType.Info);

        GUILayout.Space(8);
        filenamePrefix = EditorGUILayout.TextField(
            "Filename prefix", filenamePrefix);

        scaleIndex = EditorGUILayout.Popup(
            "Resolution multiplier", Mathf.Clamp(scaleIndex, 0, 2),
            ScaleLabels);

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Output folder", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            outputFolder = EditorGUILayout.TextField(outputFolder);

            if (GUILayout.Button("Browse", GUILayout.Width(65)))
            {
                string selected = EditorUtility.OpenFolderPanel(
                    "Screenshot folder", outputFolder, "");

                if (!string.IsNullOrEmpty(selected))
                    outputFolder = selected;
            }
        }

        GUILayout.Space(12);

        bool canCapture = EditorApplication.isPlaying &&
                          !EditorApplication.isPaused &&
                          !EditorApplication.isCompiling;

        if (!canCapture)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode and unpause to capture.",
                MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(!canCapture))
        {
            if (GUILayout.Button("Capture PNG", GUILayout.Height(36)))
                Capture();
        }

        using (new EditorGUI.DisabledScope(
                   !Directory.Exists(outputFolder)))
        {
            if (GUILayout.Button("Open Output Folder"))
                EditorUtility.RevealInFinder(Path.GetFullPath(outputFolder));
        }

        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, statusType);
    }

    private void Capture()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
                throw new ArgumentException("Choose an output folder.");

            string folder = Path.GetFullPath(outputFolder);
            Directory.CreateDirectory(folder);

            string prefix = filenamePrefix.Trim();

            foreach (char invalid in Path.GetInvalidFileNameChars())
                prefix = prefix.Replace(invalid, '_');

            prefix = prefix.TrimEnd('.', ' ');

            if (string.IsNullOrWhiteSpace(prefix))
                prefix = "Screenshot";

            // Leading text also avoids Windows reserved filenames.
            string filename =
                $"Capture_{prefix}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_" +
                $"{Guid.NewGuid().ToString("N").Substring(0, 8)}.png";

            string path = Path.Combine(folder, filename);
            int multiplier = Scales[Mathf.Clamp(scaleIndex, 0, 2)];

            ScreenCapture.CaptureScreenshot(path, multiplier);

            status = "Capture requested. Keep the Game view visible " +
                     "and allow a moment for the PNG to finish saving.\n" + path;
            statusType = MessageType.Info;
        }
        catch (Exception exception)
        {
            status = "Capture failed: " + exception.Message;
            statusType = MessageType.Error;
            Debug.LogException(exception);
        }
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }
}
#endif