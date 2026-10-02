using System;
using UnityEngine;

public partial class RemoteContentController
{
    public partial class RemoteConfigData
    {
        public RemoteAdsData ads;
    }

    [Serializable]
    public class RemoteAdsData
    {
        public string provider;
        public bool enabled;
        public bool killSwitch;
        public bool testMode;
        public RemoteAdsPlatformData android;
        public RemoteAdsPlatformData ios;
        public RemoteAdsRulesData rules;
    }

    [Serializable]
    public class RemoteAdsPlatformData
    {
        public bool enabled;
        public string appKey;
        public RemoteBannerData banner;
        public RemoteAdUnitData interstitial;
        public RemoteAdUnitData rewarded;
    }

    [Serializable]
    public class RemoteAdUnitData
    {
        public bool enabled;
        public string adUnitId;
    }

    [Serializable]
    public class RemoteBannerData : RemoteAdUnitData
    {
        public string position = "bottom_center";
    }

    [Serializable]
    public class RemoteAdsRulesData
    {
        public int interstitialEveryLevels = 3;
        public float minimumSecondsBetweenInterstitials = 120f;
        public float interstitialCountdownSeconds = 3f;
        public int maximumInterstitialsPerSession = 5;
        public string[] placementNames;
        public string rewardItemName = "coins";
        public int rewardAmount = 100;
    }

    public event Action<RemoteConfigData> ConfigApplied;

    public RemoteConfigData ActiveConfig => activeConfig;

    private void NotifyConfigApplied(RemoteConfigData config)
    {
        try
        {
            ConfigApplied?.Invoke(config);
        }
        catch (Exception exception)
        {
            Debug.LogError("[ScenePilot/Ads] Config listener failed: " + exception.Message, this);
        }
    }
}
