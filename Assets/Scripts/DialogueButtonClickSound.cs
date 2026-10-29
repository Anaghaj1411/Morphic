using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class DialogueButtonClickSound : MonoBehaviour
{
    public static DialogueButtonClickSound Instance { get; private set; }

    private const string SfxKey = "Morphic.SfxVolume";
    private const float DefaultSfx = 0.7f;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip clickSound;
    public AudioClip hoverSound;

    [Range(0f, 1f)]
    public float volume = 0.5f;

    [Range(0f, 1f)]
    public float hoverVolume = 0.4f;

    private int lastPlayedFrame = -1;
    private int lastHoverFrame = -1;
    private Button lastHoveredButton;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    private void Awake()
    {
        Instance = this;
        EnsureAudioListener();
        RegisterAllSceneButtons();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void EnsureAudioListener()
    {
        if (FindObjectOfType<AudioListener>() == null)
        {
            Camera cam = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
            if (cam != null && cam.GetComponent<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }
        }
    }

    private void Update()
    {
        Button buttonUnderPointer = FindButtonUnderPointer();
        UpdateHover(buttonUnderPointer);

        if (!WasClickedThisFrame() || buttonUnderPointer == null)
            return;

        if (buttonUnderPointer.interactable && buttonUnderPointer.isActiveAndEnabled)
            PlayClick();
    }

    private void UpdateHover(Button buttonUnderPointer)
    {
        if (buttonUnderPointer == lastHoveredButton)
            return;

        lastHoveredButton = buttonUnderPointer;
        if (buttonUnderPointer != null &&
            buttonUnderPointer.interactable &&
            buttonUnderPointer.isActiveAndEnabled)
        {
            PlayHover();
        }
    }

    private bool WasClickedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
#endif
        return Input.GetMouseButtonDown(0);
    }

    private Button FindButtonUnderPointer()
    {
        if (EventSystem.current == null)
            return null;

        Vector2 screenPos = Input.mousePosition;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            screenPos = Mouse.current.position.ReadValue();
#endif

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };

        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        for (int i = 0; i < raycastResults.Count; i++)
        {
            Button btn = raycastResults[i].gameObject.GetComponentInParent<Button>();
            if (btn != null && btn.interactable && btn.isActiveAndEnabled)
                return btn;
        }

        return null;
    }

    public void PlayClick()
    {
        if (Time.frameCount == lastPlayedFrame)
            return;

        lastPlayedFrame = Time.frameCount;

        if (audioSource == null || clickSound == null)
            return;

        audioSource.PlayOneShot(clickSound, volume * CurrentSfxScale());
    }

    public void PlayHover()
    {
        if (Time.frameCount == lastHoverFrame)
            return;

        lastHoverFrame = Time.frameCount;

        if (audioSource == null || hoverSound == null)
            return;

        audioSource.PlayOneShot(hoverSound, hoverVolume * CurrentSfxScale());
    }

    private static float CurrentSfxScale()
    {
        float sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, DefaultSfx));
        // Volume sliders feel linear, but hearing is not. Square-ish
        // taper makes 10% a whisper while 100% stays full loudness.
        return Mathf.Pow(sfx, 2.8f);
    }

    public void RegisterButton(Button button)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(PlayClick);
            button.onClick.AddListener(PlayClick);
        }
    }

    public void RegisterAllSceneButtons()
    {
        foreach (Button button in FindObjectsOfType<Button>(true))
        {
            RegisterButton(button);
        }
    }
}
