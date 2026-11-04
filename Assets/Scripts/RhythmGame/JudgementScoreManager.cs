using System;
using UnityEngine;

// 판정 결과의 종류다.
public enum JudgementType
{
    Perfect,
    Great,
    Good,
    Miss
}

// 판정 한 번의 상세 결과를 다른 시스템에 전달하는 값 형식이다.
public struct JudgementResult
{
    public int noteId;
    public int lane;
    public JudgementType judgement;
    public double timeDifference;
}

// 곡이 끝난 뒤 스토리나 결과 화면에 전달할 최종 플레이 결과다.
[Serializable]
public class PlayResult
{
    public int score;
    public int maxCombo;
    public int perfectCount;
    public int greatCount;
    public int goodCount;
    public int missCount;
    public bool isFullCombo;
}

// 입력과 노트의 시간 차이로 판정을 결정하고 점수와 콤보를 계산한다.
[DisallowMultipleComponent]
public class JudgementScoreManager : MonoBehaviour
{
    [Header("Judgement Windows (seconds)")]
    // 입력 오차가 이 값 이하면 PERFECT다.
    [SerializeField]
    private double perfectWindow = 0.050;

    // PERFECT보다 멀고 이 값 이하면 GREAT다.
    [SerializeField]
    private double greatWindow = 0.100;

    // GREAT보다 멀고 이 값 이하면 GOOD이다.
    [SerializeField]
    private double goodWindow = 0.160;

    // 이 값보다 더 일찍 누른 입력은 MISS로 만들지 않고 무시한다.
    // goodWindow와 이 값 사이에서 누른 입력만 조기 MISS가 된다.
    [SerializeField]
    private double earlyMissWindow = 0.300;

    // 판정 결과, 점수와 콤보가 바뀌었음을 UI에 알리는 이벤트다.
    public event Action<JudgementResult> Judged;
    public event Action<int> ScoreChanged;
    public event Action<int> ComboChanged;

    private NoteManager noteManager;

    public int Score { get; private set; }
    public int Combo { get; private set; }
    public int MaxCombo { get; private set; }
    public int PerfectCount { get; private set; }
    public int GreatCount { get; private set; }
    public int GoodCount { get; private set; }
    public int MissCount { get; private set; }

    // 판정 대상 노트를 찾기 위해 NoteManager를 연결한다.
    public void Initialize(NoteManager newNoteManager)
    {
        noteManager = newNoteManager;
        ResetScore();
    }

    // 키를 누른 순간 탭 노트 또는 롱노트 시작점을 판정한다.
    public bool TryJudgePress(int lane, double inputTime)
    {
        if (noteManager == null)
        {
            return false;
        }

        // 같은 레인의 가장 앞 노트만 판정한다.
        // 절댓값이 가장 가까운 노트를 고르면 뒤 노트가 대신 판정될 수 있으므로 사용하지 않는다.
        NoteManager.RuntimeNote note = noteManager.FindEarliestPendingNote(lane);
        if (note == null)
        {
            return false;
        }

        double signedDifference = inputTime - note.data.hitTime;
        double difference = Math.Abs(signedDifference);

        // 노트가 아직 너무 멀리 있다면 실수 입력으로 보지 않고 무시한다.
        // 따라서 화면 앞쪽에서 미리 키를 눌러도 MISS나 노트 제거가 발생하지 않는다.
        double safeEarlyMissWindow = Math.Max(goodWindow, earlyMissWindow);
        if (signedDifference < -safeEarlyMissWindow)
        {
            return false;
        }

        // GOOD 판정창보다 일찍 눌렀다면 가장 앞 노트 하나만 MISS로 처리한다.
        // 해당 노트를 제거한 뒤 종료하므로 같은 입력이 뒤 노트에 전달되지 않는다.
        if (signedDifference < -goodWindow)
        {
            RegisterMiss(note, difference);
            noteManager.ConsumeNote(note);
            return true;
        }

        // 너무 늦은 입력은 NoteManager의 자동 MISS가 담당한다.
        // 이때도 뒤 노트를 대신 판정하지 않는다.
        if (signedDifference > goodWindow)
        {
            return false;
        }

        JudgementType judgement;
        if (difference <= perfectWindow)
        {
            judgement = JudgementType.Perfect;
        }
        else if (difference <= greatWindow)
        {
            judgement = JudgementType.Great;
        }
        else
        {
            judgement = JudgementType.Good;
        }

        ApplySuccessfulJudgement(note, judgement, difference);

        if (note.data.noteType == NoteType.Hold)
        {
            // 롱노트는 시작 판정 후 제거하지 않고 판정선에 붙여 둔다.
            noteManager.BeginHold(note);
        }
        else
        {
            noteManager.ConsumeNote(note);
        }

        return true;
    }

