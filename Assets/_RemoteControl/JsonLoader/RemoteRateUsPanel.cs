using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RemoteStaticPanel))]
public class RemoteRateUsPanel : MonoBehaviour
{
    [Header("ScenePilot")]
    public RemoteStaticPanel staticPanel;

    [Header("Rating controls")]
    public Button[] starButtons = new Button[5];
    public Image[] starImages = new Image[5];
    public Button laterButton;
    public Button rateButton;
    public Button closeButton;
    public TextMeshProUGUI messageText;
    [Header("Local fallback labels")]
    public Image titleImage;
    public TextMeshProUGUI titleFallbackLabel;
    public TextMeshProUGUI laterFallbackLabel;
    public TextMeshProUGUI rateFallbackLabel;
    public TextMeshProUGUI closeFallbackLabel;
    [Range(0, 5)] public int selectedRating;
    public bool requireRatingSelection = true;
    public Color selectedStarColor = new Color(1f, 0.76f, 0.12f, 1f);
    public Color unselectedStarColor = new Color(0.55f, 0.57f, 0.65f, 1f);

    [Header("Store destination")]
    [Tooltip("Optional complete URL. When empty, Android uses Application.identifier and iOS uses the App Store ID below.")]
    public string overrideStoreUrl;
    [Tooltip("Numeric Apple App Store ID, without the 'id' prefix.")]
    public string appleAppId;
    public bool openStoreInEditor;

    [Header("Sequence behavior")]
    public bool advanceAfterRating = true;
    public bool advanceWhenLater = true;
    public bool advanceWhenClosed = true;
    public bool rememberSuccessfulRating = true;
    public UnityEvent<int> onRatingSelected = new UnityEvent<int>();
    public UnityEvent onStoreOpened = new UnityEvent();

    private string RatingPreferenceKey => "ScenePilot.Rated." + Application.identifier;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
        RefreshVisuals();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindButtons();
        RefreshVisuals();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    private void LateUpdate()
    {
        RefreshArtworkFallbacks();
    }

    public void SelectRating(int rating)
    {
        selectedRating = Mathf.Clamp(rating, 1, 5);
        RefreshVisuals();
        onRatingSelected.Invoke(selectedRating);
    }

    public void RateNow()
    {
        if (requireRatingSelection && selectedRating == 0)
            return;

        string url = StoreUrl();
        if (!string.IsNullOrWhiteSpace(url))
        {
#if UNITY_EDITOR
            if (openStoreInEditor)
                Application.OpenURL(url);
            else
                Debug.Log("[ScenePilot/RateUs] Store URL: " + url, this);
#else
            Application.OpenURL(url);
#endif
            if (rememberSuccessfulRating)
            {
                PlayerPrefs.SetInt(RatingPreferenceKey, selectedRating);
                PlayerPrefs.Save();
            }
            onStoreOpened.Invoke();
        }
        else
        {
            Debug.LogWarning("[ScenePilot/RateUs] No store URL is available. Assign overrideStoreUrl or an Apple App Store ID.", this);
        }

        if (advanceAfterRating)
            AdvanceSequence();
    }

    public void Later()
    {
        if (advanceWhenLater)
            AdvanceSequence();
    }

    public void Close()
    {
        if (advanceWhenClosed)
            AdvanceSequence();
    }

    private string StoreUrl()
    {
        if (!string.IsNullOrWhiteSpace(overrideStoreUrl))
            return overrideStoreUrl.Trim();

#if UNITY_IOS
        return string.IsNullOrWhiteSpace(appleAppId)
            ? string.Empty
            : "itms-apps://itunes.apple.com/app/id" + appleAppId.Trim() + "?action=write-review";
#else
        return string.IsNullOrWhiteSpace(Application.identifier)
            ? string.Empty
            : "https://play.google.com/store/apps/details?id=" + Uri.EscapeDataString(Application.identifier);
#endif
    }

    private void AdvanceSequence()
    {
        if (staticPanel == null)
            staticPanel = GetComponent<RemoteStaticPanel>();
        if (staticPanel != null)
            staticPanel.ShowNextPanel();
    }

    private void ResolveReferences()
    {
        if (staticPanel == null)
            staticPanel = GetComponent<RemoteStaticPanel>();
    }

    private void BindButtons()
    {
        UnbindButtons();
        if (starButtons.Length > 0 && starButtons[0] != null) starButtons[0].onClick.AddListener(SelectOneStar);
        if (starButtons.Length > 1 && starButtons[1] != null) starButtons[1].onClick.AddListener(SelectTwoStars);
        if (starButtons.Length > 2 && starButtons[2] != null) starButtons[2].onClick.AddListener(SelectThreeStars);
        if (starButtons.Length > 3 && starButtons[3] != null) starButtons[3].onClick.AddListener(SelectFourStars);
        if (starButtons.Length > 4 && starButtons[4] != null) starButtons[4].onClick.AddListener(SelectFiveStars);
        if (laterButton != null) laterButton.onClick.AddListener(Later);
        if (rateButton != null) rateButton.onClick.AddListener(RateNow);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void UnbindButtons()
    {
        if (starButtons.Length > 0 && starButtons[0] != null) starButtons[0].onClick.RemoveListener(SelectOneStar);
        if (starButtons.Length > 1 && starButtons[1] != null) starButtons[1].onClick.RemoveListener(SelectTwoStars);
        if (starButtons.Length > 2 && starButtons[2] != null) starButtons[2].onClick.RemoveListener(SelectThreeStars);
        if (starButtons.Length > 3 && starButtons[3] != null) starButtons[3].onClick.RemoveListener(SelectFourStars);
        if (starButtons.Length > 4 && starButtons[4] != null) starButtons[4].onClick.RemoveListener(SelectFiveStars);
        if (laterButton != null) laterButton.onClick.RemoveListener(Later);
        if (rateButton != null) rateButton.onClick.RemoveListener(RateNow);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    private void SelectOneStar() => SelectRating(1);
    private void SelectTwoStars() => SelectRating(2);
    private void SelectThreeStars() => SelectRating(3);
    private void SelectFourStars() => SelectRating(4);
    private void SelectFiveStars() => SelectRating(5);

    private void RefreshVisuals()
    {
        for (int i = 0; i < starImages.Length; i++)
        {
            if (i < starButtons.Length && starButtons[i] != null)
                starButtons[i].transform.localScale = i < selectedRating ? Vector3.one * 1.08f : Vector3.one;
            if (starImages[i] != null)
                starImages[i].color = i < selectedRating ? selectedStarColor : unselectedStarColor;
        }
        if (rateButton != null)
            rateButton.interactable = !requireRatingSelection || selectedRating > 0;
        if (messageText != null)
            messageText.alpha = 1f;
        RefreshArtworkFallbacks();
    }

    private void RefreshArtworkFallbacks()
    {
        SetFallbackVisibility(titleImage, titleFallbackLabel);
        SetFallbackVisibility(laterButton != null ? laterButton.targetGraphic as Image : null, laterFallbackLabel);
        SetFallbackVisibility(rateButton != null ? rateButton.targetGraphic as Image : null, rateFallbackLabel);
        SetFallbackVisibility(closeButton != null ? closeButton.targetGraphic as Image : null, closeFallbackLabel);
    }

    private static void SetFallbackVisibility(Image artwork, TextMeshProUGUI fallback)
    {
        if (fallback != null)
            fallback.enabled = artwork == null || artwork.sprite == null;
    }
}
