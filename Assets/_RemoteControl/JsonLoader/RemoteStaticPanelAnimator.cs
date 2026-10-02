using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

// UI animation based on authored transform values. Position, size and anchors
// stay untouched, and every temporary scale is restored when animation stops.
[DisallowMultipleComponent]
public sealed class RemoteStaticPanelAnimator : MonoBehaviour
{
    [Header("PANEL SHOW")]
    public CanvasGroup panelCanvasGroup;
    [Min(0.01f)] public float panelFadeDuration = 0.38f;
    [Range(0.8f, 1f)] public float panelStartScale = 0.96f;

    [Header("BUTTONS AND TOGGLES")]
    public bool animateButtons = true;
    public bool animateToggles = true;
    [Min(0.01f)] public float controlFadeDuration = 0.24f;
    [Min(0f)] public float controlStaggerSeconds = 0.065f;
    [Range(0f, 1f)] public float controlStartAlpha = 0f;
    [Range(0.8f, 1f)] public float controlStartScale = 0.94f;

    [Header("CONTROL INTERACTION")]
    [Min(0.01f)] public float interactionFadeDuration = 0.12f;
    [Range(0f, 1f)] public float buttonIdleAlpha = 0.94f;
    [Range(0f, 1f)] public float hoverAlpha = 1f;
    [Range(0f, 1f)] public float pressedAlpha = 0.68f;
    [Range(0f, 1f)] public float toggleUnselectedAlpha = 0.82f;
    [Range(0f, 1f)] public float toggleSelectedAlpha = 1f;
    [Min(0.01f)] public float toggleSelectPulseDuration = 0.2f;
    [Range(0f, 1f)] public float toggleSelectPulseAlpha = 0.7f;
    [Range(0.9f, 1.1f)] public float idleScale = 1f;
    [Range(0.9f, 1.1f)] public float hoverScale = 1.035f;
    [Range(0.9f, 1.1f)] public float pressedScale = 0.96f;
    [Range(0.5f, 1.1f)] public float toggleSelectedScale = 0.7f;
    [Range(0.9f, 1.15f)] public float toggleSelectPulseScale = 1.06f;

    private sealed class ControlState
    {
        public Selectable selectable;
        public Toggle toggle;
        public CanvasGroup canvasGroup;
        public float originalAlpha;
        public Vector3 originalScale;
        public bool hovered;
        public bool pressed;
    }

    private readonly List<ControlState> controls = new List<ControlState>();
    private readonly Dictionary<Toggle, UnityAction<bool>> toggleActions =
        new Dictionary<Toggle, UnityAction<bool>>();
    private readonly Dictionary<CanvasGroup, Coroutine> interactionRoutines =
        new Dictionary<CanvasGroup, Coroutine>();

    private float panelOriginalAlpha = 1f;
    private Vector3 panelOriginalScale = Vector3.one;
    private bool panelOriginalInteractable = true;
    private bool panelOriginalBlocksRaycasts = true;
    private bool playedForCurrentActivation;
    private Coroutine showRoutine;

    private void Awake()
    {
        EnsurePanelCanvasGroup();
        CapturePanelState();
        DiscoverAndBindControls();
    }

    private void OnEnable()
    {
        PlayShowIfNeeded();
    }

    private void OnDisable()
    {
        CancelAndRestore();
        playedForCurrentActivation = false;
    }

    private void OnDestroy()
    {
        CancelAndRestore();

        foreach (KeyValuePair<Toggle, UnityAction<bool>> pair in toggleActions)
            if (pair.Key != null) pair.Key.onValueChanged.RemoveListener(pair.Value);
    }

    public void PlayShowIfNeeded()
    {
        if (!isActiveAndEnabled || playedForCurrentActivation)
            return;

        playedForCurrentActivation = true;
        EnsurePanelCanvasGroup();
        DiscoverAndBindControls();

        if (showRoutine != null)
            StopCoroutine(showRoutine);
        RestoreControlAlpha();
        showRoutine = StartCoroutine(PlayShowRoutine());
    }

    internal void SetHovered(Selectable selectable, bool hovered)
    {
        ControlState state = FindControl(selectable);
        if (state == null)
            return;

        state.hovered = hovered;
        if (showRoutine == null)
            FadeToInteractionState(state);
    }

    internal void SetPressed(Selectable selectable, bool pressed)
    {
        ControlState state = FindControl(selectable);
        if (state == null)
            return;

        state.pressed = pressed;
        if (showRoutine == null)
            FadeToInteractionState(state);
    }

