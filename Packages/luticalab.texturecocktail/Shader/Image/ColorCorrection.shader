Shader "Hidden/ColorCorrection"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        
        // Color Grading
        _Temperature ("Temperature", Range(-1, 1)) = 0
        _Tint ("Tint", Range(-1, 1)) = 0
        
        // Exposure and Tone
        _Exposure ("Exposure", Range(-3, 3)) = 0
        _Lift ("Lift", Color) = (1, 1, 1, 0)
        _Gamma ("Gamma", Color) = (1, 1, 1, 0)
        _Gain ("Gain", Color) = (1, 1, 1, 0)
        
        // Color Balance
        _ShadowsColor ("Shadows", Color) = (0.5, 0.5, 0.5, 1)
        _MidtonesColor ("Midtones", Color) = (0.5, 0.5, 0.5, 1)
        _HighlightsColor ("Highlights", Color) = (0.5, 0.5, 0.5, 1)
        
        // Split Toning
        _ShadowsTone ("Shadows Tone", Color) = (0.5, 0.5, 0.5, 1)
        _HighlightsTone ("Highlights Tone", Color) = (0.5, 0.5, 0.5, 1)
        _Balance ("Balance", Range(-1, 1)) = 0
        
        // Advanced
        _Hue ("Hue Shift", Range(-180, 180)) = 0
        _Vibrance ("Vibrance", Range(-1, 1)) = 0
    }
    
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ COLOR_GRADING SPLIT_TONING
            
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
            
            float _Temperature;
            float _Tint;
            float _Exposure;
            fixed4 _Lift;
            fixed4 _Gamma;
            fixed4 _Gain;
            fixed4 _ShadowsColor;
            fixed4 _MidtonesColor;
            fixed4 _HighlightsColor;
            fixed4 _ShadowsTone;
            fixed4 _HighlightsTone;
            float _Balance;
            float _Hue;
            float _Vibrance;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            // RGB to HSV
            float3 rgb2hsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }
            
            // HSV to RGB
            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }
            
            // Luminance
            float luminance(float3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }
            
            // Temperature and Tint
            float3 whiteBalance(float3 color, float temperature, float tint)
            {
                // Temperature shift
                float t = temperature * 0.1;
                color.r *= 1.0 + t;
                color.b *= 1.0 - t;
                
                // Tint shift
                float ti = tint * 0.1;
                color.g *= 1.0 + ti;
                
                return saturate(color);
            }
            
            // Lift, Gamma, Gain
            float3 applyLGG(float3 color, float3 lift, float3 gamma, float3 gain)
            {
                // Lift (shadows)
                color = color + (lift - 0.5) * 0.5;
                
                // Gamma (midtones)
                color = pow(abs(color), 1.0 / max(gamma, 0.01));
                
                // Gain (highlights)
                color = color * (gain * 2.0);
                
                return saturate(color);
            }
            
            // Color balance
            float3 colorBalance(float3 color, float3 shadows, float3 midtones, float3 highlights)
            {
                float lum = luminance(color);
                
                // Calculate weights based on luminance
                float shadowWeight = 1.0 - smoothstep(0.0, 0.3, lum);
                float highlightWeight = smoothstep(0.7, 1.0, lum);
                float midtoneWeight = 1.0 - shadowWeight - highlightWeight;
                
                // Apply color shifts
                color += (shadows - 0.5) * shadowWeight * 0.5;
                color += (midtones - 0.5) * midtoneWeight * 0.5;
                color += (highlights - 0.5) * highlightWeight * 0.5;
                
                return saturate(color);
            }
            
            // Split toning
            float3 splitToning(float3 color, float3 shadowTone, float3 highlightTone, float balance)
            {
                float lum = luminance(color);
                float threshold = 0.5 + balance * 0.5;
                
                float3 toneColor = lerp(shadowTone, highlightTone, smoothstep(threshold - 0.2, threshold + 0.2, lum));
                return lerp(color, color * toneColor * 2.0, 0.5);
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Exposure
                col.rgb *= exp2(_Exposure);
                
                // White balance
                col.rgb = whiteBalance(col.rgb, _Temperature, _Tint);
                
                #ifdef COLOR_GRADING
                    // Lift Gamma Gain
                    col.rgb = applyLGG(col.rgb, _Lift.rgb, _Gamma.rgb, _Gain.rgb);
                    
                    // Color balance
                    col.rgb = colorBalance(col.rgb, _ShadowsColor.rgb, _MidtonesColor.rgb, _HighlightsColor.rgb);
                #endif
                
                #ifdef SPLIT_TONING
                    // Split toning
                    col.rgb = splitToning(col.rgb, _ShadowsTone.rgb, _HighlightsTone.rgb, _Balance);
                #endif
                
                // Hue shift
                if (abs(_Hue) > 0.01)
                {
                    float3 hsv = rgb2hsv(col.rgb);
                    hsv.x = frac(hsv.x + _Hue / 360.0);
                    col.rgb = hsv2rgb(hsv);
                }
                
                // Vibrance
                if (abs(_Vibrance) > 0.01)
                {
                    float lum = luminance(col.rgb);
                    float sat = max(max(col.r, col.g), col.b) - min(min(col.r, col.g), col.b);
                    float boost = (1.0 - sat) * _Vibrance;
                    col.rgb = lerp(float3(lum, lum, lum), col.rgb, 1.0 + boost);
                }
                
                return col;
            }
            ENDCG
        }
    }
}
