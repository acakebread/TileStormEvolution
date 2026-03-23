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
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.25
        _Skybox ("Skybox", Cube) = "" {}
        _NormalScale ("Normal Scale", Range(0, 5)) = 2.0
        _FresnelPower ("Fresnel Exponent - use 15–30 for stylized water (reflection mostly at very grazing angles)", Range(1, 40)) = 12 }

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
            #pragma unroll // Optimize for WebGL
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float4 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float3 viewDirWS : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _RippleSpeed;
                float _RippleAmplitude;
                float _RippleFrequency;
                float _RippleOffset;
                float _TimeSeed;
                float _DepthThreshold;
                float _ReflectionStrength;
                float _NormalScale;
                float _FresnelPower;
                float4x4 _ReflectionViewProjMatrix;
                float4 _MainTex_TexelSize;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURECUBE(_Skybox);
            SAMPLER(sampler_Skybox);

            // Internal scalars for adjusting normalized inputs
            #define RIPPLE_SPEED_SCALE 10.0
            #define RIPPLE_AMPLITUDE_SCALE 0.1
            #define RIPPLE_FREQUENCY_SCALE 10.0
            #define RIPPLE_FREQUENCY_OFFSET 1.0

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.positionWS = mul(unity_ObjectToWorld, input.positionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(output.positionWS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                
                // Normalize and scale inputs
                float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
                float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
                float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE;
                float time = _TimeSeed * speed;

                // Procedural ripple displacement for texture sampling
                float2 uv = input.uv;

                // Four waves at 45-degree angles
                float2 wave1Dir = normalize(float2(cos(0.0), sin(0.0))); // 0°
                float2 wave2Dir = normalize(float2(cos(0.785398), sin(0.785398))); // 45°
                float2 wave3Dir = normalize(float2(cos(1.570796), sin(1.570796))); // 90°
                float2 wave4Dir = normalize(float2(cos(2.356194), sin(2.356194))); // 135°

                // Seeds with UV and phase offsets
                float seed1 = dot(uv, wave1Dir) * frequency + time + _RippleOffset * 0.0;
                float seed2 = dot(uv, wave2Dir) * frequency + time + _RippleOffset * 0.25;
                float seed3 = dot(uv, wave3Dir) * frequency + time + _RippleOffset * 0.5;
                float seed4 = dot(uv, wave4Dir) * frequency + time + _RippleOffset * 0.75;

                // Combine multiple sine terms for noise
                float wave1 = (sin(seed1 * frequency) + sin(seed1 * 1.61803398875) - sin(seed1 * 1.41421356237));
                float wave2 = (sin(seed2 * frequency) + sin(seed2 * 1.73205080757) - sin(seed2 * 1.30901699437));
                float wave3 = (sin(seed3 * frequency) + sin(seed3 * 1.61803398875) - sin(seed3 * 1.41421356237));
                float wave4 = (sin(seed4 * frequency) + sin(seed4 * 1.73205080757) - sin(seed4 * 1.30901699437));

                // Combine waves for displacement
                float2 displacement = amplitude * (
                    wave1 * wave1Dir +
                    wave2 * wave2Dir +
                    wave3 * wave3Dir +
                    wave4 * wave4Dir
                ) / input.screenPos.w;

                // Apply displacement to UVs for texture sampling
                float2 displacedUV = screenUV + displacement;

                // Sample the depth buffer at displaced UV
                float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, displacedUV).r;
                float fragmentDepth = input.screenPos.z / input.screenPos.w;

                // Convert depths to linear eye-space for comparison
                float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
                float fragmentDepthLinear = LinearEyeDepth(fragmentDepth, _ZBufferParams);

                // Compute normal for reflections
                float3 normalWS = normalize(input.normalWS); // Base normal
                float3 perturbNormal = float3(displacement.x, 0.0, displacement.y) * _NormalScale * 1.0;
                float3 reflectionNormal = normalize(normalWS + perturbNormal); // Add and normalize

                // Compute reflection vector
                float3 viewDirWS = normalize(input.viewDirWS);
                float3 reflectDir = reflect(-viewDirWS, reflectionNormal);

                // Sample skybox with reflection vector
                half4 reflectionColor = SAMPLE_TEXTURECUBE(_Skybox, sampler_Skybox, reflectDir);

                float cosTheta = saturate(dot(viewDirWS, reflectionNormal));
                float fresnelTerm = pow(1.0 - cosTheta, _FresnelPower);
                float reflectionIntensity = fresnelTerm * _ReflectionStrength;

                #if !defined(SHADER_API_GLES) && !defined(SHADER_API_GLES3) // Non-WebGL
                // Handle depth test for texture sampling
                if (sampledDepthLinear < fragmentDepthLinear)
                {
                    // Sample at undisplaced UV as fallback
                    half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);
                    color.rgb = lerp(color.rgb, _BaseColor.rgb, _BaseColor.a);
                    // Blend reflection with fallback color
                    color.rgb = lerp(color.rgb, reflectionColor.rgb, reflectionIntensity);
                    return half4(color.rgb, 1);
                }
                #endif

                // Sample texture with displaced UVs
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

                // Blend with base color for water tint
                color.rgb = lerp(color.rgb, _BaseColor.rgb, _BaseColor.a);

                // Blend reflection with water color
                color.rgb = lerp(color.rgb, reflectionColor.rgb, reflectionIntensity);

                return half4(color.rgb, 1);
            }
            ENDHLSL
        }
    }
}

// DEBUG TEST


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
//         _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.25
//         _Skybox ("Skybox", Cube) = "" {}
//         _NormalScale ("Normal Scale", Range(0, 5)) = 2.0 // Increased range for stronger perturbation
//         _FresnelPower ("Fresnel Power", Range(1, 5)) = 2.0 // Controls Fresnel effect strength
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
//                 float3 normalOS : NORMAL;
//             };

