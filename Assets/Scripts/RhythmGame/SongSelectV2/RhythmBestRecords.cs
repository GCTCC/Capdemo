using UnityEngine;
using UnityEngine.SceneManagement;

// 곡 + 난이도별 최고 기록(점수·랭크·풀콤보)과 "한 번이라도 골라 본 곡"(NEW 표시용)을 PlayerPrefs에 저장한다.
// 결과 화면 코드(RhythmResultSceneController)는 건드리지 않는다:
// 결과 씬이 열릴 때 RhythmResultContext에 담긴 결과를 읽어서 여기서 저장한다.
// 키: best_{곡}_{난이도} = "점수|랭크|풀콤보(0/1)",  seen_{곡} = 1
public static class RhythmBestRecords
{
    public struct Record
    {
        public int score;
        public string rank;     // S/A/B/C/D, 기록 없으면 ""
        public bool fullCombo;
        public bool HasRecord => !string.IsNullOrEmpty(rank);
    }

    // 지금은 곡당 채보가 하나라 결과는 NORMAL로 저장한다 (난이도 탭은 아직 목업)
    public const string DefaultDifficulty = "NORMAL";

    private static string lastSavedResult;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        lastSavedResult = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 결과 씬(이름에 Result가 들어간 씬)이 열리면 방금 결과를 저장한다
        if (!scene.name.Contains("Result")) return;
        var result = RhythmResultContext.Result;
        string song = RhythmResultContext.SongTitle;
        if (result == null || string.IsNullOrEmpty(song)) return;

        // 같은 결과를 두 번 저장하지 않게
        string stamp = song + "|" + result.score + "|" + result.maxCombo + "|" + result.missCount;
        if (stamp == lastSavedResult) return;
        lastSavedResult = stamp;

        Submit(song, DefaultDifficulty, result);
    }

    public static void Submit(string song, string difficulty, PlayResult result)
    {
        var old = Get(song, difficulty);
        string rank = RankOf(result);
        bool fc = result.isFullCombo || old.fullCombo;
        int score = Mathf.Max(old.score, result.score);
        if (old.HasRecord && RankValue(old.rank) > RankValue(rank)) rank = old.rank;
        PlayerPrefs.SetString(Key(song, difficulty), score + "|" + rank + "|" + (fc ? 1 : 0));
        PlayerPrefs.Save();
    }

    public static Record Get(string song, string difficulty)
    {
        var record = new Record { rank = "" };
        string raw = PlayerPrefs.GetString(Key(song, difficulty), "");
        var parts = raw.Split('|');
        if (parts.Length < 3) return record;
        int.TryParse(parts[0], out record.score);
        record.rank = parts[1];
        record.fullCombo = parts[2] == "1";
        return record;
    }

    public static bool IsNew(string song) => PlayerPrefs.GetInt("seen_" + song, 0) == 0;

    public static void MarkSeen(string song)
    {
        if (!IsNew(song)) return;
        PlayerPrefs.SetInt("seen_" + song, 1);
        PlayerPrefs.Save();
    }

    // 정확도로 랭크를 정한다 (PERFECT 100%, GREAT 70%, GOOD 40%)
    public static string RankOf(PlayResult r)
    {
        int total = r.perfectCount + r.greatCount + r.goodCount + r.missCount;
        if (total <= 0) return "D";
        float acc = (r.perfectCount + r.greatCount * 0.7f + r.goodCount * 0.4f) / total;
        if (acc >= 0.95f) return "S";
        if (acc >= 0.90f) return "A";
        if (acc >= 0.80f) return "B";
        if (acc >= 0.70f) return "C";
        return "D";
    }

    private static int RankValue(string rank)
    {
        switch (rank)
        {
            case "S": return 5;
            case "A": return 4;
            case "B": return 3;
            case "C": return 2;
            case "D": return 1;
            default: return 0;
        }
    }

    private static string Key(string song, string difficulty) => "best_" + song + "_" + difficulty;
}
