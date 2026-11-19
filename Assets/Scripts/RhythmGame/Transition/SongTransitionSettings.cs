using System.Collections.Generic;
using UnityEngine;

// 곡 선택 → 인게임 전환 연출(A) 설정. Assets/Resources/SongTransitionSettings.asset 하나를 만들어 쓴다.
// (Tools > CapDemo > 리듬게임 > 전환 연출 설정 만들기 메뉴가 글꼴까지 채워 만든다)
//  - 밴드 대사: 곡마다 다르게 쓸 수 있다. 곡 이름이 비어 있는 줄이 기본 대사
//  - 아티스트 글씨, 드럼 카운트 효과음, 카운트 간격, 테스트용 인공 로딩 지연
[CreateAssetMenu(fileName = "SongTransitionSettings", menuName = "Rhythm Game/Song Transition Settings")]
public class SongTransitionSettings : ScriptableObject
{
    [System.Serializable]
    public class BandLine
    {
        [Tooltip("이 대사를 쓸 곡 (Resources/RhythmSongs 파일 이름, 확장자 없이). 비우면 기본 대사")]
        public string songName = "";
        [Tooltip("화자 태그 (BASS, DRUMS, VOX …)")]
        public string speaker = "BASS";
        [TextArea] public string line = "긴장 풀어. 늘 하던 대로, 첫 박만 같이 들어가자.";
    }

    [Header("밴드 대사 (화자 + 문장)")]
    public List<BandLine> bandLines = new List<BandLine> { new BandLine() };

    [Header("곡 정보")]
    [Tooltip("곡명 아래 아티스트 글씨 (곡별 정보가 생기면 바꾸기)")]
    public string artistText = "RE:WRITE  ·  ORIGINAL";
    [Tooltip("로그에 보일 인게임 배경 이름")]
    public string stageBackgroundName = "bg_live_teal";

    [Header("로딩 소리 (재킷이 나올 때부터, 인게임이 열리면 점점 작아지며 꺼짐)")]
    public AudioClip loadingSound;
    [Range(0f, 1f)] public float loadingVolume = 0.8f;
    [Tooltip("인게임 화면이 열린 뒤 소리가 완전히 꺼질 때까지 걸리는 시간(초)")]
    public float loadingFadeOut = 0.8f;

    [Header("드럼 카운트인")]
    [Tooltip("숫자가 바뀔 때마다 나는 소리. 비우면 소리 없음 (기본: 없음)")]
    public AudioClip countInClick;
    [Range(0f, 1f)] public float countInVolume = 0.8f;
    [Tooltip("카운트 간격(초). 곡 BPM에 맞추려면 SongTransitionOverlay.CountInterval을 60/BPM x 박수로")]
    public float countInterval = 0.6f;

    [Header("글꼴 (TTF)")]
    public Font oxaniumBold;
    [Tooltip("인게임 HUD 기본 글꼴 (미리 만들어 두기용)")]
    public Font oxaniumSemiBold;
    public Font oxaniumExtraBold;
    public Font sairaCondensed;
    public Font jetBrainsMono;
    public Font notoSansKr;
    public Texture2D noCover;
    [Tooltip("CapDemo/UI/TransitionDisc (빌드에 셰이더가 들어가도록 여기서 참조)")]
    public Shader discShader;

    [Header("테스트")]
    [Tooltip("로딩이 느린 경우를 보려고 일부러 늦추는 시간(초). 실제 게임에서는 0")]
    public float testExtraLoadSeconds = 0f;

    public BandLine LineFor(string songName)
    {
        BandLine fallback = null;
        for (int i = 0; i < bandLines.Count; i++)
        {
            var l = bandLines[i];
            if (l == null) continue;
            if (!string.IsNullOrEmpty(l.songName) && l.songName == songName) return l;
            if (string.IsNullOrEmpty(l.songName) && fallback == null) fallback = l;
        }
        return fallback;
    }

    private static SongTransitionSettings cached;

    // 도메인 리로드가 꺼져 있으므로 Play마다 다시 읽는다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        cached = null;
    }

    public static SongTransitionSettings Load()
    {
        if (cached == null) cached = Resources.Load<SongTransitionSettings>("SongTransitionSettings");
        if (cached == null) cached = CreateInstance<SongTransitionSettings>();
        return cached;
    }
}
