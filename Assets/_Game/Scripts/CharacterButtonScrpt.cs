using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CharacterButtonScrpt : MonoBehaviour
{
    public Button myBtn;
    public GameObject Lock;
    public int Index;
    public bool Active;
    // Start is called before the first frame update
    void Start()
    {
        myBtn = GetComponent<Button>();
        myBtn.onClick.AddListener(Onclick);
        if(Active || PlayerPrefs.GetInt("Character"+Index)==1 || PlayerPrefs.GetInt("RemoveAds") == 1)
        {
            Open();
        }
        else
            Close();
    }
    public void Onclick()
    {
        if (Active || PlayerPrefs.GetInt("Character" + Index) == 1 || PlayerPrefs.GetInt("RemoveAds") == 1)
        {
            DressUpScript.instance.ActivePlayer(Index);
        }
        else
        {
            AdsController.Instance.ShowRewardedInterstitialAd(Unlock);
        }  
    }
    // Update is called once per frame
    void Update()
    {
        
    }
    public void Unlock()
    {
        PlayerPrefs.SetInt("Character" + Index, 1);
             Open();
    }
    public void Open()
    {
        Lock.SetActive(false);
    }
    public void Close()
    {
        Lock.SetActive(true);
    }
}
