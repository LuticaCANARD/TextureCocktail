Shader "Hidden/TextureCocktail/MeshVisibilityOverlay"
{
    Properties
    {
        _Color ("Candidate Color", Color) = (1, 0.2, 0.2, 0.5)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest [_ZTest]
            Blend SrcAlpha OneMinusSrcAlpha
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; };
            fixed4 _Color;

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                return output;
            }
            fixed4 frag(v2f input) : SV_Target { return _Color; }
            ENDCG
        }
    }
}
