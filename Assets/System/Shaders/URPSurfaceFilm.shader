Shader "Unlit/URPSurfaceFilm"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.1, 0.1, 0.1, 0.5)
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _FilmIntensity ("Film Intensity", Range(0, 0.5)) = 0.2
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 1
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
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _FilmIntensity;
                float _NoiseScale;
            CBUFFER_END

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _NoiseScale;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv).r;
                half3 color = _BaseColor.rgb + noise * _FilmIntensity;
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}