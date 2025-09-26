Shader "Unlit/URPWaterOpaque"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.25, 0.5, 0.75, 1)
        _MainTex ("Texture", 2D) = "white" {}
        _WaterDepthTexture ("Water Depth Texture", 2D) = "black" {}
        _RippleSpeed ("Ripple Speed", Range(0, 1)) = 0.5
        _RippleAmplitude ("Ripple Amplitude", Range(0, 1)) = 0.5
        _RippleFrequency ("Ripple Frequency", Range(0, 1)) = 0.5
        _RippleOffset ("Ripple Offset", Range(0, 1)) = 0.5
        _TimeSeed ("Time Seed", Float) = 0.0
        _DepthThreshold ("Depth Threshold", Float) = 2.0
        _DepthTolerance ("Depth Tolerance", Float) = 0.01
        _DebugDepthScalar ("Debug Depth Mode", Range(0, 6)) = 4 // Added mode 5 for raw _WaterDepthTexture
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "WaterOpaque"
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _RippleSpeed;
                float _RippleAmplitude;
                float _RippleFrequency;
                float _RippleOffset;
                float _TimeSeed;
                float _DepthThreshold;
                float _DepthTolerance;
                float _DebugDepthScalar;
                float4 _MainTex_TexelSize;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_WaterDepthTexture);
            SAMPLER(sampler_WaterDepthTexture);

            // Internal scalars for adjusting normalized inputs
            #define RIPPLE_SPEED_SCALE 20.0
            #define RIPPLE_AMPLITUDE_SCALE 0.5
            #define RIPPLE_FREQUENCY_SCALE 250.0
            #define RIPPLE_FREQUENCY_OFFSET 1.0

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // Sample depths
                float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
                float waterSampledDepth = SAMPLE_TEXTURE2D(_WaterDepthTexture, sampler_WaterDepthTexture, screenUV).r; // Depth in red channel
                float fragmentDepth = input.screenPos.z / input.screenPos.w;

                // Convert depths to linear eye-space
                float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
                float waterSampledDepthLinear = LinearEyeDepth(waterSampledDepth, _ZBufferParams);
                float fragmentDepthLinear = LinearEyeDepth(fragmentDepth, _ZBufferParams);

                // Calculate depth difference (using _WaterDepthTexture)
                float depthDifference = abs(waterSampledDepthLinear - fragmentDepthLinear);

                // Debug modes
                //if (_DebugDepthScalar < 0.5) // Mode 0: Nonlinear depth (_CameraDepthTexture)
                // {
                //     float scaledDepth = sampledDepth * 100.0;
                //     return half4(scaledDepth, scaledDepth, scaledDepth, 1);
                // }
                // else if (_DebugDepthScalar < 1.5) // Mode 1: Linear depth (_CameraDepthTexture)
                // {
                //     float scaledLinearDepth = sampledDepthLinear / 10.0;
                //     return half4(scaledLinearDepth, scaledLinearDepth, scaledLinearDepth, 1);
                // }
                // else if (_DebugDepthScalar < 2.5) // Mode 2: Depth difference (_WaterDepthTexture)
                // {
                //     return half4(depthDifference, depthDifference, depthDifference, 1);
                // }
                // else if (_DebugDepthScalar < 3.5) // Mode 3: Nonlinear fragment depth
                // {
                //     float scaledFragmentDepth = fragmentDepth * 100.0;
                //     return half4(scaledFragmentDepth, scaledFragmentDepth, scaledFragmentDepth, 1);
                // }
                // else if (_DebugDepthScalar < 4.5) // Mode 4: Linear depth (_WaterDepthTexture)
                // {
                //     float scaledWaterDepthLinear = waterSampledDepthLinear / 10.0;
                //     return half4(scaledWaterDepthLinear, scaledWaterDepthLinear, scaledWaterDepthLinear, 1);
                // }
                // else if (_DebugDepthScalar < 5.5) // Mode 5: Raw _WaterDepthTexture
                // {
                //     return half4(waterSampledDepth, waterSampledDepth, waterSampledDepth, 1);
                // }
                // else // Mode 6: Water effect
                // {
                    // Normalize and scale inputs
                    float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
                    float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
                    float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE + RIPPLE_FREQUENCY_OFFSET;

                    // Calculate depth scalar using _WaterDepthTexture
                    float normalizedDepth = depthDifference / _DepthThreshold;
                    float depthScalar = normalizedDepth * normalizedDepth;
                    depthScalar = min(depthScalar, 1.0);

                    // Procedural ripple displacement
                    float2 uv = input.uv;
                    float time = _TimeSeed * speed;

                    float2 wave1Dir = normalize(float2(1, 1));
                    float2 wave2Dir = normalize(float2(-1, 1));

                    float wave1 = sin(dot(uv, wave1Dir) * frequency + time * 1.41421356237 + _RippleOffset) + sin(frequency + time * 0.173205080757 + _RippleOffset);
                    float wave2 = sin(dot(uv, wave2Dir) * frequency + time * 1.61803398875 + _RippleOffset) + sin(frequency + time * 0.223606797750 + _RippleOffset);

                    float2 displacement = amplitude * (wave1 * wave1Dir + wave2 * wave2Dir) / input.screenPos.w * depthScalar;

                    // Apply displacement to UVs
                    float2 displacedUV = screenUV + displacement;

                    // Sample texture with displaced UVs
                    half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

                    // Apply base color tint
                    color.rgb = lerp(color.rgb, _BaseColor.rgb, 0.5);

                    return half4(color.rgb, 1);
                // }
            }
            ENDHLSL
        }
    }
}


