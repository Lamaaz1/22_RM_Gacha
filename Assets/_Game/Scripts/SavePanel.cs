using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SavePanel : MonoBehaviour
{
    public static SavePanel instance;

    private void Awake()
    {
        if(instance == null)
            instance = this;
        else
            Destroy(instance);
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SaveCharacter(int Index, ChildrenParts PartName, string ItemName )
    {
        PlayerPrefs.SetString("Player"+Index+PartName.ToString(), ItemName );
        Debug.Log("saaave");
        Debug.Log("Player" + Index + PartName.ToString()+ PlayerPrefs.GetString("Player" + Index + PartName.ToString()));
        //PlayerPrefs.SetString("Player"+Index+PartName.ToString()+"Color", Color);
    }
    public void SaveColorPart(int Index, ChildrenParts PartName, int ColorIndex)
    {
        PlayerPrefs.SetInt("Player" + Index + PartName.ToString()+"Color", ColorIndex);
        Debug.Log("saaave");
        Debug.Log("Player" + Index + PartName.ToString() + "Color=" + PlayerPrefs.GetInt("Player" + Index + PartName.ToString() + "Color"));
        //PlayerPrefs.SetString("Player"+Index+PartName.ToString()+"Color", Color);
    }
    public string LoadCharacter(int PlayerIndex, ChildrenParts PartName , out string SpriteName)
    {
        SpriteName = PlayerPrefs.GetString("Player" + PlayerIndex + PartName.ToString());
        return SpriteName;
    }
    public int LoadColors(int PlayerIndex, ChildrenParts PartName, out int ColorName)
    {
        if (PlayerPrefs.HasKey("Player" + PlayerIndex + PartName.ToString() + "Color"))
        {
            ColorName = PlayerPrefs.GetInt("Player" + PlayerIndex + PartName.ToString() + "Color");
            
        }
        else
        {
            ColorName = 11111;
          
        }
        return ColorName;
    }
    public void SaveBackGround(int i)
    {
        PlayerPrefs.SetInt("Background", i);
    }
    public int LoadBackground(out int i)
    {
        i = PlayerPrefs.GetInt("Background");
        return i;
    }
}
