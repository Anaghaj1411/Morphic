using UnityEngine;
using UnityEngine.UI;

/// Lisa stills to the left of the intro dialogue, white keyed out.
/// Waves on the greeting, talks while she speaks, then stays talking.
/// Hidden when the camera zooms into the pottery shop and stays hidden after.
public sealed class MorphicLisaPortrait : MonoBehaviour
{
    private static readonly string[] WaveNames =
    {
        "Lisa/lisa_waving image",
        "Lisa/lisa_waving",
        "Lisa/LisaWave"
    };

    private static readonly string[] TalkNames =
    {
        "Lisa/lisa_talking image",
        "Lisa/lisa_talking",
        "Lisa/LisaTalk"
    };

    private enum Pose
    {
        Hidden,
        Waving,
        Talking
    }

    private static MorphicLisaPortrait instance;

    private GameObject root;
    private RawImage picture;
    private Texture2D waveTexture;
    private Texture2D talkTexture;
    private Pose pose = Pose.Hidden;
    private bool warnedMissing;
    private float waveUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        Ensure(null);
    }

    public static MorphicLisaPortrait Ensure(Transform canvas)
    {
        if (instance == null)
        {
            instance = Object.FindFirstObjectByType<MorphicLisaPortrait>(
                FindObjectsInactive.Include);
            if (instance != null && instance.transform.parent != null)
            {
                instance.transform.SetParent(null);
                DontDestroyOnLoad(instance.gameObject);
            }
        }

        if (instance == null)
        {
            GameObject host = new GameObject("MorphicLisaPortrait");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<MorphicLisaPortrait>();
        }

        instance.gameObject.SetActive(true);
        instance.LoadStills();
        if (canvas != null)
            instance.AttachUi(canvas);
        return instance;
    }

    public void SetVisible(bool visible)
    {
        LoadStills();
        if (root != null)
            root.SetActive(visible);

        if (visible)
        {
            PlacePortrait();
            PutBehindDialogue();
            ShowWave();
        }
        else
        {
            pose = Pose.Hidden;
        }
    }

    private void Update()
    {
        if (pose == Pose.Hidden || root == null || !root.activeInHierarchy)
            return;

        PlacePortrait();
        PutBehindDialogue();

        bool speak = MorphicLisaVoice.IsSpeaking;
        if (pose == Pose.Waving)
        {
            if (speak && Time.unscaledTime >= waveUntil)
                ShowTalk();
            return;
        }

        if (speak && pose != Pose.Talking)
            ShowTalk();
    }

    private void LoadStills()
    {
        if (waveTexture != null || talkTexture != null)
            return;

        waveTexture = LoadLisa(WaveNames);
        talkTexture = LoadLisa(TalkNames);
        if (talkTexture == null)
            talkTexture = waveTexture;
        if (waveTexture == null)
            waveTexture = talkTexture;

        if (waveTexture == null && talkTexture == null && !warnedMissing)
        {
            warnedMissing = true;
            Debug.LogWarning(
                "MORPHIC: Put lisa_talking and lisa_waving images in Assets/Resources/Lisa/");
        }
    }

    private static Texture2D LoadLisa(string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Texture2D texture = Resources.Load<Texture2D>(names[i]);
            if (texture != null)
                return texture;
        }

        Texture2D[] folder = Resources.LoadAll<Texture2D>("Lisa");
        if (folder == null || folder.Length == 0)
            return null;

        string needle = names[0];
        int slash = needle.LastIndexOf('/');
        if (slash >= 0)
            needle = needle.Substring(slash + 1);
        needle = needle.Replace(" image", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();

        for (int i = 0; i < folder.Length; i++)
        {
            string name = folder[i].name.Replace("_", "").Replace(" ", "").ToLowerInvariant();
            if (name.Contains(needle) || (needle.Contains("waving") && name.Contains("wav")) ||
                (needle.Contains("talking") && name.Contains("talk")))
                return folder[i];
        }

        return null;
    }

    private void AttachUi(Transform canvas)
    {
        if (canvas == null)
            return;

        if (root != null)
        {
            PlacePortrait();
            PutBehindDialogue();
            return;
        }

        root = new GameObject("LisaPortrait", typeof(RectTransform));
        root.transform.SetParent(canvas, false);
        PlacePortrait();

        GameObject pictureObject = new GameObject(
            "Picture", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        pictureObject.layer = root.layer;
        pictureObject.transform.SetParent(root.transform, false);
        picture = pictureObject.GetComponent<RawImage>();
        picture.raycastTarget = false;
        picture.maskable = false;
        picture.color = Color.white;
        picture.uvRect = new Rect(0f, 0f, 1f, 1f);
        LanguagePageBuilder.Stretch(picture.rectTransform);
        picture.material = CreateChromaMaterial();
        picture.texture = waveTexture != null ? waveTexture : talkTexture;
        root.SetActive(false);
        PutBehindDialogue();
    }

    private void PlacePortrait()
    {
        if (root == null)
            return;

        RectTransform frame = root.GetComponent<RectTransform>();
        frame.anchorMin = new Vector2(0.5f, 0f);
        frame.anchorMax = new Vector2(0.5f, 0f);
        frame.pivot = new Vector2(1f, 0f);
        frame.anchoredPosition = new Vector2(-230f, 48f);
        frame.sizeDelta = new Vector2(380f, 560f);
    }

    private void PutBehindDialogue()
    {
        if (root == null)
            return;

        Transform dialogue = FindDialoguePanel(root.transform.parent);
        if (dialogue == null)
            return;

        if (root.transform.parent != dialogue.parent)
            root.transform.SetParent(dialogue.parent, false);

        int dialogueIndex = dialogue.GetSiblingIndex();
        int lisaIndex = root.transform.GetSiblingIndex();
        if (lisaIndex > dialogueIndex)
            root.transform.SetSiblingIndex(dialogueIndex);

        Canvas lisaCanvas = root.GetComponent<Canvas>();
        if (lisaCanvas == null)
            lisaCanvas = root.AddComponent<Canvas>();
        lisaCanvas.overrideSorting = true;
        lisaCanvas.sortingOrder = -1;
        if (root.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            root.AddComponent<UnityEngine.UI.GraphicRaycaster>().enabled = false;
    }

    private static Transform FindDialoguePanel(Transform start)
    {
        if (start == null)
            return null;
        Transform direct = start.Find("Dialogue Panel");
        if (direct != null)
            return direct;

        Canvas canvas = start.GetComponentInParent<Canvas>(true);
        if (canvas == null)
            return GameObject.Find("Dialogue Panel") != null
                ? GameObject.Find("Dialogue Panel").transform
                : null;

        Transform[] children = canvas.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "Dialogue Panel")
                return children[i];
        }

        return null;
    }

    private void ShowWave()
    {
        pose = Pose.Waving;
        waveUntil = Time.unscaledTime + 1.4f;
        if (picture != null && waveTexture != null)
            picture.texture = waveTexture;
    }

    private void ShowTalk()
    {
        pose = Pose.Talking;
        if (picture != null && talkTexture != null)
            picture.texture = talkTexture;
    }

    private static Material CreateChromaMaterial()
    {
        Shader shader = Resources.Load<Shader>("MorphicLisaChroma");
        if (shader == null)
            shader = Shader.Find("Morphic/LisaChroma");
        if (shader == null)
            return null;
        Material material = new Material(shader);
        material.SetFloat("_Threshold", 0.10f);
        material.SetFloat("_Smoothness", 0.16f);
        material.SetFloat("_Spill", 0.82f);
        return material;
    }
}
