using UnityEngine;

// 로컬 플레이 데이터 창구. 저장 방식을 바꿀 때는 이 파일만 고치면 된다.
// 지금은 PlayerPrefs(키-값 저장소)를 사용한다.
public static class SaveData
{
    private const string TutorialClearedKey = "TutoCleared";
    private const string PlayerNameKey = "PlayerName";

    // TutoScene을 끝까지 플레이했는지
    public static bool IsTutorialCleared() => PlayerPrefs.GetInt(TutorialClearedKey, 0) == 1;

    // TutoScene 마지막에서 호출한다
    public static void MarkTutorialCleared()
    {
        PlayerPrefs.SetInt(TutorialClearedKey, 1);
        PlayerPrefs.Save();
    }

    // 튜토리얼에서 입력받은 플레이어 이름. 아직 없으면 빈 문자열
    public static string GetPlayerName() => PlayerPrefs.GetString(PlayerNameKey, "");

    public static void SetPlayerName(string name)
    {
        PlayerPrefs.SetString(PlayerNameKey, name);
        PlayerPrefs.Save();
    }

    // 플레이 진행 데이터 초기화 (설정값 같은 다른 키는 건드리지 않는다)
    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(TutorialClearedKey);
        PlayerPrefs.DeleteKey(PlayerNameKey);
        PlayerPrefs.Save();
    }
}
