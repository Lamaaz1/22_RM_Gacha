
using UnityEngine;
using UnityEngine.UI;

public class InterstitialAds : MonoBehaviour
{

    public Button mybnt;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        mybnt = GetComponent<Button>();

        if (mybnt != null)
        {
            mybnt.onClick.AddListener(() => { if (AdsController.Instance != null) { AdsController.Instance.ShowInterstitialAd(); } } );
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