// Shader "Unlit/URPWaterOpaque"
// {
//     Properties
//     {
//         _BaseColor ("Base Color", Color) = (0.25, 0.5, 0.75, 1)
//         _MainTex ("Texture", 2D) = "white" {}
//         _WaterDepthTexture ("Water Depth Texture", 2D) = "black" {}
//         _RippleSpeed ("Ripple Speed", Range(0, 1)) = 0.5
//         _RippleAmplitude ("Ripple Amplitude", Range(0, 1)) = 0.5
//         _RippleFrequency ("Ripple Frequency", Range(0, 1)) = 0.5
//         _RippleOffset ("Ripple Offset", Range(0, 1)) = 0.5
//         _TimeSeed ("Time Seed", Float) = 0.0
//         _DepthThreshold ("Depth Threshold", Float) = 2.0
//         _DepthTolerance ("Depth Tolerance", Float) = 0.01
//         _DebugDepthScalar ("Debug Depth Mode", Range(0, 5)) = 0 // 0 = nonlinear depth, 1 = linear depth, 2 = depth difference, 3 = nonlinear fragment, 4 = linear water depth, 5 = water effect
//     }
//     SubShader
//     {
//         Tags
//         {
//             "RenderType" = "Opaque"
//             "Queue" = "Geometry"
//             "RenderPipeline" = "UniversalPipeline"
//         }
//         LOD 100

//         Pass
//         {
//             Name "WaterOpaque"
//             ZWrite On
//             ZTest LEqual

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
//                 float _RippleSpeed;
//                 float _RippleAmplitude;
//                 float _RippleFrequency;
//                 float _RippleOffset;
//                 float _TimeSeed;
//                 float _DepthThreshold;
//                 float _DepthTolerance;
//                 float _DebugDepthScalar;
//                 float4 _MainTex_TexelSize;
//             CBUFFER_END

//             TEXTURE2D(_MainTex);
//             SAMPLER(sampler_MainTex);
//             TEXTURE2D(_WaterDepthTexture);
//             SAMPLER(sampler_WaterDepthTexture);

//             // Internal scalars for adjusting normalized inputs
//             #define RIPPLE_SPEED_SCALE 20.0
//             #define RIPPLE_AMPLITUDE_SCALE 0.5
//             #define RIPPLE_FREQUENCY_SCALE 250.0
//             #define RIPPLE_FREQUENCY_OFFSET 1.0

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

//                 // Sample depths
//                 float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
//                 float waterSampledDepth = SAMPLE_TEXTURE2D(_WaterDepthTexture, sampler_WaterDepthTexture, screenUV).r;
//                 float fragmentDepth = input.screenPos.z / input.screenPos.w;

//                 // Convert depths to linear eye-space
//                 float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
//                 float waterSampledDepthLinear = LinearEyeDepth(waterSampledDepth, _ZBufferParams);
//                 float fragmentDepthLinear = LinearEyeDepth(fragmentDepth, _ZBufferParams);

//                 // Calculate depth difference (using _WaterDepthTexture)
//                 float depthDifference = abs(waterSampledDepthLinear - fragmentDepthLinear);

//                 // // Debug modes
//                 // if (_DebugDepthScalar < 0.5) // Mode 0: Nonlinear depth (_CameraDepthTexture)
//                 // {
//                 //     float scaledDepth = sampledDepth * 100.0;
//                 //     return half4(scaledDepth, scaledDepth, scaledDepth, 1);
//                 // }
//                 // else if (_DebugDepthScalar < 1.5) // Mode 1: Linear depth (_CameraDepthTexture)
//                 // {
//                 //     float scaledLinearDepth = sampledDepthLinear / 10.0;
//                 //     return half4(scaledLinearDepth, scaledLinearDepth, scaledLinearDepth, 1);
//                 // }
//                 // else if (_DebugDepthScalar < 2.5) // Mode 2: Depth difference (_WaterDepthTexture)
//                 // {
//                 //     return half4(depthDifference, depthDifference, depthDifference, 1);
//                 // }
//                 // else if (_DebugDepthScalar < 3.5) // Mode 3: Nonlinear fragment depth
//                 // {
//                 //     float scaledFragmentDepth = fragmentDepth * 100.0;
//                 //     return half4(scaledFragmentDepth, scaledFragmentDepth, scaledFragmentDepth, 1);
//                 // }
//                 // else if (_DebugDepthScalar < 4.5) // Mode 4: Linear depth (_WaterDepthTexture)
//                 {
//                     //float scaledWaterDepthLinear = waterSampledDepthLinear / 10.0;
//                     //return half4(scaledWaterDepthLinear, scaledWaterDepthLinear, scaledWaterDepthLinear, 1);
//                     return SAMPLE_TEXTURE2D(_WaterDepthTexture, sampler_WaterDepthTexture, screenUV);
//                 }
//                 // else // Mode 5: Water effect
//                 // {
//                 //     // Normalize and scale inputs
//                 //     float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
//                 //     float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
//                 //     float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE + RIPPLE_FREQUENCY_OFFSET;

