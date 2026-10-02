using System;
using System.Collections;
using System.Linq;
using TMPro;
using Unity.Services.LevelPlay;
using UnityEngine;
using UnityEngine.Events;

[DefaultExecutionOrder(10001)]
[DisallowMultipleComponent]
public sealed class ScenePilotAdsController : MonoBehaviour
{
    [Serializable]
    public sealed class RewardEvent : UnityEvent<string, int> { }

    [Header("SCENEPILOT SOURCE")]
    public RemoteContentController remoteContent;

    [Header("EDITOR")]
    [Tooltip("LevelPlay uses mock ads in the Unity Editor. Leave disabled when website values are placeholders.")]
    public bool initializeInEditor;
    public bool debugLogs = true;

    [Header("AD BREAK COUNTDOWN")]
    [Tooltip("Keep this panel under the persistent AdsManager so it survives scene changes.")]
    public GameObject adBreakPanel;
    public TextMeshProUGUI adBreakCountdownText;
    public string adBreakCountdownFormat = "Ad break in {0}";
    [Tooltip("Optional LevelPlay placement. Leave empty to use the default placement.")]
    public string automaticInterstitialPlacementName;

    [Header("STATE - READ ONLY")]
    public bool adsAllowed;
    public bool sdkInitialized;
    public bool bannerReady;
    public bool interstitialReady;
    public bool rewardedReady;
    public int completedLevels;
    public int interstitialsThisSession;

    [Header("EVENTS")]
    public UnityEvent onInitialized = new UnityEvent();
    public UnityEvent onInitializationFailed = new UnityEvent();
    public UnityEvent onInterstitialClosed = new UnityEvent();
    public UnityEvent onRewardedClosed = new UnityEvent();
    public RewardEvent onRewardGranted = new RewardEvent();

    private RemoteContentController.RemoteAdsData adsConfig;
    private RemoteContentController.RemoteAdsPlatformData platformConfig;
    private LevelPlayBannerAd bannerAd;
    private LevelPlayInterstitialAd interstitialAd;
    private LevelPlayRewardedAd rewardedAd;
    private string initializedAppKey;
    private string bannerUnitId;
    private string bannerPositionKey;
    private string interstitialUnitId;
    private string rewardedUnitId;
    private float lastInterstitialRealtime = float.NegativeInfinity;
    private bool initRequested;
    private bool levelPlayEventsBound;
    private bool automaticInterstitialScheduled;
    private bool adBreakCountdownRunning;
    private float nextAutomaticInterstitialTime;
    private Coroutine adBreakCountdownCoroutine;
    private float timeScaleBeforeAdBreak = 1f;
    private bool gamePausedForAdBreak;


    public static ScenePilotAdsController instance;
    private void Reset()
    {
        remoteContent = GetComponent<RemoteContentController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        ResolveController();
        SetAdBreakPanelVisible(false);
    }

    private void OnEnable()
    {
        ResolveController();
        if (remoteContent != null)
            remoteContent.ConfigApplied += ApplyRemoteConfig;
    }

    private void Start()
    {
        if (remoteContent != null && remoteContent.ActiveConfig != null)
            ApplyRemoteConfig(remoteContent.ActiveConfig);
        ShowBanner();
    }

    private void OnDisable()
    {
        if (remoteContent != null)
            remoteContent.ConfigApplied -= ApplyRemoteConfig;
    }

    private void OnDestroy()
    {
        StopAdBreakCountdown();
        if (instance == this)
            instance = null;
        UnbindLevelPlayEvents();
        DestroyAdObjects();
    }

    private void Update()
    {
        if (!CanRunAutomaticInterstitials())
            return;

        if (!automaticInterstitialScheduled)
        {
            ScheduleNextAutomaticInterstitial();
            return;
        }

        if (adBreakCountdownRunning || Time.unscaledTime < nextAutomaticInterstitialTime)
            return;

        if (CanShowInterstitial(AutomaticInterstitialPlacement))
            adBreakCountdownCoroutine = StartCoroutine(ShowAutomaticInterstitialAfterCountdown());
    }

