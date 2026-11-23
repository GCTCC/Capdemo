using System;
using System.Collections.Generic;
using UnityEngine;

// 안내·퀘스트·포탈·NPC 대화가 "언제"를 묻는 공통 조건 한 줄.
// 여러 줄을 목록으로 달면 모두 맞아야 통과한다. "아님"을 켜면 반대가 된다(예: 안내를 아직 안 봄).
// Inspector에서는 고른 종류에 필요한 칸만 보인다 (Editor/ConditionDrawer.cs).
[Serializable]
public class Condition
{
    public enum Kind
    {
        [InspectorName("튜토리얼 단계")] TutorialStep,
        [InspectorName("퀘스트 상태")] QuestState,
        [InspectorName("NPC 만남")] NpcMet,
        [InspectorName("대화·스토리 봄")] DialogueSeen,
        [InspectorName("안내 봄")] GuideSeen,
        [InspectorName("구역 방문")] AreaVisited,
        [InspectorName("챕터 클리어")] ChaptersCleared,
    }

    public enum Compare
    {
        [InspectorName("이상")] AtLeast,
        [InspectorName("같음")] Equal,
    }

    public enum QuestStateKind
    {
        [InspectorName("시작 전")] NotStarted,
        [InspectorName("진행 중")] Active,
        [InspectorName("끝남")] Finished,
    }

    public Kind kind;
    [Tooltip("켜면 반대 조건이 된다 (예: 안내를 아직 안 봄)")]
    public bool not;

    // 튜토리얼 단계
    public TutorialStep step = TutorialStep.Exploring;
    public Compare compare = Compare.AtLeast;

    // 퀘스트 상태
    public QuestData quest;
    public QuestStateKind questState = QuestStateKind.Active;
    [Tooltip("진행 중일 때 이 단계 번호(1부터)일 때만. 0이면 단계 상관없음")]
    [Min(0)] public int questStep;

    // NPC 만남
    [Tooltip("NPC의 NPC ID (예: npc.bouncer)")]
    public string npcId = "";

    // 대화·스토리 봄
    [Tooltip("NPC 대화(NpcDialogue) 또는 스토리(StoryChapter) 에셋. 끝까지 봤으면 맞음")]
    public ScriptableObject dialogue;

    // 안내 봄 (첫 안내 또는 조작 안내)
    [Tooltip("첫 안내(GuideData) 또는 조작 안내(ControlsGuide) 에셋")]
    public ScriptableObject guide;

    // 구역 방문
    public AreaData area;

    // 챕터 클리어
    [Min(0)] public int chapters = 1;

    public bool IsMet()
    {
        bool result = Evaluate();
        return not ? !result : result;
    }

    private bool Evaluate()
    {
        switch (kind)
        {
            case Kind.TutorialStep:
                return compare == Compare.Equal ? TutorialRun.Step == step : TutorialRun.Step >= step;
            case Kind.QuestState:
                if (!quest) return false;
                switch (questState)
                {
                    case QuestStateKind.Finished: return SaveData.IsQuestFinished(quest.Id);
                    case QuestStateKind.Active:
                        if (SaveData.GetActiveQuest() != quest.Id) return false;
                        return questStep <= 0 || SaveData.GetActiveQuestStep() == questStep - 1;
                    default:
                        return !SaveData.IsQuestFinished(quest.Id) && SaveData.GetActiveQuest() != quest.Id;
                }
            case Kind.NpcMet:
                return SaveData.HasMetNpc(npcId);
            case Kind.DialogueSeen:
                return dialogue && SaveData.HasSeenDialogue(dialogue.name);
            case Kind.GuideSeen:
                return SaveData.HasSeenGuide(GuideIdOf(guide));
            case Kind.AreaVisited:
                return area && SaveData.HasVisitedArea(area.Id);
            case Kind.ChaptersCleared:
                return SaveData.GetChaptersCleared() >= chapters;
            default:
                return false;
        }
    }

    private static string GuideIdOf(ScriptableObject asset)
    {
        if (asset is GuideData g) return g.Id;
        if (asset is ControlsGuide c) return c.Id;
        return "";
    }

    // 목록의 조건이 모두 맞는지. 비어 있으면 맞음
    public static bool AllMet(List<Condition> conditions)
    {
        if (conditions == null) return true;
        foreach (var c in conditions)
            if (c != null && !c.IsMet()) return false;
        return true;
    }

    // 검사 메뉴용: 이 조건에 빠진 값이 있으면 설명을 돌려준다
    public string Problem()
    {
        switch (kind)
        {
            case Kind.QuestState: return quest ? null : "퀘스트가 비어 있음";
            case Kind.NpcMet: return string.IsNullOrWhiteSpace(npcId) ? "NPC ID가 비어 있음" : null;
            case Kind.DialogueSeen: return dialogue ? null : "대화 에셋이 비어 있음";
            case Kind.GuideSeen:
                return guide is GuideData || guide is ControlsGuide ? null : "안내 에셋이 비어 있거나 안내 종류가 아님";
            case Kind.AreaVisited: return area ? null : "구역이 비어 있음";
            default: return null;
        }
    }
}
