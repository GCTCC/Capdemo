using System;
using System.Collections.Generic;
using UnityEngine;

// 채보에 맞춰 노트를 생성하고 음악 시간에 따라 위치와 모양을 갱신한다.
[DisallowMultipleComponent]
public class NoteManager : MonoBehaviour
{
    // 화면에 존재하는 노트 하나의 런타임 상태다.
    public class RuntimeNote
    {
        public NoteData data;
        public GameObject gameObject;
        public Mesh mesh;
        public bool isJudged;
    }

    // 자동 MISS가 발생했을 때 판정·점수 시스템에 알려주는 이벤트다.
    public event Action<RuntimeNote> NoteMissed;

    // 현재 활성화된 노트들을 보관한다.
    private readonly List<RuntimeNote> activeNotes = new List<RuntimeNote>();

    private RhythmClock rhythmClock;
    private ChartData chartData;
    private Material leftNoteMaterial;
    private Material rightNoteMaterial;

    private float nearZ;
    private float farZ;
    private float judgeZ;
    private float nearHalfWidth;
    private float farHalfWidth;
    private float noteSpeed;
    private float minSpawnInterval;
    private float maxSpawnInterval;
    private float doubleNoteChance;
    private float approachTime;

    // 채보에서 다음에 생성할 노트의 인덱스다.
    private int nextChartNoteIndex;

    // 채보가 비어 있을 때 다음 랜덤 노트를 생성할 곡 시간이다.
    private double nextRandomSpawnTime;

    // 랜덤 노트에 부여할 임시 고유 번호다.
    private int nextRandomNoteId;

    // GOOD 허용 범위를 지난 노트를 자동 MISS로 처리할 시간이다.
    private const double MissWindow = 0.18;

    // 컨트롤러로부터 시간, 채보, 머티리얼과 플레이필드 설정을 전달받는다.
    public void Initialize(
        RhythmClock newRhythmClock,
        ChartData newChartData,
        Material newLeftNoteMaterial,
        Material newRightNoteMaterial,
        float newNearZ,
        float newFarZ,
        float newJudgeZ,
        float newNearHalfWidth,
        float newFarHalfWidth,
        float newNoteSpeed,
        float newMinSpawnInterval,
        float newMaxSpawnInterval,
        float newDoubleNoteChance)
    {
        rhythmClock = newRhythmClock;
        chartData = newChartData;
        leftNoteMaterial = newLeftNoteMaterial;
        rightNoteMaterial = newRightNoteMaterial;

        nearZ = newNearZ;
        farZ = newFarZ;
        judgeZ = newJudgeZ;
        nearHalfWidth = newNearHalfWidth;
        farHalfWidth = newFarHalfWidth;
        noteSpeed = Mathf.Max(0.01f, newNoteSpeed);
        minSpawnInterval = Mathf.Max(0.05f, newMinSpawnInterval);
        maxSpawnInterval = Mathf.Max(minSpawnInterval, newMaxSpawnInterval);
        doubleNoteChance = Mathf.Clamp01(newDoubleNoteChance);

        // 노트가 farZ에서 judgeZ까지 이동하는 데 필요한 시간을 계산한다.
        approachTime = Mathf.Max(0.1f, (farZ - judgeZ) / noteSpeed);

        nextChartNoteIndex = 0;
        nextRandomNoteId = 0;
        nextRandomSpawnTime = 0.6;
        ClearNotes();
    }

    // 매 프레임 컨트롤러가 호출해 생성, 이동과 MISS 검사를 진행한다.
    public void Tick()
    {
        if (rhythmClock == null || !rhythmClock.IsPlaying || rhythmClock.IsPaused)
        {
            return;
        }

        SpawnDueNotes();
        UpdateActiveNotes();
    }

    // 채보가 있으면 채보 노트를, 없으면 기존 프로토타입처럼 랜덤 노트를 만든다.
    private void SpawnDueNotes()
    {
        if (chartData != null && chartData.HasNotes)
        {
            SpawnChartNotes();
            return;
        }

        SpawnRandomNotes();
    }

    // 판정 시각에서 approachTime만큼 앞선 순간에 채보 노트를 화면에 만든다.
    private void SpawnChartNotes()
    {
        while (nextChartNoteIndex < chartData.notes.Count)
        {
            NoteData noteData = chartData.notes[nextChartNoteIndex];
            if (noteData.hitTime - approachTime > rhythmClock.SongTime)
            {
                break;
            }

            SpawnNote(noteData);
            nextChartNoteIndex++;
        }
    }

