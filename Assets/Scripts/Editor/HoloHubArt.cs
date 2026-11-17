using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 홀로그램 허브에 쓰는 단순한 그림(모서리 잘린 패널, 그라데이션, 스캔라인, 아이콘 등)을 코드로 그려 PNG로 저장한다.
// HoloHubMenu(허브 씬 만들기)가 부른다. 다시 실행하면 같은 이름으로 덮어쓴다.
// 그림은 전부 흰색이라, Image의 Color로 원하는 색을 입힌다.
public static class HoloHubArt
{
    public const string Folder = "Assets/Art/UI/HoloHub/Generated/";

    // ── 그리기 판 ─────────────────────────────────────────────────
    // 좌표는 왼쪽 위가 (0,0)이고 아래로 갈수록 y가 커진다 (디자인 SVG와 같은 방향).
    private class Pad
    {
        public readonly int w, h;
        public readonly float[] a;      // 투명도 0~1
        public readonly float[] shade;  // 밝기 0~1 (기본 1 = 흰색)

        public Pad(int w, int h)
        {
            this.w = w; this.h = h;
            a = new float[w * h];
            shade = new float[w * h];
            for (int i = 0; i < shade.Length; i++) shade[i] = 1f;
        }

        // 각 픽셀 가운데에서 값을 구해 합친다 (op: 합치기 / 지우기)
        public void Each(Func<float, float, float> coverage, bool erase = false)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float c = Mathf.Clamp01(coverage(x + 0.5f, y + 0.5f));
                int i = y * w + x;
                a[i] = erase ? a[i] * (1f - c) : Mathf.Max(a[i], c);
            }
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                byte s = (byte)Mathf.RoundToInt(shade[i] * 255f);
                // 텍스처는 아래가 0이라 뒤집어 넣는다
                px[(h - 1 - y) * w + x] = new Color32(s, s, s, (byte)Mathf.RoundToInt(a[i] * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }

    // ── 모양 거리 함수 ─────────────────────────────────────────────

    private static float SegDist(float px, float py, Vector2 a, Vector2 b)
    {
        var p = new Vector2(px, py);
        var ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    // 다각형 안쪽이면 +, 바깥이면 - 인 가장자리까지 거리
    private static float PolySigned(float px, float py, Vector2[] pts)
    {
        float d = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
        {
            d = Mathf.Min(d, SegDist(px, py, pts[j], pts[i]));
            if ((pts[i].y > py) != (pts[j].y > py) &&
                px < (pts[j].x - pts[i].x) * (py - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                inside = !inside;
        }
        return inside ? d : -d;
    }

    private static void FillPoly(Pad pad, params Vector2[] pts) =>
        pad.Each((x, y) => 0.5f + PolySigned(x, y, pts));

    private static void StrokeLines(Pad pad, float width, bool closed, params Vector2[] pts)
    {
        pad.Each((x, y) =>
        {
            float d = float.MaxValue;
            int n = closed ? pts.Length : pts.Length - 1;
            for (int i = 0; i < n; i++) d = Mathf.Min(d, SegDist(x, y, pts[i], pts[(i + 1) % pts.Length]));
            return width * 0.5f + 0.5f - d;
        });
    }

    private static void Ring(Pad pad, Vector2 c, float r, float width) =>
        pad.Each((x, y) => width * 0.5f + 0.5f - Mathf.Abs(Vector2.Distance(new Vector2(x, y), c) - r));

    private static void Disc(Pad pad, Vector2 c, float r) =>
        pad.Each((x, y) => r + 0.5f - Vector2.Distance(new Vector2(x, y), c));

    private static Vector2 V(float x, float y) => new Vector2(x, y);

    // ── 저장 ─────────────────────────────────────────────────────

    private class Spec
    {
        public string name;
        public Pad pad;
        public int border;
        public bool repeat;
        public bool point;
    }

    private static readonly List<Spec> pending = new List<Spec>();

    private static void Add(string name, Pad pad, int border = 0, bool repeat = false, bool point = false) =>
        pending.Add(new Spec { name = name, pad = pad, border = border, repeat = repeat, point = point });

    public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + name + ".png");
    public static Texture2D LoadTexture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + name + ".png");

    // 모든 그림을 만들어 저장한다
    public static void GenerateAll()
    {
        Directory.CreateDirectory(Folder);
        pending.Clear();

        // 모서리 잘린 패널 (채움 + 테두리). 9-slice라 어떤 크기로 늘려도 모서리가 유지된다.
        MakePanel("panel_tr18", tl: false, tr: true, br: false, bl: false, cut: 18);
        MakePanel("panel_tlbr8", tl: true, tr: false, br: true, bl: false, cut: 8);
        MakePanel("panel_tlbr14", tl: true, tr: false, br: true, bl: false, cut: 14);
        MakePanel("panel_trbl14", tl: false, tr: true, br: false, bl: true, cut: 14);
        MakePanel("panel_br10", tl: false, tr: false, br: true, bl: false, cut: 10);

        MakeGradients();
        MakeTextures();
        MakeIcons();

        foreach (var s in pending) Save(s);
        pending.Clear();
    }

    private static void MakePanel(string name, bool tl, bool tr, bool br, bool bl, int cut)
    {
        const int S = 64;
        var pts = new List<Vector2>();
        if (tl) { pts.Add(V(0, cut)); pts.Add(V(cut, 0)); } else pts.Add(V(0, 0));
        if (tr) { pts.Add(V(S - cut, 0)); pts.Add(V(S, cut)); } else pts.Add(V(S, 0));
        if (br) { pts.Add(V(S, S - cut)); pts.Add(V(S - cut, S)); } else pts.Add(V(S, S));
        if (bl) { pts.Add(V(cut, S)); pts.Add(V(0, S - cut)); } else pts.Add(V(0, S));
        var poly = pts.ToArray();

        var fill = new Pad(S, S);
        FillPoly(fill, poly);
        Add(name, fill, cut + 2);

        const float lineWidth = 1.6f;
        var line = new Pad(S, S);
        line.Each((x, y) =>
        {
            float sd = PolySigned(x, y, poly);
            return Mathf.Clamp01(0.5f + sd) * Mathf.Clamp01(lineWidth + 0.5f - sd);
        });
        Add(name + "_line", line, cut + 2);
    }

    private static void MakeGradients()
    {
        // 왼쪽을 어둡게 (메뉴 뒤). 디자인의 linear-gradient(90deg, .92 → .80 → .35 → 0) 그대로
        var left = new Pad(256, 4);
        for (int y = 0; y < 4; y++)
        for (int x = 0; x < 256; x++)
        {
            float t = (x + 0.5f) / 256f;
            float v = t < 0.436f ? Mathf.Lerp(0.92f, 0.80f, t / 0.436f)
                : t < 0.727f ? Mathf.Lerp(0.80f, 0.35f, (t - 0.436f) / 0.291f)
                : Mathf.Lerp(0.35f, 0f, (t - 0.727f) / 0.273f);
            left.a[y * 256 + x] = v;
        }
        Add("grad_left", left);

        // 위가 진하고 아래로 사라지는 세로 그라데이션 (위·아래 어둡게에 같이 쓴다)
        var top = new Pad(4, 256);
        for (int y = 0; y < 256; y++)
        for (int x = 0; x < 4; x++)
        {
            float t = (y + 0.5f) / 256f;
            top.a[y * 4 + x] = (1f - t) * (1f - t);
        }
        Add("grad_top", top);

        // 가장자리 어둡게 (비네트)
        var vig = new Pad(256, 256);
        vig.Each((x, y) =>
        {
            float dx = (x / 256f - 0.5f) * 2f, dy = (y / 256f - 0.5f) * 2f;
            float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.45f, r));
        });
        Add("vignette", vig);
    }

