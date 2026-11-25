// 곡 선택 → 인게임 전환 연출(A, 레코드 회전)에서 재킷·레코드판·라벨을 그리는 UI 셰이더.
//  - _Radius: 모서리 둥글기 (0 = 정사각형, 0.5 = 원)
//  - _Spin: 그림만 가운데를 기준으로 돌린다 (라디안). 테두리 모양은 그대로
//  - _UVRect: 원본 그림에서 쓸 부분 (정사각형이 아닌 재킷은 가운데 1:1만)
//  - _Hole: 가운데 작은 구멍 반지름 (레코드 라벨용, 0이면 없음)
//  - _Scan: 재킷 위 가는 스캔라인 세기 (1px 선 / 3px 간격)
//  - _BlurArc: 회전 잔상 (라디안, 부호 = 방향). 그림을 _Spin부터 _Spin + _BlurArc까지 10번 읽어 고르게 섞는다.
//              0이면 예전과 똑같이 한 번만 읽는다. 120°가 넘으면 채도도 조금 뺀다 (시안 vblur와 같은 공식)
//  - _Holo: 홀로그램처럼 사라지기 (0이면 예전과 같음). 민트로 물들고 화면 가로 줄무늬가 생긴다 (전환 C2 "지직 꺼짐")
//  - _Split: RGB 분리 (판 크기 대비 비율). 테두리 왼쪽에 빨강, 오른쪽에 시안 번짐
Shader "CapDemo/UI/TransitionDisc"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Radius ("Corner Radius", Range(0, 0.5)) = 0
        _Spin ("Spin (rad)", Float) = 0
        _BlurArc ("Spin Blur Arc (rad)", Float) = 0
        _UVRect ("UV Rect", Vector) = (0, 0, 1, 1)
        _Hole ("Center Hole", Range(0, 0.5)) = 0
        _HoleColor ("Hole Color", Color) = (0.02, 0.024, 0.024, 1)
        _Scan ("Scanline", Range(0, 1)) = 0
        _ScanColor ("Scanline Color", Color) = (0.64, 0.92, 0.84, 1)
        _Edge ("Edge Softness", Range(0.0005, 0.05)) = 0.004
        _Holo ("Hologram", Range(0, 1)) = 0
        _HoloColor ("Hologram Color", Color) = (0.64, 0.92, 0.84, 1)
        _Split ("RGB Split", Range(0, 0.1)) = 0
        [Toggle] _Additive ("Additive (RGB split)", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend [_SrcBlend] [_DstBlend]
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Radius, _Spin, _Hole, _Scan, _Edge, _BlurArc, _Holo, _Split;
            fixed4 _HoloColor;
            float4 _UVRect;
            fixed4 _HoleColor, _ScanColor;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float4 screen : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            // 판 가운데 기준 p를 ang만큼 돌려 원본 그림(_UVRect 안)에서 읽는다
            fixed4 SampleSpin(float2 p, float ang)
            {
                float s = sin(ang), c = cos(ang);
                float2 rp = float2(c * p.x - s * p.y, s * p.x + c * p.y) + 0.5;
                return tex2D(_MainTex, _UVRect.xy + rp * _UVRect.zw);
            }

            // 둥근 사각형 마스크 (r = 0.5면 원)
            float ShapeMask(float2 p)
            {
                float r = _Radius;
                float2 q = abs(p) - (0.5 - r);
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
                return saturate(-d / _Edge + 0.5);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                float mask = ShapeMask(p);

                // 그림만 회전 (회전 잔상이 있으면 원 방향으로 10번 읽어 섞는다)
                fixed4 col;
                if (abs(_BlurArc) > 0.0001)
                {
                    float4 sum = 0;
                    [unroll]
                    for (int k = 0; k < 10; k++) sum += SampleSpin(p, _Spin + _BlurArc * (k / 9.0));
                    col = sum * 0.1;
                    float arcDeg = abs(_BlurArc) * 57.29578;
                    if (arcDeg > 120.0)
                    {
                        float sat = 1.0 - min(0.4, (arcDeg - 120.0) / 500.0);
                        float l = dot(col.rgb, float3(0.299, 0.587, 0.114));
                        col.rgb = lerp(float3(l, l, l), col.rgb, sat);
                    }
                }
                else col = SampleSpin(p, _Spin);
                col *= i.color;

                // 재킷 스캔라인 (위에서 3칸마다 1칸)
                if (_Scan > 0)
                {
                    float line3 = step(fmod((1.0 - i.uv.y) * 400.0, 3.0), 1.0);
                    col.rgb = lerp(col.rgb, _ScanColor.rgb, line3 * _Scan * 0.10);
                }

                // 가운데 구멍
                if (_Hole > 0)
                {
                    float h = saturate((length(p) - _Hole) / _Edge + 0.5);
                    col.rgb = lerp(_HoleColor.rgb, col.rgb, h);
                }

                // 홀로그램처럼: 민트로 물들고 화면 가로 줄무늬로 비어 간다
                if (_Holo > 0)
                {
                    float l = dot(col.rgb, float3(0.299, 0.587, 0.114));
                    float3 holoCol = _HoloColor.rgb * (0.45 + 0.9 * l);
                    col.rgb = lerp(col.rgb, holoCol, saturate(_Holo * 2.0) * 0.85);
                    float py = i.screen.y / max(i.screen.w, 0.0001) * _ScreenParams.y;
                    float stripe = step(fmod(py, 4.0), 1.5);
                    col.a *= 1.0 - stripe * saturate(_Holo * 1.4) * 0.7;
                }

                col.a *= mask;

                // RGB 분리: 모양을 좌우로 밀어 빨강·시안 번짐을 더한다
                if (_Split > 0)
                {
                    float mR = ShapeMask(p + float2(_Split, 0));
                    float mB = ShapeMask(p - float2(_Split, 0));
                    float fr = max(0, mR - mask), fb = max(0, mB - mask);
                    col.rgb = col.rgb * col.a + float3(1.0, 0.30, 0.37) * fr * 0.8 + float3(0.35, 0.91, 1.0) * fb * 0.8;
                    col.a = saturate(col.a + (fr + fb) * 0.6 * i.color.a);
                    col.rgb /= max(col.a, 0.0001);
                }
                return col;
            }
            ENDCG
        }
    }
}
