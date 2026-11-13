using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// This is part of the SAME controller, not a second component or downloader.
public partial class RemoteContentController
{
    [Header("STATIC PANEL BINDINGS")]
    public List<RemoteStaticPanel> staticPanels = new List<RemoteStaticPanel>();

    [Tooltip("Automatically finds RemoteStaticPanel components in loaded scenes, including inactive objects.")]
    public bool autoDiscoverStaticPanels = true;

    [Tooltip("Logs static panel registration, matching and application steps.")]
    public bool debugStaticPanels = true;

    [Header("STATIC PANEL OWNERSHIP")]

    [Tooltip("If another script or Animator replaces a successfully applied remote static sprite, restore the remote sprite in LateUpdate.")]
    public bool lockRemoteStaticSprites = true;

    [Tooltip("When a RemoteStaticPanel appears later (for example from an instantiated prefab), register it and immediately apply the current cached/active config.")]
    public bool applyActiveConfigToLatePanels = true;

    [Tooltip("Called when a manual or automatic sequence step has no later enabled panel in the loaded JSON order.")]
    public UnityEvent onStaticSequenceFinished = new UnityEvent();

    [Serializable]
    public class RemoteStaticPanelData
    {
        public string id;
        // Missing in legacy JSON defaults to true and preserves the scene state
        // unless the matching RemoteStaticPanel explicitly opts into activation.
        public bool enabled = true;
        // Normal sequence loading panels only. Missing legacy values use 5 seconds.
        public float durationSeconds = 5f;
        // Optional platform destinations used only by the rate_us panel.
        public RemoteStoreUrlsData storeUrls;
        public RemoteStaticElementData[] elements;
    }
    [Serializable]
    public class RemoteStoreUrlsData
    {
        public string android;
        public string ios;
    }
    [Serializable]
    public class RemoteStaticElementData
    {
        public string key;
        public bool overrideEnabled;
        public bool enabled = true;
        public bool overrideColor;
        public string color;
        // null means preserve existing image. key must match the enclosing key.
        public RemoteImageData image;
        public StaticLayoutData layout;
        public StaticTextData text;
        public bool? preserveAspect;
        public bool? raycastTarget;
        // Optional: none, interstitial or rewarded. Missing preserves legacy behavior (none).
        public string adAction;
        // Optional HTTPS destination for supported game-promotion buttons.
        public string url;
    }

    private bool staticStartupComplete;
    private bool staticSequenceStarted;
    private bool staticNavigationReady;
    private bool staticSequenceFinished;

    public bool IsStaticPanelSequenceFinished => staticSequenceFinished;
    private readonly HashSet<string> staticWarnings = new HashSet<string>();
    private readonly Dictionary<RemoteStaticPanel.Element, Sprite> staticOriginalSprites = new Dictionary<RemoteStaticPanel.Element, Sprite>();
    private readonly Dictionary<RemoteStaticPanel, RemoteStaticPanelData> deferredStaticPanels = new Dictionary<RemoteStaticPanel, RemoteStaticPanelData>();
    private readonly HashSet<RemoteStaticPanel.Element> staticOwnedImages = new HashSet<RemoteStaticPanel.Element>();

    private static string StaticTemplateId(string id)
    {
        if (string.IsNullOrEmpty(id)) return id;
        if (Regex.IsMatch(id, "^card_select_[0-9]+$", RegexOptions.IgnoreCase)) return "card_selection";
        if (Regex.IsMatch(id, "^level_select_[0-9]+$", RegexOptions.IgnoreCase)) return "level_selection";
        if (Regex.IsMatch(id, "^loading_[0-9]+$", RegexOptions.IgnoreCase)) return "loading";
        if (Regex.IsMatch(id, "^download_loading_[0-9]+$", RegexOptions.IgnoreCase)) return "download_loading";
        if (Regex.IsMatch(id, "^main_menu_[0-9]+$", RegexOptions.IgnoreCase)) return "main_menu";
        if (Regex.IsMatch(id, "^rate_us_[0-9]+$", RegexOptions.IgnoreCase)) return "rate_us";
        return id;
    }

    private static bool IsRepeatableStaticId(string id)
    {
        return Regex.IsMatch(id ?? string.Empty, "^(loading|download_loading|card_select|level_select|main_menu|rate_us)_[0-9]+$", RegexOptions.IgnoreCase);
    }

    public static bool IsStaticSequencePanelId(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return !string.Equals(id, "download_loading", StringComparison.OrdinalIgnoreCase) &&
               !Regex.IsMatch(id, "^download_loading_[0-9]+$", RegexOptions.IgnoreCase);
    }

    private string PanelTemplateId(RemoteStaticPanel panel)
    {
        return !string.IsNullOrEmpty(panel.panelTemplateId)
            ? panel.panelTemplateId
            : StaticTemplateId(panel.panelId);
    }

    private void StaticWarning(string message)
    {
        if (staticWarnings.Add(message)) Debug.LogWarning("[RemoteContent/Static] " + message, this);
    }

    private void StaticLog(string message)
    {
        if (debugStaticPanels) Debug.Log("[RemoteContent/Static] " + message, this);
    }

    // Keeps static bindings reliable without requiring a second JSON or a
    // manually maintained controller list. Existing inspector references are
    // preserved and missing scene panels are appended.
    private void AutoRegisterStaticPanels()
    {
        if (!autoDiscoverStaticPanels) return;

        if (staticPanels == null)
            staticPanels = new List<RemoteStaticPanel>();

        staticPanels.RemoveAll(panel => panel == null);

#if UNITY_2023_1_OR_NEWER
        RemoteStaticPanel[] found = UnityEngine.Object.FindObjectsByType<RemoteStaticPanel>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
#else
        RemoteStaticPanel[] found = UnityEngine.Object.FindObjectsOfType<RemoteStaticPanel>(true);
#endif

        int added = 0;

        foreach (RemoteStaticPanel panel in found)
        {
            if (panel == null || !panel.useRemoteContent)
                continue;

            if (staticPanels.Contains(panel))
                continue;

            staticPanels.Add(panel);
            InitializeStaticPanel(panel);
            added++;
        }

        if (added > 0 || debugStaticPanels)
        {
            StaticLog(
                "Registered " +
                staticPanels.Count +
                " static panel instance(s); added " +
                added +
                "."
            );
        }
    }

    // Called directly by RemoteStaticPanel.OnEnable().
    // This handles panels instantiated after this controller's Awake/Start.
    public void RegisterStaticPanel(RemoteStaticPanel panel)
    {
        if (panel == null || !panel.useRemoteContent)
            return;

        if (staticPanels == null)
            staticPanels = new List<RemoteStaticPanel>();

        staticPanels.RemoveAll(item => item == null);

        bool added = false;

        if (!staticPanels.Contains(panel))
        {
            staticPanels.Add(panel);
            added = true;
        }

        InitializeStaticPanel(panel);

        if (added)
        {
            StaticLog(
                "Self-registered static panel instance '" +
                panel.panelId +
                "'."
            );
        }

        // Late remote instances must not expose scene defaults during offline
        // startup, a skipped flow, or when the active config does not include them.
        if (IsStaticSequencePanelId(panel.panelId) &&
            (waitingForInternet || staticSequenceFinished ||
             (staticStartupComplete && StaticData(activeConfig, panel.panelId) == null)))
            SetStaticPanelVisible(panel, false);

        // A late-created panel should not wait for the next network refresh.
        // Apply the already validated active config immediately.
        if (
            applyActiveConfigToLatePanels &&
            staticStartupComplete &&
            activeConfig != null &&
            activeConfig.enabled
        )
        {
            RemoteStaticPanelData data =
                StaticData(
                    activeConfig,
                    panel.panelId
                );

            if (data != null)
            {
                if (DeferStatic(panel))
                {
                    deferredStaticPanels[panel] = data;
                }
                else
                {
                    ApplyStaticPanel(
                        panel,
                        data
                    );
                    if (IsStaticSequencePanelId(panel.panelId) && !IsRemoteStaticPanelEligible(panel, data))
                        SetStaticPanelVisible(panel, false);
                }
            }
        }
    }

