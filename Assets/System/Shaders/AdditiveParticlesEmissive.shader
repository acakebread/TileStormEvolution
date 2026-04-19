Shader "MassiveHadronLtd/Unlit/AdditiveParticlesEmissive"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        [HDR] _BaseColor ("Base Color (HDR)", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("Emission Color (HDR - for bloom)", Color) = (0,0,0,0)
        _EmissionMap ("Emission Map (Optional - overrides base alpha)", 2D) = "" {}
        _EmissionBoost ("Emission Boost", Range(0.1, 20)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        LOD 100

        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float4 _EmissionMap_ST;
                float _EmissionBoost;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color * _BaseColor;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 baseColor = IN.color * texColor;           // includes vertex color (RGB + A)
    
                // Fade both base and emission using vertex alpha
                baseColor.rgb *= IN.color.a;                     // same as your non-emissive shader
    
                // Determine emission mask (unchanged)
                half emissionMask = 1.0;

                #if defined(_EMISSIONMAP)
                    half4 emissionTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, TRANSFORM_TEX(IN.uv, _EmissionMap));
                    emissionMask = dot(emissionTex.rgb, 0.333);  // or use .a if you prefer
                #else
                    emissionMask = texColor.a;
                #endif

                half3 emission = emissionMask * _EmissionColor.rgb * _EmissionBoost * IN.color.a;  // ← key change: * IN.color.a

                half3 finalColor = baseColor.rgb + emission;

                finalColor = MixFog(finalColor, IN.fogFactor);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}