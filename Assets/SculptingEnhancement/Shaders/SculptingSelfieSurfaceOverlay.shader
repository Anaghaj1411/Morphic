Shader "Morphic/Selfie Surface Overlay"
{
    Properties
    {
        _SelfieTex ("Selfie", 2D) = "white" {}
        _Opacity ("Opacity", Range(0, 1)) = 1
        _PatchHalfAngle ("Patch Half Angle", Range(0.05, 0.75)) = 0.52
        _VerticalHalfFraction ("Vertical Half Fraction", Range(0.05, 0.9)) = 0.42
        _ObjectCenter ("Object Center", Vector) = (0, 0, 0, 0)
        _ObjectExtents ("Object Extents", Vector) = (1, 1, 1, 0)
        _BrushEdgeWidth ("Brush Edge Width", Range(0.005, 0.12)) = 0.025
        _BrushEdgeRoughness ("Brush Edge Roughness", Range(0, 0.4)) = 0.085
        _BrushEdgeFeather ("Brush Edge Feather", Range(0.002, 0.05)) = 0.012
        _BrushCornerRoundness ("Brush Corner Roundness", Range(0, 0.8)) = 0.28
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "SelfieOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SelfieTex);
            SAMPLER(sampler_SelfieTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _SelfieTex_ST;
                float4 _ObjectCenter;
                float4 _ObjectExtents;
                float _Opacity;
                float _PatchHalfAngle;
                float _VerticalHalfFraction;
                float _BrushEdgeWidth;
                float _BrushEdgeRoughness;
                float _BrushEdgeFeather;
                float _BrushCornerRoundness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionOS = input.positionOS.xyz;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float BrushHash(float value)
            {
                return frac(sin(value * 127.1 + 19.19) * 43758.5453);
            }

            float BrushEdgeNoise(float coordinate, float seed)
            {
                // Blend two different bristle sizes. The lower frequency makes
                // the silhouette uneven, while the higher frequency adds the
                // small serrations left by individual brush hairs.
                float broadCell = floor(coordinate * 31.0);
                float fineCell = floor(coordinate * 89.0);
                float broad = BrushHash(broadCell + seed);
                float fine = BrushHash(fineCell + seed * 3.17);
                return broad * 0.65 + fine * 0.35;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                const float HalfPi = 1.57079632679;
                float2 radial = input.positionOS.xy - _ObjectCenter.xy;
                float angle = atan2(radial.y, radial.x);

                // Repeat the same image once in each 90-degree quadrant. The
                // unused part of each quadrant is transparent, leaving a gap
                // between the front, back, left and right copies.
                float wrappedAngle = frac((angle + HalfPi * 0.5) / HalfPi) * HalfPi - HalfPi * 0.5;
                clip(_PatchHalfAngle - abs(wrappedAngle));

                // The imported lantern uses local Z as its vertical axis.
                // Sampling directly from the captured texture keeps the same
                // upright, non-mirrored orientation as the Canvas preview.
                float vertical = (input.positionOS.z - _ObjectCenter.z) /
                    max(_ObjectExtents.z, 0.00001);
                clip(_VerticalHalfFraction - abs(vertical));

                float2 uv = float2(
                    wrappedAngle / (2.0 * _PatchHalfAngle) + 0.5,
                    vertical / (2.0 * _VerticalHalfFraction) + 0.5);

                // Shape the photograph as one broad horizontal paint stroke.
                // The middle stays dense, while the stroke narrows strongly
                // toward all four corners. Per-row noise makes the left/right
                // ends look like separate brush hairs of uneven length.
                float verticalFromCentre = abs(uv.y - 0.5) * 2.0;
                float horizontalFromCentre = abs(uv.x - 0.5) * 2.0;
                float horizontalCornerCut =
                    pow(verticalFromCentre, 2.15) * _BrushCornerRoundness;
                float verticalCornerCut =
                    pow(horizontalFromCentre, 2.4) *
                    _BrushCornerRoundness * 0.62;

                float leftNoise = BrushEdgeNoise(uv.y, 1.0) - 0.42;
                float rightNoise = BrushEdgeNoise(uv.y, 7.0) - 0.42;
                float bottomNoise = BrushEdgeNoise(uv.x, 13.0) - 0.46;
                float topNoise = BrushEdgeNoise(uv.x, 23.0) - 0.46;

                float leftCut = max(
                    0.002,
                    _BrushEdgeWidth + horizontalCornerCut +
                    leftNoise * _BrushEdgeRoughness * 1.35);
                float rightCut = max(
                    0.002,
                    _BrushEdgeWidth + horizontalCornerCut +
                    rightNoise * _BrushEdgeRoughness * 1.35);
                float bottomCut = max(
                    0.002,
                    _BrushEdgeWidth + verticalCornerCut +
                    bottomNoise * _BrushEdgeRoughness * 0.85);
                float topCut = max(
                    0.002,
                    _BrushEdgeWidth + verticalCornerCut +
                    topNoise * _BrushEdgeRoughness * 0.85);

                float brushDistance = min(
                    min(uv.x - leftCut, (1.0 - uv.x) - rightCut),
                    min(uv.y - bottomCut, (1.0 - uv.y) - topCut));
                float brushMask = smoothstep(
                    -_BrushEdgeFeather, _BrushEdgeFeather, brushDistance);

                half4 color = SAMPLE_TEXTURE2D(_SelfieTex, sampler_SelfieTex, uv);
                color.a *= _Opacity * brushMask;
                clip(color.a - 0.001);
                return color;
            }
            ENDHLSL
        }
    }
}
