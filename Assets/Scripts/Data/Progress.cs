using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 진행 관리자. 게임에서 일어난 일(사건)을 받아 퀘스트·안내를 검사하고, 저장하고, UI에 알린다.
//   사건: 씬 열림, NPC 범위 진입, 구역 칸 진입, 대화·스토리 끝, 튜토리얼 단계 변경
//   → 지금 퀘스트 단계의 완료 조건 검사 → SaveData 저장 → Changed 등 이벤트
// UI는 저장 데이터를 직접 고치지 않고, 여기 이벤트를 듣고 다시 그리기만 한다.
// 씬에 따로 놓을 것은 없다 (게임이 시작되면 자동으로 동작). 내용 에셋은 Resources/DataCatalog에서 찾는다.
public static class Progress
{
    // 진행 데이터가 바뀌었다 (목표 패널, NPC 표시 등이 다시 그린다)
    public static event Action Changed;
    // 퀘스트 또는 퀘스트 단계가 바뀌었다 (목표 패널 지직 다시 켜짐)
    public static event Action QuestChanged;
    // 구역에 들어왔다. 두 번째 값 = 위치 갱신 표시를 띄울지 (구역의 Show Banner 설정)
    public static event Action<AreaData, bool> AreaEntered;
    // 첫 안내를 띄워 달라 / 내려 달라
    public static event Action<GuideData> GuideRequested;
    public static event Action<GuideData> GuideHidden;
    // 조작 안내를 띄워 달라
    public static event Action<ControlsGuide> ControlsGuideRequested;

    private static DataCatalog catalog;
    private static bool catalogLoaded;
    private static bool evaluating;
    private static bool dirty;

    public static DataCatalog Catalog
    {
        get
        {
            if (!catalogLoaded)
            {
                catalogLoaded = true;
                catalog = Resources.Load<DataCatalog>(DataCatalog.ResourcePath);
            }
            return catalog;
        }
    }

    // 지금 씬의 구역 (구역 에셋이 없는 씬이면 null)
    public static AreaData CurrentArea { get; private set; }
    // 이번에 위치 갱신 표시를 띄울지 (늦게 켜진 UI가 읽는다)
    public static bool CurrentAreaShowBanner { get; private set; }
    // 지금 떠 있는 첫 안내 (없으면 null)
    public static GuideData CurrentGuide { get; private set; }

