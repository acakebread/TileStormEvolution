Shader "Unlit/URPWaterOpaque"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.25, 0.5, 0.75, 1)
        _MainTex ("Texture", 2D) = "white" {}
        _RippleSpeed ("Ripple Speed", Range(0, 1)) = 0.5
        _RippleAmplitude ("Ripple Amplitude", Range(0, 1)) = 0.5
        _RippleFrequency ("Ripple Frequency", Range(0, 1)) = 0.5
        _RippleOffset ("Ripple Offset", Range(0, 1)) = 0.5
        _TimeSeed ("Time Seed", Float) = 0.0
        _DepthThreshold ("Depth Threshold", Float) = 5.0
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
                float4 positionWS : TEXCOORD2; // World-space position
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _RippleSpeed;
                float _RippleAmplitude;
                float _RippleFrequency;
                float _RippleOffset;
                float _TimeSeed;
                float _DepthThreshold;
                float4x4 _ReflectionViewProjMatrix; // Reflection camera's view-projection matrix
                float4 _MainTex_TexelSize;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

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
                output.positionWS = mul(unity_ObjectToWorld, input.positionOS); // Compute world-space position
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // Sample depth from main camera's depth texture
                float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;

                //return half4(0.05f/sampledDepth, sampledDepth * 0.1, sampledDepth * 0.1, 1);


                // Compute screen-space UVs for the reflection camera
                float4 worldPos = input.positionWS;
                float4 clipPosReflected = mul(_ReflectionViewProjMatrix, worldPos);
                float2 reflectedUV = (clipPosReflected.xy / clipPosReflected.w) * 0.5 + 0.5;

                // // // Clamp UVs and check validity
                // reflectedUV = clamp(reflectedUV, 0.0, 1.0);
                // float uvValid = all(reflectedUV >= 0.0 && reflectedUV <= 1.0) ? 1.0 : 0.0;

                // // // Sample depth texture using reflected UVs
                // float waterSampledDepth = uvValid ? SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, reflectedUV).r : sampledDepth;

                // Sample depth texture using reflected UVs
                float waterSampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
                //return half4(0.05f/waterSampledDepth * 0.1, waterSampledDepth * 0.1, waterSampledDepth * 0.1, 1);

                // Convert depths to linear eye-space
                float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
                float waterSampledDepthLinear = LinearEyeDepth(waterSampledDepth, _ZBufferParams);
                float fragmentDepthLinear = LinearEyeDepth(input.positionCS.z / input.positionCS.w, _ZBufferParams);

                // Calculate depth difference
                float depthDifference = abs(waterSampledDepthLinear - fragmentDepthLinear);

                // Debug: Visualize depth difference
                //return half4(depthDifference * 0.1, depthDifference * 0.1, depthDifference * 0.1, 1); // Scale for visibility

                // Debug: Visualize waterSampledDepth
                //return half4(waterSampledDepthLinear * 0.1, waterSampledDepthLinear * 0.1, waterSampledDepthLinear * 0.1, 1);

                // Debug: Visualize reflected UVs
                //return half4(reflectedUV.x, reflectedUV.y, 0, 1);

                // Debug: Visualize UV validity
                //return half4(uvValid, uvValid, uvValid, 1);

                // Normalize and scale inputs
                float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
                float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
                float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE + RIPPLE_FREQUENCY_OFFSET;

                // Calculate depth scalar
                float normalizedDepth = depthDifference / _DepthThreshold;
                float depthScalar = normalizedDepth * normalizedDepth;
                depthScalar = min(depthScalar, 1.0);

                // Debug: Visualize depth scalar
                //return half4(depthScalar, depthScalar, depthScalar, 1);

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
//         _RippleSpeed ("Ripple Speed", Range(0, 1)) = 0.5
//         _RippleAmplitude ("Ripple Amplitude", Range(0, 1)) = 0.5
//         _RippleFrequency ("Ripple Frequency", Range(0, 1)) = 0.5
//         _RippleOffset ("Ripple Offset", Range(0, 1)) = 0.5
//         _TimeSeed ("Time Seed", Float) = 0.0
//         _DepthThreshold ("Depth Threshold", Float) = 2.0
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
//                 float4 positionWS : TEXCOORD2; // World-space position
//             };

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _BaseColor;
//                 float _RippleSpeed;
//                 float _RippleAmplitude;
//                 float _RippleFrequency;
//                 float _RippleOffset;
//                 float _TimeSeed;
//                 float _DepthThreshold;
//                 float4x4 _ReflectionViewProjMatrix; // Reflection camera's view-projection matrix
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
//                 output.positionWS = mul(unity_ObjectToWorld, input.positionOS); // Compute world-space position
//                 return output;
//             }

