// Assets/Editor/AndroidReleaseExporter.cs
//
// Unity Android Release Manager
// - Unity Android keystore focused.
// - Uses Unity's configured Android keystore first.
// - If Unity has no valid configured keystore, scans Assets/ recursively.
// - Supports .keystore and .jks.
// - Reads aliases with Unity's bundled keytool.
// - Builds signed APK + AAB.
// - Archives APK, AAB, keystore, signing.txt and release-info.txt.
// - Console Account Name is project-scoped.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class AndroidReleaseExporter : EditorWindow
{
    private const string WindowTitle = "Android Release Manager";
    private const string TempFolderName = "_AndroidReleaseTemp";
    private const string DefaultArchiveRootName = "UnityBuilds";

    private string projectRoot;
    private string assetsFolder;
    private string archiveRoot;

    // Signing
    private string keystorePath = "";
    private string storePassword = "";
    private string aliasPassword = "";
    private string selectedAlias = "";

    // Release metadata
    private string consoleAccountName = "";
    private string releaseNotes = "";

    private string gameName = "";
    private string packageName = "";
    private string version = "";
    private int buildNumber;

    private readonly List<string> discoveredKeystores = new List<string>();
    private readonly List<string> aliases = new List<string>();

    private bool autoIncrementBuildNumber = true;
    private bool showProject = true;
    private bool showSigning = true;
    private bool showArchive = true;

    private string status = "Ready.";
    private MessageType statusType = MessageType.Info;

    private Vector2 scroll;

    private GUIStyle headerStyle;
    private GUIStyle cardTitleStyle;
    private GUIStyle mutedStyle;

    private static readonly string[] SupportedKeystoreExtensions =
    {
        ".keystore",
        ".jks"
    };

    [MenuItem("Tools/Android Release Exporter")]
    public static void ShowWindow()
    {
        AndroidReleaseExporter window = GetWindow<AndroidReleaseExporter>();
        window.titleContent = new GUIContent(WindowTitle);
        window.minSize = new Vector2(650, 720);
        window.Show();
    }

    private void OnEnable()
    {
        projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        assetsFolder = Application.dataPath;

        archiveRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            DefaultArchiveRootName);

        LoadProjectSettings();
        RefreshProjectValues();

        // Unity's Android signing configuration is checked first.
        RefreshKeystores();
    }

    private void OnFocus()
    {
        RefreshProjectValues();
        Repaint();
    }

    private void OnInspectorUpdate()
    {
        RefreshProjectValues();
        Repaint();
    }

    // ---------------------------------------------------------------------
    // PROJECT SETTINGS
    // ---------------------------------------------------------------------

    private string GetPrefsPrefix()
    {
        return "AndroidReleaseExporter." +
               Sha256(projectRoot).Substring(0, 16);
    }

    private void LoadProjectSettings()
    {
        string prefix = GetPrefsPrefix();

        consoleAccountName =
            EditorPrefs.GetString(prefix + ".consoleAccountName", "");

        archiveRoot =
            EditorPrefs.GetString(prefix + ".archiveRoot", archiveRoot);

        autoIncrementBuildNumber =
            EditorPrefs.GetBool(prefix + ".autoIncrementBuildNumber", true);

        keystorePath =
            EditorPrefs.GetString(prefix + ".keystorePath", "");

        storePassword =
            EditorPrefs.GetString(prefix + ".storePassword", "");

        aliasPassword =
            EditorPrefs.GetString(prefix + ".aliasPassword", "");

        selectedAlias =
            EditorPrefs.GetString(prefix + ".alias", "");

        // IMPORTANT:
        // Unity itself may already have a keystore configured.
        // Resolve that setting before falling back to our saved preference.
        string unityKeystore = GetUnityConfiguredKeystorePath();

        if (!string.IsNullOrEmpty(unityKeystore) &&
            File.Exists(unityKeystore))
        {
            keystorePath = unityKeystore;
        }
    }

    private void SaveProjectSettings()
    {
        string prefix = GetPrefsPrefix();

        EditorPrefs.SetString(
            prefix + ".consoleAccountName",
            consoleAccountName ?? "");

        EditorPrefs.SetString(
            prefix + ".archiveRoot",
            archiveRoot ?? "");

        EditorPrefs.SetBool(
            prefix + ".autoIncrementBuildNumber",
            autoIncrementBuildNumber);

        EditorPrefs.SetString(
            prefix + ".keystorePath",
            keystorePath ?? "");

        EditorPrefs.SetString(
            prefix + ".storePassword",
            storePassword ?? "");

        EditorPrefs.SetString(
            prefix + ".aliasPassword",
            aliasPassword ?? "");

        EditorPrefs.SetString(
            prefix + ".alias",
            selectedAlias ?? "");
    }

    private void RefreshProjectValues()
    {
        gameName = PlayerSettings.productName ?? "";

        packageName =
            PlayerSettings.GetApplicationIdentifier(
                NamedBuildTarget.Android) ?? "";

        version = PlayerSettings.bundleVersion ?? "";

        buildNumber =
            PlayerSettings.Android.bundleVersionCode;
    }

    // ---------------------------------------------------------------------
    // UI
    // ---------------------------------------------------------------------

    private void EnsureStyles()
    {
        if (headerStyle != null)
            return;

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 22,
            margin = new RectOffset(4, 4, 4, 3)
        };

        cardTitleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            margin = new RectOffset(0, 0, 0, 5)
        };

        mutedStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            wordWrap = true
        };
    }

    private void OnGUI()
    {
        EnsureStyles();

        DrawHeader();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawProjectSection();

        GUILayout.Space(10);

        DrawSigningSection();

        GUILayout.Space(10);

        DrawArchiveSection();

        GUILayout.Space(10);

        DrawBuildSection();

        GUILayout.Space(12);

        DrawStatusSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        GUILayout.Label(
            "Android Release Manager",
            headerStyle);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(
            "Refresh",
            GUILayout.Width(80),
            GUILayout.Height(26)))
        {
            RefreshKeystores();
            RefreshProjectValues();

            SetStatus(
                "Project and Unity keystore information refreshed.",
                MessageType.Info);
        }

        EditorGUILayout.EndHorizontal();

        GUILayout.Label(
            "Unity Android signing • APK + AAB • Release archive",
            mutedStyle);

        EditorGUILayout.EndVertical();
    }

    private void DrawProjectSection()
    {
        showProject = EditorGUILayout.BeginFoldoutHeaderGroup(
            showProject,
            "PROJECT");

        if (showProject)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawReadOnlyField("Game Name", gameName);
            DrawReadOnlyField("Package Name", packageName);
            DrawReadOnlyField("Version", version);
            DrawReadOnlyField(
                "Build Number",
                buildNumber.ToString());

            GUILayout.Space(4);

            if (EditorUserBuildSettings.activeBuildTarget !=
                BuildTarget.Android)
            {
                EditorGUILayout.HelpBox(
                    "Android is not the active Build Target. Switch the project to Android before building.",
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Android Build Target is active.",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawSigningSection()
    {
        showSigning = EditorGUILayout.BeginFoldoutHeaderGroup(
            showSigning,
            "ANDROID SIGNING");

        if (showSigning)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField(
                "Unity Android Keystore",
                cardTitleStyle);

            DrawKeystoreDropArea();

            EditorGUILayout.BeginHorizontal();

            keystorePath =
                EditorGUILayout.TextField(
                    "Keystore",
                    keystorePath);

            if (GUILayout.Button(
                "Browse",
                GUILayout.Width(75)))
            {
                string selected =
                    EditorUtility.OpenFilePanel(
                        "Select Unity Android Keystore",
                        assetsFolder,
                        "keystore,jks");

                if (!string.IsNullOrEmpty(selected))
                    SelectKeystore(selected);
            }

            EditorGUILayout.EndHorizontal();

            if (string.IsNullOrEmpty(keystorePath))
            {
                EditorGUILayout.HelpBox(
                    "No keystore selected. The tool first checks Unity's Android signing configuration, then scans Assets/ for .keystore and .jks files.",
                    MessageType.Warning);
            }
            else if (!File.Exists(keystorePath))
            {
                EditorGUILayout.HelpBox(
                    "The selected keystore does not exist:\n" +
                    keystorePath,
                    MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Keystore found: " +
                    Path.GetFileName(keystorePath),
                    MessageType.Info);
            }

            if (discoveredKeystores.Count > 1)
            {
                GUILayout.Space(3);

                EditorGUILayout.LabelField(
                    "Keystores detected in Assets/",
                    EditorStyles.boldLabel);

                string[] names =
                    new string[discoveredKeystores.Count];

                int currentIndex = 0;

                for (int i = 0;
                     i < discoveredKeystores.Count;
                     i++)
                {
                    names[i] =
                        MakeProjectRelative(
                            discoveredKeystores[i]);

                    if (PathsEqual(
                        discoveredKeystores[i],
                        keystorePath))
                    {
                        currentIndex = i;
                    }
                }

                int newIndex =
                    EditorGUILayout.Popup(
                        currentIndex,
                        names);

                if (newIndex >= 0 &&
                    newIndex < discoveredKeystores.Count &&
                    !PathsEqual(
                        discoveredKeystores[newIndex],
                        keystorePath))
                {
                    SelectKeystore(
                        discoveredKeystores[newIndex]);
                }
            }

            GUILayout.Space(5);

            storePassword =
                EditorGUILayout.PasswordField(
                    "Store Password",
                    storePassword);

            EditorGUILayout.BeginHorizontal();

            if (aliases.Count == 0)
            {
                if (GUILayout.Button(
                    "Read Aliases",
                    GUILayout.Height(26)))
                {
                    ReadAliases();
                }
            }
            else
            {
                if (aliases.Count == 1)
                {
                    selectedAlias = aliases[0];

                    EditorGUILayout.TextField(
                        "Key Alias",
                        selectedAlias);
                }
                else
                {
                    int current =
                        aliases.IndexOf(selectedAlias);

                    if (current < 0)
                        current = 0;

                    int next =
                        EditorGUILayout.Popup(
                            "Key Alias",
                            current,
                            aliases.ToArray());

                    if (next >= 0 &&
                        next < aliases.Count)
                    {
                        selectedAlias = aliases[next];
                    }
                }

                if (GUILayout.Button(
                    "Re-read",
                    GUILayout.Width(65)))
                {
                    ReadAliases();
                }
            }

            EditorGUILayout.EndHorizontal();

            aliasPassword =
                EditorGUILayout.PasswordField(
                    "Key Password",
                    aliasPassword);

            GUILayout.Space(5);

            if (GUILayout.Button(
                "Validate Signing",
                GUILayout.Height(30)))
            {
                ValidateConfiguration(true);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawKeystoreDropArea()
    {
        Rect rect =
            GUILayoutUtility.GetRect(
                GUIContent.none,
                EditorStyles.helpBox,
                GUILayout.Height(52));

        GUI.Box(
            rect,
            "Drop Unity .keystore or .jks here",
            EditorStyles.helpBox);

        Event evt = Event.current;

        if (!rect.Contains(evt.mousePosition))
            return;

        if (evt.type == EventType.DragUpdated ||
            evt.type == EventType.DragPerform)
        {
            bool valid = false;

            foreach (string path in DragAndDrop.paths)
            {
                if (IsSupportedKeystore(path))
                {
                    valid = true;
                    break;
                }
            }

            DragAndDrop.visualMode =
                valid
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;

            if (evt.type == EventType.DragPerform &&
                valid)
            {
                DragAndDrop.AcceptDrag();

                foreach (string path in DragAndDrop.paths)
                {
                    if (IsSupportedKeystore(path))
                    {
                        SelectKeystore(path);
                        break;
                    }
                }

                evt.Use();
            }
        }
    }

    private void DrawArchiveSection()
    {
        showArchive = EditorGUILayout.BeginFoldoutHeaderGroup(
            showArchive,
            "RELEASE ARCHIVE");

        if (showArchive)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // THIS IS THE CONSOLE ACCOUNT NAME FIELD.
            consoleAccountName =
                EditorGUILayout.TextField(
                    "Console Account Name",
                    consoleAccountName);

            EditorGUILayout.HelpBox(
                "This name becomes the first folder under Documents/UnityBuilds and is saved per Unity project.",
                MessageType.None);

            GUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();

            archiveRoot =
                EditorGUILayout.TextField(
                    "Archive Root",
                    archiveRoot);

            if (GUILayout.Button(
                "Browse",
                GUILayout.Width(75)))
            {
                string selected =
                    EditorUtility.OpenFolderPanel(
                        "Select Release Archive Root",
                        archiveRoot,
                        "");

                if (!string.IsNullOrEmpty(selected))
                    archiveRoot = selected;
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            EditorGUILayout.LabelField(
                "Release Notes",
                EditorStyles.boldLabel);

            releaseNotes =
                EditorGUILayout.TextArea(
                    releaseNotes,
                    GUILayout.MinHeight(60));

            autoIncrementBuildNumber =
                EditorGUILayout.ToggleLeft(
                    "Auto-increment Android Build Number before release",
                    autoIncrementBuildNumber);

            GUILayout.Space(6);

            EditorGUILayout.LabelField(
                "Release Folder Preview",
                EditorStyles.boldLabel);

            EditorGUILayout.SelectableLabel(
                GetReleaseFolderPreview(),
                EditorStyles.textField,
                GUILayout.MinHeight(40));

            GUILayout.Space(4);

            EditorGUILayout.LabelField(
                "Files included",
                EditorStyles.boldLabel);

            string keystoreFile =
                string.IsNullOrEmpty(keystorePath)
                    ? "<keystore>.keystore"
                    : Path.GetFileName(keystorePath);

            EditorGUILayout.LabelField(
                "• " + SafeFileName(gameName) + ".apk\n" +
                "• " + SafeFileName(gameName) + ".aab\n" +
                "• " + keystoreFile + "\n" +
                "• signing.txt\n" +
                "• release-info.txt",
                mutedStyle);

            EditorGUILayout.HelpBox(
                "signing.txt contains the Store Password and Key Password in plaintext. Protect the release archive.",
                MessageType.Warning);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawBuildSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField(
            "BUILD RELEASE",
            cardTitleStyle);

        EditorGUILayout.HelpBox(
            "Builds a signed APK and signed AAB using the Unity Android keystore configuration, then archives both.",
            MessageType.Info);

        bool android =
            EditorUserBuildSettings.activeBuildTarget ==
            BuildTarget.Android;

        GUI.enabled = android;

        if (GUILayout.Button(
            "BUILD APK + AAB",
            GUILayout.Height(46)))
        {
            BuildRelease();
        }

        GUI.enabled = true;

        EditorGUILayout.EndVertical();
    }

    private void DrawStatusSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField(
            "STATUS",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            status,
            statusType);

        EditorGUILayout.EndVertical();
    }

    private void DrawReadOnlyField(
        string label,
        string value)
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.PrefixLabel(label);

        EditorGUILayout.SelectableLabel(
            value ?? "",
            EditorStyles.textField,
            GUILayout.Height(
                EditorGUIUtility.singleLineHeight));

        EditorGUILayout.EndHorizontal();
    }

    // ---------------------------------------------------------------------
    // UNITY KEYSTORE DETECTION
    // ---------------------------------------------------------------------

    private string GetUnityConfiguredKeystorePath()
    {
        string configured =
            PlayerSettings.Android.keystoreName;

        if (string.IsNullOrWhiteSpace(configured))
            return "";

        configured = configured.Trim();

        // Unity normally gives us an absolute path.
        if (Path.IsPathRooted(configured))
        {
            try
            {
                string absolute =
                    Path.GetFullPath(configured);

                if (File.Exists(absolute))
                    return absolute;
            }
            catch
            {
                // Continue with project-relative checks.
            }
        }

        // Handle Assets/foo.keystore or project-relative paths.
        string projectPath =
            Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    configured));

        if (File.Exists(projectPath))
            return projectPath;

        // Handle values relative to Assets/.
        string assetsPath =
            Path.GetFullPath(
                Path.Combine(
                    assetsFolder,
                    configured));

        if (File.Exists(assetsPath))
            return assetsPath;

        // Unity can sometimes expose only the filename.
        string fileName =
            Path.GetFileName(configured);

        if (!string.IsNullOrEmpty(fileName) &&
            Directory.Exists(assetsFolder))
        {
            try
            {
                string[] matches =
                    Directory.GetFiles(
                        assetsFolder,
                        fileName,
                        SearchOption.AllDirectories);

                if (matches.Length == 1)
                    return Path.GetFullPath(matches[0]);
            }
            catch
            {
                // Ignore and allow Assets scan below.
            }
        }

        return "";
    }

    private void RefreshKeystores()
    {
        discoveredKeystores.Clear();

        // 1. UNITY CONFIGURED KEYSTORE HAS PRIORITY.
        string unityKeystore =
            GetUnityConfiguredKeystorePath();

        if (!string.IsNullOrEmpty(unityKeystore) &&
            File.Exists(unityKeystore))
        {
            keystorePath = unityKeystore;
        }

        // 2. SCAN ASSETS IF UNITY DOES NOT HAVE A VALID KEYSTORE.
        if (Directory.Exists(assetsFolder))
        {
            try
            {
                foreach (string file in Directory.GetFiles(
                    assetsFolder,
                    "*",
                    SearchOption.AllDirectories))
                {
                    if (!IsSupportedKeystore(file))
                        continue;

                    bool duplicate = false;

                    foreach (string existing in discoveredKeystores)
                    {
                        if (PathsEqual(existing, file))
                        {
                            duplicate = true;
                            break;
                        }
                    }

                    if (!duplicate)
                        discoveredKeystores.Add(
                            Path.GetFullPath(file));
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    "Assets keystore scan failed: " +
                    ex.Message);
            }
        }

        discoveredKeystores.Sort(
            StringComparer.OrdinalIgnoreCase);

        // 3. FALLBACK RULE:
        //    Exactly one Assets keystore -> use it.
        //    Multiple -> DO NOT blindly choose one.
        if (string.IsNullOrEmpty(unityKeystore) ||
            !File.Exists(unityKeystore))
        {
            if (File.Exists(keystorePath))
            {
                // Keep the project-scoped selection.
            }
            else if (discoveredKeystores.Count == 1)
            {
                keystorePath =
                    discoveredKeystores[0];
            }
            else if (discoveredKeystores.Count > 1)
            {
                keystorePath = "";
            }
        }

        // 4. READ ALIASES IF POSSIBLE.
        if (!string.IsNullOrEmpty(keystorePath) &&
            File.Exists(keystorePath) &&
            !string.IsNullOrEmpty(storePassword))
        {
            ReadAliases();
        }

        SaveProjectSettings();
        Repaint();
    }

    private void SelectKeystore(string path)
    {
        if (!IsSupportedKeystore(path))
        {
            SetStatus(
                "Only Unity .keystore and .jks files are supported.",
                MessageType.Error);
            return;
        }

        keystorePath =
            Path.GetFullPath(path);

        aliases.Clear();
        selectedAlias = "";

        SaveProjectSettings();

        // Alias reading needs the Store Password.
        if (!string.IsNullOrEmpty(storePassword))
            ReadAliases();
        else
            SetStatus(
                "Keystore selected. Enter the Store Password, then read aliases.",
                MessageType.Info);
    }

    private void ReadAliases()
    {
        aliases.Clear();

        if (string.IsNullOrEmpty(keystorePath) ||
            !File.Exists(keystorePath))
        {
            SetStatus(
                "Select a valid Unity Android keystore first.",
                MessageType.Error);
            return;
        }

        if (string.IsNullOrEmpty(storePassword))
        {
            SetStatus(
                "Enter the Store Password first.",
                MessageType.Warning);
            return;
        }

        string keytool = FindUnityKeytool();

        if (string.IsNullOrEmpty(keytool))
        {
            SetStatus(
                "Unity's bundled keytool could not be found.",
                MessageType.Error);
            return;
        }

        string[] storeTypes =
        {
            "JKS",
            "PKCS12"
        };

        string lastOutput = "";

        foreach (string storeType in storeTypes)
        {
            string output;
            int exitCode;

            RunKeytool(
                keytool,
                "-list -v " +
                "-keystore " + Quote(keystorePath) + " " +
                "-storepass " + Quote(storePassword) + " " +
                "-storetype " + storeType,
                out output,
                out exitCode);

            lastOutput = output;

            if (exitCode != 0)
                continue;

            ExtractAliases(output);

            if (aliases.Count > 0)
                break;
        }

        if (aliases.Count == 0)
        {
            SetStatus(
                "Unity keystore could not be opened with the supplied Store Password.\n\n" +
                lastOutput,
                MessageType.Error);
            return;
        }

        aliases.Sort(
            StringComparer.OrdinalIgnoreCase);

        if (!aliases.Contains(selectedAlias))
        {
            selectedAlias =
                aliases.Count == 1
                    ? aliases[0]
                    : "";
        }

        SaveProjectSettings();

        if (aliases.Count == 1)
        {
            SetStatus(
                "Unity keystore verified. Alias selected: " +
                aliases[0],
                MessageType.Info);
        }
        else
        {
            SetStatus(
                "Unity keystore verified. " +
                aliases.Count +
                " aliases found. Select the signing alias.",
                MessageType.Info);
        }
    }

    private void ExtractAliases(string output)
    {
        using (StringReader reader =
               new StringReader(output))
        {
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();

                if (!trimmed.StartsWith(
                    "Alias name:",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string alias =
                    trimmed.Substring(
                        "Alias name:".Length).Trim();

                if (string.IsNullOrEmpty(alias))
                    continue;

                bool exists = false;

                foreach (string existing in aliases)
                {
                    if (string.Equals(
                        existing,
                        alias,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    aliases.Add(alias);
            }
        }
    }

    private bool IsSupportedKeystore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string extension =
            Path.GetExtension(path);

        foreach (string supported in
                 SupportedKeystoreExtensions)
        {
            if (string.Equals(
                extension,
                supported,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------------
    // VALIDATION
    // ---------------------------------------------------------------------

    private bool ValidateConfiguration(bool showDialog)
    {
        RefreshProjectValues();

        List<string> errors =
            new List<string>();

        if (EditorUserBuildSettings.activeBuildTarget !=
            BuildTarget.Android)
        {
            errors.Add(
                "Active Build Target must be Android.");
        }

        if (string.IsNullOrWhiteSpace(gameName))
            errors.Add("Game Name is empty.");

        if (string.IsNullOrWhiteSpace(packageName))
            errors.Add("Package Name is empty.");

        if (string.IsNullOrWhiteSpace(version))
            errors.Add("Version is empty.");

        if (buildNumber <= 0)
            errors.Add(
                "Android Build Number must be greater than zero.");

        if (string.IsNullOrWhiteSpace(consoleAccountName))
            errors.Add(
                "Console Account Name is required.");

        if (string.IsNullOrWhiteSpace(archiveRoot))
            errors.Add(
                "Archive Root is required.");

        if (string.IsNullOrWhiteSpace(keystorePath))
        {
            errors.Add(
                "No Unity Android keystore is selected.");
        }
        else if (!File.Exists(keystorePath))
        {
            errors.Add(
                "Selected Unity Android keystore does not exist.");
        }

        if (string.IsNullOrWhiteSpace(storePassword))
            errors.Add("Store Password is required.");

        if (string.IsNullOrWhiteSpace(selectedAlias))
            errors.Add("Key Alias is required.");

        if (aliases.Count == 0 &&
            errors.Count == 0)
        {
            ReadAliases();
        }

        if (aliases.Count > 0 &&
            !ContainsAlias(selectedAlias))
        {
            errors.Add(
                "The selected alias does not exist in the keystore.");
        }

        if (string.IsNullOrWhiteSpace(aliasPassword))
            errors.Add("Key Password is required.");

        if (errors.Count > 0)
        {
            string message =
                string.Join(
                    "\n• ",
                    errors.ToArray());

            SetStatus(
                message,
                MessageType.Error);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Release Configuration Invalid",
                    "Fix the following:\n\n• " +
                    message,
                    "OK");
            }

            return false;
        }

        if (!ValidatePrivateKeyPassword())
        {
            SetStatus(
                "The keystore and alias were found, but the Key Password could not be validated.",
                MessageType.Error);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Signing Validation Failed",
                    "Check:\n\n" +
                    "• Store Password\n" +
                    "• Key Alias\n" +
                    "• Key Password",
                    "OK");
            }

            return false;
        }

        SaveProjectSettings();

        SetStatus(
            "Unity Android signing configuration is valid.",
            MessageType.Info);

        return true;
    }

    private bool ContainsAlias(string alias)
    {
        foreach (string item in aliases)
        {
            if (string.Equals(
                item,
                alias,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool ValidatePrivateKeyPassword()
    {
        string keytool =
            FindUnityKeytool();

        if (string.IsNullOrEmpty(keytool))
            return false;

        string temp =
            Path.Combine(
                Path.GetTempPath(),
                "UnityAndroidRelease_" +
                Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(temp);

            string certificate =
                Path.Combine(
                    temp,
                    "certificate.cer");

            string args =
                "-exportcert " +
                "-rfc " +
                "-alias " + Quote(selectedAlias) + " " +
                "-keystore " + Quote(keystorePath) + " " +
                "-storepass " + Quote(storePassword) + " " +
                "-keypass " + Quote(aliasPassword) + " " +
                "-file " + Quote(certificate);

            string output;
            int exitCode;

            RunKeytool(
                keytool,
                args,
                out output,
                out exitCode);

            return exitCode == 0 &&
                   File.Exists(certificate);
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(temp))
                    Directory.Delete(temp, true);
            }
            catch
            {
                // Ignore cleanup errors.
            }
        }
    }

    // ---------------------------------------------------------------------
    // BUILD
    // ---------------------------------------------------------------------

    private void BuildRelease()
    {
        SaveProjectSettings();

        if (!ValidateConfiguration(true))
            return;

        List<string> scenes =
            new List<string>();

        foreach (EditorBuildSettingsScene scene
                 in EditorBuildSettings.scenes)
        {
            if (scene.enabled &&
                !string.IsNullOrEmpty(scene.path))
            {
                scenes.Add(scene.path);
            }
        }

        if (scenes.Count == 0)
        {
            SetStatus(
                "No enabled scenes are configured.",
                MessageType.Error);

            EditorUtility.DisplayDialog(
                "Build Failed",
                "Add at least one enabled scene in the Android Build Settings.",
                "OK");

            return;
        }

        string originalKeystoreName =
            PlayerSettings.Android.keystoreName;

        string originalStorePassword =
            PlayerSettings.Android.keystorePass;

        string originalAlias =
            PlayerSettings.Android.keyaliasName;

        string originalAliasPassword =
            PlayerSettings.Android.keyaliasPass;

        bool originalUseCustomKeystore =
            PlayerSettings.Android.useCustomKeystore;

        bool originalBuildAppBundle =
            EditorUserBuildSettings.buildAppBundle;

        int originalBuildNumber =
            PlayerSettings.Android.bundleVersionCode;

        string tempFolder =
            Path.Combine(
                projectRoot,
                "Builds",
                TempFolderName);

        try
        {
            if (autoIncrementBuildNumber)
            {
                PlayerSettings.Android.bundleVersionCode =
                    Math.Max(
                        1,
                        PlayerSettings.Android.bundleVersionCode + 1);

                buildNumber =
                    PlayerSettings.Android.bundleVersionCode;
            }

            ConfigureUnitySigning();

            if (Directory.Exists(tempFolder))
                Directory.Delete(
                    tempFolder,
                    true);

            Directory.CreateDirectory(tempFolder);

            string apkPath =
                Path.Combine(
                    tempFolder,
                    SafeFileName(gameName) +
                    ".apk");

            string aabPath =
                Path.Combine(
                    tempFolder,
                    SafeFileName(gameName) +
                    ".aab");

            // -------------------------------------------------------------
            // APK
            // -------------------------------------------------------------

            SetStatus(
                "Building signed APK...",
                MessageType.Info);

            EditorUserBuildSettings.buildAppBundle =
                false;

            BuildReport apkReport =
                BuildPipeline.BuildPlayer(
                    new BuildPlayerOptions
                    {
                        scenes = scenes.ToArray(),
                        locationPathName = apkPath,
                        target = BuildTarget.Android,
                        options = BuildOptions.None
                    });

            EnsureBuildSucceeded(
                apkReport,
                "APK");

            // -------------------------------------------------------------
            // AAB
            // -------------------------------------------------------------

            SetStatus(
                "APK succeeded. Building signed AAB...",
                MessageType.Info);

            EditorUserBuildSettings.buildAppBundle =
                true;

            BuildReport aabReport =
                BuildPipeline.BuildPlayer(
                    new BuildPlayerOptions
                    {
                        scenes = scenes.ToArray(),
                        locationPathName = aabPath,
                        target = BuildTarget.Android,
                        options = BuildOptions.None
                    });

            EnsureBuildSucceeded(
                aabReport,
                "AAB");

            // -------------------------------------------------------------
            // ARCHIVE
            // -------------------------------------------------------------

            string releaseFolder =
                GetReleaseFolder();

            if (Directory.Exists(releaseFolder))
            {
                // Intentionally replace the exact same release folder.
                Directory.Delete(
                    releaseFolder,
                    true);
            }

            Directory.CreateDirectory(
                releaseFolder);

            string archiveApk =
                Path.Combine(
                    releaseFolder,
                    Path.GetFileName(apkPath));

            string archiveAab =
                Path.Combine(
                    releaseFolder,
                    Path.GetFileName(aabPath));

            File.Copy(
                apkPath,
                archiveApk,
                true);

            File.Copy(
                aabPath,
                archiveAab,
                true);

            string archivedKeystore =
                Path.Combine(
                    releaseFolder,
                    Path.GetFileName(keystorePath));

            File.Copy(
                keystorePath,
                archivedKeystore,
                true);

            File.WriteAllText(
                Path.Combine(
                    releaseFolder,
                    "signing.txt"),
                BuildSigningText());

            File.WriteAllText(
                Path.Combine(
                    releaseFolder,
                    "release-info.txt"),
                BuildReleaseInfo(
                    archiveApk,
                    archiveAab));

            SaveProjectSettings();

            SetStatus(
                "Release completed successfully.\n\n" +
                releaseFolder,
                MessageType.Info);

            try
            {
                EditorUtility.RevealInFinder(
                    releaseFolder);
            }
            catch
            {
                // Ignore OS explorer errors.
            }

            EditorUtility.DisplayDialog(
                "Release Complete",
                "Signed APK + AAB created successfully.\n\n" +
                releaseFolder,
                "OK");
        }
        catch (Exception ex)
        {
            SetStatus(
                "Build failed:\n" + ex.Message,
                MessageType.Error);

            UnityEngine.Debug.LogException(ex);

            EditorUtility.DisplayDialog(
                "Release Failed",
                ex.Message,
                "OK");
        }
        finally
        {
            // Restore Unity's original signing configuration.
            PlayerSettings.Android.useCustomKeystore =
                originalUseCustomKeystore;

            PlayerSettings.Android.keystoreName =
                originalKeystoreName;

            PlayerSettings.Android.keystorePass =
                originalStorePassword;

            PlayerSettings.Android.keyaliasName =
                originalAlias;

            PlayerSettings.Android.keyaliasPass =
                originalAliasPassword;

            EditorUserBuildSettings.buildAppBundle =
                originalBuildAppBundle;

            // When auto-increment is disabled, keep the original build number.
            if (!autoIncrementBuildNumber)
            {
                PlayerSettings.Android.bundleVersionCode =
                    originalBuildNumber;
            }

            if (Directory.Exists(tempFolder))
            {
                try
                {
                    Directory.Delete(
                        tempFolder,
                        true);
                }
                catch (Exception cleanupEx)
                {
                    UnityEngine.Debug.LogWarning(
                        "Could not delete temporary build folder: " +
                        cleanupEx.Message);
                }
            }

            AssetDatabase.Refresh();
        }
    }

    private void ConfigureUnitySigning()
    {
        PlayerSettings.Android.useCustomKeystore = true;

        PlayerSettings.Android.keystoreName =
            Path.GetFullPath(keystorePath);

        PlayerSettings.Android.keystorePass =
            storePassword;

        PlayerSettings.Android.keyaliasName =
            selectedAlias;

        PlayerSettings.Android.keyaliasPass =
            aliasPassword;
    }

    private void EnsureBuildSucceeded(
        BuildReport report,
        string label)
    {
        if (report == null)
        {
            throw new Exception(
                label +
                " build returned no BuildReport.");
        }

        if (report.summary.result !=
            BuildResult.Succeeded)
        {
            throw new Exception(
                label +
                " build failed.\n" +
                "Result: " +
                report.summary.result +
                "\nErrors: " +
                report.summary.totalErrors +
                "\nWarnings: " +
                report.summary.totalWarnings);
        }
    }

    // ---------------------------------------------------------------------
    // ARCHIVE
    // ---------------------------------------------------------------------

    private string GetReleaseFolder()
    {
        return Path.Combine(
            archiveRoot,
            SafeFileName(consoleAccountName),
            SafeFileName(gameName),
            SafeFileName(
                version + "_" + buildNumber));
    }

    private string GetReleaseFolderPreview()
    {
        // Do NOT use Path.Combine here.
        // This prevents invalid user-entered path characters from breaking
        // IMGUI before validation.
        string root =
            string.IsNullOrWhiteSpace(archiveRoot)
                ? "<Archive Root>"
                : archiveRoot.Trim();

        string account =
            string.IsNullOrWhiteSpace(consoleAccountName)
                ? "<Console Account Name>"
                : SafeFileName(consoleAccountName);

        string game =
            string.IsNullOrWhiteSpace(gameName)
                ? "<Game Name>"
                : SafeFileName(gameName);

        string release =
            SafeFileName(
                (string.IsNullOrWhiteSpace(version)
                    ? "<Version>"
                    : version) +
                "_" +
                (buildNumber <= 0
                    ? "<Build Number>"
                    : buildNumber.ToString()));

        root = root.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        return root +
               Path.DirectorySeparatorChar +
               account +
               Path.DirectorySeparatorChar +
               game +
               Path.DirectorySeparatorChar +
               release;
    }

    private string BuildSigningText()
    {
        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine(
            "UNITY ANDROID SIGNING INFORMATION");

        sb.AppendLine(
            "================================");

        sb.AppendLine();

        sb.AppendLine(
            "Console Account Name: " +
            consoleAccountName);

        sb.AppendLine(
            "Game Name: " +
            gameName);

        sb.AppendLine(
            "Package Name: " +
            packageName);

        sb.AppendLine(
            "Version: " +
            version);

        sb.AppendLine(
            "Build Number: " +
            buildNumber);

        sb.AppendLine(
            "Keystore: " +
            Path.GetFileName(keystorePath));

        sb.AppendLine(
            "Store Password: " +
            storePassword);

        sb.AppendLine(
            "Key Alias: " +
            selectedAlias);

        sb.AppendLine(
            "Key Password: " +
            aliasPassword);

        sb.AppendLine();

        sb.AppendLine(
            "WARNING: This file contains sensitive signing credentials.");

        sb.AppendLine(
            "Do not commit it to source control or share it publicly.");

        return sb.ToString();
    }

    private string BuildReleaseInfo(
        string apkPath,
        string aabPath)
    {
        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine(
            "UNITY ANDROID RELEASE");

        sb.AppendLine(
            "=====================");

        sb.AppendLine();

        sb.AppendLine(
            "Console Account Name: " +
            consoleAccountName);

        sb.AppendLine(
            "Game Name: " +
            gameName);

        sb.AppendLine(
            "Package Name: " +
            packageName);

        sb.AppendLine(
            "Version: " +
            version);

        sb.AppendLine(
            "Build Number: " +
            buildNumber);

        sb.AppendLine(
            "Created: " +
            DateTime.Now.ToString(
                "yyyy-MM-dd HH:mm:ss"));

        sb.AppendLine(
            "Unity Version: " +
            Application.unityVersion);

        sb.AppendLine();

        sb.AppendLine(
            "APK: " +
            Path.GetFileName(apkPath));

        sb.AppendLine(
            "APK SHA-256: " +
            Sha256File(apkPath));

        sb.AppendLine();

        sb.AppendLine(
            "AAB: " +
            Path.GetFileName(aabPath));

        sb.AppendLine(
            "AAB SHA-256: " +
            Sha256File(aabPath));

        sb.AppendLine();

        sb.AppendLine(
            "Release Notes:");

        sb.AppendLine(
            releaseNotes ?? "");

        return sb.ToString();
    }

    // ---------------------------------------------------------------------
    // KEYTOOL
    // ---------------------------------------------------------------------

    private string FindUnityKeytool()
    {
        string editorPath =
            EditorApplication.applicationPath;

        if (string.IsNullOrEmpty(editorPath))
            return "";

        string editorDirectory =
            Path.GetDirectoryName(editorPath);

        if (string.IsNullOrEmpty(editorDirectory))
            return "";

#if UNITY_EDITOR_WIN
        string[] candidates =
        {
            Path.Combine(
                editorDirectory,
                "Data",
                "PlaybackEngines",
                "AndroidPlayer",
                "OpenJDK",
                "bin",
                "keytool.exe"),

            Path.Combine(
                editorDirectory,
                "Data",
                "PlaybackEngines",
                "AndroidPlayer",
                "OpenJDK",
                "bin",
                "keytool")
        };
#else
        string[] candidates =
        {
            Path.Combine(
                editorDirectory,
                "Contents",
                "PlaybackEngines",
                "AndroidPlayer",
                "OpenJDK",
                "bin",
                "keytool"),

            Path.Combine(
                editorDirectory,
                "Contents",
                "PlaybackEngines",
                "AndroidPlayer",
                "OpenJDK",
                "bin",
                "keytool.exe")
        };
#endif

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return "";
    }

    private bool RunKeytool(
        string executable,
        string arguments,
        out string output,
        out int exitCode)
    {
        output = "";
        exitCode = -1;

        try
        {
            ProcessStartInfo psi =
                new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

            using (Process process =
                   new Process())
            {
                process.StartInfo = psi;

                if (!process.Start())
                {
                    output =
                        "Could not start Unity's keytool.";

                    return false;
                }

                string stdout =
                    process.StandardOutput.ReadToEnd();

                string stderr =
                    process.StandardError.ReadToEnd();

                process.WaitForExit();

                exitCode =
                    process.ExitCode;

                output =
                    stdout +
                    Environment.NewLine +
                    stderr;

                return true;
            }
        }
        catch (Exception ex)
        {
            output = ex.ToString();
            return false;
        }
    }

    // ---------------------------------------------------------------------
    // PATH / TEXT HELPERS
    // ---------------------------------------------------------------------

    private static bool PathsEqual(
        string a,
        string b)
    {
        if (string.IsNullOrEmpty(a) ||
            string.IsNullOrEmpty(b))
        {
            return false;
        }

        try
        {
            string fullA =
                Path.GetFullPath(a).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            string fullB =
                Path.GetFullPath(b).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            return string.Equals(
                fullA,
                fullB,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(
                a,
                b,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private string MakeProjectRelative(
        string path)
    {
        try
        {
            string root =
                projectRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;

            Uri rootUri =
                new Uri(root);

            Uri pathUri =
                new Uri(
                    Path.GetFullPath(path));

            return Uri.UnescapeDataString(
                rootUri.MakeRelativeUri(
                    pathUri)
                .ToString()
                .Replace(
                    '/',
                    Path.DirectorySeparatorChar));
        }
        catch
        {
            return path;
        }
    }

    private static string SafeFileName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "_";

        StringBuilder sb =
            new StringBuilder(
                value.Trim());

        foreach (char invalid
                 in Path.GetInvalidFileNameChars())
        {
            sb.Replace(
                invalid,
                '_');
        }

        string result =
            sb.ToString()
              .Trim()
              .TrimEnd('.');

        if (string.IsNullOrEmpty(result))
            result = "_";

        string upper =
            result.ToUpperInvariant();

        string[] reserved =
        {
            "CON",
            "PRN",
            "AUX",
            "NUL",
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9",
            "LPT1",
            "LPT2",
            "LPT3",
            "LPT4",
            "LPT5",
            "LPT6",
            "LPT7",
            "LPT8",
            "LPT9"
        };

        foreach (string reservedName
                 in reserved)
        {
            if (upper == reservedName)
            {
                result = "_" + result;
                break;
            }
        }

        return result;
    }

    private static string Quote(
        string value)
    {
        if (value == null)
            return "\"\"";

        return "\"" +
               value.Replace(
                   "\"",
                   "\\\"") +
               "\"";
    }

    private static string Sha256(
        string value)
    {
        using (SHA256 sha =
               SHA256.Create())
        {
            byte[] bytes =
                Encoding.UTF8.GetBytes(
                    value ?? "");

            byte[] hash =
                sha.ComputeHash(bytes);

            StringBuilder sb =
                new StringBuilder();

            foreach (byte b in hash)
                sb.Append(
                    b.ToString("x2"));

            return sb.ToString();
        }
    }

    private static string Sha256File(
        string path)
    {
        using (SHA256 sha =
               SHA256.Create())
        using (FileStream stream =
               File.OpenRead(path))
        {
            byte[] hash =
                sha.ComputeHash(stream);

            StringBuilder sb =
                new StringBuilder();

            foreach (byte b in hash)
                sb.Append(
                    b.ToString("x2"));

            return sb.ToString();
        }
    }

    private void SetStatus(
        string message,
        MessageType type)
    {
        status = message;
        statusType = type;
        Repaint();
    }
}