//                 //     // Calculate depth scalar using _WaterDepthTexture
//                 //     float normalizedDepth = depthDifference / _DepthThreshold;
//                 //     float depthScalar = normalizedDepth * normalizedDepth;
//                 //     depthScalar = min(depthScalar, 1.0);

//                 //     // Procedural ripple displacement
//                 //     float2 uv = input.uv;
//                 //     float time = _TimeSeed * speed;

//                 //     float2 wave1Dir = normalize(float2(1, 1));
//                 //     float2 wave2Dir = normalize(float2(-1, 1));

//                 //     float wave1 = sin(dot(uv, wave1Dir) * frequency + time * 1.41421356237 + _RippleOffset) + sin(frequency + time * 0.173205080757 + _RippleOffset);
//                 //     float wave2 = sin(dot(uv, wave2Dir) * frequency + time * 1.61803398875 + _RippleOffset) + sin(frequency + time * 0.223606797750 + _RippleOffset);

//                 //     float2 displacement = amplitude * (wave1 * wave1Dir + wave2 * wave2Dir) / input.screenPos.w * depthScalar;

//                 //     // Apply displacement to UVs
//                 //     float2 displacedUV = screenUV + displacement;

//                 //     // Sample texture with displaced UVs
//                 //     half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

//                 //     // Apply base color tint
//                 //     color.rgb = lerp(color.rgb, _BaseColor.rgb, 0.5);

//                 //     return half4(color.rgb, 1);
//                 // }
//             }
//             ENDHLSL
//         }
//     }
// }

// Shader "Unlit/URPWaterOpaque"
// {
//     Properties
//     {
//         _BaseColor ("Base Color", Color) = (0.25, 0.5, 0.75, 1)
//         _MainTex ("Texture", 2D) = "white" {}
//         _WaterDepthTexture ("Water Depth Texture", 2D) = "white" {} // Depth from renderTexture
//         _RippleSpeed ("Ripple Speed", Range(0, 1)) = 0.5
//         _RippleAmplitude ("Ripple Amplitude", Range(0, 1)) = 0.5
//         _RippleFrequency ("Ripple Frequency", Range(0, 1)) = 0.5
//         _RippleOffset ("Ripple Offset", Range(0, 1)) = 0.5
//         _TimeSeed ("Time Seed", Float) = 0.0
//         _DepthThreshold ("Depth Threshold", Float) = 2.0
//         _DepthTolerance ("Depth Tolerance", Float) = 0.01
//         _DebugDepthScalar ("Debug Depth Mode", Range(0, 5.5)) = 5 // 0 = nonlinear depth, 1 = linear depth, 2 = depth difference, 3 = nonlinear fragment, 4 = linear fragment, 5 = water effect, 5.5 = nonlinear water depth
//     }
//     SubShader
//     {
//         Tags
//         {
//             "RenderType" = "Opaque"
//             "Queue" = "Geometry"
//             "RenderPipeline" = "UniversalPipeline"
//         }
//         LOD 100

//         Pass
//         {
//             Name "WaterOpaque"
//             ZWrite On
//             ZTest LEqual

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
//                 float _RippleSpeed;
//                 float _RippleAmplitude;
//                 float _RippleFrequency;
//                 float _RippleOffset;
//                 float _TimeSeed;
//                 float _DepthThreshold;
//                 float _DepthTolerance;
//                 float _DebugDepthScalar;
//                 float4 _MainTex_TexelSize;
//                 float4 _WaterDepthTexture_TexelSize;
//             CBUFFER_END

//             TEXTURE2D(_MainTex);
//             SAMPLER(sampler_MainTex);
//             TEXTURE2D(_WaterDepthTexture);
//             SAMPLER(sampler_WaterDepthTexture);

//             // Internal scalars for adjusting normalized inputs
//             #define RIPPLE_SPEED_SCALE 20.0
//             #define RIPPLE_AMPLITUDE_SCALE 0.5
//             #define RIPPLE_FREQUENCY_SCALE 250.0
//             #define RIPPLE_FREQUENCY_OFFSET 1.0

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

//                 // Sample depths
//                 float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
//                 float waterDepth = SAMPLE_TEXTURE2D(_WaterDepthTexture, sampler_WaterDepthTexture, screenUV).r;
//                 float fragmentDepth = input.screenPos.z / input.screenPos.w;

//                 // Convert depths to linear eye-space
//                 float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
//                 float waterDepthLinear = LinearEyeDepth(waterDepth, _ZBufferParams);
//                 float fragmentDepthLinear = LinearEyeDepth(fragmentDepth, _ZBufferParams);

//                 // Calculate depth differences
//                 float depthDifference = abs(sampledDepthLinear - fragmentDepthLinear);
//                 float waterDepthDifference = abs(waterDepthLinear - fragmentDepthLinear);