    // 실제 채보가 없을 때 빠르게 게임을 시험할 수 있도록 랜덤 노트를 만든다.
    private void SpawnRandomNotes()
    {
        if (rhythmClock.SongTime < nextRandomSpawnTime)
        {
            return;
        }

        double hitTime = rhythmClock.SongTime + approachTime;
        if (UnityEngine.Random.value < doubleNoteChance)
        {
            // 동시 노트는 왼손과 오른손에서 하나씩 선택한다.
            SpawnNote(CreateRandomNote(UnityEngine.Random.Range(0, 3), hitTime));
            SpawnNote(CreateRandomNote(UnityEngine.Random.Range(3, 6), hitTime));
        }
        else
        {
            SpawnNote(CreateRandomNote(UnityEngine.Random.Range(0, 6), hitTime));
        }

        nextRandomSpawnTime = rhythmClock.SongTime
            + UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    // 랜덤 프로토타입용 탭 노트 데이터를 만든다.
    private NoteData CreateRandomNote(int lane, double hitTime)
    {
        NoteData noteData = new NoteData();
        noteData.noteId = nextRandomNoteId++;
        noteData.lane = lane;
        noteData.hitTime = hitTime;
        noteData.noteType = NoteType.Tap;
        noteData.endTime = hitTime;
        return noteData;
    }

    // NoteData 하나를 실제 GameObject와 Mesh로 변환한다.
    private void SpawnNote(NoteData noteData)
    {
        // 잘못된 채보가 게임을 중단시키지 않도록 레인 범위를 검사한다.
        if (noteData.lane < 0 || noteData.lane >= 6)
        {
            Debug.LogWarning("Invalid note lane: " + noteData.lane);
            return;
        }

        GameObject noteObject = new GameObject("Note Lane " + (noteData.lane + 1));
        noteObject.transform.parent = transform;

        MeshFilter filter = noteObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = noteObject.AddComponent<MeshRenderer>();
        Mesh mesh = new Mesh();
        mesh.name = "Falling Note Mesh";

        filter.sharedMesh = mesh;
        renderer.sharedMaterial = noteData.lane < 3
            ? leftNoteMaterial
            : rightNoteMaterial;

        RuntimeNote runtimeNote = new RuntimeNote();
        runtimeNote.data = noteData;
        runtimeNote.gameObject = noteObject;
        runtimeNote.mesh = mesh;
        runtimeNote.isJudged = false;

        activeNotes.Add(runtimeNote);
        UpdateNoteMesh(runtimeNote);
    }

    // 모든 활성 노트의 위치를 음악 시간에서 다시 계산한다.
    private void UpdateActiveNotes()
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            RuntimeNote note = activeNotes[i];
            if (note.isJudged)
            {
                RemoveNoteAt(i);
                continue;
            }

            // 판정 허용 시간을 지나면 MISS 이벤트를 보내고 노트를 제거한다.
            if (rhythmClock.SongTime - note.data.hitTime > MissWindow)
            {
                if (NoteMissed != null)
                {
                    NoteMissed(note);
                }

                RemoveNoteAt(i);
                continue;
            }

            UpdateNoteMesh(note);
        }
    }

    // 음악 시간과 판정 시각 차이로 노트의 Z 좌표를 계산한다.
    private void UpdateNoteMesh(RuntimeNote note)
    {
        double timeUntilHit = note.data.hitTime - rhythmClock.SongTime;
        float z;

        if (timeUntilHit >= 0.0)
        {
            float remainingRatio = Mathf.Clamp01((float)(timeUntilHit / approachTime));
            z = Mathf.Lerp(judgeZ, farZ, remainingRatio);
        }
        else
        {
            // 판정선을 지난 노트도 MISS가 확정될 때까지 같은 속도로 앞쪽으로 이동한다.
            z = judgeZ - (float)(-timeUntilHit * noteSpeed);
        }

        float halfWidth = HalfWidthAtZ(z);
        float laneWidth = halfWidth * 2.0f / 6.0f;
        float centerX = -halfWidth + laneWidth * (note.data.lane + 0.5f);
        float noteHalfWidth = laneWidth * 0.39f;
        float halfDepth = Mathf.Lerp(0.10f, 0.24f, 1.0f - NormalizedZ(z));
        float y = 0.10f;

        note.mesh.Clear();
        note.mesh.vertices = new Vector3[]
        {
            new Vector3(centerX - noteHalfWidth, y, z - halfDepth),
            new Vector3(centerX + noteHalfWidth, y, z - halfDepth),
            new Vector3(centerX - noteHalfWidth, y, z + halfDepth),
            new Vector3(centerX + noteHalfWidth, y, z + halfDepth)
        };
        note.mesh.triangles = new int[] { 0, 2, 1, 1, 2, 3 };
        note.mesh.RecalculateBounds();
    }

    // 입력 레인에서 판정 시각과 가장 가까운 노트를 찾는다.
    public RuntimeNote FindClosestNote(int lane, double inputTime, out double timeDifference)
    {
        RuntimeNote closestNote = null;
        timeDifference = double.MaxValue;

        for (int i = 0; i < activeNotes.Count; i++)
        {
            RuntimeNote note = activeNotes[i];
            if (note.isJudged || note.data.lane != lane)
            {
                continue;
            }

            double difference = Math.Abs(note.data.hitTime - inputTime);
            if (difference < timeDifference)
            {
                timeDifference = difference;
                closestNote = note;
            }
        }

        return closestNote;
    }

    // 판정이 끝난 노트를 화면과 활성 목록에서 제거한다.
    public void ConsumeNote(RuntimeNote note)
    {
        if (note == null)
        {
            return;
        }

        note.isJudged = true;
        int index = activeNotes.IndexOf(note);
        if (index >= 0)
        {
            RemoveNoteAt(index);
        }
    }

    // 특정 인덱스의 Mesh와 GameObject를 안전하게 파괴한다.
    private void RemoveNoteAt(int index)
    {
        RuntimeNote note = activeNotes[index];
        activeNotes.RemoveAt(index);

        if (note.mesh != null)
        {
            Destroy(note.mesh);
        }

        if (note.gameObject != null)
        {
            Destroy(note.gameObject);
        }
    }

    // 현재 활성화된 모든 노트를 제거하고 목록을 초기화한다.
    public void ClearNotes()
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            RemoveNoteAt(i);
        }
    }

    private float HalfWidthAtZ(float z)
    {
        return Mathf.Lerp(nearHalfWidth, farHalfWidth, NormalizedZ(z));
    }

    private float NormalizedZ(float z)
    {
        return Mathf.Clamp01((z - nearZ) / (farZ - nearZ));
    }

    private void OnDestroy()
    {
        ClearNotes();
    }
}
