Shader "Hidden/TextureCocktailTests/ReorderedPasses"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        fixed4 frag(v2f_img i) : SV_Target { return tex2D(_MainTex, i.uv); }
        ENDCG
        Pass
        {
            Name "HistogramEnhance"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            ENDCG
        }
        Pass
        {
            Name "ColorSegmentation"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            ENDCG
        }
        Pass
        {
            Name "CannyEdge"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            ENDCG
        }
        Pass
        {
            Name "EdgeDetection"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            ENDCG
        }
    }
}
