using UnityEngine;

public class OrientationUI : MonoBehaviour
{
    public GameObject portraitLayout;
    public GameObject landscapeLayout;

    void Update()
    {
        bool isLandscape = Screen.width > Screen.height;
      
        portraitLayout.SetActive(!isLandscape);
        landscapeLayout.SetActive(isLandscape);
        if (isLandscape)
        {
            //if (!DressUpScript.instance.Lndscap)
                DressUpScript.instance.LandscapSwitch();
        }
        else
        {
            //if (DressUpScript.instance.Lndscap)
                DressUpScript.instance.PortraiSwitch();

        }
    }
}
