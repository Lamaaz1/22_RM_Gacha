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
        if(isLandscape)
        {
            DressUpScript.instance.LandscapSwitch();
        }
        else
        {
            DressUpScript.instance.PortraiSwitch();

        }
    }
}
