using UnityEngine;
using UnityEngine.UI;

public class PlayerSelector : MonoBehaviour
{
    public GameObject[] players; // Assign all player GameObjects in the Inspector
    private GameObject selectedPlayer;
    private bool isDragging = false;

    void Update()
    {
        //if (Input.GetMouseButton(0) && UiMnager.instance.AnimationMode)// Detect left mouse click
        //{
        //    SelectPlayer();
        //}
        //if (isDragging )
        //{
            
            
        //    Vector3 mousePosition = Input.mousePosition;
        //    //mousePosition.z = 10f; // Adjust depth for 3D positioning
        //    selectedPlayer.transform.position = Camera.main.ScreenToWorldPoint(mousePosition);
        //}
        //if (Input.GetMouseButtonUp(0)) // Release click
        //{
        //    isDragging = false;
        //}
    }

    //public void SelectPlayer(int index)
    //{
    //    if (index >= 0 && index < players.Length)
    //    {
    //        selectedPlayer = players[index];
    //    }
    //}
    [SerializeField] Transform parentObject;
    public void SelectPlayerByIndex(int childIndex)
    {
        if (childIndex >= 0 && childIndex < parentObject.childCount)
        {
            parentObject.GetChild(childIndex).gameObject.SetActive(true);
            selectedPlayer = parentObject.GetChild(childIndex).gameObject;
            
        }
    }
    void SelectPlayer()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

        if (hit.collider != null /*&& System.Array.Exists(players, player => player == hit.transform.gameObject)*/)
        {
            if (selectedPlayer == hit.transform.gameObject) ;
            

        }

        if (selectedPlayer == null)
        {
            selectedPlayer = DressUpScript.instance.ActivdPlayer;
        }
        isDragging = true;
    }
    public void StartDrag()
    {
        if (selectedPlayer != null)
        {
            isDragging = true;
        }
    }

    public void StopDrag()
    {
        isDragging = false;
    }
}
