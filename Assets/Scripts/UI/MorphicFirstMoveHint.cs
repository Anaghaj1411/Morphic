using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Studio coaching chip shown after the gesture tutorial. Steps through
/// each live sculpt pose one at a time. Hides when the last pose is done.
internal static class MorphicFirstMoveHint
{
    public const float HoldSeconds = 0.22f;
    public const float RotateHoldSeconds = 0.06f;

    public static bool ShouldAdvance(
        bool matched,
        bool rotateStep,
        bool currentNone,
        ref float hold,
        bool lockStep = false)
    {
        float need = (rotateStep || lockStep) ? RotateHoldSeconds : HoldSeconds;
        if (matched)
        {
            hold += Time.unscaledDeltaTime;
            if (hold >= need)
            {
                hold = 0f;
                return true;
            }

            return false;
        }

        if ((rotateStep || lockStep) && currentNone)
            return false;

        hold = 0f;
        return false;
    }

    public static void CompleteIfFinished(
        int stepIndex,
        int count,
        ref bool armed,
        ref bool done,
        GameObject root)
    {
        if (stepIndex < count)
            return;

        armed = false;
        done = true;
        Hide(root);
    }

    public static readonly string[] LanternPrompts =
    {
        "WIDTH & HEIGHT: Point both index fingers. Move them apart or together, and up or down.",
        "TOP LID: Pinch with your right hand. Lift to open, lower to close.",
        "BOTTOM LID: Pinch with your left hand. Lift to open, lower to close.",
        "TASSEL: Make two fists and pull them down.",
        "BAMBOO: Make a V with both hands and spread them to the sides.",
        "ROTATE: Make one fist and move it left, right, up, or down.",
        "TOPPER: Make a V with your right hand and raise or lower it.",
        "BOTTOM: Make a V with your left hand and raise or lower it.",
        "LOCK: After you change the lantern, open your palm to lock the shape."
    };

    public static readonly TaiwanLanternGestureController.PracticeGesture[] LanternGestures =
    {
        TaiwanLanternGestureController.PracticeGesture.TwoIndex,
        TaiwanLanternGestureController.PracticeGesture.RightPinch,
        TaiwanLanternGestureController.PracticeGesture.LeftPinch,
        TaiwanLanternGestureController.PracticeGesture.TwoFists,
        TaiwanLanternGestureController.PracticeGesture.TwoV,
        TaiwanLanternGestureController.PracticeGesture.SingleFist,
        TaiwanLanternGestureController.PracticeGesture.RightV,
        TaiwanLanternGestureController.PracticeGesture.LeftV,
        TaiwanLanternGestureController.PracticeGesture.OpenPalm
    };

    public static readonly string[] TeapotPrompts =
    {
        "WIDTH & HEIGHT: Point both index fingers. Move them apart or together, and up or down.",
        "LID: Pinch with your right hand, then lift to raise the lid.",
        "SPOUT: Pinch with your left hand, then lift to pull the spout.",
        "HANDLE: Make two fists and pull them down.",
        "MIDDLE: Make a V with both hands and spread them to the sides.",
        "ROTATE: Make one fist and move it left, right, up, or down.",
        "BEVEL: Make a V with your right hand and raise or lower it.",
        "LOCK: After you change the teapot, open your palm to rest."
    };

    public static readonly TeapotGestureController.PracticeGesture[] TeapotGestures =
    {
        TeapotGestureController.PracticeGesture.TwoIndex,
        TeapotGestureController.PracticeGesture.RightPinch,
        TeapotGestureController.PracticeGesture.LeftPinch,
        TeapotGestureController.PracticeGesture.TwoFists,
        TeapotGestureController.PracticeGesture.TwoV,
        TeapotGestureController.PracticeGesture.SingleFist,
        TeapotGestureController.PracticeGesture.RightV,
        TeapotGestureController.PracticeGesture.OpenPalm
    };

    public static readonly string[] KerisPrompts =
    {
        "WIDTH & HEIGHT: Point both index fingers. Move them apart or together, and up or down.",
        "SHARPNESS: Pinch with your right hand, then lift to sharpen the blade.",
        "CURVE: Pinch with your left hand, then lift to curve the blade.",
        "HANDLE: Make two fists and pull them down.",
        "CARVING: Make a V with both hands and spread them to the sides.",
        "ROTATE: Make one fist and move it left, right, up, or down.",
        "LOCK: After you change the keris, open your palm to rest."
    };

    public static readonly KerisGestureController.PracticeGesture[] KerisGestures =
    {
        KerisGestureController.PracticeGesture.TwoIndex,
        KerisGestureController.PracticeGesture.RightPinch,
        KerisGestureController.PracticeGesture.LeftPinch,
        KerisGestureController.PracticeGesture.TwoFists,
        KerisGestureController.PracticeGesture.TwoV,
        KerisGestureController.PracticeGesture.SingleFist,
        KerisGestureController.PracticeGesture.OpenPalm
    };

    public static void Ensure(
        ref GameObject root,
        ref TMP_Text label,
        Transform canvas,
        string name)
    {
        if (root != null || canvas == null)
            return;

        Image frame = LanguagePageBuilder.CreateRoundedImage(
            name, canvas, 24, LanguagePageBuilder.Green);
        root = frame.gameObject;
        RectTransform rect = frame.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -22f);
        rect.sizeDelta = new Vector2(720f, 78f);
        frame.raycastTarget = false;

        Image fill = LanguagePageBuilder.CreateRoundedImage(
            "Fill", frame.transform, 20, LanguagePageBuilder.Cream);
        LanguagePageBuilder.Stretch(fill.rectTransform, new Vector2(5f, 5f));
        fill.raycastTarget = false;

        LanguagePageBuilder.TextLabel created = LanguagePageBuilder.CreateLabel(
            "Label", fill.transform, "", 18);
        LanguagePageBuilder.Stretch(created.Rect, new Vector2(16f, 8f));
        label = created.Label;
        if (label != null)
        {
            label.alignment = TextAlignmentOptions.Center;
            MorphicText.FitBody(label);
        }

        root.SetActive(false);
    }

    public static void Show(GameObject root, TMP_Text label, string message)
    {
        if (root == null)
            return;
        if (label != null)
        {
            MorphicLoc.Apply(label, message);
            MorphicText.FitBody(label);
        }

        root.SetActive(true);
        root.transform.SetAsLastSibling();
    }

    public static void Hide(GameObject root)
    {
        if (root != null)
            root.SetActive(false);
    }
}
