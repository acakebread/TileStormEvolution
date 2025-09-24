// //doesn't work!
// Shader "Unlit/URPFrosted2Pass"
// {
//     Properties
//     {
//         _BaseColor ("Base Color", Color) = (0.25, 0.25, 0.25, 1)
//         _Radius ("Radius", Range(1, 128)) = 64
//         _MainTex ("Texture", 2D) = "white" {}
//         _NoiseTex ("Noise Texture", 2D) = "white" {}
//         _NoiseStrength ("Noise Strength", Range(0, 0.1)) = 0.02
//     }
//     SubShader
//     {
//         Tags
//         {
//             "RenderType" = "Transparent"
//             "Queue" = "Transparent+1"
//             "RenderPipeline" = "UniversalPipeline"
//         }
//         LOD 100

//         // First Pass: Compute blur and noise, write to temporary texture
//         Pass
//         {
//             Name "FrostedBlur"
//             ZWrite Off

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag
//             #pragma unroll
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

//             struct Attributes
//             {
//                 float4 positionOS : POSITION;
//                 float2 uv : TEXCOORD0;
//             };

//             struct Varyings
//             {
//                 float4 positionCS : SV_POSITION;
//                 float2 uv : TEXCOORD0;
//                 float4 screenPos : TEXCOORD1;
//             };

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _BaseColor;
//                 float _Radius;
//                 float _NoiseStrength;
//                 float4 _MainTex_TexelSize;
//             CBUFFER_END

//             TEXTURE2D(_MainTex);
//             SAMPLER(sampler_MainTex);
//             TEXTURE2D(_NoiseTex);
//             SAMPLER(sampler_NoiseTex);

//             Varyings vert(Attributes input)
//             {
//                 Varyings output;
//                 output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
//                 output.uv = input.uv;
//                 output.screenPos = ComputeScreenPos(output.positionCS);
//                 return output;
//             }

//             half4 frag(Varyings input) : SV_Target
//             {
//                 float2 screenUV = input.screenPos.xy / input.screenPos.w;
//                 half4 sum = 0;
//                 float measurements = 1;

//                 // Sample center pixel
//                 sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);

//                 // Radial blur with dithered sampling
//                 float step = 0.1;
//                 float radius = _Radius * 1.41421356237;
//                 float scale = 1.0 / input.screenPos.w;
//                 float2 pixelPos = screenUV * _ScreenParams.xy;
//                 float dither = fmod(pixelPos.x + pixelPos.y, 2.0);

//                 float diagScale = 0.70710678118;
//                 float2 sampleOffsets[4];
//                 if (dither < 1.0)
//                 {
//                     sampleOffsets[0] = float2(diagScale, diagScale);
//                     sampleOffsets[1] = float2(diagScale, -diagScale);
//                     sampleOffsets[2] = float2(-diagScale, diagScale);
//                     sampleOffsets[3] = float2(-diagScale, -diagScale);
//                 }
//                 else
//                 {
//                     sampleOffsets[0] = float2(1.0, 0.0);
//                     sampleOffsets[1] = float(-1.0, 0.0);
//                     sampleOffsets[2] = float2(0.0, 1.0);
//                     sampleOffsets[3] = float2(0.0, -1.0);
//                 }

//                 // Sample loop
//                 for (float range = step; range <= radius; range += step)
//                 {
//                     float2 texelOffset = _MainTex_TexelSize.xy * range * scale;
//                     for (int i = 0; i < 4; i++)
//                     {
//                         sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV + texelOffset * sampleOffsets[i]);
//                     }
//                     measurements += 4;
//                 }

//                 half4 color = sum / measurements;

//                 // Apply noise
//                 half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv);
//                 color.rgb += (noise.rgb - 0.5) * _NoiseStrength;

//                 return color; // Write blurred color with noise to render texture
//             }
//             ENDHLSL
//         }

//         // Second Pass: Read from temporary texture and blend with base color
//         Pass
//         {
//             Name "FrostedBlur"
//             ZWrite Off

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

//             struct Attributes
//             {
//                 float4 positionOS : POSITION;
//                 float2 uv : TEXCOORD0;
//             };

//             struct Varyings
//             {
//                 float4 positionCS : SV_POSITION;
//                 float2 uv : TEXCOORD0;
//                 float4 screenPos : TEXCOORD1;
//             };

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _BaseColor;
//                 float _Radius;
//                 float _NoiseStrength;
//                 float4 _MainTex_TexelSize;
//             CBUFFER_END

//             TEXTURE2D(_MainTex);
//             SAMPLER(sampler_MainTex);
//             TEXTURE2D(_NoiseTex);
//             SAMPLER(sampler_NoiseTex);

//             Varyings vert(Attributes input)
//             {
//                 Varyings output;
//                 output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
//                 output.uv = input.uv;
//                 output.screenPos = ComputeScreenPos(output.positionCS);
//                 return output;
//             }

//             half4 frag(Varyings input) : SV_Target
//             {
//                 float2 screenUV = input.screenPos.xy / input.screenPos.w;
//                 half4 sum = 0;
//                 float measurements = 1;

//                 // Sample center pixel
//                 sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);

//                 // Radial blur with dithered sampling
//                 float step = 0.1;
//                 float radius = _Radius * 1.41421356237;
//                 float scale = 1.0 / input.screenPos.w;
//                 float2 pixelPos = screenUV * _ScreenParams.xy;
//                 float dither = fmod(pixelPos.x + pixelPos.y, 2.0);

//                 float diagScale = 0.70710678118;
//                 float2 sampleOffsets[4];
//                 if (dither < 1.0)
//                 {
//                     sampleOffsets[0] = float2(diagScale, diagScale);
//                     sampleOffsets[1] = float2(diagScale, -diagScale);
//                     sampleOffsets[2] = float2(-diagScale, diagScale);
//                     sampleOffsets[3] = float2(-diagScale, -diagScale);
//                 }
//                 else
//                 {
//                     sampleOffsets[0] = float2(1.0, 0.0);
//                     sampleOffsets[1] = float2(-1.0, 0.0);
//                     sampleOffsets[2] = float2(0.0, 1.0);
//                     sampleOffsets[3] = float2(0.0, -1.0);
//                 }

//                 // Sample loop
//                 for (float range = step; range <= radius; range += step)
//                 {
//                     float2 texelOffset = _MainTex_TexelSize.xy * range * scale;
//                     // Manually unroll the inner loop
//                     sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV + texelOffset * sampleOffsets[0]);
//                     sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV + texelOffset * sampleOffsets[1]);
//                     sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV + texelOffset * sampleOffsets[2]);
//                     sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV + texelOffset * sampleOffsets[3]);
//                     measurements += 4;
//                 }

//                 half4 color = sum / measurements;

//                 // Apply noise
//                 half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv);
//                 color.rgb += (noise.rgb - 0.5) * _NoiseStrength;

//                 return color;
//             }
//             ENDHLSL
//         }
// }
// }