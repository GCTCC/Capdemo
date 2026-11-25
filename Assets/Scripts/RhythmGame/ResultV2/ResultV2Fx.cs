using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 결과 화면 v2(시안 v19)에서 쓰는 색, 움직임 공식, 코드로 만드는 그림 도우미.
// 그림은 PNG 없이 실행할 때 만들고, 결과 화면이 닫히면 ReleaseAll()로 지운다.
// 좌표는 시안과 같은 1600 x 900 (왼쪽 위 기준, 아래로 +).
public static class ResultV2Fx
{
    // ── 색 (시안 팔레트) ──
    public static readonly Color Bg = IngameV2Art.Hex(0x0A1413);
    public static readonly Color Fg = IngameV2Art.Hex(0xD9EFE9);
    public static readonly Color Dim = IngameV2Art.Hex(0x7F9F98);
    public static readonly Color Mint = IngameV2Art.Hex(0x7FE8CF);
    public static readonly Color MintHi = IngameV2Art.Hex(0xC6FBEC);
    public static readonly Color MintDeep = IngameV2Art.Hex(0x2F8F7C);
    public static readonly Color Panel = new Color(8 / 255f, 28 / 255f, 26 / 255f, 0.62f);
    public static readonly Color Line = new Color(127 / 255f, 232 / 255f, 207 / 255f, 0.35f);
    public static readonly Color RedGhost = IngameV2Art.Hex(0xE06A86);
    public static readonly Color Gold = IngameV2Art.Hex(0xFFD36B);
    public static readonly Color NormalDiff = IngameV2Art.Hex(0x7DD3F0);
    public static readonly Color Perfect = IngameV2Art.Hex(0xEFFFFA);
    public static readonly Color Great = IngameV2Art.Hex(0x7FE8CF);
    public static readonly Color Good = IngameV2Art.Hex(0x3F9F8D);
    public static readonly Color Miss = IngameV2Art.Hex(0x7D8F8A);
    public static readonly Color DarkPanel = new Color(8 / 255f, 28 / 255f, 26 / 255f, 1f);

    public static Color A(Color c, float a)
    {
        c.a = a;
        return c;
    }