//                 // Debug modes
//                 if (_DebugDepthScalar < 0.5) // Mode 0: Nonlinear depth (_CameraDepthTexture)
//                 {
//                     float scaledDepth = sampledDepth * 100.0;
//                     return half4(scaledDepth, scaledDepth, scaledDepth, 1);
//                 }
//                 else if (_DebugDepthScalar < 1.5) // Mode 1: Linear depth (_CameraDepthTexture)
//                 {
//                     float scaledLinearDepth = sampledDepthLinear / 10.0;
//                     return half4(scaledLinearDepth, scaledLinearDepth, scaledLinearDepth, 1);
//                 }
//                 else if (_DebugDepthScalar < 2.5) // Mode 2: Depth difference (_CameraDepthTexture)
//                 {
//                     return half4(depthDifference, depthDifference, depthDifference, 1);
//                 }
//                 else if (_DebugDepthScalar < 3.5) // Mode 3: Nonlinear fragment depth
//                 {
//                     float scaledFragmentDepth = fragmentDepth * 100.0;
//                     return half4(scaledFragmentDepth, scaledFragmentDepth, scaledFragmentDepth, 1);
//                 }
//                 else if (_DebugDepthScalar < 4.5) // Mode 4: Linear fragment depth
//                 {
//                     float scaledFragmentLinear = fragmentDepthLinear / 10.0;
//                     return half4(scaledFragmentLinear, scaledFragmentLinear, scaledFragmentLinear, 1);
//                 }
//                 else if (_DebugDepthScalar < 5.0) // Mode 5: Water effect using _WaterDepthTexture
//                 {
//                     // Normalize and scale inputs
//                     float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
//                     float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
//                     float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE + RIPPLE_FREQUENCY_OFFSET;

//                     // Calculate depth scalar using _WaterDepthTexture
//                     float normalizedDepth = waterDepthDifference / _DepthThreshold;
//                     float depthScalar = normalizedDepth * normalizedDepth;
//                     depthScalar = min(depthScalar, 1.0);

//                     // Procedural ripple displacement
//                     float2 uv = input.uv;
//                     float time = _TimeSeed * speed;

//                     float2 wave1Dir = normalize(float2(1, 1));
//                     float2 wave2Dir = normalize(float2(-1, 1));

//                     float wave1 = sin(dot(uv, wave1Dir) * frequency + time * 1.41421356237 + _RippleOffset) + sin(frequency + time * 0.173205080757 + _RippleOffset);
//                     float wave2 = sin(dot(uv, wave2Dir) * frequency + time * 1.61803398875 + _RippleOffset) + sin(frequency + time * 0.223606797750 + _RippleOffset);

//                     float2 displacement = amplitude * (wave1 * wave1Dir + wave2 * wave2Dir) / input.screenPos.w * depthScalar;

//                     // Apply displacement to UVs
//                     float2 displacedUV = screenUV + displacement;

//                     // Sample texture with displaced UVs
//                     half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

//                     // Apply base color tint
//                     color.rgb = lerp(color.rgb, _BaseColor.rgb, 0.5);

//                     return half4(color.rgb, 1);
//                 }
//                 else // Mode 5.5: Nonlinear _WaterDepthTexture
//                 {
//                     float scaledWaterDepth = waterDepth * 100.0;
//                     return half4(scaledWaterDepth, scaledWaterDepth, scaledWaterDepth, 1);
//                 }
//             }
//             ENDHLSL
//         }
//     }
// }

// Shader "Unlit/URPWaterOpaque"
// {
//     Properties
//     {
//         _BaseColor ("Base Color", Color) = (0.25, 0.5, 0.75, 1)
//         _MainTex ("Texture", 2D) = "white" {}
//         _WaterDepthTexture ("Water Depth Texture", 2D) = "white" {} // Added for reflected depth
//         _RippleSpeed ("Ripple Speed", Range(0, 1)) = 0.5
//         _RippleAmplitude ("Ripple Amplitude", Range(0, 1)) = 0.5
//         _RippleFrequency ("Ripple Frequency", Range(0, 1)) = 0.5
//         _RippleOffset ("Ripple Offset", Range(0, 1)) = 0.5
//         _TimeSeed ("Time Seed", Float) = 0.0
//         _DepthThreshold ("Depth Threshold", Float) = 2.0
//         _DepthTolerance ("Depth Tolerance", Float) = 0.01
//         _DebugDepthScalar ("Debug Depth Mode", Range(0, 5)) = 0 // 0 = nonlinear depth, 1 = linear depth, 2 = depth difference, 3 = nonlinear fragment, 4 = linear fragment, 5 = water effect
//     }
//     SubShader
//     {
//         Tags
//         {
//             "RenderType" = "Opaque"
//             "Queue" = "Geometry"
//             "RenderPipeline" = "UniversalPipeline"
//         }
//         LOD 100

//         Pass
//         {
//             Name "WaterOpaque"
//             ZWrite On
//             ZTest LEqual

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
//                 float _RippleSpeed;
//                 float _RippleAmplitude;
//                 float _RippleFrequency;
//                 float _RippleOffset;
//                 float _TimeSeed;
//                 float _DepthThreshold;
//                 float _DepthTolerance;
//                 float _DebugDepthScalar;
//                 float4 _MainTex_TexelSize;
//                 float4 _WaterDepthTexture_TexelSize; // Added
//             CBUFFER_END