    public void UnregisterStaticPanel(RemoteStaticPanel panel)
    {
        if (panel == null)
            return;

        deferredStaticPanels.Remove(panel);

        if (staticPanels != null)
            staticPanels.Remove(panel);

        if (panel.elements != null)
        {
            foreach (RemoteStaticPanel.Element element in panel.elements)
            {
                if (element == null)
                    continue;

                staticOwnedImages.Remove(element);
                staticOriginalSprites.Remove(element);

                // The object is being removed/destroyed, so only release the
                // generated runtime objects. Do not try to force a scene sprite.
                DestroyElementRuntimeImage(element);
            }
        }
    }

    private static bool StaticIdValid(string id)
    {
        return !string.IsNullOrEmpty(id) &&
               Regex.IsMatch(
                   id,
                   "^[A-Za-z][A-Za-z0-9_]{0,63}$"
               );
    }

    private void InitializeStaticBindings()
    {
        foreach (RemoteStaticPanel panel in ValidStaticPanels())
        {
            InitializeStaticPanel(panel);
        }
    }

    private void InitializeStaticPanel(RemoteStaticPanel panel)
    {
        if (panel == null)
            return;

        foreach (RemoteStaticPanel.Element element in ValidStaticElements(panel))
        {
            RememberStaticSprite(element);
        }
    }

    private void RememberStaticSprite(RemoteStaticPanel.Element e)
    {
        if (e == null || e.targetImage == null)
            return;

        Sprite current = e.targetImage.sprite;
        Sprite remembered;

        // Never save null as the original. If the local owner assigns a
        // sprite later, capture it as long as it is not our runtime sprite.
        if (
            current == null ||
            current == e.runtimeSprite ||
            (
                staticOriginalSprites.TryGetValue(e, out remembered) &&
                remembered != null
            )
        )
        {
            return;
        }

        staticOriginalSprites[e] = current;
    }

    private RemoteStaticPanel FindStaticPanelInstance(string id)
    {
        if (staticPanels == null) return null;
        foreach (RemoteStaticPanel panel in staticPanels)
            if (panel != null && string.Equals(panel.panelId, id, StringComparison.OrdinalIgnoreCase))
                return panel;
        return null;
    }

    private RemoteStaticPanel FindStaticTemplate(string templateId)
    {
        if (staticPanels == null) return null;

        // Prefer the base panel (for example "loading") when it exists.
        foreach (RemoteStaticPanel panel in staticPanels)
            if (panel != null && !panel.runtimeGenerated &&
                string.Equals(panel.panelId, templateId, StringComparison.OrdinalIgnoreCase))
                return panel;

        foreach (RemoteStaticPanel panel in staticPanels)
            if (panel != null && !panel.runtimeGenerated &&
                string.Equals(StaticTemplateId(panel.panelId), templateId, StringComparison.OrdinalIgnoreCase))
                return panel;
        return null;
    }

    // Reuse authored panels first. Only a server ID with no authored match is
    // cloned from its authored template, which supports deliberate website
    // duplication without creating a second copy of an existing Unity panel.
    private void EnsureStaticPanelInstances(RemoteConfigData config)
    {
        AutoRegisterStaticPanels();
        if (config == null || config.staticPanels == null) return;

        var configuredIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (RemoteStaticPanelData data in config.staticPanels)
            if (data != null && !string.IsNullOrEmpty(data.id)) configuredIds.Add(data.id);

        if (staticPanels != null)
        {
            foreach (RemoteStaticPanel stale in new List<RemoteStaticPanel>(staticPanels))
            {
                if (stale == null || !stale.runtimeGenerated || configuredIds.Contains(stale.panelId)) continue;
                staticPanels.Remove(stale);
                Destroy(stale.gameObject);
            }
        }

        foreach (RemoteStaticPanelData data in config.staticPanels)
        {
            if (data == null || !IsRepeatableStaticId(data.id) || FindStaticPanelInstance(data.id) != null)
                continue;

            string templateId = StaticTemplateId(data.id);
            RemoteStaticPanel source = FindStaticTemplate(templateId);
            if (source == null)
            {
                StaticWarning(
                    "No authored Unity static panel has ID '" + data.id +
                    "' and no authored template exists for '" + templateId + "'."
                );
                continue;
            }

            // Prevent OnEnable from running with the template ID or capturing a transient entrance state.
            var staging = new GameObject("Inactive ScenePilot Clone");
            staging.SetActive(false);
            GameObject cloneObject = Instantiate(source.gameObject, staging.transform, false);
            cloneObject.SetActive(false);
            cloneObject.transform.SetParent(source.transform.parent, false);
            Destroy(staging);
            cloneObject.name = data.id;
            RemoteStaticPanel clone = cloneObject.GetComponent<RemoteStaticPanel>();
            if (clone == null)
            {
                Destroy(cloneObject);
                StaticWarning(data.id + ": cloned template has no RemoteStaticPanel component.");
                continue;
            }

            clone.panelId = data.id;
            clone.panelTemplateId = templateId;
            clone.runtimeGenerated = true;
            if (clone.panelRoot != null) clone.panelRoot.SetActive(false);
            if (!staticPanels.Contains(clone)) staticPanels.Add(clone);
            InitializeStaticPanel(clone);
            clone.BindNextButton();
            StaticLog("Created missing runtime panel '" + data.id + "' from template '" + templateId + "'.");
        }
    }

    private IEnumerable<RemoteStaticPanel> ValidStaticPanels()
    {
        if (staticPanels == null)
            yield break;

        // IMPORTANT:
        // Duplicate scene panel IDs are intentionally allowed.
        // Multiple instances can consume the same one JSON staticPanels entry.
        // JSON IDs themselves are still required to be unique by
        // ValidateStaticConfig().
        foreach (RemoteStaticPanel panel in staticPanels)
        {
            if (panel == null || !panel.useRemoteContent)
                continue;

            if (!StaticIdValid(panel.panelId))
            {
                StaticWarning(
                    "Empty or invalid panel ID: " +
                    panel.panelId
                );

                continue;
            }

            yield return panel;
        }
    }

