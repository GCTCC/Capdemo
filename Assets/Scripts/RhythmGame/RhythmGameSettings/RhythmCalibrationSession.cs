using System;
using System.Collections.Generic;

// 음원 시각과 판정 시각을 구분하는 측정 모델. UI/입력 장치와 독립적으로 검사할 수 있다.
public sealed class RhythmCalibrationSession
{
    public const double BeatInterval = 0.75; // 80 BPM
    public const int MinimumSamples = 8;
    public const double InputWindowMs = 250;
    private const int Capacity = 24;
    private readonly List<double> desiredOffsets = new List<double>();
    private long lastAcceptedBeat = -1;

    public int Count { get { return desiredOffsets.Count; } }
    public double MedianDesiredMs { get { return Median(desiredOffsets); } }
    public double VariationMs
    {
        get
        {
            double median = MedianDesiredMs;
            var deviations = new List<double>();
            foreach (double value in desiredOffsets) deviations.Add(Math.Abs(value - median));
            return Median(deviations);
        }
    }
    public bool CanSuggest { get { return Count >= MinimumSamples && VariationMs <= 40; } }
    public int SuggestedSyncMs
    {
        get { return JudgementTimingSetting.Normalize((int)Math.Round(MedianDesiredMs)); }
    }

    public bool TryTap(double referenceTime, double deviceCalibrationMs, int draftSyncMs,
        out double errorMs, out double targetTime)
    {
        errorMs = 0;
        targetTime = 0;
        if (double.IsNaN(referenceTime) || double.IsInfinity(referenceTime) || referenceTime < 0) return false;
        double chartTime = JudgementTimingSetting.ChartTime(referenceTime, deviceCalibrationMs, draftSyncMs);
        long beat = (long)Math.Round(chartTime / BeatInterval);
        if (beat < 0 || beat <= lastAcceptedBeat) return false;
        targetTime = beat * BeatInterval;
        errorMs = (chartTime - targetTime) * 1000.0;
        if (Math.Abs(errorMs) > InputWindowMs) return false;
        lastAcceptedBeat = beat;
        // 절대 추천값을 보관하므로 임시 싱크를 바꾸어도 앞선 측정값을 재사용할 수 있다.
        desiredOffsets.Add(errorMs + JudgementTimingSetting.Normalize(draftSyncMs));
        if (desiredOffsets.Count > Capacity) desiredOffsets.RemoveAt(0);
        return true;
    }

    public void Reset() { desiredOffsets.Clear(); lastAcceptedBeat = -1; }

    private static double Median(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = new List<double>(values);
        sorted.Sort();
        int middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) * 0.5;
    }
}
