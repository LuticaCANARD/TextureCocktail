Shader "Luticalab/ImageSync"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OverlayTex ("Overlay Texture", 2D) = "white" {}
        _OverlayPos ("Overlay Pos", Vector) = (0,0,0,0)
        [KeywordEnum(Add, Subtract, Multiply)] 
        _BlendOp ("Blend Operation", Float) = 0
        _Color ("Tint", Color) = (1,1,1,1)
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
            #pragma multi_compile _BLENDOP_ADD _BLENDOP_SUBTRACT _BLENDOP_MULTIPLY

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;

            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };
            sampler2D _MainTex;
            sampler2D _OverlayTex;
            float4 _OverlayPos;
            fixed4 _Color;
            v2f vert (appdata IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }


            fixed4 frag (v2f IN) : SV_Target
            {
                fixed4 mainColor = tex2D(_MainTex, IN.texcoord) * IN.color;

                // 오버레이 텍스쳐의 UV 좌표 계산 (위치 오프셋 적용)
                float2 overlayUV = IN.texcoord - _OverlayPos.xy;

                fixed4 overlayColor = fixed4(0,0,0,0); // 기본은 투명

                // 오버레이 텍스쳐의 UV가 0~1 범위 내에 있을 때만 샘플링
                if (overlayUV.x > 0 && overlayUV.x < 1 && overlayUV.y > 0 && overlayUV.y < 1)
                {
                    overlayColor = tex2D(_OverlayTex, overlayUV);
                }

                fixed4 finalColor;

                #if _BLENDOP_ADD
                    finalColor = mainColor + overlayColor;
                #elif _BLENDOP_SUBTRACT
                    finalColor = mainColor - overlayColor;
                #elif _BLENDOP_MULTIPLY
                    finalColor = mainColor * overlayColor;
                #else
                    finalColor = mainColor;
                #endif

                finalColor.rgb *= finalColor.a;

                return finalColor;


            }
            ENDCG
        }
    }
}
