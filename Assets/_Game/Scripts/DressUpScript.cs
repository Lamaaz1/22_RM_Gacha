using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class DressUpScript : MonoBehaviour
{
    public static DressUpScript instance;
    public GameObject PlayerTransform;
    public RectTransform PortraitPlayerTransform;
    public RectTransform PortraitPlayerParent;
    public RectTransform LandscapPlayerTransform;
    public RectTransform LandscapPlayerParent;
    public GameObject ActivdPlayer;
    public GameObject BtnActivdPlayer;
    public GameObject PlayerVdIcon;
    public Vector2 PlayerPosition;
    public List<GameObject> PlatersButtons;
    public List<GameObject> IconsVdButtons;
    public int ActivePlayerIndex=0;
    public ChildrenParts ActualPart;
    private void Awake()
    {
        PlayerPosition= PlayerTransform.transform.GetChild(0).localPosition;
        instance = this;
    }
    // Start is called before the first frame update
    void Start()
    {
        bool isLandscape = Screen.width > Screen.height;
        Lndscap = isLandscape;
        if (isLandscape)
        {
            PlayerTransform.GetComponent<RectTransform>().SetParent(LandscapPlayerParent);
            CopyRectTransform(LandscapPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            CopyChildZeroToAll(LandscapPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            Lndscap = true;
           
        }
        else
        {
            PlayerTransform.GetComponent<RectTransform>().SetParent(PortraitPlayerParent);
            CopyRectTransform(PortraitPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            CopyChildZeroToAll(PortraitPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            Lndscap = false;
        }
        ActivePlayer(0);
        init();
    }
    int ColorIndex;
    public void RePosPlayer()
    {
        foreach (RectTransform child in PlayerTransform.transform)
        {
            child.localPosition = PlayerPosition;
        }
    }
    public void init()
    {
        RePosPlayer();
        for (int i = 0; i < PlayerTransform.transform.childCount; i++)
        {
            ActivePlayer(i);
            Debug.Log(i);
            foreach (Transform child in PlayerTransform.transform.GetChild(i).transform)
            {
                if(child.gameObject.name!="0" && child.gameObject.name != "Hand")
                {
                    ChildrenParts part = (ChildrenParts)Enum.Parse(typeof(ChildrenParts), child.gameObject.name);
                    string SpritName;
                    SavePanel.instance.LoadCharacter(i, part, out SpritName);

                    SavePanel.instance.LoadColors(i, part, out ColorIndex);
                    Debug.Log(ColorIndex); //here 44
                    if (ColorIndex != 11111)
                    {
                        Debug.Log(SpritName);
                    }
                    if (SpritName != "")
                    {
                        Debug.Log(ColorIndex); // here 11111
                        Sprite s = Resources.Load<Sprite>("Sprites/" + child.gameObject.name + "/" + SpritName);
                        Debug.Log(ColorIndex + 111111111111);
                        DressUpp(s, child.gameObject.name);
                    }
                }
                             
            }
        }
        ActivePlayer(0);
       
    }
    public void ActivePlayer(int i)
    {
        ActivePlayerIndex = i;
        foreach (Transform child in PlayerTransform.transform)
        {
            child.gameObject.SetActive(false);
        }
        ActivdPlayer = PlayerTransform.transform.GetChild(i).gameObject;
        BtnActivdPlayer = PlatersButtons[i];
        PlayerVdIcon = IconsVdButtons[i];

        ActivdPlayer.SetActive(true);
    }
  
    
    public void DressUp(Sprite s, string PartName )
    { 
            foreach (Transform child in ActivdPlayer.transform)
            {
                if (child.name == PartName)
                {
                    Color c = new Color();
                c = child.GetComponent<Image>().color;
                if (s.name == "0")
                    {
                        c.a = 0;
                    }
                    else
                    {
                        c.a = 1;
                    }
               
                    child.GetComponent<Image>().color = c;
                    child.GetComponent<Image>().sprite = s;
             

                }

            }
       
        foreach (Transform child in BtnActivdPlayer.transform)
        {
            Debug.Log("ok find");
            //BtnActivdPlayer.SetActive(false);
            if (child.name == PartName)
            {
                Color c = new Color();
                c = child.GetComponent<Image>().color;
                if (s.name == "0")
                {
                    c.a = 0;
                }
                else
                {
                    c.a = 1;
                }
              

                child.GetComponent<Image>().color = c;
                child.GetComponent<Image>().sprite = s;
               

            }
          

        }
        foreach (Transform child in PlayerVdIcon.transform)
        {
            Debug.Log("ok find");
            //BtnActivdPlayer.SetActive(false);
            if (child.name == PartName)
            {
                Color c = new Color();
                c = child.GetComponent<Image>().color;
                if (s.name == "0")
                {
                    c.a = 0;
                }
                else
                {
                    c.a = 1;
                }


                child.GetComponent<Image>().color = c;
                child.GetComponent<Image>().sprite = s;


            }


        }
    }
    public void DressUpp(Sprite s, string PartName)
    {
        foreach (Transform child in ActivdPlayer.transform)
        {
            if (child.name == PartName)
            {
                Color c = new Color();
                c = child.GetComponent<Image>().color;
                if (s.name == "0")
                {
                    c.a = 0;
                }
                else
                {
                    c.a = 1;
                }

                child.GetComponent<Image>().color = c;
                child.GetComponent<Image>().sprite = s;
                Debug.Log(ColorIndex);
                if (ColorIndex != 11111)
                {
                    child.GetComponent<Image>().color = UiMnager.instance.colorPicker.ColoratItem(ColorIndex);
                }
            }

        }

        foreach (Transform child in BtnActivdPlayer.transform)
        {
            Debug.Log("ok find");
            //BtnActivdPlayer.SetActive(false);
            if (child.name == PartName)
            {
                Color c = new Color();
                c = child.GetComponent<Image>().color;
                if (s.name == "0")
                {
                    c.a = 0;
                }
                else
                {
                    c.a = 1;
                }


                child.GetComponent<Image>().color = c;
                child.GetComponent<Image>().sprite = s;
                Debug.Log(ColorIndex);
                if (ColorIndex != 11111)
                {
                    child.GetComponent<Image>().color = UiMnager.instance.colorPicker.ColoratItem(ColorIndex);
                }

            }
        }
        foreach (Transform child in PlayerVdIcon.transform)
        {
            Debug.Log("ok find");
            //BtnActivdPlayer.SetActive(false);
            if (child.name == PartName)
            {
                Color c = new Color();
                c = child.GetComponent<Image>().color;
                if (s.name == "0")
                {
                    c.a = 0;
                }
                else
                {
                    c.a = 1;
                }


                child.GetComponent<Image>().color = c;
                child.GetComponent<Image>().sprite = s;

                Debug.Log(ColorIndex);
                if (ColorIndex != 11111)
                {
                    child.GetComponent<Image>().color = UiMnager.instance.colorPicker.ColoratItem(ColorIndex);
                }
            }


        }
    }
    Image C;
    Image CI;
    Image CIC;
    public void ChangeColor(string PartName,out Image CharacterPart,out Image CharacterPartPnl, out Image CharacterIconVd)
    {
              
        foreach (Transform child in ActivdPlayer.transform)
        {
            if (child.name == PartName)
            {

                C= child.GetComponent<Image>();
            }
        }
        foreach (Transform child in BtnActivdPlayer.transform)
        {
            if (child.name == PartName)
            {

                CI = child.GetComponent<Image>();
            }
        }
        foreach (Transform child in PlayerVdIcon.transform)
        {
            if (child.name == PartName)
            {

                CIC = child.GetComponent<Image>();
            }
        }
        CharacterPart = C;
        CharacterPartPnl = CI;
        CharacterIconVd = CIC;

    }
    // Update is called once per frame
    void Update()
    {
        
    }
    public void UnlockAllCharacters()
    {
        foreach(GameObject child in PlatersButtons)
        {
            child.transform.parent.GetComponent<CharacterButtonScrpt>().Unlock();
        }
    }
    public void PortraiSwitch()
    {
        //if (Lndscap)
        //{
            PlayerTransform.GetComponent<RectTransform>().SetParent(PortraitPlayerParent);
            CopyRectTransform(PortraitPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            CopyChildZeroToAll(PortraitPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            Lndscap = false;
        //}

    }
     public bool Lndscap;
    public void LandscapSwitch()
    {
        //if(!Lndscap)
        //{
            PlayerTransform.GetComponent<RectTransform>().SetParent(LandscapPlayerParent);
            CopyRectTransform(LandscapPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            CopyChildZeroToAll(LandscapPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            Lndscap = true;
        //}
        

    }
    public static void CopyRectTransform(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;

        to.anchoredPosition = from.anchoredPosition;
        to.sizeDelta = from.sizeDelta;

        to.offsetMin = from.offsetMin;
        to.offsetMax = from.offsetMax;

        to.localScale = from.localScale;
        to.localRotation = from.localRotation;
    }
    public static void CopyChildZeroToAll(RectTransform from, RectTransform to)
    {
        if (from.childCount == 0) return;

        RectTransform source = from.GetChild(0) as RectTransform;

        for (int i = 0; i < to.childCount; i++)
        {
            RectTransform target = to.GetChild(i) as RectTransform;
            CopyRectTransform(source, target);
        }
    }

}
