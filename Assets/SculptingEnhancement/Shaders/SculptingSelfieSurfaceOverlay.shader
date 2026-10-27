Shader "Morphic/Selfie Surface Overlay"
{
    Properties
    {
        _SelfieTex ("Selfie", 2D) = "white" {}
        _Opacity ("Opacity", Range(0, 1)) = 1
        _PatchHalfAngle ("Patch Half Angle", Range(0.05, 0.78)) = 0.74
        _VerticalHalfFraction ("Vertical Half Fraction", Range(0.05, 0.95)) = 0.88
        _ObjectCenter ("Object Center", Vector) = (0, 0, 0, 0)
        _ObjectExtents ("Object Extents", Vector) = (1, 1, 1, 0)
        _BrushEdgeWidth ("Brush Edge Width", Range(0.005, 0.12)) = 0.025
        _BrushEdgeRoughness ("Brush Edge Roughness", Range(0, 0.4)) = 0.085
        _BrushEdgeFeather ("Brush Edge Feather", Range(0.002, 0.05)) = 0.012
        _BrushCornerRoundness ("Brush Corner Roundness", Range(0, 0.8)) = 0.28
        _PhotoAspect ("Photo Aspect", Float) = 1
        _UseYUp ("Use Y Up", Float) = 1
        _SurfacePush ("Surface Push", Float) = 0.012
        _UpDirection ("Up Direction", Vector) = (0, 1, 0, 0)
        _StampMode ("Stamp Mode", Float) = 0
        _WorldCenter ("World Center", Vector) = (0, 0, 0, 0)
        _WorldExtents ("World Extents", Vector) = (1, 1, 1, 0)
        _FrontDirection ("Front Direction", Vector) = (0, 0, 1, 0)
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
            Offset -2, -2

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
                float _PhotoAspect;
                float _UseYUp;
                float _SurfacePush;
                float4 _UpDirection;
                float _StampMode;
                float4 _WorldCenter;
                float4 _WorldExtents;
                float4 _FrontDirection;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionOS = input.positionOS.xyz;
                float3 pushed = input.positionOS.xyz + normalize(input.normalOS) * _SurfacePush;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformObjectToHClip(pushed);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                const float HalfPi = 1.57079632679;
                float3 local = input.positionOS - _ObjectCenter.xyz;
                float3 up = _UpDirection.xyz;
                if (dot(up, up) < 0.0001)
                    up = _UseYUp > 0.5 ? float3(0, 1, 0) : float3(0, 0, 1);
                up = normalize(up);
                float2 uv;

                if (_StampMode > 2.5)
                {
                    // Teapot mesh is Z-up. Wrap around the belly (XY) like the
                    // body stamps, so the photo sits on the side, not the lid.
                    float2 frontXY = _FrontDirection.xy;
                    if (dot(frontXY, frontXY) < 0.0001)
                        frontXY = float2(0, 1);
                    frontXY = normalize(frontXY);
                    float2 rightXY = float2(-frontXY.y, frontXY.x);

                    float2 radial = local.xy;
                    float sideX = dot(radial, rightXY);
                    float sideZ = dot(radial, frontXY);
                    float angle = atan2(sideX, sideZ);
                    float verticalExtent = max(_ObjectExtents.z, 0.00001);
                    float vertical = local.z / verticalExtent;
                    const float Pi = HalfPi * 2.0;
                    float toFront = abs(angle);
                    float toBack = Pi - toFront;
                    float sideDist = min(toFront, toBack);
                    float upFacing = abs(local.z) / (length(local) + 0.00001);
                    clip(_PatchHalfAngle - sideDist);
                    clip(_VerticalHalfFraction - abs(vertical));
                    clip(0.55 - upFacing);
                    clip(dot(radial, radial) - 0.000001);
                    float localAngle = toFront < toBack
                        ? angle
                        : (angle > 0.0 ? angle - Pi : angle + Pi);
                    float around = localAngle / (2.0 * _PatchHalfAngle) + 0.5;
                    float height = vertical / (2.0 * _VerticalHalfFraction) + 0.5;
                    uv = float2(around, 1.0 - height);
                }
                else if (_StampMode > 1.5)
                {
                    // World-space wrap. Same math as the lantern, but around
                    // world Y so a rotated teapot still shows an upright face.
                    float3 world = input.positionWS - _WorldCenter.xyz;
                    float angle = atan2(world.x, world.z);
                    float verticalExtent = max(_WorldExtents.y, 0.00001);
                    float vertical = world.y / verticalExtent;
                    float wrappedAngle = frac((angle + HalfPi * 0.5) / HalfPi) * HalfPi - HalfPi * 0.5;
                    clip(_PatchHalfAngle - abs(wrappedAngle));
                    clip(_VerticalHalfFraction - abs(vertical));
                    uv = float2(
                        wrappedAngle / (2.0 * _PatchHalfAngle) + 0.5,
                        vertical / (2.0 * _VerticalHalfFraction) + 0.5);
                }
                else if (_StampMode > 0.5)
                {
                    float3 refForward = abs(up.y) > 0.7 ? float3(0, 0, 1) : float3(0, 1, 0);
                    float3 right = cross(up, refForward);
                    if (dot(right, right) < 0.0001)
                        right = cross(up, float3(1, 0, 0));
                    right = normalize(right);
                    float3 fwd = normalize(cross(right, up));

                    float sideX = dot(local, right);
                    float sideZ = dot(local, fwd);
                    float sideY = dot(local, up);
                    float angle = atan2(sideX, sideZ);
                    float sector = frac((angle + HalfPi * 0.5) / HalfPi) * HalfPi - HalfPi * 0.5;

                    float height = max(abs(dot(_ObjectExtents.xyz, up)), 0.00001);
                    float radius = 0.00001;
                    radius = max(radius, abs(dot(_ObjectExtents.xyz, right)));
                    radius = max(radius, abs(dot(_ObjectExtents.xyz, fwd)));
                    float halfH = height * _VerticalHalfFraction;
                    float halfW = min(halfH, radius * 0.55);

                    clip(halfW - abs(radius * sector));
                    clip(halfH - abs(sideY));

                    uv = float2(
                        (radius * sector) / (2.0 * halfW) + 0.5,
                        sideY / (2.0 * halfH) + 0.5);
                }
                else
                {
                    float angle;
                    float vertical;
                    float verticalExtent;
                    float radius;
                    if (up.y > 0.85)
                    {
                        angle = atan2(local.x, local.z);
                        verticalExtent = max(_ObjectExtents.y, 0.00001);
                        vertical = local.y / verticalExtent;
                        radius = max(max(_ObjectExtents.x, _ObjectExtents.z), 0.00001);
                    }
                    else if (up.y < -0.85)
                    {
                        angle = atan2(local.x, local.z);
                        verticalExtent = max(_ObjectExtents.y, 0.00001);
                        vertical = -local.y / verticalExtent;
                        radius = max(max(_ObjectExtents.x, _ObjectExtents.z), 0.00001);
                    }
                    else if (abs(up.z) > 0.85)
                    {
                        angle = atan2(local.x, local.y);
                        verticalExtent = max(_ObjectExtents.z, 0.00001);
                        vertical = (up.z > 0.0 ? local.z : -local.z) / verticalExtent;
                        radius = max(max(_ObjectExtents.x, _ObjectExtents.y), 0.00001);
                    }
                    else
                    {
                        float3 radial3 = local - up * dot(local, up);
                        float3 refAxis = abs(up.x) < 0.9 ? float3(1, 0, 0) : float3(0, 0, 1);
                        float3 tangent = cross(up, refAxis);
                        if (dot(tangent, tangent) < 0.0001)
                            tangent = cross(up, float3(0, 1, 0));
                        tangent = normalize(tangent);
                        float3 bitangent = normalize(cross(up, tangent));
                        angle = atan2(dot(radial3, tangent), dot(radial3, bitangent));
                        verticalExtent = max(abs(dot(_ObjectExtents.xyz, up)), 0.00001);
                        vertical = dot(local, up) / verticalExtent;
                        float3 absUp = abs(up);
                        radius = max(dot(_ObjectExtents.xyz, float3(1, 1, 1) - absUp), 0.00001);
                    }

                    float wrappedAngle = frac((angle + HalfPi * 0.5) / HalfPi) * HalfPi - HalfPi * 0.5;
                    clip(_PatchHalfAngle - abs(wrappedAngle));
                    clip(_VerticalHalfFraction - abs(vertical));

                    uv = float2(
                        wrappedAngle / (2.0 * _PatchHalfAngle) + 0.5,
                        vertical / (2.0 * _VerticalHalfFraction) + 0.5);
                }

                float2 centered = uv * 2.0 - 1.0;
                float edge = 1.0 - max(abs(centered.x), abs(centered.y));
                float brushMask = smoothstep(0.0, 0.16, edge);

                half4 color = SAMPLE_TEXTURE2D(_SelfieTex, sampler_SelfieTex, uv);
                float3 shadeNormal = normalize(local);
                float shade = saturate(dot(shadeNormal, normalize(float3(0.22, 0.62, 0.75))) * 0.38 + 0.68);
                color.rgb *= shade;
                color.a *= _Opacity * brushMask;
                clip(color.a - 0.001);
                return color;
            }
            ENDHLSL
        }
    }
}
