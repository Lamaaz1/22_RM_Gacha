using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RemoteStaticUrlInteraction : MonoBehaviour
{
    [Header("STATE - READ ONLY")]
    [SerializeField] private string panelId;
    [SerializeField] private string elementKey;
    [SerializeField] private string remoteUrl;
    [SerializeField] private bool openUrlInEditor = true;

    private Button targetButton;
    private bool listenerRegistered;

    public void Configure(string newPanelId, string newElementKey, string newUrl)
    {
        panelId = newPanelId ?? string.Empty;
        elementKey = newElementKey ?? string.Empty;
        remoteUrl = SafeHttpsUrl(newUrl);
        ResolveButton();
        BindOnce();
    }

    private void Awake()
    {
        ResolveButton();
        BindOnce();
    }

    private void OnDestroy()
    {
        if (targetButton != null && listenerRegistered)
            targetButton.onClick.RemoveListener(OpenConfiguredUrl);
        listenerRegistered = false;
    }

    private void ResolveButton()
    {
        if (targetButton == null)
            targetButton = GetComponent<Button>();
    }

    private void BindOnce()
    {
        if (targetButton == null || listenerRegistered)
            return;

        targetButton.onClick.AddListener(OpenConfiguredUrl);
        listenerRegistered = true;
    }

    private void OpenConfiguredUrl()
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
            return;

#if UNITY_EDITOR
        if (openUrlInEditor)
            Application.OpenURL(remoteUrl);
        else
            Debug.Log("[ScenePilot/URL] " + PlacementName + " -> " + remoteUrl, this);
#else
        Application.OpenURL(remoteUrl);
#endif
    }

    private string PlacementName
    {
        get { return panelId + "." + elementKey; }
    }

    private static string SafeHttpsUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        Uri uri;
        string trimmed = value.Trim();
        return Uri.TryCreate(trimmed, UriKind.Absolute, out uri) &&
               string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : string.Empty;
    }
}
