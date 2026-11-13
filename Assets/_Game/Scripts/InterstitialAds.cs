using UnityEngine;
using UnityEngine.UI;

public class InterstitialAds : MonoBehaviour
{
    public Button mybnt;

    private void Start()
    {
        mybnt = GetComponent<Button>();
        if (mybnt != null)
            mybnt.onClick.AddListener(ShowInterstitial);
    }

    private void OnDestroy()
    {
        if (mybnt != null)
            mybnt.onClick.RemoveListener(ShowInterstitial);
    }

    private void ShowInterstitial()
    {
        var ads = ScenePilotAdsController.instance;
        if (ads != null)
            ads.ShowInterstitial();
    }
}
