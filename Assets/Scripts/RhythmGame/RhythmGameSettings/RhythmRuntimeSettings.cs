using UnityEngine;

// 컨트롤러가 자동으로 연결한다. 씬에 수동 추가할 필요가 없다.
// 노트 생성/위치, 마디선, 판정 시계가 같은 설정을 사용하도록 적용한다.
[DisallowMultipleComponent]
public class RhythmRuntimeSettings : MonoBehaviour
{
    private RhythmClock clock;
    private NoteManager notes;
    private RhythmPlayFieldV2 field;
    private float baseSpeed = 15f;

    public float EffectiveNoteSpeed { get; private set; } = 15f;

    public void Initialize(RhythmClock rhythmClock, NoteManager noteManager,
        RhythmPlayFieldV2 playField, float sceneBaseSpeed)
    {
        clock = rhythmClock;
        notes = noteManager;
        field = playField;
        baseSpeed = sceneBaseSpeed;
        Apply();
    }

    private void OnEnable()
    {
        RhythmSettings.Changed += Apply;
        Apply();
    }

    private void OnDisable() { RhythmSettings.Changed -= Apply; }
    private void OnApplicationPause(bool paused) { if (paused) RhythmSettings.Save(); }
    private void OnApplicationQuit() { RhythmSettings.Save(); }

    public void Apply()
    {
        EffectiveNoteSpeed = NoteScrollSetting.ToWorldSpeed(baseSpeed, RhythmSettings.NoteSpeed);
        if (clock == null || notes == null || field == null) return;
        clock.UserSyncMilliseconds = RhythmSettings.SyncMs;
        notes.SetScrollSpeed(EffectiveNoteSpeed);
        field.SetTiming(clock, EffectiveNoteSpeed);
    }
}
