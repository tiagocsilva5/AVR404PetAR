using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class PetCareUI : MonoBehaviour
{
    [Serializable]
    public sealed class Pet
    {
        public string name;
        public int dog, hunger = 55, happiness = 60, energy = 75;
        public bool fed, played, rested;
    }

    private const string SaveKey = "PetAR.CareProfile";
    public GameObject menu, help, selection, scan, care, pause, summary;
    public Button choose, resume, instructions, helpChoose, helpBack, selectionBack, confirm;
    public Button scanBack, preview, feed, play, rest, pauseButton, unpause, finish, returnToMenu;
    public Button[] dogs;
    public InputField nameInput;
    public Text selectionMessage, feedback, petTitle, summaryTitle, summaryText, scanTitle;
    public Text[] meterLabels;
    public Image[] meterFills;
    public Image petImage, scanCard;
    public Sprite[] dogPortraits, caregiverCards;

    public PetCareAR ar;
    public Image fullBackdrop, scanBackground, careBackground;
    public GameObject scanIllustration, careIllustration;
    public Text scanStatus, careSubtitle;

    private Pet pet;
    private GameObject current;
    private int selected;
    private bool scanFromSavedProfile;
    private static bool CaptureSession
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.SessionState.GetBool("PetAR.CaptureWeek4", false);
#else
            return false;
#endif
        }
    }

    private void Awake()
    {
        if (!CaptureSession && PlayerPrefs.HasKey(SaveKey))
        {
            try { pet = JsonUtility.FromJson<Pet>(PlayerPrefs.GetString(SaveKey)); }
            catch (ArgumentException) { }
        }
        if (pet != null && (pet.dog < 0 || pet.dog >= dogs.Length || string.IsNullOrWhiteSpace(pet.name)))
            pet = null;

        choose.onClick.AddListener(ChooseDog);
        resume.onClick.AddListener(() => OpenScan(true));
        instructions.onClick.AddListener(() => Show(help));
        helpChoose.onClick.AddListener(ChooseDog);
        helpBack.onClick.AddListener(() => Show(menu));
        selectionBack.onClick.AddListener(() => Show(menu));
        confirm.onClick.AddListener(ConfirmDog);
        nameInput.onValueChanged.AddListener(_ => UpdateSelection());
        scanBack.onClick.AddListener(() => Show(scanFromSavedProfile ? menu : selection));
        preview.onClick.AddListener(ScanAction);
        if (UseAR)
        {
            ar.Changed += RefreshAR;
            ar.SurfacePlaced += PlaceDogOnSurface;
        }
        feed.onClick.AddListener(Feed);
        play.onClick.AddListener(Play);
        rest.onClick.AddListener(Rest);
        pauseButton.onClick.AddListener(() => Show(pause));
        unpause.onClick.AddListener(ResumeCare);
        finish.onClick.AddListener(OpenSummary);
        returnToMenu.onClick.AddListener(() => Show(menu));
        for (int i = 0; i < dogs.Length; i++)
        {
            int index = i;
            dogs[i].onClick.AddListener(() => { selected = index; UpdateSelection(); });
        }

        Show(menu);
    }

    private bool UseAR => ar != null && PetCareAR.IsPhone && !CaptureSession;

    private void Show(GameObject panel)
    {
        if (UseAR && panel != scan && panel != care) ar.Close();
        if (current != null)
            current.SetActive(false);
        current = panel;
        menu.SetActive(panel == menu);
        help.SetActive(panel == help);
        selection.SetActive(panel == selection);
        scan.SetActive(panel == scan);
        care.SetActive(panel == care);
        pause.SetActive(panel == pause);
        summary.SetActive(panel == summary);
        resume.interactable = pet != null;
        if (UseAR) RefreshAR();
    }

    private void ChooseDog()
    {
        if (pet != null)
        {
            selected = pet.dog;
            nameInput.SetTextWithoutNotify(pet.name);
        }
        UpdateSelection();
        Show(selection);
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < dogs.Length; i++)
        {
            dogs[i].GetComponent<Image>().color = i == selected
                ? new Color32(222, 235, 255, 255) : Color.white;
            string breed = BreedName(i);
            string tile = breed == "German Shepherd" ? "Shepherd" : breed;
            dogs[i].GetComponentInChildren<Text>(true).text = i == selected ? $"{tile} ✓" : tile;
        }
        selectionMessage.text = $"Selected: {BreedName(selected)}  /  Caregiver {(char)('A' + selected)}";
    }

    private string BreedName(int index)
    {
        if (ar != null && index < ar.breedNames.Length && !string.IsNullOrEmpty(ar.breedNames[index]))
            return ar.breedNames[index];
        return $"Dog {index + 1}";
    }

    private void ConfirmDog()
    {
        string name = nameInput.text.Trim();
        if (name.Length == 0)
        {
            selectionMessage.text = "Enter a name for your dog to continue.";
            return;
        }
        if (pet == null || pet.dog != selected || pet.name != name)
            pet = new Pet { name = name, dog = selected };
        Save();
        OpenScan(false);
    }

    private void OpenScan(bool fromSavedProfile)
    {
        scanFromSavedProfile = fromSavedProfile;
        scanTitle.text = $"Scan Caregiver {(char)('A' + pet.dog)}";
        scanCard.sprite = caregiverCards[pet.dog];
        if (UseAR) ar.Close();
        Show(scan);
    }

    private void ScanAction()
    {
        if (!UseAR || ar.Ready)
        {
            OpenCare();
            return;
        }
        if (!ar.CameraOpen) ar.Open(pet.dog);
    }

    private void PlaceDogOnSurface()
    {
        if (current == scan) OpenCare();
    }

    private void ResumeCare()
    {
        if (UseAR) OpenScan(true);
        else Show(care);
    }

    private void RefreshAR()
    {
        bool live = ar.CameraOpen && (current == scan || current == care);
        SetAlpha(fullBackdrop, live ? 0f : 1f);
        SetAlpha(scanBackground, live ? 0f : 1f);
        SetAlpha(careBackground, live ? 0f : 1f);
        scanIllustration.SetActive(!live);
        careIllustration.SetActive(!live);
        scanStatus.text = string.IsNullOrEmpty(ar.Status) ? "Open the camera to scan a card or a flat surface." : ar.Status;
        var caption = preview.GetComponentInChildren<Text>(true);
        caption.text = ar.Ready ? "Start AR care" : ar.CameraOpen ? "Waiting for target" : "Open AR camera";
        preview.interactable = ar.Ready || !ar.CameraOpen;
        careSubtitle.text = ar.Ready ? $"Live AR · {ar.TargetLabel}" : "Live AR · Find the placement area";
        feed.interactable = play.interactable = rest.interactable = ar.CanInteract;
        if (current == care && !ar.Ready) feedback.text = ar.Status;
        else if (current == care && ar.IsGreeting) feedback.text = $"{pet.name} is happy to see you!";
        else if (current == care && feedback.text != null && feedback.text.StartsWith("Tracking lost"))
            feedback.text = "Tracking restored. Care can resume.";
    }

    private static void SetAlpha(Image target, float opacity)
    {
        Color color = target.color;
        color.a = opacity;
        target.color = color;
    }

    private void OpenCare()
    {
        petTitle.text = pet.name;
        petImage.sprite = dogPortraits[pet.dog];
        feedback.text = $"Choose how to care for {pet.name}.";
        RefreshMeters();
        if (UseAR) ar.ShowDog(true);
        Show(care);
    }

    private void Feed()
    {
        if (UseAR)
        {
            if (ar.PlayAction(PetCareAR.DogAction.Feed, CompleteMeal))
                feedback.text = $"{pet.name} is eating…";
            return;
        }
        CompleteMeal();
    }

    private void CompleteMeal()
    {
        pet.hunger = Mathf.Max(0, pet.hunger - 20);
        pet.fed = true;
        CompleteAction($"{pet.name} was fed.");
    }

    private void Play()
    {
        if (UseAR && !ar.PlayAction(PetCareAR.DogAction.Play)) return;
        pet.happiness = Mathf.Min(100, pet.happiness + 15);
        pet.energy = Mathf.Max(0, pet.energy - 10);
        pet.played = true;
        CompleteAction($"You played with {pet.name}.");
    }

    private void Rest()
    {
        if (UseAR && !ar.PlayAction(PetCareAR.DogAction.Rest)) return;
        pet.energy = Mathf.Min(100, pet.energy + 25);
        pet.rested = true;
        CompleteAction($"{pet.name} got some rest.");
    }

    private void CompleteAction(string message)
    {
        RefreshMeters();
        feedback.text = message;
        Save();
    }

    private void RefreshMeters()
    {
        SetMeter(0, "Hunger", pet.hunger);
        SetMeter(1, "Happiness", pet.happiness);
        SetMeter(2, "Energy", pet.energy);
    }

    private void SetMeter(int index, string label, int value)
    {
        meterLabels[index].text = $"{label}  {value}%";
        meterFills[index].fillAmount = value / 100f;
    }

    private void OpenSummary()
    {
        Save();
        summaryTitle.text = $"{pet.name}'s care summary";
        summaryText.text = $"Feed  ·  {Status(pet.fed)}\nPlay  ·  {Status(pet.played)}\nRest  ·  {Status(pet.rested)}"
            + $"\n\nHunger {pet.hunger}%  ·  Happiness {pet.happiness}%  ·  Energy {pet.energy}%";
        Show(summary);
    }

    private static string Status(bool done) => done ? "Done" : "Available";

    private void Save()
    {
        if (CaptureSession) return;
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(pet));
        PlayerPrefs.Save();
    }
}
