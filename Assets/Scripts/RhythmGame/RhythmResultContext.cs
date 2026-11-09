using UnityEngine;

// 씬 오브젝트를 유지하지 않고 플레이 결과의 복사본만 다음 씬에 전달한다.
public static class RhythmResultContext
{
    public static string SongTitle { get; private set; }
    public static PlayResult Result { get; private set; }
    public static RhythmEntryMode Mode { get; private set; }
    public static string ReturnSceneName { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        SongTitle = null;
        Result = null;
        Mode = RhythmEntryMode.Single;
        ReturnSceneName = null;
    }

    public static void Set(string title, PlayResult source, RhythmEntryMode mode,
        string returnSceneName)
    {
        SongTitle = title;
        Mode = mode;
        ReturnSceneName = returnSceneName;
        Result = new PlayResult
        {
            score = source.score,
            maxCombo = source.maxCombo,
            perfectCount = source.perfectCount,
            greatCount = source.greatCount,
            goodCount = source.goodCount,
            missCount = source.missCount,
            isFullCombo = source.isFullCombo
        };
    }
}
