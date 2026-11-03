// 스토리·싱글 모드가 RhythmScene에 전달하는 진입 정보다.
public enum RhythmEntryMode
{
    Direct,
    Single,
    Story
}

public sealed class RhythmLaunchRequest
{
    public RhythmEntryMode mode;
    public string songName;
    public string returnSceneName;
}

// 씬이 바뀌어도 다음 RhythmScene이 한 번 소비할 요청을 보관한다.
// ScriptableObject나 싱글턴 오브젝트 없이도 초기 통합 단계에서 사용할 수 있다.
public static class RhythmLaunchContext
{
    private static RhythmLaunchRequest pendingRequest;

    public static void RequestSingle(string songName, string returnSceneName)
    {
        Request(RhythmEntryMode.Single, songName, returnSceneName);
    }

    public static void RequestStory(string songName, string returnSceneName)
    {
        Request(RhythmEntryMode.Story, songName, returnSceneName);
    }

    public static void Request(
        RhythmEntryMode mode,
        string songName,
        string returnSceneName)
    {
        pendingRequest = new RhythmLaunchRequest();
        pendingRequest.mode = mode;
        pendingRequest.songName = songName ?? string.Empty;
        pendingRequest.returnSceneName = returnSceneName ?? string.Empty;
    }

    public static bool TryConsume(out RhythmLaunchRequest request)
    {
        request = pendingRequest;
        pendingRequest = null;
        return request != null;
    }

    public static void Clear()
    {
        pendingRequest = null;
    }
}
