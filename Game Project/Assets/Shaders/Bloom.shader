Shader "Custom/Bloom"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
       Cull Off
       ZTest Always
       ZWrite OFF
       Pass {
           CGPROGRAM
               #pragma vertex VertexProgram
               #pragma fragment FragmentProgram
           ENDCG
       }
    }
}