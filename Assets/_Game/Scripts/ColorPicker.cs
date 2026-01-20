using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ColorPicker : MonoBehaviour
{
    public Transform Content;
    public List<ColorBtn> Colors;
    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < Colors.Count; i++)
        {
            Colors[i].i = i;
        }
    }
    public Color ColoratItem(int i)
    {    
       return Colors[i].GetComponent<Image>().color;
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
