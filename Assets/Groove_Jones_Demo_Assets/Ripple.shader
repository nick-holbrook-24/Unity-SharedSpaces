Shader "SharedSpaces/Ripple"
{
    Properties
    {
        _Color("Base Color", Color) = (1,1,1,1)
        _MainTex("Texture", 2D) = "white" {}
        _RippleCenter("Ripple Center", Vector) = (0,0,0,0)
        _RippleTime("Ripple Time", Float) = 0
        _RippleSpeed("Speed", Float) = 2
        _RippleAmp("Amplitude", Float) = 0.05
        _RippleFreq("Frequency", Float) = 15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard vertex:vert

        sampler2D _MainTex;
        float4 _Color;
        float3 _RippleCenter;
        float _RippleTime, _RippleSpeed, _RippleAmp, _RippleFreq;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void vert(inout appdata_full v)
        {
            float3 worldP = mul(unity_ObjectToWorld, v.vertex).xyz;
            float d = distance(worldP, _RippleCenter);
            float wave = sin((d - _RippleSpeed * _RippleTime) * _RippleFreq) 
                         * _RippleAmp / (d + 1);
            v.vertex.xyz += v.normal * wave;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