    // ── 움직임 공식 ──
    public static float Seg(float t, float a, float b) { return Mathf.Clamp01((t - a) / (b - a)); }
    public static float EaseOut(float u) { u = Mathf.Clamp01(u); return 1f - Mathf.Pow(1f - u, 3f); }
    public static float EaseInOut(float u) { u = Mathf.Clamp01(u); return u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) / 2f; }
    // 살짝 넘쳤다가 돌아오는 (오버슈트) 곡선
    public static float Back(float u, float s = 1.70158f)
    {
        u = Mathf.Clamp01(u) - 1f;
        return 1f + (s + 1f) * u * u * u + s * u * u;
    }

    // ── 만든 그림 정리 ──
    private static readonly List<Object> created = new List<Object>();

    public static T Keep<T>(T obj) where T : Object
    {
        if (obj != null)
        {
            obj.hideFlags = HideFlags.DontSave;
            created.Add(obj);
        }
        return obj;
    }

    public static void ReleaseAll()
    {
        for (int i = 0; i < created.Count; i++)
        {
            if (created[i] == null) continue;
            if (created[i] is RenderTexture rt) rt.Release();
            Object.Destroy(created[i]);
        }
        created.Clear();
    }

    public static Texture2D NewTex(int w, int h, TextureWrapMode wrap = TextureWrapMode.Clamp, FilterMode filter = FilterMode.Bilinear)
    {
        var tex = new Texture2D(Mathf.Max(1, w), Mathf.Max(1, h), TextureFormat.RGBA32, false);
        tex.wrapMode = wrap;
        tex.filterMode = filter;
        return Keep(tex);
    }

    // 픽셀마다 색을 정해 그린다. f(x, yTop) — yTop은 위에서부터 (픽셀 가운데 좌표)
    public static Texture2D Paint(int w, int h, System.Func<float, float, Color> f,
        TextureWrapMode wrap = TextureWrapMode.Clamp, FilterMode filter = FilterMode.Bilinear)
    {
        var tex = NewTex(w, h, wrap, filter);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float yTop = h - 1 - y + 0.5f;
            for (int x = 0; x < w; x++) px[y * w + x] = f(x + 0.5f, yTop);
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        return tex;
    }

    public static Sprite ToSprite(Texture2D tex, Vector4 border = default(Vector4))
    {
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, border);
        return Keep(sprite);
    }

    // 여러 지점 그라데이션 (pos 0~1, 색)
    public static Color Sample(float u, params (float pos, Color c)[] stops)
    {
        if (stops.Length == 0) return Color.clear;
        if (u <= stops[0].pos) return stops[0].c;
        for (int i = 1; i < stops.Length; i++)
        {
            if (u <= stops[i].pos)
            {
                float k = (u - stops[i - 1].pos) / Mathf.Max(0.0001f, stops[i].pos - stops[i - 1].pos);
                return Color.Lerp(stops[i - 1].c, stops[i].c, k);
            }
        }
        return stops[stops.Length - 1].c;
    }

    // 가로 그라데이션 (256 x 1)
    public static Texture2D GradientH(params (float pos, Color c)[] stops)
    {
        return Paint(256, 1, (x, y) => Sample(x / 256f, stops));
    }

    // 세로 그라데이션 (1 x 256, 위 → 아래)
    public static Texture2D GradientV(params (float pos, Color c)[] stops)
    {
        return Paint(1, 256, (x, y) => Sample(y / 256f, stops));
    }

    // 반복 무늬: 가로로 period 칸 중 앞 on 칸만 색 (점선, 세로 줄무늬). RawImage uvRect로 반복해서 깐다
    public static Texture2D TileX(int period, int on, Color color)
    {
        return Paint(period, 1, (x, y) => x < on ? color : Color.clear, TextureWrapMode.Repeat, FilterMode.Point);
    }

    // 반복 무늬: 세로로 period 칸 중 위 on 칸만 색 (스캔라인, 가로 줄무늬)
    public static Texture2D TileY(int period, int on, Color color)
    {
        return Paint(1, period, (x, y) => y < on ? color : Color.clear, TextureWrapMode.Repeat, FilterMode.Point);
    }

    // 사선 줄무늬 타일 (45도)
    public static Texture2D DiagonalTile(int period, int on, Color color)
    {
        return Paint(period, period, (x, y) => ((int)(x + y) % period) < on ? color : Color.clear,
            TextureWrapMode.Repeat, FilterMode.Point);
    }

    // 가장자리가 부드럽게 사라지는 사각형 (글로우, 그림자). 9칸 늘이기(Sliced)용 테두리 포함
    public static Sprite SoftRect(int blur)
    {
        int size = blur * 2 + 4;
        var tex = Paint(size, size, (x, y) =>
        {
            float dx = Mathf.Max(0f, Mathf.Max(blur - x, x - (size - blur)));
            float dy = Mathf.Max(0f, Mathf.Max(blur - y, y - (size - blur)));
            float d = Mathf.Sqrt(dx * dx + dy * dy) / blur;
            float a = Mathf.Clamp01(1f - d);
            return new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
        });
        return ToSprite(tex, new Vector4(blur + 1, blur + 1, blur + 1, blur + 1));
    }

    // 기울어진 사각형 안쪽 판정 (CSS skewX(-deg): 위가 오른쪽으로). 0~1 가장자리 부드럽게
    public static float SkewInside(float x, float yTop, float w, float h, float slant)
    {
        // 박스 가운데 기준으로 위는 +slant/2, 아래는 -slant/2 만큼 밀린다. 텍스처 폭 = w + slant
        float shift = (0.5f - yTop / h) * slant + slant * 0.5f;
        float left = shift, right = shift + w;
        float d = Mathf.Min(x - left, right - x);
        float dy = Mathf.Min(yTop, h - yTop);
        return Mathf.Clamp01(d + 0.5f) * Mathf.Clamp01(dy + 0.5f);
    }

    public static float SkewLeft(float yTop, float h, float slant)
    {
        return (0.5f - yTop / h) * slant + slant * 0.5f;
    }

    // ── 글자 ──
    // 글자 안은 비우고 테두리만 그리는 머티리얼 (워터마크, 흐르는 큰 글씨)
    public static Material OutlineMaterial(TMP_FontAsset font, Color outline, float width)
    {
        if (font == null || font.material == null) return null;
        var mat = Keep(new Material(font.material));
        mat.SetColor("_FaceColor", new Color(1f, 1f, 1f, 0f));
        mat.SetColor("_OutlineColor", outline);
        mat.SetFloat("_OutlineWidth", width);
        mat.SetFloat("_OutlineSoftness", 0f);
        return mat;
    }

    // 같은 글자 하나를 계속 이어 붙여 width 이상 되게 만든다. 반복 한 칸의 폭을 돌려준다
    public static float FillRepeated(TextMeshProUGUI text, string unit, float minWidth, out int repeats)
    {
        float unitWidth = Mathf.Max(1f, text.GetPreferredValues(unit, 99999f, 9999f).x);
        repeats = Mathf.CeilToInt(minWidth / unitWidth) + 2;
        if (repeats % 2 == 1) repeats++;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < repeats; i++) sb.Append(unit);
        text.text = sb.ToString();
        return unitWidth;
    }

    // ── 화면 배치 도우미 (부모 왼쪽 위 기준 픽셀) ──
    public static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var rt = IngameV2Art.NewRect(name, parent);
        IngameV2Art.Place(rt, x, y, w, h);
        return rt;
    }

    public static RawImage Raw(string name, Transform parent, Texture tex, Color color, float x, float y, float w, float h)
    {
        var rt = Rect(name, parent, x, y, w, h);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static Image Img(string name, Transform parent, Sprite sprite, Color color, float x, float y, float w, float h)
    {
        var img = IngameV2Art.Img(name, parent, sprite, color);
        IngameV2Art.Place(img.rectTransform, x, y, w, h);
        return img;
    }

    // 반복 타일 RawImage: 실제 크기에 맞춰 uv를 늘린다
    public static RawImage Tiled(string name, Transform parent, Texture2D tile, Color color, float x, float y, float w, float h)
    {
        var img = Raw(name, parent, tile, color, x, y, w, h);
        img.uvRect = new UnityEngine.Rect(0, 0, w / tile.width, h / tile.height);
        return img;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
        TextAlignmentOptions align, float x, float y, float w, float h, float spacing = 0f, bool italic = false)
    {
        var t = IngameV2Art.Text(name, parent, font, text, size, color, align, spacing, italic);
        IngameV2Art.Place(t.rectTransform, x, y, w, h);
        return t;
    }

    public static float Width(TextMeshProUGUI t)
    {
        return t.GetPreferredValues(t.text, 99999f, 9999f).x;
    }
}

