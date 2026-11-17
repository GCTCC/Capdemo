using UnityEngine;

// 홀로그램 허브(목업 디자인)에서 쓰는 색 모음. 디자인 시안(hub_v4.html)의 색을 그대로 옮겼다.
public static class HoloPalette
{
    public static readonly Color Teal = Hex(0xA3EBD6);          // 민트 (선택·강조)
    public static readonly Color TealDark = Hex(0x4FA597);      // 진한 민트 (선 강조)
    public static readonly Color TealDeep = Hex(0x173A36);      // 아주 어두운 청록
    public static readonly Color Ink = Hex(0xEBF5F2);           // 기본 글자
    public static readonly Color Mute = Hex(0xA8C2BD);          // 흐린 글자
    public static readonly Color Dim = Hex(0x6A8A85);           // 더 흐린 글자
    public static readonly Color Red = Hex(0xFF4D5E);           // 경고·이벤트
    public static readonly Color Amber = Hex(0xFFB45C);
    public static readonly Color DarkInk = Hex(0x06201C);       // 민트 바탕 위 글자
    public static readonly Color DarkSub = Hex(0x174A42);       // 민트 바탕 위 흐린 글자
    public static readonly Color Panel = new Color(10 / 255f, 24 / 255f, 24 / 255f, 0.72f);
    public static readonly Color Panel2 = new Color(14 / 255f, 34 / 255f, 33 / 255f, 0.82f);
    public static readonly Color Line = new Color(115 / 255f, 204 / 255f, 189 / 255f, 0.55f);
    public static readonly Color Holo = Hex(0x5AE8FF);          // 라디오 홀로그램 하늘색

    public static Color Hex(int rgb, float a = 1f) =>
        new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

    public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
}
