using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class AdsController : MonoBehaviour
{
    public static AdsController Instance { get; private set; }

    [SerializeField] private int WTime = 60;
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TextMeshProUGUI countdownText;

    private AdsMediation adsMediation;
    private Coroutine _coroutine;

    private void Start()
    {
        StartDelayedAction();
    }
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            adsMediation = GetComponent<AdsMediation>();
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            adsMediation.Init();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Starts the repeated ad + countdown cycle
    public void StartDelayedAction()
    {
        if (_coroutine == null)
            _coroutine = StartCoroutine(WaitAndDo());
    }

    private IEnumerator WaitAndDo()
    {
        while (true)
        {
            yield return new WaitForSeconds(WTime);

            yield return StartCoroutine(ShowCountdown());
            ShowInterstitialAd();
        }
    }

    private IEnumerator ShowCountdown()
    {
        countdownPanel.SetActive(true);

        for (int i = 3; i > 0; i--)
        {
            countdownText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }

        countdownPanel.SetActive(false);
    }

    public void ShowInterstitialAd()
    {
        if (PlayerPrefs.GetInt("RemoveAds") == 1)
            return;

        Debug.Log("🔹 Showing interstitial ad...");
        adsMediation.ShowInterstital();
    }

    public void ShowInterstitialAdTimer(float delay)
    {
        if (_coroutine != null)
            StopCoroutine(_coroutine);

        _coroutine = StartCoroutine(ShowAdAfterDelay(delay));
    }

    private IEnumerator ShowAdAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowInterstitialAd();
        _coroutine = null;
    }

    public void ShowRewardedInterstitialAd(Action completeMethod)
    {
#if UNITY_EDITOR
        if (Application.isEditor)
        {
            StartCoroutine(FakeReward(completeMethod));
            return;
        }
#endif

        Debug.Log("🔹 Showing rewarded ad...");
        adsMediation.ShowRewardAd(completeMethod, "RewardedAd");
    }

#if UNITY_EDITOR
    private IEnumerator FakeReward(Action callback)
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(1f, 3f));
        callback?.Invoke();
    }
#endif

    public void TestReward()
    {
        ShowRewardedInterstitialAd(() => Debug.Log("✅ Reward triggered"));
    }

    public void _ShowBannerAds(bool show)
    {
        Debug.Log($"🔹 Show Banner: {show}");
        adsMediation.ShowBanner(show);
    }
    public void NoAds()
    {
        PlayerPrefs.SetInt("RemoveAds", 1);
    }
}
