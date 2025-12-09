Shader "Hidden/URPGizmoSolid"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry+500"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent+500"           // After most geometry, before Overlay gizmos
        }

        LOD 100

        Pass
        {
            Name "GizmoSolid"

            ZWrite Off
            ZTest Always
            Cull Back
            Blend One Zero   // fully opaque

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
