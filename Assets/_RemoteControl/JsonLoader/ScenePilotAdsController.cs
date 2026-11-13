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
    private bool initRequested;
    private bool levelPlayEventsBound;
    private bool staticPanelSequenceCompleted;
    private bool automaticInterstitialScheduled;
    private bool adBreakCountdownRunning;
    private float nextAutomaticInterstitialTime;
    private Coroutine adBreakCountdownCoroutine;
    private float timeScaleBeforeAdBreak = 1f;
    private bool gamePausedForAdBreak;


    // Retry state belongs to one ad object and is reset when that object changes.
    private sealed class AdLoadState
    {
        public bool loading;
        public bool showing;
        public int failures;
        public float retryAt;
    }

    private readonly AdLoadState interstitialState = new AdLoadState();
    private readonly AdLoadState rewardedState = new AdLoadState();
    private float nextReadinessCheck;
    private bool appPaused;
    public string LastInterstitialRejection { get; private set; }
    public string LastRewardedRejection { get; private set; }

    // If gameplay asks for an interstitial while it is still loading,
    // remember the request and show it immediately once LevelPlay reports ready.
    private bool pendingInterstitialShow;
    private string pendingInterstitialPlacement;
    // Optional gameplay-specific reward for the rewarded ad currently being shown.
    // It is consumed only by LevelPlay's reward callback, never by ad close.
    private Action pendingRewardCallback;

    private void RecoverFullScreenAds()
    {
        if (!isActiveAndEnabled || appPaused || !adsAllowed || !sdkInitialized)
            return;
        RequestAdLoad(false);
        RequestAdLoad(true);
    }

    private void OnApplicationPause(bool paused)
    {
        appPaused = paused;
        if (!paused) RecoverFullScreenAds();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused) RecoverFullScreenAds();
    }

    private bool FullScreenAdShowing => interstitialState.showing || rewardedState.showing;

    public bool IsFullScreenExperienceActive => FullScreenAdShowing || adBreakCountdownRunning;

    public void DeferAutomaticInterstitialAfterExternalBreak()
    {
        StopAdBreakCountdown();
        ScheduleNextAutomaticInterstitial();
    }

    private void RequestAdLoad(bool rewarded)
    {
        AdLoadState state = rewarded ? rewardedState : interstitialState;
        if (!isActiveAndEnabled || appPaused || !adsAllowed || !sdkInitialized ||
            state.loading || state.showing || Time.realtimeSinceStartup < state.retryAt)
            return;
        if (rewarded ? rewardedAd == null : interstitialAd == null)
            return;
        bool ready = rewarded ? rewardedAd.IsAdReady() : interstitialAd.IsAdReady();
        if (rewarded) rewardedReady = ready;
        else interstitialReady = ready;
        if (ready) return;

        state.loading = true;
        try
        {
            if (rewarded) rewardedAd.LoadAd();
            else interstitialAd.LoadAd();
        }
        catch (Exception exception)
        {
            ScheduleAdRetry(rewarded, exception.Message);
        }
    }

    private void ScheduleAdRetry(bool rewarded, string error)
    {
        AdLoadState state = rewarded ? rewardedState : interstitialState;
        state.loading = false;
        state.showing = false;
        if (rewarded) rewardedReady = false;
        else interstitialReady = false;
        state.failures = Math.Min(state.failures + 1, 6);
        float delay = Math.Min(60f, 2f * (float)Math.Pow(2, state.failures - 1));
        state.retryAt = Time.realtimeSinceStartup + delay;
        Debug.LogWarning("[ScenePilot/Ads] " + (rewarded ? "Rewarded" : "Interstitial") +
            " failed: " + error + ". Retry in " + delay + " seconds.", this);
    }

    private static void ResetAdState(AdLoadState state)
    {
        state.loading = false;
        state.showing = false;
        state.failures = 0;
        state.retryAt = 0f;
    }

    private string ShowRejection(bool rewarded, string placementName)
    {
        if (!isActiveAndEnabled || appPaused) return "manager inactive or app paused";
        if (!adsAllowed) return "ads disabled by configuration";
        if (!sdkInitialized) return "SDK not initialized";
        if (rewarded ? rewardedAd == null : interstitialAd == null) return "ad unit disabled or missing";
        if (FullScreenAdShowing) return "another fullscreen ad is opening or showing";
        if (!PlacementAllowed(placementName)) return "placement disallowed by server";
        if (!string.IsNullOrWhiteSpace(placementName) &&
            (rewarded ? LevelPlayRewardedAd.IsPlacementCapped(placementName) :
                        LevelPlayInterstitialAd.IsPlacementCapped(placementName))) return "SDK placement capped";
        if (rewarded ? !IsRewardedReady() : !IsInterstitialReady())
            return (rewarded ? rewardedState.loading : interstitialState.loading) ? "ad loading" : "ad not ready";
        return null;
    }

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
        {
            remoteContent.ConfigApplied += ApplyRemoteConfig;
            remoteContent.onStaticSequenceFinished.AddListener(HandleStaticPanelSequenceFinished);
            if (remoteContent.ActiveConfig != null)
                ApplyRemoteConfig(remoteContent.ActiveConfig);
            if (remoteContent.IsStaticPanelSequenceFinished)
                HandleStaticPanelSequenceFinished();
        }
    }

    private void Start()
    {
        if (remoteContent != null && remoteContent.ActiveConfig != null)
            ApplyRemoteConfig(remoteContent.ActiveConfig);
        ShowBanner();
    }

    private void OnDisable()
    {
        StopAdBreakCountdown();
        if (remoteContent != null)
        {
            remoteContent.ConfigApplied -= ApplyRemoteConfig;
            remoteContent.onStaticSequenceFinished.RemoveListener(HandleStaticPanelSequenceFinished);
        }
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
        if (Time.realtimeSinceStartup >= nextReadinessCheck)
        {
            nextReadinessCheck = Time.realtimeSinceStartup + 1f;
            RecoverFullScreenAds();
        }
        // Recover completion if this manager was enabled after the finish event.
        if (!staticPanelSequenceCompleted && remoteContent != null && remoteContent.IsStaticPanelSequenceFinished)
            HandleStaticPanelSequenceFinished();

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
        string reason = ShowRejection(false, placementName);
        LastInterstitialRejection = reason;

        if (reason != null)
        {
            Log("Interstitial request rejected (" + (placementName ?? "default") + "): " + reason);

            // A button/request may arrive just before LevelPlay finishes loading.
            // Keep that request instead of losing the click.
            if (reason == "ad loading" || reason == "ad not ready")
            {
                pendingInterstitialShow = true;
                pendingInterstitialPlacement = placementName;
                RequestAdLoad(false);
                Log("Interstitial request queued until the ad is ready.");
            }

            return false;
        }

        // We are about to satisfy any queued request.
        pendingInterstitialShow = false;
        pendingInterstitialPlacement = null;

        // Claim the show before calling the SDK; callbacks may arrive later.
        interstitialState.showing = true;
        interstitialReady = false;
        try
        {
            interstitialAd.ShowAd(placementName);
            return true;
        }
        catch (Exception exception)
        {
            LastInterstitialRejection = "SDK show exception: " + exception.Message;
            ScheduleAdRetry(false, exception.Message);
            return false;
        }
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
        string reason = ShowRejection(true, placementName);
        LastRewardedRejection = reason;
        if (reason != null)
        {
            Log("Rewarded request rejected (" + (placementName ?? "default") + "): " + reason);
            RequestAdLoad(true);
            return false;
        }

        // Claim the show before calling the SDK; callbacks may arrive later.
        rewardedState.showing = true;
        rewardedReady = false;
        try
        {
            rewardedAd.ShowAd(placementName);
            return true;
        }
        catch (Exception exception)
        {
            LastRewardedRejection = "SDK show exception: " + exception.Message;
            ScheduleAdRetry(true, exception.Message);
            return false;
        }
    }

    // Inspector/Button-friendly wrapper.
    public void ShowRewardedNow()
    {
        ShowRewarded();
    }

    // Public preload hook used by gameplay reward panels.
    public void LoadRewardedNow()
    {
        RequestAdLoad(true);
    }

    // Shows one rewarded ad and invokes this request's callback only after
    // LevelPlay confirms that the player earned the reward.
    public bool ShowRewardedFor(Action rewardCallback, string placementName = null)
    {
        if (rewardCallback == null)
        {
            LastRewardedRejection = "reward callback is missing";
            return false;
        }

        if (pendingRewardCallback != null)
        {
            LastRewardedRejection = "another rewarded request is pending";
            return false;
        }

        // Assign before ShowAd in case an SDK implementation invokes callbacks
        // synchronously. Clear it again when the show request is rejected.
        pendingRewardCallback = rewardCallback;
        if (ShowRewarded(placementName))
            return true;

        pendingRewardCallback = null;
        return false;
    }

    public bool IsInterstitialReady()
    {
        interstitialReady = adsAllowed && sdkInitialized && interstitialAd != null &&
            !interstitialState.loading && !FullScreenAdShowing && interstitialAd.IsAdReady();
        return interstitialReady;
    }

    public bool IsRewardedReady()
    {
        rewardedReady = adsAllowed && sdkInitialized && rewardedAd != null &&
            !rewardedState.loading && !FullScreenAdShowing && rewardedAd.IsAdReady();
        return rewardedReady;
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
        var source = interstitialAd;
        interstitialAd.OnAdLoaded += (info) => { if (ReferenceEquals(interstitialAd, source)) HandleInterstitialLoaded(info); };
        interstitialAd.OnAdLoadFailed += (error) => { if (ReferenceEquals(interstitialAd, source)) HandleInterstitialLoadFailed(error); };
        interstitialAd.OnAdDisplayed += (info) => { if (ReferenceEquals(interstitialAd, source)) HandleInterstitialDisplayed(info); };
        interstitialAd.OnAdDisplayFailed += (info, error) => { if (ReferenceEquals(interstitialAd, source)) HandleInterstitialDisplayFailed(info, error); };
        interstitialAd.OnAdClosed += (info) => { if (ReferenceEquals(interstitialAd, source)) HandleInterstitialClosed(info); };
        RequestAdLoad(false);
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
        var source = rewardedAd;
        rewardedAd.OnAdDisplayed += (info) => { if (ReferenceEquals(rewardedAd, source)) HandleRewardedDisplayed(info); };
        rewardedAd.OnAdLoaded += (info) => { if (ReferenceEquals(rewardedAd, source)) HandleRewardedLoaded(info); };
        rewardedAd.OnAdLoadFailed += (error) => { if (ReferenceEquals(rewardedAd, source)) HandleRewardedLoadFailed(error); };
        rewardedAd.OnAdDisplayFailed += (info, error) => { if (ReferenceEquals(rewardedAd, source)) HandleRewardedDisplayFailed(info, error); };
        rewardedAd.OnAdRewarded += (info, reward) => { if (ReferenceEquals(rewardedAd, source)) HandleRewardGranted(info, reward); };
        rewardedAd.OnAdClosed += (info) => { if (ReferenceEquals(rewardedAd, source)) HandleRewardedClosed(info); };
        RequestAdLoad(true);
    }

    private bool CanShowInterstitial(string placementName)
    {
        // Automatic timing belongs to the scheduler, never to button requests.
        return ShowRejection(false, placementName) == null;
    }

    private bool CanRunAutomaticInterstitials()
    {
        RemoteContentController.RemoteAdsRulesData rules = Rules;
        return staticPanelSequenceCompleted &&
               !ScenePilotNativeReviewController.IsBlockingAutomaticInterstitials &&
               adsAllowed &&
               isActiveAndEnabled &&
               !appPaused &&
               !FullScreenAdShowing &&
               sdkInitialized &&
               interstitialAd != null &&
               rules.minimumSecondsBetweenInterstitials > 0f;
    }

    private string AutomaticInterstitialPlacement =>
        string.IsNullOrWhiteSpace(automaticInterstitialPlacementName)
            ? null
            : automaticInterstitialPlacementName;

    private void HandleStaticPanelSequenceFinished()
    {
        if (staticPanelSequenceCompleted)
            return;

        staticPanelSequenceCompleted = true;
        ScheduleNextAutomaticInterstitial();
        Log("Static panels finished; automatic ad-break timer may now start.");
    }

    private void ScheduleNextAutomaticInterstitial()
    {
        if (!staticPanelSequenceCompleted)
        {
            automaticInterstitialScheduled = false;
            return;
        }

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
        pendingInterstitialShow = false;
        pendingInterstitialPlacement = null;
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
        pendingInterstitialShow = false;
        pendingInterstitialPlacement = null;
        var old = interstitialAd;
        interstitialAd = null;
        interstitialUnitId = null;
        interstitialReady = false;
        ResetAdState(interstitialState);
        old?.DestroyAd();
    }

    private void DestroyRewarded()
    {
        pendingRewardCallback = null;
        var old = rewardedAd;
        rewardedAd = null;
        rewardedUnitId = null;
        rewardedReady = false;
        ResetAdState(rewardedState);
        old?.DestroyAd();
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
        interstitialState.loading = false;
        interstitialState.failures = 0;
        interstitialState.retryAt = 0f;
        interstitialReady = true;
        Log("Interstitial loaded.");

        // If the player already requested an interstitial while it was loading,
        // consume that request immediately now that the ad is ready.
        if (pendingInterstitialShow)
        {
            string placement = pendingInterstitialPlacement;
            pendingInterstitialShow = false;
            pendingInterstitialPlacement = null;

            Log("Showing queued interstitial now that it is ready.");
            ShowInterstitial(placement);
        }
    }

    private void HandleInterstitialLoadFailed(LevelPlayAdError error)
    {
        ScheduleAdRetry(false, "load: " + error);
    }

    private void HandleInterstitialDisplayed(LevelPlayAdInfo info)
    {
        StopAdBreakCountdown();
        interstitialReady = false;
        interstitialState.showing = true;
        interstitialsThisSession++;
        ScheduleNextAutomaticInterstitial();
    }

    private void HandleInterstitialDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        ScheduleAdRetry(false, "display: " + error);
    }

    private void HandleInterstitialClosed(LevelPlayAdInfo info)
    {
        interstitialState.showing = false;
        interstitialReady = false;
        RequestAdLoad(false);
        // A gameplay listener must not prevent replacement loading.
        onInterstitialClosed?.Invoke();
    }

    private void HandleRewardedLoaded(LevelPlayAdInfo info)
    {
        rewardedState.loading = false;
        rewardedState.failures = 0;
        rewardedState.retryAt = 0f;
        rewardedReady = true;
        Log("Rewarded loaded.");
    }

    private void HandleRewardedLoadFailed(LevelPlayAdError error)
    {
        ScheduleAdRetry(true, "load: " + error);
    }

    private void HandleRewardedDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        pendingRewardCallback = null;
        ScheduleAdRetry(true, "display: " + error);
    }

    private void HandleRewardedDisplayed(LevelPlayAdInfo info)
    {
        rewardedState.showing = true;
        rewardedReady = false;
        StopAdBreakCountdown();
        ScheduleNextAutomaticInterstitial();
    }

    private void HandleRewardGranted(LevelPlayAdInfo info, LevelPlayReward reward)
    {
        Action callback = pendingRewardCallback;
        pendingRewardCallback = null;

        if (callback != null)
        {
            try
            {
                callback.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        RemoteContentController.RemoteAdsRulesData rules = Rules;
        onRewardGranted?.Invoke(rules.rewardItemName ?? string.Empty, Mathf.Max(0, rules.rewardAmount));
    }

    private void HandleRewardedClosed(LevelPlayAdInfo info)
    {
        // Closing without OnAdRewarded must never grant the gameplay reward.
        pendingRewardCallback = null;
        rewardedState.showing = false;
        rewardedReady = false;
        RequestAdLoad(true);
        // A gameplay listener must not prevent replacement loading.
        onRewardedClosed?.Invoke();
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

