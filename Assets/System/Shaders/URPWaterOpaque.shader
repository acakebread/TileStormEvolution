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
        _FresnelSharpness ("Fresnel Sharpness - Higher = reflection only at extreme grazing angles (recommended 8–30)", Range(1, 40)) = 12
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
                float _FresnelSharpness;
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
                
                // Normalize and scale inputs (your ripple code unchanged)
                float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
                float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
                float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE;
                float time = _TimeSeed * speed;

                float2 uv = input.uv;

                float2 wave1Dir = normalize(float2(cos(0.0), sin(0.0)));
                float2 wave2Dir = normalize(float2(cos(0.785398), sin(0.785398)));
                float2 wave3Dir = normalize(float2(cos(1.570796), sin(1.570796)));
                float2 wave4Dir = normalize(float2(cos(2.356194), sin(2.356194)));

                float seed1 = dot(uv, wave1Dir) * frequency + time + _RippleOffset * 0.0;
                float seed2 = dot(uv, wave2Dir) * frequency + time + _RippleOffset * 0.25;
                float seed3 = dot(uv, wave3Dir) * frequency + time + _RippleOffset * 0.5;
                float seed4 = dot(uv, wave4Dir) * frequency + time + _RippleOffset * 0.75;

                float wave1 = (sin(seed1 * frequency) + sin(seed1 * 1.61803398875) - sin(seed1 * 1.41421356237));
                float wave2 = (sin(seed2 * frequency) + sin(seed2 * 1.73205080757) - sin(seed2 * 1.30901699437));
                float wave3 = (sin(seed3 * frequency) + sin(seed3 * 1.61803398875) - sin(seed3 * 1.41421356237));
                float wave4 = (sin(seed4 * frequency) + sin(seed4 * 1.73205080757) - sin(seed4 * 1.30901699437));

                float2 displacement = amplitude * (
                    wave1 * wave1Dir +
                    wave2 * wave2Dir +
                    wave3 * wave3Dir +
                    wave4 * wave4Dir
                ) / input.screenPos.w;

                float2 displacedUV = screenUV + displacement;

                // ===================================================================
                // THIS IS THE OFFICIAL URP FIX FOR WEBGL (and all platforms)
                // SampleSceneDepth + NDC adjustment so sampled depth is in EXACTLY
                // the same space as fragmentDepthRaw = screenPos.z / screenPos.w
                // This eliminates the "magnified / different space / distance cutoff"
                // ===================================================================
                float sampledDepth = SampleSceneDepth(displacedUV);   // official helper from DeclareDepthTexture.hlsl

                // On WebGL/OpenGL the depth texture stores values differently.
                // This lerp makes sampledDepth match the exact NDC z that Unity writes
                // for your water plane (the space your fragmentDepthRaw lives in).
                #if !UNITY_REVERSED_Z
                    sampledDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, sampledDepth);
                #endif

                float fragmentDepthRaw = input.screenPos.z / input.screenPos.w;

                // Now both depths are in identical space → comparison is reliable at any distance
                bool objectInFront;
                #if UNITY_REVERSED_Z
                    objectInFront = (sampledDepth > fragmentDepthRaw);   // reversed platforms: larger = closer
                #else
                    objectInFront = (sampledDepth < fragmentDepthRaw);   // WebGL: smaller = closer
                #endif

                // Reflection setup (unchanged)
                float3 normalWS = normalize(input.normalWS);
                float3 perturbNormal = float3(displacement.x, 0.0, displacement.y) * _NormalScale;
                float3 reflectionNormal = normalize(normalWS + perturbNormal);

                float3 viewDirWS = normalize(input.viewDirWS);
                float3 reflectDir = reflect(-viewDirWS, reflectionNormal);

                half4 reflectionColor = SAMPLE_TEXTURECUBE(_Skybox, sampler_Skybox, reflectDir);

                float cosTheta = saturate(dot(viewDirWS, reflectionNormal));
                float fresnelTerm = pow(1.0 - cosTheta, _FresnelSharpness);
                float reflectionIntensity = fresnelTerm * _ReflectionStrength;

                half4 color;

                if (objectInFront)
                {
                    // Object in front → use undisplaced UV (no wobbling)
                    color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);
                    color.rgb = lerp(color.rgb, _BaseColor.rgb, _BaseColor.a);
                    color.rgb = lerp(color.rgb, reflectionColor.rgb, reflectionIntensity);
                    return half4(color.rgb, 1);
                }

                // No object in front → use rippled/displaced UV
                color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);
                color.rgb = lerp(color.rgb, _BaseColor.rgb, _BaseColor.a);
                color.rgb = lerp(color.rgb, reflectionColor.rgb, reflectionIntensity);

                return half4(color.rgb, 1);
            }
            ENDHLSL
        }
    }
}