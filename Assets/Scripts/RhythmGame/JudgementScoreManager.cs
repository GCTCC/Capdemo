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

    // 지정한 레인의 입력 시각을 사용해 가장 가까운 노트를 판정한다.
    public bool TryJudge(int lane, double inputTime)
    {
        if (noteManager == null)
        {
            return false;
        }

        double difference;
        NoteManager.RuntimeNote note = noteManager.FindClosestNote(
            lane,
            inputTime,
            out difference);

        // 같은 레인에 노트가 없거나 GOOD 범위보다 멀면 입력을 무시한다.
        if (note == null || difference > goodWindow)
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
        noteManager.ConsumeNote(note);
        return true;
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
    public void RegisterMiss(NoteManager.RuntimeNote note)
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
        result.timeDifference = 0.0;

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
