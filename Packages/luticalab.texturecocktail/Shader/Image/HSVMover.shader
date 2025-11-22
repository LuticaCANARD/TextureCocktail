Shader "Luticalab/ImageFilter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _hsvConvertVector("HSV Convert Vector // X = H, Y = S, Z = V", Vector) = (0, 0, 0, 0)
        _hsvOffsetVector("HSV Offset Vector", Vector) = (0, 0, 0, 0)
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
            #include "Packages/luticalab.core/Shader/HLSL/CanarinUtilCore.hlsl"
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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            Vector _hsvConvertVector;
            Vector _hsvOffsetVector;
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                // Convert to HSV
                // RGB to HSV conversion function
                float3 hsv = rgb2hsv(col.rgb);
                // Extract conversion and offset values from the vectors
                float _hConvert = _hsvConvertVector.x;
                float _sConvert = _hsvConvertVector.y;
                float _vConvert = _hsvConvertVector.z;
                float _hOffset = _hsvOffsetVector.x;
                float _sOffset = _hsvOffsetVector.y;
                float _vOffset = _hsvOffsetVector.z;
                // Apply conversion and offset to HSV values

                hsv.x += _hConvert + _hOffset; // Hue
                hsv.y += _sConvert + _sOffset; // Saturation
                hsv.z += _vConvert + _vOffset; // Value
                // Clamp values to valid ranges
                hsv.x = fmod(hsv.x, 1.0); // Wrap hue
                hsv.y = clamp(hsv.y, 0.0, 1.0); // Clamp saturation
                hsv.z = clamp(hsv.z, 0.0, 1.0); // Clamp value
                // Convert back to RGB
                col.rgb = hsv2rgb(hsv);

                return col;
            }
            ENDCG
        }
    }
}
