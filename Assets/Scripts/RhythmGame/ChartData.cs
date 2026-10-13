using System;
using System.Collections.Generic;
using UnityEngine;

// 채보에서 사용할 노트 종류다. 현재 프로토타입은 Tap만 실제 판정한다.
public enum NoteType
{
    Tap,
    Hold
}

// 채보에 저장되는 노트 하나의 변하지 않는 원본 데이터다.
[Serializable]
public struct NoteData
{
    // 채보 안에서 노트를 구분하기 위한 고유 번호다.
    public int noteId;

    // 노트가 내려올 레인 번호이며 0부터 5까지 사용한다.
    [Range(0, 5)]
    public int lane;

    // 음악 시작을 0초로 보았을 때 판정선에 도착해야 하는 시간이다.
    public double hitTime;

    // 탭 노트인지 홀드 노트인지 나타낸다.
    public NoteType noteType;

    // 홀드 노트일 때 손을 떼어야 하는 시간이다.
    public double endTime;
}

// 곡 중간의 BPM 변경 정보를 나타낸다.
[Serializable]
public struct BpmEventData
{
    // BPM이 변경되는 박자 위치다.
    public double beat;

    // 해당 위치부터 적용할 새로운 BPM이다.
    public float bpm;
}

// 한 곡의 음악 파일과 채보 전체를 저장하는 ScriptableObject다.
[CreateAssetMenu(fileName = "ChartData", menuName = "Rhythm Game/Chart Data")]
public class ChartData : ScriptableObject
{
    [Header("Song")]
    // 저장 데이터와 스토리에서 곡을 식별할 때 사용할 ID다.
    public string songId = "prototype_song";

    // UI에 표시할 곡 제목이다.
    public string title = "Prototype Song";

    // 실제로 재생할 음악 파일이며 비워 두면 무음으로 시간을 진행한다.
    public AudioClip audioClip;

    [Header("Timing")]
    // 곡 시작 시 적용할 기본 BPM이다.
    public float initialBpm = 120.0f;

    // 음악 파일과 채보 사이의 시간 차이를 보정하는 값이다.
    public double musicOffset;

    // 곡 중간의 BPM 변경 목록이다. 현재 프로토타입은 첫 BPM을 기준으로 동작한다.
    public List<BpmEventData> bpmEvents = new List<BpmEventData>();

    [Header("Notes")]
    // 채보 에디터에서 변환된 노트들을 시간순으로 저장한다.
    public List<NoteData> notes = new List<NoteData>();

    // 실제 채보 노트가 하나 이상 들어 있는지 알려준다.
    public bool HasNotes
    {
        get { return notes != null && notes.Count > 0; }
    }

    // 채보가 비어 있을 때 사용할 안전한 BPM 값을 반환한다.
    public float SafeBpm
    {
        get { return Mathf.Max(1.0f, initialBpm); }
    }
}
