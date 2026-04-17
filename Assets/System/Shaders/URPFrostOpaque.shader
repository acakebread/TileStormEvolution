Shader "Unlit/URPFrostOpaque"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.25, 0.25, 0.25, 1)
        _Depth ("Depth", Range(0, 1)) = 1.0
        _DepthMax ("Depth Max", Range(1, 512)) = 512
        _MainTex ("Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseStrength ("Noise Strength", Range(0, 0.1)) = 0.02
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.25
        _Skybox ("Skybox", Cube) = "" {}
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
            Name "Frost"
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float _Depth;
                float _DepthMax;
                float _NoiseStrength;
                float _ReflectionStrength;
                float _FresnelSharpness;
                float4 _MainTex_TexelSize;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURECUBE(_Skybox);
            SAMPLER(sampler_Skybox);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.positionWS = mul(unity_ObjectToWorld, input.positionOS).xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                half4 sum = 0;
                float measurements = 1;

                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);

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

                float ct = _Depth * _DepthMax;
                for (float d = 1; d <= ct; ++d)
                {
                    float2 texelOffset = _MainTex_TexelSize.xy * d * scale;
                    for (int j = 0; j < 4; j++)
                    {
                        sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV + texelOffset * sampleOffsets[j]);
                    }
                    measurements += 4;
                }

                half4 color = sum / measurements;

                // Add noise
                half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv);
                color.rgb += (noise.rgb - 0.5) * _NoiseStrength;

                // Blend base color (original behavior)
                color.rgb = color.rgb * (1.0 - _BaseColor.a) + _BaseColor.rgb * _BaseColor.a;

                // === Added Fresnel Reflection ===
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float3 reflectDir = reflect(-viewDirWS, normalWS);

                half4 reflectionColor = SAMPLE_TEXTURECUBE(_Skybox, sampler_Skybox, reflectDir);

                float cosTheta = saturate(dot(viewDirWS, normalWS));
                float fresnelTerm = pow(1.0 - cosTheta, _FresnelSharpness);
                float reflectionIntensity = fresnelTerm * _ReflectionStrength;

                color.rgb = lerp(color.rgb, reflectionColor.rgb, reflectionIntensity);
                // === End Reflection ===

                return half4(color.rgb, 1);
            }
            ENDHLSL
        }
    }
}