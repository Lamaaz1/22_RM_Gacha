using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum ChildrenParts
{
    Eyes,Lips,Ear,Brows,
    TShirts,Jackets,
    HairInFront,atTheBack,Tail,
    Caps,Glasses,
    Skirts,Shoes,
    Toys
}
public class BodyParts : MonoBehaviour
{
    Button mybtn;
    [SerializeField] Text mytext;
    [SerializeField] List<ChildrenParts> ChildresnP;
    public string CategoryTitle => mytext != null ? mytext.text : gameObject.name;
    public List<ChildrenParts> CategoryParts => ChildresnP;
   
    // Start is called before the first frame update
    void Start()
    {
        mybtn=GetComponent<Button>();
        mybtn.onClick.AddListener(OnclickB);
    }

    public void OnclickB()
    {
        var customize = GetComponentInParent<LightCustomizePanel>();
        if (customize != null)
        {
            customize.ShowCategory(this);
            return;
        }
        PanelChoicesManager.Instance.ShowPanel(mytext.text, ChildresnP);
    }
  
}
