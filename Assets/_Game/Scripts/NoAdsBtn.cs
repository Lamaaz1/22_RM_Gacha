using UnityEngine;
using UnityEngine.UI;

public class NoAdsBtn : MonoBehaviour
{
    public Button MyBtn;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        MyBtn = GetComponent<Button>();
        if (PlayerPrefs.GetInt("RemoveAds") == 1)
        {
            MyBtn.interactable = false;

        }
        //MyBtn.onClick.AddListener(Onclick);
    }
    public void Onclick()
    {
        AdsController.Instance.NoAds();
        MyBtn.interactable = false;
        DressUpScript.instance.UnlockAllCharacters();
    }
    // Update is called once per frame
    void Update()
    {

    }
}
