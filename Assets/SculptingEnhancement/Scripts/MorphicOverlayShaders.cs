using UnityEngine;

internal static class MorphicOverlayShaders
{
    private static Shader selfieSurface;
    private static Shader bodyDesign;
    private static Shader patternRibbon;

    public static Shader SelfieSurface()
    {
        if (selfieSurface == null)
            selfieSurface = Shader.Find("Morphic/Selfie Surface Overlay");
        if (selfieSurface == null)
            Debug.LogError("MORPHIC: Selfie overlay shader was not included in this build.");
        return selfieSurface;
    }

    public static Shader BodyDesign()
    {
        if (bodyDesign == null)
            bodyDesign = Shader.Find("Morphic/Lantern Body Design Overlay");
        if (bodyDesign == null)
            Debug.LogError("MORPHIC: Body design overlay shader was not included in this build.");
        return bodyDesign;
    }

    public static Shader KerisBodyDesign()
    {
        Shader keris = Shader.Find("Morphic/Keris Body Design Overlay");
        return keris != null ? keris : BodyDesign();
    }

    public static Shader PatternRibbon()
    {
        if (patternRibbon == null)
            patternRibbon = Shader.Find("Morphic/Pattern Ribbon Stroke");
        return patternRibbon;
    }

    public static Texture2D CaptureWebcamFrame(Texture source)
    {
        if (source == null || source.width <= 16 || source.height <= 16)
            return null;

        WebCamTexture webcam = source as WebCamTexture;
        if (webcam != null && webcam.isPlaying)
        {
            Texture2D fromWebcam = new Texture2D(webcam.width, webcam.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            fromWebcam.SetPixels32(webcam.GetPixels32());
            fromWebcam.Apply(false, false);
            return fromWebcam;
        }

        const int maxDimension = 768;
        float scale = Mathf.Min(1f, maxDimension / (float)Mathf.Max(source.width, source.height));
        int width = Mathf.Max(2, Mathf.RoundToInt(source.width * scale));
        int height = Mathf.Max(2, Mathf.RoundToInt(source.height * scale));
        RenderTexture temporary = RenderTexture.GetTemporary(
            width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, temporary);
        RenderTexture.active = temporary;
        Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
        readable.Apply(false, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        return readable;
    }

    public static Texture2D BakeInkTint(
        Texture source,
        Color tint,
        Texture2D reuse,
        TextureWrapMode wrapU,
        TextureWrapMode wrapV)
    {
        return BakePattern(source, tint, reuse, wrapU, wrapV, true);
    }

    public static Texture2D BakeRibbonPattern(
        Texture source,
        Texture2D reuse,
        TextureWrapMode wrapU,
        TextureWrapMode wrapV)
    {
        return BakePattern(source, Color.white, reuse, wrapU, wrapV, false);
    }

    private static Texture2D BakePattern(
        Texture source,
        Color tint,
        Texture2D reuse,
        TextureWrapMode wrapU,
        TextureWrapMode wrapV,
        bool cutout)
    {
        if (source == null)
            return reuse;

        Texture2D readable = source as Texture2D;
        bool copied = false;
        if (readable == null || !readable.isReadable)
        {
            readable = CaptureWebcamFrame(source);
            copied = readable != source;
        }

        if (readable == null)
            return reuse;

        int width = readable.width;
        int height = readable.height;
        if (reuse == null || reuse.width != width || reuse.height != height)
        {
            if (reuse != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(reuse);
                else
                    UnityEngine.Object.DestroyImmediate(reuse);
            }

            reuse = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = source.name + (cutout ? "_TintedInk" : "_Ribbon"),
                filterMode = FilterMode.Bilinear,
                wrapModeU = wrapU,
                wrapModeV = wrapV
            };
        }
        else
        {
            reuse.wrapModeU = wrapU;
            reuse.wrapModeV = wrapV;
        }

        tint.a = 1f;
        Color[] pixels = readable.GetPixels();
        bool darkBackground = false;
        if (cutout)
            darkBackground = CornerBackgroundIsDark(pixels, width, height);

        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            float luma = pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f;
            if (cutout)
            {
                float lightInk = Mathf.Clamp01((luma - 0.32f) * 5f);
                float darkInk = Mathf.Clamp01((0.68f - luma) * 5f);
                float ink = pixel.a < 0.98f
                    ? pixel.a
                    : (darkBackground ? lightInk : darkInk);
                pixels[i] = new Color(tint.r, tint.g, tint.b, ink);
            }
            else
            {
                float shade = Mathf.Clamp01((luma - 0.5f) * 1.55f + 0.5f);
                float alpha = pixel.a < 0.02f ? 0f : 1f;
                pixels[i] = new Color(shade, shade, shade, alpha);
            }
        }

        reuse.SetPixels(pixels);
        reuse.Apply(false, false);
        if (copied && readable != reuse)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(readable);
            else
                UnityEngine.Object.DestroyImmediate(readable);
        }

        return reuse;
    }

    private static bool CornerBackgroundIsDark(Color[] pixels, int width, int height)
    {
        if (pixels == null || pixels.Length == 0 || width < 1 || height < 1)
            return false;

        int maxX = Mathf.Max(0, width - 1);
        int maxY = Mathf.Max(0, height - 1);
        int insetX = Mathf.Min(2, maxX);
        int insetY = Mathf.Min(2, maxY);
        int[] xs = { 0, insetX, maxX - insetX, maxX };
        int[] ys = { 0, insetY, maxY - insetY, maxY };
        float lumaSum = 0f;
        int count = 0;
        for (int yi = 0; yi < ys.Length; yi++)
        {
            int y = ys[yi];
            if (y < 0 || y >= height)
                continue;
            for (int xi = 0; xi < xs.Length; xi++)
            {
                int x = xs[xi];
                if (x < 0 || x >= width)
                    continue;
                Color pixel = pixels[y * width + x];
                if (pixel.a < 0.5f)
                    continue;
                lumaSum += pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f;
                count++;
            }
        }

        if (count == 0)
            return false;
        return (lumaSum / count) < 0.5f;
    }
}
