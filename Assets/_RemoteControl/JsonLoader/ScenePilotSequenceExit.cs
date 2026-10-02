using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class ScenePilotSequenceExit : MonoBehaviour
{
    public RemoteContentController controller;

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
    }

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<RemoteContentController>();
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