    public void ApplyRemoteConfig(RemoteContentController.RemoteConfigData config)
    {
        adsConfig = config != null ? config.ads : null;
        platformConfig = SelectPlatform(adsConfig);
        adsAllowed = IsAdsAllowed(config, adsConfig, platformConfig);

        if (!adsAllowed)
        {
            DisableAdsImmediately();
            Log("Ads disabled by remote config, platform setting, or kill switch.");
            return;
        }

#if UNITY_EDITOR
        if (!initializeInEditor)
        {
            DisableAdsImmediately();
            Log("Ads config applied. Editor initialization is disabled.");
            return;
        }
#endif

        LevelPlay.SetAdaptersDebug(adsConfig.testMode);
        BindLevelPlayEvents();

        if (sdkInitialized)
        {
            if (!string.Equals(initializedAppKey, platformConfig.appKey, StringComparison.Ordinal))
            {
                Debug.LogWarning("[ScenePilot/Ads] App key changed after LevelPlay initialized. Restart the app to use the new key.", this);
                return;
            }

            BuildEnabledAdObjects();
            ScheduleNextAutomaticInterstitial();
            return;
        }

        if (initRequested)
            return;

        initRequested = true;
        initializedAppKey = platformConfig.appKey;
        Log("Initializing LevelPlay from the active ScenePilot config.");
        LevelPlay.Init(initializedAppKey);
    }

    public void SetGdprConsent(bool consent)
    {
        LevelPlayPrivacySettings.SetGDPRConsent(consent);
    }

    public void SetCcpaOptOut(bool optedOut)
    {
        LevelPlayPrivacySettings.SetCCPA(optedOut);
    }

    public void SetCoppa(bool isChild)
    {
        LevelPlayPrivacySettings.SetCOPPA(isChild);
    }

    public void LaunchTestSuite()
    {
        if (sdkInitialized)
            LevelPlay.LaunchTestSuite();
    }

    public void ShowBanner()
    {
        if (!adsAllowed || bannerAd == null)
            return;

        if (bannerReady)
            bannerAd.ShowAd();
        else
            bannerAd.LoadAd();
    }

    public void HideBanner()
    {
        bannerAd?.HideAd();
    }

    public bool ShowInterstitial(string placementName = null)
    {
        if (!CanShowInterstitial(placementName))
        {
            if (adsAllowed && interstitialAd != null && !interstitialReady)
                interstitialAd.LoadAd();
            return false;
        }

        interstitialAd.ShowAd(placementName);
        return true;
    }

    // Inspector/Button-friendly wrapper.
    public void ShowInterstitialNow()
    {
        ShowInterstitial();
    }

    public bool RegisterLevelCompleted(string placementName = "level_end")
    {
        completedLevels++;
        RemoteContentController.RemoteAdsRulesData rules = Rules;
        int every = rules.interstitialEveryLevels;
        if (every <= 0)
            return false;
        if (completedLevels % every != 0)
            return false;

        return ShowInterstitial(placementName);
    }

    // Call this from a level-complete UnityEvent.
    public void NotifyLevelCompleted()
    {
        RegisterLevelCompleted("level_end");
    }

    public bool ShowRewarded(string placementName = null)
    {
        if (!adsAllowed || rewardedAd == null || !rewardedReady || !PlacementAllowed(placementName))
        {
            if (adsAllowed && rewardedAd != null && !rewardedReady)
                rewardedAd.LoadAd();
            return false;
        }

        rewardedAd.ShowAd(placementName);
        return true;
    }

    // Inspector/Button-friendly wrapper.
    public void ShowRewardedNow()
    {
        ShowRewarded();
    }

    public bool IsInterstitialReady()
    {
        return adsAllowed && interstitialAd != null && interstitialReady && interstitialAd.IsAdReady();
    }

    public bool IsRewardedReady()
    {
        return adsAllowed && rewardedAd != null && rewardedReady && rewardedAd.IsAdReady();
    }

    private RemoteContentController.RemoteAdsRulesData Rules =>
        adsConfig != null && adsConfig.rules != null
            ? adsConfig.rules
            : new RemoteContentController.RemoteAdsRulesData();

    private void ResolveController()
    {
        if (remoteContent == null)
            remoteContent = GetComponent<RemoteContentController>();
    }

    private static RemoteContentController.RemoteAdsPlatformData SelectPlatform(RemoteContentController.RemoteAdsData config)
    {
        if (config == null)
            return null;
#if UNITY_IOS && !UNITY_EDITOR
        return config.ios;
#else
        return config.android;
#endif
    }

    private static bool IsAdsAllowed(
        RemoteContentController.RemoteConfigData root,
        RemoteContentController.RemoteAdsData ads,
        RemoteContentController.RemoteAdsPlatformData platform)
    {
        if (root == null || !root.enabled || ads == null || !ads.enabled || ads.killSwitch || platform == null || !platform.enabled)
            return false;
        if (!string.IsNullOrWhiteSpace(ads.provider) && !string.Equals(ads.provider, "levelplay", StringComparison.OrdinalIgnoreCase))
            return false;
        return !string.IsNullOrWhiteSpace(platform.appKey);
    }

