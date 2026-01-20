namespace TMPro.Examples
{
    public class TMP_TextSelector_B : global::UnityEngine.MonoBehaviour, global::UnityEngine.EventSystems.IPointerEnterHandler, global::UnityEngine.EventSystems.IEventSystemHandler, global::UnityEngine.EventSystems.IPointerExitHandler, global::UnityEngine.EventSystems.IPointerClickHandler, global::UnityEngine.EventSystems.IPointerUpHandler
    {
        public global::UnityEngine.RectTransform TextPopup_Prefab_01;

        private global::UnityEngine.RectTransform m_TextPopup_RectTransform;

        private global::TMPro.TextMeshProUGUI m_TextPopup_TMPComponent;

        private const string k_LinkText = "You have selected link <#ffff00>";

        private const string k_WordText = "Word Index: <#ffff00>";

        private global::TMPro.TextMeshProUGUI m_TextMeshPro;

        private global::UnityEngine.Canvas m_Canvas;

        private global::UnityEngine.Camera m_Camera;

        private bool isHoveringObject;

        private int m_selectedWord;

        private int m_selectedLink;

        private int m_lastIndex;

        private global::UnityEngine.Matrix4x4 m_matrix;

        private global::TMPro.TMP_MeshInfo[] m_cachedMeshInfoVertexData;

        private void Awake()
        {
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
        }

        private void ON_TEXT_CHANGED(global::UnityEngine.Object obj)
        {
        }

        private void LateUpdate()
        {
        }

        public void OnPointerEnter(global::UnityEngine.EventSystems.PointerEventData eventData)
        {
        }

        public void OnPointerExit(global::UnityEngine.EventSystems.PointerEventData eventData)
        {
        }

        public void OnPointerClick(global::UnityEngine.EventSystems.PointerEventData eventData)
        {
        }

        public void OnPointerUp(global::UnityEngine.EventSystems.PointerEventData eventData)
        {
        }

        private void RestoreCachedVertexAttributes(int index)
        {
        }
    }
}
