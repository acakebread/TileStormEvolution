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

            #define MAX_DEPTH_LAYER_COUNT 8
            #define MAX_VISIBLE_DEPTH_LAYER_COUNT 10

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float _PseudoDepth;
                float _DepthLayerCount;
                float _FogFarPlane;
            CBUFFER_END

            float4 _LayerRotationCompensations[MAX_VISIBLE_DEPTH_LAYER_COUNT];

            float LayerFovScale(float layerDepth01)
            {
                float nearToFogFar = saturate(_ProjectionParams.y / max(_FogFarPlane, 1e-6));
                return lerp(nearToFogFar, 1.0, saturate(layerDepth01));
            }

            float4 NormalizeQuaternion(float4 q)
            {
                return q * rsqrt(max(dot(q, q), 1e-8));
            }

            float3 RotateByQuaternion(float3 v, float4 q)
            {
                q = NormalizeQuaternion(q);
                return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
            }

            float3 GetLayerWorldViewDirection(float2 screenUV, float fovScale, int localLayerIndex)
            {
                float2 ndc = screenUV * 2.0 - 1.0;
                ndc *= fovScale;
                ndc.y = -ndc.y;
                float4 viewClip = float4(ndc, 1.0, 1.0);
                float4 viewSpace = mul(UNITY_MATRIX_I_P, viewClip);
                float3 viewDir = normalize(viewSpace.xyz / max(viewSpace.w, 1e-6));
                float3 worldViewDir = normalize(mul(UNITY_MATRIX_I_V, float4(viewDir, 0.0)).xyz);
                return normalize(RotateByQuaternion(worldViewDir, _LayerRotationCompensations[localLayerIndex]));
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
                // Convert the perspective depth buffer into fog-range space
                // while accounting for the camera near clip, so the normalized
                // scene depth matches the fog layer span endpoints.
                float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float fogRange = max(_FogFarPlane - _ProjectionParams.y, 1e-6);
                return saturate((eyeDepth - _ProjectionParams.y) / fogRange);
            }

            float DebugLayerRead(float3 worldViewDir, int layerIndex, bool isEndLayer)
            {
                float layer = (float)(layerIndex * 2 + (isEndLayer ? 1 : 0));
                float3 layerDirection = isEndLayer ? -worldViewDir : worldViewDir;
                float3 layerOffset = float3(layer * 6.13, layer * -9.47, layer * 2.71);
                return LayerDepth(layerDirection, layerOffset, 4.0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                float sceneDepth01 = NormalizedSceneDepth(screenUV);

                float pseudoDepth = _PseudoDepth;
                int depthLayerCount = clamp((int)round(_DepthLayerCount), 1, MAX_DEPTH_LAYER_COUNT);
                float layerScale = rcp((float)depthLayerCount);
                int firstLayerIndex = -(int)floor(pseudoDepth) - 1;
                float fogAmount = 0.0;
                float nearestFogStartDepth = 1.0;

                [unroll]
                for (int localLayerIndex = 0; localLayerIndex < MAX_DEPTH_LAYER_COUNT + 2; localLayerIndex++)
                {
                    if (localLayerIndex < depthLayerCount + 2)
                    {
                        int layerIndex = firstLayerIndex + localLayerIndex;
                        float rawBandStart = (pseudoDepth + (float)layerIndex) * layerScale;
                        float rawBandEnd = rawBandStart + layerScale;
                        float visibleBandStart = max(rawBandStart, 0.0);
                        float visibleBandEnd = min(rawBandEnd, 1.0);
                        float visibleBandWidth = max(visibleBandEnd - visibleBandStart, 0.0);

                        if (visibleBandWidth > 1e-5)
                        {
                            float visibleBandMid = (visibleBandStart + visibleBandEnd) * 0.5;
                            float layerFovScale = LayerFovScale(visibleBandMid);
                            float3 worldViewDir = GetLayerWorldViewDirection(screenUV, layerFovScale, localLayerIndex);

                            float layer0Depth = rawBandStart + DebugLayerRead(worldViewDir, layerIndex, false) * layerScale;
                            float layer1Depth = rawBandStart + DebugLayerRead(worldViewDir, layerIndex, true) * layerScale;
                            float clippedStartDepth = max(layer0Depth, 0.0);
                            float clippedEndDepth = min(min(layer1Depth, sceneDepth01), 1.0);
                            float bandFogAmount = max(clippedEndDepth - clippedStartDepth, 0.0);

                            fogAmount += bandFogAmount;
                            nearestFogStartDepth = bandFogAmount > 1e-5 ? min(nearestFogStartDepth, saturate(clippedStartDepth)) : nearestFogStartDepth;
                        }
                    }
                }

                float3 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, screenUV).rgb;
                float fogMask = fogAmount > 1e-5 ? 1.0 : 0.0;
                float fogDebugShade = 1.0 - nearestFogStartDepth;
                float3 finalColor = lerp(sceneColor, float3(fogDebugShade, fogDebugShade, fogDebugShade), fogMask);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}










