using System;
using System.Collections.Generic;
using UnityEngine;

// 채보에 맞춰 탭·롱노트를 생성하고 음악 시간에 따라 화면 위치를 갱신한다.
[DisallowMultipleComponent]
public class NoteManager : MonoBehaviour
{
    public class RuntimeNote
    {
        public NoteData data;
        public GameObject gameObject;
        public Mesh mesh;
        public bool isJudged;
        public bool isHolding;
    }

    public event Action<RuntimeNote> NoteMissed;

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
    private int nextChartNoteIndex;
    private double nextRandomSpawnTime;
    private int nextRandomNoteId;

    private const double MissWindow = 0.18;

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
        ClearNotes();

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
        approachTime = Mathf.Max(0.1f, (farZ - judgeZ) / noteSpeed);

        nextChartNoteIndex = 0;
        nextRandomNoteId = 0;
        nextRandomSpawnTime = 0.6;
    }

    public void Tick()
    {
        if (rhythmClock == null
            || !rhythmClock.IsPlaying
            || !rhythmClock.HasStarted
            || rhythmClock.IsPaused)
        {
            return;
        }

        SpawnDueNotes();
        UpdateActiveNotes();
    }

    private void SpawnDueNotes()
    {
        if (chartData != null && chartData.HasNotes)
        {
            SpawnChartNotes();
        }
        else
        {
            SpawnRandomNotes();
        }
    }

    private void SpawnChartNotes()
    {
        while (nextChartNoteIndex < chartData.notes.Count)
        {
            NoteData noteData = chartData.notes[nextChartNoteIndex];
            if (noteData.hitTime - approachTime > rhythmClock.ChartTime)
            {
                break;
            }

            SpawnNote(noteData);
            nextChartNoteIndex++;
        }
    }

    private void SpawnRandomNotes()
    {
        if (rhythmClock.ChartTime < nextRandomSpawnTime)
        {
            return;
        }

        double hitTime = rhythmClock.ChartTime + approachTime;
        if (UnityEngine.Random.value < doubleNoteChance)
        {
            SpawnNote(CreateRandomNote(UnityEngine.Random.Range(0, 3), hitTime));
            SpawnNote(CreateRandomNote(UnityEngine.Random.Range(3, 6), hitTime));
        }
        else
        {
            SpawnNote(CreateRandomNote(UnityEngine.Random.Range(0, 6), hitTime));
        }

        nextRandomSpawnTime = rhythmClock.ChartTime
            + UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval);
    }

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

    private void SpawnNote(NoteData noteData)
    {
        if (noteData.lane < 0 || noteData.lane >= RhythmGameController.LaneCount)
        {
            return;
        }

        GameObject noteObject = new GameObject(
            noteData.noteType == NoteType.Hold
                ? "Hold Note Lane " + (noteData.lane + 1)
                : "Tap Note Lane " + (noteData.lane + 1));
        noteObject.transform.SetParent(transform, false);

        MeshFilter filter = noteObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = noteObject.AddComponent<MeshRenderer>();
        Mesh mesh = new Mesh();
        mesh.name = noteData.noteType == NoteType.Hold
            ? "Hold Note Mesh"
            : "Tap Note Mesh";
        filter.sharedMesh = mesh;
        renderer.sharedMaterial = noteData.lane < 3
            ? leftNoteMaterial
            : rightNoteMaterial;

        RuntimeNote runtimeNote = new RuntimeNote();
        runtimeNote.data = noteData;
        runtimeNote.gameObject = noteObject;
        runtimeNote.mesh = mesh;
        runtimeNote.isJudged = false;
        runtimeNote.isHolding = false;
        activeNotes.Add(runtimeNote);
        UpdateNoteMesh(runtimeNote);
    }

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

            double missReferenceTime = note.isHolding
                ? note.data.endTime
                : note.data.hitTime;

            if (rhythmClock.ChartTime - missReferenceTime > MissWindow)
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

    private void UpdateNoteMesh(RuntimeNote note)
    {
        if (note.data.noteType == NoteType.Hold)
        {
            UpdateHoldMesh(note);
        }
        else
        {
            UpdateTapMesh(note);
        }
    }

    private void UpdateTapMesh(RuntimeNote note)
    {
        float z = ZAtTime(note.data.hitTime);
        float halfWidth = HalfWidthAtZ(z);
        float laneWidth = halfWidth * 2.0f / RhythmGameController.LaneCount;
        float centerX = -halfWidth + laneWidth * (note.data.lane + 0.5f);
        float noteHalfWidth = laneWidth * 0.39f;
        float halfDepth = Mathf.Lerp(0.10f, 0.24f, 1.0f - NormalizedZ(z));
        float y = 0.10f;

        SetQuad(
            note.mesh,
            new Vector3(centerX - noteHalfWidth, y, z - halfDepth),
            new Vector3(centerX + noteHalfWidth, y, z - halfDepth),
            new Vector3(centerX - noteHalfWidth, y, z + halfDepth),
            new Vector3(centerX + noteHalfWidth, y, z + halfDepth));
    }

    private void UpdateHoldMesh(RuntimeNote note)
    {
        float headZ = note.isHolding ? judgeZ : ZAtTime(note.data.hitTime);
        float tailZ = Mathf.Min(farZ, ZAtTime(note.data.endTime));
        tailZ = Mathf.Max(headZ + 0.12f, tailZ);

        float headHalfWidth = HalfWidthAtZ(headZ);
        float tailHalfWidth = HalfWidthAtZ(tailZ);
        float headLaneWidth = headHalfWidth * 2.0f / RhythmGameController.LaneCount;
        float tailLaneWidth = tailHalfWidth * 2.0f / RhythmGameController.LaneCount;
        float headCenterX = -headHalfWidth
            + headLaneWidth * (note.data.lane + 0.5f);
        float tailCenterX = -tailHalfWidth
            + tailLaneWidth * (note.data.lane + 0.5f);
        float headNoteHalfWidth = headLaneWidth * 0.36f;
        float tailNoteHalfWidth = tailLaneWidth * 0.36f;
        float y = note.isHolding ? 0.14f : 0.11f;

        SetQuad(
            note.mesh,
            new Vector3(headCenterX - headNoteHalfWidth, y, headZ - 0.10f),
            new Vector3(headCenterX + headNoteHalfWidth, y, headZ - 0.10f),
            new Vector3(tailCenterX - tailNoteHalfWidth, y, tailZ + 0.10f),
            new Vector3(tailCenterX + tailNoteHalfWidth, y, tailZ + 0.10f));
    }

    private static void SetQuad(
        Mesh mesh,
        Vector3 nearLeft,
        Vector3 nearRight,
        Vector3 farLeft,
        Vector3 farRight)
    {
        mesh.Clear();
        mesh.vertices = new Vector3[] { nearLeft, nearRight, farLeft, farRight };
        mesh.triangles = new int[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateBounds();
    }

    private float ZAtTime(double targetTime)
    {
        return judgeZ + (float)((targetTime - rhythmClock.ChartTime) * noteSpeed);
    }

    // 같은 레인에서 아직 판정되지 않은 가장 앞 노트를 찾는다.
    // 입력 하나가 뒤 노트로 건너뛰지 않도록 항상 hitTime이 가장 이른 노트만 반환한다.
    public RuntimeNote FindEarliestPendingNote(int lane)
    {
        RuntimeNote earliestNote = null;

        for (int i = 0; i < activeNotes.Count; i++)
        {
            RuntimeNote note = activeNotes[i];
            if (note.isJudged || note.isHolding || note.data.lane != lane)
            {
                continue;
            }

            if (earliestNote == null
                || note.data.hitTime < earliestNote.data.hitTime
                || (Math.Abs(note.data.hitTime - earliestNote.data.hitTime) < 0.000001
                    && note.data.noteId < earliestNote.data.noteId))
            {
                earliestNote = note;
            }
        }

        return earliestNote;
    }

    public RuntimeNote FindHoldingNote(int lane)
    {
        for (int i = 0; i < activeNotes.Count; i++)
        {
            RuntimeNote note = activeNotes[i];
            if (!note.isJudged && note.isHolding && note.data.lane == lane)
            {
                return note;
            }
        }

        return null;
    }

    public void BeginHold(RuntimeNote note)
    {
        if (note != null && note.data.noteType == NoteType.Hold)
        {
            note.isHolding = true;
            UpdateNoteMesh(note);
        }
    }

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

    public void ClearNotes()
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            RemoveNoteAt(i);
        }

        nextChartNoteIndex = 0;
        nextRandomSpawnTime = 0.6;
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
