Shader "Unlit/URPMirrorWithFilmOpaque"
{
    Properties
    {
        _MainTex ("Reflection", 2D) = "black" {}
        _DimColor ("Dim Color", Color) = (1,1,1,1)
        _NoiseTex ("Noise", 2D) = "white" {}
        _FilmIntensity ("Film Intensity", Range(0,1)) = 0.2
        _NoiseScale ("Noise Scale", Range(0.1,10)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 pos : POSITION;
                float2 uv  : TEXCOORD0;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DimColor;
                float _FilmIntensity;
                float _NoiseScale;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = i.uv * _NoiseScale;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                half3 mirror = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV).rgb;
                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv).r;
                half3 film = _DimColor.rgb * (noise * _FilmIntensity);
                half3 color = mirror * _DimColor.rgb + film;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}