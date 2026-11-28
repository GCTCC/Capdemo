using UnityEngine;

// 표시 배속을 실제 이동 속도로 변환한다. 음악 pitch/판정 시각은 바꾸지 않는다.
public static class NoteScrollSetting
{
    public const float Min = 1f;
    public const float Max = 10f;
    public const float Step = 0.5f;
    public const float Precision = 0.01f;
    public const float Default = 8.5f;

    public static float Normalize(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return Default;
        return Mathf.Clamp(Mathf.Round(value / Precision) * Precision, Min, Max);
    }

    public static float ToWorldSpeed(float baseSpeed, float setting)
    {
        if (float.IsNaN(baseSpeed) || float.IsInfinity(baseSpeed) || baseSpeed <= 0f) baseSpeed = 15f;
        return baseSpeed * Normalize(setting) / Default;
    }

    public static float ApproachSeconds(float spawnZ, float judgeZ, float worldSpeed)
    {
        return Mathf.Max(0.1f, (spawnZ - judgeZ) / Mathf.Max(0.01f, worldSpeed));
    }
}
