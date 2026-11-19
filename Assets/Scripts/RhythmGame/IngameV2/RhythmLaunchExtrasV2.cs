// 곡 선택 v2 → 인게임 v2로 넘기는 추가 정보 (난이도).
// 원본 RhythmLaunchContext는 고치지 않으려고 따로 둔다. 곡 선택(SongSelectHoloController)이 PLAY 직전에 채운다.
// 값이 없으면 인게임은 NORMAL로 보여 준다.
public static class RhythmLaunchExtrasV2
{
    public static string Difficulty { get; private set; }
    public static int Level { get; private set; }
    public static bool HasValue { get; private set; }

    // 도메인 리로드가 꺼져 있어서 지난 Play 값이 남지 않게 비운다
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        Clear();
    }

    public static void Set(string difficulty, int level)
    {
        Difficulty = string.IsNullOrWhiteSpace(difficulty) ? "NORMAL" : difficulty.ToUpperInvariant();
        Level = level;
        HasValue = true;
    }

    public static void Clear()
    {
        Difficulty = "NORMAL";
        Level = 0;
        HasValue = false;
    }
}
