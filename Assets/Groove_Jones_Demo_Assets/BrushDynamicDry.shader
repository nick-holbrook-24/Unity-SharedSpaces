Shader "SharedSpaces/BrushDynamicDry"
{
    Properties
    {
        _MainTex     ("MainTex", 2D)      = "white" {}
        _Color       ("Brush Color", Color) = (1,1,1,1)
        _StartTime   ("Stroke Start Time", Float) = 0
        _DryDuration ("Dry Duration (s)", Float)   = 2
        _Amplitude   ("Wobble Amp", Float)         = 0.05
        _Frequency   ("Wobble Freq", Float)        = 20
        _FadeAlpha   ("Fade Alpha", Range(0,1))    = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4   _Color;
            float    _StartTime;
            float    _DryDuration;
            float    _Amplitude;
            float    _Frequency;
            float    _FadeAlpha;

            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f    { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldP = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = saturate(1 - ( (_Time.y - _StartTime) / _DryDuration ));
                float wobble = sin((_Time.y * _Frequency) + dot(worldP, float3(12,78,34)))
                             * _Amplitude * t;
                worldP += v.normal * wobble;
                o.pos = UnityObjectToClipPos(float4(worldP,1));
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                col.a *= _FadeAlpha;
                return col;
            }
            ENDCG
        }
    }
}
