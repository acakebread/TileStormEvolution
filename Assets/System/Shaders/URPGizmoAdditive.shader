Shader "Hidden/URPGizmoAdditive"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Overlay"        // Ensures it draws last
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "GizmoAdditive"

            // Core settings
            ZWrite Off                 // Do not write depth
            ZTest Always               // Always pass depth test
            Blend SrcAlpha One          // Additive blending
            Cull Off                   // Render both sides

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 _BaseColor;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _BaseColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
