Shader "Morphic/Lantern Body Design Overlay"
{
    Properties
    {
        _DesignTex ("Design", 2D) = "white" {}
        [MainColor] _DesignColor ("Design Color", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _RepeatCount ("Repeat Count", Range(3, 12)) = 6
        _PatchHalfAngle ("Patch Half Angle", Range(0.05, 0.6)) = 0.22
        _VerticalHalfFraction ("Vertical Half Fraction", Range(0.05, 0.8)) = 0.38
        _ObjectCenter ("Object Center", Vector) = (0, 0, 0, 0)
        _ObjectExtents ("Object Extents", Vector) = (1, 1, 1, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+15"
            "RenderType" = "Transparent"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "BodyDesignOverlay"
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_DesignTex);
            SAMPLER(sampler_DesignTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _DesignTex_ST;
                float4 _DesignColor;
                float _Opacity;
                float _RepeatCount;
                float _PatchHalfAngle;
                float _VerticalHalfFraction;
                float4 _ObjectCenter;
                float4 _ObjectExtents;
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

            half4 Frag(Varyings input) : SV_Target
            {
                float2 radial = input.positionOS.xy - _ObjectCenter.xy;
                float angle = atan2(radial.y, radial.x);
                float repeats = max(_RepeatCount, 1.0);
                float sector = 6.28318530718 / repeats;
                float wrappedAngle = frac(angle / sector + 0.5) * sector - sector * 0.5;
                clip(_PatchHalfAngle - abs(wrappedAngle));

                float vertical = (input.positionOS.z - _ObjectCenter.z) /
                    max(_ObjectExtents.z, 0.00001);
                clip(_VerticalHalfFraction - abs(vertical));

                float2 uv = float2(
                    wrappedAngle / (2.0 * _PatchHalfAngle) + 0.5,
                    vertical / (2.0 * _VerticalHalfFraction) + 0.5);

                half4 tex = SAMPLE_TEXTURE2D(_DesignTex, sampler_DesignTex, uv);
                half4 color = half4(tex.rgb, tex.a * _Opacity);
                clip(color.a - 0.001);
                return color;
            }
            ENDHLSL
        }
    }
}
