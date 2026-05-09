Shader "Hidden/TextureCocktail/MeshHighlightOverlay"
{
    Properties
    {
        _MaskTex ("Mask (UV)", 2D) = "black" {}
        _HighlightColor ("Highlight Color", Color) = (0.2, 1.0, 0.4, 1.0)
        _HighlightIntensity ("Highlight Intensity", Range(0, 4)) = 1.5
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" "IgnoreProjector"="True" }

        Pass
        {
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One
            Offset -1, -1

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
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MaskTex;
            fixed4 _HighlightColor;
            float _HighlightIntensity;
            float _PulseSpeed;
            float _PulseStrength;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float mask = tex2D(_MaskTex, i.uv).a;
                if (mask < 0.001)
                    discard;
                float pulse = 1.0 + _PulseStrength * sin(_Time.y * _PulseSpeed);
                fixed3 col = _HighlightColor.rgb * _HighlightIntensity * pulse;
                return fixed4(col * mask, mask * _HighlightColor.a);
            }
            ENDCG
        }
    }
}
