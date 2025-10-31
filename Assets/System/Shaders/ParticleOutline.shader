Shader "Debug/ParticleOutlineSimple"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (0,1,1,0.3)
        _OutlineColor ("Outline Color", Color) = (1,0,1,1)
        _OutlineWidth ("Outline Width", Range(0.01, 0.5)) = 0.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        // // PASS 0: OUTLINE — expanded in screen space
        // Pass
        // {
        //     Blend SrcAlpha OneMinusSrcAlpha
        //     ZWrite Off
        //     Cull Off

        //     CGPROGRAM
        //     #pragma vertex vert
        //     #pragma fragment frag
        //     #include "UnityCG.cginc"

        //     fixed4 _OutlineColor;
        //     float _OutlineWidth;

        //     struct appdata { float4 vertex : POSITION; };
        //     struct v2f     { float4 pos : SV_POSITION; };

        //     v2f vert(appdata v)
        //     {
        //         v2f o;
        //         float4 clip = UnityObjectToClipPos(v.vertex);
        //         float2 ndc = clip.xy / clip.w;
        //         float2 pixel = ndc * _ScreenParams.xy;

        //         // Expand by a fraction of the particle's screen radius
        //         float radius = length(pixel);
        //         if (radius > 0.01)
        //         {
        //             float2 dir = pixel / radius;
        //             pixel += dir * radius * _OutlineWidth;
        //         }

        //         ndc = pixel / _ScreenParams.xy;
        //         o.pos = float4(ndc * clip.w, clip.z, clip.w);
        //         return o;
        //     }

        //     fixed4 frag(v2f i) : SV_Target { return _OutlineColor; }
        //     ENDCG
        // }

        // PASS 1: FILL — original size
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _MainColor;

            struct appdata { float4 vertex : POSITION; };
            struct v2f     { float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return _MainColor; }
            ENDCG
        }
    }
}