    private void EnsurePanelCanvasGroup()
    {
        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void CapturePanelState()
    {
        if (panelCanvasGroup == null)
            return;

        panelOriginalAlpha = panelCanvasGroup.alpha;
        panelOriginalScale = transform.localScale;
        panelOriginalInteractable = panelCanvasGroup.interactable;
        panelOriginalBlocksRaycasts = panelCanvasGroup.blocksRaycasts;
    }

    private void DiscoverAndBindControls()
    {
        controls.Clear();
        HashSet<Selectable> unique = new HashSet<Selectable>();

        if (animateButtons)
            foreach (Button button in GetComponentsInChildren<Button>(true))
                AddControl(button, unique);

        if (animateToggles)
        {
            foreach (Toggle toggle in GetComponentsInChildren<Toggle>(true))
            {
                AddControl(toggle, unique);
                if (toggleActions.ContainsKey(toggle))
                    continue;

                Toggle captured = toggle;
                UnityAction<bool> action = selected => OnToggleValueChanged(captured, selected);
                toggleActions.Add(toggle, action);
                toggle.onValueChanged.AddListener(action);
            }
        }
    }

    private void AddControl(Selectable selectable, HashSet<Selectable> unique)
    {
        if (selectable == null || selectable.gameObject == gameObject || !unique.Add(selectable))
            return;

        CanvasGroup group = selectable.GetComponent<CanvasGroup>();
        if (group == null)
            group = selectable.gameObject.AddComponent<CanvasGroup>();

        RemoteSelectableAnimationRelay relay =
            selectable.GetComponent<RemoteSelectableAnimationRelay>();
        if (relay == null)
            relay = selectable.gameObject.AddComponent<RemoteSelectableAnimationRelay>();
        relay.Configure(this, selectable);

        controls.Add(new ControlState
        {
            selectable = selectable,
            toggle = selectable as Toggle,
            canvasGroup = group,
            originalAlpha = group.alpha,
            originalScale = selectable.transform.localScale
        });
    }

    private IEnumerator PlayShowRoutine()
    {
        panelCanvasGroup.alpha = 0f;
        transform.localScale = panelOriginalScale * panelStartScale;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        foreach (ControlState control in controls)
            if (control.canvasGroup != null)
            {
                control.canvasGroup.alpha = control.originalAlpha * controlStartAlpha;
                control.selectable.transform.localScale = control.originalScale * controlStartScale;
            }

        float controlsEnd = controls.Count == 0
            ? 0f
            : (controls.Count - 1) * controlStaggerSeconds + controlFadeDuration;
        float totalDuration = Mathf.Max(panelFadeDuration, controlsEnd);
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            panelCanvasGroup.alpha = Mathf.Lerp(
                0f,
                panelOriginalAlpha,
                EaseOutCubic(elapsed / Mathf.Max(0.01f, panelFadeDuration))
            );
            transform.localScale = Vector3.Lerp(
                panelOriginalScale * panelStartScale,
                panelOriginalScale,
                EaseOutCubic(elapsed / Mathf.Max(0.01f, panelFadeDuration))
            );

            for (int i = 0; i < controls.Count; i++)
            {
                ControlState control = controls[i];
                if (control.canvasGroup == null) continue;
                float delay = i * controlStaggerSeconds;
                float progress = EaseOutCubic(
                    (elapsed - delay) / Mathf.Max(0.01f, controlFadeDuration)
                );
                control.canvasGroup.alpha = Mathf.Lerp(
                    control.originalAlpha * controlStartAlpha,
                    GetInteractionTargetAlpha(control),
                    progress
                );
                control.selectable.transform.localScale = Vector3.Lerp(
                    control.originalScale * controlStartScale,
                    GetInteractionTargetScale(control),
                    progress
                );
            }

            yield return null;
        }

        RestorePanelState();
        foreach (ControlState control in controls)
            ApplyInteractionStateImmediately(control);
        showRoutine = null;
    }

    private void OnToggleValueChanged(Toggle toggle, bool selected)
    {
        ControlState state = FindControl(toggle);
        if (!isActiveAndEnabled || showRoutine != null || state == null)
            return;

        if (selected)
            StartInteractionRoutine(state, PlayToggleSelectRoutine(state));
        else
            FadeToInteractionState(state);
    }

    private void FadeToInteractionState(ControlState state)
    {
        if (!isActiveAndEnabled || state == null || state.canvasGroup == null)
            return;

        StartInteractionRoutine(
            state,
            FadeControlRoutine(
                state,
                GetInteractionTargetAlpha(state),
                GetInteractionTargetScale(state),
                interactionFadeDuration
            )
        );
    }