//             half4 frag(Varyings input) : SV_Target
//             {
//                 float2 screenUV = input.screenPos.xy / input.screenPos.w;

//                 // Sample depth from main camera's depth texture
//                 float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;

//                 // Compute screen-space UVs for the reflection camera
//                 float4 worldPos = input.positionWS;
//                 float4 clipPosReflected = mul(_ReflectionViewProjMatrix, worldPos);
//                 float2 reflectedUV = (clipPosReflected.xy / clipPosReflected.w) * 0.5 + 0.5; // Convert to [0,1] UV space

//                 // Sample depth texture using reflected UVs
//                 float waterSampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, reflectedUV).r;

//                 // Convert depths to linear eye-space
//                 float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
//                 float waterSampledDepthLinear = LinearEyeDepth(waterSampledDepth, _ZBufferParams);
//                 float fragmentDepthLinear = LinearEyeDepth(input.positionCS.z / input.positionCS.w, _ZBufferParams);

//                 // Calculate depth difference
//                 float depthDifference = abs(waterSampledDepthLinear - fragmentDepthLinear);

//                 return half4(depthDifference, depthDifference, depthDifference, 1);

//                 // Normalize and scale inputs
//                 float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
//                 float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
//                 float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE + RIPPLE_FREQUENCY_OFFSET;

//                 // Calculate depth scalar
//                 float normalizedDepth = depthDifference / _DepthThreshold;
//                 float depthScalar = normalizedDepth * normalizedDepth;
//                 depthScalar = min(depthScalar, 1.0);
//                 return half4(depthScalar, depthScalar, depthScalar, 1);

//                 // Procedural ripple displacement
//                 float2 uv = input.uv;
//                 float time = _TimeSeed * speed;

//                 float2 wave1Dir = normalize(float2(1, 1));
//                 float2 wave2Dir = normalize(float2(-1, 1));

//                 float wave1 = sin(dot(uv, wave1Dir) * frequency + time * 1.41421356237 + _RippleOffset) + sin(frequency + time * 0.173205080757 + _RippleOffset);
//                 float wave2 = sin(dot(uv, wave2Dir) * frequency + time * 1.61803398875 + _RippleOffset) + sin(frequency + time * 0.223606797750 + _RippleOffset);

//                 float2 displacement = amplitude * (wave1 * wave1Dir + wave2 * wave2Dir) / input.screenPos.w * depthScalar;

//                 // Apply displacement to UVs
//                 float2 displacedUV = screenUV + displacement;

//                 // Sample texture with displaced UVs
//                 half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

//                 // Apply base color tint
//                 color.rgb = lerp(color.rgb, _BaseColor.rgb, 0.5);

//                 return half4(color.rgb, 1);
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
//         _DepthThreshold ("Depth Threshold", Float) = 5.0
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

//                 // Sample depths
//                 float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
//                 float waterSampledDepth = sampledDepth;//Need to reflect cam pos in 
//                 float fragmentDepth = input.screenPos.z / input.screenPos.w;

//                 // Convert depths to linear eye-space
//                 float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
//                 float waterSampledDepthLinear = LinearEyeDepth(waterSampledDepth, _ZBufferParams);
//                 float fragmentDepthLinear = LinearEyeDepth(fragmentDepth, _ZBufferParams);

//                 // Calculate depth difference (using _WaterDepthTexture)
//                 float depthDifference = abs(waterSampledDepthLinear - fragmentDepthLinear);
//                 //return half4(depthDifference, depthDifference, depthDifference, 1);

//                 // Normalize and scale inputs
//                 float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
//                 float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
//                 float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE + RIPPLE_FREQUENCY_OFFSET;

//                 // Calculate depth scalar using _WaterDepthTexture
//                 float normalizedDepth = depthDifference / _DepthThreshold;
//                 float depthScalar = normalizedDepth * normalizedDepth;
//                 depthScalar = min(depthScalar, 1.0);
//                 return half4(depthScalar, depthScalar, depthScalar, 1);

