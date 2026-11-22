using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class PetCareWeek4
{
    private const string ScenePath = "Assets/Scenes/PetAR.unity";
    private const string ArtPath = "Assets/PetCare/Art/";
    private const string CaptureFlag = "PetAR.CaptureWeek4";
    private const int Width = 720, Height = 1000;
    private static readonly Color Navy = new Color32(17, 36, 62, 255);
    private static readonly Color Blue = new Color32(27, 80, 212, 255);
    private static readonly Color Slate = new Color32(62, 82, 108, 255);
    private static readonly Color Cloud = new Color32(239, 245, 252, 255);
    private static readonly Color Line = new Color32(211, 223, 240, 255);
    private static readonly Color Green = new Color32(19, 107, 88, 255);
    private static readonly Color Mint = new Color32(218, 245, 234, 255);
    private static readonly Color Paper = Color.white;
    private static readonly Color Warm = new Color32(255, 231, 170, 255);
    private static Sprite rounded;
    private static Font font;

    static PetCareWeek4()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(CaptureFlag, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += Capture;
            if (state == PlayModeStateChange.EnteredEditMode)
                SessionState.EraseBool(CaptureFlag);
        };
    }

    [MenuItem("Tools/PetAR/Build Week 4 Interface")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath || scene.isDirty)
        {
            Debug.LogError("Save and open PetAR before building the care interface.");
            return;
        }
        if (scene.GetRootGameObjects().Any(go => go.GetComponentInChildren<PetCareUI>(true)))
        {
            Debug.LogWarning("PetAR already has its Week 4 Canvas. Edit the existing hierarchy.");
            return;
        }

        CreateRoundedSprite();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root = new GameObject("Pet Care Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PetCareUI));
        root.layer = 5;
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(Width, Height);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var full = root.GetComponent<RectTransform>();
        var backdrop = Rect(full, "Background", 0, 0, 0, 0);
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
        AddImage(backdrop, Navy);
        var frame = Rect(full, "Portrait Layout", -Width / 2, -Height / 2, Width, Height);
        frame.anchorMin = frame.anchorMax = new Vector2(.5f, .5f);
        var ui = root.GetComponent<PetCareUI>();
        ui.dogPortraits = Enumerable.Range(1, 5).Select(i => LoadSprite($"Dog{i}.png")).ToArray();
        ui.dogPortraits[0] = LoadSprite("Biscuit.png");
        ui.caregiverCards = Enumerable.Range(1, 5).Select(i => LoadSprite($"Caregiver{i}.png")).ToArray();

        MakeMenu(ui, frame);
        MakeHelp(ui, frame);
        MakeSelection(ui, frame);
        MakeScan(ui, frame);
        MakeCare(ui, frame);
        MakePause(ui, frame);
        MakeSummary(ui, frame);
        foreach (RectTransform panel in frame)
            panel.gameObject.SetActive(panel.gameObject == ui.menu);
        if (!scene.GetRootGameObjects().Any(go => go.GetComponentInChildren<EventSystem>(true)))
            new GameObject("Pet Care EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("PetAR scene did not save.");
        var buildScenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (entry.path == ScenePath) continue;
            string path = entry.path == "Assets/ARFramework.unity" ? "Assets/Scenes/ARFramework.unity" : entry.path;
            buildScenes.Add(new EditorBuildSettingsScene(path, entry.enabled));
        }
        EditorBuildSettings.scenes = buildScenes.ToArray();
        Selection.activeGameObject = root;
        Debug.Log("Week 4 interface saved to PetAR.");
    }

    [MenuItem("Tools/PetAR/Verify and Capture Week 4")]
    public static void VerifyAndCapture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath || scene.isDirty || UnityEngine.Object.FindObjectOfType<PetCareUI>() == null)
        {
            Debug.LogError("Open the saved PetAR scene before capturing it.");
            return;
        }
        SessionState.SetBool(CaptureFlag, true);
        EditorApplication.isPlaying = true;
    }

    private static void Capture()
    {
        try
        {
            string folder = Path.GetFullPath("Temp/PetAR-Week4");
            Directory.CreateDirectory(folder);
            var ui = UnityEngine.Object.FindObjectOfType<PetCareUI>();
            if (ui == null) throw new InvalidOperationException("Pet care UI was not found in Play mode.");
            RequireVisible(ui.menu);
            Require(!ui.resume.interactable, "A new profile started with Continue enabled.");
            SaveImage(ui, folder, "01-PetAR-Main-Menu.png");

            ui.instructions.onClick.Invoke();
            RequireVisible(ui.help);
            ui.helpChoose.onClick.Invoke();
            RequireVisible(ui.selection);
            ui.confirm.onClick.Invoke();
            RequireVisible(ui.selection);
            Require(ui.selectionMessage.text.StartsWith("Enter a name"), "Blank name was accepted.");
            ui.dogs[2].onClick.Invoke();
            Require(ui.selectionMessage.text.Contains("Caregiver C"), "Dog selection did not update the caregiver.");
            ui.dogs[0].onClick.Invoke();
            ui.nameInput.text = "Biscuit";
            SaveImage(ui, folder, "02-PetAR-Dog-Selection.png");

            ui.confirm.onClick.Invoke();
            RequireVisible(ui.scan);
            Require(ui.scanTitle.text == "Scan Caregiver A", "Selected caregiver did not carry over.");
            SaveImage(ui, folder, "03-PetAR-Scan-Instructions.png");
            ui.preview.onClick.Invoke();
            RequireVisible(ui.care);
            ui.feed.onClick.Invoke();
            ui.play.onClick.Invoke();
            ui.rest.onClick.Invoke();
            Require(ui.meterLabels[0].text.Contains("35%") && ui.meterLabels[1].text.Contains("75%")
                && ui.meterLabels[2].text.Contains("90%"), "Care actions produced incorrect meter values.");
            SaveImage(ui, folder, "04-PetAR-Care-UI.png");

            ui.pauseButton.onClick.Invoke();
            RequireVisible(ui.pause);
            ui.unpause.onClick.Invoke();
            RequireVisible(ui.care);
            ui.pauseButton.onClick.Invoke();
            ui.finish.onClick.Invoke();
            RequireVisible(ui.summary);
            Require(ui.summaryText.text.Contains("Rest  ·  Done"), "Completed action was missing from the summary.");
            ui.returnToMenu.onClick.Invoke();
            RequireVisible(ui.menu);
            Require(ui.resume.interactable, "Continue was disabled after creating a profile.");
            ui.resume.onClick.Invoke();
            RequireVisible(ui.scan);
            ui.preview.onClick.Invoke();
            Require(ui.meterLabels[2].text.Contains("90%"), "Progress changed after reopening the care screen.");
            File.WriteAllText(Path.Combine(folder, "verification.txt"),
                "Unity Play mode passed: menu navigation, name validation, dog selection, scan instructions, care actions and meters, pause/resume, summary, and returning to the active profile.");
            Debug.Log("PetAR UI checks passed. Evidence written to " + folder);
        }
        catch (Exception e) { Debug.LogException(e); }
        finally { EditorApplication.isPlaying = false; }
    }

    private static void RequireVisible(GameObject panel) =>
        Require(panel.activeInHierarchy, $"Expected {panel.name} to be visible.");

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    private static void SaveImage(PetCareUI ui, string folder, string name)
    {
        var canvas = ui.GetComponent<Canvas>();
        var scaler = ui.GetComponent<CanvasScaler>();
        var cameraObject = new GameObject("PetAR evidence camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Navy;
        camera.cullingMask = 1 << 5;
        camera.orthographic = true;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 100;
        var target = new RenderTexture(Width, Height, 24);
        var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            canvas.scaleFactor = 1f;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(folder, name), pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            canvas.worldCamera = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static void MakeMenu(PetCareUI ui, RectTransform frame)
    {
        var p = Panel(frame, "Main Menu Panel", "AR Dog Care", "Virtual pet simulator");
        ui.menu = p.gameObject;
        var hero = Box(p, "Dog illustration panel", 42, 156, 636, 446, Paper);
        Box(hero, "Illustration backdrop", 152, 37, 332, 245, new Color32(228, 239, 254, 255));
        Artwork(hero, "Dog illustration", LoadSprite("Biscuit.png"), 212, 47, 212, 216);
        Label(hero, "Heading", "Care for your\nvirtual dog", 40, 293, 556, 102, 47,
            Navy, true, TextAnchor.MiddleCenter);
        Label(hero, "Description", "Choose a dog and start your care routine.", 35, 400, 566, 30, 23,
            Slate, false, TextAnchor.MiddleCenter);
        ui.choose = Action(p, "Choose a dog", 42, 639, 636, 76, true);
        ui.resume = Action(p, "Continue care", 42, 730, 636, 68, false);
        ui.instructions = Action(p, "How to care", 42, 814, 636, 68, false);
        ui.resume.interactable = false;
    }

    private static void MakeHelp(PetCareUI ui, RectTransform frame)
    {
        var p = Panel(frame, "Instructions Panel", "How to care", "Get started");
        ui.help = p.gameObject;
        Step(p, "01", "Choose and name your dog", "Pick one of five dogs and give it a name.", 167);
        Step(p, "02", "Find its caregiver card", "The selected dog has a matching illustrated card.", 322);
        Step(p, "03", "Feed, play, and rest", "Watch how each action changes the three meters.", 477);
        Label(p, "Care context", "The meters describe your virtual dog's needs.", 50, 668, 620, 50, 25, Slate);
        ui.helpChoose = Action(p, "Choose a dog", 42, 778, 636, 74, true);
        ui.helpBack = Action(p, "Back to menu", 42, 871, 636, 66, false);
    }

    private static void Step(RectTransform panel, string number, string heading, string copy, int y)
    {
        var card = Box(panel, heading + " Panel", 42, y, 636, 133, Paper);
        var tag = Box(card, "Step number panel", 24, 33, 64, 64, Blue);
        Label(tag, "Step number", number, 0, 0, 64, 64, 25, Paper, true, TextAnchor.MiddleCenter);
        Label(card, "Title", heading, 113, 25, 494, 39, 29, Navy, true);
        Label(card, "Instructions", copy, 113, 68, 490, 51, 23, Slate);
    }

    private static void MakeSelection(PetCareUI ui, RectTransform frame)
    {
        var p = Panel(frame, "Dog Selection Panel", "Choose your dog", "Dog and caregiver card");
        ui.selection = p.gameObject;
        Label(p, "Dog choices", "Choose one of five dogs", 42, 157, 636, 50, 33, Navy, true);
        ui.dogs = new Button[5];
        for (int i = 0; i < 5; i++)
        {
            string label = i == 0 ? "Dog 1 ✓" : $"Dog {i + 1}";
            var tile = Box(p, $"Dog {i + 1} option", 42 + i * 130, 224, 116, 156, i == 0 ? new Color32(222, 235, 255, 255) : Paper);
            ui.dogs[i] = ButtonOn(tile);
            Artwork(tile, "Dog portrait", ui.dogPortraits[i], 18, 18, 80, 77);
            Label(tile, "Dog choice label", label, 5, 102, 106, 40, 21, Navy, true, TextAnchor.MiddleCenter);
        }

        Label(p, "Name label", "Dog name", 42, 416, 636, 50, 32, Navy, true);
        var field = Box(p, "Dog Name Input", 42, 478, 636, 76, Paper);
        var input = field.gameObject.AddComponent<InputField>();
        input.targetGraphic = field.GetComponent<Image>();
        input.targetGraphic.raycastTarget = true;
        input.characterLimit = 24;
        input.textComponent = Label(field, "Name", "", 22, 0, 591, 76, 30, Navy, richText: false);
        input.placeholder = Label(field, "Placeholder", "Enter a name", 22, 0, 591, 76, 28, Slate);
        ui.nameInput = input;

        var assignment = Box(p, "Caregiver assignment panel", 42, 590, 636, 160, Paper);
        Label(assignment, "Card selection title", "Caregiver card", 25, 24, 586, 42, 30, Navy, true);
        ui.selectionMessage = Label(assignment, "Selected dog and validation", "Selected: Dog 1  /  Caregiver A",
            25, 77, 586, 52, 25, Blue, true);
        ui.confirm = Action(p, "Continue to card setup", 42, 792, 636, 76, true);
        ui.selectionBack = Action(p, "Back to menu", 42, 884, 636, 65, false);
    }

    private static void MakeScan(PetCareUI ui, RectTransform frame)
    {
        var p = Panel(frame, "Caregiver Scan Panel", "Caregiver card", "Camera setup");
        ui.scan = p.gameObject;
        ui.scanTitle = Label(p, "Caregiver title", "Scan Caregiver A", 42, 151, 636, 51, 36, Navy, true);
        var scanFrame = Box(p, "Scan Guide Panel", 42, 223, 636, 393, Navy);
        Box(scanFrame, "Card frame", 194, 46, 248, 260, Paper);
        Box(scanFrame, "Card accent", 214, 67, 208, 14, Blue);
        ui.scanCard = Artwork(scanFrame, "Caregiver portrait", ui.caregiverCards[0], 245, 104, 146, 166);
        Label(scanFrame, "Mode", "CARD PREVIEW", 75, 326, 486, 41, 23, Warm, true, TextAnchor.MiddleCenter);
        Label(p, "Card instructions", "Lay the matching card flat and keep it in view.", 48, 640, 624, 60, 26, Slate);
        Label(p, "Preview explanation", "Care preview works without a camera.", 48, 704, 624, 55, 25, Slate);
        ui.preview = Action(p, "Open care preview", 42, 790, 636, 76, true);
        ui.scanBack = Action(p, "Back", 42, 883, 636, 65, false);
    }

    private static void MakeCare(PetCareUI ui, RectTransform frame)
    {
        var p = Panel(frame, "Dog Care Panel", "Dog care", "Care preview");
        ui.care = p.gameObject;
        ui.pauseButton = Action(p, "Pause", 526, 45, 152, 58, false);
        var meters = Box(p, "Care Meters Panel", 42, 154, 636, 252, Paper);
        ui.meterLabels = new Text[3];
        ui.meterFills = new Image[3];
        string[] names = { "Hunger", "Happiness", "Energy" };
        int[] values = { 55, 60, 75 };
        Color[] colors = { new Color32(180, 109, 19, 255), Blue, Green };
        for (int i = 0; i < 3; i++)
        {
            int y = 19 + i * 72;
            ui.meterLabels[i] = Label(meters, names[i] + " value", $"{names[i]}  {values[i]}%",
                28, y, 580, 37, 26, Navy, true);
            Box(meters, names[i] + " track", 29, y + 43, 578, 13, Line, false);
            var fillRect = Rect(meters, names[i] + " meter fill", 29, y + 43, 578, 13);
            var fill = AddImage(fillRect, colors[i]);
            fill.sprite = rounded;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = values[i] / 100f;
            ui.meterFills[i] = fill;
        }

        var portrait = Box(p, "Pet Preview Panel", 42, 424, 636, 294, new Color32(224, 236, 255, 255));
        ui.petTitle = Label(portrait, "Pet name", "Biscuit", 24, 11, 588, 56, 40, Navy, true, TextAnchor.MiddleCenter, false);
        ui.petImage = Artwork(portrait, "Selected dog illustration", ui.dogPortraits[0], 207, 73, 222, 201);
        var status = Box(p, "Action Feedback Panel", 42, 735, 636, 61, Mint);
        ui.feedback = Label(status, "Feedback", "Choose a care action.", 20, 0, 596, 61, 25,
            Green, true, TextAnchor.MiddleCenter, false);
        ui.feed = Action(p, "Feed", 42, 830, 198, 77, true);
        ui.play = Action(p, "Play", 261, 830, 198, 77, true);
        ui.rest = Action(p, "Rest", 480, 830, 198, 77, true);
    }

    private static void MakePause(PetCareUI ui, RectTransform frame)
    {
        var p = Panel(frame, "Pause Panel", "Care paused", "Your dog can wait");
        ui.pause = p.gameObject;
        var card = Box(p, "Pause message panel", 42, 175, 636, 244, Paper);
        Label(card, "Pause state", "Progress saved", 33, 41, 570, 67, 42, Navy, true);
        Label(card, "Pause options", "Resume your care routine or finish to see your session summary.",
            33, 122, 563, 90, 29, Slate);
        ui.unpause = Action(p, "Resume care", 42, 492, 636, 76, true);
        ui.finish = Action(p, "Finish and view summary", 42, 587, 636, 70, false);
    }

    private static void MakeSummary(PetCareUI ui, RectTransform frame)
    {
        var p = Panel(frame, "Care Summary Panel", "Care summary", "Progress saved on this device");
        ui.summary = p.gameObject;
        var card = Box(p, "Session Results Panel", 42, 166, 636, 364, Paper);
        ui.summaryTitle = Label(card, "Summary title", "Care summary", 28, 25, 580, 77, 35, Navy, true,
            TextAnchor.MiddleLeft, false);
        ui.summaryText = Label(card, "Care actions and final meters", "", 28, 115, 580, 218, 28, Slate, false,
            TextAnchor.UpperLeft, false);
        ui.returnToMenu = Action(p, "Return to menu", 42, 589, 636, 76, true);
    }

    private static RectTransform Panel(RectTransform parent, string name, string heading, string subtitle)
    {
        var panel = Rect(parent, name, 0, 0, Width, Height);
        AddImage(panel, Cloud);
        var header = Rect(panel, "Header Panel", 0, 0, Width, 132);
        AddImage(header, Navy);
        Label(header, "Screen title", heading, 42, 29, 473, 54, 43, Paper, true);
        Label(header, "Screen subtitle", subtitle, 44, 82, 470, 33, 23, new Color32(192, 213, 242, 255));
        return panel;
    }

    private static RectTransform Box(RectTransform parent, string name, int x, int y, int width, int height,
        Color color, bool sliced = true)
    {
        var rect = Rect(parent, name, x, y, width, height);
        var image = AddImage(rect, color);
        if (sliced) { image.sprite = rounded; image.type = Image.Type.Sliced; }
        return rect;
    }

    private static Button Action(RectTransform parent, string title, int x, int y, int w, int h, bool primary)
    {
        var rect = Box(parent, title + " Button", x, y, w, h, primary ? Blue : Paper);
        var button = ButtonOn(rect);
        Label(rect, "Button label", title, 8, 0, w - 16, h, 29, primary ? Paper : Navy, true, TextAnchor.MiddleCenter);
        return button;
    }

    private static Button ButtonOn(RectTransform rect)
    {
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.targetGraphic.raycastTarget = true;
        var colors = button.colors;
        colors.highlightedColor = new Color32(227, 236, 255, 255);
        colors.pressedColor = new Color32(191, 210, 253, 255);
        colors.disabledColor = new Color32(201, 212, 229, 255);
        button.colors = colors;
        return button;
    }

    private static Text Label(RectTransform parent, string name, string value, int x, int y, int w,
        int h, int size, Color color, bool bold = false, TextAnchor alignment = TextAnchor.MiddleLeft, bool richText = true)
    {
        var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        text.supportRichText = richText;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Math.Max(size - 7, 17);
        text.resizeTextMaxSize = size;
        return text;
    }

    private static Image Artwork(RectTransform parent, string name, Sprite sprite, int x, int y, int w, int h)
    {
        var image = AddImage(Rect(parent, name, x, y, w, h), Paper);
        image.sprite = sprite;
        image.preserveAspect = true;
        return image;
    }

    private static Image AddImage(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Rect(RectTransform parent, string name, int x, int y, int w, int h)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.layer = 5;
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    private static Sprite LoadSprite(string file)
    {
        string assetPath = ArtPath + file;
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("PetAR artwork is missing.", assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        if (file == "Rounded.png") importer.spriteBorder = new Vector4(19, 19, 19, 19);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static void CreateRoundedSprite()
    {
        const int size = 64;
        const float radius = 18;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(Mathf.Abs(x + .5f - size / 2f) - (size / 2f - radius), 0);
            float dy = Mathf.Max(Mathf.Abs(y + .5f - size / 2f) - (size / 2f - radius), 0);
            pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy)));
        }
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(ArtPath + "Rounded.png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        rounded = LoadSprite("Rounded.png");
    }
}