    private static void MakeTextures()
    {
        // 스캔라인: 3줄 중 1줄만 칠한다 (반복해서 깐다)
        var scan = new Pad(4, 3);
        for (int x = 0; x < 4; x++) scan.a[x] = 1f;
        Add("scanline", scan, repeat: true, point: true);

        // 사선 줄무늬 (이벤트 카드 왼쪽 띠)
        var stripe = new Pad(32, 32);
        stripe.Each((x, y) =>
        {
            float u = Mathf.Repeat(x + y, 16f);
            float d = u < 8f ? Mathf.Min(u, 8f - u) : -Mathf.Min(u - 8f, 16f - u);
            return 0.5f + d;
        });
        Add("stripes", stripe, repeat: true);

        // 부드러운 빛 동그라미 (전구 빛, 먼지, 발광)
        var glow = new Pad(128, 128);
        glow.Each((x, y) =>
        {
            float dx = (x - 64f) / 64f, dy = (y - 64f) / 64f;
            float r2 = dx * dx + dy * dy;
            return Mathf.Exp(-r2 * 5f) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) * 4f);
        });
        Add("glow", glow);

        // 동그라미 (점, 배지)
        var dot = new Pad(32, 32);
        Disc(dot, V(16, 16), 15f);
        Add("dot", dot);

        // 라디오 홀로그램 빛기둥: 위가 넓고 아래가 좁은 사다리꼴, 아래가 진하다
        var beam = new Pad(256, 96);
        beam.Each((x, y) =>
        {
            float t = y / 96f;                  // 0 위 ~ 1 아래
            float inset = Mathf.Lerp(0f, 0.04f, t) * 256f;
            float edge = Mathf.Clamp01(Mathf.Min(x - inset, 256f - inset - x) / 10f);
            float v = t > 0.3f ? Mathf.Lerp(0.18f, 1f, (t - 0.3f) / 0.7f) : Mathf.Lerp(0f, 0.18f, t / 0.3f);
            return v * edge;
        });
        Add("beam", beam);

        // 반짝임 띠 (가운데가 밝고 양옆으로 사라짐)
        var sheen = new Pad(128, 8);
        sheen.Each((x, y) =>
        {
            float t = (x / 128f - 0.5f) / 0.22f;
            return Mathf.Exp(-t * t);
        });
        Add("sheen", sheen);

        // 프로필 육각형 (위가 밝은 그라데이션)
        var hex = new Pad(128, 128);
        FillPoly(hex, V(32, 0), V(96, 0), V(128, 64), V(96, 128), V(32, 128), V(0, 64));
        for (int y = 0; y < 128; y++)
        for (int x = 0; x < 128; x++)
            hex.shade[y * 128 + x] = Mathf.Lerp(1f, 0.35f, (x * 0.3f + y) / 166f);
        Add("hex", hex);

        // 프로필 사람 실루엣
        var avatar = new Pad(128, 128);
        Disc(avatar, V(64, 50), 23f);
        avatar.Each((x, y) =>
        {
            float dx = (x - 64f) / 42f, dy = (y - 126f) / 46f;
            float inside = 1f - Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(inside * 40f) * Mathf.Clamp01(118f - y);
        });
        Add("avatar", avatar);
    }

    private static void MakeIcons()
    {
        const float sw = 3.6f; // 선 굵기
        Pad P() => new Pad(64, 64);

        var play = P();
        FillPoly(play, V(20, 12), V(52, 32), V(20, 52));
        Add("icon_play", play);

        float k = 64f / 26f;
        var book = P();
        StrokeLines(book, sw, true, V(3 * k, 5 * k), V(11 * k, 5 * k), V(13 * k, 7 * k), V(13 * k, 22 * k), V(11 * k, 20 * k), V(3 * k, 20 * k));
        StrokeLines(book, sw, true, V(23 * k, 5 * k), V(15 * k, 5 * k), V(13 * k, 7 * k), V(13 * k, 22 * k), V(15 * k, 20 * k), V(23 * k, 20 * k));
        Add("icon_book", book);

        var disc = P();
        Ring(disc, V(32, 32), 24f, sw);
        Ring(disc, V(32, 32), 7f, sw);
        Add("icon_disc", disc);

        var sliders = P();
        foreach (float x in new[] { 12f, 32f, 52f }) StrokeLines(sliders, sw, false, V(x, 8), V(x, 56));
        foreach (var knob in new[] { V(12, 38), V(32, 21), V(52, 43) })
        {
            var r = new[] { V(knob.x - 7, knob.y - 4.5f), V(knob.x + 7, knob.y - 4.5f), V(knob.x + 7, knob.y + 4.5f), V(knob.x - 7, knob.y + 4.5f) };
            sliders.Each((x, y) => 0.5f + PolySigned(x, y, r), erase: true);
            StrokeLines(sliders, sw * 0.8f, true, r);
        }
        Add("icon_sliders", sliders);

        var bell = P();
        var bellPts = new List<Vector2> { V(13, 47), V(19, 41), V(19, 29) };
        for (int i = 0; i <= 12; i++)
        {
            float ang = Mathf.PI + Mathf.PI * i / 12f;
            bellPts.Add(V(32 + Mathf.Cos(ang) * 13f, 29 + Mathf.Sin(ang) * 15f));
        }
        bellPts.Add(V(45, 41));
        bellPts.Add(V(51, 47));
        StrokeLines(bell, sw, true, bellPts.ToArray());
        StrokeLines(bell, sw, false, V(32, 9), V(32, 13));
        Ring(bell, V(32, 53), 3.5f, sw * 0.8f);
        Add("icon_bell", bell);

        var mail = P();
        StrokeLines(mail, sw, true, V(9, 16), V(55, 16), V(55, 48), V(9, 48));
        StrokeLines(mail, sw, false, V(10, 18), V(32, 34), V(54, 18));
        Add("icon_mail", mail);

        var menu = P();
        foreach (float y in new[] { 20f, 32f, 44f }) StrokeLines(menu, sw, false, V(14, y), V(50, y));
        Add("icon_menu", menu);

        var ticket = P();
        StrokeLines(ticket, sw, true, V(8, 18), V(56, 18), V(56, 46), V(8, 46));
        for (float y = 22; y < 44; y += 7) StrokeLines(ticket, sw * 0.8f, false, V(22, y), V(22, y + 3.5f));
        Add("icon_ticket", ticket);

        var coin = P();
        var hexPts = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float ang = Mathf.PI / 6f + i * Mathf.PI / 3f;
            hexPts[i] = V(32 + Mathf.Cos(ang) * 22f, 32 + Mathf.Sin(ang) * 22f);
        }
        StrokeLines(coin, sw, true, hexPts);
        Ring(coin, V(32, 32), 7f, sw);
        Add("icon_coin", coin);

        var exit = P();
        StrokeLines(exit, sw, false, V(24, 12), V(12, 12), V(12, 52), V(24, 52));
        StrokeLines(exit, sw, false, V(40, 20), V(52, 32), V(40, 44));
        StrokeLines(exit, sw, false, V(52, 32), V(24, 32));
        Add("icon_exit", exit);

        var chevron = P();
        StrokeLines(chevron, sw * 1.1f, false, V(24, 14), V(42, 32), V(24, 50));
        Add("icon_chevron", chevron);

        var arrow = P();
        StrokeLines(arrow, sw * 1.2f, false, V(12, 32), V(52, 32));
        StrokeLines(arrow, sw * 1.2f, false, V(36, 16), V(52, 32), V(36, 48));
        Add("icon_arrow", arrow);

        // 홀로콜: 세로 폰 + 화면 속 글줄
        var phone = P();
        StrokeLines(phone, sw, true, V(17, 6), V(47, 6), V(47, 58), V(17, 58));
        StrokeLines(phone, sw * 0.9f, false, V(27, 50), V(37, 50));
        StrokeLines(phone, sw * 0.9f, false, V(24, 20), V(40, 20));
        StrokeLines(phone, sw * 0.9f, false, V(24, 30), V(35, 30));
        Add("icon_phone", phone);

        var speaker = P();
        FillPoly(speaker, V(10, 24), V(22, 24), V(36, 12), V(36, 52), V(22, 40), V(10, 40));
        Add("icon_speaker", speaker);
    }

    private static void Save(Spec s)
    {
        string path = Folder + s.name + ".png";
        var tex = s.pad.ToTexture();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = s.repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.filterMode = s.point ? FilterMode.Point : FilterMode.Bilinear;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = new Vector4(s.border, s.border, s.border, s.border);
        importer.SaveAndReimport();
    }
}
