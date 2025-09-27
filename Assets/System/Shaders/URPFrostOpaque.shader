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
            #pragma unroll
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                float _Depth;
                float _DepthMax;
                float _NoiseStrength;
                float4 _MainTex_TexelSize;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

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
                    for (int i = 0; i < 4; i++)
                    {
                        sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV + texelOffset * sampleOffsets[i]);
                    }
                    measurements += 4;
                }

                half4 color = sum / measurements;

                half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv);
                color.rgb += (noise.rgb - 0.5) * _NoiseStrength;

                color.rgb = color.rgb * (1.0 - _BaseColor.a) + _BaseColor.rgb * _BaseColor.a;
                return half4(color.rgb, 1);
            }

            ENDHLSL
        }
    }
}