//             TEXTURE2D(_MainTex);
//             SAMPLER(sampler_MainTex);
//             TEXTURE2D(_WaterDepthTexture); // Added
//             SAMPLER(sampler_WaterDepthTexture); // Added

//             // Internal scalars for adjusting normalized inputs
//             #define RIPPLE_SPEED_SCALE 20.0
//             #define RIPPLE_AMPLITUDE_SCALE 0.5
//             #define RIPPLE_FREQUENCY_SCALE 250.0
//             #define RIPPLE_FREQUENCY_OFFSET 1.0

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

//                 // Sample depths for debug modes
//                 float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
//                 float waterDepth = SAMPLE_TEXTURE2D(_WaterDepthTexture, sampler_WaterDepthTexture, screenUV).r; // Added
//                 float fragmentDepth = input.screenPos.z / input.screenPos.w;

//                 // Convert depths to linear eye-space
//                 float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
//                 float waterDepthLinear = LinearEyeDepth(waterDepth, _ZBufferParams); // Added
//                 float fragmentDepthLinear = LinearEyeDepth(fragmentDepth, _ZBufferParams);

//                 // Calculate depth differences
//                 float depthDifference = abs(sampledDepthLinear - fragmentDepthLinear);
//                 float waterDepthDifference = abs(waterDepthLinear - fragmentDepthLinear); // Added

//                 // // Debug modes
//                 // if (_DebugDepthScalar < 0.5) // Mode 0: Nonlinear depth (_CameraDepthTexture)
//                 // {
//                 //     float scaledDepth = sampledDepth * 100.0;
//                 //     return half4(scaledDepth, scaledDepth, scaledDepth, 1);
//                 // }
//                 // else if (_DebugDepthScalar < 1.5) // Mode 1: Linear depth (_CameraDepthTexture)
//                 // {
//                 //     float scaledLinearDepth = sampledDepthLinear / 10.0;
//                 //     return half4(scaledLinearDepth, scaledLinearDepth, scaledLinearDepth, 1);
//                 // }
//                 // else if (_DebugDepthScalar < 2.5) // Mode 2: Depth difference (_CameraDepthTexture)
//                 // {
//                 //     return half4(depthDifference, depthDifference, depthDifference, 1);
//                 // }
//                 // else if (_DebugDepthScalar < 3.5) // Mode 3: Nonlinear fragment depth
//                 // {
//                 //     float scaledFragmentDepth = fragmentDepth * 100.0;
//                 //     return half4(scaledFragmentDepth, scaledFragmentDepth, scaledFragmentDepth, 1);
//                 // }
//                 // else if (_DebugDepthScalar < 4.5) // Mode 4: Linear fragment depth
//                 // {
//                 //     float scaledFragmentLinear = fragmentDepthLinear / 10.0;
//                 //     return half4(scaledFragmentLinear, scaledFragmentLinear, scaledFragmentLinear, 1);
//                 // }
//                 // else // Mode 5: Water effect using _WaterDepthTexture
//                 // {
//                 //     // Normalize and scale inputs
//                 //     float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
//                 //     float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
//                 //     float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE + RIPPLE_FREQUENCY_OFFSET;

//                 //     // Calculate depth scalar using _WaterDepthTexture
//                 //     float normalizedDepth = waterDepthDifference / _DepthThreshold;
//                 //     float depthScalar = normalizedDepth * normalizedDepth;
//                 //     depthScalar = min(depthScalar, 1.0);

//                 //     // Procedural ripple displacement
//                 //     float2 uv = input.uv;
//                 //     float time = _TimeSeed * speed;

//                 //     float2 wave1Dir = normalize(float2(1, 1));
//                 //     float2 wave2Dir = normalize(float2(-1, 1));

//                 //     float wave1 = sin(dot(uv, wave1Dir) * frequency + time * 1.41421356237 + _RippleOffset) + sin(frequency + time * 0.173205080757 + _RippleOffset);
//                 //     float wave2 = sin(dot(uv, wave2Dir) * frequency + time * 1.61803398875 + _RippleOffset) + sin(frequency + time * 0.223606797750 + _RippleOffset);

//                 //     float2 displacement = amplitude * (wave1 * wave1Dir + wave2 * wave2Dir) / input.screenPos.w * depthScalar;

//                 //     // Apply displacement to UVs
//                 //     float2 displacedUV = screenUV + displacement;

//                 //     // Sample texture with displaced UVs
//                 //     half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

//                 //     // Apply base color tint
//                 //     color.rgb = lerp(color.rgb, _BaseColor.rgb, 0.5);

//                 //     return half4(color.rgb, 1);
//                 // }

// //else if (_DebugDepthScalar < 5.5) // Mode 5.5: Nonlinear _WaterDepthTexture
// {
//     float scaledWaterDepth = waterDepth * 100.0;
//     return half4(scaledWaterDepth, scaledWaterDepth, scaledWaterDepth, 1);
// }
//             }
//             ENDHLSL
//         }
//     }
// }

