Shader "Hidden/ArtisticEffects"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        
        // Pixelation
        _PixelSize ("Pixel Size", Range(1, 100)) = 1
        _PixelCenterX ("Pixel Center X", Range(0, 1)) = 0.5
        _PixelCenterY ("Pixel Center Y", Range(0, 1)) = 0.5
        
        // Posterize
        _ColorLevels ("Color Levels", Range(2, 256)) = 16
        
        // Halftone
        _DotSize ("Dot Size", Range(1, 20)) = 5
        _DotAngle ("Dot Angle", Range(0, 360)) = 45
        
        // Oil Painting
        _Radius ("Oil Paint Radius", Range(1, 10)) = 3
        
        // Emboss
        _EmbossStrength ("Emboss Strength", Range(0, 5)) = 1
        
        // Cartoon
        _EdgeThreshold ("Cartoon Edge Threshold", Range(0, 1)) = 0.1
        _ColorSteps ("Cartoon Color Steps", Range(2, 10)) = 4
    }
    
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ PIXELATE POSTERIZE HALFTONE OILPAINT EMBOSS CARTOON
            
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
            
            float _PixelSize;
            float _PixelCenterX;
            float _PixelCenterY;
            float _ColorLevels;
            float _DotSize;
            float _DotAngle;
            float _Radius;
            float _EmbossStrength;
            float _EdgeThreshold;
            float _ColorSteps;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            float luminance(float3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }
            
            // Pixelate effect with center point control
            fixed4 pixelate(float2 uv, float pixelSize, float2 center)
            {
                // Offset UV by center point
                float2 offsetUV = uv - center;
                
                // Use actual pixel size in texture space
                float2 pixelUV = floor(offsetUV * _MainTex_TexelSize.zw / max(pixelSize, 1.0)) * max(pixelSize, 1.0) / _MainTex_TexelSize.zw;
                
                // Add center back
                pixelUV += center;
                
                return tex2D(_MainTex, pixelUV);
            }
            
            // Posterize effect with stronger quantization
            fixed4 posterize(float2 uv, float levels)
            {
                fixed4 col = tex2D(_MainTex, uv);
                // Ensure levels is at least 2 to avoid division by zero
                float validLevels = max(levels, 2.0);
                
                // Apply stronger posterization by squaring the effect
                col.rgb = floor(col.rgb * validLevels) / validLevels;
                
                // Enhance contrast to make posterization more visible
                col.rgb = pow(col.rgb, 0.8);
                
                return col;
            }
            
            // Halftone effect
            fixed4 halftone(float2 uv, float dotSize, float angle)
            {
                fixed4 col = tex2D(_MainTex, uv);
                float gray = luminance(col.rgb);
                
                // Rotate UV
                float rad = radians(angle);
                float2 center = float2(0.5, 0.5);
                float2 rotUV = uv - center;
                float cosA = cos(rad);
                float sinA = sin(rad);
                float2 rotatedUV = float2(
                    rotUV.x * cosA - rotUV.y * sinA,
                    rotUV.x * sinA + rotUV.y * cosA
                ) + center;
                
                // Create dot pattern
                float2 grid = frac(rotatedUV * _MainTex_TexelSize.zw / dotSize);
                float dist = length(grid - 0.5);
                float threshold = gray * 0.5;
                
                float dot = step(dist, threshold);
                return fixed4(dot, dot, dot, col.a);
            }
            
            // Oil painting effect (simplified)
            fixed4 oilPaint(float2 uv, float radius)
            {
                float3 sum = float3(0, 0, 0);
                float weight = 0;
                
                int r = (int)radius;
                for (int y = -r; y <= r; y++)
                {
                    for (int x = -r; x <= r; x++)
                    {
                        float2 offset = float2(x, y) * _MainTex_TexelSize.xy;
                        fixed4 sample = tex2D(_MainTex, uv + offset);
                        float w = 1.0 - length(float2(x, y)) / radius;
                        sum += sample.rgb * w;
                        weight += w;
                    }
                }
                
                return fixed4(sum / weight, 1);
            }
            
            // Emboss effect
            fixed4 emboss(float2 uv, float strength)
            {
                float3 sum = float3(0, 0, 0);
                
                // Emboss kernel
                float kernel[9] = {
                    -2, -1, 0,
                    -1,  1, 1,
                     0,  1, 2
                };
                
                int index = 0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y) * _MainTex_TexelSize.xy;
                        fixed4 sample = tex2D(_MainTex, uv + offset);
                        sum += sample.rgb * kernel[index] * strength;
                        index++;
                    }
                }
                
                sum = sum * 0.5 + 0.5;
                return fixed4(sum, 1);
            }
            
            // Cartoon effect
            fixed4 cartoon(float2 uv, float edgeThreshold, float colorSteps)
            {
                fixed4 col = tex2D(_MainTex, uv);
                
                // Color quantization - ensure colorSteps is valid
                float validSteps = max(colorSteps, 2.0);
                col.rgb = floor(col.rgb * validSteps) / validSteps;
                
                // Edge detection
                float edge = 0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        if (x == 0 && y == 0) continue;
                        float2 offset = float2(x, y) * _MainTex_TexelSize.xy;
                        fixed4 sample = tex2D(_MainTex, uv + offset);
                        edge += length(col.rgb - sample.rgb);
                    }
                }
                edge /= 8.0;
                
                // Apply edge - darken edge pixels
                if (edge > edgeThreshold)
                {
                    col.rgb = lerp(col.rgb, float3(0, 0, 0), 0.8);
                }
                
                return col;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                #ifdef PIXELATE
                    col = pixelate(i.uv, _PixelSize, float2(_PixelCenterX, _PixelCenterY));
                #elif POSTERIZE
                    col = posterize(i.uv, _ColorLevels);
                #elif HALFTONE
                    col = halftone(i.uv, _DotSize, _DotAngle);
                #elif OILPAINT
                    col = oilPaint(i.uv, _Radius);
                #elif EMBOSS
                    col = emboss(i.uv, _EmbossStrength);
                #elif CARTOON
                    col = cartoon(i.uv, _EdgeThreshold, _ColorSteps);
                #endif
                
                return col;
            }
            ENDCG
        }
    }
}
