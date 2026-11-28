using System;
using UnityEngine;

// 곡 선택/설정 창/인게임이 함께 쓰는 리듬 설정 저장소. 기존 저장 키를 그대로 사용한다.
public static class RhythmSettings
{
    public const string NoteSpeedKey = "opt_noteSpeed";
    public const string SyncKey = "opt_syncMs";
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() { Changed = null; }

    public static float NoteSpeed
    {
        get { return NoteScrollSetting.Normalize(PlayerPrefs.GetFloat(NoteSpeedKey, NoteScrollSetting.Default)); }
        set { SetValues(value, SyncMs); }
    }

    public static int SyncMs
    {
        get { return JudgementTimingSetting.Normalize(PlayerPrefs.GetInt(SyncKey, JudgementTimingSetting.Default)); }
        set { SetValues(NoteSpeed, value); }
    }

    // 확인 버튼은 두 값을 함께 저장하고 변경 이벤트를 한 번만 보낸다.
    public static void SetValues(float speed, int syncMs)
    {
        float normalizedSpeed = NoteScrollSetting.Normalize(speed);
        int normalizedSync = JudgementTimingSetting.Normalize(syncMs);
        if (PlayerPrefs.HasKey(NoteSpeedKey) && PlayerPrefs.HasKey(SyncKey)
            && Mathf.Approximately(normalizedSpeed, NoteSpeed) && normalizedSync == SyncMs) return;
        PlayerPrefs.SetFloat(NoteSpeedKey, normalizedSpeed);
        PlayerPrefs.SetInt(SyncKey, normalizedSync);
        Save();
        Changed?.Invoke();
    }

    public static void Save() { PlayerPrefs.Save(); }
}
