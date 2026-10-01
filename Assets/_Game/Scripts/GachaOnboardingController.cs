using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Four editable choice pages. Preferences are committed only after the last page.</summary>
public sealed class GachaOnboardingController : MonoBehaviour
{
    public const string PreferencePrefix = "GachaNox.Onboarding.";
    public static readonly string[] PreferenceKeys = { "Character", "Style", "World", "Activity" };

    [Serializable]
    public sealed class Choice
    {
        public string id;
        public string label;
        public Button button;
        public GameObject selectedVisual;
        public bool locked;
        public GameObject lockedVisual;
    }

    [Serializable]
    public sealed class Page
    {
        public GameObject panel;
        public Choice[] choices;
    }

    [Header("Four pages in order: character, style, world, activity")]
    public Page[] pages;
    public Button backButton;
    public Button nextButton;
    public TMP_Text nextLabel;
    public TMP_Text progressLabel;
    public TMP_Text selectionLabel;
    public TMP_Text summaryLabel;
    public Image[] stepMarkers;
    public TMP_Text[] stepNumbers;
    public Image background;
    public Image backgroundWash;
    public Sprite defaultBackground;
    public Sprite[] worldBackgrounds;
    public RectTransform safeArea;
    public RectTransform designSurface;
    public UnityEvent onCompleted = new UnityEvent();

    private readonly int[] selections = { -1, -1, -1, -1 };
    private int currentPage;
    private bool bound;
    private bool completed;
    private CanvasGroup[] pageGroups;
    private Vector3[] pagePositions;
    private Vector3[][] cardScales;
    private float entranceTime = -1f;
    private float selectionTime = -1f;
    private int entranceDirection = 1;
    private Vector3 nextScale;
    private bool motionReady;
    public int CurrentPage => currentPage;
    public bool IsComplete => completed;
    public int GetSelection(int page) => page >= 0 && page < selections.Length ? selections[page] : -1;

    private void OnEnable()
    {
        if (Application.isPlaying) Begin();
    }

    public void Begin(bool restorePreferences = true)
    {
        if (pages == null || pages.Length != 4) return;
        BindButtons();
        currentPage = 0;
        completed = false;
        for (int p = 0; p < pages.Length; p++)
        {
            selections[p] = -1;
            if (!restorePreferences) continue;
            string id = PlayerPrefs.GetString(PreferencePrefix + PreferenceKeys[p], "");
            for (int c = 0; c < pages[p].choices.Length; c++)
                if (!pages[p].choices[c].locked && pages[p].choices[c].id == id) selections[p] = c;
        }
        Refresh();
        FitToSafeArea();
        AnimateEntrance(1);
    }

    private void BindButtons()
    {
        if (bound) return;
        for (int p = 0; p < pages.Length; p++)
        for (int c = 0; c < pages[p].choices.Length; c++)
        {
            int page = p, choice = c;
            pages[p].choices[c].button.onClick.AddListener(() => SelectChoice(page, choice));
        }
        backButton.onClick.AddListener(Previous);
        nextButton.onClick.AddListener(Next);
        bound = true;
    }

    public void SelectChoice(int page, int choice)
    {
        if (completed || page != currentPage || page < 0 || page >= pages.Length || choice < 0 || choice >= pages[page].choices.Length || pages[page].choices[choice].locked) return;
        selections[page] = choice;
        Refresh();
        if (Application.isPlaying && motionReady) selectionTime = Time.unscaledTime;
    }

    public void Previous()
    {
        if (completed || currentPage == 0) return;
        currentPage--;
        Refresh();
        AnimateEntrance(-1);
    }

    public void Next()
    {
        if (completed || !HasAvailableSelection(currentPage)) return;
        if (currentPage < pages.Length - 1)
        {
            currentPage++;
            Refresh();
            AnimateEntrance(1);
            return;
        }
        for (int p = 0; p < pages.Length; p++)
        {
            if (HasAvailableSelection(p)) continue;
            currentPage = p;
            Refresh();
            AnimateEntrance(-1);
            return;
        }
        for (int p = 0; p < pages.Length; p++)
            PlayerPrefs.SetString(PreferencePrefix + PreferenceKeys[p], pages[p].choices[selections[p]].id);
        PlayerPrefs.SetInt(PreferencePrefix + "Completed", 1);
        PlayerPrefs.Save();
        completed = true;
        nextButton.interactable = false;
        backButton.interactable = false;
        nextLabel.text = "Loading...";
        onCompleted.Invoke();
    }

