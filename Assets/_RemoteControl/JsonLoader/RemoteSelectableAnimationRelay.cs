using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Added to buttons and toggles at runtime. It forwards pointer interaction to
// the panel animator without changing each control's authored transition setup.
[AddComponentMenu("ScenePilot/Selectable Animation Relay")]
public sealed class RemoteSelectableAnimationRelay : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [SerializeField, Tooltip("Panel animator containing the editable entrance, shake and selection settings.")] private RemoteStaticPanelAnimator owner;
    [SerializeField, Tooltip("Button or Toggle animated by this relay.")] private Selectable selectable;

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
