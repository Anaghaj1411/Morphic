using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Candy SETTINGS sheet: music, SFX, Lisa voice, and Lisa volume.
/// The top-left pill is shown from Lisa's intro onward; the homepage
/// Settings button opens the same sheet.
public sealed class MorphicSettingsUI : MonoBehaviour
{
    private GameObject root;
    private GameObject sheet;
    private GameObject pill;
    private Button settingsButton;
    private TMP_Text voiceToggleLabel;
    private TMP_Text volumeLabel;
    private TMP_Text musicLabel;
    private TMP_Text sfxLabel;

    public void SetVisible(bool visible)
    {
        EnsureUi();
        if (pill != null)
            pill.SetActive(visible);
        if (visible)
            PlaceSettingsButton();
    }

    public void OpenSheet()
    {
        EnsureUi();
        if (root == null || sheet == null)
            return;
        root.SetActive(true);
        sheet.SetActive(true);
        RefreshSoundLabels();
        sheet.transform.SetAsLastSibling();
        root.transform.SetAsLastSibling();
    }

    public void HideSheet()
    {
        if (sheet != null)
            sheet.SetActive(false);
    }

    private void Awake()
    {
        EnsureUi();
        SetVisible(false);
        MorphicLoc.Changed -= RefreshSheet;
        MorphicLoc.Changed += RefreshSheet;
    }

    private void OnDestroy()
    {
        MorphicLoc.Changed -= RefreshSheet;
    }

    private void RefreshSheet()
    {
        if (sheet != null)
            MorphicLoc.BindTree(sheet.transform);
        RefreshSoundLabels();
    }

    private void EnsureUi()
    {
        if (root != null)
        {
            WireButton();
            PlaceSettingsButton();
            RebuildSheetIfNeeded();
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform existing = canvas.transform.Find("MorphicSettings");
        if (existing != null)
        {
            root = existing.gameObject;
            Transform frame = existing.Find("Settings");
            pill = frame != null ? frame.gameObject : null;
            settingsButton = frame != null
                ? frame.GetComponent<Button>()
                : existing.GetComponentInChildren<Button>(true);
            Transform existingSheet = existing.Find("SettingsSheet");
            if (existingSheet != null)
                sheet = existingSheet.gameObject;
            WireButton();
            PlaceSettingsButton();
            RebuildSheetIfNeeded();
            return;
        }

        root = new GameObject("MorphicSettings", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        StretchFull(rootRect);

        Image frameImage = LanguagePageBuilder.CreateRoundedImage(
            "Settings", root.transform, 28, LanguagePageBuilder.Green);
        pill = frameImage.gameObject;
        PlaceSettingsButton(frameImage.rectTransform);
        frameImage.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frameImage.transform, 24, LanguagePageBuilder.Pink);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(5f, 5f));
        fill.raycastTarget = true;

        LanguagePageBuilder.TextLabel label = LanguagePageBuilder.CreateLabel(
            "Label", fill.transform, "SETTINGS", 18);
        LanguagePageBuilder.Stretch(label.Rect);
        if (label.Label != null)
            MorphicText.FitButton(label.Label);

        settingsButton = frameImage.gameObject.AddComponent<Button>();
        settingsButton.targetGraphic = fill;
        settingsButton.colors = LanguagePageBuilder.ButtonColors();
        WireButton();
        BuildSheet(root.transform);
        pill.SetActive(false);
    }

    private void WireButton()
    {
        if (settingsButton == null)
            return;
        settingsButton.onClick.RemoveAllListeners();
        settingsButton.onClick.AddListener(ToggleSheet);
        DialogueButtonClickSound.Instance?.RegisterButton(settingsButton);
    }

    private void BuildSheet(Transform parent)
    {
        Image outline = LanguagePageBuilder.CreateRoundedImage(
            "SettingsSheet", parent, 32, LanguagePageBuilder.Green);
        sheet = outline.gameObject;
        RectTransform box = outline.rectTransform;
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(500f, 500f);
        outline.raycastTarget = true;

        Image cream = LanguagePageBuilder.CreateRoundedImage(
            "Fill", outline.transform, 28, LanguagePageBuilder.Cream, false);
        LanguagePageBuilder.Stretch(cream.rectTransform, new Vector2(10f, 10f));
        cream.raycastTarget = true;

        LanguagePageBuilder.TextLabel title = LanguagePageBuilder.CreateLabel(
            "Title", cream.transform, "SETTINGS", 28);
        Place(title.Rect, new Vector2(0f, -16f), new Vector2(380f, 44f), new Vector2(0.5f, 1f));
        if (title.Label != null)
            MorphicText.FitTitle(title.Label);

        CreateVolumeRow(cream.transform, "Music", "MUSIC", -76f,
            () => NudgeMusic(-1f), () => NudgeMusic(1f), out musicLabel);

        CreateVolumeRow(cream.transform, "Sfx", "SFX", -148f,
            () => NudgeSfx(-1f), () => NudgeSfx(1f), out sfxLabel);

        voiceToggleLabel = CreateSheetButton(
            cream.transform, "VoiceToggle", VoiceToggleLabel(),
            new Vector2(0f, -220f), new Vector2(420f, 64f), ToggleVoice).GetComponentInChildren<TMP_Text>();

        CreateVolumeRow(cream.transform, "Vol", "LISA", -292f,
            () => NudgeVolume(-0.1f), () => NudgeVolume(0.1f), out volumeLabel);

        CreateSheetButton(cream.transform, "Exit", "EXIT", new Vector2(0f, -364f), new Vector2(420f, 64f),
            HideSheet);
        sheet.SetActive(false);
    }

