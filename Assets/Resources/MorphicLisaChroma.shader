Shader "Morphic/LisaChroma"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Threshold ("White Distance", Range(0, 1)) = 0.16
        _Smoothness ("Edge Softness", Range(0.001, 0.5)) = 0.14
        _Spill ("Spill Cut", Range(0, 1)) = 0.78
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "LisaChroma"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Threshold;
            float _Smoothness;
            float _Spill;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord) * i.color;
                float maxc = max(col.r, max(col.g, col.b));
                float minc = min(col.r, min(col.g, col.b));
                float sat = maxc - minc;
                float luma = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                float toWhite = distance(col.rgb, float3(1.0, 1.0, 1.0));

                float key = 1.0 - smoothstep(_Threshold, _Threshold + _Smoothness, toWhite);
                float pale = smoothstep(_Spill, 0.96, luma) * (1.0 - smoothstep(0.02, 0.13, sat));
                key = saturate(max(key, pale));

                col.a *= 1.0 - key;
                col.rgb = lerp(col.rgb, col.rgb * col.rgb, key * 0.55);
                clip(col.a - 0.008);
                return col;
            }
            ENDCG
        }
    }
}
