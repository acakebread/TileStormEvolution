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
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
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

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 worldViewDir = GetWorldViewDirection(screenUV);

                float3 p = worldViewDir * 5.0;
                float clouds = Fbm(p);
                float detail = Fbm(p * 2.75 + 17.0);
                float field = saturate(clouds * 0.75 + detail * 0.25);

                float3 baseColor = float3(0.16, 0.78, 0.78);
                float3 cloudColor = float3(0.62, 0.42, 0.95);
                float3 skyColor = lerp(baseColor, cloudColor, smoothstep(0.35, 0.8, field));
                skyColor = lerp(skyColor, skyColor * 0.75 + float3(0.15, 0.15, 0.18), saturate((worldViewDir.y + 0.2) * 0.8));

                return half4(skyColor, 1.0);
            }
            ENDHLSL
        }
    }
}
