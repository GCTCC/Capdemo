using System.Collections.Generic;
using UnityEngine;

// 전환 연출에 쓰는 그림을 코드로 한 번만 만든다 (PNG 없이).
// 레코드 홈, 광택, 격자, 비네트, 디스크 빈자리, 스캔 띠, 플래시, 부드러운 사각 그림자, 작은 기호, 드럼 클릭음.
public static class TransitionArt
{
    private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
    private static AudioClip click;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        cache.Clear();
        click = null;
    }

    private static Texture2D New(int w, int h, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = wrap;
        t.filterMode = FilterMode.Bilinear;
        t.hideFlags = HideFlags.DontSave;
        return t;
    }

    private static bool TryGet(string key, out Texture2D tex)
    {
        return cache.TryGetValue(key, out tex) && tex != null;
    }

    private static Texture2D Done(string key, Texture2D tex, Color[] px)
    {
        tex.SetPixels(px);
        tex.Apply(false, true);
        cache[key] = tex;
        return tex;
    }

    private static Color C(int rgb, float a = 1f)
    {
        return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
    }

    // 레코드판 홈: 가는 동심원 + 라벨 바깥 홈 링 (판 지름 400px 기준 값을 비율로)
    public static Texture2D Grooves()
    {
        Texture2D tex;
        if (TryGet("grooves", out tex)) return tex;
        const int s = 512;
        tex = New(s, s);
        var px = new Color[s * s];
        Color a = C(0x0b0c0d), b = C(0x16191a), c = C(0x0a0b0c);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy); // 0 ~ 0.5 (판 가장자리)
                float rPct = r * 2f;                       // 0 ~ 1 (CSS의 %와 같게: 지름 대비)
                Color col;
                if (rPct < 0.36f) col = new Color(0, 0, 0, 0);
                else if (rPct < 0.368f) col = C(0x0d0f10);
                else if (rPct < 0.39f) col = C(0x08090a);
                else
                {
                    float rpx = r * 400f; // 판 반지름 200px 기준
                    float m = rpx % 3.6f;
                    col = m < 1.6f ? a : (m < 2.4f ? b : c);
                }
                px[y * s + x] = col;
            }
        }
        return Done("grooves", tex, px);
    }

    // 고정 광택: 원뿔형 그라데이션 (흰 0.16 / 민트 0.14). CSS conic-gradient(from 20deg …)
    public static Texture2D Sheen()
    {
        Texture2D tex;
        if (TryGet("sheen", out tex)) return tex;
        const int s = 256;
        tex = New(s, s);
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s - 0.5f, dy = 0.5f - (y + 0.5f) / s; // y 위로 +
                // 12시에서 시계방향 각도 (0~1), from 20deg
                float ang = Mathf.Atan2(dx, dy) / (Mathf.PI * 2f);
                if (ang < 0) ang += 1f;
                float u = ang - 20f / 360f;
                if (u < 0) u += 1f;
                Color col = new Color(0, 0, 0, 0);
                col = Stop(u, 0.08f, 0.12f, 0.17f, new Color(1, 1, 1, 0.16f), col);
                col = Stop(u, 0.50f, 0.60f, 0.66f, new Color(0.64f, 0.92f, 0.84f, 0.14f), col);
                px[y * s + x] = col;
            }
        }
        return Done("sheen", tex, px);
    }

    private static Color Stop(float u, float a, float peak, float b, Color c, Color current)
    {
        if (u <= a || u >= b) return current;
        float k = u < peak ? (u - a) / (peak - a) : 1f - (u - peak) / (b - peak);
        Color r = c;
        r.a *= Mathf.Clamp01(k);
        return r.a > current.a ? r : current;
    }

    // 아주 옅은 민트 격자 (40px 간격, 알파 0.05) + 가운데만 보이는 타원 마스크. 1600x900 그대로
    public static Texture2D Grid()
    {
        Texture2D tex;
        if (TryGet("grid", out tex)) return tex;
        const int w = 1600, h = 900;
        tex = New(w, h);
        var px = new Color32[w * h]; // 기본값 투명
        // 선이 있는 픽셀만 계산한다 (전체를 돌면 느려서 화면이 멈칫한다)
        for (int y = 0; y < h; y++)
        {
            int yTop = h - 1 - y;
            float my = ((yTop + 0.5f) / h - 0.5f) / 0.55f;
            bool rowLine = yTop % 40 == 0;
            for (int x = rowLine ? 0 : 0; x < w; x += rowLine ? 1 : 40)
            {
                float mx = ((x + 0.5f) / w - 0.5f) / 0.55f;
                float mask = 1f - Mathf.Sqrt(mx * mx + my * my);
                if (mask <= 0f) continue;
                px[y * w + x] = new Color32(163, 235, 214, (byte)Mathf.RoundToInt(255 * 0.05f * mask));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        cache["grid"] = tex;
        return tex;
    }

    // 어두운 비네트: 가운데(45%, 48%) 알파 0.55 → 가장자리 0.94
    public static Texture2D Dim()
    {
        Texture2D tex;
        if (TryGet("dim", out tex)) return tex;
        const int w = 320, h = 180;
        tex = New(w, h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = ((x + 0.5f) / w - 0.45f) / 0.6f;
                float dy = ((1f - (y + 0.5f) / h) - 0.48f) / 0.6f;
                float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                px[y * w + x] = new Color(3 / 255f, 10 / 255f, 11 / 255f, Mathf.Lerp(0.55f, 0.94f, r));
            }
        }
        return Done("dim", tex, px);
    }

    // 원래 디스크 자리: 어두운 원 + 홀로 링 테두리
    public static Texture2D Hole()
    {
        Texture2D tex;
        if (TryGet("hole", out tex)) return tex;
        const int s = 256;
        tex = New(s, s);
        var px = new Color[s * s];
        Color dark = new Color(3 / 255f, 10 / 255f, 11 / 255f, 1f);
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s - 0.5f, dy = (y + 0.5f) / s - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f; // 0~1 (반지름 비율)
                Color col;
                if (r < 0.70f) col = new Color(dark.r, dark.g, dark.b, Mathf.Lerp(0.95f, 0.85f, r / 0.7f));
                else if (r < 0.71f) col = new Color(dark.r, dark.g, dark.b, 0.85f);
                else if (r < 0.74f) col = new Color(0.64f, 0.92f, 0.84f, 0.25f * (1f - (r - 0.71f) / 0.03f));
                else col = new Color(0, 0, 0, 0);
                px[y * s + x] = col;
            }
        }
        return Done("hole", tex, px);
    }

    // 스캔 띠 (세로 70px): 투명 → 민트 0.16(70%) → 밝은 민트 0.95(96%) → 투명
    public static Texture2D Sweep()
    {
        Texture2D tex;
        if (TryGet("sweep", out tex)) return tex;
        const int h = 64;
        tex = New(1, h);
        var px = new Color[h];
        for (int y = 0; y < h; y++)
        {
            float u = 1f - (y + 0.5f) / h; // 0 위, 1 아래
            Color c;
            if (u < 0.7f) c = new Color(0.64f, 0.92f, 0.84f, Mathf.Lerp(0f, 0.16f, u / 0.7f));
            else if (u < 0.96f) c = Color.Lerp(new Color(0.64f, 0.92f, 0.84f, 0.16f), new Color(0.86f, 1f, 0.96f, 0.95f), (u - 0.7f) / 0.26f);
            else c = Color.Lerp(new Color(0.86f, 1f, 0.96f, 0.95f), new Color(0.86f, 1f, 0.96f, 0f), (u - 0.96f) / 0.04f);
            px[y] = c;
        }
        return Done("sweep", tex, px);
    }

    // 첫 박 플래시: 아래쪽(50%, 70%)이 가장 밝은 방사형 (흰 → 연민트 → 민트 0.4)
    public static Texture2D Flash()
    {
        Texture2D tex;
        if (TryGet("flash", out tex)) return tex;
        const int w = 256, h = 144;
        tex = New(w, h);
        var px = new Color[w * h];
        Color a = Color.white, b = new Color(0.86f, 1f, 0.96f, 0.8f), c = new Color(0.64f, 0.92f, 0.84f, 0.4f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = ((x + 0.5f) / w - 0.5f) / 0.7f;
                float dy = ((1f - (y + 0.5f) / h) - 0.7f) / 0.6f;
                float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                px[y * w + x] = r < 0.5f ? Color.Lerp(a, b, r / 0.5f) : Color.Lerp(b, c, (r - 0.5f) / 0.5f);
            }
        }
        return Done("flash", tex, px);
    }

    // 가장자리가 부드러운 사각 (재킷 그림자·글로우)
    public static Texture2D SoftSquare()
    {
        Texture2D tex;
        if (TryGet("softsq", out tex)) return tex;
        const int s = 128;
        tex = New(s, s);
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / s - 0.5f), dy = Mathf.Abs((y + 0.5f) / s - 0.5f);
                float d = Mathf.Max(dx, dy); // 0 가운데 ~ 0.5 끝
                float a = Mathf.Clamp01(1f - Mathf.Max(0f, d - 0.25f) / 0.25f);
                px[y * s + x] = new Color(1, 1, 1, a * a);
            }
        }
        return Done("softsq", tex, px);
    }

    // 작은 오른쪽 삼각형 ▶ (글꼴에 없을 수 있어서)
    public static Texture2D TriangleRight()
    {
        Texture2D tex;
        if (TryGet("tri", out tex)) return tex;
        const int s = 32;
        tex = New(s, s);
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                float half = 0.5f * (1f - u);
                float d = Mathf.Min(half - Mathf.Abs(v - 0.5f), u - 0.08f) * s;
                px[y * s + x] = new Color(1, 1, 1, Mathf.Clamp01(d + 0.5f));
            }
        }
        return Done("tri", tex, px);
    }

    // 드럼 스틱 클릭 (임시): 짧은 잡음 + 높은 톤, 빠르게 사라짐
    public static AudioClip StickClick()
    {
        if (click != null) return click;
        int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
        int n = Mathf.RoundToInt(rate * 0.07f);
        var data = new float[n];
        var rnd = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t * 90f);
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float tone = Mathf.Sin(2f * Mathf.PI * 2300f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 3900f * t) * 0.3f;
            data[i] = (noise * 0.5f + tone) * env * 0.8f;
        }
        click = AudioClip.Create("Stick Click (temp)", n, 1, rate, false);
        click.SetData(data, 0);
        return click;
    }
}
