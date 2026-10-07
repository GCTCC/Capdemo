using System;
using System.Collections.Generic;
using UnityEngine;

// 스토리 장면 하나(예: 1-1)의 데이터. 프로젝트 창 우클릭 > Create > CapDemo > Story Chapter로 만든다.
// 장면 = 컷 목록, 컷 = 배경 일러스트 한 장 + 대사 여러 줄
[CreateAssetMenu(fileName = "Story_", menuName = "CapDemo/Story Chapter")]
public class StoryChapter : ScriptableObject
{
    [SerializeField] private List<StoryCut> cuts = new List<StoryCut>();

    public IReadOnlyList<StoryCut> Cuts => cuts;

    // index번 컷에서 실제로 보이는 일러스트와 연출 값.
    // Illustration을 비운 컷은 앞 컷의 그림과 연출을 그대로 이어받고, Black Screen이면 그림 없음(null)
    public (Sprite sprite, IllustFraming framing) ResolveIllustration(int index)
    {
        for (int i = Mathf.Min(index, cuts.Count - 1); i >= 0; i--)
        {
            var cut = cuts[i];
            if (cut.BlackScreen) return (null, default);
            if (cut.Illustration) return (cut.Illustration, cut.Framing);
        }
        return (null, default);
    }

#if UNITY_EDITOR
    // 인스펙터에서 새로 추가한 효과음·BGM은 음량이 0으로 들어오므로, 처음 Clip을 넣을 때 1로 맞춰 준다
    private void OnValidate()
    {
        foreach (var cut in cuts)
        {
            if (cut == null) continue;
            cut.Bgm?.InitVolume();
            foreach (var sound in cut.Sounds) sound?.InitVolume();
            foreach (var line in cut.Lines)
                if (line != null)
                    foreach (var sound in line.Sounds) sound?.InitVolume();
        }
    }
#endif
}

[Serializable]
public class StoryCut
{
    [Tooltip("인스펙터 목록에 보일 이름. 게임에는 나오지 않는다.")]
    [SerializeField] private string memo;

    [Tooltip("이 컷의 배경 일러스트. 비우면 이전 컷의 일러스트(연출 포함)를 그대로 둔다.")]
    [SerializeField, SpritePreview] private Sprite illustration;

    [Tooltip("체크하면 이 컷은 일러스트 없이 검은 화면에서 대사가 나온다. (Illustration은 무시)")]
    [SerializeField] private bool blackScreen;

    [Tooltip("일러스트를 어떻게 보여 줄지 (맞춤 방식, 확대, 위치, 색). Illustration을 넣은 컷에만 적용된다.")]
    [SerializeField] private IllustFraming framing;

    [Tooltip("이전 컷에서 이 컷의 일러스트로 넘어오는 방식. 첫 컷은 항상 검은 화면에서 페이드 인한다.")]
    [SerializeField] private CutTransition transition = CutTransition.Crossfade;

    [Tooltip("전환에 걸리는 시간(초). 0이면 StoryManager의 기본값을 쓴다.")]
    [SerializeField, Min(0f)] private float transitionDuration;

    [Tooltip("이 컷에서 BGM을 어떻게 할지. Keep이면 흐르던 BGM을 그대로 둔다.")]
    [SerializeField] private CutBgm bgm = new CutBgm();

    [Tooltip("컷이 바뀔 때 나오는 효과음 (전환 시작 / 전환 끝)")]
    [SerializeField] private List<CutSound> sounds = new List<CutSound>();

    [SerializeField] private List<StoryLine> lines = new List<StoryLine>();

    public string Memo => memo;
    public Sprite Illustration => illustration;
    public bool BlackScreen => blackScreen;
    public IllustFraming Framing => framing;
    public CutTransition Transition => transition;
    public float TransitionDuration => transitionDuration;
    public CutBgm Bgm => bgm;
    public IReadOnlyList<CutSound> Sounds => sounds;
    public IReadOnlyList<StoryLine> Lines => lines;
}

[Serializable]
public class StoryLine
{
    [Tooltip("화자 이름. 비우면 이름표를 숨긴다. {player}는 플레이어 이름으로 바뀐다.")]
    [SerializeField] private string speaker;

    [Tooltip("스탠딩 일러스트. 비우면 이전 줄의 스탠딩을 그대로 둔다.")]
    [SerializeField, SpritePreview] private Sprite standing;

    [Tooltip("체크하면 이 줄에서 스탠딩을 숨긴다.")]
    [SerializeField] private bool hideStanding;

