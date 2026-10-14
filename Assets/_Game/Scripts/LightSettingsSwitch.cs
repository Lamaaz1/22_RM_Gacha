using UnityEngine;
using UnityEngine.UI;

// Uses the existing settings indicator as the source of truth.
public class LightSettingsSwitch : MonoBehaviour
{
    public GameObject enabledIndicator;
    public Image track;
    public RectTransform thumb;
    public Text status;
    public Sprite onSprite, offSprite;
    private void OnEnable() { Refresh(); }
    private void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        if (enabledIndicator == null || track == null || thumb == null || status == null) return;
        bool on = enabledIndicator.activeSelf;
        track.sprite = on ? onSprite : offSprite;
        track.color = on ? Color.white : new Color(.78f, .71f, .89f);
        thumb.anchorMin = new Vector2(on ? .65f : .06f, .16f);
        thumb.anchorMax = new Vector2(on ? .94f : .35f, .84f);
        thumb.offsetMin = thumb.offsetMax = Vector2.zero;
        status.text = on ? "ON" : "OFF";
        status.color = on ? Color.white : new Color(.45f, .38f, .58f);
        status.rectTransform.anchorMin = new Vector2(on ? .07f : .37f, .1f);
        status.rectTransform.anchorMax = new Vector2(on ? .63f : .95f, .9f);
        status.rectTransform.offsetMin = status.rectTransform.offsetMax = Vector2.zero;
    }
}
