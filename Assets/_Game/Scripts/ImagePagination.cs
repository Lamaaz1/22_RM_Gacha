using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImagePagination : MonoBehaviour
{
    public List<Sprite> images;
    public GameObject imagePrefab;
    public Transform imagePanel;
    public Button nextButton;
    public Button previousButton;

    private int currentPage = 0;
    private int imagesPerPage = 4;

    void Start()
    {
        nextButton.onClick.AddListener(NextPage);
        previousButton.onClick.AddListener(PreviousPage);
        UpdatePage();
    }

    void UpdatePage()
    {
        foreach (Transform child in imagePanel)
        {
            Destroy(child.gameObject);
        }

        int startIndex = currentPage * imagesPerPage;
        int endIndex = Mathf.Min(startIndex + imagesPerPage, images.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            GameObject imageObj = Instantiate(imagePrefab, imagePanel);
            Image imgComponent = imageObj.GetComponent<Image>();
            if (imgComponent != null)
            {
                imgComponent.sprite = images[i];
            }
        }

        previousButton.interactable = currentPage > 0;
        nextButton.interactable = endIndex < images.Count;
    }

    void NextPage()
    {
        if ((currentPage + 1) * imagesPerPage < images.Count)
        {
            currentPage++;
            UpdatePage();
        }
    }

    void PreviousPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            UpdatePage();
        }
    }
}