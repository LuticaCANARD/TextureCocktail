Shader "Hidden/TextureCocktail/MeshHighlight"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _MaskTex ("Mask (UV)", 2D) = "black" {}
        _HighlightColor ("Highlight Color", Color) = (0.2, 1.0, 0.4, 1.0)
        _HighlightIntensity ("Highlight Intensity", Range(0, 4)) = 1.5
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.4
        _BaseDim ("Base Dim Outside Mask", Range(0, 1)) = 0.0
        _AmbientLevel ("Ambient Level", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            fixed4 _HighlightColor;
            float _HighlightIntensity;
            float _PulseSpeed;
            float _PulseStrength;
            float _BaseDim;
            float _AmbientLevel;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 base = tex2D(_MainTex, i.uv);
                float mask = tex2D(_MaskTex, i.uv).a;

                float3 lightDir = normalize(float3(0.4, 0.8, -0.5));
                float ndl = saturate(dot(normalize(i.worldNormal), lightDir));
                float lightFactor = ndl * (1.0 - _AmbientLevel) + _AmbientLevel;
                fixed3 lit = base.rgb * lightFactor;

                float dimFactor = lerp(1.0, lerp(1.0, 0.25, _BaseDim), 1.0 - mask);
                lit *= dimFactor;

                float pulse = 1.0 + _PulseStrength * sin(_Time.y * _PulseSpeed);
                fixed3 highlight = _HighlightColor.rgb * _HighlightIntensity * pulse * mask;

                return fixed4(lit + highlight, 1.0);
            }
            ENDCG
        }
    }
}
