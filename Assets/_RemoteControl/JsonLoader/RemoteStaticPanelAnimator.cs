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
    public bool choicesEaseInQuart = false;
    public bool randomChoiceEntrance = false;
    private static int previousChoiceEntrance = -1;
    public bool dimOtherChoicesOnSelection = false;
    [Range(0.5f, 1f)] public float otherChoiceScale = 0.9f;
    [Range(0f, 1f)] public float otherChoiceAlpha = 0.55f;
    [Min(0f)] public float choiceShakeDegrees = 0f;
    [Min(0.01f)] public float choiceShakeDuration = 0.3f;
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
    [Range(0.5f, 1.4f)] public float toggleSelectedScale = 0.7f;
    [Range(0.9f, 1.5f)] public float toggleSelectPulseScale = 1.06f;

    [Header("NEXT BUTTON PULSE")]
    public RectTransform nextPulseTarget;
    [Range(0f, 0.2f)] public float nextPulseAmount = 0.07f;
    [Min(0.1f)] public float nextPulsePeriod = 0.85f;
    private Vector3 nextOriginalScale;
    private float nextPulseTime;
    private bool nextWasPulsing;
    [Header("ORDERED ENTRANCE")]
    public RectTransform[] entranceItems;
    [Range(0f, 0.2f)] public float choicesGroupBumpAmount = 0.08f;
    [Min(0.01f)] public float choicesGroupBumpDuration = 0.36f;
    private sealed class EntranceState
    {
        public RectTransform rect;
        public Quaternion rotation;
        public Vector2 shuffleHome;
        public bool shuffling;
        public CanvasGroup group;
        public float alpha;
        public Vector3 scale;
    }
    private readonly List<EntranceState> entranceStates = new List<EntranceState>();
    private sealed class ControlState
    {
        public Selectable selectable;
        public Toggle toggle;
        public CanvasGroup canvasGroup;
        public float originalAlpha;
        public Vector3 originalScale;
        public Quaternion originalRotation;
        public Coroutine shakeRoutine;
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
        CaptureEntranceItems();
        if (nextPulseTarget != null) nextOriginalScale = nextPulseTarget.localScale;
        DiscoverAndBindControls();
    }

    private void OnEnable()
    {
        PlayShowIfNeeded();
    }

    private void LateUpdate()
    {
        if (nextPulseTarget == null || showRoutine != null) return;
        bool selected = controls.Exists(control => control.toggle != null &&
            control.toggle.isActiveAndEnabled && control.toggle.isOn);
        ControlState nextControl = controls.Find(control => control.selectable != null &&
            control.selectable.transform == nextPulseTarget);
        bool interacting = nextControl != null && (nextControl.hovered || nextControl.pressed ||
            interactionRoutines.ContainsKey(nextControl.canvasGroup));
        if (selected && !interacting && nextPulseTarget.gameObject.activeInHierarchy)
        {
            nextPulseTime += Time.unscaledDeltaTime;
            float wave = 0.5f - 0.5f * Mathf.Cos(nextPulseTime * Mathf.PI * 2f / Mathf.Max(0.1f, nextPulsePeriod));
            nextPulseTarget.localScale = nextOriginalScale * (1f + nextPulseAmount * wave);
            nextWasPulsing = true;
        }
        else
        {
            if (nextWasPulsing && !interacting)
                nextPulseTarget.localScale = nextOriginalScale;
            nextPulseTime = 0f;
            nextWasPulsing = false;
        }
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
        RestoreEntranceItems();
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

    private void OrderChoicesForEntrance()
    {
        Selectable[] hierarchy = GetComponentsInChildren<Selectable>(true);
        controls.Sort((a, b) =>
        {
            int kind = (a.toggle == null ? 1 : 0).CompareTo(b.toggle == null ? 1 : 0);
            return kind != 0 ? kind : System.Array.IndexOf(hierarchy, a.selectable)
                .CompareTo(System.Array.IndexOf(hierarchy, b.selectable));
        });
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
            originalRotation = selectable.transform.localRotation,
            originalScale = selectable.transform.localScale
        });
    }

    private void CaptureEntranceItems()
    {
        if (entranceItems == null) return;
        foreach (RectTransform rect in entranceItems)
        {
            if (rect == null || rect == transform || entranceStates.Exists(item => item.rect == rect)) continue;
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
            entranceStates.Add(new EntranceState { rect = rect, group = group,
                alpha = group.alpha, scale = rect.localScale, rotation = rect.localRotation });
        }
    }

    private IEnumerator PlayOrderedEntrance()
    {
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
        foreach (EntranceState item in entranceStates)
        {
            if (item.rect == null || item.group == null) continue;
            item.group.alpha = 0f;
            item.rect.localScale = item.scale * controlStartScale;
        }
        int entranceStyle = 0;
        if (randomChoiceEntrance)
        {
            // Left tilt is enabled again; right tilt remains disabled.
            int[] enabledStyles = { 0, 1, 3 };
            int pick = Random.Range(0, enabledStyles.Length);
            entranceStyle = enabledStyles[pick];
            if (entranceStyle == previousChoiceEntrance)
                entranceStyle = enabledStyles[(pick + Random.Range(1, enabledStyles.Length)) % enabledStyles.Length];
            previousChoiceEntrance = entranceStyle;
        }
        int choicesRemaining = entranceStates.FindAll(item => item.rect != null && item.rect.gameObject.activeInHierarchy &&
            controls.Exists(control => control.toggle != null && control.selectable.transform == item.rect)).Count;
        // Background stays visible; only the authored items appear sequentially.
        panelCanvasGroup.alpha = panelOriginalAlpha;
        transform.localScale = panelOriginalScale;
        foreach (EntranceState item in entranceStates)
        {
            if (item.rect == null || item.group == null || !item.rect.gameObject.activeInHierarchy) continue;
            ControlState control = controls.Find(state => state.selectable != null && state.selectable.transform == item.rect);
            bool choice = control != null && control.toggle != null;
            float elapsed = 0f;
            while (elapsed < controlFadeDuration && item.rect != null && item.group != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, controlFadeDuration));
                float progress = EaseInQuart(Mathf.Clamp01(t / 0.75f));
                float alpha = control != null ? GetInteractionTargetAlpha(control) : item.alpha;
                Vector3 scale = control != null ? GetInteractionTargetScale(control) : item.scale;
                float visibility = choice && randomChoiceEntrance
                    ? EaseOutCubic(Mathf.Clamp01(t / 0.2f)) : progress;
                item.group.alpha = Mathf.Lerp(0f, alpha, visibility);
                item.rect.localScale = Vector3.Lerp(item.scale * controlStartScale,
                    scale * (choice ? ChoiceEntranceBump(t) : 1f), progress);
                if (choice && randomChoiceEntrance)
                    ApplyRandomChoiceEntrance(item, scale, progress, t, entranceStyle);
                yield return null;
            }
            if (item.rect != null && item.group != null)
            {
                item.group.alpha = control != null ? GetInteractionTargetAlpha(control) : item.alpha;
                item.rect.localScale = control != null ? GetInteractionTargetScale(control) : item.scale;
                item.rect.localRotation = item.rotation;
            }
            if (choice && --choicesRemaining == 0)
            {
                if (randomChoiceEntrance && entranceStyle == 0) yield return ShuffleChoices();
                yield return BumpChoicesTogether();
            }
        }
        RestorePanelState();
        showRoutine = null;
    }

    private void ApplyRandomChoiceEntrance(EntranceState item, Vector3 targetScale, float progress, float t, int style)
    {
        float settle = ChoiceEntranceBump(t);
        progress = EaseOutCubic(Mathf.Clamp01(t / 0.8f));
        switch (style)
        {
            case 0: // Enter upright before the quick three-card position swap.
                item.rect.localScale = Vector3.Lerp(item.scale * 0.6f, targetScale * settle, progress);
                item.rect.localRotation = item.rotation;
                break;
            case 1: // Tilt from the left, then settle upright.
            case 2: // Tilt from the right.
                float angle = (style == 1 ? -25f : 25f) * (1f - progress);
                item.rect.localRotation = item.rotation * Quaternion.Euler(0f, 0f, angle);
                item.rect.localScale = Vector3.Lerp(item.scale * 0.6f, targetScale * settle, progress);
                break;
            case 3: // Squash and stretch without moving the authored rectangle.
                Vector3 start = Vector3.Scale(item.scale, new Vector3(1.25f, 0.4f, 1f));
                item.rect.localScale = Vector3.Lerp(start, targetScale * settle, progress);
                break;
        }
    }
    private IEnumerator ShuffleChoices()
    {
        List<EntranceState> cards = entranceStates.FindAll(item => item.rect != null &&
            item.rect.gameObject.activeInHierarchy && controls.Exists(control =>
                control.toggle != null && control.selectable.transform == item.rect));
        if (cards.Count != 3) yield break;
        Vector3[] homes = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            homes[i] = cards[i].rect.localPosition;
            cards[i].shuffleHome = cards[i].rect.anchoredPosition;
            cards[i].shuffling = true;
        }
        // Swap the cards currently occupying these slots: left/middle,
        // middle/right, then right/left. Only two cards move at a time.
        int[] occupants = { 0, 1, 2 };
        int[] fromSlots = { 0, 1, 2, 1 };
        int[] toSlots = { 1, 2, 0, 2 };
        for (int step = 0; step < fromSlots.Length; step++)
        {
            int leftSlot = fromSlots[step];
            int rightSlot = toSlots[step];
            int a = occupants[leftSlot];
            int b = occupants[rightSlot];
            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / 0.2f);
                float smooth = t * t * (3f - 2f * t);
                if (cards[a].rect != null)
                    cards[a].rect.localPosition = Vector3.Lerp(homes[leftSlot], homes[rightSlot], smooth);
                if (cards[b].rect != null)
                    cards[b].rect.localPosition = Vector3.Lerp(homes[rightSlot], homes[leftSlot], smooth);
                yield return null;
            }
            occupants[leftSlot] = b;
            occupants[rightSlot] = a;
        }
        // The fourth pair swap restores the original slot order.
        for (int i = 0; i < 3; i++)
        {
            if (cards[i].rect != null) cards[i].rect.localPosition = homes[i];
            cards[i].shuffling = false;
        }
    }
    private IEnumerator BumpChoicesTogether()
    {
        float elapsed = 0f;
        while (elapsed < choicesGroupBumpDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, choicesGroupBumpDuration));
            float bump = t < 0.5f ? EaseOutCubic(t * 2f) : 1f - EaseOutCubic((t - 0.5f) * 2f);
            foreach (ControlState control in controls)
            {
                if (control.toggle == null || !control.toggle.isActiveAndEnabled) continue;
                control.canvasGroup.alpha = control.originalAlpha;
                control.selectable.transform.localScale = GetInteractionTargetScale(control) * (1f + choicesGroupBumpAmount * bump);
            }
            yield return null;
        }
        foreach (ControlState control in controls)
            if (control.toggle != null && control.selectable != null)
                control.selectable.transform.localScale = GetInteractionTargetScale(control);
    }
    private void RestoreEntranceItems()
    {
        foreach (EntranceState item in entranceStates)
        {
            if (item.rect != null)
            {
                if (item.shuffling) item.rect.anchoredPosition = item.shuffleHome;
                item.shuffling = false;
                item.rect.localScale = item.scale;
                item.rect.localRotation = item.rotation;
            }
            if (item.group != null) item.group.alpha = item.alpha;
        }
    }
    private IEnumerator PlayShowRoutine()
    {
        if (entranceStates.Count > 0)
        {
            yield return PlayOrderedEntrance();
            yield break;
        }
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

        if (choicesEaseInQuart) OrderChoicesForEntrance();

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
                float entranceTime = Mathf.Clamp01((elapsed - delay) / Mathf.Max(0.01f, controlFadeDuration));
                bool choice = choicesEaseInQuart && control.toggle != null;
                float progress = choice ? EaseInQuart(Mathf.Clamp01(entranceTime / 0.75f))
                    : EaseOutCubic(entranceTime);
                control.canvasGroup.alpha = Mathf.Lerp(
                    control.originalAlpha * controlStartAlpha,
                    GetInteractionTargetAlpha(control),
                    progress
                );
                control.selectable.transform.localScale = Vector3.Lerp(
                    control.originalScale * controlStartScale,
                    GetInteractionTargetScale(control) * (choice ? ChoiceEntranceBump(entranceTime) : 1f),
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

        if (state.shakeRoutine != null) StopCoroutine(state.shakeRoutine);
        state.selectable.transform.localRotation = state.originalRotation;
        state.shakeRoutine = null;
        if (selected)
        {
            if (choiceShakeDegrees > 0f)
                state.shakeRoutine = StartCoroutine(ShakeChoice(state));
            StartInteractionRoutine(state, PlayToggleSelectRoutine(state));
        }
        else
            FadeToInteractionState(state);
    
        if (dimOtherChoicesOnSelection)
            foreach (ControlState other in controls)
                if (other != state && other.toggle != null)
                    FadeToInteractionState(other);
    }

    private IEnumerator ShakeChoice(ControlState state)
    {
        float elapsed = 0f;
        while (elapsed < choiceShakeDuration && state.selectable != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, choiceShakeDuration));
            float angle = Mathf.Sin(t * Mathf.PI * 6f) * choiceShakeDegrees * (1f - t);
            state.selectable.transform.localRotation = state.originalRotation * Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }
        if (state.selectable != null)
            state.selectable.transform.localRotation = state.originalRotation;
        state.shakeRoutine = null;
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

    private bool IsOtherChoice(ControlState state)
    {
        return dimOtherChoicesOnSelection && state.toggle != null && !state.toggle.isOn &&
            controls.Exists(control => control.toggle != null && control.toggle.isActiveAndEnabled && control.toggle.isOn);
    }
    private float GetInteractionTargetAlpha(ControlState state)
    {
        if (IsOtherChoice(state)) return state.originalAlpha * otherChoiceAlpha;
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
        if (IsOtherChoice(state)) return state.originalScale * otherChoiceScale;
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
        if (nextPulseTarget != null) nextPulseTarget.localScale = nextOriginalScale;
        nextPulseTime = 0f;
        nextWasPulsing = false;
        showRoutine = null;
        interactionRoutines.Clear();

        foreach (ControlState control in controls)
        {
            control.hovered = false;
            control.pressed = false;
        }

        RestorePanelState();
        RestoreControlAlpha();
        RestoreEntranceItems();
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
            {
                control.selectable.transform.localScale = control.originalScale;
                control.selectable.transform.localRotation = control.originalRotation;
                control.shakeRoutine = null;
            }
        }
    }

    private static float EaseInQuart(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * t * t;
    }

    private static float ChoiceEntranceBump(float t)
    {
        // Reach 5% overshoot as the choice appears, then settle smoothly.
        if (t <= 0.75f) return 1f + 0.05f * EaseInQuart(t / 0.75f);
        return 1.05f - 0.05f * EaseOutCubic((t - 0.75f) / 0.25f);
    }
    private static float EaseOutCubic(float value)
    {
        float t = Mathf.Clamp01(value);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }
}

