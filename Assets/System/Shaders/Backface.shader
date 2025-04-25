Shader "Custom/Backface"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Brightness ("Brightness", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Front // Render backfaces
        Pass
        {
            Tags { "LightMode"="ForwardBase" } // Support main directional light

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Brightness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // Use original normal
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Texture color
                fixed4 col = tex2D(_MainTex, i.uv);

                // Diffuse lighting (Lambert)
                float3 normal = normalize(i.worldNormal);
                // Reflect light direction across Y=0 plane
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                // Apply Y-plane reflection: (x, y, z) -> (x, -y, z)
                lightDir = float3(lightDir.x, -lightDir.y, lightDir.z);
                float NdotL = max(0, dot(normal, lightDir));
                fixed3 diffuse = col.rgb * _LightColor0.rgb * NdotL;

                // Ambient lighting
                fixed3 ambient = col.rgb * UNITY_LIGHTMODEL_AMBIENT.rgb;

                // Combine and apply brightness
                fixed3 finalColor = (ambient + diffuse) * _Brightness;

                return fixed4(finalColor, col.a);
                // return fixed4(lightDir * 0.5 + 0.5, 1); // Debug light direction
                // return fixed4(normal * 0.5 + 0.5, 1); // Debug normals
            }
            ENDCG
        }
    }
}