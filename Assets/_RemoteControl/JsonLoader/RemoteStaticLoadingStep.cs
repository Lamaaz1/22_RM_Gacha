using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RemoteStaticPanel))]
public sealed class RemoteStaticLoadingStep : MonoBehaviour
{
    public RemoteStaticPanel staticPanel;
    public Slider progressSlider;

    [Min(0f)]
    public float durationSeconds = 3f;

    public bool startAutomatically = true;

    private float currentTime;
    private bool running;
    private bool completed;
    private bool wasReady;
    private RemoteContentController controller;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResetLoading();
    }

    private void Start()
    {
        if (startAutomatically)
            StartLoading();
    }

    private void OnDisable()
    {
        running = false;
        wasReady = false;
    }

    private void Update()
    {
        if (!IsPanelReady())
        {
            if (wasReady || running || completed)
                ResetLoading();
            return;
        }

        // A child panelRoot can become visible without OnEnable on this host.
        if (!wasReady)
        {
            wasReady = true;
            if (startAutomatically)
                StartLoading();
            return;
        }

        if (!running || completed)
            return;

        float maxTime = Mathf.Max(0f, durationSeconds);
        currentTime += Time.unscaledDeltaTime;

        if (maxTime > 0f)
            SetSliderValue(currentTime / maxTime);

        if (currentTime >= maxTime)
            CompleteAndAdvance();
    }

    public void StartLoading()
    {
        if (!isActiveAndEnabled || completed || running || !IsPanelReady())
            return;

        currentTime = 0f;
        running = true;
        wasReady = true;
        SetSliderValue(0f);
    }

    public void CompleteNow()
    {
        if (!isActiveAndEnabled || completed || !IsPanelReady())
            return;

        CompleteAndAdvance();
    }

    private void CompleteAndAdvance()
    {
        running = false;
        completed = true;
        SetSliderValue(1f);

        if (staticPanel != null)
        {
            if (controller == null)
                controller = FindFirstObjectByType<RemoteContentController>(FindObjectsInactive.Include);

            if (controller != null)
                controller.ShowNextStaticLoadingPanel(staticPanel);
            else
                staticPanel.ShowNextPanel();
            return;
        }

        Debug.LogError(
            "[RemoteContent] Loading step cannot advance because RemoteStaticPanel is missing.",
            this
        );

        gameObject.SetActive(false);
    }

    private void SetSliderValue(float normalizedValue)
    {
        if (progressSlider == null)
            return;

        progressSlider.normalizedValue = Mathf.Clamp01(normalizedValue);
    }

    private void ResetLoading()
    {
        currentTime = 0f;
        running = false;
        completed = false;
        wasReady = false;
        SetSliderValue(0f);
    }

    private bool IsPanelReady()
    {
        if (!IsLoadingPanel())
            return false;

        if (controller == null)
            controller = FindFirstObjectByType<RemoteContentController>(FindObjectsInactive.Include);

        return controller != null && controller.CanRunStaticLoadingPanel(staticPanel);
    }

    private void ResolveReferences()
    {
        if (staticPanel == null)
            staticPanel = GetComponent<RemoteStaticPanel>();

        if (progressSlider == null)
            progressSlider = GetComponentInChildren<Slider>(true);
    }

    private bool IsLoadingPanel()
    {
        if (staticPanel == null || string.IsNullOrEmpty(staticPanel.panelId))
            return false;

        return staticPanel.panelId.Equals(
                   "loading",
                   System.StringComparison.OrdinalIgnoreCase
               ) ||
               staticPanel.panelId.StartsWith(
                   "loading_",
                   System.StringComparison.OrdinalIgnoreCase
               );
    }
}
