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
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.25
        _Skybox ("Skybox", Cube) = "" {}
        _NormalScale ("Normal Scale", Range(0, 5)) = 2.0
        _FresnelPower ("Fresnel Exponent", Range(1, 40)) = 12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "WaterOpaque"
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

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
                float3 positionWS : TEXCOORD2;
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
                float _ReflectionStrength;
                float _NormalScale;
                float _FresnelPower;
            CBUFFER_END

            TEXTURE2D(_MainTex);            SAMPLER(sampler_MainTex);
            TEXTURECUBE(_Skybox);           SAMPLER(sampler_Skybox);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // ── Ripple Displacement (kept very close to your original) ──
                float speed     = _RippleSpeed * 10.0;
                float amplitude = _RippleAmplitude * 0.1;
                float frequency = _RippleFrequency * 10.0 + 1.0;
                float time      = _TimeSeed * speed;

                float2 uv = input.uv;

                float2 d1 = normalize(float2(1.0, 0.0));
                float2 d2 = normalize(float2(0.7071, 0.7071));
                float2 d3 = normalize(float2(0.0, 1.0));
                float2 d4 = normalize(float2(-0.7071, 0.7071));

                float s1 = dot(uv, d1) * frequency + time + _RippleOffset * 0.0;
                float s2 = dot(uv, d2) * frequency + time + _RippleOffset * 0.25;
                float s3 = dot(uv, d3) * frequency + time + _RippleOffset * 0.5;
                float s4 = dot(uv, d4) * frequency + time + _RippleOffset * 0.75;

                float wave =  sin(s1*frequency) + sin(s1*1.61803) - sin(s1*1.41421) +
                              sin(s2*frequency) + sin(s2*1.73205) - sin(s2*1.30902) +
                              sin(s3*frequency) + sin(s3*1.61803) - sin(s3*1.41421) +
                              sin(s4*frequency) + sin(s4*1.73205) - sin(s4*1.30902);

                float2 displacement = (amplitude * wave * float2(1.0, 1.0)) / input.screenPos.w;

                float2 displacedUV = screenUV + displacement;

                // ── Reflection (WebGL-safe version) ──
                float3 normalWS = normalize(input.normalWS);
                float3 perturb  = float3(displacement.x, 0.0, displacement.y) * _NormalScale;
                float3 reflNormal = normalize(normalWS + perturb);

                float3 viewDir = normalize(input.viewDirWS);
                float3 reflectDir = reflect(-viewDir, reflNormal);

                half4 reflectionColor = SAMPLE_TEXTURECUBE(_Skybox, sampler_Skybox, reflectDir);

                // Safer Fresnel for WebGL
                float cosTheta = saturate(dot(viewDir, reflNormal));
                float fresnel = 1.0 - cosTheta;
                fresnel = pow(fresnel, _FresnelPower);
                fresnel = min(fresnel, 0.95);                    // your cap

                float reflectionIntensity = fresnel * _ReflectionStrength;

                // ── Final color ──
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

                color.rgb = lerp(color.rgb, _BaseColor.rgb, _BaseColor.a);

                // This lerp is now safe on WebGL
                color.rgb = lerp(color.rgb, reflectionColor.rgb, reflectionIntensity);

                return half4(color.rgb, 1.0);
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
