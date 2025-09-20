Shader "Unlit/URPFrosted"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.1, 0.1, 0.1, 0.5)
        _FrostRadius ("Frost Radius", Range(0, 0.02)) = 0.005
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
                float _FrostRadius;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

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
                // Screen-space UVs
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // 9-sample box blur
                half4 color = 0;
                float samples = 0;
                for (int i = -1; i <= 1; i++)
                {
                    for (int j = -1; j <= 1; j++)
                    {
                        float2 sampleUV = screenUV + float2(i, j) * _FrostRadius;
                        color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);
                        samples += 1;
                    }
                }
                color /= samples;
                return half4(color.rgb * _BaseColor.rgb, _BaseColor.a);

                // Debug: Uncomment to test shader rendering
                // return half4(1, 0, 0, _BaseColor.a); // Red for testing
            }
            ENDHLSL
        }
    }
}