using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 인게임 v2(시안 E4)에서 쓰는 색, 글꼴, 코드로 만드는 그림을 한곳에 모은 도우미.
// 그림(유리 패널, 평행사변형, 빛 번짐 등)은 PNG 파일 없이 실행할 때 코드로 한 번만 만든다.
// 화면 좌표는 1920 x 1080 캔버스 기준이다. 시안(1600 x 900) 값에 1.2를 곱해 쓴다.
public static class IngameV2Art
{
    public const float K = 1.2f; // 시안 좌표 → 캔버스 좌표

    // ── 색 ──
    public static readonly Color Mint = Hex(0xA3EBD6);
    public static readonly Color MintLight = Hex(0xEFFFFB);
    public static readonly Color Red = Hex(0xFF4D5E);
    public static readonly Color Cyan = Hex(0x5AE8FF);
    public static readonly Color TapNote = Hex(0xC9FFF0);
    public static readonly Color HoldNote = Hex(0xFF4D5E);
    public static readonly Color TextDim = new Color(0.66f, 0.78f, 0.75f, 1f);
    public static readonly Color Ink = new Color(0.02f, 0.06f, 0.06f, 1f);
    public static readonly Color GoodGray = new Color(0.78f, 0.82f, 0.81f, 1f);

    public static Color Hex(int rgb, float a = 1f)
    {
        return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
    }

    public static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    public static Color DifficultyColor(string difficulty)
    {
        switch ((difficulty ?? "").ToUpperInvariant())
        {
            case "EASY": return Hex(0x7FE0A8);
            case "HARD": return Hex(0xFF4D5E);
            case "EXPERT": return Hex(0xC48BFF);
            default: return Hex(0x7FD4FF);
        }
    }