// Shader "Unlit/URPWaterOpaque"
// {
//     Properties
//     {
//         _BaseColor ("Base Color", Color) = (0.25, 0.5, 0.75, 1)
//         _MainTex ("Texture", 2D) = "white" {}
//         _RippleSpeed ("Ripple Speed", Range(0, 1)) = 0.5
//         _RippleAmplitude ("Ripple Amplitude", Range(0, 1)) = 0.5
//         _RippleFrequency ("Ripple Frequency", Range(0, 1)) = 0.5
//         _RippleOffset ("Ripple Offset", Range(0, 1)) = 0.5
//         _TimeSeed ("Time Seed", Float) = 0.0
//         _DepthThreshold ("Depth Threshold", Float) = 2.0
//         _DepthTolerance ("Depth Tolerance", Float) = 0.01
//         _DebugDepthScalar ("Debug Depth Mode", Range(0, 5)) = 0 // 0 = nonlinear depth, 1 = linear depth, 2 = depth difference, 3 = nonlinear fragment, 4 = linear fragment, 5 = water effect
//     }
//     SubShader
//     {
//         Tags
//         {
//             "RenderType" = "Opaque"
//             "Queue" = "Geometry"
//             "RenderPipeline" = "UniversalPipeline"
//         }
//         LOD 100

//         Pass
//         {
//             Name "WaterOpaque"
//             ZWrite On
//             ZTest LEqual

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
//                 float _RippleSpeed;
//                 float _RippleAmplitude;
//                 float _RippleFrequency;
//                 float _RippleOffset;
//                 float _TimeSeed;
//                 float _DepthThreshold;
//                 float _DepthTolerance;
//                 float _DebugDepthScalar;
//                 float4 _MainTex_TexelSize;
//             CBUFFER_END

//             TEXTURE2D(_MainTex);
//             SAMPLER(sampler_MainTex);

//             // Internal scalars for adjusting normalized inputs
//             #define RIPPLE_SPEED_SCALE 20.0
//             #define RIPPLE_AMPLITUDE_SCALE 0.5
//             #define RIPPLE_FREQUENCY_SCALE 250.0
//             #define RIPPLE_FREQUENCY_OFFSET 1.0

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

//                 // Sample depth for debug modes
//                 float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
//                 float fragmentDepth = input.screenPos.z / input.screenPos.w;

//                 // Convert depths to linear eye-space
//                 float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
//                 float fragmentDepthLinear = LinearEyeDepth(fragmentDepth, _ZBufferParams);

//                 // Calculate depth difference
//                 float depthDifference = abs(sampledDepthLinear - fragmentDepthLinear);

//                 // // Debug modes
//                 // if (_DebugDepthScalar < 0.5) // Mode 0: Scaled nonlinear depth
//                 // {
//                 //     float scaledDepth = sampledDepth * 100.0; // Adjust scaling as needed
//                 //     return half4(scaledDepth, scaledDepth, scaledDepth, 1);
//                 // }
//                 // else if (_DebugDepthScalar < 1.5) // Mode 1: Scaled linear depth
//                 // {
//                 //     float scaledLinearDepth = sampledDepthLinear / 10.0; // Adjust 10.0 based on scene depth
//                 //     return half4(scaledLinearDepth, scaledLinearDepth, scaledLinearDepth, 1);
//                 // }
//                 // else if (_DebugDepthScalar < 2.5) // Mode 2: Depth difference
//                 // {
//                 //     return half4(depthDifference, depthDifference, depthDifference, 1);
//                 // }
//                 // else if (_DebugDepthScalar < 3.5) // Mode 3: Nonlinear fragment depth
//                 // {
//                 //     float scaledFragmentDepth = fragmentDepth * 100.0; // Match mode 0 scaling
//                 //     return half4(scaledFragmentDepth, scaledFragmentDepth, scaledFragmentDepth, 1);
//                 // }
//                 // else if (_DebugDepthScalar < 4.5) // Mode 4: Linear fragment depth
//                 // {
//                 //     float scaledFragmentLinear = fragmentDepthLinear / 10.0; // Match mode 1 scaling
//                 //     return half4(scaledFragmentLinear, scaledFragmentLinear, scaledFragmentLinear, 1);
//                 // }
//                 // else // Mode 5: Water effect
//                 {
//                     // Normalize and scale inputs
//                     float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
//                     float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
//                     float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE + RIPPLE_FREQUENCY_OFFSET;

//                     // Calculate depth scalar
//                     float depthScalar;
//                     if (_DebugDepthScalar < 5.0) // Use depth difference (original behavior)
//                     {
//                         float normalizedDepth = depthDifference / _DepthThreshold;
//                         depthScalar = normalizedDepth * normalizedDepth; // Square for slow initial rise
//                         depthScalar = min(depthScalar, 1.0);
//                     }
//                     else // Bypass depth difference for testing (use fixed depthScalar)
//                     {
//                         depthScalar = 1.0; // No depth-based scaling
//                     }

//                     // Procedural ripple displacement
//                     float2 uv = input.uv;
//                     float time = _TimeSeed * speed;

//                     float2 wave1Dir = normalize(float2(1, 1));
//                     float2 wave2Dir = normalize(float2(-1, 1));

