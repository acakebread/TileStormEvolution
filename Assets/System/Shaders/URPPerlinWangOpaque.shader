Shader "Unlit/URPPerlinWangOpaque"
{
    Properties
    {
        _MainTex ("Reflection / Mirror", 2D) = "black" {}
        _DimColor ("Dim Color", Color) = (1,1,1,1)
        _NoiseTex ("Noise / Wang Atlas", 2D) = "white" {}
        _NoiseScale ("Tiles per World Unit", Float) = 1.0
        _TilesPerRow ("Tiles Per Row", Float) = 4.0
        _FilmIntensity ("Film Intensity", Range(0,1)) = 0.2
        _NoiseTexSize ("Noise Texture Size (width,height)", Vector) = (256,256,0,0)
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.25
        _Skybox ("Skybox", Cube) = "" {}
        _FresnelPower ("Fresnel Exponent", Range(1, 40)) = 12
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
                float2 uv  : TEXCOORD0; // world-space coordinates
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float3 viewDirWS : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DimColor;
                float _NoiseScale;
                float _TilesPerRow;
                float _FilmIntensity;
                float4 _NoiseTexSize;
                float _ReflectionStrength;
                float _FresnelPower;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            TEXTURECUBE(_Skybox);
            SAMPLER(sampler_Skybox);

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = i.uv;
                o.screenPos = ComputeScreenPos(o.pos);
                o.positionWS = mul(unity_ObjectToWorld, i.pos).xyz;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewDirWS = GetWorldSpaceViewDir(o.positionWS);
                return o;
            }

            float2 frac2(float2 v) { return v - floor(v); }

            float Hash2D(float2 p)
            {
                p = frac(p * float2(0.3183099, 0.3678794));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            int RandomTileIndex(float coord, float TilesPerRow)
            {
                return (int)(Hash2D(float2(coord, coord)) * TilesPerRow);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                half3 mirror = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV).rgb;

                float2 scaled = i.uv * _NoiseScale;
                float2 tileCoord = floor(scaled);

                float2 atlasTileIndex;
                atlasTileIndex.x = RandomTileIndex(tileCoord.x, _TilesPerRow);
                atlasTileIndex.y = RandomTileIndex(tileCoord.y, _TilesPerRow);

                float2 localUV = frac2(scaled);

                float tileSize = 1.0 / _TilesPerRow;

                float2 texelSize = 1.0 / _NoiseTexSize.xy;
                float2 atlasUV = atlasTileIndex * tileSize + localUV * (tileSize - texelSize) + texelSize * 0.5;

                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, atlasUV).r;

                half3 film = _DimColor.rgb * (noise * _FilmIntensity);
                half3 color = mirror * _DimColor.rgb + film;

                // === Added Fresnel Reflection ===
                float3 normalWS = normalize(i.normalWS);
                float3 viewDirWS = normalize(i.viewDirWS);
                float3 reflectDir = reflect(-viewDirWS, normalWS);

                half4 reflectionColor = SAMPLE_TEXTURECUBE(_Skybox, sampler_Skybox, reflectDir);

                float cosTheta = saturate(dot(viewDirWS, normalWS));
                float fresnelTerm = pow(1.0 - cosTheta, _FresnelPower);
                float reflectionIntensity = fresnelTerm * _ReflectionStrength;

                color.rgb = lerp(color.rgb, reflectionColor.rgb, reflectionIntensity);
                // === End Reflection ===

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}