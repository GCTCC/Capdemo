using System;
using UnityEditor;
using UnityEngine;

// 별도 테스트 어셈블리 없이 Unity에서 실제 구현을 호출한다.
public static class RhythmSettingsChecks
{
    [MenuItem("Tools/CapDemo/리듬게임/리듬 설정 수치 검사")]
    public static void CheckMath()
    {
        RequireNear(NoteScrollSetting.ToWorldSpeed(15f, 8.5f), 15, "기존 기본 배속 보존");
        RequireNear(NoteScrollSetting.ToWorldSpeed(15f, 4f) / NoteScrollSetting.ToWorldSpeed(15f, 8f), 0.5, "화면 배속과 이동 속도 비례");
        float slow = NoteScrollSetting.ToWorldSpeed(15, 4);
        float fast = NoteScrollSetting.ToWorldSpeed(15, 10);
        float distance = 19.85f - 1.15f;
        RequireNear(NoteScrollSetting.ApproachSeconds(19.85f, 1.15f, slow) * slow, distance, "느린 배속의 생성 거리");
        RequireNear(NoteScrollSetting.ApproachSeconds(19.85f, 1.15f, fast) * fast, distance, "빠른 배속의 생성 거리");
        RequireNear(JudgementTimingSetting.ChartTime(10, 150, 40), 9.81, "양수 싱크는 판정 시계를 늦춤");
        RequireNear(JudgementTimingSetting.ChartTime(10, 150, -40), 9.89, "음수 싱크는 판정 시계를 앞당김");

        // 이전 곡 복제 방식과 새 판정 시계 방식이 동일한 입력 오차를 내는지 확인한다.
        foreach (int sync in new[] { -200, -12, 0, 12, 200 })
        {
            double oldError = (10 - 0.14583333) - (9.8 + sync / 1000.0);
            double newError = JudgementTimingSetting.ChartTime(10, 145.83333, sync) - 9.8;
            RequireNear(oldError, newError, "기존 채보 싱크 호환: " + sync);
        }
        Debug.Log("[RhythmSettings] 수치 검사 통과: 기본값/배속 비례/생성 거리/싱크 부호/기존 판정 호환");
    }

    [MenuItem("Tools/CapDemo/리듬게임/리듬 설정 연결 검사 (일시정지 중)")]
    public static void CheckRuntime()
    {
        var controller = UnityEngine.Object.FindAnyObjectByType<RhythmGameControllerV2>();
        if (controller == null || controller.Clock == null || !controller.Clock.IsPaused)
            throw new InvalidOperationException("v2 게임을 실행하고 곡 재생 중 ESC로 일시정지한 뒤 검사하세요.");
        var notes = controller.GetComponent<NoteManager>();
        float originalSpeed = RhythmSettings.NoteSpeed;
        int originalSync = RhythmSettings.SyncMs;
        int originalCount = notes.ActiveNoteCount;
        double songTime = controller.Clock.SongTime;
        var source = controller.MusicSource;
        float pitch = source != null ? source.pitch : 1;
        try
        {
            RhythmSettings.NoteSpeed = originalSpeed == 4f ? 8f : 4f;
            RhythmSettings.SyncMs = originalSync == 40 ? -40 : 40;
            RequireNear(notes.ScrollSpeed, controller.NoteSpeed, "설정 이벤트 → 노트 매니저");
            RequireNear(controller.Clock.UserSyncMilliseconds, RhythmSettings.SyncMs, "설정 이벤트 → 판정 시계");
            RequireNear(controller.Clock.SongTime, songTime, "설정 중 음악 시각 유지");
            RequireNear(notes.ActiveNoteCount, originalCount, "활성 노트를 지우지 않음");
            if (source != null) RequireNear(source.pitch, pitch, "음악 재생 속도 유지");
            RequireNear(GameSettings.NoteSpeed, RhythmSettings.NoteSpeed, "기존 설정 창과 배속 공유");
            RequireNear(GameSettings.SyncMs, RhythmSettings.SyncMs, "기존 설정 창과 싱크 공유");
            Debug.Log("[RhythmSettings] 연결 검사 통과: 변경 이벤트/활성 노트 유지/음악 시각·pitch 유지/기존 API 공유");
        }
        finally
        {
            RhythmSettings.NoteSpeed = originalSpeed;
            RhythmSettings.SyncMs = originalSync;
        }
    }

    [MenuItem("Tools/CapDemo/리듬게임/리듬 설정 연결 검사 (일시정지 중)", true)]
    private static bool CanCheckRuntime() { return EditorApplication.isPlaying; }

    private static void RequireNear(double actual, double expected, string label)
    {
        if (double.IsNaN(actual) || Math.Abs(actual - expected) > 0.00001)
            throw new InvalidOperationException(label + ": expected " + expected + ", actual " + actual);
    }
}
