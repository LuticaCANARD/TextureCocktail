Shader "Hidden/TextureCocktailTests/TextureCocktailLegacyFixture"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Gain ("Gain", Range(0, 3)) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Name "Copy"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Gain;
            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv);
                color.rgb *= _Gain;
                return color;
            }
            ENDCG
        }
    }
}