    // ── 글꼴 (TTF에서 실행 중에 TMP 글꼴을 만든다) ──
    private static readonly Dictionary<Font, TMP_FontAsset> fontCache = new Dictionary<Font, TMP_FontAsset>();
    private static TMP_FontAsset koreanFallback;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        // 도메인 리로드가 꺼져 있어서 지난 Play에서 만든 글꼴·그림을 버리고 새로 만든다
        fontCache.Clear();
        koreanFallback = null;
        textureCache.Clear();
        spriteCache.Clear();
    }

    public static void SetKoreanFallback(Font korean)
    {
        if (korean != null) koreanFallback = MakeFont(korean, null);
    }

    public static TMP_FontAsset MakeFont(Font font, TMP_FontAsset fallback)
    {
        if (font == null) return fallback != null ? fallback : TMP_Settings.defaultFontAsset;
        TMP_FontAsset asset;
        if (fontCache.TryGetValue(font, out asset) && asset != null) return asset;
        asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
        asset.name = font.name + " (IngameV2)";
        if (koreanFallback != null && koreanFallback != asset)
        {
            asset.fallbackFontAssetTable = new List<TMP_FontAsset> { koreanFallback };
        }
        fontCache[font] = asset;
        return asset;
    }

    // ── 그림 ──
    private static readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    private static Texture2D NewTexture(int w, int h, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = wrap;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.DontSave;
        return tex;
    }

    private static Sprite ToSprite(Texture2D tex, string key, Vector4 border = default(Vector4))
    {
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, border);
        sprite.name = key;
        sprite.hideFlags = HideFlags.DontSave;
        spriteCache[key] = sprite;
        return sprite;
    }

    // 유리 홀로 패널: 민트 가로 그라데이션(0.12→0.03), 테두리 0.32, 1px/3px 스캔라인, 오른쪽 위·왼쪽 아래 12px 사선
    public static Sprite GlassPanel(int w, int h, float cut = 14f)
    {
        string key = "glass_" + w + "x" + h + "_" + cut;
        Sprite cached;
        if (spriteCache.TryGetValue(key, out cached) && cached) return cached;

        var tex = NewTexture(w, h);
        var px = new Color[w * h];
        Color mint = Mint;
        for (int y = 0; y < h; y++)
        {
            int yTop = h - 1 - y; // 위에서부터
            for (int x = 0; x < w; x++)
            {
                float inside = ChamferInside(x + 0.5f, yTop + 0.5f, w, h, cut);
                if (inside <= 0f) { px[y * w + x] = Color.clear; continue; }
                float edge = ChamferEdgeDistance(x + 0.5f, yTop + 0.5f, w, h, cut);
                float fill = Mathf.Lerp(0.12f, 0.03f, x / (float)(w - 1));
                if (yTop % 3 == 0) fill += 0.035f; // 안쪽 스캔라인
                float a;
                Color c;
                if (edge < 1.3f)
                {
                    a = 0.32f * Mathf.Clamp01(inside);
                    c = mint;
                }
                else
                {
                    // 검은 바탕을 살짝 깔아 글씨가 읽히게 한다
                    float darkA = 0.55f;
                    float mintA = fill;
                    a = darkA + mintA * (1f - darkA);
                    c = Color.Lerp(new Color(0.01f, 0.04f, 0.04f), mint, mintA / Mathf.Max(0.001f, a));
                }
                c.a = a;
                px[y * w + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return ToSprite(tex, key);
    }

    // 평행사변형 (태그, 버튼, LIFE 칸)
    public static Sprite Parallelogram(int w, int h, float slant, bool outlineOnly = false)
    {
        string key = "para_" + w + "x" + h + "_" + slant + (outlineOnly ? "_o" : "");
        Sprite cached;
        if (spriteCache.TryGetValue(key, out cached) && cached) return cached;
        var tex = NewTexture(w, h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = y / (float)Mathf.Max(1, h - 1); // 0 아래, 1 위
            float left = slant * (1f - t);
            float right = w - slant * t;
            for (int x = 0; x < w; x++)
            {
                float fx = x + 0.5f;
                float d = Mathf.Min(fx - left, right - fx); // 가로 방향 안쪽 거리
                float dy = Mathf.Min(y + 0.5f, h - (y + 0.5f));
                float a = Mathf.Clamp01(d) * Mathf.Clamp01(dy * 1.5f);
                if (outlineOnly && d > 1.5f && dy > 1.5f) a = 0f;
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return ToSprite(tex, key);
    }

    // 부드러운 원 (빛 번짐, LED 점)
    public static Sprite SoftDot()
    {
        return RadialSprite("softdot", 64, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2f));
    }

    // 가운데가 밝은 가로 빛 띠 (글로우 선). 세로로만 흐려진다
    public static Sprite SoftBar()
    {
        string key = "softbar";
        Sprite cached;
        if (spriteCache.TryGetValue(key, out cached) && cached) return cached;
        const int w = 8, h = 32;
        var tex = NewTexture(w, h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float d = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
            float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
            for (int x = 0; x < w; x++) px[y * w + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return ToSprite(tex, key);
    }

    // 위에서 아래로 퍼지는 흐린 사다리꼴 빔 (스포트라이트)
    public static Sprite Beam()
    {
        string key = "beam";
        Sprite cached;
        if (spriteCache.TryGetValue(key, out cached) && cached) return cached;
        const int w = 128, h = 256;
        var tex = NewTexture(w, h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = y / (float)(h - 1); // 0 아래, 1 위
            float half = Mathf.Lerp(0.48f, 0.10f, t);
            float vertical = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2.2f)) * Mathf.Lerp(0.75f, 1f, t);
            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / w - 0.5f);
                float a = Mathf.Clamp01((half - u) / (half * 0.55f)); // 가장자리 흐림(블러)
                a = a * a * (3f - 2f * a) * vertical;
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return ToSprite(tex, key);
    }

    // 별 ★ (글꼴에 없을 수 있어서 그림으로)
    public static Sprite Star()
    {
        string key = "star";
        Sprite cached;
        if (spriteCache.TryGetValue(key, out cached) && cached) return cached;
        const int s = 64;
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float ang = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
            float r = i % 2 == 0 ? 0.48f : 0.2f;
            pts[i] = new Vector2(0.5f + Mathf.Cos(ang) * r, 0.5f + Mathf.Sin(ang) * r);
        }
        var tex = NewTexture(s, s);
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                // 4x 슈퍼샘플로 가장자리를 부드럽게
                int hit = 0;
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                        if (InPolygon(new Vector2((x + 0.25f + sx * 0.5f) / s, (y + 0.25f + sy * 0.5f) / s), pts)) hit++;
                px[y * s + x] = new Color(1f, 1f, 1f, hit / 4f);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return ToSprite(tex, key);
    }

    private static bool InPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y))
                && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                inside = !inside;
        }
        return inside;
    }

    // 가장자리가 어두운 비네트
    public static Sprite Vignette()
    {
        string key = "vignette";
        Sprite cached;
        if (spriteCache.TryGetValue(key, out cached) && cached) return cached;
        const int w = 128, h = 72;
        var tex = NewTexture(w, h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = ((x + 0.5f) / w - 0.5f) * 2f;
                float dy = ((y + 0.5f) / h - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 0.9f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((r - 0.45f) / 0.85f)) * 0.85f;
                // 위아래 어두운 그라데이션
                float ty = (y + 0.5f) / h;
                float top = Mathf.Clamp01((ty - 0.78f) / 0.22f);
                float bottom = Mathf.Clamp01((0.30f - ty) / 0.30f);
                a = 1f - (1f - a) * (1f - top * 0.75f) * (1f - bottom * 0.85f);
                px[y * w + x] = new Color(0f, 0.012f, 0.012f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return ToSprite(tex, key);
    }

    // 화면 전체 스캔라인 (1px 선, 3px 간격). RawImage uvRect로 반복해서 깐다
    public static Texture2D ScanlineTile(float alpha)
    {
        string key = "scan_" + alpha;
        Texture2D cached;
        if (textureCache.TryGetValue(key, out cached) && cached) return cached;
        var tex = NewTexture(1, 3, TextureWrapMode.Repeat);
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(new[] { new Color(0, 0, 0, alpha), Color.clear, Color.clear });
        tex.Apply(false, true);
        textureCache[key] = tex;
        return tex;
    }

    // 3D 하이웨이용: 가로로 가운데가 밝은 선 단면 (U 방향). 레일·판정선 글로우
    public static Texture2D GlowProfile()
    {
        string key = "glowprofile";
        Texture2D cached;
        if (textureCache.TryGetValue(key, out cached) && cached) return cached;
        const int w = 64;
        var tex = NewTexture(w, 1);
        var px = new Color[w];
        for (int x = 0; x < w; x++)
        {
            float d = Mathf.Abs((x + 0.5f) / w - 0.5f) * 2f;
            px[x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2.4f));
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        textureCache[key] = tex;
        return tex;
    }

    // 3D 바닥의 타원 링
    public static Texture2D RingTexture()
    {
        string key = "ring";
        Texture2D cached;
        if (textureCache.TryGetValue(key, out cached) && cached) return cached;
        const int s = 128;
        var tex = NewTexture(s, s);
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = ((x + 0.5f) / s - 0.5f) * 2f;
                float dy = ((y + 0.5f) / s - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.06f);
                float glow = Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.2f) * 0.35f;
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Max(ring, glow));
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        textureCache[key] = tex;
        return tex;
    }

    public static Texture2D SoftDotTexture()
    {
        return SoftDot().texture;
    }

    private static Sprite RadialSprite(string key, int s, System.Func<float, float> alpha)
    {
        Sprite cached;
        if (spriteCache.TryGetValue(key, out cached) && cached) return cached;
        var tex = NewTexture(s, s);
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dx = ((x + 0.5f) / s - 0.5f) * 2f;
                float dy = ((y + 0.5f) / s - 0.5f) * 2f;
                px[y * s + x] = new Color(1f, 1f, 1f, alpha(Mathf.Sqrt(dx * dx + dy * dy)));
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return ToSprite(tex, key);
    }

    // 오른쪽 위·왼쪽 아래가 잘린 사각형 안쪽이면 1 (가장자리는 0~1로 부드럽게)
    private static float ChamferInside(float x, float yTop, float w, float h, float cut)
    {
        return Mathf.Clamp01(ChamferEdgeDistance(x, yTop, w, h, cut) + 0.5f);
    }

    private static float ChamferEdgeDistance(float x, float yTop, float w, float h, float cut)
    {
        float d = Mathf.Min(Mathf.Min(x, w - x), Mathf.Min(yTop, h - yTop));
        // 오른쪽 위 사선: (w - cut, 0) ~ (w, cut)
        float tr = ((w - x) + yTop - cut) * 0.70710678f;
        // 왼쪽 아래 사선: (0, h - cut) ~ (cut, h)
        float bl = (x + (h - yTop) - cut) * 0.70710678f;
        return Mathf.Min(d, Mathf.Min(tr, bl));
    }

    // ── UI 조립 도우미 (왼쪽 위 기준 픽셀 좌표) ──
    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 가운데 기준 배치 (x, y는 중심점)
    public static void PlaceCenter(RectTransform rt, float cx, float cy, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(cx, -cy);
        rt.sizeDelta = new Vector2(w, h);
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public static Image Img(string name, Transform parent, Sprite sprite, Color color)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string text, float size,
        Color color, TextAlignmentOptions align, float spacing = 0f, bool italic = false)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.characterSpacing = spacing;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        if (italic) t.fontStyle |= FontStyles.Italic;
        return t;
    }

    // 글자 뒤에 같은 색 번짐(글로우)을 주는 머티리얼. 글꼴마다 하나씩 만든다
    private static readonly Dictionary<string, Material> glowMaterials = new Dictionary<string, Material>();

    public static void Glow(TextMeshProUGUI text, Color glowColor, float softness = 0.6f)
    {
        if (!text || !text.font) return;
        string key = text.font.GetHashCode() + "_" + ColorUtility.ToHtmlStringRGBA(glowColor) + "_" + softness;
        Material mat;
        if (!glowMaterials.TryGetValue(key, out mat) || !mat)
        {
            mat = new Material(text.font.material);
            mat.hideFlags = HideFlags.DontSave;
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", glowColor);
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", 0f);
            mat.SetFloat("_UnderlayDilate", 0.35f);
            mat.SetFloat("_UnderlaySoftness", softness);
            glowMaterials[key] = mat;
        }
        text.fontSharedMaterial = mat;
    }

    // 빨강(-2px)·시안(+2px)으로 번진 RGB 분리 글자 3장을 겹쳐 만든다. 맨 위 흰 글자를 돌려준다
    public static TextMeshProUGUI GlitchText(string name, Transform parent, TMP_FontAsset font, string text, float size,
        Color color, TextAlignmentOptions align, float split, bool italic, float spacing,
        out TextMeshProUGUI red, out TextMeshProUGUI cyan)
    {
        var root = NewRect(name, parent);
        red = Text("R", root, font, text, size, WithAlpha(Red, 0.75f), align, spacing, italic);
        cyan = Text("C", root, font, text, size, WithAlpha(Cyan, 0.75f), align, spacing, italic);
        var main = Text("Main", root, font, text, size, color, align, spacing, italic);
        Stretch(red.rectTransform);
        Stretch(cyan.rectTransform);
        Stretch(main.rectTransform);
        red.rectTransform.anchoredPosition = new Vector2(-split, 0f);
        cyan.rectTransform.anchoredPosition = new Vector2(split, 0f);
        return main;
    }
}
