Shader "Hidden/ScreenSpaceVolumetricFog"
{
    Properties
    {
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ScreenSpaceVolumetricFog"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
            CBUFFER_END

            float3 GetWorldViewDirection(float2 screenUV)
            {
                float2 ndc = screenUV * 2.0 - 1.0;
                ndc.y = -ndc.y;
                float4 viewClip = float4(ndc, 1.0, 1.0);
                float4 viewSpace = mul(UNITY_MATRIX_I_P, viewClip);
                float3 viewDir = normalize(viewSpace.xyz / max(viewSpace.w, 1e-6));
                return normalize(mul(UNITY_MATRIX_I_V, float4(viewDir, 0.0)).xyz);
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, u.x);
                float nx10 = lerp(n010, n110, u.x);
                float nx01 = lerp(n001, n101, u.x);
                float nx11 = lerp(n011, n111, u.x);

                float nxy0 = lerp(nx00, nx10, u.y);
                float nxy1 = lerp(nx01, nx11, u.y);

                return lerp(nxy0, nxy1, u.z);
            }

            float Fbm(float3 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    sum += amplitude * ValueNoise3D(p * frequency);
                    frequency *= 2.0;
                    amplitude *= 0.5;
                }

                return sum;
            }

            float LayerDepth(float3 worldViewDir, float3 layerOffset, float layerFrequency)
            {
                float3 p = worldViewDir * layerFrequency + layerOffset;
                float clouds = Fbm(p);
                float detail = Fbm(p * 2.75 + 17.0);
                float depth = saturate(clouds * 0.75 + detail * 0.25);
                return saturate((depth - 0.32) * 2.78);
            }

            float NormalizedSceneDepth(float2 screenUV)
            {
                float rawDepth = SampleSceneDepth(screenUV);
                float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                return saturate((eyeDepth - _ProjectionParams.y) / max(_ProjectionParams.z - _ProjectionParams.y, 1e-6));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 worldViewDir = GetWorldViewDirection(screenUV);

                float sceneDepth01 = NormalizedSceneDepth(screenUV);

                float nearDepth = LayerDepth(worldViewDir, float3(0.0, 0.0, 0.0), 4.0);
                float farDepth = 1.0 - LayerDepth(-worldViewDir, float3(6.13, -9.47, 2.71), 4.0);

                float clippedFarDepth = min(farDepth, sceneDepth01);
                float fogAmount = saturate(clippedFarDepth - nearDepth);

                float3 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, screenUV).rgb;
                float3 finalColor = lerp(sceneColor, _FogColor.rgb, fogAmount);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
