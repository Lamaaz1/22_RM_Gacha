using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RemoteStaticPanel))]
public class RemoteRateUsPanel : MonoBehaviour
{
    public RemoteStaticPanel staticPanel;
    [Min(1f)] public float reviewTimeoutSeconds = 15f;
    private bool requested;
    private bool reviewFinished;
    private bool advancePending;
    private int requestGeneration;

    private void Awake()
    {
        staticPanel = GetComponent<RemoteStaticPanel>();
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    public void PresentNativeReview()
    {
        if (requested)
        {
            if (reviewFinished) advancePending = true;
            return;
        }
        requested = true;
        StartCoroutine(PresentAndContinue());
    }

    private IEnumerator PresentAndContinue()
    {
        // Never navigate inside SetStaticPanelVisible's loop. A synchronous
        // fallback must not have its next panel hidden by the outer loop.
        yield return null;
        RemoteContentController controller = FindAnyObjectByType<RemoteContentController>();
        while (controller != null && IsVisible() && !controller.CanAdvanceStaticPanel(staticPanel))
            yield return null;
        if (!IsVisible()) yield break;

        int generation = requestGeneration;
        try
        {
            ScenePilotNativeReviewController review = ScenePilotNativeReviewController.instance;
            if (review == null)
                review = new GameObject("ScenePilot Native Review").AddComponent<ScenePilotNativeReviewController>();
            review.RequestAndroidReviewNow(() => { if (generation == requestGeneration) reviewFinished = true; });
        }
        catch (Exception error)
        {
            Debug.LogWarning("[ScenePilot/RateUs] Review unavailable; continuing. " + error.Message, this);
            reviewFinished = true;
        }

        float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, reviewTimeoutSeconds);
        while (!reviewFinished && IsVisible() && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (!reviewFinished)
            Debug.LogWarning("[ScenePilot/RateUs] Review request timed out; continuing the sequence.", this);
        reviewFinished = true;
        advancePending = true;
        // Always leave the review callback before changing navigation state.
        yield return null;
        ContinueWhenReady();
    }

    private void OnDisable()
    {
        requestGeneration++;
        StopAllCoroutines();
        requested = false;
        reviewFinished = false;
        advancePending = false;
    }

    private bool IsVisible()
    {
        return staticPanel != null && staticPanel.panelRoot != null && staticPanel.panelRoot.activeInHierarchy;
    }

    private void Update()
    {
        if (advancePending) ContinueWhenReady();
    }

    private void ContinueWhenReady()
    {
        if (!IsVisible()) { advancePending = false; return; }
        RemoteContentController controller = FindAnyObjectByType<RemoteContentController>();
        if (controller == null)
        {
            advancePending = false;
            staticPanel.panelRoot.SetActive(false);
            return;
        }
        if (!controller.CanAdvanceStaticPanel(staticPanel)) return;
        advancePending = false;
        controller.ShowNextStaticPanel(staticPanel);
    }

    public void RateNow() => PresentNativeReview();
    public void Later() { advancePending = true; }
    public void Close() { advancePending = true; }
    public void ConfigureRemoteStoreUrls(string androidUrl, string iosUrl) { }
}
