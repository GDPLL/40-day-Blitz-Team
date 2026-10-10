// 红雾透视，本体雾气与远处雾团
Shader "XRay/RedFog"
{
    Properties
    {
        _FogColor("Fog Color", Color) = (1, 0.05, 0.05, 1)
        _Intensity("Intensity", Range(0, 8)) = 1.2
        _RimPower("Rim Power", Range(0.5, 8)) = 1.6
        _RimStrength("Rim Strength", Range(0, 4)) = 0.7
        _FillAlpha("Fill Alpha", Range(0, 1)) = 0.7
        _MaxDistance("Max Distance", Float) = 0
        _Expand("Expand", Float) = 0
        _Fade("Fade", Range(0, 1)) = 1
        _Density("Density", Range(0, 1)) = 1
        _NoiseScale("Noise Scale", Float) = 3.5
        _NoiseSpeed("Noise Speed", Float) = 0.35
        _HeightFade("Height Fade", Float) = 0.15
        _MistOrigin("Mist Origin", Vector) = (0, 0, 0, 0)
        _CardSize("Card Size", Float) = 0
        _CardFade("Card Fade", Range(0, 1)) = 0
        _CardSoft("Card Soft", Range(0.5, 4)) = 1.5
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
            #pragma target 3.0
            #pragma multi_compile _ _FOG_CARD
            #include "UnityCG.cginc"

            fixed4 _FogColor;
            float _Intensity;
            float _RimPower;
            float _RimStrength;
            float _FillAlpha;
            float _MaxDistance;
            float _Expand;
            float _Fade;
            float _Density;
            float _NoiseScale;
            float _NoiseSpeed;
            float _HeightFade;
            float3 _MistOrigin;
            float _CardSize;
            float _CardFade;
            float _CardSoft;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float viewDist : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            // 三维取值噪声
            float Hash(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // 三维插值噪声，x 为输入坐标
            float Noise(float3 x)
            {
                float3 cell = floor(x);
                float3 w = frac(x);
                w = w * w * (3.0 - 2.0 * w);

                float n000 = Hash(cell + float3(0.0, 0.0, 0.0));
                float n100 = Hash(cell + float3(1.0, 0.0, 0.0));
                float n010 = Hash(cell + float3(0.0, 1.0, 0.0));
                float n110 = Hash(cell + float3(1.0, 1.0, 0.0));
                float n001 = Hash(cell + float3(0.0, 0.0, 1.0));
                float n101 = Hash(cell + float3(1.0, 0.0, 1.0));
                float n011 = Hash(cell + float3(0.0, 1.0, 1.0));
                float n111 = Hash(cell + float3(1.0, 1.0, 1.0));

                float n00 = lerp(n000, n100, w.x);
                float n10 = lerp(n010, n110, w.x);
                float n01 = lerp(n001, n101, w.x);
                float n11 = lerp(n011, n111, w.x);

                return lerp(lerp(n00, n10, w.y), lerp(n01, n11, w.y), w.z);
            }

            // 三层噪声叠成雾团，p 为以目标为原点的坐标
            float Mist(float3 p)
            {
                return Noise(p) * 0.5 + Noise(p * 2.13) * 0.3 + Noise(p * 4.31) * 0.2;
            }

            #ifdef _FOG_CARD
            // 顶点，把面片按相机轴展开
            v2f vert(appdata v)
            {
                v2f o;

                float3 origin = mul(unity_ObjectToWorld, float4(0.0, 0.0, 0.0, 1.0)).xyz;
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 worldPos = origin + (right * v.vertex.x + up * v.vertex.y) * _CardSize;

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.worldPos = worldPos;
                o.worldNormal = normalize(UnityWorldSpaceViewDir(worldPos));
                o.viewDist = length(UnityWorldSpaceViewDir(worldPos));
                o.uv = v.uv;
                return o;
            }

            // 片元，径向衰减叠噪声出雾团
            fixed4 frag(v2f i) : SV_Target
            {
                float2 d = i.uv - 0.5;
                float radius = saturate(1.0 - length(d) * 2.0);
                radius = pow(radius, _CardSoft);

                float3 p = (i.worldPos - _MistOrigin) * _NoiseScale;
                float mist = Mist(p + float3(0.0, _Time.y * _NoiseSpeed, 0.0));
                float cloud = 0.6 + 0.6 * mist;

                float density = saturate(radius * cloud * _FillAlpha * _Density * _Fade * _CardFade);

                return fixed4(_FogColor.rgb * _Intensity, density * _FogColor.a);
            }
            #else
            // 顶点，世界空间沿法线外扩并算世界法线与距离
            v2f vert(appdata v)
            {
                v2f o;

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);

                // 外扩量随噪声起伏，形成雾团而不是被吹大的模型
                float lump = 0.8 + 0.35 * Noise((worldPos - _MistOrigin) * _NoiseScale * 0.25);
                float3 offset = worldNormal * _Expand * lump;

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos + offset, 1.0));
                o.worldPos = worldPos + offset;
                o.worldNormal = worldNormal;
                o.viewDist = length(UnityWorldSpaceViewDir(worldPos));
                o.uv = v.uv;
                return o;
            }

            // 片元，用边缘菲涅尔打形，噪声只做轻起伏
            fixed4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float rim = 1.0 - saturate(dot(normalize(i.worldNormal), viewDir));
                rim = pow(rim, _RimPower);

                // 以目标为原点的低频噪声，缓慢上飘
                float3 p = (i.worldPos - _MistOrigin) * _NoiseScale;
                float mist = Mist(p + float3(0.0, _Time.y * _NoiseSpeed, 0.0));
                float cloud = lerp(0.88, 1.08, mist);

                // 距目标脚点越高越淡
                float low = saturate(1.0 - (i.worldPos.y - _MistOrigin.y) * _HeightFade);

                // 越靠轮廓越淡，避免在轮廓上留一圈硬边
                float edge = saturate(1.0 - rim * _RimStrength);

                // 薄雾打底，再乘起伏、距离浓度与淡入淡出
                float density = saturate(_FillAlpha * low * edge);
                density = saturate(density * cloud * _Density * _Fade);

                float fade = 1.0;
                if (_MaxDistance > 0.001) fade = saturate(1.0 - i.viewDist / _MaxDistance);

                return fixed4(_FogColor.rgb * _Intensity, density * _FogColor.a * fade);
            }
            #endif
            ENDCG
        }
    }
    Fallback Off
}
