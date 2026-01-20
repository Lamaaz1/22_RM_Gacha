using UnityEngine;

public class LoadingPanel : MonoBehaviour
{
    public GameObject panelToClose;

    void Start()
    {
        if (panelToClose != null)
        {
            // Show the panel
            panelToClose.SetActive(true);
            // Start the coroutine to hide it after 3 seconds
            StartCoroutine(HidePanelAfterSeconds(3f));
        }
    }

    System.Collections.IEnumerator HidePanelAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        panelToClose.SetActive(false);
    }
}