    private void BindLevelPlayEvents()
    {
        if (levelPlayEventsBound)
            return;
        LevelPlay.OnInitSuccess += HandleInitializationSuccess;
        LevelPlay.OnInitFailed += HandleInitializationFailed;
        levelPlayEventsBound = true;
    }

    private void UnbindLevelPlayEvents()
    {
        if (!levelPlayEventsBound)
            return;
        LevelPlay.OnInitSuccess -= HandleInitializationSuccess;
        LevelPlay.OnInitFailed -= HandleInitializationFailed;
        levelPlayEventsBound = false;
    }

    private void HandleInitializationSuccess(LevelPlayConfiguration configuration)
    {
        initRequested = false;
        sdkInitialized = true;
        Log("LevelPlay initialized.");
        BuildEnabledAdObjects();
        ScheduleNextAutomaticInterstitial();
        onInitialized?.Invoke();
    }

    private void HandleInitializationFailed(LevelPlayInitError error)
    {
        initRequested = false;
        sdkInitialized = false;
        Debug.LogError("[ScenePilot/Ads] LevelPlay initialization failed: " + error, this);
        onInitializationFailed?.Invoke();
    }

    private void BuildEnabledAdObjects()
    {
        if (!adsAllowed || platformConfig == null)
            return;

        BuildBanner();
        BuildInterstitial();
        BuildRewarded();
    }

    private void BuildBanner()
    {
        RemoteContentController.RemoteBannerData data = platformConfig.banner;
        if (data == null || !data.enabled || string.IsNullOrWhiteSpace(data.adUnitId))
        {
            DestroyBanner();
            return;
        }
        string positionKey = (data.position ?? "bottom_center").Trim().ToLowerInvariant();
        if (bannerAd != null &&
            string.Equals(bannerUnitId, data.adUnitId, StringComparison.Ordinal) &&
            string.Equals(bannerPositionKey, positionKey, StringComparison.Ordinal))
            return;

        DestroyBanner();
        bannerUnitId = data.adUnitId;
        bannerPositionKey = positionKey;
        LevelPlayBannerAd.Config bannerConfig = new LevelPlayBannerAd.Config.Builder()
            .SetPosition(ParseBannerPosition(data.position))
            .Build();
        bannerAd = new LevelPlayBannerAd(bannerUnitId, bannerConfig);
        bannerAd.OnAdLoaded += HandleBannerLoaded;
        bannerAd.OnAdLoadFailed += HandleBannerLoadFailed;
        bannerAd.OnAdDisplayFailed += HandleBannerDisplayFailed;
        bannerAd.LoadAd();
    }

    private void BuildInterstitial()
    {
        RemoteContentController.RemoteAdUnitData data = platformConfig.interstitial;
        if (data == null || !data.enabled || string.IsNullOrWhiteSpace(data.adUnitId))
        {
            DestroyInterstitial();
            return;
        }
        if (interstitialAd != null && string.Equals(interstitialUnitId, data.adUnitId, StringComparison.Ordinal))
            return;

        DestroyInterstitial();
        interstitialUnitId = data.adUnitId;
        interstitialAd = new LevelPlayInterstitialAd(interstitialUnitId);
        interstitialAd.OnAdLoaded += HandleInterstitialLoaded;
        interstitialAd.OnAdLoadFailed += HandleInterstitialLoadFailed;
        interstitialAd.OnAdDisplayed += HandleInterstitialDisplayed;
        interstitialAd.OnAdDisplayFailed += HandleInterstitialDisplayFailed;
        interstitialAd.OnAdClosed += HandleInterstitialClosed;
        interstitialAd.LoadAd();
    }

    private void BuildRewarded()
    {
        RemoteContentController.RemoteAdUnitData data = platformConfig.rewarded;
        if (data == null || !data.enabled || string.IsNullOrWhiteSpace(data.adUnitId))
        {
            DestroyRewarded();
            return;
        }
        if (rewardedAd != null && string.Equals(rewardedUnitId, data.adUnitId, StringComparison.Ordinal))
            return;

        DestroyRewarded();
        rewardedUnitId = data.adUnitId;
        rewardedAd = new LevelPlayRewardedAd(rewardedUnitId);
        rewardedAd.OnAdLoaded += HandleRewardedLoaded;
        rewardedAd.OnAdLoadFailed += HandleRewardedLoadFailed;
        rewardedAd.OnAdDisplayFailed += HandleRewardedDisplayFailed;
        rewardedAd.OnAdRewarded += HandleRewardGranted;
        rewardedAd.OnAdClosed += HandleRewardedClosed;
        rewardedAd.LoadAd();
    }

