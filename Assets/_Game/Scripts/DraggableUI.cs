using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableUI : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private RectTransform rectTransform;
    private Canvas canvas;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // This method is required for the drag to be detected
    }

    public void OnDrag(PointerEventData eventData)
    {
        if(UiMnager.instance.AnimationMode)
        {
            if (canvas == null) return;
            rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
        }
      
    }
}