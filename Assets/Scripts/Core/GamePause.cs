using UnityEngine;
using UnityEngine.SceneManagement;

// 일시정지 상태 하나를 모두가 같이 본다. PausePanel이 켜고 끈다.
//   - Time.timeScale = 0 (맵 플레이어·물리·애니메이션 멈춤)
//   - 모든 소리 일시정지 (AudioListener.pause). 일시정지 창 자기 소리는 ignoreListenerPause로 낸다
//   - 스토리(대사 출력·홀로그램·일러스트 전환)는 실제 시간으로 움직이므로 Delta를 써서 멈춘다
public static class GamePause
{
    public static bool IsPaused { get; private set; }

    private static float timeScaleBefore = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        IsPaused = false;
        timeScaleBefore = 1f;
        // 일시정지인 채로 씬이 넘어가면(메인화면으로 돌아가기) 새 씬에서는 풀어 준다
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Resume();
    }

    // 씬을 넘기는 동안: 스토리·맵은 멈춘 채로 두고 소리만 다시 켠다 (지직 전환 효과음이 들리게)
    public static void ReleaseAudio() => AudioListener.pause = false;

    // 일시정지 중이면 0, 아니면 실제 시간 델타. 스토리 연출에서 Time.unscaledDeltaTime 대신 쓴다
    public static float Delta => IsPaused ? 0f : Time.unscaledDeltaTime;

    public static void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;
        timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    public static void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = timeScaleBefore;
        AudioListener.pause = false;
    }
}
