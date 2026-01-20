using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using System.IO;



public class UiMnager : MonoBehaviour
{
    public static UiMnager instance;

    public GameObject LeftPanel;
    public GameObject RightPanel;
    public GameObject PlayerPanel;
    public GameObject BackgroundImage;
    public GameObject SettingsPanel;
    public GameObject downp;
    public GameObject BackgroundsP;
    public GameObject ScreenShotBtn;
    public GameObject SelectModBtn;
    public ColorPicker colorPicker;
    public int BckgdIndex;
    public List<BackgroundBtn> BackgrounButtons;
    public bool AnimationMode=false;
    private void Awake()
    {
        instance = this;
    }
    // Start is called before the first frame update
    void Start()
    {
        Init();
    }
    public void OpenChoicePanel()
    {
        LeftPanel.SetActive(false);
        PlayerPanel.GetComponent<RectTransform>().DOAnchorPos(Vector3.zero+Vector3.left*300,1);

    }
    public void CloseChoicePanel()
    {
        LeftPanel.SetActive(true);
        PlayerPanel.GetComponent<RectTransform>().DOAnchorPos(Vector3.zero , 1);

    }
    public void LoadBackground()
    {
      foreach(BackgroundBtn bg in BackgrounButtons)
        {
            if(bg.i==PlayerPrefs.GetInt("Background"))
            {
                bg.OnclickM();
            }
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
    public void OpenPanel(GameObject panel)
    {
        panel.SetActive(true);
    }
    public void Init()
    {
        LeftPanel.SetActive(true );
        RightPanel.SetActive(true );
        downp.SetActive(true );
        SettingsPanel.SetActive(false );
        BackgroundsP.SetActive(false );
        ScreenShotBtn.SetActive(false);
        SelectModBtn.SetActive(false);
        AnimationMode = false;
        DressUpScript.instance.RePosPlayer();
        LoadBackground();
        DressUpScript.instance.ActivePlayer(0);
    }
    public void ShowSettings()
    {
        LeftPanel.SetActive(false);
        RightPanel.SetActive(false);
        downp.SetActive(false);
        SettingsPanel.SetActive(true);
        BackgroundsP.SetActive(false);
        ScreenShotBtn.SetActive(false);
        SelectModBtn.SetActive(false);

    }
    public void ChooseBackGroundP()
    {
        LeftPanel.SetActive(false);
        RightPanel.SetActive(false);
        downp.SetActive(false);
        SettingsPanel.SetActive(false);
        BackgroundsP.SetActive(true);
        ScreenShotBtn.SetActive(false);
        SelectModBtn.SetActive(false);

    }
    public void SeeViews()
    {
        LeftPanel.SetActive(false);
        RightPanel.SetActive(false);
        downp.SetActive(false);
        SettingsPanel.SetActive(false);
        BackgroundsP.SetActive(false);
        ScreenShotBtn.SetActive(true);
        SelectModBtn.SetActive(false);

    }
    public void AnimationPanel()
    {
        LeftPanel.SetActive(false);
        RightPanel.SetActive(false);
        downp.SetActive(false);
        SettingsPanel.SetActive(false);
        BackgroundsP.SetActive(false);
        ScreenShotBtn.SetActive(false);
        SelectModBtn.SetActive(true);
        AnimationMode = true;
    }
    public void ChangeBG(Image IMG)
    {
        BackgroundImage.GetComponent<Image>().sprite= IMG.sprite;
      
    }
    public void TakeScreenshot()
    {
        if(PlayerPrefs.GetInt("SavePermission")==1)
        {
            StartCoroutine(CaptureAndSaveScreenshot());
        }
        else
        {
            PermissionSc.Instance.OpenPermision();
        }
    }

    //    private IEnumerator CaptureAndSaveScreenshot()
    //    {
    //        yield return new WaitForEndOfFrame();

    //        Texture2D tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
    //        tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
    //        tex.Apply();

    //        byte[] imageData = tex.EncodeToPNG();

    //        string fileName = "Screenshot_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";

    //#if UNITY_ANDROID && !UNITY_EDITOR
    //    string path = Path.Combine(Application.persistentDataPath, fileName);
    //    File.WriteAllBytes(path, imageData);
    //    Debug.Log("Screenshot saved to: " + path);

    //    // Make the image visible in Gallery
    //    using (AndroidJavaClass mediaScanner = new AndroidJavaClass("android.media.MediaScannerConnection"))
    //    using (AndroidJavaObject activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
    //                                                .GetStatic<AndroidJavaObject>("currentActivity"))
    //    {
    //        mediaScanner.CallStatic("scanFile", activity,
    //            new string[] { path }, null, null);
    //    }
    //#else
    //        string path = Path.Combine(Application.temporaryCachePath, fileName);
    //        //string path = Path.Combine(Application.persistentDataPath, fileName);
    //        File.WriteAllBytes(path, imageData);
    //        Debug.Log("Screenshot saved to: " + path);
    //#endif

    //        Destroy(tex);
    //    }
    //    public void ShowPanel(GameObject panel)
    //    {
    //        panel.SetActive(true);
    //    }
    //    public void ClosePanel(GameObject panel)
    //    {
    //        panel.SetActive(false);
    //    }
    private IEnumerator CaptureAndSaveScreenshot()
    {
        yield return new WaitForEndOfFrame();

        // Capture the screen into a texture
        Texture2D tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        tex.Apply();

        // Save the image to the Gallery (not Downloads, but visible to user)
        /*NativeGallery.Permission result = */NativeGallery.SaveImageToGallery(tex, "MyGameScreenshots", "Screenshot_{0}.png");
        //tex,
        //    "MyGameScreenshots",                    // Album/folder name in Gallery
        //    "Screenshot_{0}.png"                    // Filename pattern
        //);

        //Debug.Log("Screenshot saved with result: " + result);
        NativeGallery.SaveImageToGallery(tex, "MyGameScreenshots", "Screenshot_{0}.png");
        Debug.Log("Screenshot saved to Gallery folder: MyGameScreenshots");
        // Clean up the texture
        Destroy(tex);
    }
    string GetAndroidDownloadsPath()
    {
        using (AndroidJavaClass env = new AndroidJavaClass("android.os.Environment"))
        {
            using (AndroidJavaObject downloadsDir = env.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", env.GetStatic<string>("DIRECTORY_DOWNLOADS")))
            {
                return downloadsDir.Call<string>("getAbsolutePath");
            }
        }
    }
}