//             struct Varyings
//             {
//                 float4 positionCS : SV_POSITION;
//                 float2 uv : TEXCOORD0;
//                 float4 screenPos : TEXCOORD1;
//                 float4 positionWS : TEXCOORD2;
//                 float3 normalWS : TEXCOORD3;
//                 float3 viewDirWS : TEXCOORD4;
//             };

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _BaseColor;
//                 float _RippleSpeed;
//                 float _RippleAmplitude;
//                 float _RippleFrequency;
//                 float _RippleOffset;
//                 float _TimeSeed;
//                 float _DepthThreshold;
//                 float _ReflectionStrength;
//                 float _NormalScale;
//                 float _FresnelPower;
//                 float4x4 _ReflectionViewProjMatrix;
//                 float4 _MainTex_TexelSize;
//             CBUFFER_END

//             TEXTURE2D(_MainTex);
//             SAMPLER(sampler_MainTex);
//             TEXTURECUBE(_Skybox);
//             SAMPLER(sampler_Skybox);

//             // Internal scalars for adjusting normalized inputs
//             #define RIPPLE_SPEED_SCALE 10.0
//             #define RIPPLE_AMPLITUDE_SCALE 0.1
//             #define RIPPLE_FREQUENCY_SCALE 10.0
//             #define RIPPLE_FREQUENCY_OFFSET 1.0

//             Varyings vert(Attributes input)
//             {
//                 Varyings output;
//                 output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
//                 output.uv = input.uv;
//                 output.screenPos = ComputeScreenPos(output.positionCS);
//                 output.positionWS = mul(unity_ObjectToWorld, input.positionOS);
//                 output.normalWS = TransformObjectToWorldNormal(input.normalOS);
//                 output.viewDirWS = GetWorldSpaceViewDir(output.positionWS.xyz);
//                 return output;
//             }

//             float2 RotatingSineEllipse(float2 uv, float t, float k, float scale)
//             {
//                 // Combine UVs for phase variation
//                 float phase = (uv.x + uv.y) * 3.14159 + t; // Add UVs for spatial variation
//                 float2 v = float2(sin(phase), cos(phase)) * scale; // Circular motion

//                 // Rotation angle based on frequency and UV
//                 float angle = k * (uv.x * uv.y + t); // Unique rotation per UV
//                 float c = cos(angle);
//                 float s = sin(angle);

//                 // Rotate point around origin
//                 return float2(v.x * c - v.y * s, v.x * s + v.y * c);
//             }

//             half4 frag(Varyings input) : SV_Target
//             {
//                 // Normalize and scale inputs
//                 float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
//                 float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
//                 float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE;
//                 float time = _TimeSeed * speed;

//                 // Procedural ripple displacement
//                 float2 uv = input.uv;

//                 float seed1 = sin(uv.x) * 8;
//                 float seed2 = sin(uv.y) * 8;

//                 float2 wave2Dx = RotatingSineEllipse(seed1, frequency, 0, amplitude);
//                 float2 wave2Dy = RotatingSineEllipse(seed2, frequency, 0, amplitude);

//                 float2 displacement = float2(wave2Dx[0], wave2Dy[0]) / input.screenPos.w;
//                 displacement.y *= _ScreenParams.x / _ScreenParams.y; // Aspect ratio

//                 // Apply displacement to UVs
//                 float2 screenUV = input.screenPos.xy / input.screenPos.w;
//                 float2 displacedUV = screenUV + displacement;

//                 // Sample the depth buffer at displaced UV
//                 float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, displacedUV).r;
//                 float fragmentDepth = input.screenPos.z / input.screenPos.w;

//                 // Convert depths to linear eye-space for comparison
//                 float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
//                 float fragmentDepthLinear = LinearEyeDepth(fragmentDepth, _ZBufferParams);

//                 // Compute normal for reflections
//                 float3 normalWS = normalize(input.normalWS); // Base normal
//                 float3 perturbNormal = float3(displacement.x, 0.0, displacement.y) * _NormalScale * 1.0;
//                 float3 reflectionNormal = normalize(normalWS + perturbNormal); // Add and normalize

//                 // Compute reflection vector
//                 float3 viewDirWS = normalize(input.viewDirWS);
//                 float3 reflectDir = reflect(-viewDirWS, reflectionNormal);

//                 // Sample skybox with reflection vector
//                 half4 reflectionColor = SAMPLE_TEXTURECUBE(_Skybox, sampler_Skybox, reflectDir);

//                 // Compute reflection intensity using Fresnel effect
//                 float fresnelTerm = pow(1.0 - saturate(dot(viewDirWS, reflectionNormal)), _FresnelPower);
//                 float reflectionIntensity = fresnelTerm * _ReflectionStrength;

//                 // Handle depth test for texture sampling
//                 if (sampledDepthLinear < fragmentDepthLinear)
//                 {
//                     // Sample at undisplaced UV as fallback
//                     half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);
//                     color.rgb = lerp(color.rgb, _BaseColor.rgb, _BaseColor.a);
//                     // Blend reflection with fallback color
//                     color.rgb = lerp(color.rgb, reflectionColor.rgb, reflectionIntensity);
//                     return half4(color.rgb, 1);
//                 }

//                 // Sample texture with displaced UVs
//                 half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

//                 // Blend with base color for water tint
//                 color.rgb = lerp(color.rgb, _BaseColor.rgb, _BaseColor.a);

//                 // Blend reflection with water color
//                 color.rgb = lerp(color.rgb, reflectionColor.rgb, reflectionIntensity);

//                 return half4(color.rgb, 1);
//             }
//             ENDHLSL
//         }
//     }
// }
