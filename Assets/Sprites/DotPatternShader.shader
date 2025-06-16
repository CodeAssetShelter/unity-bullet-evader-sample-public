Shader "Custom/DotPatternShader"
{
    Properties
    {
        _MainTex     ("Dot Pattern (Repeat)", 2D)    = "white" {}
        _ScrollSpeed ("Scroll Speed (UV/sec)", Vector)= (0,0,0,0)
        _Color       ("Target Color", Color)         = (1,0,0,1)
        _LifeStart   ("Life Start (0~1)", Range(0,1))= 0.2
        _LifeEnd     ("Life End (0~1)", Range(0,1))  = 0.8
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4   _MainTex_ST;
            float2   _ScrollSpeed;
            float4   _Color;
            float    _LifeStart;
            float    _LifeEnd;
            float4   _MainTex_TexelSize; // x=1/width, y=1/height, z=width, w=height

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };
            struct v2f
            {
                float2 uv      : TEXCOORD0;
                float4 color   : COLOR;
                float4 pos     : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            // 2D → 1D 해싱 함수
            float hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.5);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 1) 스크롤된 UV
                float2 uvWorld = i.uv + _ScrollSpeed * _Time.y;

                // 2) 픽셀 해상도 가져오기
                float2 resolution = float2(_MainTex_TexelSize.z, _MainTex_TexelSize.w);

                // 3) 전역 픽셀 좌표 계산 & seed 생성
                float2 pixelCoord = floor(uvWorld * resolution);
                float  seed       = hash21(pixelCoord);

                // 4) 시간 위상 (0~1 반복)
                float t = frac(_Time.y * 0.5 + seed);

                // 5) 페이드 알파 계산
                float alpha = 1;
                if (t > _LifeStart)
                {
                    float f = saturate((t - _LifeStart) / (_LifeEnd - _LifeStart));
                    alpha = 1 - f;
                }

                // 6) 텍스처 샘플링 & 색상 보간
                fixed4 col = tex2D(_MainTex, uvWorld);
                col.rgb   = lerp(col.rgb, _Color.rgb, t);
                col.a    *= alpha * i.color.a;

                return col;
            }
            ENDCG
        }
    }
}