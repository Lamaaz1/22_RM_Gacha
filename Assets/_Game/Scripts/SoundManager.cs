using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SoundManager : MonoBehaviour
{
    private static SoundManager instance;
    private readonly HashSet<Button> boundButtons = new HashSet<Button>();
    private AudioSource source;
    private AudioClip click, clothesChange, pendingClip;
    private int pendingPriority;
    private bool ownsRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() { EnsureInstance(); }

    private static SoundManager EnsureInstance()
    {
        if (instance == null)
        {
            var root = new GameObject("UI Sounds");
            root.SetActive(false);
            var manager = root.AddComponent<SoundManager>();
            manager.ownsRoot = true;
            root.SetActive(true);
        }
        return instance;
    }

    private void Awake()
    {
        // Older scenes attach this script beside other managers. Never remove or
        // move that scene object when creating the dedicated audio service.
        if (!ownsRoot)
        {
            EnsureInstance();
            Destroy(this);
            return;
        }
        if (instance != null && instance != this) { Destroy(this); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.volume = .45f;
        source.ignoreListenerPause = true;
        click = Resources.Load<AudioClip>("Audio/UI/button-click");
        clothesChange = Resources.Load<AudioClip>("Audio/UI/clothes-change");
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        boundButtons.RemoveWhere(button => button == null);
        BindScene(scene);
        StartCoroutine(BindAfterStart(scene));
    }

    private IEnumerator BindAfterStart(Scene scene)
    {
        yield return null;
        if (scene.IsValid() && scene.isLoaded) BindScene(scene);
    }

    private void BindScene(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects()) BindButtons(root);
    }

    public static void BindButtons(GameObject root)
    {
        if (!Application.isPlaying || root == null) return;
        var manager = EnsureInstance();
        foreach (var button in root.GetComponentsInChildren<Button>(true))
            if (manager.boundButtons.Add(button)) button.onClick.AddListener(PlayClick);
    }

    public static void PlayClick()
    {
        if (!Application.isPlaying) return;
        var manager = EnsureInstance();
        manager.Queue(manager.click, 1);
    }

    public static void PlayClothesChange()
    {
        if (!Application.isPlaying) return;
        var manager = EnsureInstance();
        manager.Queue(manager.clothesChange, 2);
    }

    public static void PlayEffect(AudioClip clip)
    {
        if (Application.isPlaying) EnsureInstance().Queue(clip, 3);
    }

    private void Queue(AudioClip clip, int priority)
    {
        if (PlayerPrefs.GetInt("Sound", 0) != 0 || clip == null || priority < pendingPriority) return;
        pendingClip = clip;
        pendingPriority = priority;
    }

    public static void RefreshSoundSetting()
    {
        if (instance == null || PlayerPrefs.GetInt("Sound", 0) == 0) return;
        instance.pendingClip = null;
        instance.pendingPriority = 0;
        instance.source.Stop();
    }

    private void LateUpdate()
    {
        if (PlayerPrefs.GetInt("Sound", 0) != 0) { RefreshSoundSetting(); return; }
        // Changing an outfit takes priority over its button click in the same frame.
        if (pendingClip != null) source.PlayOneShot(pendingClip);
        pendingClip = null;
        pendingPriority = 0;
    }
}