    private IEnumerable<RemoteStaticPanel.Element> ValidStaticElements(RemoteStaticPanel panel)
    {
        if (panel.elements == null) yield break;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in panel.elements)
        {
            if (e == null) continue;
            if (!StaticIdValid(e.key) || !keys.Add(e.key)) { StaticWarning(panel.panelId + ": invalid or duplicate key " + e.key); continue; }
            yield return e;
        }
    }
    private RemoteStaticPanelData StaticData(RemoteConfigData config, string id)
    {
        if (config == null || config.staticPanels == null) return null;
        foreach (var p in config.staticPanels)
            if (p != null && string.Equals(p.id, id, StringComparison.OrdinalIgnoreCase)) return p;
        return null;
    }
    private RemoteStaticElementData StaticElement(RemoteStaticPanelData p, string key)
    {
        if (p == null || p.elements == null) return null;
        foreach (var e in p.elements)
            if (e != null && string.Equals(e.key, key, StringComparison.OrdinalIgnoreCase)) return e;
        return null;
    }
    private static void ParseStaticPanels(string json, RemoteConfigData config)
    {
        // JsonUtility may create an empty image for an omitted nested object.
        // Newtonsoft preserves null versus an explicit {}, so validation can stay strict.
        var root = Newtonsoft.Json.Linq.JObject.Parse(json);
        var panels = root["staticPanels"];
        var serializer = Newtonsoft.Json.JsonSerializer.Create(new Newtonsoft.Json.JsonSerializerSettings
        {
            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.None,
            MetadataPropertyHandling = Newtonsoft.Json.MetadataPropertyHandling.Ignore
        });
        config.staticPanels = panels == null || panels.Type == Newtonsoft.Json.Linq.JTokenType.Null
            ? null : panels.ToObject<RemoteStaticPanelData[]>(serializer);
    }

    public bool ValidateStaticConfig(RemoteConfigData config, out string error)
    {
        error = null;
        if (config == null || config.staticPanels == null) return true;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in config.staticPanels)
        {
            if (p == null || !StaticIdValid(p.id) || !ids.Add(p.id)) { error = "Invalid or duplicate static panel ID."; return false; }
            if (string.Equals(StaticTemplateId(p.id), "loading", StringComparison.OrdinalIgnoreCase) &&
                (p.durationSeconds < 0.1f || p.durationSeconds > 3600f))
            { error = p.id + ": durationSeconds must be between 0.1 and 3600."; return false; }
            if (!ValidateStaticStoreUrls(p, out error)) return false;
            if (p.elements == null) continue;
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in p.elements)
            {
                if (e == null || !StaticIdValid(e.key) || !keys.Add(e.key)) { error = p.id + ": invalid or duplicate static element key."; return false; }
                Color unused;
                if (e.overrideColor && (string.IsNullOrEmpty(e.color) || !Regex.IsMatch(e.color, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$") || !ColorUtility.TryParseHtmlString(e.color, out unused))) { error = p.id + "/" + e.key + ": use #RRGGBB or #RRGGBBAA."; return false; }
                if (!ValidateStaticPresentation(e, out error)) { error = p.id + "/" + e.key + ": " + error; return false; }
                if (!StaticAdActionValid(e.adAction)) { error = p.id + "/" + e.key + ": invalid adAction; use none, interstitial or rewarded."; return false; }
                if (!ValidateStaticElementUrl(p.id, e, out error)) return false;
                if (e.image == null) continue;
                Uri uri;
                if (!string.Equals(e.image.key, e.key, StringComparison.OrdinalIgnoreCase) || e.image.version < 1 || e.image.size < 0 || !Uri.TryCreate(e.image.url, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https")) { error = p.id + "/" + e.key + ": invalid image metadata."; return false; }
                if (!string.IsNullOrEmpty(e.image.sha256) && !Regex.IsMatch(e.image.sha256, "^[0-9a-fA-F]{64}$")) { error = p.id + "/" + e.key + ": invalid SHA256."; return false; }
            }
        }
        return true;
    }

    private static bool ValidateStaticStoreUrls(RemoteStaticPanelData panel, out string error)
    {
        error = null;
        if (panel == null || panel.storeUrls == null)
            return true;

        if (!string.Equals(StaticTemplateId(panel.id), "rate_us", StringComparison.OrdinalIgnoreCase))
        {
            error = panel.id + ": storeUrls is supported only by rate_us.";
            return false;
        }

        if (!StaticHttpsUrlValid(panel.storeUrls.android) || !StaticHttpsUrlValid(panel.storeUrls.ios))
        {
            error = panel.id + ": storeUrls must be blank or absolute HTTPS URLs.";
            return false;
        }

        return true;
    }

    private static bool ValidateStaticElementUrl(string panelId, RemoteStaticElementData element, out string error)
    {
        error = null;
        if (element == null || string.IsNullOrWhiteSpace(element.url))
            return true;

        if (!StaticUrlActionAllowed(panelId, element.key))
        {
            error = panelId + "/" + element.key + ": URL actions are not supported on this element.";
            return false;
        }

        if (!StaticHttpsUrlValid(element.url))
        {
            error = panelId + "/" + element.key + ": URL must be an absolute HTTPS URL.";
            return false;
        }

        return true;
    }

    private static bool StaticHttpsUrlValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        Uri uri;
        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri) &&
               string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static bool StaticUrlActionAllowed(string panelId, string elementKey)
    {
        string templateId = StaticTemplateId(panelId);
        if (string.Equals(templateId, "more_games", StringComparison.OrdinalIgnoreCase))
            return Regex.IsMatch(elementKey ?? string.Empty, "^game_[1-6]$", RegexOptions.IgnoreCase);
        return string.Equals(templateId, "play_other_game", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(elementKey, "play_button", StringComparison.OrdinalIgnoreCase);
    }
    private string StaticImagePath(string panel, RemoteImageData image)
    {
        // Static panels share immutable files by content/source identity. Panel id
        // and element key intentionally do not participate, so two panels that
        // reference the same background create one download job and one disk file.
        string sha256 = image != null && !string.IsNullOrWhiteSpace(image.sha256)
            ? image.sha256.Trim().ToLowerInvariant()
            : string.Empty;

        string identity = !string.IsNullOrEmpty(sha256)
            ? "sha256|" + sha256 + "|" + image.size
            : "source|" + (image.url ?? string.Empty).Trim() + "|" + image.size + "|v" + Mathf.Max(1, image.version);

        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant();

        return Path.Combine(CacheRoot, "shared_static_images", hash + ".img");
    }

    private string LegacyStaticImagePath(string panel, RemoteImageData image)
    {
        // Path used by earlier ScenePilot versions. Retained only so an existing
        // valid download can be promoted into the shared cache without networking.
        string identity = image.url + "|" + image.sha256 + "|" + image.size;
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant();

        return Path.Combine(
            CacheRoot,
            "static_panels",
            panel.ToLowerInvariant(),
            image.key.ToLowerInvariant(),
            "v" + image.version + "_" + hash + ".img"
        );
    }

    private string ResolveStaticImagePath(string panel, RemoteImageData image)
    {
        string sharedPath = StaticImagePath(panel, image);
        if (IsCachedFileValid(sharedPath, image))
            return sharedPath;

        string legacyPath = LegacyStaticImagePath(panel, image);
        if (!IsCachedFileValid(legacyPath, image))
            return sharedPath;

        string migrationTemp = sharedPath + ".migrate.part";
        try
        {
            string folder = Path.GetDirectoryName(sharedPath);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            TryDeleteFile(migrationTemp);
            File.Copy(legacyPath, migrationTemp, true);

            string validationError;
            if (ValidateFile(migrationTemp, image, out validationError) &&
                PromoteTempFile(migrationTemp, sharedPath) &&
                IsCachedFileValid(sharedPath, image))
            {
                return sharedPath;
            }

            TryDeleteFile(migrationTemp);
        }
        catch (Exception exception)
        {
            TryDeleteFile(migrationTemp);
            StaticWarning(
                panel + "/" + image.key +
                ": could not migrate the legacy cached image; continuing to use it. " +
                exception.Message
            );
        }

        // Migration failure must never discard or hide an already-valid image.
        return legacyPath;
    }
    private bool StaticImageConflict(RemoteStaticPanel owner, RemoteStaticPanel.Element element)
    {
        if (element.targetImage == null) return false;
        if (slides != null) foreach (var s in slides) if (s != null && s.imageElements != null)
            foreach (var e in s.imageElements) if (e != null && e.targetImage == element.targetImage) { StaticWarning(owner.panelId + "/" + element.key + ": Image is already controlled by a regular slide; skipping static image/color."); return true; }
        foreach (var p in ValidStaticPanels()) foreach (var e in ValidStaticElements(p))
        {
            if (ReferenceEquals(e, element)) return false;
            if (e.targetImage == element.targetImage && (e.allowImage || e.allowColor)) { StaticWarning(owner.panelId + "/" + element.key + ": duplicate Image target; first binding owns it."); return true; }
        }
        return false;
    }
    public bool AddStaticDownloads(RemoteConfigData config, List<DownloadJob> queue, out string error)
    {
        EnsureStaticPanelInstances(config);
        error = null;
        if (config == null || !config.enabled) return true;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in ValidStaticPanels()) foreach (var e in ValidStaticElements(p))
        {
            var data = StaticElement(StaticData(config, p.panelId), e.key);
            if (!e.allowImage || e.targetImage == null || data == null || data.image == null || StaticImageConflict(p, e)) continue;
            string path = ResolveStaticImagePath(p.panelId, data.image);
            if (IsCachedFileValid(path, data.image)) continue;

            // ResolveStaticImagePath returns the shared path when no usable file
            // exists. The shared path therefore makes this a content-level dedupe.
            path = StaticImagePath(p.panelId, data.image);
            if (!paths.Add(path)) continue;
            queue.Add(new DownloadJob { sliderId = "static:" + p.panelId, optionIndex = 0, image = data.image, finalPath = path, tempPath = path + ".part" });
        }
        return true;
    }
    public bool CheckStaticFiles(RemoteConfigData config, out string error)
    {
        EnsureStaticPanelInstances(config);
        error = null;
        if (config == null || !config.enabled) return true;
        foreach (var p in ValidStaticPanels()) foreach (var e in ValidStaticElements(p))
        {
            var data = StaticElement(StaticData(config, p.panelId), e.key);
            if (!e.allowImage || e.targetImage == null || data == null || data.image == null || StaticImageConflict(p, e)) continue;
            if (!IsCachedFileValid(ResolveStaticImagePath(p.panelId, data.image), data.image)) { error = p.panelId + "/" + e.key + ": missing or invalid cached file."; return false; }
        }
        return true;
    }
    private bool DeferStatic(RemoteStaticPanel panel)
    {
        if (!staticStartupComplete) return false;
        if (!panel.deferWhileVisible && !string.Equals(panel.panelId, "download_loading", StringComparison.OrdinalIgnoreCase)) return false;
        var root = panel.panelRoot != null ? panel.panelRoot : (panel.panelId == "download_loading" && loadingParent != null ? loadingParent : panel.gameObject);
        return root != null && root.activeInHierarchy;
    }
    private void ApplyStaticPanels(RemoteConfigData config)
    {
        EnsureStaticPanelInstances(config);

        // Replace pending state on every application, including failure rollback to activeConfig.
        deferredStaticPanels.Clear();

        foreach (var p in ValidStaticPanels())
        {
            var data = StaticData(config, p.panelId);
            if (data == null)
            {
                StaticLog("No JSON static panel matched scene panel '" + p.panelId + "'.");
                ResetStaticInteractions(p);
                continue;
            }

            if (DeferStatic(p))
            {
                deferredStaticPanels[p] = data;
                StaticLog("Deferred panel '" + p.panelId + "' while it is visible.");
            }
            else
            {
                ApplyStaticPanel(p, data);
            }
        }

        // Enabled sequence panels are available steps. Startup visibility is
        // owned by the first enabled panel in the server order, never by the
        // prefab's saved activeSelf state or Canvas sorting order.
        StartStaticPanelSequence(config);
    }
    private bool StaticActivationSafe(RemoteStaticPanel p, RemoteStaticPanel.Element e)
    {
        var target = e.targetObject;
        if (target == null) return false;
        // Whole-screen visibility, controller lifetime and progress UI remain locally owned.
        if (target == p.gameObject || target == p.panelRoot || target == loadingParent || target == mainParent || transform.IsChildOf(target.transform) || (progressSlider != null && progressSlider.transform.IsChildOf(target.transform)) || (retryButton != null && retryButton.transform.IsChildOf(target.transform)) || (progressText != null && progressText.transform.IsChildOf(target.transform)) || (statusText != null && statusText.transform.IsChildOf(target.transform)))
        { StaticWarning(p.panelId + "/" + e.key + ": visibility target belongs to controller/panel lifecycle; skipping SetActive."); return false; }
        if (slides != null) foreach (var s in slides) if (s != null && s.sliderParent == target) { StaticWarning(p.panelId + "/" + e.key + ": GameObject already owned by regular slide."); return false; }
        foreach (var other in ValidStaticPanels()) foreach (var binding in ValidStaticElements(other))
        {
            if (ReferenceEquals(binding, e)) return true;
            if (binding.allowEnabled && binding.targetObject == target) { StaticWarning(p.panelId + "/" + e.key + ": duplicate GameObject target; first binding owns it."); return false; }
        }
        return true;
    }
    private void ApplyStaticPanel(RemoteStaticPanel panel, RemoteStaticPanelData data)
    {
        StaticLog("Applying panel '" + panel.panelId + "'.");

        ConfigureRateUsStoreUrls(panel, data != null ? data.storeUrls : null);

        if (string.Equals(StaticTemplateId(data.id), "loading", StringComparison.OrdinalIgnoreCase))
        {
            RemoteStaticLoadingStep loadingStep = panel.GetComponent<RemoteStaticLoadingStep>();
            if (loadingStep != null)
                loadingStep.durationSeconds = Mathf.Clamp(data.durationSeconds, 0.1f, 3600f);
        }

        foreach (var e in ValidStaticElements(panel))
        {
            var value = StaticElement(data, e.key);
            bool conflict = StaticImageConflict(panel, e);
            ConfigureStaticAdInteraction(panel, e, value);
            ConfigureStaticUrlInteraction(panel, e, value);
            if (value == null)
            {
                StaticLog(panel.panelId + "/" + e.key + ": no matching JSON element; restoring local/default sprite.");
                if (!conflict && e.allowImage)
                    RestoreStaticLocalSprite(e);
                continue;
            }

            RememberStaticSprite(e);

            if (!conflict && e.allowImage && e.targetImage != null && value.image != null)
            {
                string path = ResolveStaticImagePath(panel.panelId, value.image);
                if (IsCachedFileValid(path, value.image) && TryLoadElementImage(e, path))
                {
                    staticOwnedImages.Add(e);
                    StaticLog(panel.panelId + "/" + e.key + ": sprite applied (v" + value.image.version + ").");
                }
                else
                {
                    StaticWarning(panel.panelId + "/" + e.key + ": downloaded image unavailable; restoring local/default sprite.");
                    RestoreStaticLocalSprite(e);
                }
            }
            else if (!conflict && e.allowImage && e.targetImage != null && value.image == null)
            {
                // Omitting an image means "use Unity's authored image", not
                // "keep an older downloaded runtime sprite alive".
                RestoreStaticLocalSprite(e);
            }

            ApplyStaticPresentation(panel, e, value, conflict);
            Color color;
            if (!conflict && e.allowColor && e.targetImage != null && value.overrideColor && ColorUtility.TryParseHtmlString(value.color, out color))
            {
                e.targetImage.color = color;
                StaticLog(panel.panelId + "/" + e.key + ": color applied " + value.color + ".");
            }

            if (e.allowEnabled && value.overrideEnabled && StaticActivationSafe(panel, e))
            {
                ApplyStaticEnabledState(e.targetObject, value.enabled);
                StaticLog(panel.panelId + "/" + e.key + ": enabled=" + value.enabled + ".");
            }
        }

        ApplyStaticPanelActivation(panel, data);
    }

    private static bool StaticAdActionValid(string value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "interstitial", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "rewarded", StringComparison.OrdinalIgnoreCase);
    }

    private void ResetStaticInteractions(RemoteStaticPanel panel)
    {
        foreach (RemoteStaticPanel.Element binding in ValidStaticElements(panel))
        {
            ConfigureStaticAdInteraction(panel, binding, null);
            ConfigureStaticUrlInteraction(panel, binding, null);
        }
        ConfigureRateUsStoreUrls(panel, null);
    }

    private void ConfigureRateUsStoreUrls(RemoteStaticPanel panel, RemoteStoreUrlsData storeUrls)
    {
        if (panel == null || !string.Equals(PanelTemplateId(panel), "rate_us", StringComparison.OrdinalIgnoreCase))
            return;

        RemoteRateUsPanel rateUs = panel.GetComponent<RemoteRateUsPanel>();
        if (rateUs == null)
            rateUs = panel.GetComponentInChildren<RemoteRateUsPanel>(true);

        string androidUrl = storeUrls != null ? storeUrls.android : null;
        string iosUrl = storeUrls != null ? storeUrls.ios : null;
        if (rateUs == null)
        {
            if (!string.IsNullOrWhiteSpace(androidUrl) || !string.IsNullOrWhiteSpace(iosUrl))
                StaticWarning(panel.panelId + ": storeUrls needs a RemoteRateUsPanel component on this panel or one of its children.");
            return;
        }

        rateUs.ConfigureRemoteStoreUrls(androidUrl, iosUrl);
        StaticLog(panel.panelId + ": platform store URLs updated; blank values keep the local fallback.");
    }

    private void ConfigureStaticUrlInteraction(
        RemoteStaticPanel panel,
        RemoteStaticPanel.Element binding,
        RemoteStaticElementData value)
    {
        if (panel == null || binding == null)
            return;

        GameObject target = binding.targetObject != null
            ? binding.targetObject
            : binding.targetImage != null ? binding.targetImage.gameObject : null;
        if (!binding.allowUrlAction)
        {
            RemoteStaticUrlInteraction disabled = target != null ? target.GetComponent<RemoteStaticUrlInteraction>() : null;
            if (disabled != null)
                disabled.Configure(panel.panelId, binding.key, null);
            return;
        }
        string url = value != null ? value.url : null;
        bool requestsUrl = !string.IsNullOrWhiteSpace(url);
        bool supported = StaticUrlActionAllowed(panel.panelId, binding.key);

        if (!supported)
        {
            RemoteStaticUrlInteraction stale = target != null ? target.GetComponent<RemoteStaticUrlInteraction>() : null;
            if (stale != null)
                stale.Configure(panel.panelId, binding.key, null);
            if (requestsUrl)
                StaticWarning(panel.panelId + "/" + binding.key + ": URL ignored because this is not a supported game-promotion action.");
            return;
        }

        if (target == null)
        {
            if (requestsUrl)
                StaticWarning(panel.panelId + "/" + binding.key + ": URL needs a Target Object or Target Image GameObject.");
            return;
        }

        Button button = target.GetComponent<Button>();
        RemoteStaticUrlInteraction interaction = target.GetComponent<RemoteStaticUrlInteraction>();
        if (button == null && requestsUrl && target.GetComponent<Graphic>() != null)
            button = target.AddComponent<Button>();

        if (button == null)
        {
            if (interaction != null)
                interaction.Configure(panel.panelId, binding.key, null);
            if (requestsUrl)
                StaticWarning(panel.panelId + "/" + binding.key + ": URL ignored because the bound object has no Button or UI Graphic component.");
            return;
        }

        if (interaction == null && requestsUrl)
            interaction = target.AddComponent<RemoteStaticUrlInteraction>();

        if (interaction != null)
        {
            interaction.Configure(panel.panelId, binding.key, url);
            StaticLog(panel.panelId + "/" + binding.key + ": URL action " + (requestsUrl ? "configured" : "cleared; local action preserved") + ".");
        }
    }

    private void ConfigureStaticAdInteraction(
        RemoteStaticPanel panel,
        RemoteStaticPanel.Element binding,
        RemoteStaticElementData value)
    {
        if (panel == null || binding == null || !binding.allowAdAction)
            return;

        GameObject target = binding.targetObject != null
            ? binding.targetObject
            : binding.targetImage != null ? binding.targetImage.gameObject : null;
        string action = value != null ? value.adAction : null;
        bool requestsAd = !string.IsNullOrWhiteSpace(action) &&
                          !string.Equals(action, "none", StringComparison.OrdinalIgnoreCase);

        if (target == null)
        {
            if (requestsAd)
                StaticWarning(panel.panelId + "/" + binding.key + ": adAction needs a Target Object or Target Image GameObject.");
            return;
        }

        Button button = target.GetComponent<Button>();
        Toggle toggle = target.GetComponent<Toggle>();
        RemoteStaticAdInteraction interaction = target.GetComponent<RemoteStaticAdInteraction>();

        if (button == null && toggle == null)
        {
            if (interaction != null)
                interaction.Configure(panel.panelId, binding.key, "none");
            if (requestsAd)
                StaticWarning(panel.panelId + "/" + binding.key + ": adAction ignored because the bound object has no Button or Toggle component.");
            return;
        }

        if (interaction == null && requestsAd)
            interaction = target.AddComponent<RemoteStaticAdInteraction>();

        if (interaction != null)
        {
            interaction.Configure(panel.panelId, binding.key, action);
            StaticLog(panel.panelId + "/" + binding.key + ": adAction=" + (string.IsNullOrWhiteSpace(action) ? "none" : action) + ".");
        }
    }

    private void RestoreStaticLocalSprite(RemoteStaticPanel.Element element)
    {
        if (element == null || element.targetImage == null)
            return;

        RememberStaticSprite(element);

        Sprite original;
        staticOriginalSprites.TryGetValue(element, out original);

        Sprite localSprite =
            original != null
                ? original
                : element.fallbackSprite;

        // A valid decoded remote sprite is better than an empty Image. Keep it
        // until a real local/default sprite becomes available.
        if (localSprite == null || localSprite == element.runtimeSprite)
            return;

        // Restore local content before releasing the previous runtime image.
        staticOwnedImages.Remove(element);
        element.targetImage.sprite = localSprite;
        DestroyElementRuntimeImage(element);
    }

    [Obsolete]
    private void ApplyStaticPanelActivation(RemoteStaticPanel panel, RemoteStaticPanelData data)
    {
        if (panel == null || data == null || !panel.allowRemotePanelActivation)
            return;

        if (panel.panelRoot == null)
        {
            StaticWarning(
                panel.panelId +
                ": remote panel activation is enabled but Panel Root is not assigned; preserving scene visibility."
            );
            return;
        }

        // Navigated panels use enabled as availability. Only the navigator may
        // turn an enabled panel on; disabled panels are always forced off.
        if (IsStaticSequencePanelId(panel.panelId))
        {
            if (!data.enabled) SetStaticPanelVisible(panel, false);
            return;
        }

        // A shared/containing root is dangerous only when turning it OFF.
        // Turning it ON cannot hide another panel and must always be allowed,
        // otherwise a previously disabled panel can never recover.
        if (!data.enabled && !StaticPanelActivationSafe(panel, true))
            return;

        bool before = panel.panelRoot.activeSelf;
        ApplyStaticEnabledState(panel.panelRoot, data.enabled);

        StaticLog(
            panel.panelId +
            ": Panel Root '" + StaticHierarchyPath(panel.panelRoot.transform) +
            "' (instance " + panel.panelRoot.GetInstanceID() +
            ") activeSelf " + before + " -> " + panel.panelRoot.activeSelf +
            ", activeInHierarchy=" + panel.panelRoot.activeInHierarchy +
            ", requested=" + data.enabled + "."
        );

        if (data.enabled && !panel.panelRoot.activeInHierarchy)
        {
            StaticWarning(
                panel.panelId +
                ": JSON enabled=true and Panel Root activeSelf=true, but the panel is still inactive because ancestor '" +
                StaticFirstInactiveAncestor(panel.panelRoot.transform) +
                "' is disabled."
            );
        }
    }

    private void SetStaticPanelVisible(RemoteStaticPanel panel, bool visible)
    {
        if (panel == null) return;

        // Discovery includes inactive objects. Reveal an authored prefab/scene
        // wrapper before its child root, just as we already do for runtime
        // clones, so panels may safely be saved inactive.
        if (visible && !panel.gameObject.activeSelf)
            panel.gameObject.SetActive(true);

        if (panel.panelRoot != null)
        {
            if (panel.panelRoot.activeSelf != visible)
                panel.panelRoot.SetActive(visible);

            if (visible)
            {
                RemoteStaticPanelAnimator animator =
                    panel.panelRoot.GetComponent<RemoteStaticPanelAnimator>();
                if (animator != null)
                    animator.PlayShowIfNeeded();
            }
        }
        if (visible && panel.panelRoot != null && panel.panelRoot.activeInHierarchy &&
            string.Equals(panel.panelId, "rate_us", StringComparison.OrdinalIgnoreCase))
        {
            RemoteRateUsPanel rateUs = panel.GetComponent<RemoteRateUsPanel>();
            if (rateUs == null) rateUs = panel.GetComponentInChildren<RemoteRateUsPanel>(true);
            if (rateUs != null) rateUs.PresentNativeReview();
        }
        if (!visible && panel.runtimeGenerated && panel.gameObject.activeSelf)
            panel.gameObject.SetActive(false);
    }

    private void HideRemoteStaticSequencePanels()
    {
        foreach (var panel in ValidStaticPanels())
            if (IsStaticSequencePanelId(panel.panelId)) SetStaticPanelVisible(panel, false);
    }

    private bool IsRemoteStaticPanelEligible(RemoteStaticPanel panel, RemoteStaticPanelData data)
    {
        if (panel == null || !panel.useRemoteContent || panel.panelRoot == null ||
            data == null || !data.enabled || !IsStaticSequencePanelId(panel.panelId))
            return false;

        // A script-only review step has no UI to operate without its bridge.
        if (string.Equals(panel.panelId, "rate_us", StringComparison.OrdinalIgnoreCase) &&
            panel.GetComponent<RemoteRateUsPanel>() == null &&
            panel.GetComponentInChildren<RemoteRateUsPanel>(true) == null)
        {
            StaticWarning(panel.panelId + ": native review bridge is missing; skipping this step.");
            return false;
        }

        // A main menu with no visible Play control cannot advance the sequence.
        if (string.Equals(PanelTemplateId(panel), "main_menu", StringComparison.OrdinalIgnoreCase))
        {
            var play = panel.elements?.Find(element =>
                element != null && string.Equals(element.key, "play_button", StringComparison.OrdinalIgnoreCase));
            if (play == null || play.targetObject == null || !play.targetObject.activeSelf ||
                (play.targetImage != null && (!play.targetImage.enabled || play.targetImage.color.a <= 0f)))
            {
                StaticWarning(panel.panelId + ": Play control is hidden; skipping this step.");
                return false;
            }
        }

        // Match the downloader's keys, ownership exclusions and cache validation.
        // Applied runtime objects also prove that valid bytes decoded as an image.
        foreach (var element in ValidStaticElements(panel))
        {
            var value = StaticElement(data, element.key);
            if (element.allowEnabled && value != null && value.overrideEnabled && !value.enabled) continue;
            bool requiredArtwork = element.requireSpriteForPanel ||
                (string.Equals(PanelTemplateId(panel), "card_selection", StringComparison.OrdinalIgnoreCase) &&
                 Regex.IsMatch(element.key, "^card_[0-9]+$", RegexOptions.IgnoreCase)) ||
                (string.Equals(PanelTemplateId(panel), "level_selection", StringComparison.OrdinalIgnoreCase) &&
                 Regex.IsMatch(element.key, "^level_[0-9]+$", RegexOptions.IgnoreCase));
            if (requiredArtwork && (element.targetImage == null || element.targetImage.sprite == null))
            {
                StaticWarning(panel.panelId + "/" + element.key + ": skipping panel; required artwork is empty.");
                return false;
            }
            if (!element.allowImage || element.targetImage == null || value == null ||
                value.image == null || StaticImageConflict(panel, element)) continue;
            string path = ResolveStaticImagePath(panel.panelId, value.image);
            if (!IsCachedFileValid(path, value.image) || element.runtimeSprite == null ||
                element.runtimeTexture == null ||
                !string.Equals(element.loadedPath, path, StringComparison.OrdinalIgnoreCase))
            {
                StaticWarning(panel.panelId + "/" + element.key + ": skipping panel; required remote image is missing or invalid.");
                return false;
            }
        }
        return true;
    }

    private bool HasEligibleRemoteStaticSequence(RemoteConfigData config)
    {
        if (config != null && config.enabled && config.staticPanels != null)
            foreach (var data in config.staticPanels)
                if (data != null && IsRemoteStaticPanelEligible(FindStaticPanelInstance(data.id), data)) return true;
        return false;
    }

    private void FinishUnavailableRemoteStaticSequence(RemoteConfigData config)
    {
        if (!staticNavigationReady || waitingForInternet || HasEligibleRemoteStaticSequence(config)) return;

        HideRemoteStaticSequencePanels();
        staticSequenceStarted = true;
        FinishStaticPanelSequence(null, "No eligible remote static panels; continuing to local gameplay.");
    }

    // Start from the first enabled panel in the cached server order. The
    // download_loading overlay is excluded by IsStaticSequencePanelId().
    private void StartStaticPanelSequence(RemoteConfigData config)
    {
        // Cached images may be incomplete or refer to the previous release.
        // Do not select/skip a step until the download and visual commit finish.
        if (!staticNavigationReady)
        {
            HideRemoteStaticSequencePanels();
            return;
        }

        if (waitingForInternet || config == null || !config.enabled || config.staticPanels == null)
        {
            HideRemoteStaticSequencePanels();
            return;
        }

        EnsureStaticPanelInstances(config);

        // ApplyConfig runs for cached data in Awake and Start, then again for
        // fresh network data. Never rewind a sequence that the player or an
        // automatic loading step has already advanced.
        if (staticSequenceStarted)
        {
            if (staticSequenceFinished)
            {
                foreach (RemoteStaticPanel panel in ValidStaticPanels())
                    if (IsStaticSequencePanelId(panel.panelId)) SetStaticPanelVisible(panel, false);

                StaticLog("Navigation refresh: sequence already finished; keeping panels hidden.");
                return;
            }

            RemoteStaticPanel reached = null;
            foreach (RemoteStaticPanel panel in ValidStaticPanels())
            {
                if (!IsStaticSequencePanelId(panel.panelId) || panel.panelRoot == null || !panel.panelRoot.activeInHierarchy)
                    continue;

                RemoteStaticPanelData reachedData = StaticData(config, panel.panelId);
                if (IsRemoteStaticPanelEligible(panel, reachedData))
                {
                    reached = panel;
                    break;
                }
            }

            if (reached != null)
            {
                foreach (RemoteStaticPanel panel in ValidStaticPanels())
                    if (IsStaticSequencePanelId(panel.panelId)) SetStaticPanelVisible(panel, ReferenceEquals(panel, reached));

                StaticLog("Navigation refresh: preserving reached panel '" + reached.panelId + "'.");
                return;
            }
        }

        staticSequenceStarted = true;
        staticSequenceFinished = false;

        RemoteStaticPanel first = null;
        foreach (RemoteStaticPanelData data in config.staticPanels)
        {
            if (data == null || !data.enabled || !IsStaticSequencePanelId(data.id)) continue;
            first = FindStaticPanelInstance(data.id);
            if (IsRemoteStaticPanelEligible(first, data)) break;
            first = null;
        }

        foreach (RemoteStaticPanel panel in ValidStaticPanels())
        {
            if (!IsStaticSequencePanelId(panel.panelId)) continue;
            SetStaticPanelVisible(panel, ReferenceEquals(panel, first));
        }

        StaticLog(first != null
            ? "Navigation startup: showing first enabled panel '" + first.panelId + "'."
            : "Navigation startup: no enabled static sequence panel was found.");
    }

    // Once navigation has started, preserve the reached step and only prevent
    // two enabled sequence panels from remaining visible simultaneously.
    private void EnsureSingleVisibleStaticSequencePanel()
    {
        if (activeConfig == null || activeConfig.staticPanels == null) return;

        RemoteStaticPanel selected = null;
        foreach (RemoteStaticPanelData data in activeConfig.staticPanels)
        {
            if (data == null || !data.enabled || !IsStaticSequencePanelId(data.id)) continue;
            RemoteStaticPanel candidate = FindStaticPanelInstance(data.id);
            if (candidate != null && candidate.panelRoot != null && candidate.panelRoot.activeInHierarchy)
            {
                selected = candidate;
                break;
            }
        }

        if (selected == null) return;

        foreach (RemoteStaticPanel panel in ValidStaticPanels())
        {
            if (ReferenceEquals(panel, selected) || !IsStaticSequencePanelId(panel.panelId) || panel.panelRoot == null)
                continue;
            if (panel.panelRoot.activeInHierarchy)
                SetStaticPanelVisible(panel, false);
        }
    }

    private bool FinishStaticPanelSequence(RemoteStaticPanel current, string message)
    {
        StaticLog(message);

        // Completion is a one-shot transition. Invoke gameplay callbacks first,
        // then remove the final ScenePilot panel so it cannot keep intercepting
        // input or remain visible over the destination UI.
        if (!staticSequenceFinished)
        {
            staticSequenceFinished = true;
            // Start the persistent review timer before any existing completion
            // listener can synchronously load the next scene.
            ScenePilotNativeReviewController.NotifyStaticSequenceFinished();
            if (onStaticSequenceFinished != null) onStaticSequenceFinished.Invoke();
        }

        if (current != null)
            SetStaticPanelVisible(current, false);

        return false;
    }

    // A panel's visual root may be a child of its still-active behaviour host.
    // Timers and navigation must use the visual root and the download state.
    private bool CanAdvanceStaticPanel(RemoteStaticPanel panel, bool ignoreBusyState)
    {
        if (!staticNavigationReady || panel == null || !panel.isActiveAndEnabled ||
            panel.panelRoot == null || !panel.panelRoot.activeInHierarchy ||
            !IsStaticSequencePanelId(panel.panelId) || staticSequenceFinished)
            return false;

        // Never run a sequence step behind the visible download overlay. A
        // normal loading timer may ignore the controller's internal isBusy
        // flag once that overlay is gone, so its displayed duration stays exact.
        if (loadingParent != null && loadingParent.activeInHierarchy)
            return false;

        if (!ignoreBusyState && isBusy)
            return false;

        RemoteStaticPanelData data = StaticData(activeConfig, panel.panelId);
        return !waitingForInternet && activeConfig != null && activeConfig.enabled && data != null && data.enabled;
    }

    public bool CanAdvanceStaticPanel(RemoteStaticPanel panel)
    {
        return CanAdvanceStaticPanel(panel, false);
    }

    public bool CanRunStaticLoadingPanel(RemoteStaticPanel panel)
    {
        return CanAdvanceStaticPanel(panel, true);
    }

    public bool ShowNextStaticPanel(RemoteStaticPanel current)
    {
        return ShowNextStaticPanel(current, false);
    }

    public bool ShowNextStaticLoadingPanel(RemoteStaticPanel current)
    {
        return ShowNextStaticPanel(current, true);
    }

    private bool ShowNextStaticPanel(RemoteStaticPanel current, bool ignoreBusyState)
    {
        if (current == null)
            return false;

        if (!CanAdvanceStaticPanel(current, ignoreBusyState))
        {
            StaticLog("Navigation ignored from hidden, disabled or covered panel '" + current.panelId + "'.");
            return false;
        }

        if (activeConfig == null || activeConfig.staticPanels == null)
            return FinishStaticPanelSequence(
                current,
                "Navigation: no active remote sequence is available after '" + current.panelId + "'; using local fallback."
            );

        EnsureStaticPanelInstances(activeConfig);
        int currentIndex = -1;
        for (int i = 0; i < activeConfig.staticPanels.Length; i++)
            if (activeConfig.staticPanels[i] != null && string.Equals(activeConfig.staticPanels[i].id, current.panelId, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }

        if (currentIndex < 0)
            return FinishStaticPanelSequence(
                current,
                "Navigation: active config does not contain current panel '" + current.panelId + "'; using local fallback."
            );
        for (int i = currentIndex + 1; i < activeConfig.staticPanels.Length; i++)
        {
            RemoteStaticPanelData data = activeConfig.staticPanels[i];
            if (data == null || !data.enabled) continue;
            RemoteStaticPanel next = FindStaticPanelInstance(data.id);
            if (next == null || !IsStaticSequencePanelId(next.panelId)) continue;

            ApplyStaticPanel(next, data);
            if (!IsRemoteStaticPanelEligible(next, data)) continue;
            foreach (RemoteStaticPanel panel in ValidStaticPanels())
                if (IsStaticSequencePanelId(panel.panelId)) SetStaticPanelVisible(panel, ReferenceEquals(panel, next));
            StaticLog("Navigation: " + current.panelId + " -> " + next.panelId + ".");
            return true;
        }

        return FinishStaticPanelSequence(
            current,
            "Navigation: no enabled static panel follows '" + current.panelId + "'."
        );
    }

    // A panel root must belong to one panel ID only. Disabling a shared root or
    // a root that contains another registered panel would hide that other panel
    // even though its own JSON enabled value is true.
    private bool StaticPanelActivationSafe(RemoteStaticPanel panel, bool logWarning)
    {
        if (panel == null || panel.panelRoot == null)
            return false;

        Transform target = panel.panelRoot.transform;

        foreach (RemoteStaticPanel other in ValidStaticPanels())
        {
            if (
                other == null ||
                ReferenceEquals(other, panel) ||
                string.Equals(other.panelId, panel.panelId, StringComparison.OrdinalIgnoreCase)
            )
                continue;

            bool sharesRoot = other.panelRoot != null && other.panelRoot == panel.panelRoot;
            bool containsOtherRoot =
                other.panelRoot != null &&
                other.panelRoot.transform.IsChildOf(target);
            bool containsOtherBinding = other.transform.IsChildOf(target);

            if (!sharesRoot && !containsOtherRoot && !containsOtherBinding)
                continue;

            if (logWarning)
            {
                StaticWarning(
                    panel.panelId +
                    ": refusing to change Panel Root '" + StaticHierarchyPath(target) +
                    "' because it also owns or contains static panel '" + other.panelId +
                    "'. Assign a dedicated Panel Root to each panel."
                );
            }

            return false;
        }

        return true;
    }

    private static string StaticHierarchyPath(Transform target)
    {
        if (target == null)
            return "<null>";

        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }

    private static string StaticFirstInactiveAncestor(Transform target)
    {
        if (target == null)
            return "<null>";

        Transform current = target.parent;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                return StaticHierarchyPath(current);

            current = current.parent;
        }

        return "<unknown>";
    }

    private static void ApplyStaticEnabledState(GameObject target, bool enabled)
    {
        if (target != null && target.activeSelf != enabled)
            target.SetActive(enabled);
    }



    // =========================================================
    // STARTUP VISUAL COMMIT BARRIER
    // =========================================================
    // Downloads being complete is not enough. Before the downloader overlay
    // is allowed to disappear we force-apply static visuals and verify that
    // the bound Images still hold the expected remote sprite/color.
    // This prevents one-frame (or short) flashes of the built-in artwork.
    private bool ApplyStaticPanelsImmediately(RemoteConfigData config, out string error)
    {
        error = null;
        EnsureStaticPanelInstances(config);
        deferredStaticPanels.Clear();

        if (config == null || !config.enabled)
            return true;

        foreach (var panel in ValidStaticPanels())
        {
            var data = StaticData(config, panel.panelId);
            if (data == null)
                continue;

            // Intentionally bypass DeferStatic here. The loading overlay is
            // still visible and acts as the cover while final visuals commit.
            ApplyStaticPanel(panel, data);
        }

        return VerifyStaticPanelsApplied(config, out error);
    }

    private bool VerifyStaticPanelsApplied(RemoteConfigData config, out string error)
    {
        error = null;
        EnsureStaticPanelInstances(config);

        if (config == null || !config.enabled)
            return true;

        foreach (var panel in ValidStaticPanels())
        {
            var data = StaticData(config, panel.panelId);
            if (data == null)
                continue;

            if (
                panel.allowRemotePanelActivation &&
                panel.panelRoot != null &&
                !IsStaticSequencePanelId(panel.panelId) &&
                (data.enabled || StaticPanelActivationSafe(panel, false)) &&
                panel.panelRoot.activeSelf != data.enabled
            )
            {
                error = panel.panelId + ": panel enabled state has not committed to Panel Root yet.";
                return false;
            }

            if (IsStaticSequencePanelId(panel.panelId) && !data.enabled && panel.panelRoot != null && panel.panelRoot.activeSelf)
            {
                error = panel.panelId + ": disabled navigation panel is still active.";
                return false;
            }

            foreach (var element in ValidStaticElements(panel))
            {
                var value = StaticElement(data, element.key);
                if (value == null)
                    continue;

                bool conflict = StaticImageConflict(panel, element);
                if (conflict)
                    continue;

                if (element.allowImage && element.targetImage != null && value.image != null)
                {
                    string expectedPath = ResolveStaticImagePath(panel.panelId, value.image);

                    if (!IsCachedFileValid(expectedPath, value.image))
                    {
                        error = panel.panelId + "/" + element.key + ": cached image is not valid.";
                        return false;
                    }

                    if (element.runtimeSprite == null ||
                        element.runtimeTexture == null ||
                        !string.Equals(element.loadedPath, expectedPath, StringComparison.OrdinalIgnoreCase) ||
                        element.targetImage.sprite != element.runtimeSprite)
                    {
                        error = panel.panelId + "/" + element.key + ": sprite has not committed to the target Image yet.";
                        return false;
                    }
                }

                if (element.allowColor && element.targetImage != null && value.overrideColor)
                {
                    Color expectedColor;
                    if (!ColorUtility.TryParseHtmlString(value.color, out expectedColor))
                    {
                        error = panel.panelId + "/" + element.key + ": invalid color during commit verification.";
                        return false;
                    }

                    Color actual = element.targetImage.color;
                    const float epsilon = 0.002f;
                    if (Mathf.Abs(actual.r - expectedColor.r) > epsilon ||
                        Mathf.Abs(actual.g - expectedColor.g) > epsilon ||
                        Mathf.Abs(actual.b - expectedColor.b) > epsilon ||
                        Mathf.Abs(actual.a - expectedColor.a) > epsilon)
                    {
                        error = panel.panelId + "/" + element.key + ": color was overwritten before reveal.";
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private void LateUpdate()
    {
        if (deferredStaticPanels.Count > 0)
        {
            FlushDeferredStaticPanels();
        }

        EnsureSingleVisibleStaticSequencePanel();

        if (!lockRemoteStaticSprites)
            return;

        // Remove stale bindings first. Elements are plain serializable classes,
        // so a destroyed scene object can leave the element reference alive
        // while its Image reference becomes null.
        staticOwnedImages.RemoveWhere(
            element =>
                element == null ||
                element.targetImage == null ||
                element.runtimeSprite == null ||
                element.runtimeTexture == null
        );

        // Static remote content is authoritative once it has been applied
        // successfully. This is intentionally cheap: no disk I/O, no image
        // decode and no allocation. In the normal frame this is just a sprite
        // reference comparison.
        foreach (RemoteStaticPanel.Element element in staticOwnedImages)
        {
            // If a local owner assigns its default after Awake, remember it
            // before the remote sprite is reasserted for this frame.
            RememberStaticSprite(element);

            if (
                element.targetImage.sprite !=
                element.runtimeSprite
            )
            {
                element.targetImage.sprite =
                    element.runtimeSprite;
            }
        }
    }

    // force=false: normal behavior, deferred panels wait until hidden.
    // force=true: final download completion applies sprites/colors immediately
    // while the downloader is still visible, then the main coroutine hides it.
    private void FlushDeferredStaticPanels(bool force = false)
    {
        if (activeConfig != null && !activeConfig.enabled)
        {
            deferredStaticPanels.Clear();
            return;
        }

        foreach (var entry in new List<KeyValuePair<RemoteStaticPanel, RemoteStaticPanelData>>(deferredStaticPanels))
        {
            if (entry.Key == null || !entry.Key.useRemoteContent)
            {
                deferredStaticPanels.Remove(entry.Key);
                continue;
            }

            if (!force && DeferStatic(entry.Key)) continue;

            StaticLog((force ? "Force applying deferred panel '" : "Applying deferred panel '") + entry.Key.panelId + "'.");
            ApplyStaticPanel(entry.Key, entry.Value);
            deferredStaticPanels.Remove(entry.Key);
        }
    }
    private void ReleaseStaticImages(bool forceDestroyRuntime = false)
    {
        deferredStaticPanels.Clear();

        foreach (RemoteStaticPanel.Element element in
                 new List<RemoteStaticPanel.Element>(staticOwnedImages))
        {
            if (element == null)
                continue;

            RestoreStaticLocalSprite(element);

            if (forceDestroyRuntime && element.runtimeSprite != null)
            {
                staticOwnedImages.Remove(element);
                DestroyElementRuntimeImage(element);
            }
        }

        if (forceDestroyRuntime)
        {
            staticOwnedImages.Clear();
            staticOriginalSprites.Clear();
        }
    }
}

// Optional static presentation only. Downloading and dynamic slides remain in the controller.
public partial class RemoteContentController
{
    [Serializable] public class StaticLayoutData { public float? x, y, width, height; }
    [Serializable] public class StaticTextData { public string value; public float? fontSize; public string alignment; }

    private static bool InRange(float? value, float min, float max)
    { return value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value) && value.Value >= min && value.Value <= max; }

    private static bool ValidateStaticPresentation(RemoteStaticElementData e, out string error)
    {
        error = null;
        var r = e.layout;
        if (r != null && (!InRange(r.x,-1920,1920) || !InRange(r.y,-1080,1080) || !InRange(r.width,1,3840) || !InRange(r.height,1,2160)))
        { error = "Invalid layout; require x/y/width/height in the 1920x1080 reference units."; return false; }
        var t = e.text;
        if (t != null && (t.value == null || t.value.Length > 2000 || !InRange(t.fontSize,1,300) || (t.alignment != "left" && t.alignment != "center" && t.alignment != "right")))
        { error = "Invalid text value, fontSize or alignment."; return false; }
        return true;
    }

    private void ApplyStaticPresentation(RemoteStaticPanel panel, RemoteStaticPanel.Element binding, RemoteStaticElementData value, bool imageConflict)
    {
        if (!imageConflict && binding.allowImageProperties && binding.targetImage != null)
        {
            Image image = binding.targetImage;
            // Missing/null means Keep Unity setting: use the current Image value.
            bool preserveAspect = value.preserveAspect ?? image.preserveAspect;
            // Unity's borderless Sliced fallback ignores preserveAspect. Match
            // the Inspector's sprite-change behavior, including local settings.
            // Keep Filled progress bars and sprites with real borders unchanged.
            if (preserveAspect && image.type == Image.Type.Sliced &&
                image.sprite == binding.runtimeSprite && binding.runtimeSprite != null &&
                !image.hasBorder)
                image.type = Image.Type.Simple;

            if (value.preserveAspect.HasValue)
                image.preserveAspect = value.preserveAspect.Value;
            if (value.raycastTarget.HasValue) binding.targetImage.raycastTarget = value.raycastTarget.Value;
            // Scene-authored loading templates may not have a loading-step
            // component. Apply the fill geometry here as well for every clone.
            if (string.Equals(binding.key, "fill", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(PanelTemplateId(panel), "loading", StringComparison.OrdinalIgnoreCase))
            {
                Slider slider = image.GetComponentInParent<Slider>(true);
                if (slider != null && slider.fillRect == image.rectTransform)
                {
                    image.type = Image.Type.Filled;
                    image.fillMethod = Image.FillMethod.Horizontal;
                    image.fillOrigin = slider.direction == Slider.Direction.RightToLeft ? 1 : 0;
                    image.fillAmount = slider.normalizedValue;
                    RectTransform fillRect = image.rectTransform;
                    fillRect.anchorMin = Vector2.zero;
                    fillRect.anchorMax = Vector2.one;
                    fillRect.offsetMin = new Vector2(0f, 22f);
                    fillRect.offsetMax = new Vector2(-4f, -10f);
                }
            }
        }
        var text = binding.targetText;
        if (text != null)
        {
            // Never take over the downloader's live progress/status labels.
            bool runtimeLabel = text == progressText || text == statusText;
            if (binding.allowText && value.text != null && !runtimeLabel)
            {
                text.richText = false; // Website preview treats content as literal text.
                text.enableAutoSizing = false; // Explicit font size must have a deterministic meaning.
                text.text = value.text.value;
                text.fontSize = value.text.fontSize.Value;
                text.alignment = value.text.alignment == "left" ? TMPro.TextAlignmentOptions.MidlineLeft : value.text.alignment == "right" ? TMPro.TextAlignmentOptions.MidlineRight : TMPro.TextAlignmentOptions.Midline;
            }
            else if (runtimeLabel && value.text != null) StaticWarning(panel.panelId + "/" + binding.key + ": downloader status text stays locally controlled.");
            Color color;
            if (binding.allowColor && value.overrideColor && ColorUtility.TryParseHtmlString(value.color, out color)) text.color = color;
        }
        if (value.layout == null || !binding.allowLayout) return;
        var rect = binding.targetRect;
        var parent = binding.layoutParent;
        if (rect == null || parent == null || rect.parent != parent || parent.rect.width <= 0 || parent.rect.height <= 0)
        { StaticWarning(panel.panelId + "/" + binding.key + ": layout needs a target RectTransform and its direct reference parent."); return; }
        if (imageConflict || rect == panel.transform || rect == transform || transform.IsChildOf(rect) || rect.gameObject == loadingParent || rect.gameObject == mainParent)
        { StaticWarning(panel.panelId + "/" + binding.key + ": unsafe layout target; preserving local layout."); return; }
        var group = parent.GetComponent<LayoutGroup>();
        var fitter = rect.GetComponent<ContentSizeFitter>();
        var aspect = rect.GetComponent<AspectRatioFitter>();
        if ((group != null && group.isActiveAndEnabled) || (fitter != null && fitter.isActiveAndEnabled) || (aspect != null && aspect.isActiveAndEnabled))
        { StaticWarning(panel.panelId + "/" + binding.key + ": local layout component owns this rectangle; skipping remote layout."); return; }
        float sx = parent.rect.width / 1920f, sy = parent.rect.height / 1080f;
        var r = value.layout;
        float width = r.width.Value * sx, height = r.height.Value * sy;
        rect.anchorMin = rect.anchorMax = new Vector2(0,1);
        rect.sizeDelta = new Vector2(width,height);
        rect.anchoredPosition = new Vector2(r.x.Value*sx + width*rect.pivot.x, -r.y.Value*sy - height*(1-rect.pivot.y));
    }
}
