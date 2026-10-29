using System;
using System.Collections.Generic;
using UnityEngine;

// Resources/RhythmSongs의 음악과 동명 채보를 검색하고 ChartData로 변환한다.
// 기존 중립 JSON과 NoteEditor JSON을 모두 지원한다.
[DisallowMultipleComponent]
public class ChartAutoLoader : MonoBehaviour
{
    // 로비가 표시할 곡 하나의 검색 결과다.
    public class SongEntry
    {
        public AudioClip audioClip;
        public TextAsset chartFile;

        public string DisplayName
        {
            get { return audioClip != null ? audioClip.name : "Unknown Song"; }
        }

        public bool HasChart
        {
            get { return chartFile != null; }
        }
    }

    [Header("Automatic Chart Discovery")]
    [SerializeField] private string resourcesFolder = "RhythmSongs";
    [SerializeField] private string preferredSongName = "";
    [SerializeField] private float fallbackBpm = 120.0f;
    [SerializeField] private bool warnAboutHandPatterns = true;

    private ChartData runtimeChartData;

    public bool HasMatchedChart { get; private set; }
    public string LastLoadMessage { get; private set; }

    // 프로젝트의 기존 중립 JSON 형식이다.
    [Serializable]
    private class ExternalChartFile
    {
        public string format;
        public string songId;
        public string title;
        public float initialBpm;
        public float bpm;
        public double musicOffset;
        public ExternalBpmEvent[] bpmEvents;
        public ExternalNote[] notes;
    }

    [Serializable]
    private class ExternalBpmEvent
    {
        public double beat;
        public float bpm;
    }

    [Serializable]
    private class ExternalNote
    {
        public int noteId;
        public int lane;
        public double hitTime;
        public string type;
        public double endTime;
    }

    // GitHub NoteEditor가 출력하는 JSON 형식이다.
    [Serializable]
    private class NoteEditorChartFile
    {
        public string name;
        public int maxBlock;
        public float BPM;
        public double offset;
        public NoteEditorNote[] notes;
    }

    [Serializable]
    private class NoteEditorNote
    {
        public int LPB;
        public int num;
        public int block;
        public int type;
        public NoteEditorNote[] notes;
    }

    // Resources 폴더에서 선택 가능한 모든 음악을 이름순으로 반환한다.
    public List<SongEntry> GetAvailableSongs()
    {
        AudioClip[] audioClips = Resources.LoadAll<AudioClip>(resourcesFolder);
        TextAsset[] chartFiles = Resources.LoadAll<TextAsset>(resourcesFolder);
        Array.Sort(audioClips, CompareAudioClipNames);

        List<SongEntry> songs = new List<SongEntry>();
        for (int i = 0; i < audioClips.Length; i++)
        {
            SongEntry entry = new SongEntry();
            entry.audioClip = audioClips[i];
            entry.chartFile = FindTextAsset(chartFiles, audioClips[i].name);
            songs.Add(entry);
        }

        return songs;
    }

    // 이전 자동 시작 방식과의 호환용 메서드다.
    public bool TryLoadChart(out ChartData loadedChart)
    {
        List<SongEntry> songs = GetAvailableSongs();
        if (songs.Count == 0)
        {
            loadedChart = null;
            HasMatchedChart = false;
            LastLoadMessage = "No music was found.";
            return false;
        }

        SongEntry selectedSong = SelectPreferredSong(songs);
        return TryLoadSong(selectedSong, out loadedChart);
    }

