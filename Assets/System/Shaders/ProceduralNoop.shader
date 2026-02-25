Shader "Hidden/CommandBuffer/ProceduralNoop"
{
    SubShader
    {
        Pass
        {
            ColorMask 0
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 vert(uint vertexID : SV_VertexID) : SV_POSITION
            {
                return float4(0,0,0,1);     // degenerate — doesn't matter
            }

            fixed4 frag() : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
}