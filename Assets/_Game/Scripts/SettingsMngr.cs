using UnityEngine;
using UnityEngine.UI;

public class SettingsMngr : MonoBehaviour
{
    public static SettingsMngr Instance;
    public Button MusicBtn;
    public Button SoundBtn;
    public Button MusicBtnLdscp;
    public Button SoundBtnLdscp;
    public bool ActiveM;
    public bool ActiveS;
    public GameObject ActiveImgM;
    public GameObject DesactiveImgM;
    public GameObject ActiveImgMLdscp;
    public GameObject DesactiveImgMLdscp;

    public GameObject ActiveImgS;
    public GameObject DesactiveImgS;
    public GameObject ActiveImgSLdscp;
    public GameObject DesactiveImgSLdscp;
    public AudioSource myMusic;

    public AudioClip clickSound;
    public AudioClip screenshotSound;
    public AudioSource sfxSource; // for sound effects


    private void Awake()
    {
        Instance = this;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Init();
        Debug.Log(gameObject.name);
    }
    public void Init()
    {
        MusicBtn.onClick.AddListener(OnclickM);
        SoundBtn.onClick.AddListener(OnclickS);
        MusicBtnLdscp.onClick.AddListener(OnclickM);
        SoundBtnLdscp.onClick.AddListener(OnclickS);
        if (PlayerPrefs.GetInt("Music") == 0)
        {
            ActiveImgM.SetActive(true);
            DesactiveImgM.SetActive(false);

            ActiveImgMLdscp.SetActive(true);
            DesactiveImgMLdscp.SetActive(false);
            myMusic.Play();

            ActiveM = true;
        }
        else
        {
            ActiveImgM.SetActive(false);
            DesactiveImgM.SetActive(true);
            ActiveImgMLdscp.SetActive(false);
            DesactiveImgMLdscp.SetActive(true);
            myMusic.Stop();
            ActiveM = false;
        }
        if (PlayerPrefs.GetInt("Sound") == 0)
        {
            ActiveImgS.SetActive(true);
            DesactiveImgS.SetActive(false);
            ActiveImgSLdscp.SetActive(true);
            DesactiveImgSLdscp.SetActive(false);
            ActiveS = true;
        }
        else
        {
            ActiveImgS.SetActive(false);
            DesactiveImgS.SetActive(true);
            ActiveImgSLdscp.SetActive(false);
            DesactiveImgSLdscp.SetActive(true);
            ActiveS = false;
        }


    }
    public void OnclickM()
    {
        if(ActiveM)
        {
            ActiveImgM.SetActive(false);
            DesactiveImgM.SetActive(true);
            ActiveImgMLdscp.SetActive(false);
            DesactiveImgMLdscp.SetActive(true);
            PlayerPrefs.SetInt("Music",1);
            myMusic.Stop();
            ActiveM = false; return;
        }
        else
        {
            ActiveImgM.SetActive(true);
            DesactiveImgM.SetActive(false);
            ActiveImgMLdscp.SetActive(true);
            DesactiveImgMLdscp.SetActive(false);
            PlayerPrefs.SetInt("Music", 0);
            myMusic.Play();
            ActiveM = true; return;
        }
    }
    public void OnclickS()
    {
        if (ActiveS)
        {
            ActiveImgS.SetActive(false);
            DesactiveImgS.SetActive(true);
            ActiveImgSLdscp.SetActive(false);
            DesactiveImgSLdscp.SetActive(true);
            PlayerPrefs.SetInt("Sound", 1);
            ActiveS = false; return;
        }
        else
        {
            ActiveImgS.SetActive(true);
            DesactiveImgS.SetActive(false);
            ActiveImgSLdscp.SetActive(true);
            DesactiveImgSLdscp.SetActive(false);
            PlayerPrefs.SetInt("Sound", 0);
            ActiveS = true; return;
        }
    }
    public void Stop()
    {
        myMusic.Stop();
    }
    public void Restart()
    {
        if(ActiveM)
            myMusic.Play();
        //else myMusic.Stop();
    }
    public void PlaySFX(AudioClip clip)
    {
        if (ActiveS && clip != null)
            sfxSource.PlayOneShot(clip);
    }
    public void TakeScreenshot()
    {
        // Your screenshot logic here...
        PlaySFX(screenshotSound);
    }
    public void OnecLICK()
    {
        // Your screenshot logic here...
        PlaySFX(clickSound);
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
