// 곡 선택 → 인게임 전환 연출(A, 레코드 회전)에서 재킷·레코드판·라벨을 그리는 UI 셰이더.
//  - _Radius: 모서리 둥글기 (0 = 정사각형, 0.5 = 원)
//  - _Spin: 그림만 가운데를 기준으로 돌린다 (라디안). 테두리 모양은 그대로
//  - _UVRect: 원본 그림에서 쓸 부분 (정사각형이 아닌 재킷은 가운데 1:1만)
//  - _Hole: 가운데 작은 구멍 반지름 (레코드 라벨용, 0이면 없음)
//  - _Scan: 재킷 위 가는 스캔라인 세기 (1px 선 / 3px 간격)
Shader "CapDemo/UI/TransitionDisc"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Radius ("Corner Radius", Range(0, 0.5)) = 0
        _Spin ("Spin (rad)", Float) = 0
        _UVRect ("UV Rect", Vector) = (0, 0, 1, 1)
        _Hole ("Center Hole", Range(0, 0.5)) = 0
        _HoleColor ("Hole Color", Color) = (0.02, 0.024, 0.024, 1)
        _Scan ("Scanline", Range(0, 1)) = 0
        _ScanColor ("Scanline Color", Color) = (0.64, 0.92, 0.84, 1)
        _Edge ("Edge Softness", Range(0.0005, 0.05)) = 0.004
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
            float _Radius, _Spin, _Hole, _Scan, _Edge;
            float4 _UVRect;
            fixed4 _HoleColor, _ScanColor;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                // 둥근 사각형 마스크 (r = 0.5면 원)
                float r = _Radius;
                float2 q = abs(p) - (0.5 - r);
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
                float mask = saturate(-d / _Edge + 0.5);

                // 그림만 회전
                float s = sin(_Spin), c = cos(_Spin);
                float2 rp = float2(c * p.x - s * p.y, s * p.x + c * p.y) + 0.5;
                float2 tuv = _UVRect.xy + rp * _UVRect.zw;
                fixed4 col = tex2D(_MainTex, tuv) * i.color;

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

                col.a *= mask;
                return col;
            }
            ENDCG
        }
    }
}
