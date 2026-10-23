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
        if (PartName == ChildrenParts.Body && ItemName == "0") ItemName = "1";
        PlayerPrefs.SetString("Player"+Index+PartName.ToString(), ItemName );
        Debug.Log("saaave");
        Debug.Log("Player" + Index + PartName.ToString()+ PlayerPrefs.GetString("Player" + Index + PartName.ToString()));
        //PlayerPrefs.SetString("Player"+Index+PartName.ToString()+"Color", Color);
    }
    public void SaveColorPart(int Index, ChildrenParts PartName, int ColorIndex)
    {
        PlayerPrefs.DeleteKey("Player" + Index + PartName.ToString() + "RGBA");
        PlayerPrefs.SetInt("Player" + Index + PartName.ToString()+"Color", ColorIndex);
        Debug.Log("saaave");
        Debug.Log("Player" + Index + PartName.ToString() + "Color=" + PlayerPrefs.GetInt("Player" + Index + PartName.ToString() + "Color"));
        //PlayerPrefs.SetString("Player"+Index+PartName.ToString()+"Color", Color);
    }
    [Serializable]
    private class SavedPartColor
    {
        public Color rgba;
    }

    public void SaveColorPart(int playerIndex, ChildrenParts partName, Color color)
    {
        // Store all four float channels; the palette index remains a legacy fallback.
        PlayerPrefs.SetString("Player" + playerIndex + partName.ToString() + "RGBA",
            JsonUtility.ToJson(new SavedPartColor { rgba = color }));
    }

    public Color LoadPartColor(int playerIndex, ChildrenParts partName, ColorPicker palette, Color fallback)
    {
        string key = "Player" + playerIndex + partName.ToString();
        if (PlayerPrefs.HasKey(key + "RGBA"))
        {
            try
            {
                SavedPartColor saved = JsonUtility.FromJson<SavedPartColor>(PlayerPrefs.GetString(key + "RGBA"));
                if (saved != null) return saved.rgba;
            }
            catch (ArgumentException)
            {
                // A damaged custom save can still fall back to the original palette.
            }
        }
        if (PlayerPrefs.HasKey(key + "Color") && palette != null)
        {
            int index = PlayerPrefs.GetInt(key + "Color");
            if (palette.HasColorAtIndex(index))
                return palette.ColoratItem(index);
        }
        return fallback;
    }

    public void ClearPartColor(int playerIndex, ChildrenParts partName)
    {
        string key = "Player" + playerIndex + partName;
        PlayerPrefs.DeleteKey(key + "RGBA");
        PlayerPrefs.DeleteKey(key + "Color");
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
