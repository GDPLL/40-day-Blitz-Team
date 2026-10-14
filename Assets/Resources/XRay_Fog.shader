// 红雾透视，半透明覆盖
Shader "XRay/RedFog"
{
    Properties
    {
        _FogColor("Fog Color", Color) = (1, 0.05, 0.05, 1)
        _Intensity("Intensity", Range(0, 8)) = 2
        _RimPower("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength("Rim Strength", Range(0, 4)) = 1.6
        _FillAlpha("Fill Alpha", Range(0, 1)) = 0.18
        _MaxDistance("Max Distance", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _FogColor;
            float _Intensity;
            float _RimPower;
            float _RimStrength;
            float _FillAlpha;
            float _MaxDistance;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float viewDist : TEXCOORD2;
            };

            // 顶点，算世界法线与距离
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDist = length(UnityWorldSpaceViewDir(o.worldPos));
                return o;
            }

            // 片元，按边缘菲涅尔出红雾
            fixed4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float rim = 1.0 - saturate(dot(normalize(i.worldNormal), viewDir));
                rim = pow(rim, _RimPower) * _RimStrength;

                float alpha = saturate(rim + _FillAlpha);

                float fade = 1.0;
                if (_MaxDistance > 0.001) fade = saturate(1.0 - i.viewDist / _MaxDistance);

                return fixed4(_FogColor.rgb * _Intensity * fade, alpha * _FogColor.a * fade);
            }
            ENDCG
        }
    }
    Fallback Off
}
