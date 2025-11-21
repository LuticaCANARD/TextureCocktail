Shader "Hidden/FeatureExtractor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        
        // Feature extraction parameters
        _EdgeSensitivity ("Edge Sensitivity", Range(0, 1)) = 0.5
        _ColorThreshold ("Color Threshold", Range(0, 1)) = 0.1
        
        // Analysis mode
        _AnalysisChannel ("Analysis Channel (0=RGB, 1=R, 2=G, 3=B, 4=Luminance)", Int) = 0
        
        // Feature visualization
        _FeatureIntensity ("Feature Intensity", Range(0, 2)) = 1
        _FeatureColor ("Feature Color", Color) = (1, 0, 0, 1)
    }
    
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        
        // Pass 0: Edge Detection (Sobel)
        Pass
        {
            Name "EdgeDetection"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragEdge
            
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
            float _EdgeSensitivity;
            fixed4 _FeatureColor;
            float _FeatureIntensity;
            
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
            
            fixed4 fragEdge (v2f i) : SV_Target
            {
                float2 texelSize = _MainTex_TexelSize.xy;
                
                // Sobel operator samples
                float tl = luminance(tex2D(_MainTex, i.uv + float2(-texelSize.x, texelSize.y)).rgb);
                float t  = luminance(tex2D(_MainTex, i.uv + float2(0, texelSize.y)).rgb);
                float tr = luminance(tex2D(_MainTex, i.uv + float2(texelSize.x, texelSize.y)).rgb);
                float l  = luminance(tex2D(_MainTex, i.uv + float2(-texelSize.x, 0)).rgb);
                float r  = luminance(tex2D(_MainTex, i.uv + float2(texelSize.x, 0)).rgb);
                float bl = luminance(tex2D(_MainTex, i.uv + float2(-texelSize.x, -texelSize.y)).rgb);
                float b  = luminance(tex2D(_MainTex, i.uv + float2(0, -texelSize.y)).rgb);
                float br = luminance(tex2D(_MainTex, i.uv + float2(texelSize.x, -texelSize.y)).rgb);
                
                // Sobel kernels
                float gx = -tl - 2.0 * l - bl + tr + 2.0 * r + br;
                float gy = -tl - 2.0 * t - tr + bl + 2.0 * b + br;
                
                float edge = sqrt(gx * gx + gy * gy);
                edge = saturate(edge * (1.0 - _EdgeSensitivity) * 5.0);
                
                fixed4 col = tex2D(_MainTex, i.uv);
                return lerp(col, _FeatureColor * _FeatureIntensity, edge);
            }
            ENDCG
        }
        
        // Pass 1: Canny Edge Detection (more advanced)
        Pass
        {
            Name "CannyEdge"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragCanny
            
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
            float _EdgeSensitivity;
            fixed4 _FeatureColor;
            
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
            
            fixed4 fragCanny (v2f i) : SV_Target
            {
                float2 texelSize = _MainTex_TexelSize.xy;
                
                // Gaussian blur first
                float blurred = 0.0;
                float weights[9] = {1, 2, 1, 2, 4, 2, 1, 2, 1};
                int index = 0;
                
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y) * texelSize;
                        blurred += luminance(tex2D(_MainTex, i.uv + offset).rgb) * weights[index];
                        index++;
                    }
                }
                blurred /= 16.0;
                
                // Compute gradients
                float tl = luminance(tex2D(_MainTex, i.uv + float2(-texelSize.x, texelSize.y)).rgb);
                float tr = luminance(tex2D(_MainTex, i.uv + float2(texelSize.x, texelSize.y)).rgb);
                float bl = luminance(tex2D(_MainTex, i.uv + float2(-texelSize.x, -texelSize.y)).rgb);
                float br = luminance(tex2D(_MainTex, i.uv + float2(texelSize.x, -texelSize.y)).rgb);
                
                float gx = (tr + br) - (tl + bl);
                float gy = (tl + tr) - (bl + br);
                
                float magnitude = sqrt(gx * gx + gy * gy);
                float threshold = 1.0 - _EdgeSensitivity;
                
                if (magnitude > threshold)
                    return _FeatureColor;
                else
                    return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
        
        // Pass 2: Color Segmentation
        Pass
        {
            Name "ColorSegmentation"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragColorSeg
            
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
            float _ColorThreshold;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            fixed4 fragColorSeg (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Quantize colors - _ColorThreshold is 0-1, so we need to properly map it
                // When threshold is 0, use max levels (256), when 1, use min levels (2)
                float levels = lerp(2.0, 64.0, 1.0 - _ColorThreshold);
                levels = max(levels, 2.0);
                
                col.rgb = floor(col.rgb * levels) / levels;
                
                // Enhance contrast to make quantization more visible
                col.rgb = pow(col.rgb, 0.9);
                
                return col;
            }
            ENDCG
        }
        
        // Pass 3: Histogram Equalization (Enhanced)
        Pass
        {
            Name "HistogramEnhance"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragHistEq
            
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
            int _AnalysisChannel;
            
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
            
            fixed4 fragHistEq (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Simple contrast enhancement
                float value;
                if (_AnalysisChannel == 0) // RGB
                    value = luminance(col.rgb);
                else if (_AnalysisChannel == 1) // R
                    value = col.r;
                else if (_AnalysisChannel == 2) // G
                    value = col.g;
                else if (_AnalysisChannel == 3) // B
                    value = col.b;
                else // Luminance
                    value = luminance(col.rgb);
                
                // Apply simple histogram stretching
                float enhanced = saturate((value - 0.2) / 0.6);
                
                if (_AnalysisChannel == 0 || _AnalysisChannel == 4)
                {
                    col.rgb = lerp(col.rgb, float3(enhanced, enhanced, enhanced), 0.5);
                }
                else if (_AnalysisChannel == 1)
                {
                    col.r = enhanced;
                }
                else if (_AnalysisChannel == 2)
                {
                    col.g = enhanced;
                }
                else if (_AnalysisChannel == 3)
                {
                    col.b = enhanced;
                }
                
                return col;
            }
            ENDCG
        }
    }
}
