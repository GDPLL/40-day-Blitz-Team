Shader "Signal/Glitch"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
        _Noise("Snow", Range(0, 1)) = 0
        _Tear("Tear", Range(0, 0.25)) = 0
        _Chroma("Chroma", Range(0, 16)) = 0
        _Grey("Grey", Range(0, 1)) = 0
        _Bright("Bright", Range(-0.5, 0.5)) = 0
        _Block("Block", Range(0, 1)) = 0
        _Band("Band", Range(0, 1)) = 0
        _BandBright("Band Bright", Range(0, 1)) = 0.35
        _Scanline("Scanline", Range(0, 0.5)) = 0.18
        _Roll("Roll", Range(0, 1)) = 0
        _Seed("Seed", Float) = 0
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Noise;
            float _Tear;
            float _Chroma;
            float _Grey;
            float _Bright;
            float _Block;
            float _Band;
            float _BandBright;
            float _Scanline;
            float _Roll;
            float _Seed;

            // 取二维随机值，salt 用于分组
            float Hash(float2 p, float salt)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233)) + salt + _Seed) * 43758.5453);
            }

            // 片元，撕裂叠通道分离与雪花
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 texel = _MainTex_TexelSize.xy;

                // 按行块撕裂，部分块水平位移
                float row = floor(uv.y / max(texel.y * 24.0, 0.0001));
                float offset = (Hash(float2(row, 1.7), 0.0) - 0.5) * _Tear;
                uv.x += step(1.0 - _Tear * 5.0, Hash(float2(row, 9.3), 0.0)) * offset;

                // 通道分离
                float2 split = float2(_Chroma * texel.x, 0.0);
                float3 col = float3(
                    tex2D(_MainTex, uv + split).r,
                    tex2D(_MainTex, uv).g,
                    tex2D(_MainTex, uv - split).b);

                // 块状马赛克
                if (_Block > 0.001)
                {
                    float2 grid = texel * 20.0;
                    float2 block = (floor(uv / grid) + 0.5) * grid;
                    col = lerp(col, tex2D(_MainTex, block).rgb, _Block);
                }

                // 去色与亮度
                col = lerp(col, dot(col, float3(0.299, 0.587, 0.114)), _Grey);
                col *= 1.0 + _Bright;

                // 扫描线
                col *= 1.0 - _Scanline * _Noise * step(0.5, frac(uv.y / max(texel.y, 0.0001) * 0.5));

                // 滚动亮带
                col += _BandBright * _Band * smoothstep(0.06, 0.0, abs(frac(uv.y - _Roll) - 0.5));

                // 雪花噪点
                col = lerp(col, Hash(i.uv * _MainTex_TexelSize.zw * 0.5, 4.1), _Noise);

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