    // 로비에서 선택한 음악과 동명 채보를 런타임 ChartData로 만든다.
    public bool TryLoadSong(SongEntry song, out ChartData loadedChart)
    {
        ReleaseRuntimeChart();
        loadedChart = null;
        HasMatchedChart = false;

        if (song == null || song.audioClip == null)
        {
            LastLoadMessage = "선택한 음악이 없습니다.";
            return false;
        }

        runtimeChartData = ScriptableObject.CreateInstance<ChartData>();
        runtimeChartData.songId = song.audioClip.name;
        runtimeChartData.title = song.audioClip.name;
        runtimeChartData.audioClip = song.audioClip;
        runtimeChartData.initialBpm = Mathf.Max(1.0f, fallbackBpm);
        runtimeChartData.musicOffset = 0.0;
        runtimeChartData.notes = new List<NoteData>();
        runtimeChartData.bpmEvents = new List<BpmEventData>();

        // 곡만 있으면 빈 채보를 반환하여 NoteManager가 랜덤 노트를 만든다.
        if (song.chartFile == null)
        {
            LastLoadMessage = song.DisplayName
                + " 음악은 찾았지만 동명 JSON이 없어 랜덤 채보를 사용합니다.";
            Debug.Log("ChartAutoLoader: " + LastLoadMessage);
            loadedChart = runtimeChartData;
            return true;
        }

        if (!LooksLikeJson(song.chartFile.text)
            || !TryApplyJson(song.chartFile.text, runtimeChartData))
        {
            LastLoadMessage = song.chartFile.name
                + " 채보를 읽지 못해 음악과 랜덤 채보를 사용합니다.";
            Debug.LogWarning("ChartAutoLoader: " + LastLoadMessage);
            loadedChart = runtimeChartData;
            return true;
        }

        HasMatchedChart = runtimeChartData.HasNotes;
        LastLoadMessage = HasMatchedChart
            ? song.DisplayName + " 음악과 동명 채보를 불러왔습니다."
            : song.DisplayName + " 채보가 비어 있어 랜덤 채보를 사용합니다.";
        Debug.Log("ChartAutoLoader: " + LastLoadMessage);

        loadedChart = runtimeChartData;
        return true;
    }

