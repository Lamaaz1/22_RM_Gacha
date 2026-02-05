using System.Collections;
using UnityEngine;

public class OrientationUI : MonoBehaviour
{
    public GameObject portraitLayout;
    public GameObject landscapeLayout;

    bool lastLandscape;

    void Start()
    {
        lastLandscape = Screen.width > Screen.height;
        ApplyOrientation(lastLandscape);
        StartCoroutine(InitOrientation());
    }
  

    IEnumerator InitOrientation()
    {
        yield return null; // wait 1 frame

        lastLandscape = Screen.width > Screen.height;
        ApplyOrientation(lastLandscape);
    }

    void Update()
    {
        bool isLandscape = Screen.width > Screen.height;

        if (isLandscape == lastLandscape)
            return; // nothing changed

        lastLandscape = isLandscape;
        ApplyOrientation(isLandscape);
    }

    void ApplyOrientation(bool isLandscape)
    {
        portraitLayout.SetActive(!isLandscape);
        landscapeLayout.SetActive(isLandscape);

        if (isLandscape)
            DressUpScript.instance.LandscapSwitch();
        else
            DressUpScript.instance.PortraiSwitch();
    }
}
