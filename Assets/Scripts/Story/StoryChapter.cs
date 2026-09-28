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
}

[Serializable]
public class StoryCut
{
    [Tooltip("인스펙터 목록에 보일 이름. 게임에는 나오지 않는다.")]
    [SerializeField] private string memo;

    [Tooltip("이 컷의 배경 일러스트. 비우면 이전 컷의 일러스트를 그대로 둔다.")]
    [SerializeField] private Sprite illustration;

    [Tooltip("체크하면 이 컷은 일러스트 없이 검은 화면에서 대사가 나온다. (Illustration은 무시)")]
    [SerializeField] private bool blackScreen;

    [Tooltip("이전 컷에서 이 컷의 일러스트로 넘어오는 방식. 첫 컷은 항상 검은 화면에서 페이드 인한다.")]
    [SerializeField] private CutTransition transition = CutTransition.Crossfade;

    [Tooltip("전환에 걸리는 시간(초). 0이면 StoryManager의 기본값을 쓴다.")]
    [SerializeField, Min(0f)] private float transitionDuration;

    [SerializeField] private List<StoryLine> lines = new List<StoryLine>();

    public Sprite Illustration => illustration;
    public bool BlackScreen => blackScreen;
    public CutTransition Transition => transition;
    public float TransitionDuration => transitionDuration;
    public IReadOnlyList<StoryLine> Lines => lines;
}

[Serializable]
public class StoryLine
{
    [Tooltip("화자 이름. 비우면 이름표를 숨긴다. {player}는 플레이어 이름으로 바뀐다.")]
    [SerializeField] private string speaker;

    [Tooltip("스탠딩 일러스트. 비우면 이전 줄의 스탠딩을 그대로 둔다.")]
    [SerializeField] private Sprite standing;

    [Tooltip("체크하면 이 줄에서 스탠딩을 숨긴다.")]
    [SerializeField] private bool hideStanding;

    [Tooltip("대사. {player}는 플레이어 이름으로 바뀐다.")]
    [SerializeField, TextArea(2, 5)] private string text;

    [Tooltip("대사 출력이 끝난 뒤 실행할 일. 끝나면 다음 줄로 넘어간다.")]
    [SerializeField] private StoryPause pause;

    public string Speaker => speaker;
    public Sprite Standing => standing;
    public bool HideStanding => hideStanding;
    public string Text => text;
    public StoryPause Pause => pause;
}

public enum CutTransition
{
    Crossfade,   // 이전 일러스트 위로 다음 일러스트가 서서히 겹쳐 올라온다
    FadeToBlack, // 검은 화면으로 어두워졌다가 다음 일러스트로 밝아진다
    Cut,         // 페이드 없이 바로 바뀐다
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
