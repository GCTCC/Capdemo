using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 결과 화면 v2 설정 파일. Assets/Resources/ResultV2Settings.asset 하나를 만들어 쓴다.
//
// 재이가 정하는 것 (Inspector에서):
//  - 배경 사진: 여러 장 넣어 두면 결과 화면이 열릴 때마다 그중 한 장이 랜덤으로 깔린다 (흐리고 어둡게 처리됨)
//  - 폴라로이드 묶음: [사진 + 문구]를 한 묶음으로 여러 개 넣어 두면, 그중 한 묶음이 랜덤으로 나온다
// 아래 '글꼴·소리'는 처음 만들 때 채워 두었으니 건드리지 않아도 된다.
[CreateAssetMenu(fileName = "ResultV2Settings", menuName = "CapDemo/Result V2 Settings")]
public class ResultV2Settings : ScriptableObject
{
    [System.Serializable]
    public class PolaroidSet
    {
        [Tooltip("폴라로이드에 들어갈 사진 (Sprite). 사진 칸(세로로 조금 긴 네모)에 꽉 차게 잘려서 들어간다")]
        public Sprite photo;

        [Tooltip("사진 아래 손글씨 문구. 엔터로 줄을 바꿀 수 있다 (두 줄 정도가 예쁘다)")]
        [TextArea(2, 4)] public string caption = "음, 나쁘지 않네.\n한 번 더 가 볼까?";

        [Tooltip("사진을 자를 때 가로 어디를 중심으로 볼지 (0 = 왼쪽, 0.5 = 가운데, 1 = 오른쪽). 인물이 잘리면 조절")]
        [Range(0f, 1f)] public float focusX = 0.5f;

        [Tooltip("사진을 자를 때 세로 어디를 중심으로 볼지 (1 = 맨 위 기준, 0 = 맨 아래 기준)")]
        [Range(0f, 1f)] public float focusY = 1f;
    }

    [Header("배경 사진 (여러 장 → 랜덤으로 한 장)")]
    [Tooltip("비워 두면 곡 재킷을 흐리게 깔아 배경으로 쓴다")]
    public List<Sprite> backgrounds = new List<Sprite>();

    [Header("폴라로이드 (사진 + 문구 한 묶음, 여러 개 → 랜덤으로 하나)")]
    [Tooltip("비워 두면 폴라로이드가 안 나온다. 사진이 빈 묶음은 건너뛴다")]
    public List<PolaroidSet> polaroids = new List<PolaroidSet>();

    [Header("랜덤")]
    [Tooltip("켜면 바로 전에 나온 배경·폴라로이드는 다음 번에 다시 안 나온다 (2개 이상일 때)")]
    public bool avoidRepeat = true;

    [Header("글꼴·소리 (건드리지 않아도 됨)")]
    [Tooltip("로고, RANK 글자, 판정 이름 (NEMESYS)")]
    public Font displayFont;
    public Font oxaniumSemiBold;
    public Font oxaniumBold;
    [Tooltip("큰 숫자 (Saira Condensed ExtraBold, 기울임)")]
    public Font numberFont;
    [Tooltip("한글이 없는 글꼴에 붙이는 예비 한글 글꼴")]
    public Font koreanFallback;
    [Tooltip("한글 제목·문구 글꼴 (경기천년제목 Bold SDF)")]
    public TMP_FontAsset koreanTitle;
    [Tooltip("재킷이 '팍' 켜질 때 소리. 비우면 소리 없음")]
    public AudioClip popSound;
    [Range(0f, 1f)] public float popVolume = 0.6f;

    // ── 랜덤으로 고르기 ──
    private static int lastBackground = -1, lastPolaroid = -1;
    private static ResultV2Settings cached;

    // 도메인 리로드가 꺼져 있어서 Play마다 다시 읽는다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        cached = null;
        lastBackground = lastPolaroid = -1;
    }

    public static ResultV2Settings Load()
    {
        if (cached == null) cached = Resources.Load<ResultV2Settings>("ResultV2Settings");
        if (cached == null) cached = CreateInstance<ResultV2Settings>();
        return cached;
    }

    public Sprite PickBackground()
    {
        var pool = new List<int>();
        for (int i = 0; i < backgrounds.Count; i++)
            if (backgrounds[i] != null) pool.Add(i);
        int index = Pick(pool, ref lastBackground);
        return index >= 0 ? backgrounds[index] : null;
    }

    public PolaroidSet PickPolaroid()
    {
        var pool = new List<int>();
        for (int i = 0; i < polaroids.Count; i++)
            if (polaroids[i] != null && polaroids[i].photo != null) pool.Add(i);
        int index = Pick(pool, ref lastPolaroid);
        return index >= 0 ? polaroids[index] : null;
    }

    private int Pick(List<int> pool, ref int last)
    {
        if (pool.Count == 0) return -1;
        if (avoidRepeat && pool.Count > 1) pool.Remove(last);
        int pick = pool[Random.Range(0, pool.Count)];
        last = pick;
        return pick;
    }
}
