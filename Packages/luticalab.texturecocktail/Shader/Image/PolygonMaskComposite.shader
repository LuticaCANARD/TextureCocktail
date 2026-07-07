Shader "Hidden/TextureCocktail/PolygonMaskComposite"
{
    Properties
    {
        _OriginalTex ("Original Texture", 2D) = "white" {}
        _ProcessedTex ("Processed Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _OriginalTex;
            sampler2D _ProcessedTex;
            sampler2D _MaskTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 original = tex2D(_OriginalTex, i.uv);
                fixed4 processed = tex2D(_ProcessedTex, i.uv);
                float mask = tex2D(_MaskTex, i.uv).a;
                return lerp(original, processed, saturate(mask));
            }
            ENDCG
        }
    }
}
