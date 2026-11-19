// 허브 씬 전환용 화면 셰이더 (HoloTransition.cs가 화면 전체에 깔아서 쓴다)
//  - 꺼질 때: 찍어 둔 화면 사진(_MainTex)을 지직거리게 한 뒤, 옛날 TV처럼 가로줄로 접고 → 점으로 줄인다
//  - 켜질 때: 검은 화면에 가로줄이 생기고 → 위아래로 벌어지며 새 화면이 드러난다 (_UseTex = 0)
//  - 값은 코드가 조절한다: _OpenX/_OpenY(화면이 열린 정도), _Glitch(지직거림), _Bright(접힐 때 밝아짐)
Shader "CapDemo/UI/HoloTransition"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _OpenX ("Open X (가로로 열린 정도)", Range(0, 1)) = 1
        _OpenY ("Open Y (세로로 열린 정도)", Range(0, 1)) = 1
        _Glitch ("Glitch (지직거림)", Range(0, 1)) = 0
        _Noise ("Noise (자글자글한 잡음)", Range(0, 1)) = 0
        _Bright ("Bright (접힐 때 밝아짐)", Range(0, 1)) = 0
        _UseTex ("Use Texture (1=찍은 화면, 0=뒤 화면이 비침)", Range(0, 1)) = 1

        _LineColor ("Line Color (가로줄 빛)", Color) = (0.80, 1.0, 0.95, 1)
        _GlitchColor ("Glitch Color (켜질 때 띠 색)", Color) = (0.64, 0.92, 0.84, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _OpenX, _OpenY, _Glitch, _Noise, _Bright, _UseTex;
            fixed4 _LineColor, _GlitchColor;

            float hash11(float n) { return frac(sin(n * 12.9898) * 43758.5453); }
            float hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y;
                float2 uv = IN.texcoord;
                float2 c = uv - 0.5;
                float2 px = 1.0 / _ScreenParams.xy; // 화면 1픽셀 크기

                // 화면이 열려 있는 영역 (가장자리 2픽셀은 부드럽게)
                float halfX = max(_OpenX, 0.0) * 0.5;
                float halfY = max(_OpenY, 0.0) * 0.5;
                float lineHalf = 1.5 * px.y; // 다 접혀도 가로줄 두께는 남긴다
                float insideX = 1.0 - smoothstep(halfX, halfX + 2.0 * px.x, abs(c.x));
                float insideY = 1.0 - smoothstep(max(halfY, lineHalf), max(halfY, lineHalf) + 2.0 * px.y, abs(c.y));
                float inside = insideX * insideY * step(0.0001, _OpenX);

                // 다 접혀서 가로줄만 남았을 때 1 (줄이 하얗게 빛난다)
                float lineness = 1.0 - saturate(_OpenY * _ScreenParams.y / 24.0);

                // 지직거림: 가로 띠마다 좌우로 밀기 (글리치가 셀수록 밀리는 띠가 많아진다)
                float band = floor(uv.y * 34.0) + floor(t * 22.0) * 13.0;
                float r1 = hash11(band);
                float r2 = hash11(band + 7.13);
                float bandOn = step(1.0 - _Glitch * 0.55, r2);
                float shift = (r1 - 0.5) * 0.16 * bandOn * _Glitch;

                // 접힌 화면 안에서의 좌표 (화면을 납작하게 눌러 담는다)
                float2 suv;
                suv.x = 0.5 + c.x / max(_OpenX, 0.001) + shift;
                suv.y = 0.5 + c.y / max(_OpenY, 0.001);

                // 색 번짐 (빨강·파랑을 좌우로 벌린다)
                float split = 0.004 + 0.012 * _Glitch;
                fixed3 col;
                col.r = tex2D(_MainTex, suv + float2(split, 0)).r;
                col.g = tex2D(_MainTex, suv).g;
                col.b = tex2D(_MainTex, suv - float2(split, 0)).b;

                // 스캔라인 + 잡음
                float scan = sin(uv.y * _ScreenParams.y * 1.2 + t * 30.0) * 0.5 + 0.5;
                col *= 1.0 - 0.18 * scan * saturate(_Glitch + _Bright);
                float n = hash21(floor(uv * _ScreenParams.xy * 0.5) + floor(t * 60.0));
                col = lerp(col, n.xxx, _Noise * 0.45);
                col *= 1.0 + _Bright * 2.2;
                col = lerp(col, _LineColor.rgb, saturate(_Bright * 0.7));

                // 켜질 때(_UseTex 0): 열린 곳은 투명하게 비우고, 지직 띠만 얹는다
                float bandAlpha = bandOn * _Glitch * (0.25 + 0.5 * r1);
                float noiseAlpha = _Noise * n * 0.5;
                fixed3 overlayCol = lerp(_GlitchColor.rgb, fixed3(1, 1, 1), r1 * 0.5);
                float openAlpha = lerp(saturate(bandAlpha + noiseAlpha), 1.0, _UseTex);
                fixed3 openCol = lerp(overlayCol, col, _UseTex);

                // 다 접혀 남은 가로줄은 하얗게 빛나게
                openCol = lerp(openCol, _LineColor.rgb * 1.6, lineness);
                openAlpha = lerp(openAlpha, 1.0, lineness);

                // 열린 테두리(위·아래)에 얇은 빛
                float edge = (1.0 - smoothstep(0.0, 3.0 * px.y, abs(abs(c.y) - halfY))) * insideX * (1.0 - _UseTex) * step(0.001, 1.0 - _OpenY);
                openCol = lerp(openCol, _LineColor.rgb, edge);
                openAlpha = max(openAlpha, edge * 0.9);

                fixed4 result;
                result.rgb = lerp(fixed3(0, 0, 0), openCol, inside);
                result.a = lerp(1.0, openAlpha, inside);
                return result * IN.color;
            }
        ENDCG
        }
    }
}
