using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class ScenePilotSequenceExit : MonoBehaviour
{
    public RemoteContentController controller;
    [HideInInspector]
    public ScenePilotNativeReviewController nativeReview;

    [Header("NORMAL SEQUENCE EXIT")]
    [Tooltip("Optional gameplay root enabled when the last normal static panel finishes.")]
    public GameObject gameplayRoot;

    [Tooltip("Optional ScenePilot UI root hidden after gameplay and callbacks are enabled.")]
    public GameObject scenePilotRoot;

    [Tooltip("Leave off until Scene Pilot Root is intentionally assigned to a dedicated UI wrapper.")]
    public bool hideScenePilotRootOnFinish;

    public UnityEvent onContinueToGame = new UnityEvent();

    private void Reset()
    {
        controller = GetComponent<RemoteContentController>();
        nativeReview = GetComponent<ScenePilotNativeReviewController>();
    }

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<RemoteContentController>();

        // Keep this runtime fallback so older prefabs automatically gain the
        // native-review timer without requiring their serialized data to be rebuilt.
        if (nativeReview == null)
            nativeReview = ScenePilotNativeReviewController.instance;
        if (nativeReview == null)
        {
            GameObject reviewHost = new GameObject("ScenePilot Native Review");
            nativeReview = reviewHost.AddComponent<ScenePilotNativeReviewController>();
        }
        nativeReview.remoteContent = controller;
    }

    private void OnEnable()
    {
        if (controller == null)
            controller = GetComponent<RemoteContentController>();

        if (controller != null)
        {
            controller.onStaticSequenceFinished.RemoveListener(ContinueToGame);
            controller.onStaticSequenceFinished.AddListener(ContinueToGame);
        }
    }

    private void OnDisable()
    {
        if (controller != null)
            controller.onStaticSequenceFinished.RemoveListener(ContinueToGame);
    }

    public void ContinueToGame()
    {
        if (gameplayRoot != null)
            gameplayRoot.SetActive(true);

        onContinueToGame?.Invoke();

        if (hideScenePilotRootOnFinish && scenePilotRoot != null)
            scenePilotRoot.SetActive(false);
    }
}
