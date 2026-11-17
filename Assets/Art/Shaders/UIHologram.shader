// UI용 홀로그램 셰이더 (곡 선택 화면의 앨범 커버)
// 어떤 그림을 넣어도 청록색 홀로그램처럼 보이게 한다.
//  - 그림의 밝기만 뽑아서 어두운 색 ~ 밝은 색 사이로 다시 칠한다
//  - 가로 스캔라인, 위아래로 지나가는 밝은 띠, 살짝 깜빡임
//  - 동그랗게 자르고 테두리를 빛나게 한다 (Circle 0이면 네모 그대로)
//  - _Glitch는 코드가 곡을 바꿀 때 1→0으로 잠깐 올린다 (지직거림)
// 색과 세기는 머티리얼(Assets/Art/UI/SongSelect/HoloCover.mat)에서 바꾼다.
Shader "CapDemo/UI/Hologram"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _DarkColor ("Dark Color (어두운 부분)", Color) = (0.02, 0.10, 0.10, 1)
        _LightColor ("Light Color (밝은 부분)", Color) = (0.66, 1.0, 0.90, 1)
        _Contrast ("Contrast (대비)", Range(0.5, 3)) = 1.35

        _ScanlineCount ("Scanline Count (줄 개수)", Float) = 170
        _ScanlineStrength ("Scanline Strength (줄 진하기)", Range(0, 1)) = 0.28
        _ScanlineSpeed ("Scanline Speed (줄 흐르는 속도)", Float) = 0.4

        _SweepStrength ("Sweep Strength (지나가는 밝은 띠)", Range(0, 1)) = 0.22
        _SweepSpeed ("Sweep Speed", Float) = 0.12

        _Flicker ("Flicker (깜빡임)", Range(0, 0.3)) = 0.04
        _Glitch ("Glitch (코드가 조절)", Range(0, 1)) = 0

        [Toggle] _Circle ("Circle (동그랗게 자르기)", Float) = 1
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.1)) = 0.006
        _RimColor ("Rim Color (테두리 빛)", Color) = (0.62, 1.0, 0.92, 1)
        _RimWidth ("Rim Width", Range(0, 0.4)) = 0.10
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.9

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float2 localUV       : TEXCOORD2;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            fixed4 _DarkColor;
            fixed4 _LightColor;
            float _Contrast;
            float _ScanlineCount;
            float _ScanlineStrength;
            float _ScanlineSpeed;
            float _SweepStrength;
            float _SweepSpeed;
            float _Flicker;
            float _Glitch;
            float _Circle;
            float _EdgeSoftness;
            fixed4 _RimColor;
            float _RimWidth;
            float _RimStrength;

            // RawImage의 uvRect로 잘라낸 범위와 상관없이 0~1 사각형 좌표를 쓰기 위해
            // 코드가 넣어주는 uvRect(x, y, w, h)
            float4 _UVRect;

            float hash11(float n)
            {
                return frac(sin(n * 12.9898) * 43758.5453);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                float4 r = _UVRect;
                if (r.z <= 0.0) r = float4(0, 0, 1, 1);
                OUT.localUV = (v.texcoord - r.xy) / r.zw;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y;
                float2 uv = IN.texcoord;
                float2 local = IN.localUV;

                // 지직거림: 가로 띠마다 좌우로 밀기
                float band = floor(local.y * 26.0) + floor(t * 18.0) * 7.0;
                float shift = (hash11(band) - 0.5) * 0.14 * step(0.55, hash11(band + 3.7)) * _Glitch;
                float wobble = sin(local.y * 70.0 + t * 3.0) * 0.0012;
                float2 suv = uv + float2(shift + wobble, 0.0);

                fixed4 tex = tex2D(_MainTex, suv) + _TextureSampleAdd;
                float lum = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                // 글리치 중에는 옆 픽셀 밝기를 섞어 색 번짐처럼 보이게 한다
                float lumShift = dot(tex2D(_MainTex, suv + float2(0.012 * _Glitch, 0)).rgb, float3(0.299, 0.587, 0.114));
                lum = lerp(lum, lumShift, 0.5 * _Glitch);
                lum = saturate((lum - 0.5) * _Contrast + 0.5);

                float3 col = lerp(_DarkColor.rgb, _LightColor.rgb, lum);

                // 스캔라인
                float scan = sin((local.y * _ScanlineCount + t * _ScanlineSpeed * 10.0) * 6.28318) * 0.5 + 0.5;
                col *= 1.0 - _ScanlineStrength * scan;

                // 위에서 아래로 지나가는 밝은 띠
                float sweepPos = 1.2 - frac(t * _SweepSpeed) * 1.4;
                float sweep = exp(-pow((local.y - sweepPos) * 22.0, 2.0));
                col += _LightColor.rgb * sweep * _SweepStrength;

                // 깜빡임
                col *= 1.0 - _Flicker * hash11(floor(t * 24.0));
                col *= 1.0 + _Glitch * 0.35;

                float alpha = tex.a;

                if (_Circle > 0.5)
                {
                    float d = length(local - 0.5) * 2.0;
                    float inside = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, d);
                    float rim = smoothstep(1.0 - _RimWidth, 1.0, d) * inside;
                    col += _RimColor.rgb * rim * _RimStrength;
                    alpha *= inside;
                }

                fixed4 color = fixed4(col, alpha) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
