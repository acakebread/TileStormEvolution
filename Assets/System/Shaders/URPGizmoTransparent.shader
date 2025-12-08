Shader "Hidden/URPGizmoTransparent"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,0.6)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+500"           // After most geometry, before Overlay gizmos
            "IgnoreProjector"="True"
            "ForceNoShadowCasting"="True"
            "RenderPipeline"="UniversalPipeline"
            "PreviewType"="Plane"
        }
        LOD 100

        // ──────────────────────────────────────────────────────────────
        Pass
        {
            Name "GizmoTransparent"

            ZWrite Off
            ZTest Always               // Always pass depth test
            Blend SrcAlpha OneMinusSrcAlpha   // Classic alpha transparency
            Cull Back                         // Front faces only (we have proper inside/outside geometry now)

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
        // ──────────────────────────────────────────────────────────────
    }

    FallBack Off
}