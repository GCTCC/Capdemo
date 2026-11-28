using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

// Resources/RhythmSongs의 음악과 동명 채보를 검색하고 ChartData로 변환한다.
// 중립 JSON, NoteEditor JSON, osu!mania 6K 텍스트/OSZ 바이트를 지원한다.
// .osu/.osz는 OsuManiaChartImporter가 자동 등록한다. 수동 변환 메뉴는 필요 없다.
[DisallowMultipleComponent]
public class ChartAutoLoader : MonoBehaviour
{
    // 로비가 표시할 곡 하나의 검색 결과다.
    public class SongEntry
    {
        public AudioClip audioClip;
        public TextAsset chartFile;
        public Texture2D albumCover;

        public string DisplayName
        {
            get { return audioClip != null ? audioClip.name : "Unknown Song"; }
        }

        public string ChartFileName
        {
            get
            {
                if (chartFile == null) return "(random)";
                OsuSourceFile source;
                if (TryUnwrapOsuSource(chartFile.text, out source)) return source.sourceName + source.sourceExtension;
                return chartFile.name + ".json";
            }
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

    // Unity가 main asset 이름을 파일명으로 바꾸어도 원본 이름/형식이 유지되는 내부 포맷.
    [Serializable]
    private class OsuSourceFile
    {
        public string format;
        public string sourceName;
        public string sourceExtension;
        public string osuText;
    }

    public static string CreateOsuSourceAssetText(string osuText, string sourceName, string sourceExtension)
    {
        return JsonUtility.ToJson(new OsuSourceFile {
            format = "capdemo-osu-source-v1", sourceName = sourceName,
            sourceExtension = sourceExtension.ToLowerInvariant(), osuText = osuText
        });
    }

    private static bool TryUnwrapOsuSource(string text, out OsuSourceFile source)
    {
        source = null;
        if (string.IsNullOrEmpty(text) || !text.Contains("capdemo-osu-source-v1")) return false;
        try
        {
            source = JsonUtility.FromJson<OsuSourceFile>(text);
            return source != null && source.format == "capdemo-osu-source-v1" && !string.IsNullOrEmpty(source.osuText);
        }
        catch (ArgumentException) { source = null; return false; }
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
        LoadSongOrder();
        var songs = new List<SongEntry>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string folder in GetResourceFolders())
        {
            AudioClip[] audioClips = Resources.LoadAll<AudioClip>(folder);
            TextAsset[] chartFiles = Resources.LoadAll<TextAsset>(folder);
            Texture2D[] coverTextures = Resources.LoadAll<Texture2D>(folder);
            Sprite[] coverSprites = Resources.LoadAll<Sprite>(folder);
            Array.Sort(audioClips, (left, right) => string.Compare(left.name, right.name, StringComparison.Ordinal));
            foreach (AudioClip clip in audioClips)
            {
                if (!names.Add(clip.name))
                {
                    Debug.LogWarning("ChartAutoLoader: 같은 이름의 음악이 여러 폴더에 있습니다. 설정 폴더를 우선합니다: " + clip.name);
                    continue;
                }
                var entry = new SongEntry();
                entry.audioClip = clip;
                entry.chartFile = FindTextAsset(chartFiles, clip.name);
                entry.albumCover = FindTexture(coverTextures, clip.name);
                if (entry.albumCover == null) entry.albumCover = FindSpriteTexture(coverSprites, clip.name);
                songs.Add(entry);
            }
        }
        songs.Sort((left, right) => CompareSongOrder(left.DisplayName, right.DisplayName));
        return songs;
    }

