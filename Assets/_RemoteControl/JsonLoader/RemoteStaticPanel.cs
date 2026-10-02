using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Inspector data only. RemoteContentController owns all runtime behavior.
[DisallowMultipleComponent]
public class RemoteStaticPanel : MonoBehaviour
{
    public string panelId = "main_menu";
    [Tooltip("Website template used by repeatable instances, for example card_selection or level_selection.")]
    public string panelTemplateId;
    [NonSerialized]
    public bool runtimeGenerated;
    public bool useRemoteContent = true;
    [Tooltip("Enable for download_loading. New visuals wait until this root is hidden.")]
    public bool deferWhileVisible;
    [Tooltip("Opt in to letting staticPanels[].enabled control Panel Root with the same SetActive logic used by element visibility.")]
    public bool allowRemotePanelActivation = true;
    [FormerlySerializedAs("visibilityRoot")]
    [Tooltip("The parent UI GameObject for this whole panel. Panel enabled controls only this object. Existing visibilityRoot assignments migrate automatically.")]
    public GameObject panelRoot;
    public List<Element> elements = new List<Element>();
    [Tooltip("Optional explicit Next button. When empty, the binding whose key is next_button is used automatically.")]
    public Button nextButton;

    [Serializable]
    public class Element : RemoteContentController.ImageElementBinding
    {
        public GameObject targetObject;
        public TMPro.TextMeshProUGUI targetText;
        public RectTransform targetRect;
        [Tooltip("Explicit opt-in. Layout parent must be the direct parent of targetRect.")]
        public bool allowLayout;
        public RectTransform layoutParent;
        public bool allowText = true;
        public bool allowImageProperties = true;
        public bool allowImage = true;
        public bool allowColor = true;
        public bool allowEnabled = true;
        [Tooltip("Allow this element's Button or Toggle to request the website-configured ad action.")]
        public bool allowAdAction = true;
    }

    private RemoteContentController registeredController;

    private RemoteContentController ResolveController()
    {
        if (registeredController != null)
            return registeredController;

#if UNITY_2023_1_OR_NEWER
        registeredController =
            UnityEngine.Object.FindFirstObjectByType<RemoteContentController>(
                FindObjectsInactive.Include
            );
#else
        registeredController =
            UnityEngine.Object.FindObjectOfType<RemoteContentController>(
                true
            );
#endif

        return registeredController;
    }

    private void OnEnable()
    {
        if (ResolveController() != null)
        {
            registeredController.RegisterStaticPanel(
                this
            );
        }

        BindNextButton();
    }

    private void OnDestroy()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(ShowNextPanel);
        }

        if (registeredController != null)
        {
            registeredController.UnregisterStaticPanel(
                this
            );
        }

        registeredController = null;
    }

    public void BindNextButton()
    {
        if (!RemoteContentController.IsStaticSequencePanelId(panelId))
            return;

        if (nextButton == null && elements != null)
        {
            Element next = elements.Find(item => item != null && string.Equals(item.key, "next_button", StringComparison.OrdinalIgnoreCase));
            if (next == null && (string.Equals(panelId, "main_menu", StringComparison.OrdinalIgnoreCase) || panelId.StartsWith("main_menu_", StringComparison.OrdinalIgnoreCase)))
                next = elements.Find(item => item != null && string.Equals(item.key, "play_button", StringComparison.OrdinalIgnoreCase));
            if (next != null && next.targetObject != null)
            {
                nextButton = next.targetObject.GetComponent<Button>();
                if (nextButton == null)
                    nextButton = next.targetObject.AddComponent<Button>();
            }
        }

        if (nextButton == null)
            return;

        nextButton.onClick.RemoveListener(ShowNextPanel);
        nextButton.onClick.AddListener(ShowNextPanel);
    }

    public void ShowNextPanel()
    {
        // A panel can enable before a late-created controller exists. Resolve
        // again on interaction so the button never becomes a silent no-op.
        if (ResolveController() != null)
            registeredController.ShowNextStaticPanel(this);
        else
        {
            Debug.LogError("[RemoteContent/Static] Cannot navigate because no RemoteContentController is available.", this);

            GameObject root = panelRoot != null ? panelRoot : gameObject;
            if (root.activeSelf)
                root.SetActive(false);
        }
    }

    [ContextMenu("Fill empty panel with standard element keys")]
    public void FillStandardKeys()
    {
        if (elements == null) elements = new List<Element>();
        if (elements.Count != 0) { Debug.LogWarning("Existing bindings preserved. Presets only fill an empty list.", this); return; }
        string[] keys;
        string presetId = string.IsNullOrEmpty(panelTemplateId) ? panelId : panelTemplateId;
        switch (presetId)
        {
            case "loading":
            case "download_loading":
                keys = new[] { "bg", "handle", "fill", "track", "status" }; break;
            case "card_selection":
                keys = new[] { "bg", "title", "back_button", "instruction", "card_1", "card_2", "card_3", "next_button" }; break;
            case "level_selection":
                keys = new[] { "bg", "title", "back_button", "level_01", "level_02", "level_03", "level_04", "level_05", "level_06", "level_07", "level_08", "level_09", "level_10", "next_button" }; break;
            case "main_menu":
                keys = new[] { "bg", "title", "play_button", "back_button", "settings_button", "coins", "remove_ads", "rate_us", "extra_button" }; break;
            case "rate_us":
                keys = new[] { "bg", "panel", "title", "message", "star_1", "star_2", "star_3", "star_4", "star_5", "later_button", "rate_button", "close_button" }; break;
            default: Debug.LogWarning("Choose a standard panel ID first.", this); return;
        }
        foreach (string key in keys)
            elements.Add(new Element { key = key, allowImage = key != "status" && key != "instruction" && key != "message", allowColor = true, allowEnabled = true });
        if (presetId == "download_loading") deferWhileVisible = true;
    }
}
