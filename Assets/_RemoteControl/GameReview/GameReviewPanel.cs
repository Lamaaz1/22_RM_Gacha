using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEngine.UIElements;
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.Review;
#endif

[DisallowMultipleComponent]
public sealed class GameReviewPanel : MonoBehaviour
{
    private static GameReviewPanel activePanel;

    private void Awake()
    {
        if (activePanel != null && activePanel != this)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        activePanel = this;
        if (transform.parent != null)
            transform.SetParent(null, false);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (activePanel == this)
            activePanel = null;
    }

    [SerializeField, Min(0f), Tooltip("Seconds of active play between automatic requests. Zero disables the timer.")]
    private float intervalSeconds = 120f;

    [SerializeField, Min(0), Tooltip("Maximum automatic requests while this component is loaded.")]
    private int maxAutomaticShows = 1;

#if UNITY_ANDROID && !UNITY_EDITOR
    private float elapsed;
    private int automaticRequests;
    private Coroutine reviewFlow;

    private void Update()
    {
        if (reviewFlow != null || intervalSeconds <= 0f ||
            automaticRequests >= maxAutomaticShows || !Application.isFocused ||
            Time.timeScale <= 0f)
            return;

        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= intervalSeconds)
        {
            automaticRequests++;
            Open();
        }
    }

    private void OnDisable()
    {
        if (reviewFlow != null)
        {
            StopCoroutine(reviewFlow);
            reviewFlow = null;
        }
    }

    private IEnumerator RequestReview()
    {
        Debug.Log($"[GameReview] Requesting native review for {Application.identifier}.", this);
        var manager = new ReviewManager();
        var request = manager.RequestReviewFlow();
        yield return request;

        if (request.Error == ReviewErrorCode.NoError)
        {
            var launch = manager.LaunchReviewFlow(request.GetResult());
            yield return launch;
            if (launch.Error != ReviewErrorCode.NoError)
                Debug.LogWarning($"[GameReview] Launch failed: {launch.Error}", this);
            else
                Debug.Log("[GameReview] Google Play review flow completed.", this);
        }
        else
        {
            Debug.LogWarning($"[GameReview] Request failed: {request.Error}", this);
        }

        reviewFlow = null;
    }
#endif

    public void Open()
    {
#if UNITY_EDITOR
        if (!isActiveAndEnabled || GetComponentInChildren<UIDocument>() != null)
            return;

        var host = new GameObject("Google Play Review Preview");
        host.SetActive(false);
        host.transform.SetParent(transform, false);
        var settings = ScriptableObject.CreateInstance<PanelSettings>();
        settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ScenePilotReviewTheme");
        settings.sortingOrder = 1000;
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(960, 540);
        var document = host.AddComponent<UIDocument>();
        document.panelSettings = settings;
        host.SetActive(true);
        var root = document.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.justifyContent = Justify.Center;
        root.style.alignItems = Align.Center;
        var close = new Button(() =>
        {
            Destroy(host);
            Destroy(settings);
        }) { text = "Close Google Play Review — Editor Preview" };
        root.Add(close);
        Debug.Log("[GameReview] Editor preview opened.", this);
#elif UNITY_ANDROID
        if (isActiveAndEnabled && Application.isFocused && reviewFlow == null)
        {
            elapsed = 0f;
            reviewFlow = StartCoroutine(RequestReview());
        }
#endif
    }
}
