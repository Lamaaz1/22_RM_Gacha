using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RemoteStaticPanel))]
public sealed class RemoteEndlessLoading : MonoBehaviour
{
    public enum AnimationMode
    {
        Slider,
        Spinner,
        SliderAndSpinner
    }

    [Header("ENDLESS LOADING")]
    public AnimationMode animationMode = AnimationMode.Slider;
    public Slider progressSlider;
    public RectTransform spinner;
    public TextMeshProUGUI statusText;

    [Min(0.1f)]
    [Tooltip("Seconds required for the slider to fill once before looping.")]
    public float sliderCycleSeconds = 2f;

    [Tooltip("Spinner speed in degrees per second.")]
    public float spinnerDegreesPerSecond = -180f;

    [Tooltip("Keep animating when Time.timeScale is zero.")]
    public bool useUnscaledTime = true;

    [Tooltip("Optional animated dots appended to the current status text.")]
    public bool animateStatusDots;

    [Min(0.1f)]
    public float statusDotInterval = 0.45f;

    private float elapsed;
    private float dotElapsed;
    private int dotCount;
    private string baseStatus;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        elapsed = 0f;
        dotElapsed = 0f;
        dotCount = 0;
        baseStatus = statusText != null ? statusText.text.TrimEnd('.') : string.Empty;
        SetSliderProgress(0f);
    }

    private void Update()
    {
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        elapsed += delta;

        if (animationMode == AnimationMode.Slider || animationMode == AnimationMode.SliderAndSpinner)
            SetSliderProgress(Mathf.Repeat(elapsed / Mathf.Max(0.1f, sliderCycleSeconds), 1f));

        if ((animationMode == AnimationMode.Spinner || animationMode == AnimationMode.SliderAndSpinner) && spinner != null)
            spinner.Rotate(0f, 0f, spinnerDegreesPerSecond * delta);

        if (animateStatusDots && statusText != null)
        {
            dotElapsed += delta;
            if (dotElapsed >= statusDotInterval)
            {
                dotElapsed = 0f;
                dotCount = (dotCount + 1) % 4;
                statusText.text = baseStatus + new string('.', dotCount);
            }
        }
    }

    private void OnDisable()
    {
        if (animateStatusDots && statusText != null)
            statusText.text = baseStatus;
    }

    private void SetSliderProgress(float normalized)
    {
        if (progressSlider != null)
            progressSlider.normalizedValue = Mathf.Clamp01(normalized);
    }

    private void ResolveReferences()
    {
        if (progressSlider == null)
            progressSlider = GetComponentInChildren<Slider>(true);

        if (statusText == null)
            statusText = GetComponentInChildren<TextMeshProUGUI>(true);
    }
}
