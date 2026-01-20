using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ColorBtn : MonoBehaviour
{
    public Image SelectedImg;
  
    public int i;
    //[SerializeField] Image ImgColor; 
    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void ChangeColor(Image ImgColor)
    {
        //SelectedImg.color = ImgColor.color;
        PanelChoices.Instance.ChangeColor(ImgColor.color);
        SavePanel.instance.SaveColorPart(DressUpScript.instance.ActivePlayerIndex, DressUpScript.instance.ActualPart, i);
    }
    
}