    // 기본 폴더의 단수/복수 표기를 모두 지원한다. 각 곡은 음악과 같은 폴더에서 채보를 찾는다.
    private IEnumerable<string> GetResourceFolders()
    {
        string folder = string.IsNullOrWhiteSpace(resourcesFolder) ? "RhythmSongs" : resourcesFolder.Trim().Trim('/');
        yield return folder;
        if (string.Equals(folder, "RhythmSongs", StringComparison.OrdinalIgnoreCase)) yield return "RhythmSong";
        else if (string.Equals(folder, "RhythmSong", StringComparison.OrdinalIgnoreCase)) yield return "RhythmSongs";
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
                + " 음악은 찾았지만 동명 채보가 없어 랜덤 채보를 사용합니다.";
            Debug.Log("ChartAutoLoader: " + LastLoadMessage);
            loadedChart = runtimeChartData;
            return true;
        }

        if (!TryApplyChartAsset(song.chartFile, runtimeChartData))
        {
            LastLoadMessage = song.chartFile.name
                + " 채보를 읽지 못해 음악과 랜덤 채보를 사용합니다.";
            Debug.LogWarning("ChartAutoLoader: " + LastLoadMessage);
            loadedChart = runtimeChartData;
            return true;
        }

        HasMatchedChart = runtimeChartData.HasNotes;
        LastLoadMessage = HasMatchedChart
            ? song.DisplayName + " 음악과 " + song.ChartFileName + " 채보를 불러왔습니다. ("
                + runtimeChartData.notes.Count + " notes)"
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

    // 자동 임포터가 등록한 .osu 및 OSZ 내부 선택 채보를 그대로 읽는다.
    // OSZ의 음악은 사용하지 않는다. target.audioClip은 동명의 외부 OGG다.
    private bool TryApplyChartAsset(TextAsset asset, ChartData target)
    {
        string error;
        byte[] bytes = asset.bytes;
        bool ok;
        if (bytes.Length >= 4 && bytes[0] == 'P' && bytes[1] == 'K'
            && bytes[2] == 3 && bytes[3] == 4)
            ok = TryApplyOsz(bytes, target, out error);
        else
        {
            string text = asset.text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            OsuSourceFile source;
            if (TryUnwrapOsuSource(text, out source)) text = source.osuText.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            if (text.StartsWith("osu file format v", StringComparison.Ordinal))
                ok = TryApplyOsu(text, target, out error);
            else
                return LooksLikeJson(text) && TryApplyJson(text, target);
        }
        if (!ok) Debug.LogWarning("ChartAutoLoader: " + error);
        else WarnIfRequested(target.notes);
        return ok;
    }