//                     float wave1 = sin(dot(uv, wave1Dir) * frequency + time * 1.41421356237 + _RippleOffset) + sin(frequency + time * 0.173205080757 + _RippleOffset);
//                     float wave2 = sin(dot(uv, wave2Dir) * frequency + time * 1.61803398875 + _RippleOffset) + sin(frequency + time * 0.223606797750 + _RippleOffset);

//                     float2 displacement = amplitude * (wave1 * wave1Dir + wave2 * wave2Dir) / input.screenPos.w * depthScalar;

//                     // Apply displacement to UVs
//                     float2 displacedUV = screenUV + displacement;

//                     // Sample texture with displaced UVs
//                     half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

//                     // Apply base color tint
//                     color.rgb = lerp(color.rgb, _BaseColor.rgb, 0.5);

//                     return half4(color.rgb, 1);
//                 }
//             }
//             ENDHLSL
//         }
//     }
// }

// Shader "Unlit/URPWaterOpaque"
// {
//     Properties
//     {
//         _BaseColor ("Base Color", Color) = (0.25, 0.5, 0.75, 1)
//         _MainTex ("Texture", 2D) = "white" {}
//         _RippleSpeed ("Ripple Speed", Range(0, 1)) = 0.5
//         _RippleAmplitude ("Ripple Amplitude", Range(0, 1)) = 0.5
//         _RippleFrequency ("Ripple Frequency", Range(0, 1)) = 0.5
//         _RippleOffset ("Ripple Offset", Range(0, 1)) = 0.5
//         _TimeSeed ("Time Seed", Float) = 0.0
//         _DepthThreshold ("Depth Threshold", Float) = 2.0
//         _DepthTolerance ("Depth Tolerance", Float) = 0.01
//         _DebugDepthScalar ("Debug Depth Mode", Range(0, 3)) = 0 // 0 = scaled nonlinear depth, 1 = scaled linear depth, 2 = depth difference, 3 = original water
//     }
//     SubShader
//     {
//         Tags
//         {
//             "RenderType" = "Opaque"
//             "Queue" = "Geometry"
//             "RenderPipeline" = "UniversalPipeline"
//         }
//         LOD 100

//         Pass
//         {
//             Name "WaterOpaque"
//             ZWrite On
//             ZTest LEqual

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
//                 float _RippleSpeed;
//                 float _RippleAmplitude;
//                 float _RippleFrequency;
//                 float _RippleOffset;
//                 float _TimeSeed;
//                 float _DepthThreshold;
//                 float _DepthTolerance;
//                 float _DebugDepthScalar;
//                 float4 _MainTex_TexelSize;
//             CBUFFER_END

//             TEXTURE2D(_MainTex);
//             SAMPLER(sampler_MainTex);

//             // Internal scalars for adjusting normalized inputs
//             #define RIPPLE_SPEED_SCALE 20.0
//             #define RIPPLE_AMPLITUDE_SCALE 0.5
//             #define RIPPLE_FREQUENCY_SCALE 250.0
//             #define RIPPLE_FREQUENCY_OFFSET 1.0

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

//                 // Sample depth for debug modes
//                 float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
//                 float fragmentDepth = input.screenPos.z / input.screenPos.w;

//                 // Convert depths to linear eye-space
//                 float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
//                 float fragmentDepthLinear = LinearEyeDepth(fragmentDepth, _ZBufferParams);

//                 // Calculate depth difference
//                 float depthDifference = abs(sampledDepthLinear - fragmentDepthLinear);

//                 // // Debug modes
//                 // if (_DebugDepthScalar < 0.5) // Mode 0: Scaled nonlinear depth
//                 // {
//                     // Scale nonlinear depth to make small values visible
//                     float scaledDepth = sampledDepth * 100.0; // Adjust scaling factor as needed
//                     return half4(scaledDepth, scaledDepth, scaledDepth, 1);
//                 // }
//                 // else 
//                 // if (_DebugDepthScalar < 1.5) // Mode 1: Scaled linear depth
//                 // {
//                 //     // Scale linear depth to fit scene depth range (e.g., 0–10 meters)
//                 //     float scaledLinearDepth = sampledDepthLinear / 10.0; // Adjust 10.0 based on scene depth
//                 //     return half4(scaledLinearDepth, scaledLinearDepth, scaledLinearDepth, 1);
//                 // }
//                 // else 
//                 // if (_DebugDepthScalar < 2.5) // Mode 2: Depth difference (your working version)
//                 // {
//                 //     return half4(depthDifference, depthDifference, depthDifference, 1);
//                 // }
//                 // else // Mode 3: Original water effect
//                 // {
//                 //     // Normalize and scale inputs
//                 //     float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
//                 //     float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
//                 //     float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE + RIPPLE_FREQUENCY_OFFSET;

//                 //     // Calculate depth scalar using squared normalized depth difference
//                 //     float normalizedDepth = depthDifference / _DepthThreshold;
//                 //     float depthScalar = normalizedDepth * normalizedDepth; // Square for slow initial rise
//                 //     depthScalar = min(depthScalar, 1.0);

//                 //     // Procedural ripple displacement
//                 //     float2 uv = input.uv;
//                 //     float time = _TimeSeed * speed;

