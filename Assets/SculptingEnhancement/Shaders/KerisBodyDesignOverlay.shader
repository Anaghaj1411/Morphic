Shader "Morphic/Keris Body Design Overlay"
{
    Properties
    {
        _DesignTex ("Design", 2D) = "white" {}
        _DesignColor ("Design Color", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _RepeatCount ("Repeat Count", Range(1, 12)) = 2
        _PatchHalfAngle ("Patch Half Angle", Range(0.05, 1.2)) = 0.52
        _VerticalHalfFraction ("Vertical Half Fraction", Range(0.05, 0.95)) = 0.62
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
        }

        Pass
        {
            Name "KerisBodyDesignOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

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
                float3 pos = input.positionOS - _ObjectCenter.xyz;
                float3 ext = max(_ObjectExtents.xyz, float3(0.00001, 0.00001, 0.00001));

                float2 radial;
                float vertical;
                if (ext.z >= ext.x && ext.z >= ext.y)
                {
                    radial = pos.xy;
                    vertical = pos.z / ext.z;
                }
                else if (ext.y >= ext.x)
                {
                    radial = pos.xz;
                    vertical = pos.y / ext.y;
                }
                else
                {
                    radial = pos.yz;
                    vertical = pos.x / ext.x;
                }

                float angle = atan2(radial.y, radial.x);
                float repeats = max(_RepeatCount, 1.0);
                float sector = 6.28318530718 / repeats;
                float wrappedAngle = frac(angle / sector + 0.5) * sector - sector * 0.5;
                clip(_PatchHalfAngle - abs(wrappedAngle));
                clip(_VerticalHalfFraction - abs(vertical));

                float2 uv = float2(
                    wrappedAngle / (2.0 * _PatchHalfAngle) + 0.5,
                    vertical / (2.0 * _VerticalHalfFraction) + 0.5);

                half4 tex = SAMPLE_TEXTURE2D(_DesignTex, sampler_DesignTex, uv);
                half luma = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                half cutout = tex.a < 0.98 ? 1.0 : saturate((0.68 - luma) * 5.0);
                half3 rgb = tex.a < 0.98 ? tex.rgb * _DesignColor.rgb : _DesignColor.rgb;
                half4 color = half4(rgb, tex.a * cutout * _DesignColor.a * _Opacity);
                clip(color.a - 0.001);
                return color;
            }
            ENDHLSL
        }
    }
}
