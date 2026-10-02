using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RemoteStaticAdInteraction : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    public enum AdAction
    {
        None,
        Interstitial,
        Rewarded
    }

    [Header("STATE - READ ONLY")]
    [SerializeField] private string panelId;
    [SerializeField] private string elementKey;
    [SerializeField] private AdAction action;

    private Button targetButton;
    private Toggle targetToggle;
    private bool buttonListenerRegistered;
    private bool managerWarningShown;

    public void Configure(string newPanelId, string newElementKey, string newAction)
    {
        panelId = newPanelId ?? string.Empty;
        elementKey = newElementKey ?? string.Empty;
        action = ParseAction(newAction);
        ResolveTargets();
        BindButtonOnce();
        managerWarningShown = false;
    }

    private void Awake()
    {
        ResolveTargets();
        BindButtonOnce();
    }

    private void OnDestroy()
    {
        if (targetButton != null && buttonListenerRegistered)
            targetButton.onClick.RemoveListener(HandleButtonClick);
        buttonListenerRegistered = false;
    }

    private void ResolveTargets()
    {
        if (targetButton == null)
            targetButton = GetComponent<Button>();
        if (targetToggle == null)
            targetToggle = GetComponent<Toggle>();
    }

    private void BindButtonOnce()
    {
        if (targetButton == null || buttonListenerRegistered)
            return;

        targetButton.onClick.AddListener(HandleButtonClick);
        buttonListenerRegistered = true;
    }

    private void HandleButtonClick()
    {
        RequestConfiguredAd();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Button.onClick already covers Buttons. This path is only for genuine
        // player interaction with a Toggle, avoiding programmatic value changes.
        if (targetButton != null || targetToggle == null ||
            !targetToggle.IsActive() || !targetToggle.IsInteractable())
            return;

        RequestConfiguredAd();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (targetButton != null || targetToggle == null ||
            !targetToggle.IsActive() || !targetToggle.IsInteractable())
            return;

        RequestConfiguredAd();
    }

    private void RequestConfiguredAd()
    {
        // A Next button can deactivate its panel through an earlier onClick
        // listener. Unity still completes the current onClick invocation, so
        // do not reject this already-valid click merely because navigation
        // made this bridge inactive a moment earlier.
        if (action == AdAction.None)
            return;

        ScenePilotAdsController ads = ScenePilotAdsController.instance;
        if (ads == null)
        {
            if (!managerWarningShown)
            {
                Debug.LogWarning("[ScenePilot/Ads] No ScenePilotAdsController is available for " + PlacementName + ".", this);
                managerWarningShown = true;
            }
            return;
        }

        managerWarningShown = false;
        if (action == AdAction.Interstitial)
            ads.ShowInterstitial(PlacementName);
        else if (action == AdAction.Rewarded)
            ads.ShowRewarded(PlacementName);
    }

    private string PlacementName
    {
        get { return panelId + "." + elementKey; }
    }

    private static AdAction ParseAction(string value)
    {
        if (string.Equals(value, "interstitial", StringComparison.OrdinalIgnoreCase))
            return AdAction.Interstitial;
        if (string.Equals(value, "rewarded", StringComparison.OrdinalIgnoreCase))
            return AdAction.Rewarded;
        return AdAction.None;
    }
}
