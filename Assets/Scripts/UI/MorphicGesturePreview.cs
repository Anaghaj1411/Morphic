using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Loops the original Morphic gesture illustration. The green frame stays
/// still; only the pink circle with the hands inside it moves. Two-V
/// steps (bamboo / middle / carving) slide the green hands apart on a
/// solid pink circle so the circle never splits.
public sealed class MorphicGesturePreview : MonoBehaviour
{
    public enum Motion
    {
        None,
        TwoIndex,
        TwoFists,
        RightPinch,
        LeftPinch,
        RightV,
        LeftV,
        SingleFist,
        TwoV,
        OpenPalm
    }

    private const float CircleSize = 196f;
    private static readonly Color CirclePink = new Color(0.97f, 0.76f, 0.82f, 1f);
    private static readonly Dictionary<Texture, Texture2D> greenCache =
        new Dictionary<Texture, Texture2D>();
    private static readonly Dictionary<Texture, Texture2D> circleCache =
        new Dictionary<Texture, Texture2D>();

    private RawImage image;
    private RawImage leftHalf;
    private RawImage rightHalf;
    private RectTransform circleRect;
    private RectTransform leftRect;
    private RectTransform rightRect;
    private Motion motion;
    private float time;

    public static MorphicGesturePreview Create(RawImage target)
    {
        if (target == null)
            return null;

        MorphicGesturePreview existing = target.GetComponentInParent<MorphicGesturePreview>();
        if (existing != null)
        {
            existing.image = target;
            existing.EnsureHalves();
            return existing;
        }

        Transform art = target.rectTransform.parent;
        GameObject circle = new GameObject(
            "GestureCircle",
            typeof(RectTransform),
            typeof(Image),
            typeof(Mask));
        RectTransform circleRect = circle.GetComponent<RectTransform>();
        circleRect.SetParent(art, false);
        circleRect.anchorMin = circleRect.anchorMax = new Vector2(0.5f, 0.5f);
        circleRect.pivot = new Vector2(0.5f, 0.5f);
        circleRect.anchoredPosition = Vector2.zero;
        circleRect.sizeDelta = new Vector2(CircleSize, CircleSize);

        Image maskImage = circle.GetComponent<Image>();
        maskImage.sprite = LanguagePageBuilder.Circle(256);
        maskImage.color = CirclePink;
        maskImage.raycastTarget = false;
        circle.GetComponent<Mask>().showMaskGraphic = true;

        RectTransform imageRect = target.rectTransform;
        imageRect.SetParent(circleRect, false);
        Stretch(imageRect);

        MorphicGesturePreview preview = circle.AddComponent<MorphicGesturePreview>();
        preview.image = target;
        preview.circleRect = circleRect;
        preview.EnsureHalves();
        return preview;
    }

    public void Play(string gestureName, string imagePath)
    {
        if (string.IsNullOrEmpty(gestureName) ||
            !System.Enum.TryParse(gestureName, out Motion parsed))
            parsed = Motion.None;

        motion = parsed;
        time = 0f;
        EnsureHalves();

        Texture2D texture = null;
        if (!string.IsNullOrEmpty(imagePath))
            texture = Resources.Load<Texture2D>(imagePath.Replace(".jpg", string.Empty));

        bool splitHands = motion == Motion.TwoV;
        if (splitHands)
        {
            ApplyTexture(image, CircleFill(texture));
            Texture2D hands = GreenHands(texture);
            ApplyTexture(leftHalf, hands);
            ApplyTexture(rightHalf, hands);
        }
        else
        {
            ApplyTexture(image, texture);
        }

        if (image != null)
            image.gameObject.SetActive(true);
        if (leftHalf != null)
            leftHalf.gameObject.SetActive(splitHands);
        if (rightHalf != null)
            rightHalf.gameObject.SetActive(splitHands);

        Apply(0f);
    }

    private void LateUpdate()
    {
        if (motion == Motion.None || circleRect == null)
            return;
        time += Time.unscaledDeltaTime;
        Apply(time);
    }