    private bool CanShowInterstitial(string placementName)
    {
        if (!adsAllowed || interstitialAd == null || !interstitialReady || !interstitialAd.IsAdReady() || !PlacementAllowed(placementName))
            return false;

        RemoteContentController.RemoteAdsRulesData rules = Rules;
        if (rules.maximumInterstitialsPerSession >= 0 && interstitialsThisSession >= rules.maximumInterstitialsPerSession)
            return false;
        return Time.realtimeSinceStartup - lastInterstitialRealtime >= Mathf.Max(0f, rules.minimumSecondsBetweenInterstitials);
    }

    private bool CanRunAutomaticInterstitials()
    {
        RemoteContentController.RemoteAdsRulesData rules = Rules;
        return adsAllowed &&
               sdkInitialized &&
               interstitialAd != null &&
               rules.minimumSecondsBetweenInterstitials > 0f &&
               (rules.maximumInterstitialsPerSession < 0 ||
                interstitialsThisSession < rules.maximumInterstitialsPerSession);
    }

    private string AutomaticInterstitialPlacement =>
        string.IsNullOrWhiteSpace(automaticInterstitialPlacementName)
            ? null
            : automaticInterstitialPlacementName;

    private void ScheduleNextAutomaticInterstitial()
    {
        float interval = Mathf.Max(0f, Rules.minimumSecondsBetweenInterstitials);
        automaticInterstitialScheduled = interval > 0f;
        nextAutomaticInterstitialTime = Time.unscaledTime + interval;
    }

    private IEnumerator ShowAutomaticInterstitialAfterCountdown()
    {
        adBreakCountdownRunning = true;
        PauseGameForAdBreak();
        SetAdBreakPanelVisible(true);

        float remaining = Mathf.Max(0f, Rules.interstitialCountdownSeconds);
        while (remaining > 0f)
        {
            UpdateAdBreakCountdownText(Mathf.CeilToInt(remaining));
            yield return null;

            if (!CanRunAutomaticInterstitials())
            {
                adBreakCountdownRunning = false;
                adBreakCountdownCoroutine = null;
                SetAdBreakPanelVisible(false);
                RestoreGameAfterAdBreak();
                yield break;
            }

            remaining -= Time.unscaledDeltaTime;
        }

        SetAdBreakPanelVisible(false);
        adBreakCountdownRunning = false;
        adBreakCountdownCoroutine = null;
        RestoreGameAfterAdBreak();

        // Schedule immediately so a delayed SDK callback cannot start a
        // second countdown while this interstitial is opening.
        ScheduleNextAutomaticInterstitial();
        ShowInterstitial(AutomaticInterstitialPlacement);
    }

    private void UpdateAdBreakCountdownText(int seconds)
    {
        if (adBreakCountdownText == null)
            return;

        string format = string.IsNullOrWhiteSpace(adBreakCountdownFormat)
            ? "Ad break in {0}"
            : adBreakCountdownFormat;
        adBreakCountdownText.text = string.Format(format, Mathf.Max(0, seconds));
    }

    private void SetAdBreakPanelVisible(bool visible)
    {
        if (adBreakPanel != null)
            adBreakPanel.SetActive(visible);
    }

    private void PauseGameForAdBreak()
    {
        if (gamePausedForAdBreak)
            return;

        timeScaleBeforeAdBreak = Time.timeScale;
        Time.timeScale = 0f;
        gamePausedForAdBreak = true;
    }

    private void RestoreGameAfterAdBreak()
    {
        if (!gamePausedForAdBreak)
            return;

        Time.timeScale = timeScaleBeforeAdBreak;
        gamePausedForAdBreak = false;
    }

    private void StopAdBreakCountdown()
    {
        if (adBreakCountdownCoroutine != null)
        {
            StopCoroutine(adBreakCountdownCoroutine);
            adBreakCountdownCoroutine = null;
        }

        adBreakCountdownRunning = false;
        SetAdBreakPanelVisible(false);
        RestoreGameAfterAdBreak();
    }

    private bool PlacementAllowed(string placementName)
    {
        string[] allowed = Rules.placementNames;
        if (allowed == null || allowed.Length == 0 || string.IsNullOrWhiteSpace(placementName))
            return true;
        return allowed.Any(value => string.Equals(value, placementName, StringComparison.OrdinalIgnoreCase));
    }

