using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class DressUpScript : MonoBehaviour
{
    public static DressUpScript instance;
    public GameObject PlayerTransform;
    public RectTransform PortraitPlayerTransform;
    public RectTransform PortraitPlayerParent;
    public RectTransform LandscapPlayerTransform;
    public RectTransform LandscapPlayerParent;
    public GameObject ActivdPlayer;
    public GameObject BtnActivdPlayer;
    public GameObject PlayerVdIcon;
    //public Vector2 PlayerPosition;
    RectTransform portraitPlayerPos;
    RectTransform landscapePlayerPos;

    public List<GameObject> PlatersButtons;
    public List<GameObject> IconsVdButtons;
    public int ActivePlayerIndex=0;
    public ChildrenParts ActualPart;
    private void Awake()
    {
        portraitPlayerPos = PortraitPlayerTransform.GetChild(0).GetComponent<RectTransform>();
        landscapePlayerPos = LandscapPlayerTransform.GetChild(0).GetComponent<RectTransform>();
        //PlayerPosition= PlayerTransform.transform.GetChild(0).localPosition;
        instance = this;
    }
    // Start is called before the first frame update
    void Start()
    {
        bool isLandscape = Screen.width > Screen.height;
        Lndscap = isLandscape;
        if (isLandscape)
        {
            PlayerTransform.GetComponent<RectTransform>().SetParent(LandscapPlayerParent);
            CopyRectTransform(LandscapPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            CopyChildZeroToAll(LandscapPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            Lndscap = true;
           
        }
        else
        {
            PlayerTransform.GetComponent<RectTransform>().SetParent(PortraitPlayerParent);
            CopyRectTransform(PortraitPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            CopyChildZeroToAll(PortraitPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            Lndscap = false;
        }
        ActivePlayer(0);
        init();
    }
    int ColorIndex;
    //public void RePosPlayer()
    //{
    //    foreach (RectTransform child in PlayerTransform.transform)
    //    {
    //        child.localPosition = PlayerPosition;
    //    }
    //}
    public void RePosPlayer()
    {
        bool isLandscape = Screen.width > Screen.height;

        RectTransform targetPos = isLandscape
            ? landscapePlayerPos
            : portraitPlayerPos;

        foreach (RectTransform child in PlayerTransform.GetComponent<RectTransform>())
        {
            CopyRectTransform(targetPos, child);
           
        }
    }

    public void init()
    {
       
        for (int i = 0; i < PlayerTransform.transform.childCount; i++)
        {
            ActivePlayer(i);
          
            foreach (Transform child in PlayerTransform.transform.GetChild(i).transform)
            {
                if(child.gameObject.name!="0" && child.gameObject.name != "Hand")
                {
                    ChildrenParts part = (ChildrenParts)Enum.Parse(typeof(ChildrenParts), child.gameObject.name);
                    string SpritName;
                    SavePanel.instance.LoadCharacter(i, part, out SpritName);

                    if (part == ChildrenParts.Body && (string.IsNullOrEmpty(SpritName) || SpritName == "0" ||
                        Resources.Load<Sprite>("Sprites/Body/" + SpritName) == null))
                    {
                        ResetBodyToDefault(false);
                        continue;
                    }

                    SavePanel.instance.LoadColors(i, part, out ColorIndex);
                    //Debug.Log(ColorIndex); //here 44
                    if (ColorIndex != 11111)
                    {
                        //Debug.Log(SpritName);
                    }
                    if (SpritName != "")
                    {
                        //Debug.Log(ColorIndex); // here 11111
                        Sprite s = Resources.Load<Sprite>("Sprites/" + child.gameObject.name + "/" + SpritName);
                        //Debug.Log(ColorIndex + 111111111111);
                        DressUpp(s, child.gameObject.name);
                    }
                }
                             
            }
        }
        RefreshCharacterPreviews();
        ActivePlayer(0);
        RePosPlayer();

    }
    public void ActivePlayer(int i)
    {
        ActivePlayerIndex = i;
        foreach (Transform child in PlayerTransform.transform)
        {
            child.gameObject.SetActive(false);
        }
        ActivdPlayer = PlayerTransform.transform.GetChild(i).gameObject;
        BtnActivdPlayer = PlatersButtons[i];
        PlayerVdIcon = IconsVdButtons[i];

        ActivdPlayer.SetActive(true);
        SyncCharacterPreviews(i);
    }
  
    
    public void DressUp(Sprite sprite, string partName, bool playSound = true)
    {
        if (partName == ChildrenParts.Body.ToString() && (sprite == null || sprite.name == "0"))
        {
            ResetBodyToDefault(playSound);
            return;
        }
        var image = FindPartImage(ActivdPlayer, partName);
        if (sprite == null || image == null) return;
        Sprite previousSprite = image.sprite;
        Color color = image.color;
        color.a = sprite.name == "0" ? 0 : 1;
        image.color = color;
        image.sprite = sprite;
        RestorePartColor(image, sprite, partName);
        SyncCharacterPreviews(ActivePlayerIndex);
        if (playSound && previousSprite != sprite) SoundManager.PlayClothesChange();
    }

    public void ResetBodyToDefault(bool playSound = true)
    {
        Sprite sprite = Resources.Load<Sprite>("Sprites/Body/1");
        Image body = FindPartImage(ActivdPlayer, ChildrenParts.Body.ToString());
        if (sprite == null || body == null) return;
        bool changed = body.sprite != sprite || body.color != Color.white || !body.enabled || !body.gameObject.activeSelf;
        if (SavePanel.instance != null)
        {
            SavePanel.instance.ClearPartColor(ActivePlayerIndex, ChildrenParts.Body);
            SavePanel.instance.SaveCharacter(ActivePlayerIndex, ChildrenParts.Body, "1");
        }
        body.color = Color.white;
        body.enabled = true;
        body.gameObject.SetActive(true);
        DressUp(sprite, ChildrenParts.Body.ToString(), false);
        if (playSound && changed) SoundManager.PlayClothesChange();
    }
    public void DressUpp(Sprite sprite, string partName)
    {
        DressUp(sprite, partName, false);
    }

    private readonly Dictionary<int, List<GameObject>> characterPreviews = new Dictionary<int, List<GameObject>>();
    private readonly List<CharacterButtonScrpt> characterSlots = new List<CharacterButtonScrpt>();
    private readonly List<StudioSlot> studioSlots = new List<StudioSlot>();
    private bool previewsBound;

    private sealed class StudioSlot
    {
        public int index;
        public Button button;
        public GameObject lockCover;
    }

    public void RefreshCharacterPreviews()
    {
        characterPreviews.Clear();
        characterSlots.Clear();
        studioSlots.Clear();
        AddLegacyPreviews(PlatersButtons);
        AddLegacyPreviews(IconsVdButtons);
        var ui = UiMnager.instance;
        if (ui != null)
        {
            AddCharacterSlots(ui.LeftPanel);
            AddCharacterSlots(ui.LeftPanelLSCP);
            AddStudioSlots(ui.SelectModBtn);
            AddStudioSlots(ui.SelectModBtnLSCP);
        }
        previewsBound = true;
        RefreshCharacterLocks();
        if (PlayerTransform != null)
            for (int i = 0; i < PlayerTransform.transform.childCount; i++) SyncCharacterPreviews(i);
    }

    private void AddLegacyPreviews(List<GameObject> previews)
    {
        if (previews == null) return;
        for (int i = 0; i < previews.Count; i++) AddCharacterPreview(i, previews[i]);
    }

    private void AddCharacterSlots(GameObject panel)
    {
        if (panel == null) return;
        foreach (var slot in panel.GetComponentsInChildren<CharacterButtonScrpt>(true))
        {
            characterSlots.Add(slot);
            AddCharacterPreview(slot.Index, slot.gameObject);
        }
    }

    private void AddStudioSlots(GameObject panel)
    {
        if (panel == null) return;
        int index = 0;
        // Studio buttons follow character order, including currently inactive slots.
        foreach (var button in panel.GetComponentsInChildren<Button>(true))
        {
            bool selectsCharacter = false;
            for (int call = 0; call < button.onClick.GetPersistentEventCount(); call++)
                if (button.onClick.GetPersistentMethodName(call) == "SelectPlayerByIndex") selectsCharacter = true;
            if (!selectsCharacter) continue;
            AddCharacterPreview(index, button.gameObject);
            studioSlots.Add(new StudioSlot { index = index, button = button, lockCover = StudioLockCover(button, index) });
            index++;
        }
    }

    private GameObject StudioLockCover(Button button, int index)
    {
        Transform existing = button.transform.Find("Studio Character Lock");
        if (existing != null) return existing.gameObject;
        var slot = characterSlots.Find(candidate => candidate.Index == index && candidate.Lock != null);
        if (slot == null) return null;
        GameObject cover = Instantiate(slot.Lock, button.transform, false);
        cover.name = "Studio Character Lock";
        var rect = (RectTransform)cover.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();
        foreach (var graphic in cover.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        return cover;
    }

    public bool IsCharacterUnlocked(int index)
    {
        if (index < 0 || PlayerTransform == null || index >= PlayerTransform.transform.childCount) return false;
        if (!previewsBound) RefreshCharacterPreviews();
        return PlayerPrefs.GetInt("RemoveAds") == 1 || PlayerPrefs.GetInt("Character" + index) == 1 ||
            characterSlots.Exists(slot => slot != null && slot.Index == index && slot.Active);
    }

    public void RefreshCharacterLocks()
    {
        if (!previewsBound) return;
        foreach (var slot in characterSlots)
            if (slot != null && slot.Lock != null) slot.Lock.SetActive(!IsCharacterUnlocked(slot.Index));
        foreach (var slot in studioSlots)
        {
            bool unlocked = IsCharacterUnlocked(slot.index);
            if (slot.button != null) slot.button.interactable = unlocked;
            if (slot.lockCover != null) slot.lockCover.SetActive(!unlocked);
        }
    }

    private void AddCharacterPreview(int index, GameObject root)
    {
        Transform character = FindCharacterRoot(root);
        if (character == null) return;
        if (!characterPreviews.TryGetValue(index, out var previews))
            characterPreviews[index] = previews = new List<GameObject>();
        if (!previews.Contains(character.gameObject)) previews.Add(character.gameObject);
    }

    private static Transform FindCharacterRoot(GameObject root)
    {
        if (root == null) return null;
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            foreach (Transform child in candidate)
                if (Enum.TryParse(child.name, out ChildrenParts part) && child.GetComponent<Image>() != null)
                    return candidate;
        return null;
    }

    private static Image FindPartImage(GameObject root, string partName)
    {
        Transform character = FindCharacterRoot(root);
        Transform part = character != null ? character.Find(partName) : null;
        return part != null ? part.GetComponent<Image>() : null;
    }

    public void SyncCharacterPreviews(int index)
    {
        if (!previewsBound) RefreshCharacterPreviews();
        GameObject source = PlayerTransform != null && index >= 0 && index < PlayerTransform.transform.childCount
            ? PlayerTransform.transform.GetChild(index).gameObject : index == ActivePlayerIndex ? ActivdPlayer : null;
        Transform character = FindCharacterRoot(source);
        if (character == null || !characterPreviews.TryGetValue(index, out var previews)) return;
        foreach (GameObject preview in previews)
        {
            if (preview == null || preview == character.gameObject) continue;
            foreach (Transform layer in character)
            {
                if (!Enum.TryParse(layer.name, out ChildrenParts part)) continue;
                var image = layer.GetComponent<Image>();
                Transform targetLayer = preview.transform.Find(layer.name);
                var target = targetLayer != null ? targetLayer.GetComponent<Image>() : null;
                if (image == null || target == null) continue;
                target.sprite = image.sprite;
                target.color = image.color;
                target.enabled = image.enabled;
                targetLayer.gameObject.SetActive(layer.gameObject.activeSelf);
                targetLayer.SetSiblingIndex(layer.GetSiblingIndex());
            }
        }
    }

    private void RestorePartColor(Image image, Sprite sprite, string partName)
    {
        if (sprite == null || sprite.name == "0" || SavePanel.instance == null) return;
        if (Enum.TryParse(partName, out ChildrenParts part))
            image.color = SavePanel.instance.LoadPartColor(ActivePlayerIndex, part,
                UiMnager.instance != null ? UiMnager.instance.colorPicker : null, image.color);
    }

    public void ChangeColor(string partName, out Image characterPart, out Image characterPartPnl, out Image characterIcon)
    {
        characterPart = FindPartImage(ActivdPlayer, partName);
        characterPartPnl = FindPartImage(BtnActivdPlayer, partName);
        characterIcon = FindPartImage(PlayerVdIcon, partName);
    }
    // Update is called once per frame
    void Update()
    {
        
    }
    public void UnlockAllCharacters()
    {
        foreach(GameObject child in PlatersButtons)
        {
            child.transform.parent.GetComponent<CharacterButtonScrpt>().Unlock();
        }
    }
    public void PortraiSwitch()
    {
        //if (Lndscap)
        //{
            PlayerTransform.GetComponent<RectTransform>().SetParent(PortraitPlayerParent);
            CopyRectTransform(PortraitPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            CopyChildZeroToAll(PortraitPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            Lndscap = false;

        //RePosPlayer();
        //}

    }
    public bool Lndscap;
    public void LandscapSwitch()
    {
        //if(!Lndscap)
        //{
            PlayerTransform.GetComponent<RectTransform>().SetParent(LandscapPlayerParent);
            CopyRectTransform(LandscapPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            CopyChildZeroToAll(LandscapPlayerTransform, PlayerTransform.GetComponent<RectTransform>());
            Lndscap = true;


        //RePosPlayer();
        //}


    }
    public static void CopyRectTransform(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;

        to.anchoredPosition = from.anchoredPosition;
        to.sizeDelta = from.sizeDelta;

        to.offsetMin = from.offsetMin;
        to.offsetMax = from.offsetMax;

        to.localScale = from.localScale;
        to.localRotation = from.localRotation;
    }
    public static void CopyChildZeroToAll(RectTransform from, RectTransform to)
    {
        if (from.childCount == 0) return;

        RectTransform source = from.GetChild(0) as RectTransform;

        for (int i = 0; i < to.childCount; i++)
        {
            RectTransform target = to.GetChild(i) as RectTransform;
            CopyRectTransform(source, target);
        }
    }

}
