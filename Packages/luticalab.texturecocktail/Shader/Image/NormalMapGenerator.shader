Shader "Luticalab/NormalMapGenerator"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Strength ("Normal Strength", Range(0, 10)) = 1
        _HeightScale ("Height Scale", Range(0, 1)) = 0.1
    }
    SubShader
    {
        // No culling or depth
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

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Strength;
            float _HeightScale;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float luminance(fixed3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample height from luminance
                float center = luminance(tex2D(_MainTex, i.uv).rgb);
                float left   = luminance(tex2D(_MainTex, i.uv + float2(-_MainTex_TexelSize.x, 0)).rgb);
                float right  = luminance(tex2D(_MainTex, i.uv + float2(_MainTex_TexelSize.x, 0)).rgb);
                float top    = luminance(tex2D(_MainTex, i.uv + float2(0, _MainTex_TexelSize.y)).rgb);
                float bottom = luminance(tex2D(_MainTex, i.uv + float2(0, -_MainTex_TexelSize.y)).rgb);
                
                // Calculate gradients (Sobel operator)
                float dx = (right - left) * _Strength * _HeightScale;
                float dy = (bottom - top) * _Strength * _HeightScale;
                
                // Build normal vector
                float3 normal = normalize(float3(-dx, -dy, 1.0));
                
                // Convert from -1..1 to 0..1 range for texture storage
                normal = normal * 0.5 + 0.5;
                
                return fixed4(normal, 1.0);
            }
            ENDCG
        }
    }
}
