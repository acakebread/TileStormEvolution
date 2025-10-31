Shader "Debug/WireframeTrigger"
{
    Properties
    {
        _Color ("Color", Color) = (0,1,1,0.3)
        [Toggle] _UseTexture ("Use Texture", Float) = 0
        _MainTex ("Texture", 2D) = "white" {}
        _BaseMap ("Base Map", 2D) = "white" {}
        _Albedo ("Albedo", 2D) = "white" {}
        _MainTexture ("Main Texture", 2D) = "white" {}
        _Diffuse ("Diffuse", 2D) = "white" {}
        _Texture ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _UseTexture;
            sampler2D _MainTex, _BaseMap, _Albedo, _MainTexture, _Diffuse, _Texture;
            float4 _MainTex_ST, _BaseMap_ST, _Albedo_ST, _MainTexture_ST, _Diffuse_ST, _Texture_ST;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_UseTexture > 0.5)
                {
                    fixed4 col = tex2D(_MainTex, i.uv * _MainTex_ST.xy + _MainTex_ST.zw);
                    if (col.a < 0.01) col = tex2D(_BaseMap, i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw);
                    if (col.a < 0.01) col = tex2D(_Albedo, i.uv * _Albedo_ST.xy + _Albedo_ST.zw);
                    if (col.a < 0.01) col = tex2D(_MainTexture, i.uv * _MainTexture_ST.xy + _MainTexture_ST.zw);
                    if (col.a < 0.01) col = tex2D(_Diffuse, i.uv * _Diffuse_ST.xy + _Diffuse_ST.zw);
                    if (col.a < 0.01) col = tex2D(_Texture, i.uv * _Texture_ST.xy + _Texture_ST.zw);
                    return col;
                }
                return _Color;
            }
            ENDCG
        }
    }
}