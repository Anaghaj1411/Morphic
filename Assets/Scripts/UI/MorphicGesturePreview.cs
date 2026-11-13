using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// Stationary green ring + stationary pink disk. Only the hands move,
/// clipped inside the pink so they cannot cover the green. Source JPGs
/// are stripped to green hands; the pink fill is a UI circle, never the
/// photo, so no white disk remains.
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
        OpenPalm,
        PointIndex,
        DrawBand
    }

    private const float FrameSize = 248f;
    private const float DiskSize = 196f;
    private const float RingThickness = 26f;
    private static readonly Color DiskPink = new Color32(0xFF, 0x9E, 0xE8, 0xFF);
    private static readonly Dictionary<Texture, Texture2D> handCache =
        new Dictionary<Texture, Texture2D>();
    private static Sprite ringSprite;

    private RawImage image;
    private RawImage leftHalf;
    private RawImage rightHalf;
    private RectTransform handsRect;
    private RectTransform leftRect;
    private RectTransform rightRect;
    private Motion motion;
    private float time;

    public static MorphicGesturePreview Create(RawImage target)
    {
        if (target == null)
            return null;

        RectTransform fill = target.rectTransform.parent as RectTransform;
        RectTransform frame = fill != null ? fill.parent as RectTransform : null;

        PaintDisk(frame, LanguagePageBuilder.Green, FrameSize);
        PaintDisk(fill, DiskPink, DiskSize);
        AttachGreenRing(frame);

        MorphicGesturePreview existing = target.GetComponentInParent<MorphicGesturePreview>();
        if (existing != null)
        {
            existing.image = target;
            existing.handsRect = target.rectTransform;
            existing.LockDisks(fill, frame);
            existing.EnsureHalves();
            return existing;
        }

        GameObject clip = new GameObject(
            "GestureClip",
            typeof(RectTransform),
            typeof(Image),
            typeof(Mask));
        RectTransform clipRect = clip.GetComponent<RectTransform>();
        clipRect.SetParent(fill, false);
        Center(clipRect, DiskSize);

        Image clipImage = clip.GetComponent<Image>();
        clipImage.sprite = LanguagePageBuilder.Circle(256);
        clipImage.type = Image.Type.Simple;
        clipImage.preserveAspect = true;
        clipImage.color = DiskPink;
        clipImage.raycastTarget = false;
        clip.GetComponent<Mask>().showMaskGraphic = true;

        RectTransform hands = target.rectTransform;
        hands.SetParent(clipRect, false);
        Stretch(hands);
        target.color = Color.white;
        target.raycastTarget = false;
        target.texture = null;

        MorphicGesturePreview preview = clip.AddComponent<MorphicGesturePreview>();
        preview.image = target;
        preview.handsRect = hands;
        preview.LockDisks(fill, frame);
        preview.EnsureHalves();
        AttachGreenRing(frame);
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

        Texture2D source = null;
        if (!string.IsNullOrEmpty(imagePath))
            source = Resources.Load<Texture2D>(imagePath.Replace(".jpg", string.Empty));
        Texture2D hands = HandsOnly(source);

        bool splitHands = motion == Motion.TwoV;
        bool rightHandOnly = motion == Motion.PointIndex || motion == Motion.DrawBand;
        if (image != null)
        {
            image.uvRect = rightHandOnly
                ? new Rect(0.5f, 0f, 0.5f, 1f)
                : new Rect(0f, 0f, 1f, 1f);
            image.texture = hands;
            image.color = Color.white;
            image.gameObject.SetActive(!splitHands);
        }

        ApplyTexture(leftHalf, hands);
        ApplyTexture(rightHalf, hands);
        if (leftHalf != null)
            leftHalf.gameObject.SetActive(splitHands);
        if (rightHalf != null)
            rightHalf.gameObject.SetActive(splitHands);

        Apply(0f);
    }

    private void LateUpdate()
    {
        if (motion == Motion.None)
            return;
        time += Time.unscaledDeltaTime;
        Apply(time);
    }

    private void Apply(float t)
    {
        float wave = (Mathf.Sin(t * 2.1f) + 1f) * 0.5f;
        float bob = Mathf.Sin(t * 2.4f);
        Vector2 pos = Vector2.zero;
        float scale = 1f;

        switch (motion)
        {
            case Motion.TwoIndex:
                pos = new Vector2(0f, bob * 8f);
                scale = Mathf.Lerp(0.96f, 1.04f, wave);
                break;
            case Motion.TwoFists:
                pos = new Vector2(0f, Mathf.Lerp(8f, -10f, wave));
                break;
            case Motion.RightPinch:
            case Motion.LeftPinch:
                pos = new Vector2(0f, Mathf.Lerp(-10f, 10f, wave));
                break;
            case Motion.TwoV:
                SpreadHands(Mathf.Lerp(0f, 18f, wave));
                break;
            case Motion.SingleFist:
                pos = new Vector2(Mathf.Sin(t * 1.8f) * 10f, Mathf.Cos(t * 1.8f) * 6f);
                break;
            case Motion.RightV:
            case Motion.LeftV:
                pos = new Vector2(0f, bob * 10f);
                break;
            case Motion.OpenPalm:
                scale = Mathf.Lerp(0.96f, 1.04f, wave);
                break;
            case Motion.PointIndex:
                pos = new Vector2(Mathf.Sin(t * 1.6f) * 12f, Mathf.Cos(t * 2.1f) * 8f);
                break;
            case Motion.DrawBand:
                pos = new Vector2(Mathf.Sin(t * 1.5f) * 14f, bob * 4f);
                break;
        }

        if (motion != Motion.TwoV)
            SpreadHands(0f);

        if (handsRect != null)
        {
            handsRect.anchoredPosition = pos;
            handsRect.localScale = new Vector3(scale, scale, 1f);
        }

        Transform clip = handsRect != null ? handsRect.parent : transform;
        if (clip != null)
        {
            clip.localPosition = Vector3.zero;
            clip.localScale = Vector3.one;
            clip.localEulerAngles = Vector3.zero;
            RectTransform clipRect = clip as RectTransform;
            if (clipRect != null)
            {
                clipRect.anchoredPosition = Vector2.zero;
                clipRect.sizeDelta = new Vector2(DiskSize, DiskSize);
            }
        }
    }

    private void SpreadHands(float spread)
    {
        float half = DiskSize * 0.25f;
        if (leftRect != null)
            leftRect.anchoredPosition = new Vector2(-half - spread, 0f);
        if (rightRect != null)
            rightRect.anchoredPosition = new Vector2(half + spread, 0f);
    }

    private void LockDisks(RectTransform fill, RectTransform frame)
    {
        if (fill == null && image != null)
            fill = image.rectTransform.parent as RectTransform;
        if (fill != null && frame == null)
            frame = fill.parent as RectTransform;

        PaintDisk(frame, LanguagePageBuilder.Green, FrameSize);
        PaintDisk(fill, DiskPink, DiskSize);
        AttachGreenRing(frame);

        RectTransform clip = transform as RectTransform;
        if (clip != null)
        {
            clip.anchoredPosition = Vector2.zero;
            clip.localScale = Vector3.one;
            clip.sizeDelta = new Vector2(DiskSize, DiskSize);
            Image pink = clip.GetComponent<Image>();
            if (pink != null)
            {
                pink.sprite = LanguagePageBuilder.Circle(256);
                pink.color = DiskPink;
                pink.enabled = true;
            }
        }
    }

    private void EnsureHalves()
    {
        if (handsRect == null && image != null)
            handsRect = image.rectTransform;
        if (leftHalf != null && rightHalf != null)
            return;
        if (handsRect == null)
            return;

        Transform parent = handsRect.parent;
        leftHalf = CreateHalf(parent, "LeftHand", true);
        rightHalf = CreateHalf(parent, "RightHand", false);
        leftRect = leftHalf.rectTransform;
        rightRect = rightHalf.rectTransform;
        leftHalf.gameObject.SetActive(false);
        rightHalf.gameObject.SetActive(false);
    }

    private static RawImage CreateHalf(Transform parent, string name, bool left)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(DiskSize * 0.5f, DiskSize);
        rect.anchoredPosition = new Vector2(left ? -DiskSize * 0.25f : DiskSize * 0.25f, 0f);

        RawImage raw = go.GetComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;
        raw.uvRect = left
            ? new Rect(0f, 0f, 0.5f, 1f)
            : new Rect(0.5f, 0f, 0.5f, 1f);
        return raw;
    }

    private static Texture2D HandsOnly(Texture source)
    {
        if (source == null)
            return null;
        if (handCache.TryGetValue(source, out Texture2D cached) && cached != null)
            return cached;

        Texture2D readable = CopyReadable(source);
        if (readable == null)
            return null;

        Color[] pixels = readable.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            bool isGreen = c.g > 0.18f && c.g > c.r + 0.03f && c.g > c.b + 0.02f;
            pixels[i] = isGreen
                ? new Color(c.r, c.g, c.b, 1f)
                : new Color(0f, 0f, 0f, 0f);
        }

        readable.SetPixels(pixels);
        readable.Apply(false, true);
        readable.wrapMode = TextureWrapMode.Clamp;
        readable.filterMode = FilterMode.Bilinear;
        handCache[source] = readable;
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
        raw.raycastTarget = false;
    }

    private static void PaintDisk(RectTransform rect, Color color, float size)
    {
        if (rect == null)
            return;
        Image image = rect.GetComponent<Image>();
        if (image == null)
            return;
        image.sprite = LanguagePageBuilder.Circle(256);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = color;
        Center(rect, size);
    }

    private static void AttachGreenRing(RectTransform frame)
    {
        if (frame == null)
            return;

        Transform existing = frame.Find("GreenRing");
        Image ring;
        if (existing != null)
        {
            ring = existing.GetComponent<Image>();
        }
        else
        {
            GameObject go = new GameObject(
                "GreenRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(frame, false);
            ring = go.GetComponent<Image>();
        }

        ring.sprite = RingSprite();
        ring.type = Image.Type.Simple;
        ring.preserveAspect = true;
        ring.color = Color.white;
        ring.raycastTarget = false;
        Center(ring.rectTransform, FrameSize);
        ring.transform.SetAsLastSibling();
    }

    private static Sprite RingSprite()
    {
        if (ringSprite != null)
            return ringSprite;

        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float c = size * 0.5f;
        float outer = c - 1f;
        float inner = outer - RingThickness * (size / FrameSize);
        Color green = LanguagePageBuilder.Green;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c;
                float dy = y + 0.5f - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float outerA = Mathf.Clamp01(outer - d + 0.5f);
                float hole = Mathf.Clamp01(inner - d + 0.5f);
                pixels[y * size + x] = new Color(green.r, green.g, green.b, outerA * (1f - hole));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        ringSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        ringSprite.name = "GestureGreenRing";
        return ringSprite;
    }

    private static void Center(RectTransform rect, float size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
        rect.localScale = Vector3.one;
        rect.localEulerAngles = Vector3.zero;
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