    private void DisableAdsImmediately()
    {
        automaticInterstitialScheduled = false;
        StopAdBreakCountdown();
        if (bannerAd != null)
            bannerAd.HideAd();
        DestroyAdObjects();
        bannerReady = false;
        interstitialReady = false;
        rewardedReady = false;
    }

    private void DestroyAdObjects()
    {
        DestroyBanner();
        DestroyInterstitial();
        DestroyRewarded();
    }

    private void DestroyBanner()
    {
        if (bannerAd != null)
            bannerAd.DestroyAd();
        bannerAd = null;
        bannerUnitId = null;
        bannerPositionKey = null;
        bannerReady = false;
    }

    private void DestroyInterstitial()
    {
        if (interstitialAd != null)
            interstitialAd.DestroyAd();
        interstitialAd = null;
        interstitialUnitId = null;
        interstitialReady = false;
    }

    private void DestroyRewarded()
    {
        if (rewardedAd != null)
            rewardedAd.DestroyAd();
        rewardedAd = null;
        rewardedUnitId = null;
        rewardedReady = false;
    }

    private void HandleBannerLoaded(LevelPlayAdInfo info)
    {
        bannerReady = true;
        bannerAd?.ShowAd();
    }

    private void HandleBannerLoadFailed(LevelPlayAdError error)
    {
        bannerReady = false;
        Debug.LogWarning("[ScenePilot/Ads] Banner load failed: " + error, this);
    }

    private void HandleBannerDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        bannerReady = false;
        Debug.LogWarning("[ScenePilot/Ads] Banner display failed: " + error, this);
    }

    private void HandleInterstitialLoaded(LevelPlayAdInfo info)
    {
        interstitialReady = true;
    }

    private void HandleInterstitialLoadFailed(LevelPlayAdError error)
    {
        interstitialReady = false;
        Debug.LogWarning("[ScenePilot/Ads] Interstitial load failed: " + error, this);
    }

    private void HandleInterstitialDisplayed(LevelPlayAdInfo info)
    {
        StopAdBreakCountdown();
        interstitialReady = false;
        interstitialsThisSession++;
        lastInterstitialRealtime = Time.realtimeSinceStartup;
        ScheduleNextAutomaticInterstitial();
    }

    private void HandleInterstitialDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        interstitialReady = false;
        Debug.LogWarning("[ScenePilot/Ads] Interstitial display failed: " + error, this);
        interstitialAd?.LoadAd();
    }

    private void HandleInterstitialClosed(LevelPlayAdInfo info)
    {
        onInterstitialClosed?.Invoke();
        if (adsAllowed)
            interstitialAd?.LoadAd();
    }

    private void HandleRewardedLoaded(LevelPlayAdInfo info)
    {
        rewardedReady = true;
    }

    private void HandleRewardedLoadFailed(LevelPlayAdError error)
    {
        rewardedReady = false;
        Debug.LogWarning("[ScenePilot/Ads] Rewarded load failed: " + error, this);
    }

    private void HandleRewardedDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        rewardedReady = false;
        Debug.LogWarning("[ScenePilot/Ads] Rewarded display failed: " + error, this);
        rewardedAd?.LoadAd();
    }

    private void HandleRewardGranted(LevelPlayAdInfo info, LevelPlayReward reward)
    {
        RemoteContentController.RemoteAdsRulesData rules = Rules;
        onRewardGranted?.Invoke(rules.rewardItemName ?? string.Empty, Mathf.Max(0, rules.rewardAmount));
    }

    private void HandleRewardedClosed(LevelPlayAdInfo info)
    {
        rewardedReady = false;
        onRewardedClosed?.Invoke();
        if (adsAllowed)
            rewardedAd?.LoadAd();
    }

    private static LevelPlayBannerPosition ParseBannerPosition(string value)
    {
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "top_left": return LevelPlayBannerPosition.TopLeft;
            case "top_center": return LevelPlayBannerPosition.TopCenter;
            case "top_right": return LevelPlayBannerPosition.TopRight;
            case "center_left": return LevelPlayBannerPosition.CenterLeft;
            case "center": return LevelPlayBannerPosition.Center;
            case "center_right": return LevelPlayBannerPosition.CenterRight;
            case "bottom_left": return LevelPlayBannerPosition.BottomLeft;
            case "bottom_right": return LevelPlayBannerPosition.BottomRight;
            default: return LevelPlayBannerPosition.BottomCenter;
        }
    }

    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log("[ScenePilot/Ads] " + message, this);
    }
}
