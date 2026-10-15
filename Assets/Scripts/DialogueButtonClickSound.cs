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

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip clickSound;

    [Range(0f, 1f)]
    public float volume = 0.5f;

    private int lastPlayedFrame = -1;
    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

    private void Awake()
    {
        Instance = this;
        EnsureAudioListener();
        RegisterAllSceneButtons();
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
        bool mouseClicked = false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            mouseClicked = true;
        }
#endif
        if (Input.GetMouseButtonDown(0))
        {
            mouseClicked = true;
        }

        // Catch clicks on ANY active UI Button, including runtime/dynamically created buttons
        if (mouseClicked && EventSystem.current != null)
        {
            Vector2 screenPos = Input.mousePosition;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                screenPos = Mouse.current.position.ReadValue();
            }
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
                {
                    PlayClick();
                    break;
                }
            }
        }
    }

    public void PlayClick()
    {
        // Avoid duplicate triggers on the exact same frame
        if (Time.frameCount == lastPlayedFrame)
            return;

        lastPlayedFrame = Time.frameCount;

        if (audioSource == null || clickSound == null)
            return;

        audioSource.PlayOneShot(clickSound, volume);
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
