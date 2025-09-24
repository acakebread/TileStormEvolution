Shader "Unlit/URPFrosted"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.25, 0.25, 0.25, 1)
        _Radius ("Radius", Range(1, 256)) = 64 // Match your working version
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

    // Radial blur with dithered sampling
    float step = 0.25; // Matches original
    float radius = _Radius; // Match original scaling
    float scale = 1.0 / input.screenPos.w; // Perspective correction
    float2 pixelPos = screenUV * _ScreenParams.xy; // Screen-space pixel coordinates

    // 4x4 Bayer dither matrix (normalized to [0,1])
    static const float bayer4x4[16] = {
        0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
       12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
        3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
       15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
    };

    // Compute dither index from pixel position
    int2 iPixelPos = int2(fmod(pixelPos.x, 4.0), fmod(pixelPos.y, 4.0));
    int ditherIndex = iPixelPos.x + iPixelPos.y * 4;
    float ditherValue = bayer4x4[ditherIndex];

    // Define 16 sample offsets at 22.5-degree increments
    float2 sampleOffsets[4];
    float angleIndex = floor(ditherValue * 4.0); // Map dither value to one of 16 angles
    float angleStep = 22.5 * (3.14159265359 / 180.0); // 22.5 degrees in radians
    float startAngle = angleIndex * angleStep;

    // Compute 4 sample offsets at 90-degree intervals starting from the dithered angle
    for (int i = 0; i < 4; i++)
    {
        float angle = startAngle + i * (90.0 * 3.14159265359 / 180.0); // 90-degree steps
        sampleOffsets[i] = float2(cos(angle), sin(angle));
    }

    // Sample loop
    for (float range = step; range <= radius; range += step)
    {
        float2 texelOffset = _MainTex_TexelSize.xy * range * scale;
        for (int i = 0; i < 4; i++)
        {
            sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, screenUV + texelOffset * sampleOffsets[i]);
        }
        measurements += 4;
    }

    half4 color = sum / measurements;

    // Apply noise
    half4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv);
    color.rgb += (noise.rgb - 0.5) * _NoiseStrength;

    // Blend with base color
    color.rgb = color.rgb * (1.0 - _BaseColor.a) + _BaseColor.rgb * _BaseColor.a;
    return half4(color.rgb, 1);            
}

            ENDHLSL
        }
    }
}