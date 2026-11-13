Shader "Morphic/LisaChromaHidden"
{
    // Duplicate name avoided; runtime loads Resources/MorphicLisaChroma.shader.
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 vert(float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            fixed4 frag(float4 i : SV_POSITION) : SV_Target { return 0; }
            ENDCG
        }
    }
}
