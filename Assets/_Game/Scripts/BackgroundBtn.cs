using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BackgroundBtn : MonoBehaviour
{
    public int i;
    public Button btn;
    // Start is called before the first frame update
    void Start()
    {
        btn= GetComponent<Button>();
        btn.onClick.AddListener(OnclickM);
    }
    public void OnclickM()
    {
        UiMnager.instance.ChangeBG(GetComponent<Image>());
        SavePanel.instance.SaveBackGround(i);
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
