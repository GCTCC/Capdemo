using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

// .osu/.osz를 선택해 기존 곡 선택 화면이 읽는 중립 JSON + AudioClip으로 등록한다.
public class OsuManiaImportWindow : EditorWindow
{
    private string sourcePath = "";
    private string songName = "";
    private string status = "파일을 선택하세요. 현재 게임은 osu!mania 6K만 지원합니다.";
    private readonly List<string> entries = new List<string>();
    private int selected;
    private const long MaxAudioBytes = 256L * 1024 * 1024;

    [MenuItem("Tools/CapDemo/리듬게임/osu!mania 채보 가져오기")]
    private static void Open() { GetWindow<OsuManiaImportWindow>("osu!mania 가져오기"); }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("osu!mania → RhythmSongs", EditorStyles.boldLabel);
        if (GUILayout.Button(".osu / .osz 파일 선택")) SelectFile();
        EditorGUILayout.LabelField("선택 파일", Path.GetFileName(sourcePath));
        if (entries.Count > 0)
        {
            selected = EditorGUILayout.Popup("채보 / 난이도", selected, entries.ToArray());
            songName = EditorGUILayout.TextField("게임 곡 이름 / 파일명", songName);
            if (GUILayout.Button("게임에 가져오기")) Import();
        }
        EditorGUILayout.HelpBox(status, MessageType.Info);
    }

    private void SelectFile()
    {
        string path = EditorUtility.OpenFilePanelWithFilters("osu!mania 채보 선택", "",
            new[] { "osu! beatmap", "osu,osz" });
        if (string.IsNullOrEmpty(path)) return;
        entries.Clear();
        selected = 0;
        sourcePath = path;
        songName = Path.GetFileNameWithoutExtension(path);
        try
        {
            if (Path.GetExtension(path).Equals(".osz", StringComparison.OrdinalIgnoreCase))
            {
                using (var zip = ZipFile.OpenRead(path))
                    foreach (var entry in zip.Entries)
                        if (entry.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)) entries.Add(entry.FullName);
                entries.Sort(StringComparer.Ordinal);
            }
            else entries.Add(Path.GetFileName(path));
            status = entries.Count == 0 ? "OSZ 안에 .osu 파일이 없습니다." : "난이도를 선택하고 가져오기를 누르세요. 음악은 함께 복사됩니다.";
        }
        catch (Exception ex) { entries.Clear(); status = ex.Message; }
    }

    private void Import()
    {
        ChartData chart = null;
        try
        {
            string stem = songName.Trim();
            if (string.IsNullOrEmpty(stem) || stem == "." || stem == ".."
                || stem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || stem.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
                throw new InvalidDataException("폴더 구분자나 특수문자 없는 곡 이름을 입력하세요.");
            string text;
            byte[] audio;
            string audioName;
            if (Path.GetExtension(sourcePath).Equals(".osz", StringComparison.OrdinalIgnoreCase))
            {
                using (var zip = ZipFile.OpenRead(sourcePath))
                {
                    var entry = zip.GetEntry(entries[selected]);
                    if (entry == null || entry.Length > 16 * 1024 * 1024) throw new InvalidDataException("osu 파일이 없거나 너무 큽니다.");
                    using (var reader = new StreamReader(entry.Open(), Encoding.UTF8, true)) text = reader.ReadToEnd();
                    audioName = ChartAutoLoader.ReadOsuProperty(text, "General", "AudioFilename").Trim('"');
                    string directory = Path.GetDirectoryName(entry.FullName.Replace('\\', '/')) ?? "";
                    string audioPath = SafeRelativePath(directory, audioName);
                    var audioEntry = zip.GetEntry(audioPath);
                    if (audioEntry == null)
                        foreach (var candidate in zip.Entries)
                            if (string.Equals(candidate.FullName.Replace('\\', '/'), audioPath, StringComparison.OrdinalIgnoreCase))
                            { audioEntry = candidate; break; }
                    if (audioEntry == null || audioEntry.Length > MaxAudioBytes)
                        throw new InvalidDataException("OSZ의 음악이 없거나 256MiB를 넘습니다: " + audioPath);
                    using (var input = audioEntry.Open())
                    using (var buffer = new MemoryStream()) { input.CopyTo(buffer); audio = buffer.ToArray(); }
                }
            }
            else
            {
                if (new FileInfo(sourcePath).Length > 16 * 1024 * 1024) throw new InvalidDataException("osu 파일이 너무 큽니다.");
                text = File.ReadAllText(sourcePath, Encoding.UTF8);
                audioName = ChartAutoLoader.ReadOsuProperty(text, "General", "AudioFilename").Trim('"');
                string relative = SafeRelativePath("", audioName);
                string audioPath = Path.Combine(Path.GetDirectoryName(sourcePath), relative);
                if (!File.Exists(audioPath) || new FileInfo(audioPath).Length > MaxAudioBytes)
                    throw new InvalidDataException("osu 옆에 음악 파일이 없거나 256MiB를 넘습니다: " + relative);
                audio = File.ReadAllBytes(audioPath);
            }

            chart = ScriptableObject.CreateInstance<ChartData>();
            chart.songId = stem;
            string error;
            if (!ChartAutoLoader.TryApplyOsu(text, chart, out error)) throw new InvalidDataException(error);
            string ext = Path.GetExtension(audioName).ToLowerInvariant();
            if (ext != ".mp3" && ext != ".ogg" && ext != ".wav" && ext != ".aif" && ext != ".aiff")
                throw new InvalidDataException("지원 음악 형식: mp3, ogg, wav, aif, aiff");

            const string folder = "Assets/Resources/RhythmSongs";
            // 기존 곡은 덮어쓰지 않는다. 이름을 바꾸면 별도 곡/난이도로 등록된다.
            if (Directory.Exists(folder))
                foreach (string existing in Directory.GetFiles(folder))
                    if (string.Equals(Path.GetFileNameWithoutExtension(existing), stem, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("같은 이름의 파일이 있습니다. 다른 게임 곡 이름을 입력하세요: " + stem);
            var json = new ExportChart {
                songId = stem, title = chart.title, initialBpm = chart.initialBpm,
                musicOffset = chart.musicOffset, bpmEvents = chart.bpmEvents.ToArray(),
                notes = chart.notes.ConvertAll(n => new ExportNote {
                    noteId = n.noteId, lane = n.lane, hitTime = n.hitTime,
                    type = n.noteType.ToString(), endTime = n.endTime
                }).ToArray()
            };
            string jsonText = JsonUtility.ToJson(json, true);
            Directory.CreateDirectory(folder);
            string audioOut = folder + "/" + stem + ext;
            string jsonOut = folder + "/" + stem + ".json";
            try
            {
                File.WriteAllBytes(audioOut, audio);
                File.WriteAllText(jsonOut, jsonText, new UTF8Encoding(false));
            }
            catch
            {
                if (File.Exists(audioOut)) File.Delete(audioOut);
                if (File.Exists(jsonOut)) File.Delete(jsonOut);
                throw;
            }
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(audioOut) as AudioImporter;
            if (importer != null)
            {
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }
            status = "가져오기 완료: " + stem + " / " + chart.notes.Count + "개 노트. 곡 선택 화면에서 플레이할 수 있습니다.";
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonOut);
            Debug.Log(status);
        }
        catch (Exception ex) { status = "가져오기 실패: " + ex.Message; Debug.LogWarning(status); }
        finally { if (chart != null) DestroyImmediate(chart); }
    }

    private static string SafeRelativePath(string directory, string filename)
    {
        filename = filename.Trim().Trim('"').Replace('\\', '/');
        if (filename.Length == 0 || filename.StartsWith("/", StringComparison.Ordinal) || filename.Contains(":"))
            throw new InvalidDataException("잘못된 AudioFilename입니다.");
        string path = (directory.Length == 0 ? "" : directory.Replace('\\', '/') + "/") + filename;
        foreach (string part in path.Split('/'))
            if (part == ".." || part == "." || part.Length == 0) throw new InvalidDataException("안전하지 않은 음악 경로입니다.");
        return path;
    }

    [Serializable] private class ExportChart
    {
        public string format = "capdemo-neutral-v1";
        public string songId, title;
        public float initialBpm;
        public double musicOffset;
        public BpmEventData[] bpmEvents;
        public ExportNote[] notes;
    }
    [Serializable] private class ExportNote
    {
        public int noteId, lane;
        public double hitTime, endTime;
        public string type;
    }
}
