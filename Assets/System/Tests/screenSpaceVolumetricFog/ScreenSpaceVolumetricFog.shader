Shader "Hidden/ScreenSpaceVolumetricFog"
{
    Properties
    {
        _CausticTexture("Caustic Texture", 2D) = "white" {}
        _FieldTint("Field Tint", Color) = (1, 1, 1, 1)
        _BlobTint("Blob Tint", Color) = (0.7, 0.0, 1.0, 1.0)
        _FieldScale("Field Scale", Vector) = (1, 1, 0, 0)
        _FieldOffset("Field Offset", Vector) = (0, 0, 0, 0)
        _BlobScale("Blob Scale", Vector) = (1, 1, 0, 0)
        _BlobOffset("Blob Offset", Vector) = (0.19, -0.11, 0, 0)
        _FieldBias("Field Bias", Range(-1, 1)) = 0
        _DriftSpeed("Drift Speed", Float) = 0.35
        _FieldDriftAmount("Field Drift Amount", Vector) = (0.04, 0.03, 0, 0)
        _BlobDriftAmount("Blob Drift Amount", Vector) = (0.06, 0.05, 0, 0)
        _FieldPhaseOffset("Field Phase Offset", Float) = 0
        _BlobPhaseOffset("Blob Phase Offset", Float) = 1.5708
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

            TEXTURE2D(_CausticTexture);
            SAMPLER(sampler_CausticTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _FieldTint;
                float4 _BlobTint;
                float4 _FieldScale;
                float4 _FieldOffset;
                float4 _BlobScale;
                float4 _BlobOffset;
                float _FieldBias;
                float _DriftSpeed;
                float4 _FieldDriftAmount;
                float4 _BlobDriftAmount;
                float _FieldPhaseOffset;
                float _BlobPhaseOffset;
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

            float2 DirectionToSphericalUV(float3 directionWS)
            {
                float azimuth = atan2(directionWS.x, directionWS.z);
                float elevation = asin(clamp(directionWS.y, -1.0, 1.0));

                float2 sphericalUV;
                sphericalUV.x = azimuth * 0.15915494309 + 0.5;
                sphericalUV.y = 0.5 - elevation * 0.31830988618;
                return sphericalUV;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth01 = saturate(Linear01Depth(rawDepth, _ZBufferParams));

                float3 worldViewDir = GetWorldViewDirection(screenUV);
                float2 sphereUV = DirectionToSphericalUV(worldViewDir);

                float time = _Time.y * _DriftSpeed;
                float2 fieldDrift = _FieldDriftAmount.xy * time;
                float2 blobDrift = _BlobDriftAmount.xy * time;

                float2 fieldUv = frac(sphereUV * _FieldScale.xy + _FieldOffset.xy + fieldDrift + float2(_FieldPhaseOffset, -_FieldPhaseOffset));
                float2 blobUv = frac(sphereUV * _BlobScale.xy + _BlobOffset.xy + blobDrift + float2(_BlobPhaseOffset, -_BlobPhaseOffset));

                float fieldDepth = SAMPLE_TEXTURE2D(_CausticTexture, sampler_CausticTexture, fieldUv).r;
                float blobDepth = SAMPLE_TEXTURE2D(_CausticTexture, sampler_CausticTexture, blobUv).r;

                fieldDepth = saturate(fieldDepth + _FieldBias);
                blobDepth = saturate(blobDepth + _FieldBias);

                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, screenUV);
                float delta = min(blobDepth, sceneDepth01) - fieldDepth;

                half3 finalColor = sceneColor.rgb;
                if (delta > 0.0)
                {
                    float blobBlend = saturate(delta);
                    finalColor = lerp(sceneColor.rgb, _BlobTint.rgb, blobBlend);
                }

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