    public static QuestData ActiveQuest => Catalog ? Catalog.FindQuest(SaveData.GetActiveQuest()) : null;
    public static int ActiveStepIndex => SaveData.GetActiveQuestStep();
    public static QuestData.Step ActiveStep => ActiveQuest ? ActiveQuest.GetStep(ActiveStepIndex) : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        // 도메인 리로드가 꺼져 있어 Play 사이에 static 값이 남으므로 비운다
        catalog = null;
        catalogLoaded = false;
        evaluating = false;
        dirty = false;
        CurrentArea = null;
        CurrentAreaShowBanner = false;
        CurrentGuide = null;
        Changed = null;
        QuestChanged = null;
        AreaEntered = null;
        GuideRequested = null;
        GuideHidden = null;
        ControlsGuideRequested = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SaveData.Changed -= OnSaveChanged;
        SaveData.Changed += OnSaveChanged;
    }

    // ── 사건 받기 ──

    // 무언가 바뀌었으니 다시 검사해 달라 (튜토리얼 단계가 바뀔 때 등)
    public static void NotifyChanged() => Evaluate();

    // 플레이어가 NPC 범위(트리거)에 들어왔다
    public static void NotifyNpcInRange(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return;
        TryShowGuides(GuideData.Trigger.NpcRange, npcId, null);
    }

    // 플레이어가 구역 칸(ProgressZone)에 들어왔다
    public static void NotifyZoneEntered(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return;
        TryShowGuides(GuideData.Trigger.ZoneEnter, zoneId, null);
    }

    // 대화·스토리를 끝까지 봤다 (기록은 부르는 쪽이 SaveData에 이미 남겼다)
    public static void NotifyDialogueFinished(ScriptableObject dialogue)
    {
        Evaluate();
        TryShowGuides(GuideData.Trigger.DialogueEnd, null, dialogue);
    }

    // UI가 첫 안내를 닫았다. 한 번만 뜨는 안내면 "본 안내"로 저장한다
    public static void CloseGuide(GuideData guide)
    {
        if (!guide) return;
        if (guide.Once) SaveData.MarkGuideSeen(guide.Id);
        if (CurrentGuide == guide)
        {
            CurrentGuide = null;
            GuideHidden?.Invoke(guide);
        }
    }

    // UI가 조작 안내를 닫았다
    public static void CloseControlsGuide(ControlsGuide guide)
    {
        if (guide) SaveData.MarkGuideSeen(guide.Id);
    }

    // ── NPC 표시용 ──

    // 지금 퀘스트 단계가 이 NPC와 대화하는 것인지 (노란 !)
    public static bool IsQuestTalkTarget(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return false;
        var step = ActiveStep;
        return step != null && step.talkNpcId == npcId;
    }

    // ── 남은 거리 ──

    // 지금 퀘스트 단계의 목표를 어디에 표시할지와 거리 문구. 표시할 것이 없으면 false.
    // 같은 씬에 목표 지점이 있으면 그 위, 다른 구역에 있으면 그쪽으로 가는 포탈 위.
    public static bool TryGetGuidePoint(Vector3 playerPosition, out Vector3 markerPosition, out string text)
    {
        markerPosition = Vector3.zero;
        text = "";
        var step = ActiveStep;
        if (step == null || string.IsNullOrEmpty(step.targetId)) return false;
        var settings = Catalog ? Catalog.Distance : null;
        float mpu = settings ? settings.MetersPerUnit : 1f;

        var target = QuestTarget.Find(step.targetId);
        if (target)
        {
            markerPosition = target.MarkerPosition;
            float meters = Mathf.Abs(target.transform.position.x - playerPosition.x) * mpu;
            string distance = settings ? settings.FormatMeters(meters) : Mathf.RoundToInt(meters) + "m";
            text = string.IsNullOrEmpty(target.DisplayName) ? distance : target.DisplayName + " · " + distance;
            return true;
        }

        // 다른 구역: 목표 구역 쪽으로 이어지는 다음 구역을 찾고, 그리로 가는 포탈을 가리킨다
        if (!step.targetArea || !CurrentArea || step.targetArea == CurrentArea) return false;
        var next = NextAreaToward(CurrentArea, step.targetArea);
        var portal = next ? MapPortal.FindToward(next) : null;
        if (!portal) return false;
        markerPosition = portal.MarkerPosition;
        float toPortal = Mathf.Abs(portal.transform.position.x - playerPosition.x) * mpu;
        string areaName = step.targetArea.DisplayName;
        string head = settings ? settings.FormatOtherArea(areaName) : areaName + " 방향";
        text = head + " · " + (settings ? settings.FormatMeters(toPortal) : Mathf.RoundToInt(toPortal) + "m");
        return true;
    }

    // 구역 연결(Connected Areas)을 따라 from에서 to로 가는 첫 구역. 길이 없으면 null
    public static AreaData NextAreaToward(AreaData from, AreaData to)
    {
        if (!from || !to || from == to) return null;
        var cameFrom = new Dictionary<AreaData, AreaData>();
        var queue = new Queue<AreaData>();
        queue.Enqueue(from);
        cameFrom[from] = null;
        while (queue.Count > 0)
        {
            var area = queue.Dequeue();
            if (area == to) break;
            foreach (var n in area.ConnectedAreas)
            {
                if (!n || cameFrom.ContainsKey(n)) continue;
                cameFrom[n] = area;
                queue.Enqueue(n);
            }
        }
        if (!cameFrom.ContainsKey(to)) return null;
        var step = to;
        while (cameFrom[step] != from) step = cameFrom[step];
        return step;
    }

    // ── 에디터 테스트용 ──

    // 지금 퀘스트 단계를 끝난 것으로 치고 다음 단계로 (Tools > CapDemo > 데이터)
    public static void DebugSkipStep()
    {
        var quest = ActiveQuest;
        if (!quest)
        {
            Debug.Log("[Progress] 진행 중인 퀘스트가 없습니다.");
            return;
        }
        AdvanceStep(quest);
        Evaluate();
        RaiseChanged();
    }

    // 특정 퀘스트의 특정 단계(0부터)로 바로 옮긴다
    public static void DebugStartQuest(QuestData quest, int stepIndex)
    {
        if (!quest) return;
        SaveData.SetActiveQuest(quest.Id, Mathf.Clamp(stepIndex, 0, Mathf.Max(0, quest.Steps.Count - 1)));
        QuestChanged?.Invoke();
        Evaluate();
    }

    // ── 내부 ──

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var cat = Catalog;
        if (mode == LoadSceneMode.Single)
        {
            CurrentArea = null;
            CurrentAreaShowBanner = false;
            if (CurrentGuide)
            {
                var old = CurrentGuide;
                CurrentGuide = null;
                GuideHidden?.Invoke(old);
            }
        }

        // 구역 방문
        var area = cat ? cat.FindAreaByScene(scene.name) : null;
        if (area)
        {
            bool first = !SaveData.HasVisitedArea(area.Id);
            CurrentArea = area;
            CurrentAreaShowBanner = area.ShouldShowBanner(first);
            SaveData.MarkAreaVisited(area.Id);
            AreaEntered?.Invoke(area, CurrentAreaShowBanner);
        }

        Evaluate();

        // 조작 안내
        if (cat && ControlsGuideRequested != null)
            foreach (var cg in cat.ControlsGuides)
                if (cg && cg.ShowInScene == scene.name && !SaveData.HasSeenGuide(cg.Id) && Condition.AllMet(cg.ShowWhen))
                {
                    ControlsGuideRequested.Invoke(cg);
                    break;
                }

        TryShowGuides(GuideData.Trigger.SceneStart, scene.name, null);
    }

    private static void OnSaveChanged()
    {
        // Play가 끝난 뒤 에디터 메뉴로 저장을 바꿀 때는 퀘스트를 움직이지 않는다
        if (!Application.isPlaying) return;
        if (evaluating) dirty = true;
        else Evaluate();
    }

    // 퀘스트를 시작·진행·완료시키고, 떠 있는 안내의 사라질 조건을 본다. 바뀐 게 있으면 한 번 알린다
    private static void Evaluate()
    {
        if (evaluating)
        {
            dirty = true;
            return;
        }
        if (!Catalog)
        {
            Changed?.Invoke();
            return;
        }
        evaluating = true;
        bool questChanged = false;
        try
        {
            // 단계가 연달아 끝날 수 있으므로 몇 번 반복한다 (끝없이 돌지 않게 횟수 제한)
            for (int guard = 0; guard < 32; guard++)
            {
                dirty = false;
                if (!StepOnce()) break;
                questChanged = true;
            }

            if (CurrentGuide && CurrentGuide.HideWhen.Count > 0 && Condition.AllMet(CurrentGuide.HideWhen))
                CloseGuide(CurrentGuide);
        }
        finally
        {
            evaluating = false;
        }

        if (questChanged)
        {
            QuestChanged?.Invoke();
            TryShowGuides(GuideData.Trigger.QuestStepChanged, null, null);
        }
        Changed?.Invoke();
    }

    // 퀘스트를 한 칸 움직였으면 true
    private static bool StepOnce()
    {
        var quest = ActiveQuest;
        if (!quest)
        {
            // 진행 중인 퀘스트가 없으면 시작 조건이 맞는 첫 퀘스트를 시작한다
            foreach (var q in Catalog.Quests)
            {
                if (!q || q.StartWhen.Count == 0 || SaveData.IsQuestFinished(q.Id)) continue;
                if (!Condition.AllMet(q.StartWhen)) continue;
                SaveData.SetActiveQuest(q.Id, 0);
                return true;
            }
            // 저장된 퀘스트 ID가 목록에 없으면(에셋 삭제·ID 변경) 비운다
            if (SaveData.GetActiveQuest().Length > 0)
            {
                Debug.LogWarning($"[Progress] 저장된 퀘스트 '{SaveData.GetActiveQuest()}'를 찾을 수 없어 비웁니다.");
                SaveData.SetActiveQuest("", 0);
                return true;
            }
            return false;
        }

        var step = quest.GetStep(ActiveStepIndex);
        if (step != null && (step.completeWhen.Count == 0 || !Condition.AllMet(step.completeWhen))) return false;
        AdvanceStep(quest);
        return true;
    }

    private static void AdvanceStep(QuestData quest)
    {
        int next = ActiveStepIndex + 1;
        if (next < quest.Steps.Count)
        {
            SaveData.SetActiveQuest(quest.Id, next);
            return;
        }
        // 마지막 단계 끝: 퀘스트 완료
        SaveData.MarkQuestFinished(quest.Id);
        if (quest.ClearChapters > 0) SaveData.MarkChapterCleared(quest.ClearChapters);
        var follow = quest.NextQuest;
        if (follow && !SaveData.IsQuestFinished(follow.Id)) SaveData.SetActiveQuest(follow.Id, 0);
        else SaveData.SetActiveQuest("", 0);
    }

    private static void TryShowGuides(GuideData.Trigger trigger, string id, ScriptableObject dialogue)
    {
        var cat = Catalog;
        // 띄울 UI가 없으면(아직 UI를 안 놓은 씬) "본 안내"로 넘어가지 않게 아무것도 안 한다
        if (!cat || GuideRequested == null || CurrentGuide) return;
        foreach (var g in cat.Guides)
        {
            if (!g || g.When != trigger) continue;
            if (g.Once && SaveData.HasSeenGuide(g.Id)) continue;
            switch (trigger)
            {
                case GuideData.Trigger.NpcRange:
                case GuideData.Trigger.ZoneEnter:
                    if (g.TriggerId != id) continue;
                    break;
                case GuideData.Trigger.SceneStart:
                    if (!string.IsNullOrEmpty(g.TriggerId) && g.TriggerId != id) continue;
                    break;
                case GuideData.Trigger.DialogueEnd:
                    if (g.TriggerDialogue && g.TriggerDialogue != dialogue) continue;
                    break;
            }
            if (!Condition.AllMet(g.ShowWhen)) continue;
            CurrentGuide = g;
            GuideRequested.Invoke(g);
            return;
        }
    }

    private static void RaiseChanged() => Changed?.Invoke();
}
