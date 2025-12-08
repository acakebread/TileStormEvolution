Shader "Unlit/PreviewGroundPlane"
{
    Properties
    {
        _Color ("Ground Color", Color) = (0.18, 0.18, 0.18, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+500" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
            CBUFFER_END

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = float4(v.vertex.xy * 2.0 - 1.0, 0.5, 1.0);
                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Only draw at y = -0.2 with small tolerance
                if (abs(i.worldPos.y + 0.2) > 0.05) discard;
                return half4(_Color.rgb, 1);
            }
            ENDHLSL
        }
    }
}