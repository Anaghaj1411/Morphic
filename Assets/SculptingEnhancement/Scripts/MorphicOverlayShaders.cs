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
}
