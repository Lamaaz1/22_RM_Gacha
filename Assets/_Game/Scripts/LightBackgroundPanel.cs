using UnityEngine;
using UnityEngine.UI;

public class LightBackgroundPanel : MonoBehaviour
{
    public BackgroundBtn[] items;
    public Image[] cards;
    public GameObject[] badges;
    public Sprite normalCard, selectedCard;
    public Button previousButton, nextButton;
    public Text pageLabel;
    private int page;
    private bool bound;
    private int selected = int.MinValue;
    public int Page => page;
    public int PageCount => Mathf.Max(1, (items.Length + 11) / 12);
    private void OnEnable() { Initialize(); }
    private void LateUpdate()
    {
        if (selected != PlayerPrefs.GetInt("Background")) Refresh();
    }
    public void Initialize()
    {
        if (items == null || previousButton == null || nextButton == null) return;
        if (!bound)
        {
            previousButton.onClick.AddListener(() => ChangePage(-1));
            nextButton.onClick.AddListener(() => ChangePage(1));
            bound = true;
        }
        Refresh();
    }
    public void ChangePage(int direction)
    {
        page = Mathf.Clamp(page + direction, 0, PageCount - 1);
        Refresh();
    }
    public void Refresh()
    {
        if (items == null || cards == null || badges == null || pageLabel == null) return;
        selected = PlayerPrefs.GetInt("Background");
        for (int i = 0; i < items.Length; i++)
        {
            items[i].gameObject.SetActive(i / 12 == page);
            bool active = items[i].i == selected;
            cards[i].sprite = active ? selectedCard : normalCard;
            badges[i].SetActive(active);
        }
        previousButton.interactable = page > 0;
        nextButton.interactable = page < PageCount - 1;
        pageLabel.text = (page + 1) + " / " + PageCount;
    }
}
