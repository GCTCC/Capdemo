using UnityEngine;

// 기존 UI의 싱크(판정 시점). 양수는 노트 도착/입력 판정을 음악보다 늦춘다.
public static class JudgementTimingSetting
{
    public const int Min = -200;
    public const int Max = 200;
    public const int Step = 2;
    public const int Default = 12;

    // 기존 저장값(홀수 ms 포함)은 범위 안이면 유지한다. UI 증감에는 Step을 사용한다.
    public static int Normalize(int milliseconds) { return Mathf.Clamp(milliseconds, Min, Max); }

    public static double ChartTime(double songTime, double deviceCalibrationMs, int userOffsetMs)
    {
        return songTime - deviceCalibrationMs / 1000.0 - Normalize(userOffsetMs) / 1000.0;
    }
}
