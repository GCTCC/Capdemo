using UnityEngine;

// 로컬 플레이 데이터 창구. 저장 방식을 바꿀 때는 이 파일만 고치면 된다.
// 지금은 PlayerPrefs(키-값 저장소)를 사용한다.
public static class SaveData
{
    // 클리어한 챕터 수. 0 = 아직 튜토리얼 챕터(첫 곡까지) 진행 중, 1 이상 = 허브가 열림
    private const string ChaptersClearedKey = "ChaptersCleared";
    // 예전 저장값(튜토리얼 완료 0/1). 처음 읽을 때 ChaptersCleared로 옮기고 지운다
    private const string OldTutorialClearedKey = "TutoCleared";
    private const string PlayerNameKey = "PlayerName";

    public static int GetChaptersCleared()
    {
        if (!PlayerPrefs.HasKey(ChaptersClearedKey) && PlayerPrefs.GetInt(OldTutorialClearedKey, 0) == 1)
        {
            // 예전 방식으로 튜토리얼을 끝낸 기록이 있으면 1장 클리어로 옮긴다
            PlayerPrefs.SetInt(ChaptersClearedKey, 1);
            PlayerPrefs.DeleteKey(OldTutorialClearedKey);
            PlayerPrefs.Save();
        }
        return PlayerPrefs.GetInt(ChaptersClearedKey, 0);
    }

    // 챕터를 끝냈을 때 부른다. 이미 더 많이 클리어했으면 줄이지 않는다
    public static void MarkChapterCleared(int chaptersCleared)
    {
        if (chaptersCleared <= GetChaptersCleared()) return;
        PlayerPrefs.SetInt(ChaptersClearedKey, chaptersCleared);
        PlayerPrefs.Save();
    }

    // 튜토리얼 챕터(첫 곡까지)를 끝냈는지 = 1장 이상 클리어
    public static bool IsTutorialCleared() => GetChaptersCleared() >= 1;

    public static void MarkTutorialCleared() => MarkChapterCleared(1);

    // 튜토리얼에서 입력받은 플레이어 이름. 아직 없으면 빈 문자열
    public static string GetPlayerName() => PlayerPrefs.GetString(PlayerNameKey, "");

    // 이름이 바뀌면 알린다 (PlayerNameText가 다시 그린다)
    public static event System.Action PlayerNameChanged;

    public static void SetPlayerName(string name)
    {
        PlayerPrefs.SetString(PlayerNameKey, name);
        PlayerPrefs.Save();
        PlayerNameChanged?.Invoke();
    }

    // 첫 만남 대화를 끝까지 본 NPC 목록. NPC 이름(Id)을 "|"로 이어 한 키에 저장한다
    private const string MetNpcsKey = "MetNpcs";
    private const char MetNpcsSeparator = '|';

    public static bool HasMetNpc(string npcId) =>
        System.Array.IndexOf(PlayerPrefs.GetString(MetNpcsKey, "").Split(MetNpcsSeparator), npcId) >= 0;

    // NpcInteract가 첫 만남 대화를 마쳤을 때 부른다
    public static void MarkNpcMet(string npcId)
    {
        if (string.IsNullOrEmpty(npcId) || HasMetNpc(npcId)) return;
        string met = PlayerPrefs.GetString(MetNpcsKey, "");
        PlayerPrefs.SetString(MetNpcsKey, met.Length == 0 ? npcId : met + MetNpcsSeparator + npcId);
        PlayerPrefs.Save();
    }

    // 플레이 진행 데이터 초기화 (설정값 같은 다른 키는 건드리지 않는다).
    // 튜토리얼 챕터는 중간 저장이 없으므로, 타이틀에서 튜토리얼을 새로 시작할 때도 이걸 부른다
    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(ChaptersClearedKey);
        PlayerPrefs.DeleteKey(OldTutorialClearedKey);
        PlayerPrefs.DeleteKey(PlayerNameKey);
        PlayerPrefs.DeleteKey(MetNpcsKey);
        PlayerPrefs.Save();
        PlayerNameChanged?.Invoke();
    }
}