//                 // Procedural ripple displacement
//                 float2 uv = input.uv;
//                 float time = _TimeSeed * speed;

//                 float2 wave1Dir = normalize(float2(1, 1));
//                 float2 wave2Dir = normalize(float2(-1, 1));

//                 float wave1 = sin(dot(uv, wave1Dir) * frequency + time * 1.41421356237 + _RippleOffset) + sin(frequency + time * 0.173205080757 + _RippleOffset);
//                 float wave2 = sin(dot(uv, wave2Dir) * frequency + time * 1.61803398875 + _RippleOffset) + sin(frequency + time * 0.223606797750 + _RippleOffset);

//                 float2 displacement = amplitude * (wave1 * wave1Dir + wave2 * wave2Dir) / input.screenPos.w * depthScalar;

//                 // Apply displacement to UVs
//                 float2 displacedUV = screenUV + displacement;

//                 // Sample texture with displaced UVs
//                 half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

//                 // Apply base color tint
//                 color.rgb = lerp(color.rgb, _BaseColor.rgb, 0.5);

//                 return half4(color.rgb, 1);
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
//                 float4 positionWS : TEXCOORD2; // World-space position
//             };

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _BaseColor;
//                 float _RippleSpeed;
//                 float _RippleAmplitude;
//                 float _RippleFrequency;
//                 float _RippleOffset;
//                 float _TimeSeed;
//                 float _DepthThreshold;
//                 float4x4 _ReflectionViewProjMatrix; // Reflection camera's view-projection matrix
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
//                 output.positionWS = mul(unity_ObjectToWorld, input.positionOS); // Compute world-space position
//                 return output;
//             }

//             half4 frag(Varyings input) : SV_Target
//             {
//                 float2 screenUV = input.screenPos.xy / input.screenPos.w;

//                 // Sample depth from main camera's depth texture
//                 float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;

//                 // Compute screen-space UVs for the reflection camera
//                 float4 worldPos = input.positionWS;
//                 float4 clipPosReflected = mul(_ReflectionViewProjMatrix, worldPos);
//                 float2 reflectedUV = (clipPosReflected.xy / clipPosReflected.w) * 0.5 + 0.5; // Convert to [0,1] UV space

//                 // Sample depth texture using reflected UVs
//                 float waterSampledDepth = sampledDepth;//SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, reflectedUV).r;

//                 // Convert depths to linear eye-space
//                 float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
//                 float waterSampledDepthLinear = LinearEyeDepth(waterSampledDepth, _ZBufferParams);
//                 float fragmentDepthLinear = LinearEyeDepth(input.positionCS.z / input.positionCS.w, _ZBufferParams);

//                 // Calculate depth difference
//                 float depthDifference = abs(waterSampledDepthLinear - fragmentDepthLinear);

//                 return half4(depthDifference, depthDifference, depthDifference, 1);

//                 // Normalize and scale inputs
//                 float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
//                 float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
//                 float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE + RIPPLE_FREQUENCY_OFFSET;

//                 // Calculate depth scalar
//                 float normalizedDepth = depthDifference / _DepthThreshold;
//                 float depthScalar = normalizedDepth * normalizedDepth;
//                 depthScalar = min(depthScalar, 1.0);

//                 // Procedural ripple displacement
//                 float2 uv = input.uv;
//                 float time = _TimeSeed * speed;

//                 float2 wave1Dir = normalize(float2(1, 1));
//                 float2 wave2Dir = normalize(float2(-1, 1));

//                 float wave1 = sin(dot(uv, wave1Dir) * frequency + time * 1.41421356237 + _RippleOffset) + sin(frequency + time * 0.173205080757 + _RippleOffset);
//                 float wave2 = sin(dot(uv, wave2Dir) * frequency + time * 1.61803398875 + _RippleOffset) + sin(frequency + time * 0.223606797750 + _RippleOffset);

//                 float2 displacement = amplitude * (wave1 * wave1Dir + wave2 * wave2Dir) / input.screenPos.w * depthScalar;

//                 // Apply displacement to UVs
//                 float2 displacedUV = screenUV + displacement;

//                 // Sample texture with displaced UVs
//                 half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

//                 // Apply base color tint
//                 color.rgb = lerp(color.rgb, _BaseColor.rgb, 0.5);

//                 return half4(color.rgb, 1);
//             }
//             ENDHLSL
//         }
//     }
// }