    [Tooltip("스탠딩 위치·크기·반전. Position이 Keep이면 이전 줄 상태를 그대로 둔다.")]
    [SerializeField] private StandingPose standingPose;

    [Tooltip("대사. {player}는 플레이어 이름으로 바뀐다.")]
    [SerializeField, TextArea(2, 5)] private string text;

    [Tooltip("대사 출력이 끝난 뒤 실행할 일. 끝나면 다음 줄로 넘어간다.")]
    [SerializeField] private StoryPause pause;

    [Tooltip("이 대사에 맞춰 나오는 효과음 (대사 시작 / N번째 글자 / 특정 단어 / 출력 끝 / 다음으로 넘길 때)")]
    [SerializeField] private List<LineSound> sounds = new List<LineSound>();

    public string Speaker => speaker;
    public Sprite Standing => standing;
    public bool HideStanding => hideStanding;
    public StandingPose StandingPose => standingPose;
    public string Text => text;
    public StoryPause Pause => pause;
    public IReadOnlyList<LineSound> Sounds => sounds;
}

public enum CutTransition
{
    Crossfade,   // 이전 일러스트 위로 다음 일러스트가 서서히 겹쳐 올라온다
    FadeToBlack, // 검은 화면으로 어두워졌다가 다음 일러스트로 밝아진다
    Cut,         // 페이드 없이 바로 바뀐다
}

public enum IllustFit
{
    Fit,     // 비율 유지, 화면에 다 들어오게 (남는 곳은 검게)
    Fill,    // 비율 유지, 화면을 꽉 채우게 (넘치는 곳은 잘림)
    Stretch, // 화면 크기에 맞춰 늘림
}

// 컷 일러스트 연출. 모든 값이 0인 기본 상태가 "비율 유지, 원래 크기, 가운데, 원래 색"이 되도록 했다
[Serializable]
public struct IllustFraming
{
    [Tooltip("Fit: 비율 유지·전부 보이게 / Fill: 비율 유지·화면 꽉 채움(넘치는 곳 잘림) / Stretch: 화면에 맞춰 늘림")]
    public IllustFit fit;

    [Tooltip("확대 배율. 0이나 1이면 원래 크기, 1.5면 1.5배")]
    [Min(0f)] public float zoom;

    [Tooltip("화면 기준 이동량 (1920x1080 기준 픽셀). 확대한 뒤 보여 줄 부분을 고를 때 쓴다")]
    public Vector2 offset;

    [Tooltip("체크하면 아래 색을 일러스트에 곱한다 (회상 장면을 어둡게, 붉게 등)")]
    public bool useTint;

    [ColorUsage(false)] public Color tint;

    public float Zoom => zoom > 0f ? zoom : 1f;
    public Color Tint => useTint ? new Color(tint.r, tint.g, tint.b, 1f) : Color.white;

    public bool SameAs(IllustFraming other) =>
        fit == other.fit && Mathf.Approximately(Zoom, other.Zoom) && offset == other.offset && Tint == other.Tint;
}

public enum StandingPosition
{
    Keep,   // 이전 줄 위치·크기·반전 유지 (첫 줄이면 씬에 배치한 자리)
    Left,
    Center,
    Right,
}

// 스탠딩 연출. 위치 프리셋(화면 비율)은 DialogueBox 인스펙터에서 정한다
[Serializable]
public struct StandingPose
{
    [Tooltip("Keep: 이전 줄 상태 유지 / Left·Center·Right: 그 자리로 옮기고 아래 값을 적용")]
    public StandingPosition position;

    [Tooltip("프리셋 자리에서 더 옮길 양 (1920x1080 기준 픽셀)")]
    public Vector2 offset;

    [Tooltip("크기 배율. 0이나 1이면 씬에 배치한 크기")]
    [Min(0f)] public float scale;

    [Tooltip("좌우 반전")]
    public bool flip;

    public float Scale => scale > 0f ? scale : 1f;
}

public enum BgmAction
{
    Keep, // 흐르던 BGM을 그대로 둔다
    Play, // 이 곡으로 바꾼다 (같은 곡이면 음량만 맞춘다)
    Stop, // 멈춘다
}

// 컷 단위 BGM 지시
[Serializable]
public class CutBgm
{
    [Tooltip("Keep: 그대로 / Play: 아래 곡으로 바꾸기 / Stop: 멈추기")]
    public BgmAction action;

    public AudioClip clip;

    [Tooltip("이 곡의 음량 (StoryAudio의 BGM 전체 음량과 곱해진다)")]
    [Range(0f, 1f)] public float volume = 1f;

