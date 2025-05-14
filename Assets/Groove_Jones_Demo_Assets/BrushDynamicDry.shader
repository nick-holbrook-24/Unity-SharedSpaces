Shader "SharedSpaces/BrushDynamicDry"
{
    Properties
    {
        _Color       ("Brush Color", Color) = (1,1,1,1)
        _MainTex     ("Main Texture", 2D)  = "white" {}
        _DryTime     ("Time When Dry", Float) = 0
        _DryDuration ("Dry Duration (s)", Float) = 2
        _Amplitude   ("Wobble Amplitude", Float) = 0.05
        _Frequency   ("Wobble Frequency", Float) = 20
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        LOD 100
        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4   _Color;
            float    _DryTime;
            float    _DryDuration;
            float    _Amplitude;
            float    _Frequency;

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 uv       : TEXCOORD0;
                float3 normal   : NORMAL;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float3 worldPos: TEXCOORD1;
                float3 normal  : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                // Compute world position
                float3 worldP = mul(unity_ObjectToWorld, v.vertex).xyz;

                // How much time left until dry (clamped 0→1)
                float t = (_DryTime - _Time.y) / _DryDuration;
                t = saturate(t);

                // Wobble offset along the normal
                float wobble = sin((_Time.y * _Frequency) + dot(worldP, float3(12,78,34))) 
                             * _Amplitude * t;

                worldP += v.normal * wobble;

                o.pos       = UnityObjectToClipPos(float4(worldP,1));
                o.uv        = v.uv;
                o.worldPos  = worldP;
                o.normal    = v.normal;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * _Color;
                return tex;
            }
            ENDCG
        }
    }
}
