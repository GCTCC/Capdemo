using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 로컬 플레이 진행 데이터 창구. 저장 방식을 바꿀 때는 이 파일만 고치면 된다.
// 진행 데이터는 한 묶음(JSON)으로 save.json 파일 하나에 저장한다.
//   위치: Application.persistentDataPath/save.json (Tools > CapDemo > 데이터 > 저장 파일 폴더 열기)
// 처음 한 번은 예전 PlayerPrefs 키(ChaptersCleared, TutoCleared, PlayerName, MetNpcs)를 읽어 옮긴다.
// 설정값(GameSettings, opt_ 키)은 여기에 없고 PlayerPrefs에 그대로 있다.
public static class SaveData
{
    // 저장 형식이 바뀌면 올린다. 읽을 때 옛 버전이면 Upgrade에서 고친다
    private const int CurrentVersion = 1;
    private const string FileName = "save.json";

    // 예전 PlayerPrefs 키 (옮기기용)
    private const string OldChaptersClearedKey = "ChaptersCleared";
    private const string OldTutorialClearedKey = "TutoCleared";
    private const string OldPlayerNameKey = "PlayerName";
    private const string OldMetNpcsKey = "MetNpcs";

    // save.json에 들어가는 한 묶음. 칸을 추가해도 옛 파일은 빈 값으로 읽힌다
    [Serializable]
    private class Bundle
    {
        public int version = CurrentVersion;
        public int chaptersCleared;
        public string playerName = "";
        public int tutorialStep;                                    // TutorialStep 숫자
        public List<string> metNpcs = new List<string>();           // NPC ID (예전 기록은 첫 만남 에셋 파일 이름)
        public List<string> seenDialogues = new List<string>();     // 끝까지 본 대화·스토리 에셋 이름
        public List<string> seenGuides = new List<string>();        // 본 안내 ID (조작 안내 포함)
        public List<string> visitedAreas = new List<string>();      // 가 본 구역 ID
        public string activeQuest = "";                             // 진행 중 퀘스트 ID (없으면 빈 문자열)
        public int activeQuestStep;                                 // 진행 중 퀘스트의 단계 번호 (0부터)
        public List<string> finishedQuests = new List<string>();    // 끝낸 퀘스트 ID
    }

    private static Bundle data;

    // 진행 데이터가 바뀔 때마다 알린다 (UI는 이걸 듣고 다시 그리기만 한다)
    public static event Action Changed;

    // 이름이 바뀌면 알린다 (PlayerNameText가 다시 그린다)
    public static event Action PlayerNameChanged;

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    // 이 프로젝트는 Play 때 static 값이 자동으로 지워지지 않으므로(도메인 리로드 끔) 직접 비운다.
    // 다음에 처음 읽을 때 파일에서 다시 읽는다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => data = null;

    private static Bundle Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    // ── 챕터 ──

    // 클리어한 챕터 수. 0 = 아직 튜토리얼 챕터(첫 곡까지) 진행 중, 1 이상 = 허브가 열림
    public static int GetChaptersCleared() => Data.chaptersCleared;

    // 챕터를 끝냈을 때 부른다. 이미 더 많이 클리어했으면 줄이지 않는다
    public static void MarkChapterCleared(int chaptersCleared)
    {
        if (chaptersCleared <= Data.chaptersCleared) return;
        Data.chaptersCleared = chaptersCleared;
        Save();
    }

    // 튜토리얼 챕터(첫 곡까지)를 끝냈는지 = 1장 이상 클리어
    public static bool IsTutorialCleared() => GetChaptersCleared() >= 1;

    public static void MarkTutorialCleared() => MarkChapterCleared(1);

    // ── 플레이어 이름 ──

    // 튜토리얼에서 입력받은 플레이어 이름. 아직 없으면 빈 문자열
    public static string GetPlayerName() => Data.playerName ?? "";

    public static void SetPlayerName(string name)
    {
        Data.playerName = name ?? "";
        Save();
        PlayerNameChanged?.Invoke();
    }

    // ── 튜토리얼 단계 (TutorialRun.Step이 읽고 쓴다) ──

    public static TutorialStep GetTutorialStep() => (TutorialStep)Data.tutorialStep;

    public static void SetTutorialStep(TutorialStep step)
    {
        Data.tutorialStep = (int)step;
        Save();
    }

    // ── NPC 만남 ──

    // NPC ID(또는 예전 방식의 첫 만남 에셋 이름)를 만난 적이 있는지
    public static bool HasMetNpc(string npcId) => Has(Data.metNpcs, npcId);

    // NpcInteract가 첫 만남 대화를 마쳤을 때 부른다
    public static void MarkNpcMet(string npcId) => Add(Data.metNpcs, npcId);

    // ── 본 대화·스토리 (에셋 이름) ──

    // 첫 만남 기록(예전 방식)도 함께 본다
    public static bool HasSeenDialogue(string assetName) =>
        Has(Data.seenDialogues, assetName) || Has(Data.metNpcs, assetName);

    public static void MarkDialogueSeen(string assetName) => Add(Data.seenDialogues, assetName);

    // ── 본 안내 ──

    public static bool HasSeenGuide(string guideId) => Has(Data.seenGuides, guideId);

    public static void MarkGuideSeen(string guideId) => Add(Data.seenGuides, guideId);

    // ── 방문한 구역 ──