    private void StartInteractionRoutine(ControlState state, IEnumerator routine)
    {
        // A child relay can receive OnDisable while its panel hierarchy is in
        // the middle of SetActive(false). Unity may still report this component
        // as enabled for that callback even though its GameObject is already
        // inactive, and StartCoroutine would then throw every time a panel is
        // hidden by navigation. Cleanup does not need a fade.
        if (state == null || state.canvasGroup == null ||
            !isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        Coroutine running;
        if (interactionRoutines.TryGetValue(state.canvasGroup, out running) && running != null)
            StopCoroutine(running);

        interactionRoutines[state.canvasGroup] = StartCoroutine(routine);
    }

    private IEnumerator FadeControlRoutine(
        ControlState state,
        float targetAlpha,
        Vector3 targetScale,
        float duration
    )
    {
        CanvasGroup group = state.canvasGroup;
        float startAlpha = group.alpha;
        Vector3 startScale = state.selectable.transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration && group != null)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                EaseOutCubic(elapsed / Mathf.Max(0.01f, duration))
            );
            state.selectable.transform.localScale = Vector3.Lerp(
                startScale,
                targetScale,
                EaseOutCubic(elapsed / Mathf.Max(0.01f, duration))
            );
            yield return null;
        }

        if (group != null)
        {
            group.alpha = targetAlpha;
            state.selectable.transform.localScale = targetScale;
            interactionRoutines.Remove(group);
        }
    }

    private IEnumerator PlayToggleSelectRoutine(ControlState state)
    {
        CanvasGroup group = state.canvasGroup;
        float startAlpha = group.alpha;
        float targetAlpha = GetInteractionTargetAlpha(state);
        float pulseAlpha = state.originalAlpha * toggleSelectPulseAlpha;
        Vector3 startScale = state.selectable.transform.localScale;
        Vector3 targetScale = GetInteractionTargetScale(state);
        Vector3 pulseScale = state.originalScale * toggleSelectPulseScale;
        float elapsed = 0f;

        while (elapsed < toggleSelectPulseDuration && group != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(
                elapsed / Mathf.Max(0.01f, toggleSelectPulseDuration)
            );
            if (progress < 0.5f)
            {
                group.alpha = Mathf.Lerp(startAlpha, pulseAlpha, EaseOutCubic(progress * 2f));
                state.selectable.transform.localScale = Vector3.Lerp(
                    startScale,
                    pulseScale,
                    EaseOutCubic(progress * 2f)
                );
            }
            else
            {
                group.alpha = Mathf.Lerp(pulseAlpha, targetAlpha, EaseOutCubic((progress - 0.5f) * 2f));
                state.selectable.transform.localScale = Vector3.Lerp(
                    pulseScale,
                    targetScale,
                    EaseOutCubic((progress - 0.5f) * 2f)
                );
            }
            yield return null;
        }

        if (group != null)
        {
            group.alpha = targetAlpha;
            state.selectable.transform.localScale = targetScale;
            interactionRoutines.Remove(group);
        }
    }

    private float GetInteractionTargetAlpha(ControlState state)
    {
        float multiplier;

        if (state.pressed)
            multiplier = pressedAlpha;
        else if (state.toggle != null)
            multiplier = state.toggle.isOn
                ? toggleSelectedAlpha
                : (state.hovered ? hoverAlpha : toggleUnselectedAlpha);
        else
            multiplier = state.hovered ? hoverAlpha : buttonIdleAlpha;

        return state.originalAlpha * multiplier;
    }

    private Vector3 GetInteractionTargetScale(ControlState state)
    {
        float multiplier;

        if (state.pressed)
            multiplier = pressedScale;
        else if (state.toggle != null && state.toggle.isOn)
            multiplier = toggleSelectedScale;
        else if (state.hovered)
            multiplier = hoverScale;
        else
            multiplier = idleScale;

        return state.originalScale * multiplier;
    }

    private ControlState FindControl(Selectable selectable)
    {
        return controls.Find(item => item.selectable == selectable);
    }

    private void ApplyInteractionStateImmediately(ControlState state)
    {
        if (state.canvasGroup != null)
        {
            state.canvasGroup.alpha = GetInteractionTargetAlpha(state);
            state.selectable.transform.localScale = GetInteractionTargetScale(state);
        }
    }

    private void CancelAndRestore()
    {
        StopAllCoroutines();
        showRoutine = null;
        interactionRoutines.Clear();

        foreach (ControlState control in controls)
        {
            control.hovered = false;
            control.pressed = false;
        }

        RestorePanelState();
        RestoreControlAlpha();
    }

    private void RestorePanelState()
    {
        transform.localScale = panelOriginalScale;

        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.alpha = panelOriginalAlpha;
        panelCanvasGroup.interactable = panelOriginalInteractable;
        panelCanvasGroup.blocksRaycasts = panelOriginalBlocksRaycasts;
    }

    private void RestoreControlAlpha()
    {
        foreach (ControlState control in controls)
        {
            if (control.canvasGroup != null)
                control.canvasGroup.alpha = control.originalAlpha;

            if (control.selectable != null)
                control.selectable.transform.localScale = control.originalScale;
        }
    }

    private static float EaseOutCubic(float value)
    {
        float t = Mathf.Clamp01(value);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }
}

// Added to buttons and toggles at runtime. It forwards pointer interaction to
// the panel animator without changing each control's authored transition setup.
[AddComponentMenu("")]
public sealed class RemoteSelectableAnimationRelay : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    private RemoteStaticPanelAnimator owner;
    private Selectable selectable;

    public void Configure(RemoteStaticPanelAnimator animationOwner, Selectable target)
    {
        owner = animationOwner;
        selectable = target;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null) owner.SetHovered(selectable, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null) owner.SetHovered(selectable, false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (owner != null) owner.SetPressed(selectable, true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (owner != null) owner.SetPressed(selectable, false);
    }

    private void OnDisable()
    {
        if (owner == null)
            return;

        owner.SetPressed(selectable, false);
        owner.SetHovered(selectable, false);
    }
}