    // 성공 시 target에만 반영한다. audioClip과 songId는 호출자가 지정한 값을 유지한다.
    // osu의 hitTime/endTime은 음원 시작 기준 ms이므로 BPM/offset을 다시 더하지 않는다.
    public static bool TryApplyOsu(string text, ChartData target, out string error)
    {
        error = null;
        if (target == null) { error = "ChartData가 없습니다."; return false; }
        try
        {
            if (string.IsNullOrWhiteSpace(text)
                || !text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n')
                    .StartsWith("osu file format v", StringComparison.Ordinal))
                throw new FormatException("osu 파일 헤더가 없습니다.");

            string mode = ReadOsuProperty(text, "General", "Mode");
            if (mode != "3") throw new FormatException("osu!mania(Mode:3)만 지원합니다.");
            double keys = OsuNumber(ReadOsuProperty(text, "Difficulty", "CircleSize"));
            if (keys != RhythmGameController.LaneCount)
                throw new FormatException("현재 게임은 6K 채보만 지원합니다. CircleSize=" + keys);

            var notes = new List<NoteData>();
            var timings = new List<OsuTiming>();
            string section = "";
            using (var reader = new StringReader(text))
            {
                string line;
                int lineNumber = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (line.StartsWith("[", StringComparison.Ordinal)) { section = line; continue; }
                    try
                    {
                        if (section == "[TimingPoints]")
                        {
                            string[] f = line.Split(',');
                            if (f.Length < 2) throw new FormatException("타이밍 필드가 부족합니다.");
                            double time = OsuNumber(f[0]) / 1000.0;
                            double beatLength = OsuNumber(f[1]);
                            if (f.Length > 6 && f[6].Trim() == "0") continue; // SV는 BPM이 아니다.
                            if (beatLength <= 0) throw new FormatException("BPM 타이밍의 박자 길이는 양수여야 합니다.");
                            timings.Add(new OsuTiming { time = time, beatLength = beatLength, order = timings.Count });
                        }
                        else if (section == "[HitObjects]")
                        {
                            string[] f = line.Split(',');
                            if (f.Length < 5) throw new FormatException("노트 필드가 부족합니다.");
                            int x = int.Parse(f[0], CultureInfo.InvariantCulture);
                            double hit = OsuNumber(f[2]) / 1000.0;
                            int type = int.Parse(f[3], CultureInfo.InvariantCulture);
                            if (hit < 0) throw new FormatException("음수 노트 시각은 지원하지 않습니다.");
                            bool hold = (type & 128) != 0;
                            if (!hold && (type & 1) == 0)
                                throw new FormatException("탭/롱노트 외의 오브젝트가 있습니다.");
                            double end = hit;
                            if (hold)
                            {
                                if (f.Length < 6) throw new FormatException("롱노트 끝점이 없습니다.");
                                end = OsuNumber(f[5].Split(':')[0]) / 1000.0;
                                if (end <= hit) throw new FormatException("롱노트 끝점은 시작보다 뒤여야 합니다.");
                            }
                            notes.Add(new NoteData {
                                noteId = notes.Count,
                                lane = Mathf.Clamp((int)Math.Floor(x * keys / 512.0), 0, (int)keys - 1),
                                hitTime = hit, endTime = end,
                                noteType = hold ? NoteType.Hold : NoteType.Tap
                            });
                        }
                    }
                    catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                    {
                        throw new FormatException("osu " + lineNumber + "행: " + ex.Message, ex);
                    }
                }
            }
            if (notes.Count == 0) throw new FormatException("채보에 노트가 없습니다.");
            if (timings.Count == 0) throw new FormatException("BPM 타이밍이 없습니다.");
            timings.Sort((a, b) => a.time != b.time ? a.time.CompareTo(b.time) : a.order.CompareTo(b.order));
            var bpmEvents = new List<BpmEventData>();
            double beat = 0;
            for (int i = 0; i < timings.Count; i++)
            {
                if (i > 0) beat += (timings[i].time - timings[i - 1].time) * 1000.0 / timings[i - 1].beatLength;
                double bpm = 60000.0 / timings[i].beatLength;
                if (double.IsInfinity(bpm) || bpm > float.MaxValue || bpm < 1)
                    throw new FormatException("게임에서 표현할 수 없는 BPM입니다.");
                bpmEvents.Add(new BpmEventData { beat = beat, bpm = (float)bpm });
            }
            notes.Sort(CompareNoteTimes);
            string title = ReadOsuProperty(text, "Metadata", "TitleUnicode");
            if (string.IsNullOrWhiteSpace(title)) title = ReadOsuProperty(text, "Metadata", "Title");
            if (!string.IsNullOrWhiteSpace(title)) target.title = title;
            target.initialBpm = bpmEvents[0].bpm;
            target.musicOffset = timings[0].time; // 박자 0의 위치. 노트 시각에는 더하지 않는다.
            target.notes = notes;
            target.bpmEvents = bpmEvents;
            return true;
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException)
        {
            error = ex.Message;
            return false;
        }
    }

    private struct OsuTiming
    {
        public double time;
        public double beatLength;
        public int order;
    }