    public static bool HasVisitedArea(string areaId) => Has(Data.visitedAreas, areaId);

    public static void MarkAreaVisited(string areaId) => Add(Data.visitedAreas, areaId);

    // ── 퀘스트 ──

    // 진행 중 퀘스트 ID. 없으면 빈 문자열
    public static string GetActiveQuest() => Data.activeQuest ?? "";

    public static int GetActiveQuestStep() => Data.activeQuestStep;

    public static void SetActiveQuest(string questId, int step)
    {
        questId = questId ?? "";
        if (Data.activeQuest == questId && Data.activeQuestStep == step) return;
        Data.activeQuest = questId;
        Data.activeQuestStep = step;
        Save();
    }

    public static bool IsQuestFinished(string questId) => Has(Data.finishedQuests, questId);

    public static void MarkQuestFinished(string questId) => Add(Data.finishedQuests, questId);

    // ── 초기화 ──

    // 플레이 진행 데이터 초기화 (설정값 같은 다른 키는 건드리지 않는다).
    // 타이틀에서 튜토리얼을 새로 시작할 때도 이걸 부른다
    public static void ResetProgress()
    {
        DeleteOldKeys();
        data = new Bundle();
        Save();
        PlayerNameChanged?.Invoke();
    }

    // 에디터 메뉴 "확인"용 한 줄 요약
    public static string Summary()
    {
        var d = Data;
        return $"챕터 {d.chaptersCleared}, 이름 '{d.playerName}', 튜토리얼 {(TutorialStep)d.tutorialStep}, " +
               $"퀘스트 '{d.activeQuest}' {d.activeQuestStep}단계, 끝낸 퀘스트 {d.finishedQuests.Count}개, " +
               $"만난 NPC {d.metNpcs.Count}, 본 대화 {d.seenDialogues.Count}, 본 안내 {d.seenGuides.Count}, 방문 구역 {d.visitedAreas.Count}";
    }

    // ── 내부 ──

    private static bool Has(List<string> list, string id) => !string.IsNullOrEmpty(id) && list.Contains(id);

    private static void Add(List<string> list, string id)
    {
        if (string.IsNullOrEmpty(id) || list.Contains(id)) return;
        list.Add(id);
        Save();
    }

    private static void Load()
    {
        string path = FilePath;
        if (File.Exists(path))
        {
            try
            {
                data = JsonUtility.FromJson<Bundle>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveData] save.json을 읽지 못해 새로 시작합니다: {e.Message}");
                data = null;
            }
            if (data == null) data = new Bundle();
            FillNulls(data);
            if (data.version < CurrentVersion) Upgrade(data);
            return;
        }

        // 파일이 없으면 예전 PlayerPrefs 기록을 옮겨 온다 (없으면 빈 묶음)
        data = new Bundle();
        bool migrated = false;
        if (PlayerPrefs.HasKey(OldChaptersClearedKey))
        {
            data.chaptersCleared = PlayerPrefs.GetInt(OldChaptersClearedKey, 0);
            migrated = true;
        }
        else if (PlayerPrefs.GetInt(OldTutorialClearedKey, 0) == 1)
        {
            data.chaptersCleared = 1;
            migrated = true;
        }
        if (PlayerPrefs.HasKey(OldPlayerNameKey))
        {
            data.playerName = PlayerPrefs.GetString(OldPlayerNameKey, "");
            migrated = true;
        }
        string met = PlayerPrefs.GetString(OldMetNpcsKey, "");
        if (met.Length > 0)
        {
            foreach (var id in met.Split('|'))
                if (id.Length > 0 && !data.metNpcs.Contains(id)) data.metNpcs.Add(id);
            migrated = true;
        }
        if (!migrated) return; // 새 게임: 처음 바뀔 때 파일이 생긴다

        Save();
        DeleteOldKeys();
        Debug.Log("[SaveData] 예전 PlayerPrefs 기록을 save.json으로 옮겼습니다.");
    }

    private static void FillNulls(Bundle d)
    {
        if (d.playerName == null) d.playerName = "";
        if (d.activeQuest == null) d.activeQuest = "";
        if (d.metNpcs == null) d.metNpcs = new List<string>();
        if (d.seenDialogues == null) d.seenDialogues = new List<string>();
        if (d.seenGuides == null) d.seenGuides = new List<string>();
        if (d.visitedAreas == null) d.visitedAreas = new List<string>();
        if (d.finishedQuests == null) d.finishedQuests = new List<string>();
    }

    // 옛 형식의 파일을 지금 형식으로 고친다. 버전을 올릴 때 여기에 단계를 추가한다
    private static void Upgrade(Bundle d)
    {
        d.version = CurrentVersion;
        Save();
    }

    private static void DeleteOldKeys()
    {
        PlayerPrefs.DeleteKey(OldChaptersClearedKey);
        PlayerPrefs.DeleteKey(OldTutorialClearedKey);
        PlayerPrefs.DeleteKey(OldPlayerNameKey);
        PlayerPrefs.DeleteKey(OldMetNpcsKey);
        PlayerPrefs.Save();
    }

    private static void Save()
    {
        if (data == null) return;
        data.version = CurrentVersion;
        try
        {
            string path = FilePath;
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path); // 쓰다가 꺼져도 옛 파일이 반쯤 덮이지 않게
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveData] save.json 저장 실패: {e.Message}");
        }
        Changed?.Invoke();
    }
}
