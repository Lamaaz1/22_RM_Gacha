using System.Collections;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.Review;
#endif

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class ScenePilotNativeReviewController : MonoBehaviour
{
    [HideInInspector]
    public RemoteContentController remoteContent;

    [Header("NATIVE REVIEW")]
    [Tooltip("Enable the one-time Android/iOS native review request.")]
    public bool nativeReviewEnabled = true;

    [Tooltip("Active gameplay time required after the static-panel sequence finishes.")]
    [Min(0f)] public float reviewDelaySeconds = 260f;

    [HideInInspector]
    public UnityEvent onReviewRequested = new UnityEvent();
    [HideInInspector]
    public UnityEvent onReviewFlowFinished = new UnityEvent();
    [HideInInspector]
    public UnityEvent onReviewRequestFailed = new UnityEvent();

    public static ScenePilotNativeReviewController instance;

    private bool appPaused;
    private bool appFocused = true;
    private bool attemptedThisSession;
    private bool editorSimulationCompleted;
    private float nextSaveAt;
    private bool subscribed;
    private bool gameplayTimerStarted;
    private bool reviewDue;
    private bool reviewRequestActive;
    private float accumulatedGameplaySeconds;

    private const float SaveIntervalSeconds = 10f;

    private string RequestedKey => "ScenePilot.NativeReview.Requested." + Application.identifier;
    private string GameplaySecondsKey => "ScenePilot.NativeReview.GameplaySeconds." + Application.identifier;

    public bool HasRequestedReview => PlayerPrefs.GetInt(RequestedKey, 0) == 1;
    public bool IsReviewDue => reviewDue;
    public float AccumulatedGameplaySeconds => accumulatedGameplaySeconds;

    public static bool IsBlockingAutomaticInterstitials =>
        instance != null && instance.nativeReviewEnabled &&
        (instance.reviewDue || instance.reviewRequestActive) &&
        !instance.HasRequestedReview;

    public static void NotifyStaticSequenceFinished()
    {
        instance?.BeginGameplayTimer();
    }

    private void Reset()
    {
        remoteContent = transform.root.GetComponentInChildren<RemoteContentController>(true);
        nativeReviewEnabled = true;
        reviewDelaySeconds = 260f;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        if (remoteContent == null)
            remoteContent = transform.root.GetComponentInChildren<RemoteContentController>(true);

        // The serialized helper lives inside the ScenePilot prefab for easy
        // configuration. Detach only this tiny host at runtime so the timer
        // survives scene changes without preserving the complete slider UI.
        if (transform.parent != null)
            transform.SetParent(null, false);

        instance = this;
        DontDestroyOnLoad(gameObject);

        accumulatedGameplaySeconds = Mathf.Max(0f, PlayerPrefs.GetFloat(GameplaySecondsKey, 0f));
        reviewDue = nativeReviewEnabled && !HasRequestedReview &&
            accumulatedGameplaySeconds >= Mathf.Max(0f, reviewDelaySeconds);
        nextSaveAt = Time.realtimeSinceStartup + SaveIntervalSeconds;
    }

    private void OnEnable()
    {
        BindRemoteContent();
    }

    private void OnDisable()
    {
        UnbindRemoteContent();
        SaveTimer();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (!subscribed)
            BindRemoteContent();

        if (!nativeReviewEnabled || !gameplayTimerStarted || HasRequestedReview ||
            attemptedThisSession || editorSimulationCompleted)
            return;

        if (!appPaused && appFocused && !reviewRequestActive)
        {
            // Clamp one frame so returning from a suspended app cannot count the
            // time that the player spent outside the game.
            accumulatedGameplaySeconds += Mathf.Min(Time.unscaledDeltaTime, 1f);
        }

        if (Time.realtimeSinceStartup >= nextSaveAt)
        {
            SaveTimer();
            nextSaveAt = Time.realtimeSinceStartup + SaveIntervalSeconds;
        }

        if (!reviewDue && accumulatedGameplaySeconds >= Mathf.Max(0f, reviewDelaySeconds))
        {
            reviewDue = true;
            SaveTimer();
            Log("Native review is eligible after " + accumulatedGameplaySeconds.ToString("0") + " seconds of gameplay.");
        }

        if (reviewDue && !reviewRequestActive && CanPresentReviewNow())
            StartCoroutine(RequestNativeReview());
    }

    private void BindRemoteContent()
    {
        if (subscribed)
            return;

        if (remoteContent == null)
            remoteContent = GetComponent<RemoteContentController>();
        if (remoteContent == null)
            return;

        remoteContent.onStaticSequenceFinished.RemoveListener(BeginGameplayTimer);
        remoteContent.onStaticSequenceFinished.AddListener(BeginGameplayTimer);
        subscribed = true;

        // Recover safely if this component was enabled after the one-shot event.
        if (remoteContent.IsStaticPanelSequenceFinished)
            BeginGameplayTimer();
    }

    private void UnbindRemoteContent()
    {
        if (remoteContent != null)
            remoteContent.onStaticSequenceFinished.RemoveListener(BeginGameplayTimer);
        subscribed = false;
    }

    public void BeginGameplayTimer()
    {
        if (!nativeReviewEnabled || gameplayTimerStarted || HasRequestedReview)
            return;

        gameplayTimerStarted = true;
        Log("Static panels finished; native-review gameplay timer started at " +
            accumulatedGameplaySeconds.ToString("0") + "/" + Mathf.Max(0f, reviewDelaySeconds).ToString("0") + " seconds.");
    }

    private bool CanPresentReviewNow()
    {
        if (appPaused || !appFocused)
            return false;

        ScenePilotAdsController ads = ScenePilotAdsController.instance;
        return ads == null || !ads.IsFullScreenExperienceActive;
    }

    public void RequestAndroidReviewNow(System.Action onFinished)
    {
        StartCoroutine(RequestAndroidReviewStep(onFinished));
    }

    private IEnumerator RequestAndroidReviewStep(System.Action onFinished)
    {
        // Defer until navigation has finished switching the other panel roots.
        yield return null;
#if UNITY_ANDROID && !UNITY_EDITOR
        while (reviewRequestActive)
            yield return null;
        if (nativeReviewEnabled && !HasRequestedReview && !attemptedThisSession)
        {
            reviewDue = true;
            while (!CanPresentReviewNow())
                yield return null;
            yield return RequestNativeReview();
        }
#else
        Log("Android native review skipped on this platform; continuing the sequence.");
#endif
        onFinished?.Invoke();
    }

    private IEnumerator RequestNativeReview()
    {
        reviewRequestActive = true;
        attemptedThisSession = true;
        ScenePilotAdsController.instance?.DeferAutomaticInterstitialAfterExternalBreak();
        onReviewRequested?.Invoke();

#if UNITY_ANDROID && !UNITY_EDITOR
        ReviewManager reviewManager = new ReviewManager();
        PlayReviewInfo reviewInfo = null;

        var requestOperation = reviewManager.RequestReviewFlow();
        yield return requestOperation;
        if (requestOperation.Error != ReviewErrorCode.NoError)
        {
            FailRequest("Android review-info request failed: " + requestOperation.Error);
            yield break;
        }

        reviewInfo = requestOperation.GetResult();
        var launchOperation = reviewManager.LaunchReviewFlow(reviewInfo);
        yield return launchOperation;
        reviewInfo = null;
        if (launchOperation.Error != ReviewErrorCode.NoError)
        {
            FailRequest("Android native-review flow failed: " + launchOperation.Error);
            yield break;
        }

        CompleteRequest("Android native-review flow completed.");
#elif UNITY_IOS && !UNITY_EDITOR
        UnityEngine.iOS.Device.RequestStoreReview();
        CompleteRequest("iOS native-review request was handed to StoreKit.");
        yield break;
#else
        Log("Editor simulation: native-review request completed. Production PlayerPrefs were not changed.");
        editorSimulationCompleted = true;
        reviewRequestActive = false;
        reviewDue = false;
        onReviewFlowFinished?.Invoke();
        yield break;
#endif
    }

    private void CompleteRequest(string message)
    {
        PlayerPrefs.SetInt(RequestedKey, 1);
        PlayerPrefs.DeleteKey(GameplaySecondsKey);
        PlayerPrefs.Save();

        reviewDue = false;
        reviewRequestActive = false;
        Log(message + " The local one-time request flag is now saved.");
        onReviewFlowFinished?.Invoke();
    }

    private void FailRequest(string message)
    {
        reviewRequestActive = false;
        Debug.LogWarning("[ScenePilot/Review] " + message + " It can retry in a later app session.", this);
        onReviewRequestFailed?.Invoke();
    }

    private void OnApplicationPause(bool paused)
    {
        appPaused = paused;
        if (paused)
            SaveTimer();
    }

    private void OnApplicationFocus(bool focused)
    {
        appFocused = focused;
        if (!focused)
            SaveTimer();
    }

    private void OnApplicationQuit()
    {
        SaveTimer();
    }

    private void SaveTimer()
    {
        if (HasRequestedReview)
            return;

        PlayerPrefs.SetFloat(GameplaySecondsKey, Mathf.Max(0f, accumulatedGameplaySeconds));
        PlayerPrefs.Save();
    }

    [ContextMenu("Reset Native Review State (Testing)")]
    public void ResetReviewStateForTesting()
    {
        PlayerPrefs.DeleteKey(RequestedKey);
        PlayerPrefs.DeleteKey(GameplaySecondsKey);
        PlayerPrefs.Save();

        accumulatedGameplaySeconds = 0f;
        reviewDue = false;
        reviewRequestActive = false;
        attemptedThisSession = false;
        editorSimulationCompleted = false;
        Log("Native-review timer and request flag were reset.");
    }

    private void Log(string message)
    {
        Debug.Log("[ScenePilot/Review] " + message, this);
    }
}