    // 기존 코드에서 사용하던 이름과의 호환을 유지한다.
    public bool TryJudge(int lane, double inputTime)
    {
        return TryJudgePress(lane, inputTime);
    }

    // 키를 뗀 순간 해당 레인에서 유지 중인 롱노트 끝점을 판정한다.
    public bool TryReleaseHold(int lane, double inputTime)
    {
        if (noteManager == null)
        {
            return false;
        }

        NoteManager.RuntimeNote note = noteManager.FindHoldingNote(lane);
        if (note == null)
        {
            return false;
        }

        double difference = Math.Abs(note.data.endTime - inputTime);
        if (difference <= goodWindow)
        {
            JudgementType judgement;
            if (difference <= perfectWindow)
            {
                judgement = JudgementType.Perfect;
            }
            else if (difference <= greatWindow)
            {
                judgement = JudgementType.Great;
            }
            else
            {
                judgement = JudgementType.Good;
            }

            ApplySuccessfulJudgement(note, judgement, difference);
        }
        else
        {
            // 너무 일찍 떼거나 허용 범위를 지난 해제는 MISS다.
            RegisterMiss(note);
        }

        noteManager.ConsumeNote(note);
        return true;
    }

    public bool HasHoldingNote(int lane)
    {
        return noteManager != null && noteManager.FindHoldingNote(lane) != null;
    }

    // 성공 판정에 맞춰 점수, 판정 수와 콤보를 갱신한다.
    private void ApplySuccessfulJudgement(
        NoteManager.RuntimeNote note,
        JudgementType judgement,
        double difference)
    {
        switch (judgement)
        {
            case JudgementType.Perfect:
                Score += 1000;
                PerfectCount++;
                break;

            case JudgementType.Great:
                Score += 700;
                GreatCount++;
                break;

            case JudgementType.Good:
                Score += 400;
                GoodCount++;
                break;
        }

        Combo++;
        MaxCombo = Mathf.Max(MaxCombo, Combo);

        JudgementResult result = new JudgementResult();
        result.noteId = note.data.noteId;
        result.lane = note.data.lane;
        result.judgement = judgement;
        result.timeDifference = difference;

        RaiseEvents(result);
    }

    // NoteManager가 판정 시간을 지나쳤다고 알린 노트를 MISS로 처리한다.
    public void RegisterMiss(
        NoteManager.RuntimeNote note,
        double timeDifference = 0.0)
    {
        if (note == null)
        {
            return;
        }

        Combo = 0;
        MissCount++;

        JudgementResult result = new JudgementResult();
        result.noteId = note.data.noteId;
        result.lane = note.data.lane;
        result.judgement = JudgementType.Miss;
        result.timeDifference = timeDifference;

        RaiseEvents(result);
    }

    // UI와 이펙트가 판정 시스템에 의존하지 않도록 변경 내용을 이벤트로 알린다.
    private void RaiseEvents(JudgementResult result)
    {
        if (Judged != null)
        {
            Judged(result);
        }

        if (ScoreChanged != null)
        {
            ScoreChanged(Score);
        }

        if (ComboChanged != null)
        {
            ComboChanged(Combo);
        }
    }

    // 새 곡을 시작할 때 모든 점수와 판정 기록을 초기화한다.
    public void ResetScore()
    {
        Score = 0;
        Combo = 0;
        MaxCombo = 0;
        PerfectCount = 0;
        GreatCount = 0;
        GoodCount = 0;
        MissCount = 0;
    }

    // 현재 누적 값을 스토리 또는 결과 화면에 전달할 객체로 복사한다.
    public PlayResult CreatePlayResult()
    {
        PlayResult result = new PlayResult();
        result.score = Score;
        result.maxCombo = MaxCombo;
        result.perfectCount = PerfectCount;
        result.greatCount = GreatCount;
        result.goodCount = GoodCount;
        result.missCount = MissCount;
        result.isFullCombo = MissCount == 0;
        return result;
    }
}