    private SongEntry SelectPreferredSong(List<SongEntry> songs)
    {
        if (!string.IsNullOrWhiteSpace(preferredSongName))
        {
            string preferred = preferredSongName.Trim();
            for (int i = 0; i < songs.Count; i++)
            {
                if (string.Equals(
                    songs[i].DisplayName,
                    preferred,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return songs[i];
                }
            }
        }

        for (int i = 0; i < songs.Count; i++)
        {
            if (songs[i].HasChart)
            {
                return songs[i];
            }
        }

        return songs[0];
    }

    private bool TryApplyJson(string json, ChartData target)
    {
        if (LooksLikeNoteEditorJson(json))
        {
            return TryApplyNoteEditorJson(json, target);
        }

        return TryApplyNeutralJson(json, target);
    }

    private bool TryApplyNoteEditorJson(string json, ChartData target)
    {
        NoteEditorChartFile source;
        try
        {
            source = JsonUtility.FromJson<NoteEditorChartFile>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("ChartAutoLoader: NoteEditor JSON 파싱 오류 - "
                + exception.Message);
            return false;
        }

        if (source == null || source.notes == null || source.BPM <= 0.0f)
        {
            return false;
        }

        if (source.maxBlock != RhythmGameController.LaneCount)
        {
            Debug.LogWarning("ChartAutoLoader: NoteEditor 채보가 6라인이 아닙니다. 현재 값: "
                + source.maxBlock);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(source.name))
        {
            target.songId = source.name;
            target.title = source.name;
        }

        target.initialBpm = source.BPM;

        // NoteEditor의 offset은 밀리초가 아니라 원본 음원의 샘플 개수다.
        // 실제 초 단위 값은 offset / sampleRate로 계산해야 한다.
        int sampleRate = target.audioClip != null
            ? target.audioClip.frequency
            : 44100;
        target.musicOffset = source.offset / sampleRate;
        target.bpmEvents.Clear();

        BpmEventData initialEvent = new BpmEventData();
        initialEvent.beat = 0.0;
        initialEvent.bpm = source.BPM;
        target.bpmEvents.Add(initialEvent);

        target.notes.Clear();
        for (int i = 0; i < source.notes.Length; i++)
        {
            NoteEditorNote sourceNote = source.notes[i];
            if (sourceNote == null
                || sourceNote.block < 0
                || sourceNote.block >= RhythmGameController.LaneCount
                || sourceNote.LPB <= 0)
            {
                continue;
            }

            double hitTime = ConvertNoteEditorTime(
                sourceNote.num,
                sourceNote.LPB,
                source.BPM,
                source.offset,
                sampleRate);

            NoteData note = new NoteData();
            note.noteId = i;
            note.lane = sourceNote.block;
            note.hitTime = hitTime;

            if (sourceNote.type == 2)
            {
                note.noteType = NoteType.Hold;
                note.endTime = hitTime;

                if (sourceNote.notes != null
                    && sourceNote.notes.Length > 0
                    && sourceNote.notes[0] != null
                    && sourceNote.notes[0].LPB > 0)
                {
                    NoteEditorNote endNote = sourceNote.notes[0];
                    note.endTime = ConvertNoteEditorTime(
                        endNote.num,
                        endNote.LPB,
                        source.BPM,
                        source.offset,
                        sampleRate);
                }

                // 끝점이 없는 비정상 롱노트는 탭 노트로 안전하게 처리한다.
                if (note.endTime <= note.hitTime)
                {
                    note.noteType = NoteType.Tap;
                    note.endTime = note.hitTime;
                }
            }
            else if (sourceNote.type == 1)
            {
                note.noteType = NoteType.Tap;
                note.endTime = hitTime;
            }
            else
            {
                continue;
            }

            target.notes.Add(note);
        }

        target.notes.Sort(CompareNoteTimes);
        WarnIfRequested(target.notes);
        Debug.Log("ChartAutoLoader: NoteEditor 채보에서 "
            + target.notes.Count + "개의 노트를 변환했습니다. offset="
            + source.offset + " samples ("
            + target.musicOffset.ToString("0.0000") + "초, "
            + sampleRate + "Hz)");
        return target.notes.Count > 0;
    }

    private bool TryApplyNeutralJson(string json, ChartData target)
    {
        ExternalChartFile source;
        try
        {
            source = JsonUtility.FromJson<ExternalChartFile>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("ChartAutoLoader: JSON 파싱 오류 - " + exception.Message);
            return false;
        }

        if (source == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(source.songId))
        {
            target.songId = source.songId;
        }

        if (!string.IsNullOrWhiteSpace(source.title))
        {
            target.title = source.title;
        }

        float jsonBpm = source.initialBpm > 0.0f ? source.initialBpm : source.bpm;
        target.initialBpm = jsonBpm > 0.0f
            ? jsonBpm
            : Mathf.Max(1.0f, fallbackBpm);
        target.musicOffset = source.musicOffset;

        target.bpmEvents.Clear();
        if (source.bpmEvents != null)
        {
            for (int i = 0; i < source.bpmEvents.Length; i++)
            {
                if (source.bpmEvents[i].bpm <= 0.0f)
                {
                    continue;
                }

                BpmEventData bpmEvent = new BpmEventData();
                bpmEvent.beat = source.bpmEvents[i].beat;
                bpmEvent.bpm = source.bpmEvents[i].bpm;
                target.bpmEvents.Add(bpmEvent);
            }
        }

        target.notes.Clear();
        HashSet<int> usedNoteIds = new HashSet<int>();
        if (source.notes != null)
        {
            for (int i = 0; i < source.notes.Length; i++)
            {
                ExternalNote externalNote = source.notes[i];
                if (externalNote.lane < 0
                    || externalNote.lane >= RhythmGameController.LaneCount
                    || externalNote.hitTime < 0.0)
                {
                    continue;
                }

                int noteId = externalNote.noteId;
                if (usedNoteIds.Contains(noteId))
                {
                    noteId = i;
                    while (usedNoteIds.Contains(noteId))
                    {
                        noteId++;
                    }
                }
                usedNoteIds.Add(noteId);

                NoteType noteType = ParseNoteType(externalNote.type);
                NoteData note = new NoteData();
                note.noteId = noteId;
                note.lane = externalNote.lane;
                note.hitTime = externalNote.hitTime;
                note.noteType = noteType;
                note.endTime = noteType == NoteType.Hold
                    ? Math.Max(externalNote.hitTime, externalNote.endTime)
                    : externalNote.hitTime;
                target.notes.Add(note);
            }
        }

        target.notes.Sort(CompareNoteTimes);
        WarnIfRequested(target.notes);
        return true;
    }

    private void WarnIfRequested(List<NoteData> notes)
    {
        if (warnAboutHandPatterns)
        {
            WarnForUnsupportedChords(notes);
        }
    }

    private static double ConvertNoteEditorTime(
        int num,
        int lpb,
        float bpm,
        double offsetSamples,
        int sampleRate)
    {
        double secondsPerBeat = 60.0 / bpm;
        double beatPosition = num / (double)lpb;
        double offsetSeconds = sampleRate > 0
            ? offsetSamples / sampleRate
            : 0.0;
        return offsetSeconds + beatPosition * secondsPerBeat;
    }

    private static bool LooksLikeNoteEditorJson(string json)
    {
        return json.Contains("\"maxBlock\"")
            && json.Contains("\"BPM\"")
            && json.Contains("\"LPB\"");
    }

    private static bool LooksLikeJson(string text)
    {
        return !string.IsNullOrWhiteSpace(text)
            && text.TrimStart().StartsWith("{", StringComparison.Ordinal);
    }

    private static int CompareAudioClipNames(AudioClip left, AudioClip right)
    {
        return string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
    }

    private static TextAsset FindTextAsset(TextAsset[] chartFiles, string songName)
    {
        if (chartFiles == null)
        {
            return null;
        }

        for (int i = 0; i < chartFiles.Length; i++)
        {
            if (string.Equals(
                chartFiles[i].name,
                songName,
                StringComparison.OrdinalIgnoreCase))
            {
                return chartFiles[i];
            }
        }

        return null;
    }

    private static NoteType ParseNoteType(string type)
    {
        NoteType parsedType;
        return !string.IsNullOrWhiteSpace(type)
            && Enum.TryParse(type, true, out parsedType)
            ? parsedType
            : NoteType.Tap;
    }

    private static int CompareNoteTimes(NoteData left, NoteData right)
    {
        int timeComparison = left.hitTime.CompareTo(right.hitTime);
        return timeComparison != 0
            ? timeComparison
            : left.lane.CompareTo(right.lane);
    }

    private static void WarnForUnsupportedChords(List<NoteData> notes)
    {
        const double simultaneousTolerance = 0.001;
        int start = 0;

        while (start < notes.Count)
        {
            int end = start + 1;
            while (end < notes.Count
                && Math.Abs(notes[end].hitTime - notes[start].hitTime)
                    <= simultaneousTolerance)
            {
                end++;
            }

            int count = end - start;
            if (count > 2)
            {
                Debug.LogWarning("ChartAutoLoader: "
                    + notes[start].hitTime.ToString("0.000")
                    + "초에 " + count + "개 노트가 동시에 있습니다.");
            }
            else if (count == 2)
            {
                bool firstIsLeft = notes[start].lane < 3;
                bool secondIsLeft = notes[start + 1].lane < 3;
                if (firstIsLeft == secondIsLeft)
                {
                    Debug.LogWarning("ChartAutoLoader: "
                        + notes[start].hitTime.ToString("0.000")
                        + "초의 동시 노트가 한 손 영역에 몰려 있습니다.");
                }
            }

            start = end;
        }
    }

    private void ReleaseRuntimeChart()
    {
        if (runtimeChartData != null)
        {
            Destroy(runtimeChartData);
            runtimeChartData = null;
        }
    }

    private void OnDestroy()
    {
        ReleaseRuntimeChart();
    }
}
