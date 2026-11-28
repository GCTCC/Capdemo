using System;

// 반복 오디오의 sample 위치를 연속 시각으로 바꾼다. 게임의 timeSamples 기준과 맞춘다.
// 포커스를 잃으면 프리뷰가 재시작하므로 긴 백그라운드 정지 동안의 루프를 추정하지 않는다.
public sealed class RhythmPreviewSampleClock
{
    private double previousPhase, loopOffset, time;
    public double Read(int sample, int frequency, double duration)
    {
        if (frequency <= 0 || duration <= 0) return time;
        double phase = Math.Max(0, sample) / (double)frequency;
        if (previousPhase - phase > duration * 0.5) loopOffset += duration;
        time = Math.Max(time, loopOffset + phase);
        previousPhase = phase;
        return time;
    }
    public void Reset() { previousPhase = loopOffset = time = 0; }
}
