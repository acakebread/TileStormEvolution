Shader "Debug/ParticleOutlineSimple"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (0,1,1,0.3)
        _OutlineColor ("Outline Color", Color) = (1,0,1,1)
        _OutlineWidth ("Outline Width", Range(1.0, 2.0)) = 1.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        // --- PASS 1: OUTLINE (drawn first, larger) ---
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;

            struct appdata {
                float4 vertex : POSITION;
            };
            struct v2f {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 clipPos = UnityObjectToClipPos(v.vertex);
                float3 viewPos = UnityObjectToViewPos(v.vertex);
                viewPos *= _OutlineWidth;
                o.pos = UnityViewToClipPos(viewPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        // --- PASS 2: FILL (drawn on top) ---
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

            struct appdata {
                float4 vertex : POSITION;
            };
            struct v2f {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _MainColor;
            }
            ENDCG
        }
    }
}