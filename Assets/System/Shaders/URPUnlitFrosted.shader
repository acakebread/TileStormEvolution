Shader "Unlit/URPFrosted"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.25, 0.25, 0.25, 1)
        _Radius ("Radius", Range(1, 20)) = 12 // Capped for performance
        _MainTex ("Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseStrength ("Noise Strength", Range(0, 0.1)) = 0.02
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
            Name "Frosted"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma unroll // Optimize for WebGL
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
                float _Radius;
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

                // Sample center pixel
                sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV);

                // Cross-shaped box blur (mimics FrostedGlass)
                float step = 0.1; // Matches original
                float radius = _Radius * 1.41421356237; // Match second pass of original
                for (float range = step; range <= radius; range += step)
                {
                    float2 texelOffset = _MainTex_TexelSize.xy * range;
                    sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV + float2(texelOffset.x, 0));
                    sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV + float2(-texelOffset.x, 0));
                    sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV + float2(0, texelOffset.y));
                    sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV + float2(0, -texelOffset.y));
                    measurements += 4;
                }

                half4 color = sum / measurements;

                // Apply noise
                half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv);
                color.rgb += (noise.rgb - 0.5) * _NoiseStrength;

                // Blend with base color
                color.rgb = lerp(color.rgb, _BaseColor.rgb, _BaseColor.a * 0.3);
                return half4(color.rgb, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}