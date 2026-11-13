using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class RemoteImageFallbackLabel : MonoBehaviour
{
    public Image artwork;
    public GameObject fallbackLabel;
    [SerializeField] private Sprite localSprite;

    private void Awake()
    {
        CaptureLocalSprite();
        Refresh();
    }

    private void OnEnable()
    {
        CaptureLocalSprite();
        Refresh();
    }

    private void OnValidate()
    {
        CaptureLocalSprite();
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void CaptureLocalSprite()
    {
        if (artwork == null)
            artwork = GetComponent<Image>();
        if (localSprite == null && artwork != null)
            localSprite = artwork.sprite;
    }

    private void Refresh()
    {
        if (artwork == null || fallbackLabel == null)
            return;

        bool showFallback = artwork.sprite == null || artwork.sprite == localSprite;
        if (fallbackLabel.activeSelf != showFallback)
            fallbackLabel.SetActive(showFallback);
    }
}
