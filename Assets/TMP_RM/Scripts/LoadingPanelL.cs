using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;




public class LoadingPanelL : MonoBehaviour
{
    public UnityEvent OnLoadingComplete;
    public GameObject loadingPanel; // Assign your panel GameObject here
    public Slider loadingSlider;    // Assign your slider here
    public float loadingSpeed = 0.5f; // Speed at which slider fills

    void Start()
    {
        loadingPanel.SetActive(true);
        loadingSlider.value = 0f;   
    }

    void Update()
    {
        if (loadingPanel.activeSelf)
        {
            loadingSlider.value += loadingSpeed * Time.deltaTime;

            if (loadingSlider.value >= 1f)
            {
                loadingPanel.SetActive(false);
                OnLoadingComplete?.Invoke();
            }

        }
    }
}
