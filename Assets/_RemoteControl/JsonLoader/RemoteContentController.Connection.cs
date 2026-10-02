using System;
using UnityEngine;

public partial class RemoteContentController
{
    [ContextMenu("ScenePilot/Validate connection")]
    public void ValidateScenePilotConnection()
    {
        ValidateScenePilotConnection(true);
    }

    private void ValidateScenePilotConnection(bool logSuccess)
    {
        int issues = 0;
        string configuredPackage = PackageFromConfigUrl(configUrl);

        if (string.IsNullOrWhiteSpace(configUrl) || !Uri.TryCreate(configUrl, UriKind.Absolute, out _))
        {
            Debug.LogError("[ScenePilot] Config URL is missing or invalid.", this);
            issues++;
        }

        if (!string.IsNullOrEmpty(configuredPackage) &&
            !string.Equals(configuredPackage, Application.identifier, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning(
                "[ScenePilot] URL package '" + configuredPackage + "' differs from Application.identifier '" +
                Application.identifier + "'. Confirm that this project should read that game's config.", this);
            issues++;
        }

        if (mainParent == null)
        {
            Debug.LogWarning("[ScenePilot] Main Parent is not assigned.", this);
            issues++;
        }

        if (loadingParent == null)
        {
            Debug.LogWarning("[ScenePilot] Loading Parent is not assigned.", this);
            issues++;
        }
        else if (mainParent != null &&
                 (mainParent == loadingParent || mainParent.transform.IsChildOf(loadingParent.transform)))
        {
            Debug.LogError("[ScenePilot] Loading Parent must be the dedicated download overlay and must not contain Main Parent.", this);
            issues++;
        }

        if (loadingParent != null &&
            (gameObject == loadingParent || transform.IsChildOf(loadingParent.transform)))
        {
            Debug.LogError("[ScenePilot] RemoteContentController must live outside Loading Parent so hiding the overlay cannot stop synchronization.", this);
            issues++;
        }

        AutoRegisterStaticPanels();
        if (FindStaticPanelInstance("download_loading") == null)
        {
            Debug.LogWarning("[ScenePilot] No download_loading static panel is registered.", this);
            issues++;
        }

        if (FindStaticPanelInstance("endless_loading") == null)
        {
            Debug.LogWarning("[ScenePilot] No endless_loading static panel is registered.", this);
            issues++;
        }

        ScenePilotSequenceExit sequenceExit = GetComponent<ScenePilotSequenceExit>();
        if (sequenceExit == null && (onStaticSequenceFinished == null || onStaticSequenceFinished.GetPersistentEventCount() == 0))
        {
            Debug.LogWarning("[ScenePilot] Normal sequence has no completion handler.", this);
            issues++;
        }

        if (issues == 0 && logSuccess)
        {
            Debug.Log("[ScenePilot] Connection valid. Package: " + Application.identifier + " | URL: " + configUrl, this);
        }
    }

    private static string PackageFromConfigUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri parsed))
            return string.Empty;

        string[] segments = parsed.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 2 < segments.Length; i++)
        {
            if (string.Equals(segments[i], "content", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(segments[i + 2], "remote_config.json", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(segments[i + 1]);
        }

        return string.Empty;
    }
}
