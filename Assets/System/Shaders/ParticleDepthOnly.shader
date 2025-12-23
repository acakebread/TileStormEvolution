Shader "MassiveHadronLtd/Unlit/ParticleDepthOnly"
{
    Properties
    {
        _BaseMap ("Base Texture (alpha)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }  // Opaque-like for early depth

        Pass
        {
            ColorMask 0  // No color write
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            void frag(Varyings IN)
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half alpha = texColor.a * IN.color.a;
                if (alpha < 0.05) discard;  // Soft edges
            }
            ENDHLSL
        }
    }
}

// Shader "MassiveHadronLtd/Unlit/ParticleDepthOnly"
// {
//     Properties
//     {
//         _BaseMap ("Base Texture (alpha)", 2D) = "white" {}
//     }
//     SubShader
//     {
//         Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

//         Pass
//         {
//             ColorMask 0  // No color write
//             ZWrite On
//             Cull Off

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag

//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

//             struct Attributes
//             {
//                 float4 positionOS : POSITION;
//                 float4 color : COLOR;
//                 float2 uv : TEXCOORD0;
//             };

//             struct Varyings
//             {
//                 float4 positionCS : SV_POSITION;
//                 float4 color : COLOR;
//                 float2 uv : TEXCOORD0;
//             };

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _BaseMap_ST;
//             CBUFFER_END

//             TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

//             Varyings vert(Attributes IN)
//             {
//                 Varyings OUT;
//                 OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
//                 OUT.color = IN.color;
//                 OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
//                 return OUT;
//             }

//             void frag(Varyings IN)
//             {
//                 half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
//                 half alpha = texColor.a * IN.color.a;

//                 // Binary: only write depth if alpha > 0 (any contribution)
//                 if (alpha <= 0.0) discard;

//                 // Depth written automatically — color untouched
//             }
//             ENDHLSL
//         }
//     }
// }

// Shader "MassiveHadronLtd/Unlit/ParticleDepthOnly"
// {
//     Properties
//     {
//         _BaseMap ("Base Texture (alpha)", 2D) = "white" {}
//     }
//     SubShader
//     {
//         Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }  // Opaque-like for early depth

//         Pass
//         {
//             ColorMask 0  // No color write
//             ZWrite On
//             Cull Off

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag

//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

//             struct Attributes
//             {
//                 float4 positionOS : POSITION;
//                 float4 color : COLOR;
//                 float2 uv : TEXCOORD0;
//             };

//             struct Varyings
//             {
//                 float4 positionCS : SV_POSITION;
//                 float4 color : COLOR;
//                 float2 uv : TEXCOORD0;
//             };

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _BaseMap_ST;
//             CBUFFER_END

//             TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

//             Varyings vert(Attributes IN)
//             {
//                 Varyings OUT;
//                 OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
//                 OUT.color = IN.color;
//                 OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
//                 return OUT;
//             }

//             void frag(Varyings IN)
//             {
//                 half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
//                 half alpha = texColor.a * IN.color.a;
//                 if (alpha < 0.001) discard;  // Soft edges
//             }
//             ENDHLSL
//         }
//     }
// }

// Shader "MassiveHadronLtd/Unlit/ParticleDepthOnly"
// {
//     Properties
//     {
//         _BaseMap ("Base Texture (for alpha)", 2D) = "white" {}
//     }
//     SubShader
//     {
//         Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

//         Pass
//         {
//             Name "DepthOnly"
//             Tags { "LightMode" = "DepthOnly" }

//             ColorMask 0  // Write NOTHING to color buffer
//             ZWrite On
//             Cull Off

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag

//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

//             struct Attributes
//             {
//                 float4 positionOS : POSITION;
//                 float4 color : COLOR;
//                 float2 uv : TEXCOORD0;
//             };

//             struct Varyings
//             {
//                 float4 positionCS : SV_POSITION;
//                 float4 color : COLOR;
//                 float2 uv : TEXCOORD0;
//             };

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _BaseMap_ST;
//             CBUFFER_END

//             TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

//             Varyings vert(Attributes IN)
//             {
//                 Varyings OUT;
//                 OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
//                 OUT.color = IN.color;
//                 OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
//                 return OUT;
//             }

//             void frag(Varyings IN)
//             {
//                 half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
//                 half alpha = texColor.a * IN.color.a;

//                 // Preserve soft edges — discard only near-zero
//                 if (alpha < 0.05) discard;

//                 // No color output — depth written automatically
//             }
//             ENDHLSL
//         }
//     }
// }