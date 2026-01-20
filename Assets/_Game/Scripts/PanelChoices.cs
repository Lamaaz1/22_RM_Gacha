using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Unity.VisualScripting;

public class PanelChoices : MonoBehaviour
{
    public static PanelChoices Instance;
    public ColorBtn colorPicker;
    public List<GameObject> choices = new List<GameObject>();
    [SerializeField] Text Title;
    [SerializeField] Transform Grid;
    RectTransform panelStartPos;
    Vector3 StartPos;
    public Text IndexPage;

    private void Awake()
    {
        Instance = this;
    }

    
    // Start is called before the first frame update
    void Start()
    {
        nextButton.onClick.AddListener(NextPage);
        previousButton.onClick.AddListener(PreviousPage);
        panelStartPos = GetComponent<RectTransform>();
        StartPos = panelStartPos.anchoredPosition;
    }

    public void ShowPanel(string t,List<ChildrenParts> chParts)
    {
        currentPage = 0;
      
        
        partsC = chParts;
        Remplir();
        Title.text = t;
        panelStartPos.DOAnchorPos(Vector3.zero+Vector3.right* 890, 2);
        //PLAYER MOVE
        UiMnager.instance.OpenChoicePanel();
    }
    public void HidePanel() 
    {
        panelStartPos.DOAnchorPos(StartPos, 2);
        UiMnager.instance.CloseChoicePanel();
    }
    private int currentPage = 0;
    private int imagesPerPage = 4;
    [SerializeField] GameObject childPart;
    [SerializeField] Button previousButton;
    [SerializeField] Button nextButton;
    List<ChildrenParts> partsC;
    public void Remplir()
    {
      
        foreach (Transform child in Grid)
        {
            Destroy(child.gameObject);
        }
        int startIndex = currentPage * imagesPerPage;
        int endIndex = Mathf.Min(startIndex + imagesPerPage, partsC.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            GameObject PartObj = Instantiate(childPart, Grid);

            PartObj.GetComponent<ChoicesPrefabs>().Remplir(partsC[i]);
            Debug.Log(currentPage+ partsC[i].ToString());

            //PartObj.GetComponent<ChoicesPrefabs>().ShwImg();
            //Debug.Log("currnt "+currentPage);

        }
        //currentPage--;
        //NextPage();

        previousButton.interactable = currentPage > 0;
        nextButton.interactable = endIndex < partsC.Count;
        IndexPage.text = (currentPage+1)+"/"+((partsC.Count+1)/4);
      
    }
    public void RemplirF()
    {
        foreach (Transform child in Grid)
        {
            Destroy(child.gameObject);
        }
        int startIndex = currentPage * imagesPerPage;
        int endIndex = Mathf.Min(startIndex + imagesPerPage, partsC.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            GameObject PartObj = Instantiate(childPart, Grid);

            PartObj.GetComponent<ChoicesPrefabs>().Remplir(partsC[i]);
            Debug.Log(currentPage + partsC[i].ToString());
           
            //Image imgComponent = PartObj.GetComponent<Image>();
            //if (imgComponent != null)
            //{
            //    imgComponent.sprite = partsC[i];
            //}
        }

        previousButton.interactable = currentPage > 0;
        nextButton.interactable = endIndex < partsC.Count;
        IndexPage.text = (currentPage + 1) + "/" + ((partsC.Count + 1) / 4);

    }
    void NextPage()
    {
        if ((currentPage + 1) * imagesPerPage < partsC.Count)
        {
            currentPage++;
            RemplirF();
        }
    }

    void PreviousPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            RemplirF();
        }
    }
    public void HidePanelColor(GameObject p)
    {
        p.SetActive(false);
    }
    //public Image SelectedImg;
    public Image CharacterSelectedImg;
    public Image CharacterSelectedImgPnl;
    public Image CharacterIconImgPnl;

    public void ChangeColor(Color a)
    {
        Debug.Log(CharacterSelectedImg.sprite.name);
        Debug.Log(CharacterSelectedImgPnl.sprite.name);
        if (CharacterSelectedImg.sprite.name!="0")
        {
            //SelectedImg.color = a;
            CharacterSelectedImg.color = a;
            CharacterSelectedImgPnl.color = a;
            CharacterIconImgPnl.color = a;
        }
      

    }
}
