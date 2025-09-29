Shader "Unlit/URPOceanOpaque"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.25, 0.5, 0.75, 1)
        _MainTex ("Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _RippleSpeed ("Ripple Speed", Range(0, 1)) = 0.5
        _RippleAmplitude ("Ripple Amplitude", Range(0, 1)) = 0.5
        _RippleFrequency ("Ripple Frequency", Range(0, 1)) = 0.5
        _RippleOffset ("Ripple Offset", Range(0, 1)) = 0.5
        _RippleSeed ("Ripple Seed", Float) = 0.0
        _DepthThreshold ("Frost Depth Max", Range(1, 512)) = 128
        _FrostDepth ("Frost Depth", Range(0, 1)) = 0.5
        _FrostNoiseStrength ("Frost Noise Strength", Range(0, 0.1)) = 0.02
        _FrostThreshold ("Frost Threshold", Range(0, 1)) = 0.5
        _FrostFadeRange ("Frost Fade Range", Range(0, 0.2)) = 0.1
        _FrostBrightness ("Frost Brightness", Range(1, 2)) = 1.5
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
            Name "OceanOpaque"
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
                float _RippleSeed;
                float _DepthThreshold;
                float _FrostDepth;
                float _FrostNoiseStrength;
                float _FrostThreshold;
                float _FrostFadeRange;
                float _FrostBrightness;
                float4 _MainTex_TexelSize;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            // Water ripple scalars
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

                // Debug: Check _MainTex
                // half4 mainTexColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);
                // if (mainTexColor.r == 0 && mainTexColor.g == 0 && mainTexColor.b == 0) return half4(1, 0, 0, 1);
                // return mainTexColor;

                // Debug: Check _NoiseTex
                // half4 noiseTexColor = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv);
                // if (noiseTexColor.r == 0 && noiseTexColor.g == 0 && noiseTexColor.b == 0) return half4(1, 0, 1, 1);
                // return noiseTexColor;

                // Water effect
                float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
                float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
                float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE + RIPPLE_FREQUENCY_OFFSET;
                float time = _RippleSeed * speed;

                float2 wave1Dir = normalize(float2(1, 1));
                float2 wave2Dir = normalize(float2(-1, 1));
                float wave1 = sin(dot(input.uv, wave1Dir) * frequency + time * 1.30901699437) + sin(frequency + time * 0.161803398875);
                float wave2 = sin(dot(input.uv, wave2Dir) * frequency + time * 1.61803398875) + sin(frequency + time * 0.323606797750);
                float waveHeight = (wave1 + wave2) * 0.5;
                float2 displacement = amplitude * (wave1 * wave1Dir + wave2 * wave2Dir) / input.screenPos.w;

                // Debug: Visualize displacement magnitude
                // float dispMag = length(displacement);
                // return half4(dispMag * 100.0, dispMag * 100.0, dispMag * 100.0, 1);

                // Depth testing
                float sceneDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
                float linearDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
                float objectDepth = LinearEyeDepth(input.screenPos.z / input.screenPos.w, _ZBufferParams);
                float depthDiff = linearDepth - objectDepth;

                // Debug: Visualize depthDiff
                // return half4(depthDiff * 0.1, depthDiff * 0.1, depthDiff * 0.1, 1);

                // Frost factor based on noise and displacement
                float dispMag = length(displacement) * 100.0;
                half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv + displacement);
                if (noise.r == 0 && noise.g == 0 && noise.b == 0)
                    noise = half4(0.5, 0.5, 0.5, 1);
                float noiseValue = (noise.r + noise.g + noise.b) / 3.0;
                float frostFactor = smoothstep(_FrostThreshold - _FrostFadeRange, _FrostThreshold, noiseValue * (1.0 + dispMag));
                frostFactor = max(frostFactor, 0.1);

                // Debug: Visualize frostFactor
                // return half4(frostFactor, frostFactor, frostFactor, 1);

                // Base color with ripples
                float2 displacedUV = screenUV + displacement;
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

                // Frost effect
                if (frostFactor > 0 && depthDiff > 0)
                {
                    half4 sum = 0;
                    float measurements = 1;
                    sum += SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, displacedUV, 0);

                    float scale = 1.0 / input.screenPos.w;
                    float2 pixelPos = screenUV * _ScreenParams.xy;

                    static const float bayer4x4[16] = {
                        0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                        12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                        3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                        15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                    };

                    int2 iPixelPos = int2(fmod(pixelPos.x, 4.0), fmod(pixelPos.y, 4.0));
                    int ditherIndex = iPixelPos.x + iPixelPos.y * 4;
                    float ditherValue = bayer4x4[ditherIndex];

                    float2 sampleOffsets[4];
                    float angleIndex = floor(ditherValue * 4.0);
                    float angleStep = 22.5 * (3.14159265359 / 180.0);
                    float startAngle = angleIndex * angleStep;

                    for (int i = 0; i < 4; i++)
                    {
                        float angle = startAngle + i * (90.0 * 3.14159265359 / 180.0);
                        sampleOffsets[i] = float2(cos(angle), sin(angle));
                    }

                    float ct = clamp(_FrostDepth * _DepthThreshold, 1, 128);
                    for (float d = 1; d <= ct; ++d)
                    {
                        float2 texelOffset = _MainTex_TexelSize.xy * d * scale;
                        for (int i = 0; i < 4; i++)
                        {
                            sum += SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, displacedUV + texelOffset * sampleOffsets[i], 0);
                        }
                        measurements += 4;
                    }

                    half4 frostColor = sum / measurements;
                    noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv);
                    if (noise.r == 0 && noise.g == 0 && noise.b == 0)
                        noise = half4(0.5, 0.5, 0.5, 1);
                    frostColor.rgb += (noise.rgb - 0.5) * _FrostNoiseStrength;

                    // Brighten frost
                    frostColor.rgb *= _FrostBrightness;
                    frostColor.rgb = lerp(frostColor.rgb, 1.0, 0.3);

                    color = lerp(color, frostColor, frostFactor);
                }

                // Apply depth test
                if (depthDiff <= 0)
                {
                    color = half4(_BaseColor.rgb, 1);
                }

                // Blend with base color
                color.rgb = color.rgb * (1.0 - _BaseColor.a) + _BaseColor.rgb * _BaseColor.a;
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }
    }
}