    private void CreateVolumeRow(
        Transform parent,
        string name,
        string title,
        float y,
        UnityEngine.Events.UnityAction down,
        UnityEngine.Events.UnityAction up,
        out TMP_Text label)
    {
        Image frame = LanguagePageBuilder.CreateRoundedImage(
            name + "Row", parent, 22, LanguagePageBuilder.Green);
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(420f, 64f);
        frame.raycastTarget = false;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frame.transform, 18, LanguagePageBuilder.Pink);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(4f, 4f));
        fill.raycastTarget = false;

        LanguagePageBuilder.TextLabel titleLabel = LanguagePageBuilder.CreateLabel(
            "Title", fill.transform, title, 20);
        RectTransform titleRect = titleLabel.Rect;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 0.5f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.anchoredPosition = new Vector2(16f, 0f);
        titleRect.sizeDelta = new Vector2(110f, 48f);
        if (titleLabel.Label != null)
            MorphicText.FitButton(titleLabel.Label);

        CreateCircleStepper(fill.transform, name + "Down", "–", new Vector2(28f, 0f), down);
        label = CreateValuePill(fill.transform, name + "Readout", new Vector2(118f, 0f));
        CreateCircleStepper(fill.transform, name + "Up", "+", new Vector2(208f, 0f), up);
    }

    private static TMP_Text CreateValuePill(Transform parent, string name, Vector2 pos)
    {
        Image frame = LanguagePageBuilder.CreateRoundedImage(
            name, parent, 18, LanguagePageBuilder.Green);
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(92f, 42f);
        frame.raycastTarget = false;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frame.transform, 14, LanguagePageBuilder.PanelFill);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(3f, 3f));
        fill.raycastTarget = false;

        LanguagePageBuilder.TextLabel label = LanguagePageBuilder.CreateLabel(
            "Label", fill.transform, "100%", 18);
        LanguagePageBuilder.Stretch(label.Rect);
        if (label.Label != null)
            MorphicText.FitButton(label.Label);
        return label.Label;
    }

    private void CreateCircleStepper(
        Transform parent,
        string name,
        string symbol,
        Vector2 pos,
        UnityEngine.Events.UnityAction click)
    {
        const float size = 46f;
        Sprite circle = LanguagePageBuilder.Circle(128);

        Image disc = LanguagePageBuilder.CreateImage(name, parent, circle);
        RectTransform rect = disc.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(size, size);
        disc.color = LanguagePageBuilder.Green;
        disc.raycastTarget = true;
        disc.preserveAspect = true;

        Image core = LanguagePageBuilder.CreateImage("Core", disc.transform, circle);
        LanguagePageBuilder.Stretch(core.rectTransform, new Vector2(5f, 5f));
        core.color = LanguagePageBuilder.Pink;
        core.raycastTarget = false;
        core.preserveAspect = true;

        LanguagePageBuilder.TextLabel glyph = LanguagePageBuilder.CreateLabel(
            "Glyph", disc.transform, symbol, 26);
        LanguagePageBuilder.Stretch(glyph.Rect);
        if (glyph.Label != null)
            MorphicText.FitButton(glyph.Label);

        Button button = disc.gameObject.AddComponent<Button>();
        button.targetGraphic = disc;
        button.colors = LanguagePageBuilder.ButtonColors();
        button.onClick.AddListener(click);
        DialogueButtonClickSound.Instance?.RegisterButton(button);
    }

    private Button CreateSheetButton(
        Transform parent,
        string name,
        string english,
        Vector2 pos,
        Vector2 size,
        UnityEngine.Events.UnityAction click)
    {
        Image frame = LanguagePageBuilder.CreateRoundedImage(
            name, parent, 22, LanguagePageBuilder.Green);
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        frame.raycastTarget = true;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frame.transform, 18, LanguagePageBuilder.Pink);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(4f, 4f));
        fill.raycastTarget = click != null;

        LanguagePageBuilder.TextLabel label = LanguagePageBuilder.CreateLabel(
            "Label", fill.transform, english, 20);
        LanguagePageBuilder.Stretch(label.Rect);
        if (label.Label != null)
            MorphicText.FitButton(label.Label);

        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        button.colors = LanguagePageBuilder.ButtonColors();
        if (click != null)
        {
            button.onClick.AddListener(click);
            DialogueButtonClickSound.Instance?.RegisterButton(button);
        }
        else
            button.interactable = false;
        return button;
    }

    private void ToggleSheet()
    {
        EnsureUi();
        if (sheet == null)
            return;
        if (sheet.activeSelf)
            HideSheet();
        else
            OpenSheet();
    }

    private void ToggleVoice()
    {
        MorphicLisaVoice.Muted = !MorphicLisaVoice.Muted;
        RefreshSoundLabels();
    }

    private void NudgeVolume(float delta)
    {
        MorphicLisaVoice.Volume = MorphicLisaVoice.Volume + delta;
        RefreshSoundLabels();
    }

    private void NudgeMusic(float direction)
    {
        SettingsPageController audio = AudioSettings();
        if (audio == null)
            return;
        if (direction < 0f)
            audio.DecreaseMusic();
        else
            audio.IncreaseMusic();
        RefreshSoundLabels();
    }

    private void NudgeSfx(float direction)
    {
        SettingsPageController audio = AudioSettings();
        if (audio == null)
            return;
        if (direction < 0f)
            audio.DecreaseSfx();
        else
            audio.IncreaseSfx();
        RefreshSoundLabels();
    }

    private static SettingsPageController AudioSettings()
    {
        if (SettingsPageController.Instance == null)
            SettingsPageController.Install();
        return SettingsPageController.Instance;
    }

    private void RefreshSoundLabels()
    {
        if (voiceToggleLabel != null)
            MorphicLoc.Bind(voiceToggleLabel, VoiceToggleLabel());
        WriteReadout(volumeLabel, Percent(MorphicLisaVoice.Volume));
        WriteReadout(musicLabel, Percent(MusicVolume()));
        WriteReadout(sfxLabel, Percent(SfxVolume()));
    }

    private static void WriteReadout(TMP_Text label, string english)
    {
        if (label == null)
            return;
        label.text = english;
        MorphicText.FitButton(label);
    }

    private static string VoiceToggleLabel()
    {
        return MorphicLisaVoice.Muted ? "LISA VOICE: OFF" : "LISA VOICE: ON";
    }

    private static string Percent(float volume)
    {
        return Mathf.RoundToInt(volume * 100f) + "%";
    }

    private static float MusicVolume()
    {
        return AudioSettings() != null
            ? AudioSettings().MusicVolume
            : PlayerPrefs.GetFloat("Morphic.MusicVolume", 0.7f);
    }

    private static float SfxVolume()
    {
        return AudioSettings() != null
            ? AudioSettings().SfxVolume
            : PlayerPrefs.GetFloat("Morphic.SfxVolume", 0.7f);
    }

    private void RebuildSheetIfNeeded()
    {
        if (root == null)
            return;

        bool stale = sheet == null || FindNamed(sheet.transform, "MusicRow") == null;
        if (stale)
        {
            if (sheet != null)
            {
                Object.DestroyImmediate(sheet);
                sheet = null;
            }
            voiceToggleLabel = null;
            volumeLabel = null;
            musicLabel = null;
            sfxLabel = null;
            BuildSheet(root.transform);
        }

        CacheLabels();
    }

    private void CacheLabels()
    {
        if (sheet == null)
            return;
        if (voiceToggleLabel == null)
            voiceToggleLabel = LabelOn(sheet.transform, "VoiceToggle");
        if (volumeLabel == null)
            volumeLabel = LabelOn(sheet.transform, "VolReadout");
        if (musicLabel == null)
            musicLabel = LabelOn(sheet.transform, "MusicReadout");
        if (sfxLabel == null)
            sfxLabel = LabelOn(sheet.transform, "SfxReadout");
    }

    private static TMP_Text LabelOn(Transform root, string name)
    {
        Transform named = FindNamed(root, name);
        return named != null ? named.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private void PlaceSettingsButton()
    {
        if (pill == null && root != null)
        {
            Transform frame = root.transform.Find("Settings");
            if (frame != null)
                pill = frame.gameObject;
        }
        if (pill != null)
            PlaceSettingsButton(pill.transform as RectTransform);
    }

    private static void PlaceSettingsButton(RectTransform button)
    {
        if (button == null)
            return;

        button.anchorMin = button.anchorMax = new Vector2(0f, 1f);
        button.pivot = new Vector2(0f, 1f);
        button.anchoredPosition = new Vector2(StudioBackVisible() ? 124f : 24f, -22f);
        button.sizeDelta = new Vector2(188f, 54f);
    }

    private static bool StudioBackVisible()
    {
        return NamedActive("MenuBackButton") || NamedActive("SculptBackButton");
    }

    private static bool NamedActive(string name)
    {
        GameObject found = GameObject.Find(name);
        return found != null && found.activeInHierarchy;
    }

    private static Transform FindNamed(Transform root, string name)
    {
        if (root == null)
            return null;
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform nested = FindNamed(root.GetChild(i), name);
            if (nested != null)
                return nested;
        }
        return null;
    }

    private static void Place(RectTransform rect, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        if (rect == null)
            return;
        rect.anchorMin = rect.anchorMax = pivot;
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