// 재킷 자리의 지직거리는 노이즈 (190 x 190 그림을 매 프레임 다시 그린다)
public class ResultV2Noise
{
    public const int Size = 190;
    public readonly Texture2D Texture;
    private readonly Color32[] px = new Color32[Size * Size];
    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    private static readonly Color32 MintHi = new Color32(198, 251, 236, 255);
    private static readonly Color32 Mint = new Color32(127, 232, 207, 255);
    private static readonly Color32 Reddish = new Color32(255, 90, 120, 230);
    private static readonly Color32 Band = new Color32(220, 255, 245, 90);

    public ResultV2Noise()
    {
        Texture = ResultV2Fx.NewTex(Size, Size, TextureWrapMode.Clamp, FilterMode.Point);
        Fill(Clear);
        Apply();
    }

    private void Fill(Color32 c)
    {
        for (int i = 0; i < px.Length; i++) px[i] = c;
    }

    private void Apply()
    {
        Texture.SetPixels32(px);
        Texture.Apply(false, false);
    }

    // k = 0~1 진행도: 가로 막대가 점점 많아지고 퍼진다 + 모래 점
    public void DrawStatic(float k)
    {
        Fill(Clear);
        int bars = 4 + Mathf.RoundToInt(k * k * 38f);
        float spread = Size * (0.08f + k * 0.42f);
        for (int b = 0; b < bars; b++)
        {
            int w = Mathf.RoundToInt(Size * Random.Range(0.10f, 0.95f));
            int h = Random.Range(1, 5);
            int x0 = Random.Range(0, Size - w + 1);
            int yc = Mathf.RoundToInt(Size * 0.5f + Random.Range(-spread, spread));
            Color32 c = Random.value < 0.15f ? Reddish : (Random.value < 0.5f ? MintHi : Mint);
            for (int y = yc; y < yc + h; y++)
            {
                if (y < 0 || y >= Size) continue;
                for (int x = x0; x < x0 + w; x++) px[y * Size + x] = c;
            }
        }
        int dots = 60 + Mathf.RoundToInt(k * 500f);
        for (int d = 0; d < dots; d++)
        {
            int x = Random.Range(0, Size);
            int y = Mathf.Clamp(Mathf.RoundToInt(Size * 0.5f + Random.Range(-spread * 1.3f, spread * 1.3f)), 0, Size - 1);
            px[y * Size + x] = Random.value < 0.5f ? MintHi : Mint;
        }
        if (k > 0.88f)
        {
            int y0 = Mathf.RoundToInt(Size * 0.38f), y1 = Mathf.RoundToInt(Size * 0.62f);
            for (int y = y0; y < y1; y++)
                for (int x = 0; x < Size; x++)
                    px[y * Size + x] = Blend(px[y * Size + x], Band);
        }
        Apply();
    }

    // 재킷이 켜진 뒤 남는 아주 옅은 알갱이
    public void DrawGrain()
    {
        for (int i = 0; i < px.Length; i++)
        {
            float r = Random.value;
            px[i] = r < 0.06f ? MintHi : (r < 0.1f ? Mint : Clear);
        }
        Apply();
    }

    private static Color32 Blend(Color32 under, Color32 over)
    {
        float a = over.a / 255f;
        if (under.a == 0) return over;
        return new Color32(
            (byte)Mathf.Lerp(under.r, over.r, a), (byte)Mathf.Lerp(under.g, over.g, a),
            (byte)Mathf.Lerp(under.b, over.b, a), (byte)Mathf.Max(under.a, over.a));
    }
}