    private void Apply(float t)
    {
        if (circleRect == null)
            return;

        float wave = (Mathf.Sin(t * 2.1f) + 1f) * 0.5f;
        float bob = Mathf.Sin(t * 2.4f);
        Vector2 pos = Vector2.zero;
        float scale = 1f;

        switch (motion)
        {
            case Motion.TwoIndex:
                pos = new Vector2(0f, bob * 10f);
                scale = Mathf.Lerp(0.94f, 1.05f, wave);
                break;
            case Motion.TwoFists:
                pos = new Vector2(0f, Mathf.Lerp(10f, -14f, wave));
                break;
            case Motion.RightPinch:
            case Motion.LeftPinch:
                pos = new Vector2(0f, Mathf.Lerp(-12f, 14f, wave));
                break;
            case Motion.TwoV:
                SpreadHands(Mathf.Lerp(0f, 26f, wave));
                break;
            case Motion.SingleFist:
                pos = new Vector2(Mathf.Sin(t * 1.8f) * 14f, Mathf.Cos(t * 1.8f) * 8f);
                break;
            case Motion.RightV:
            case Motion.LeftV:
                pos = new Vector2(0f, bob * 14f);
                break;
            case Motion.OpenPalm:
                scale = Mathf.Lerp(0.94f, 1.06f, wave);
                break;
        }

        if (motion != Motion.TwoV)
            SpreadHands(0f);

        circleRect.anchoredPosition = pos;
        circleRect.localScale = new Vector3(scale, scale, 1f);
        circleRect.localEulerAngles = Vector3.zero;
    }

    private void SpreadHands(float spread)
    {
        float half = CircleSize * 0.25f;
        if (leftRect != null)
            leftRect.anchoredPosition = new Vector2(-half - spread, 0f);
        if (rightRect != null)
            rightRect.anchoredPosition = new Vector2(half + spread, 0f);
    }

    private void EnsureHalves()
    {
        if (circleRect == null && image != null)
            circleRect = image.rectTransform.parent as RectTransform;
        if (circleRect == null)
            return;
        if (leftHalf != null && rightHalf != null)
            return;

        leftHalf = CreateHalf("LeftHand", true);
        rightHalf = CreateHalf("RightHand", false);
        leftRect = leftHalf.rectTransform;
        rightRect = rightHalf.rectTransform;
        leftHalf.gameObject.SetActive(false);
        rightHalf.gameObject.SetActive(false);
    }

    private RawImage CreateHalf(string name, bool left)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(circleRect, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(CircleSize * 0.5f, CircleSize);
        rect.anchoredPosition = new Vector2(left ? -CircleSize * 0.25f : CircleSize * 0.25f, 0f);

        RawImage raw = go.GetComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;
        raw.uvRect = left
            ? new Rect(0f, 0f, 0.5f, 1f)
            : new Rect(0.5f, 0f, 0.5f, 1f);
        return raw;
    }

    private static Texture2D GreenHands(Texture source)
    {
        return Process(source, greenCache, keepGreen: true);
    }

    private static Texture2D CircleFill(Texture source)
    {
        return Process(source, circleCache, keepGreen: false);
    }

    private static Texture2D Process(
        Texture source,
        Dictionary<Texture, Texture2D> cache,
        bool keepGreen)
    {
        if (source == null)
            return null;
        if (cache.TryGetValue(source, out Texture2D cached) && cached != null)
            return cached;

        Texture2D readable = CopyReadable(source);
        if (readable == null)
            return null;

        Color[] pixels = readable.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            bool isGreen = c.g > 0.28f && c.g > c.r + 0.05f && c.g > c.b + 0.04f;
            if (keepGreen)
                pixels[i] = isGreen ? new Color(c.r, c.g, c.b, 1f) : new Color(0f, 0f, 0f, 0f);
            else
                pixels[i] = isGreen ? CirclePink : c;
        }

        readable.SetPixels(pixels);
        readable.Apply(false, true);
        readable.wrapMode = TextureWrapMode.Clamp;
        readable.filterMode = FilterMode.Bilinear;
        cache[source] = readable;
        return readable;
    }

    private static Texture2D CopyReadable(Texture source)
    {
        int width = source.width;
        int height = source.height;
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }

    private static void ApplyTexture(RawImage raw, Texture texture)
    {
        if (raw == null)
            return;
        raw.texture = texture;
        raw.color = Color.white;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localEulerAngles = Vector3.zero;
    }
}
