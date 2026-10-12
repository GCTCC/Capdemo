using System;
using System.Collections.Generic;
using UnityEngine;

// Resources/RhythmSongs에서 음악과 동명의 중립 JSON 채보를 자동으로 찾는다.
// 특정 외부 에디터 형식에 종속되지 않으며, 향후 .osu 등의 변환기가 이 JSON을 만들면
// 리듬게임 런타임 코드를 수정하지 않고 그대로 사용할 수 있다.
[DisallowMultipleComponent]
public class ChartAutoLoader : MonoBehaviour
{
    [Header("Automatic Chart Discovery")]
    // Assets/Resources 아래에서 음악과 채보를 검색할 상대 경로다.
    [SerializeField]
    private string resourcesFolder = "RhythmSongs";

    // 여러 곡이 있을 때 우선 선택할 파일명이다. 확장자는 입력하지 않는다.
    // 비워 두면 동명 채보가 있는 첫 음악을 우선하고, 없으면 첫 음악을 선택한다.
    [SerializeField]
    private string preferredSongName = "";

    // JSON에 BPM이 없을 때 사용할 기본값이다.
    [SerializeField]
    private float fallbackBpm = 120.0f;

    // 호환되지 않는 동시 입력 형태를 발견했을 때 Console에 경고할지 결정한다.
    [SerializeField]
    private bool warnAboutHandPatterns = true;

    // 자동으로 생성한 ChartData는 프로젝트 에셋이 아니므로 이 컴포넌트가 소유한다.
    private ChartData runtimeChartData;

    // 동명의 JSON 채보를 실제로 읽었는지 외부에서 확인할 수 있다.
    public bool HasMatchedChart { get; private set; }

    // 마지막 자동 탐색 결과를 디버깅할 때 사용할 설명이다.
    public string LastLoadMessage { get; private set; }

    // 중립 JSON 파일의 최상위 구조다.
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

    // JSON에서 읽은 BPM 변경 데이터다.
    [Serializable]
    private class ExternalBpmEvent
    {
        public double beat;
        public float bpm;
    }

    // JSON에서 읽은 노트 하나의 데이터다.
    [Serializable]
    private class ExternalNote
    {
        public int noteId;
        public int lane;
        public double hitTime;
        public string type;
        public double endTime;
    }

    // 음악을 하나 찾으면 ChartData를 만든다.
    // 동명의 JSON이 없으면 노트 목록이 빈 ChartData를 반환하므로 NoteManager가 랜덤 채보를 사용한다.
    public bool TryLoadChart(out ChartData loadedChart)
    {
        // 재시작이나 테스트 도중 다시 호출되면 이전 런타임 데이터부터 정리한다.
        if (runtimeChartData != null)
        {
            Destroy(runtimeChartData);
            runtimeChartData = null;
        }

        loadedChart = null;
        HasMatchedChart = false;
        LastLoadMessage = "No music was found.";

        AudioClip[] audioClips = Resources.LoadAll<AudioClip>(resourcesFolder);
        TextAsset[] chartFiles = Resources.LoadAll<TextAsset>(resourcesFolder);

        if (audioClips == null || audioClips.Length == 0)
        {
            Debug.Log("ChartAutoLoader: Resources/" + resourcesFolder
                + "에 음악이 없어 기존 무음 랜덤 채보를 사용합니다.");
            return false;
        }

        Array.Sort(audioClips, CompareAudioClipNames);
        AudioClip selectedAudio = SelectAudioClip(audioClips, chartFiles);
        if (selectedAudio == null)
        {
            return false;
        }

        // 음악만 있어도 랜덤 채보와 함께 재생할 수 있도록 기본 ChartData를 만든다.
        runtimeChartData = ScriptableObject.CreateInstance<ChartData>();
        runtimeChartData.songId = selectedAudio.name;
        runtimeChartData.title = selectedAudio.name;
        runtimeChartData.audioClip = selectedAudio;
        runtimeChartData.initialBpm = Mathf.Max(1.0f, fallbackBpm);
        runtimeChartData.musicOffset = 0.0;
        runtimeChartData.notes = new List<NoteData>();
        runtimeChartData.bpmEvents = new List<BpmEventData>();

        TextAsset matchingChart = FindTextAsset(chartFiles, selectedAudio.name);
        if (matchingChart == null)
        {
            LastLoadMessage = selectedAudio.name
                + " 음악은 찾았지만 동명의 JSON 채보가 없어 랜덤 채보를 사용합니다.";
            Debug.Log("ChartAutoLoader: " + LastLoadMessage);
            loadedChart = runtimeChartData;
            return true;
        }

        if (!LooksLikeJson(matchingChart.text))
        {
            // .osu처럼 다른 형식의 파일이 같은 이름으로 들어온 경우 자동으로 잘못 해석하지 않는다.
            LastLoadMessage = matchingChart.name
                + " 파일은 중립 JSON이 아닙니다. 해당 형식용 변환 어댑터가 필요하므로 랜덤 채보를 사용합니다.";
            Debug.LogWarning("ChartAutoLoader: " + LastLoadMessage);
            loadedChart = runtimeChartData;
            return true;
        }

        if (!TryApplyJson(matchingChart.text, runtimeChartData))
        {
            LastLoadMessage = matchingChart.name
                + " JSON을 읽지 못해 랜덤 채보를 사용합니다.";
            Debug.LogWarning("ChartAutoLoader: " + LastLoadMessage);
            loadedChart = runtimeChartData;
            return true;
        }

        HasMatchedChart = runtimeChartData.HasNotes;
        LastLoadMessage = HasMatchedChart
            ? selectedAudio.name + " 음악과 동명 채보를 불러왔습니다."
            : selectedAudio.name + " 채보에 유효한 노트가 없어 랜덤 채보를 사용합니다.";
        Debug.Log("ChartAutoLoader: " + LastLoadMessage);

        loadedChart = runtimeChartData;
        return true;
    }

