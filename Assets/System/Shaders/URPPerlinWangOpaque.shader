Shader "Unlit/URPPerlinWangOpaque"
{
    Properties
    {
        _MainTex ("Reflection", 2D) = "black" {}
        _DimColor ("Dim Color", Color) = (1,1,1,1)
        _NoiseTex ("Noise / Wang Atlas", 2D) = "white" {}
        _NoiseScale ("Tiles per World Unit", Float) = 1.0
        _TilesPerRow ("Tiles Per Row", Float) = 4.0
        _FilmIntensity ("Film Intensity", Range(0,1)) = 0.2
        _NoiseTexSize ("Noise Texture Size (width,height)", Vector) = (256,256,0,0)
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
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DimColor;
                float _NoiseScale;
                float _TilesPerRow;
                float _FilmIntensity;
                float4 _NoiseTexSize; // xy = width,height
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = i.uv;
                o.screenPos = ComputeScreenPos(o.pos);
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

                // Half-texel inset in UV space
                float2 texelSize = 1.0 / _NoiseTexSize.xy;
                float2 atlasUV = atlasTileIndex * tileSize + localUV * (tileSize - texelSize) + texelSize * 0.5;

                half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, atlasUV).r;

                half3 film = _DimColor.rgb * (noise * _FilmIntensity);
                half3 color = mirror * _DimColor.rgb + film;

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}



// Shader "Unlit/URPPerlinWangOpaque"
// {
//     Properties
//     {
//         _MainTex ("Reflection", 2D) = "black" {}
//         _DimColor ("Dim Color", Color) = (1,1,1,1)
//         _NoiseTex ("Noise / Wang Atlas", 2D) = "white" {}
//         _NoiseScale ("Noise Scale (tiles per UV)", Float) = 1.0
//         _TilesPerRow ("Tiles Per Row", Float) = 4.0
//         _FilmIntensity ("Film Intensity", Range(0,1)) = 0.2
//     }

//     SubShader
//     {
//         Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
//         LOD 100

//         Pass
//         {
//             ZWrite On
//             ZTest LEqual
//             Blend One Zero

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

//             struct Attributes
//             {
//                 float4 pos : POSITION;
//                 float2 uv  : TEXCOORD0;
//             };

//             struct Varyings
//             {
//                 float4 pos : SV_POSITION;
//                 float2 uv  : TEXCOORD0;
//                 float4 screenPos : TEXCOORD1;
//             };

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _DimColor;
//                 float _NoiseScale;
//                 float _TilesPerRow;
//                 float _FilmIntensity;
//             CBUFFER_END

//             TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
//             TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex); // <- the correct one now

//             Varyings vert(Attributes i)
//             {
//                 Varyings o;
//                 o.pos = TransformObjectToHClip(i.pos.xyz);
//                 o.uv = i.uv;
//                 o.screenPos = ComputeScreenPos(o.pos);
//                 return o;
//             }

//             float2 frac2(float2 v) { return v - floor(v); }

//             half4 frag(Varyings i) : SV_Target
//             {
//                 // mirror/reflection lookup in screen space
//                 float2 screenUV = i.screenPos.xy / i.screenPos.w;
//                 half3 mirror = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV).rgb;

//                 // scaled UV into tile space
//                 float2 scaled = i.uv * _NoiseScale;
//                 float2 tilePos = floor(scaled);
//                 float2 tileFrac = frac2(scaled);

//                 // wrap tile indices so atlas repeats
//                 float2 tileIndexMod = fmod(tilePos, _TilesPerRow);
//                 tileIndexMod = fmod(tileIndexMod + _TilesPerRow, _TilesPerRow);

//                 // atlas UV = (tileIndex + localUV) / tilesPerRow
//                 float2 atlasUV = (tileIndexMod + tileFrac) / _TilesPerRow;

//                 // sample the actual atlas
//                 half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, atlasUV).r;

//                 half3 film = _DimColor.rgb * (noise * _FilmIntensity);
//                 half3 color = mirror * _DimColor.rgb + film;
//                 return half4(color, 1);
//             }
//             ENDHLSL
//         }
//     }
// }



// Shader "Unlit/URPPerlinWangOpaque"
// {
//     Properties
//     {
//         _MainTex ("Reflection", 2D) = "black" {}
//         _DimColor ("Dim Color", Color) = (1,1,1,1)
//         _NoiseTex ("Noise / Wang Atlas", 2D) = "white" {}
//         _NoiseScale ("Noise Scale (tiles per UV)", Float) = 1.0
//         _TilesPerRow ("Tiles Per Row", Float) = 4.0
//         _FilmIntensity ("Film Intensity", Range(0,1)) = 0.2
//     }

//     SubShader
//     {
//         Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
//         LOD 100

//         Pass
//         {
//             ZWrite On
//             ZTest LEqual
//             Blend One Zero

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag
//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

//             struct Attributes
//             {
//                 float4 pos : POSITION;
//                 float2 uv  : TEXCOORD0;
//             };

//             struct Varyings
//             {
//                 float4 pos : SV_POSITION;
//                 float2 uv  : TEXCOORD0;
//                 float4 screenPos : TEXCOORD1;
//             };

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _DimColor;
//                 float _NoiseScale;
//                 float _TilesPerRow;
//                 float _FilmIntensity;
//             CBUFFER_END

//             TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
//             TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex); // <- the correct one now

//             Varyings vert(Attributes i)
//             {
//                 Varyings o;
//                 o.pos = TransformObjectToHClip(i.pos.xyz);
//                 o.uv = i.uv;
//                 o.screenPos = ComputeScreenPos(o.pos);
//                 return o;
//             }

//             float2 frac2(float2 v) { return v - floor(v); }

//             half4 frag(Varyings i) : SV_Target
//             {
//                 // mirror/reflection lookup in screen space
//                 float2 screenUV = i.screenPos.xy / i.screenPos.w;
//                 half3 mirror = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV).rgb;

//                 // scaled UV into tile space
//                 float2 scaled = i.uv * _NoiseScale;
//                 float2 tilePos = floor(scaled);
//                 float2 tileFrac = frac2(scaled);

//                 // wrap tile indices so atlas repeats
//                 float2 tileIndexMod = fmod(tilePos, _TilesPerRow);
//                 tileIndexMod = fmod(tileIndexMod + _TilesPerRow, _TilesPerRow);

//                 // atlas UV = (tileIndex + localUV) / tilesPerRow
//                 float2 atlasUV = (tileIndexMod + tileFrac) / _TilesPerRow;

//                 // sample the actual atlas
//                 half noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, atlasUV).r;

//                 half3 film = _DimColor.rgb * (noise * _FilmIntensity);
//                 half3 color = mirror * _DimColor.rgb + film;
//                 return half4(color, 1);
//             }
//             ENDHLSL
//         }
//     }
// }
