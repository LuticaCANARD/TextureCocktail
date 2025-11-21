Shader "Hidden/FastImageConverter"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        
        // Brightness/Contrast
        _Brightness ("Brightness", Range(-1, 1)) = 0
        _Contrast ("Contrast", Range(0, 3)) = 1
        
        // Color Adjustment
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Gamma ("Gamma", Range(0.1, 3)) = 1
        
        // Filters
        _BlurAmount ("Blur Amount", Range(0, 1)) = 0
        _SharpAmount ("Sharpen Amount", Range(0, 2)) = 0
        
        // Edge Detection
        _EdgeThreshold ("Edge Threshold", Range(0, 1)) = 0.1
        _EdgeColor ("Edge Color", Color) = (0, 0, 0, 1)
        
        // Advanced
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0
    }
    
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ EDGE_DETECTION BLUR_FILTER SHARPEN_FILTER
            
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
            
            float _Brightness;
            float _Contrast;
            float _Saturation;
            float _Gamma;
            float _BlurAmount;
            float _SharpAmount;
            float _EdgeThreshold;
            fixed4 _EdgeColor;
            float _NoiseAmount;
            float _VignetteIntensity;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            // Simple random function
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }
            
            // RGB to Luminance
            float luminance(fixed3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }
            
            // Apply saturation
            fixed3 applySaturation(fixed3 color, float saturation)
            {
                float lum = luminance(color);
                return lerp(float3(lum, lum, lum), color, saturation);
            }
            
            // Sobel edge detection
            float sobelEdge(float2 uv)
            {
                float2 texelSize = _MainTex_TexelSize.xy;
                
                // Sample surrounding pixels
                float tl = luminance(tex2D(_MainTex, uv + float2(-texelSize.x, texelSize.y)).rgb);
                float t  = luminance(tex2D(_MainTex, uv + float2(0, texelSize.y)).rgb);
                float tr = luminance(tex2D(_MainTex, uv + float2(texelSize.x, texelSize.y)).rgb);
                float l  = luminance(tex2D(_MainTex, uv + float2(-texelSize.x, 0)).rgb);
                float r  = luminance(tex2D(_MainTex, uv + float2(texelSize.x, 0)).rgb);
                float bl = luminance(tex2D(_MainTex, uv + float2(-texelSize.x, -texelSize.y)).rgb);
                float b  = luminance(tex2D(_MainTex, uv + float2(0, -texelSize.y)).rgb);
                float br = luminance(tex2D(_MainTex, uv + float2(texelSize.x, -texelSize.y)).rgb);
                
                // Sobel operator
                float gx = -tl - 2.0 * l - bl + tr + 2.0 * r + br;
                float gy = -tl - 2.0 * t - tr + bl + 2.0 * b + br;
                
                return sqrt(gx * gx + gy * gy);
            }
            
            // Box blur
            fixed4 boxBlur(float2 uv, float amount)
            {
                float2 texelSize = _MainTex_TexelSize.xy * amount;
                fixed4 result = fixed4(0, 0, 0, 0);
                
                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        result += tex2D(_MainTex, uv + float2(x, y) * texelSize);
                    }
                }
                
                return result / 25.0;
            }
            
            // Sharpen filter
            fixed4 sharpen(float2 uv, float amount)
            {
                float2 texelSize = _MainTex_TexelSize.xy;
                fixed4 center = tex2D(_MainTex, uv);
                
                fixed4 top    = tex2D(_MainTex, uv + float2(0, texelSize.y));
                fixed4 bottom = tex2D(_MainTex, uv + float2(0, -texelSize.y));
                fixed4 left   = tex2D(_MainTex, uv + float2(-texelSize.x, 0));
                fixed4 right  = tex2D(_MainTex, uv + float2(texelSize.x, 0));
                
                fixed4 edges = (top + bottom + left + right) * 0.25;
                fixed4 sharpened = center + (center - edges) * amount;
                
                return sharpened;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                #ifdef EDGE_DETECTION
                    float edge = sobelEdge(i.uv);
                    if (edge > _EdgeThreshold)
                    {
                        col = _EdgeColor;
                    }
                    return col;
                #endif
                
                #ifdef BLUR_FILTER
                    col = boxBlur(i.uv, _BlurAmount * 2.0);
                #endif
                
                #ifdef SHARPEN_FILTER
                    col = sharpen(i.uv, _SharpAmount);
                #endif
                
                // Apply brightness and contrast
                col.rgb = ((col.rgb - 0.5) * _Contrast + 0.5) + _Brightness;
                
                // Apply saturation
                col.rgb = applySaturation(col.rgb, _Saturation);
                
                // Apply gamma
                col.rgb = pow(abs(col.rgb), _Gamma);
                
                // Add noise
                if (_NoiseAmount > 0)
                {
                    float noise = rand(i.uv * _Time.y) * _NoiseAmount;
                    col.rgb += noise;
                }
                
                // Apply vignette
                if (_VignetteIntensity > 0)
                {
                    float2 center = i.uv - 0.5;
                    float vignette = 1.0 - length(center) * _VignetteIntensity * 2.0;
                    col.rgb *= saturate(vignette);
                }
                
                return col;
            }
            ENDCG
        }
    }
}
