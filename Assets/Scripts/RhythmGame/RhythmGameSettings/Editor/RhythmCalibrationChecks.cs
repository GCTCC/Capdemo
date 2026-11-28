using System;
using UnityEditor;
using UnityEngine;

public static class RhythmCalibrationChecks
{
    [MenuItem("Tools/CapDemo/리듬게임/싱크 프리뷰 계산 검사")]
    public static void Run()
    {
        const double calibration = 145.83333;
        var session = new RhythmCalibrationSession();
        for (int i = 1; i <= 8; i++)
        {
            double error, target;
            int draft = i <= 4 ? 12 : 40;
            Require(session.TryTap(i * 0.75 + calibration / 1000 + 0.04, calibration, draft, out error, out target), "박자 입력");
            Near(error, 40 - draft, "임시 싱크를 바꾼 뒤의 잔여 오차");
        }
        Require(session.CanSuggest && session.SuggestedSyncMs == 40, "양수 추천값/임시 조절 중 중복 보정 방지");
        double duplicateError, duplicateTarget;
        Require(!session.TryTap(8 * 0.75 + calibration / 1000 + 0.04, calibration, 40,
            out duplicateError, out duplicateTarget), "같은 박자 중복 입력 거절");
        Require(session.Count == 8, "중복 입력이 표본 수를 늘리지 않음");
        Require(session.TryTap(9 * 0.75 + calibration / 1000 + 0.18, calibration, 40,
            out duplicateError, out duplicateTarget), "일시적인 흔들림 표본");
        Require(session.SuggestedSyncMs == 40, "한 번의 큰 오차에 추천값이 끌려가지 않음");
        session.Reset();
        for (int i = 1; i <= 30; i++)
        {
            double error, target;
            Require(session.TryTap(i * 0.75 + calibration / 1000 - 0.037, calibration, 0, out error, out target), "빠른 입력");
        }
        Require(session.Count == 24 && session.SuggestedSyncMs == -37, "음수 추천값/표본 용량");
        Require(NoteScrollSetting.Normalize(8.51f) > 8.50f, "0.01 배속 정밀 조절 보존");
        var clock = new RhythmPreviewSampleClock();
        Near(clock.Read(575520, 48000, 12), 11.99, "루프 직전");
        Near(clock.Read(480, 48000, 12), 12.01, "루프 경계의 연속 시각");
        Near(clock.Read(400, 48000, 12), 12.01, "작은 sample 후퇴에서 시간 유지");
        clock.Reset();
        Near(clock.Read(480, 48000, 12), 0.01, "프리뷰 재시작");
        Debug.Log("[RhythmCalibration] 계산 검사 통과: 오차 부호/중앙값/중복 입력/중간 설정 변경/0.01 배속/오디오 루프");
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
    private static void Near(double actual, double expected, string message)
    {
        Require(Math.Abs(actual - expected) < 0.00001, message + ": " + actual + " != " + expected);
    }
}