    [Tooltip("Play: 이전 곡에서 넘어오며 서서히 커지는 시간(초)")]
    [Min(0f)] public float fadeIn = 1f;

    [Tooltip("이 컷을 포함해 몇 컷 동안 재생할지. 3이면 이 컷과 다음 두 컷 동안 흐르고 그다음 컷에서 멈춘다. 0이면 다른 지시가 있거나 장면이 끝날 때까지 계속")]
    [Min(0)] public int cutCount;

    [Tooltip("멈출 때(Stop, 또는 Cut Count가 끝났을 때) 서서히 줄어드는 시간(초)")]
    [Min(0f)] public float fadeOut = 1f;

    [SerializeField, HideInInspector] private bool volumeInitialized;

    public void InitVolume()
    {
        if (volumeInitialized || !clip) return;
        volumeInitialized = true;
        if (volume <= 0f) volume = 1f;
        if (fadeIn <= 0f) fadeIn = 1f;
        if (fadeOut <= 0f) fadeOut = 1f;
    }
}

public enum CutSoundTiming
{
    TransitionStart, // 이전 컷에서 넘어가기 시작할 때 (첫 컷이면 화면이 밝아지기 시작할 때)
    CutShown,        // 이 컷 그림이 다 보인 뒤
}

[Serializable]
public class CutSound
{
    [Tooltip("TransitionStart: 전환 시작 / CutShown: 그림이 다 보인 뒤")]
    public CutSoundTiming timing;

    public AudioClip clip;

    [Tooltip("이 효과음의 음량 (StoryAudio의 효과음 전체 음량과 곱해진다)")]
    [Range(0f, 1f)] public float volume = 1f;

    [Tooltip("타이밍에서 몇 초 뒤에 낼지")]
    [Min(0f)] public float delay;

    [Tooltip("반복 재생 (빗소리 같은 환경음). 이 컷이 끝나면 멈춘다")]
    public bool loop;

    [SerializeField, HideInInspector] private bool volumeInitialized;

    public void InitVolume()
    {
        if (volumeInitialized || !clip) return;
        volumeInitialized = true;
        if (volume <= 0f) volume = 1f;
    }
}

public enum LineSoundTiming
{
    LineStart,   // 대사가 나오기 시작할 때
    AtCharacter, // N번째 글자가 나올 때
    AtText,      // 특정 단어가 나오기 시작할 때
    TypingDone,  // 대사 출력이 다 끝났을 때
    OnNext,      // 플레이어가 다음으로 넘길 때
}

public enum SoundLoopUntil
{
    LineEnd, // 이 대사를 넘기면 멈춤
    CutEnd,  // 이 컷이 끝나면 멈춤
}

[Serializable]
public class LineSound
{
    [Tooltip("LineStart: 대사 시작 / AtCharacter: N번째 글자 / AtText: 특정 단어 / TypingDone: 출력 끝 / OnNext: 다음으로 넘길 때")]
    public LineSoundTiming timing;

    [Tooltip("AtCharacter일 때: 몇 번째 글자가 나올 때 낼지 (1부터, 띄어쓰기 포함)")]
    [Min(0)] public int atCharacter;

    [Tooltip("AtText일 때: 대사 속 이 단어가 나오기 시작할 때 낸다 (예: 쾅)")]
    public string atText;

    public AudioClip clip;

    [Tooltip("이 효과음의 음량 (StoryAudio의 효과음 전체 음량과 곱해진다)")]
    [Range(0f, 1f)] public float volume = 1f;

    [Tooltip("타이밍에서 몇 초 뒤에 낼지")]
    [Min(0f)] public float delay;

    [Tooltip("반복 재생. Loop Until 시점에 멈춘다")]
    public bool loop;

    [Tooltip("반복 재생일 때 언제 멈출지: 이 대사를 넘길 때 / 이 컷이 끝날 때")]
    public SoundLoopUntil loopUntil;

    [SerializeField, HideInInspector] private bool volumeInitialized;

    public void InitVolume()
    {
        if (volumeInitialized || !clip) return;
        volumeInitialized = true;
        if (volume <= 0f) volume = 1f;
    }
}

public enum StoryPause
{
    None,
    NameInput, // 이름 입력 창을 띄우고, 입력이 끝나면 다음 줄로
}

public static class StoryText
{
    public const string PlayerToken = "{player}";

    // {player}를 저장된 플레이어 이름으로 바꾼다
    public static string Format(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        string name = SaveData.GetPlayerName();
        return raw.Replace(PlayerToken, string.IsNullOrEmpty(name) ? "???" : name);
    }
}
