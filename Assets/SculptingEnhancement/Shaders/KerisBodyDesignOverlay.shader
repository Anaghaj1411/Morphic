Shader "Morphic/Keris Body Design Overlay"
{
    Properties
    {
        _DesignTex ("Design", 2D) = "white" {}
        [MainColor] _DesignColor ("Design Color", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _RepeatCount ("Repeat Count", Range(1, 12)) = 2
        _PatchHalfAngle ("Patch Half Angle", Range(0.05, 1.2)) = 0.52
        _VerticalHalfFraction ("Vertical Half Fraction", Range(0.05, 0.95)) = 0.62
        _ObjectCenter ("Object Center", Vector) = (0, 0, 0, 0)
        _ObjectExtents ("Object Extents", Vector) = (1, 1, 1, 0)
        _UseMeshUV ("Use Mesh UV", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "KerisBodyDesignOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            Offset -2, -2

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
                float4 _ObjectCenter;
                float4 _ObjectExtents;
                float _Opacity;
                float _RepeatCount;
                float _PatchHalfAngle;
                float _VerticalHalfFraction;
                float _UseMeshUV;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.uv = input.uv;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_DesignTex, sampler_DesignTex, input.uv);
                half4 color = half4(tex.rgb, tex.a * _Opacity);
                clip(color.a - 0.02);
                return color;
            }
            ENDHLSL
        }
    }
}
