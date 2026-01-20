using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PermissionSc : MonoBehaviour
{
    public static PermissionSc Instance;
    public GameObject StorrageP;
    private void Awake()
    {
        if (Instance == null)
        {
            Instance =this;
            DontDestroyOnLoad(Instance);
        }
        else Destroy(Instance);
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void OpenPermision()
    {
        StorrageP.SetActive(true);
    }
    public void AcceptPermission()
    {
        PlayerPrefs.SetInt("SavePermission", 1);
        CloseIt();
    }
    public void CloseIt()
    {
        StorrageP.SetActive(false);
    }
}
