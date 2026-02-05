using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class ChoicesPrefabs : MonoBehaviour
{
    [SerializeField] Button ChoicesParent;
    public List<Sprite> images;
    public List<Sprite> iCons;
    private int currentPage = 0;
    private int imagesPerPage = 1;
    ChildrenParts PartNamee;
    public Button nextButton;
    public Button previousButton;
    public Button ColorP;
    public Text Name;
    public Text Index;
    // Start is called before the first frame update
    void Start()
    {
        nextButton.onClick.AddListener(NextPage);
        previousButton.onClick.AddListener(PreviousPage);
        ColorP.onClick.AddListener(ChooseColor);
    }

    // Update is called once per frame
    void Update()
    {
       
    }
    public int index=0;

    public int IndexS(out int indeex)
    {
        indeex = index;
        return index;
    }
    public void Remplir(ChildrenParts PartName)
    {
        PartNamee = PartName;
        Name.text=PartName.ToString();
        LoadSprites();
       
    }
    void LoadSprites()
    {
        // Load all sprites from the Resources/Sprites folder
        Sprite[] loadedSprites = Resources.LoadAll<Sprite>("Sprites/"+ PartNamee.ToString());
        Sprite[] loadedIcons = Resources.LoadAll<Sprite>("Sprites/" + PartNamee.ToString()+"Icons");

        if (loadedSprites.Length > 0)
        {
            images.AddRange(loadedSprites);
            iCons.AddRange(loadedIcons);
            Debug.Log($"Loaded {images.Count} sprites.");
        }
        else
        {
            Debug.Log(PartNamee.ToString());
            Debug.LogError("No sprites found in Resources/Sprites.");
        }
        foreach (Transform child in DressUpScript.instance.ActivdPlayer.transform)
        {
            if (child.name == PartNamee.ToString())
            {
                for(int i = 0; i < images.Count; i++) 
                {
                    if (images[i] == child.GetComponent<Image>().sprite)
                    {
                        index = i;
                        currentPage = index;
                    }
                }
            }

        }
        ShwImg();
    }
    public void ShowImage()
    {
        if (iCons.Count > 0)
        {         
            ChoicesParent.GetComponent<Image>().sprite = iCons[currentPage];
            ChoicesParent.GetComponent<Image>().type = Image.Type.Simple;
            ChoicesParent.GetComponent<Image>().preserveAspect = true;
            Index.text= currentPage+"/"+(iCons.Count-1).ToString();
            DressUpScript.instance.DressUp(images[currentPage],PartNamee.ToString()/*, currentPag*/);
            SavePanel.instance.SaveCharacter(DressUpScript.instance.ActivePlayerIndex, PartNamee, images[currentPage].name);
            DressUpScript.instance.ActualPart = PartNamee;
          
        }
    }
    public void ShwImg()
    {
        if (iCons.Count > 0)
        {
            //Debug.Log("okkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk");
            //IndexS(out currentPage);
            ChoicesParent.GetComponent<Image>().sprite = iCons[index];
            ChoicesParent.GetComponent<Image>().type = Image.Type.Simple;
            ChoicesParent.GetComponent<Image>().preserveAspect = true;
            Index.text = index + "/" + (iCons.Count - 1).ToString();
            DressUpScript.instance.DressUp(images[index], PartNamee.ToString()/*, currentPag*/);
            SavePanel.instance.SaveCharacter(DressUpScript.instance.ActivePlayerIndex, PartNamee, images[index].name);
            DressUpScript.instance.ActualPart = PartNamee;
        }
    }
    void NextPage()
    {
        if ((currentPage + 1) * imagesPerPage < iCons.Count)
        {
            currentPage++;
            ShowImage();
        }
    }

    void PreviousPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            ShowImage();
        }
    }
    public Image CharacterSelectedImg;
    public Image CharacterSelectedImgPnl;
    public Image CharacterIconImgPnl;
    public void ChooseColor()
    {
        string partt = PartNamee.ToString();
        PanelChoicesManager.Instance.ActivePanel().colorPicker.SelectedImg = ChoicesParent.image;
        DressUpScript.instance.ChangeColor(partt, out CharacterSelectedImg,out CharacterSelectedImgPnl,out CharacterIconImgPnl);
        PanelChoicesManager.Instance.ActivePanel().CharacterSelectedImg = CharacterSelectedImg;
        PanelChoicesManager.Instance.ActivePanel().CharacterSelectedImgPnl = CharacterSelectedImgPnl;
        PanelChoicesManager.Instance.ActivePanel().CharacterIconImgPnl = CharacterIconImgPnl;
        PanelChoicesManager.Instance.ActivePanel().colorPicker.gameObject.SetActive(true);
    }
    public void ChangePart(int index, string partName)
    {
        if (index == 0) { }
        else
        {
            
        }
    }
}