    // 사용자가 지정한 곡을 우선하고, 미지정이면 채보가 있는 음악을 먼저 선택한다.
    private AudioClip SelectAudioClip(AudioClip[] audioClips, TextAsset[] chartFiles)
    {
        if (!string.IsNullOrWhiteSpace(preferredSongName))
        {
            for (int i = 0; i < audioClips.Length; i++)
            {
                if (string.Equals(
                    audioClips[i].name,
                    preferredSongName.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return audioClips[i];
                }
            }

            Debug.LogWarning("ChartAutoLoader: Preferred Song Name '"
                + preferredSongName + "'과 일치하는 음악을 찾지 못했습니다.");
        }

        for (int i = 0; i < audioClips.Length; i++)
        {
            if (FindTextAsset(chartFiles, audioClips[i].name) != null)
            {
                return audioClips[i];
            }
        }

        return audioClips[0];
    }

    private static int CompareAudioClipNames(AudioClip left, AudioClip right)
    {
        return string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
    }

    // 확장자를 제외한 Unity 에셋 이름이 음악 이름과 같은 TextAsset을 찾는다.
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

    private static bool LooksLikeJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.TrimStart().StartsWith("{", StringComparison.Ordinal);
    }

    // 중립 JSON을 현재 게임이 사용하는 ChartData와 NoteData로 변환한다.
    private bool TryApplyJson(string json, ChartData target)
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
                if (externalNote.lane < 0 || externalNote.lane >= 6)
                {
                    Debug.LogWarning("ChartAutoLoader: " + i
                        + "번 노트의 lane이 0~5 범위를 벗어나 제외했습니다.");
                    continue;
                }

                if (externalNote.hitTime < 0.0)
                {
                    Debug.LogWarning("ChartAutoLoader: " + i
                        + "번 노트의 hitTime이 음수라 제외했습니다.");
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

        // NoteManager가 앞에서부터 생성하므로 판정 시각 기준 오름차순으로 정렬한다.
        target.notes.Sort(CompareNoteTimes);

        if (warnAboutHandPatterns)
        {
            WarnForUnsupportedChords(target.notes);
        }

        return true;
    }

    private static NoteType ParseNoteType(string type)
    {
        NoteType parsedType;
        if (!string.IsNullOrWhiteSpace(type)
            && Enum.TryParse(type, true, out parsedType))
        {
            return parsedType;
        }

        return NoteType.Tap;
    }

    private static int CompareNoteTimes(NoteData left, NoteData right)
    {
        int timeComparison = left.hitTime.CompareTo(right.hitTime);
        return timeComparison != 0
            ? timeComparison
            : left.lane.CompareTo(right.lane);
    }

    // 현재 프로토타입 규칙인 최대 2개, 왼손·오른손 하나씩 구성에서 벗어나면 경고한다.
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
                    + "초에 " + count
                    + "개 노트가 동시에 있습니다. 현재 권장 규칙은 최대 2개입니다.");
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

    private void OnDestroy()
    {
        if (runtimeChartData != null)
        {
            Destroy(runtimeChartData);
        }
    }
}
