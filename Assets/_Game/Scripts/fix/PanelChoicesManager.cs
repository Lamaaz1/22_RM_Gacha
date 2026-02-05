using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PanelChoicesManager : MonoBehaviour
{
    public static PanelChoicesManager Instance;
    public PanelChoices portraitPanel;
    public PanelChoices landscapePanel;

    private void Awake()
    {
        Instance = this;
    }
    public PanelChoices ActivePanel()
    {
        return Screen.width > Screen.height
            ? landscapePanel
            : portraitPanel;
    }

    public void ShowPanel(string t, List<ChildrenParts> parts)
    {
        ActivePanel().ShowPanel(t, parts);
    }
    public void ChangeColor(Color c)
    {
        ActivePanel().ChangeColor(c);
    }
    public void HidePanel()
    {
        ActivePanel().HidePanel();
    }
    public void SetCharacterImages(
    Image img,
    Image imgPnl,
    Image iconImg)
    {
        ActivePanel().SetCharacterImages(img, imgPnl, iconImg);
    }

    public void ShowColorPicker()
    {
        ActivePanel().colorPicker.gameObject.SetActive(true);
    }

}