//                 //     // Two intersecting sine waves with different angles and phases
//                 //     float2 wave1Dir = normalize(float2(1, 1));
//                 //     float2 wave2Dir = normalize(float2(-1, 1));

//                 //     float wave1 = sin(dot(uv, wave1Dir) * frequency + time * 1.41421356237 + _RippleOffset) + sin(frequency + time * 0.173205080757 + _RippleOffset);
//                 //     float wave2 = sin(dot(uv, wave2Dir) * frequency + time * 1.61803398875 + _RippleOffset) + sin(frequency + time * 0.223606797750 + _RippleOffset);

//                 //     // Combine waves for displacement, using depth scaling
//                 //     float2 displacement = amplitude * (wave1 * wave1Dir + wave2 * wave2Dir) / input.screenPos.w * depthScalar;

//                 //     // Apply displacement to UVs
//                 //     float2 displacedUV = screenUV + displacement;

//                 //     // Sample texture with displaced UVs
//                 //     half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

//                 //     // Apply base color tint
//                 //     color.rgb = lerp(color.rgb, _BaseColor.rgb, 0.5);

//                 //     return half4(color.rgb, 1);
//                 // }
//             }
//             ENDHLSL
//         }
//     }
// }

// Shader "Unlit/URPWaterOpaque"
// {
//     Properties
//     {
//         _BaseColor ("Base Color", Color) = (0.25, 0.5, 0.75, 1) // Blue-ish tint for water
//         _MainTex ("Texture", 2D) = "white" {}
//         _RippleSpeed ("Ripple Speed", Range(0, 1)) = 0.5
//         _RippleAmplitude ("Ripple Amplitude", Range(0, 1)) = 0.5
//         _RippleFrequency ("Ripple Frequency", Range(0, 1)) = 0.5
//         _RippleOffset ("Ripple Offset", Range(0, 1)) = 0.5
//         _TimeSeed ("Time Seed", Float) = 0.0
//         _DepthThreshold ("Depth Threshold", Float) = 2.0 // Depth at which depthScalar reaches 1
//         _DepthTolerance ("Depth Tolerance", Float) = 0.01 // Tolerance for depth test
//         _DebugDepthScalar ("Debug Depth Scalar", Range(0, 1)) = 0 // Set to 1 to visualize depthScalar
//     }
//     SubShader
//     {
//         Tags
//         {
//             "RenderType" = "Opaque"
//             "Queue" = "Geometry"
//             "RenderPipeline" = "UniversalPipeline"
//         }
//         LOD 100

//         Pass
//         {
//             Name "WaterOpaque"
//             ZWrite On

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
//                 float _RippleSpeed;
//                 float _RippleAmplitude;
//                 float _RippleFrequency;
//                 float _RippleOffset;
//                 float _TimeSeed;
//                 float _DepthThreshold;
//                 float _DepthTolerance;
//                 float _DebugDepthScalar;
//                 float4 _MainTex_TexelSize;
//             CBUFFER_END

//             TEXTURE2D(_MainTex);
//             SAMPLER(sampler_MainTex);

//             // Internal scalars for adjusting normalized inputs
//             #define RIPPLE_SPEED_SCALE 20.0
//             #define RIPPLE_AMPLITUDE_SCALE 0.5
//             #define RIPPLE_FREQUENCY_SCALE 250.0
//             #define RIPPLE_FREQUENCY_OFFSET 1.0

//             Varyings vert(Attributes input)
//             {
//                 Varyings output;
//                 output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
//                 output.uv = input.uv;
//                 output.screenPos = ComputeScreenPos(output.positionCS);
//                 return output;
//             }


//             //this shows some grey scale differences on the screen
//             half4 frag(Varyings input) : SV_Target
//             {
//                 float2 screenUV = input.screenPos.xy / input.screenPos.w;
                
//                 // Sample depth for depth scalar
//                 float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
//                 float fragmentDepth = input.screenPos.z / input.screenPos.w;

//                 // Convert depths to linear eye-space
//                 float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
//                 float fragmentDepthLinear = LinearEyeDepth(fragmentDepth, _ZBufferParams);

//                 // Calculate depth scalar using squared normalized depth difference
//                 float depthDifference = abs(sampledDepthLinear - fragmentDepthLinear);

//                 //return half4(depthDifference, depthDifference, depthDifference, 1);
//                 return half4(depthDifference, depthDifference, depthDifference, 1);
//             }

//             //but this only shows black
//             // half4 frag(Varyings input) : SV_Target
//             // {
//             //     float2 screenUV = input.screenPos.xy / input.screenPos.w;

//             //     // Sample the destination depth buffer
//             //     float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;

//             //     // Output depth in grayscale (nonlinear depth buffer value)
//             //     return half4(sampledDepth, sampledDepth, sampledDepth, 1);

//             //     // Optional: Linearize depth for better visualization (uncomment to use)
//             //     // float linearDepth = LinearEyeDepth(sampledDepth, _ZBufferParams);
//             //     // float scaledDepth = linearDepth / 10.0; // Scale for visibility (adjust 10.0 based on scene depth range)
//             //     // return half4(scaledDepth, scaledDepth, scaledDepth, 1);
//             // }
//            ENDHLSL
//         }
//     }
// }