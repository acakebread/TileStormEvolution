Shader "Custom/CommandRender"
{
    Properties
    {
        _Color ("Dummy Color", Color) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" }

        Pass
        {
            ColorMask 0
            ZWrite Off
            Cull Off
            Blend Zero One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float4 _Color;

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert(uint vertexID : SV_VertexID)
            {
                v2f o;
                // Position doesn't matter → we write nothing anyway
                // Simplest valid clip-space value
                o.vertex = float4(0, 0, 0, 1);
                return o;
            }

            fixed4 frag() : SV_Target
            {
                return _Color;   // ← keeps the property upload alive
            }
            ENDCG
        }
    }
}