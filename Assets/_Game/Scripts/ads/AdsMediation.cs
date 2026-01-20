using System;
using UnityEngine;
using Unity.Services.LevelPlay;

public class AdsMediation : MonoBehaviour
{
    [SerializeField] private string appKey;
    [SerializeField] private string rewardedAdUnitId;
    [SerializeField] private string interstitialAdUnitId;
    [SerializeField] private string bannerAdUnitId;

    private LevelPlayRewardedAd rewardedAd;
    private LevelPlayInterstitialAd interstitialAd;
    private LevelPlayBannerAd bannerAd;

    private Action rewardCallback;
    public static float lastTimeShowAds;

   
    public void Init()
    {
        Debug.Log("Initializing LevelPlay...");

        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;

        LevelPlay.Init(appKey);
    }

    private void OnInitSuccess(LevelPlayConfiguration config)
    {
        Debug.Log("✅ LevelPlay Initialized");

        // Setup rewarded ad
        rewardedAd = new LevelPlayRewardedAd(rewardedAdUnitId);
        rewardedAd.OnAdRewarded += (reward, info) => {
            rewardCallback?.Invoke();
            rewardCallback = null;
        };
        rewardedAd.OnAdClosed += info => {
            rewardedAd.LoadAd();
        };
        rewardedAd.LoadAd();

        // Setup interstitial ad
        interstitialAd = new LevelPlayInterstitialAd(interstitialAdUnitId);
        interstitialAd.OnAdClosed += info => {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            lastTimeShowAds = Time.realtimeSinceStartup;
            interstitialAd.LoadAd();
            //if(GameLoaderWithResources.instance != null) 
            //{
            //    GameLoaderWithResources.instance.ReplayGame();
            //}
           
        };
        interstitialAd.LoadAd();

        // Setup banner
        //bannerAd = new LevelPlayBannerAd(bannerAdUnitId, LevelPlayBannerPosition.BottomCenter);
        //bannerAd.LoadAd();
    }

    private void OnInitFailed(LevelPlayInitError error)
    {
        Debug.LogError("LevelPlay failed to initialize: " + error);
    }

    public void ShowRewardAd(Action onRewarded, string placement)
    {
        if (rewardedAd != null && rewardedAd.IsAdReady())
        {
            rewardCallback = onRewarded;
            rewardedAd.ShowAd();
        }
    }

    public bool ShowInterstital()
    {
        if (interstitialAd != null && interstitialAd.IsAdReady())
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
            interstitialAd.ShowAd();
            return true;
        }
        return false;
    }

    public void ShowBanner(bool show)
    {
        if (bannerAd == null) return;

        if (show)
            bannerAd.ShowAd();
        else
            bannerAd.HideAd();
    }
}
