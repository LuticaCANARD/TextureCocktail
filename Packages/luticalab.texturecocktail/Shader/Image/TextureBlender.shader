Shader "Hidden/TextureBlender"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _BlendTex ("Blend Texture", 2D) = "white" {}
        _BlendAmount ("Blend Amount", Range(0, 1)) = 0.5
        
        // Blend modes
        [KeywordEnum(Normal, Multiply, Screen, Overlay, SoftLight, HardLight, ColorDodge, ColorBurn, Darken, Lighten, Difference, Exclusion)] 
        _BlendMode ("Blend Mode", Float) = 0
        
        // UV Controls
        _BlendTexScale ("Blend Texture Scale", Vector) = (1, 1, 0, 0)
        _BlendTexOffset ("Blend Texture Offset", Vector) = (0, 0, 0, 0)
        _BlendTexRotation ("Blend Texture Rotation", Range(0, 360)) = 0
        
        // Mask
        _UseMask ("Use Mask", Float) = 0
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _MaskChannel ("Mask Channel (0=R, 1=G, 2=B, 3=A)", Int) = 0
    }
    
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _BLENDMODE_NORMAL _BLENDMODE_MULTIPLY _BLENDMODE_SCREEN _BLENDMODE_OVERLAY _BLENDMODE_SOFTLIGHT _BLENDMODE_HARDLIGHT _BLENDMODE_COLORDODGE _BLENDMODE_COLORBURN _BLENDMODE_DARKEN _BLENDMODE_LIGHTEN _BLENDMODE_DIFFERENCE _BLENDMODE_EXCLUSION
            
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
            sampler2D _BlendTex;
            sampler2D _MaskTex;
            float _BlendAmount;
            float2 _BlendTexScale;
            float2 _BlendTexOffset;
            float _BlendTexRotation;
            float _UseMask;
            int _MaskChannel;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            // Rotation matrix
            float2 rotateUV(float2 uv, float angle)
            {
                float rad = radians(angle);
                float cosAngle = cos(rad);
                float sinAngle = sin(rad);
                
                // Center pivot
                uv -= 0.5;
                float2 rotated;
                rotated.x = uv.x * cosAngle - uv.y * sinAngle;
                rotated.y = uv.x * sinAngle + uv.y * cosAngle;
                rotated += 0.5;
                
                return rotated;
            }
            
            // Blend modes
            float3 blendNormal(float3 base, float3 blend)
            {
                return blend;
            }
            
            float3 blendMultiply(float3 base, float3 blend)
            {
                return base * blend;
            }
            
            float3 blendScreen(float3 base, float3 blend)
            {
                return 1.0 - (1.0 - base) * (1.0 - blend);
            }
            
            float3 blendOverlay(float3 base, float3 blend)
            {
                float3 result;
                result.r = base.r < 0.5 ? (2.0 * base.r * blend.r) : (1.0 - 2.0 * (1.0 - base.r) * (1.0 - blend.r));
                result.g = base.g < 0.5 ? (2.0 * base.g * blend.g) : (1.0 - 2.0 * (1.0 - base.g) * (1.0 - blend.g));
                result.b = base.b < 0.5 ? (2.0 * base.b * blend.b) : (1.0 - 2.0 * (1.0 - base.b) * (1.0 - blend.b));
                return result;
            }
            
            float3 blendSoftLight(float3 base, float3 blend)
            {
                return (1.0 - 2.0 * blend) * base * base + 2.0 * blend * base;
            }
            
            float3 blendHardLight(float3 base, float3 blend)
            {
                return blendOverlay(blend, base);
            }
            
            float3 blendColorDodge(float3 base, float3 blend)
            {
                return base / (1.0 - blend + 0.001);
            }
            
            float3 blendColorBurn(float3 base, float3 blend)
            {
                return 1.0 - (1.0 - base) / (blend + 0.001);
            }
            
            float3 blendDarken(float3 base, float3 blend)
            {
                return min(base, blend);
            }
            
            float3 blendLighten(float3 base, float3 blend)
            {
                return max(base, blend);
            }
            
            float3 blendDifference(float3 base, float3 blend)
            {
                return abs(base - blend);
            }
            
            float3 blendExclusion(float3 base, float3 blend)
            {
                return base + blend - 2.0 * base * blend;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 base = tex2D(_MainTex, i.uv);
                
                // Transform blend texture UVs
                float2 blendUV = i.uv;
                blendUV = (blendUV - _BlendTexOffset) / _BlendTexScale;
                if (abs(_BlendTexRotation) > 0.01)
                {
                    blendUV = rotateUV(blendUV, _BlendTexRotation);
                }
                
                fixed4 blend = tex2D(_BlendTex, blendUV);
                
                // Apply blend mode
                float3 result = base.rgb;
                
                #if _BLENDMODE_NORMAL
                    result = blendNormal(base.rgb, blend.rgb);
                #elif _BLENDMODE_MULTIPLY
                    result = blendMultiply(base.rgb, blend.rgb);
                #elif _BLENDMODE_SCREEN
                    result = blendScreen(base.rgb, blend.rgb);
                #elif _BLENDMODE_OVERLAY
                    result = blendOverlay(base.rgb, blend.rgb);
                #elif _BLENDMODE_SOFTLIGHT
                    result = blendSoftLight(base.rgb, blend.rgb);
                #elif _BLENDMODE_HARDLIGHT
                    result = blendHardLight(base.rgb, blend.rgb);
                #elif _BLENDMODE_COLORDODGE
                    result = blendColorDodge(base.rgb, blend.rgb);
                #elif _BLENDMODE_COLORBURN
                    result = blendColorBurn(base.rgb, blend.rgb);
                #elif _BLENDMODE_DARKEN
                    result = blendDarken(base.rgb, blend.rgb);
                #elif _BLENDMODE_LIGHTEN
                    result = blendLighten(base.rgb, blend.rgb);
                #elif _BLENDMODE_DIFFERENCE
                    result = blendDifference(base.rgb, blend.rgb);
                #elif _BLENDMODE_EXCLUSION
                    result = blendExclusion(base.rgb, blend.rgb);
                #endif
                
                // Apply mask if enabled
                float maskValue = 1.0;
                if (_UseMask > 0.5)
                {
                    fixed4 mask = tex2D(_MaskTex, i.uv);
                    if (_MaskChannel == 0)
                        maskValue = mask.r;
                    else if (_MaskChannel == 1)
                        maskValue = mask.g;
                    else if (_MaskChannel == 2)
                        maskValue = mask.b;
                    else
                        maskValue = mask.a;
                }
                
                // Blend with amount and mask
                float finalBlend = _BlendAmount * maskValue;
                result = lerp(base.rgb, result, finalBlend);
                
                return fixed4(result, base.a);
            }
            ENDCG
        }
    }
}
