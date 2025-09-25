Shader "Unlit/URPWater"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.25, 0.5, 0.75, 1) // Blue-ish tint for water
        _MainTex ("Texture", 2D) = "white" {}
        _RippleSpeed ("Ripple Speed", Range(0, 1)) = 0.5
        _RippleAmplitude ("Ripple Amplitude", Range(0, 1)) = 0.5
        _RippleFrequency ("Ripple Frequency", Range(0, 1)) = 0.5
        _RippleOffset ("Ripple Offset", Range(0, 1)) = 0.5
        _TimeSeed ("Time Seed", Float) = 0.0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "Water"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

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
                float4 _MainTex_TexelSize;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Internal scalars for adjusting normalized inputs
            #define RIPPLE_SPEED_SCALE 20.0 // Scales normalized speed back to 0-20 range
            #define RIPPLE_AMPLITUDE_SCALE 0.5 // Scales normalized amplitude back to 0-0.5 range
            #define RIPPLE_FREQUENCY_SCALE 250.0 // Scales normalized frequency back to 1-250 range
            #define RIPPLE_FREQUENCY_OFFSET 1.0 // Adds the minimum frequency (1.0)

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
                
                // Normalize and scale inputs
                float speed = _RippleSpeed * RIPPLE_SPEED_SCALE;
                float amplitude = _RippleAmplitude * RIPPLE_AMPLITUDE_SCALE;
                float frequency = _RippleFrequency * RIPPLE_FREQUENCY_SCALE + RIPPLE_FREQUENCY_OFFSET;

                // Procedural ripple displacement
                float2 uv = input.uv;
                float time = _TimeSeed * speed;

                // Four intersecting sine waves with different angles and phases
                float2 wave1Dir = normalize(float2(1, 1)); // First wave direction
                float2 wave2Dir = normalize(float2(-1, 1)); // Second wave direction, perpendicular

                float wave1 = sin(dot(uv, wave1Dir) * frequency + time * 1.41421356237) + sin(frequency + time * .173205080757);
                float wave2 = sin(dot(uv, wave2Dir) * frequency + time * 1.61803398875) + sin(frequency + time * .223606797750);

                // Combine waves for displacement, using full directional vectors
                float2 displacement = amplitude * (wave1 * wave1Dir + wave2 * wave2Dir) / input.screenPos.w;

                // Apply displacement to UVs
                float2 displacedUV = screenUV + displacement;

                // Sample the depth buffer at displaced UV
                float sampledDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, displacedUV).r;
                float fragmentDepth = input.screenPos.z / input.screenPos.w;

                // Convert depths to linear eye-space for comparison
                float sampledDepthLinear = LinearEyeDepth(sampledDepth, _ZBufferParams);
                float fragmentDepthLinear = LinearEyeDepth(fragmentDepth, _ZBufferParams);

                // Reject samples that are nearer than the fragment's depth
                if (sampledDepthLinear < fragmentDepthLinear)
                {
                    // Sample at undisplaced UV as fallback
                    half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);
                    color.rgb = color.rgb * (1.0 - _BaseColor.a) + _BaseColor.rgb * _BaseColor.a;
                    return half4(color.rgb, 1);
                }

                // Sample texture with displaced UVs
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, displacedUV);

                // Blend with base color for water tint
                color.rgb = color.rgb * (1.0 - _BaseColor.a) + _BaseColor.rgb * _BaseColor.a;

                return half4(color.rgb, 1);
            }
            ENDHLSL
        }
    }
}