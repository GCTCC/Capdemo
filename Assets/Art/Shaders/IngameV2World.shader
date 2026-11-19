// 인게임 v2 하이웨이(3D)용 가벼운 셰이더.
// 정점 색 x 텍스처 x Tint. 빛을 받지 않고(Unlit), 반투명으로 겹쳐 그린다.
//  - _Additive 1이면 더하기(빛나는 글로우), 0이면 일반 반투명
//  - _Scan > 0이면 화면 가로 스캔라인(1px 선, 3px 간격)을 얹는다 (홀로그램 바닥)
// URP에서도 LightMode 태그 없는 패스는 그대로 그려진다.
Shader "CapDemo/IngameV2/HoloWorld"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Scan ("Scanline Strength", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest [_ZTest]
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Scan;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float4 screen : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                if (_Scan > 0)
                {
                    float py = i.screen.y / i.screen.w * _ScreenParams.y;
                    float scanLine = step(fmod(py, 3.0), 1.0);
                    c.rgb *= 1.0 - scanLine * _Scan;
                }
                return c;
            }
            ENDCG
        }
    }
}