    private void Refresh()
    {
        Color pink = new Color(0.96f, 0.47f, 0.75f);
        Color ink = new Color(0.23f, 0.15f, 0.34f);
        for (int p = 0; p < pages.Length; p++)
        {
            pages[p].panel.SetActive(p == currentPage);
            for (int c = 0; c < pages[p].choices.Length; c++)
            {
                Choice choice = pages[p].choices[c];
                choice.button.interactable = !choice.locked;
                choice.selectedVisual.SetActive(!choice.locked && selections[p] == c);
                if (choice.lockedVisual != null) choice.lockedVisual.SetActive(choice.locked);
            }
            stepMarkers[p].color = p <= currentPage ? pink : new Color(0.79f, 0.74f, 0.89f);
            stepNumbers[p].color = p <= currentPage ? Color.white : ink;
        }
        backButton.interactable = currentPage > 0;
        nextButton.interactable = HasAvailableSelection(currentPage);
        nextLabel.text = currentPage == 3 ? "Let's Play!" : "Next  >";
        progressLabel.text = $"{currentPage + 1} / 4";
        selectionLabel.text = !HasAvailableSelection(currentPage)
            ? (currentPage == 3 ? "Choose Gacha Nox to start dressing" : "Pick one card to continue")
            : pages[currentPage].choices[selections[currentPage]].label + " selected";
        string summary = "";
        for (int p = 0; p < pages.Length; p++)
        {
            if (selections[p] < 0) continue;
            if (summary.Length > 0) summary += "  /  ";
            summary += pages[p].choices[selections[p]].label;
        }
        summaryLabel.text = summary.Length > 0 ? summary : "Your character. Your style. Your world.";
        if (background != null)
            background.sprite = selections[2] >= 0 && selections[2] < worldBackgrounds.Length ? worldBackgrounds[selections[2]] : defaultBackground;
        if (backgroundWash != null)
            backgroundWash.color = selections[1] == 1 ? new Color(0.88f, 0.82f, 0.95f, 0.86f)
                : selections[1] == 2 ? new Color(0.82f, 0.94f, 1f, 0.82f)
                : new Color(0.94f, 0.90f, 1f, 0.78f);
    }

    private bool HasAvailableSelection(int page)
    {
        int choice = selections[page];
        return choice >= 0 && choice < pages[page].choices.Length && !pages[page].choices[choice].locked;
    }

    private void LateUpdate()
    {
        FitToSafeArea();
        UpdateMotion();
    }

    // Cache the authored layout once. Motion always starts from those values,
    // including after rapid navigation or reopening a disabled panel.
    private void AnimateEntrance(int direction)
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (!motionReady)
        {
            pageGroups = new CanvasGroup[pages.Length];
            pagePositions = new Vector3[pages.Length];
            cardScales = new Vector3[pages.Length][];
            for (int p = 0; p < pages.Length; p++)
            {
                var panel = pages[p].panel;
                pageGroups[p] = panel.GetComponent<CanvasGroup>();
                if (pageGroups[p] == null) pageGroups[p] = panel.AddComponent<CanvasGroup>();
                pagePositions[p] = panel.transform.localPosition;
                cardScales[p] = new Vector3[pages[p].choices.Length];
                for (int c = 0; c < cardScales[p].Length; c++)
                    cardScales[p][c] = pages[p].choices[c].button.transform.localScale;
            }
            nextScale = nextButton.transform.localScale;
            motionReady = true;
        }
        ResetMotion();
        entranceDirection = direction;
        entranceTime = Time.unscaledTime;
        UpdateMotion();
    }

    private void UpdateMotion()
    {
        if (!Application.isPlaying || !motionReady) return;
        float elapsed = entranceTime < 0f ? 1f : Time.unscaledTime - entranceTime;
        float progress = Mathf.Clamp01(elapsed / .34f);
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        pageGroups[currentPage].alpha = eased;
        pages[currentPage].panel.transform.localPosition = pagePositions[currentPage]
            + Vector3.right * (entranceDirection * 75f * (1f - eased));
        float clickAge = selectionTime < 0f ? 1f : Time.unscaledTime - selectionTime;
        float pulse = clickAge < .28f ? Mathf.Sin(clickAge / .28f * Mathf.PI) : 0f;
        for (int c = 0; c < pages[currentPage].choices.Length; c++)
        {
            float t = Mathf.Clamp01((elapsed - c * .085f) / .42f);
            // Pop in from zero, slightly overshoot, then settle at the authored size.
            float remaining = t - 1f;
            float scale = t <= 0f ? 0f : 1f + 2.45f * remaining * remaining * remaining
                + 1.45f * remaining * remaining;
            if (selections[currentPage] == c) scale += .035f * pulse;
            pages[currentPage].choices[c].button.transform.localScale = cardScales[currentPage][c] * scale;
        }
        nextButton.transform.localScale = nextScale * (1f + .045f * pulse);
    }

    private void ResetMotion()
    {
        entranceTime = selectionTime = -1f;
        if (!motionReady) return;
        for (int p = 0; p < pages.Length; p++)
        {
            pageGroups[p].alpha = 1f;
            pages[p].panel.transform.localPosition = pagePositions[p];
            for (int c = 0; c < cardScales[p].Length; c++)
                pages[p].choices[c].button.transform.localScale = cardScales[p][c];
        }
        nextButton.transform.localScale = nextScale;
    }

    private void OnDisable() { ResetMotion(); }

    public void FitToSafeArea()
    {
        if (safeArea == null || designSurface == null || Screen.width <= 0 || Screen.height <= 0) return;
        Rect area = Screen.safeArea;
        safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        float scale = Mathf.Min(safeArea.rect.width / 2560f, safeArea.rect.height / 1440f);
        designSurface.localScale = Vector3.one * Mathf.Max(0.01f, scale);
    }
}