    private static double OsuNumber(string value)
    {
        double number;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            || double.IsNaN(number) || double.IsInfinity(number))
            throw new FormatException("올바르지 않은 osu 숫자: " + value);
        return number;
    }

    public static string ReadOsuProperty(string text, string sectionName, string key)
    {
        string section = "";
        using (var reader = new StringReader(text ?? ""))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal)) { section = line; continue; }
                if (section != "[" + sectionName + "]" || line.StartsWith("//", StringComparison.Ordinal)) continue;
                int colon = line.IndexOf(':');
                if (colon >= 0 && line.Substring(0, colon).Trim() == key)
                    return line.Substring(colon + 1).Trim();
            }
        }
        return "";
    }

    // OSZ 안에서 6K 채보 텍스트만 읽는다. 음악 파일은 추출/변환하지 않는다.
    // entryName이 비어 있으면 엔트리 이름의 Ordinal 순서상 첫 6K 난이도를 선택한다.
    public static bool TryReadOszChartText(byte[] data, out string text, out string selectedEntry,
        out string error, string entryName = null)
    {
        text = null;
        selectedEntry = null;
        error = null;
        if (data == null) { error = "OSZ 데이터가 없습니다."; return false; }
        try
        {
            using (var stream = new MemoryStream(data, false))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var entries = new List<ZipArchiveEntry>();
                foreach (var entry in zip.Entries)
                    if (entry.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)) entries.Add(entry);
                entries.Sort((left, right) => string.Compare(left.FullName, right.FullName, StringComparison.Ordinal));
                int compatible = 0;
                foreach (var entry in entries)
                {
                    if (!string.IsNullOrWhiteSpace(entryName) && entry.FullName != entryName) continue;
                    if (entry.Length > 16 * 1024 * 1024) throw new InvalidDataException("osu 파일이 16MiB를 넘습니다.");
                    string candidate;
                    using (var reader = new StreamReader(entry.Open(), Encoding.UTF8, true)) candidate = reader.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(entryName))
                    {
                        double keys;
                        if (ReadOsuProperty(candidate, "General", "Mode") != "3"
                            || !double.TryParse(ReadOsuProperty(candidate, "Difficulty", "CircleSize"),
                                NumberStyles.Float, CultureInfo.InvariantCulture, out keys)
                            || keys != RhythmGameController.LaneCount) continue;
                    }
                    compatible++;
                    if (text == null) { text = candidate; selectedEntry = entry.FullName; }
                }
                if (text == null) { error = "OSZ에서 선택 가능한 osu!mania 6K 채보를 찾지 못했습니다."; return false; }
                if (compatible > 1)
                    Debug.LogWarning("ChartAutoLoader: OSZ에 6K 난이도가 " + compatible + "개 있어 "
                        + selectedEntry + "을 선택했습니다. OSZ 에셋 Inspector의 Beatmap Entry Name으로 변경할 수 있습니다.");
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is NotSupportedException)
        {
            text = null;
            selectedEntry = null;
            error = "OSZ 읽기 오류: " + ex.Message;
            return false;
        }
    }

    public static bool TryApplyOsz(byte[] data, ChartData target, out string error, string entryName = null)
    {
        if (target == null) { error = "ChartData가 없습니다."; return false; }
        string text, selected;
        return TryReadOszChartText(data, out text, out selected, out error, entryName)
            && TryApplyOsu(text, target, out error);
    }

    private struct OsuSummary
    {
        public float bpm;
        public int notes;
    }
    private static readonly Dictionary<TextAsset, OsuSummary> osuSummaries = new Dictionary<TextAsset, OsuSummary>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOsuSummaries() { osuSummaries.Clear(); }

    // 곡 선택의 BPM/노트 수 표시도 동일한 osu 파서를 사용한다. JSON은 기존 UI 코드가 읽는다.
    public static bool TryGetOsuSummary(TextAsset asset, out float bpm, out int noteCount)
    {
        bpm = 0;
        noteCount = 0;
        if (asset == null) return false;
        OsuSummary summary;
        if (osuSummaries.TryGetValue(asset, out summary))
        {
            bpm = summary.bpm;
            noteCount = summary.notes;
            return true;
        }
        byte[] bytes = asset.bytes;
        bool zip = bytes.Length >= 4 && bytes[0] == 'P' && bytes[1] == 'K' && bytes[2] == 3 && bytes[3] == 4;
        string text = zip ? null : asset.text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        OsuSourceFile source;
        if (!zip && TryUnwrapOsuSource(text, out source)) text = source.osuText.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (!zip && !text.StartsWith("osu file format v", StringComparison.Ordinal)) return false;
        var chart = ScriptableObject.CreateInstance<ChartData>();
        try
        {
            string error;
            bool ok = zip ? TryApplyOsz(bytes, chart, out error) : TryApplyOsu(text, chart, out error);
            if (ok) summary = new OsuSummary { bpm = chart.initialBpm, notes = chart.notes.Count };
            else
            {
                summary = new OsuSummary();
                Debug.LogWarning("ChartAutoLoader: " + asset.name + " / " + error);
            }
            osuSummaries[asset] = summary;
            bpm = summary.bpm;
            noteCount = summary.notes;
            return true;
        }
        finally
        {
            if (Application.isPlaying) Destroy(chart);
            else DestroyImmediate(chart);
        }
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

    // 곡 목록 순서: Resources/RhythmSongs/SongOrder.txt에 한 줄에 곡 이름 하나씩 적은 순서대로.
    // 파일에 없는 곡은 그 뒤에 이름순으로 붙는다.
    private List<string> songOrder = new List<string>();

    private void LoadSongOrder()
    {
        songOrder.Clear();
        TextAsset orderFile = Resources.Load<TextAsset>(resourcesFolder + "/SongOrder");
        if (orderFile == null)
        {
            return;
        }

        string[] lines = orderFile.text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length > 0)
            {
                songOrder.Add(line);
            }
        }
    }

    public int CompareSongOrder(string left, string right)
    {
        int leftIndex = songOrder.FindIndex(n => string.Equals(n, left, StringComparison.OrdinalIgnoreCase));
        int rightIndex = songOrder.FindIndex(n => string.Equals(n, right, StringComparison.OrdinalIgnoreCase));
        if (leftIndex < 0) leftIndex = int.MaxValue;
        if (rightIndex < 0) rightIndex = int.MaxValue;
        if (leftIndex != rightIndex)
        {
            return leftIndex.CompareTo(rightIndex);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    // Native osu > native osz > 기존 JSON/텍스트. 이전 변환 JSON이 있어도 새 원본을 우선한다.
    private static TextAsset FindTextAsset(TextAsset[] chartFiles, string songName)
    {
        if (chartFiles == null) return null;
        TextAsset best = null;
        int bestPriority = int.MaxValue;
        foreach (TextAsset asset in chartFiles)
        {
            string matchName = asset.name;
            int priority = 2;
            OsuSourceFile source;
            if (TryUnwrapOsuSource(asset.text, out source))
            {
                matchName = source.sourceName;
                priority = source.sourceExtension == ".osu" ? 0 : 1;
            }
            if (!string.Equals(matchName, songName, StringComparison.OrdinalIgnoreCase)) continue;
            if (priority < bestPriority || (priority == bestPriority && best != null
                && string.Compare(asset.name, best.name, StringComparison.Ordinal) < 0))
            { best = asset; bestPriority = priority; }
        }
        return best;
    }

    // 음악과 확장자를 제외한 이름이 같은 이미지를 앨범커버로 연결한다.
    private static Texture2D FindTexture(Texture2D[] textures, string songName)
    {
        if (textures == null)
        {
            return null;
        }

        for (int i = 0; i < textures.Length; i++)
        {
            if (string.Equals(
                textures[i].name,
                songName,
                StringComparison.OrdinalIgnoreCase))
            {
                return textures[i];
            }
        }

        return null;
    }

    private static Texture2D FindSpriteTexture(Sprite[] sprites, string songName)
    {
        if (sprites == null)
        {
            return null;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            if (string.Equals(
                sprites[i].name,
                songName,
                StringComparison.OrdinalIgnoreCase))
            {
                return sprites[i].texture;
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
