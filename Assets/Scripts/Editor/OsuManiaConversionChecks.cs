using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

// 외부 테스트 패키지 없이 Unity 메뉴에서 실행하는 회귀 검사.
public static class OsuManiaConversionChecks
{
    private const string Fixture = "osu file format v14\n[General]\nMode:3\n[Metadata]\nTitle:Test\n"
        + "[Difficulty]\nCircleSize:6\n[TimingPoints]\n1000,500,4,0,0,100,1,0\n"
        + "1500,-50,4,0,0,100,0,0\n2000,250,4,0,0,100,1,0\n[HitObjects]\n"
        + "512,192,2500,132,0,3000:0:0:0:0:\n-1,192,1500,5,0,0:0:0:0:\n";

    [MenuItem("Tools/CapDemo/리듬게임/osu!mania 변환 자체 검사")]
    public static void Run()
    {
        var chart = ScriptableObject.CreateInstance<ChartData>();
        var culture = CultureInfo.CurrentCulture;
        try
        {
            string error;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Require(ChartAutoLoader.TryApplyOsu("\uFEFF" + Fixture, chart, out error), error);
            Require(chart.notes.Count == 2 && chart.notes[0].lane == 0 && chart.notes[1].lane == 5, "정렬/레인 경계");
            Require(chart.notes[0].noteType == NoteType.Tap && chart.notes[1].noteType == NoteType.Hold, "비트 플래그");
            Require(chart.notes[0].hitTime == 1.5 && chart.notes[1].endTime == 3, "ms→초 변환/offset 중복 금지");
            Require(chart.initialBpm == 120 && chart.musicOffset == 1, "초기 BPM/박자 원점");
            Require(chart.bpmEvents.Count == 2 && chart.bpmEvents[1].beat == 2 && chart.bpmEvents[1].bpm == 240, "변속/SV 제외");
            var nativeAsset = new TextAsset(ChartAutoLoader.CreateOsuSourceAssetText(Fixture, "My Song", ".osz"));
            try
            {
                float bpm;
                int count;
                Require(ChartAutoLoader.TryGetOsuSummary(nativeAsset, out bpm, out count)
                    && bpm == 120 && count == 2, "자동 임포터 내부 포맷/곡 선택 메타데이터");
            }
            finally { UnityEngine.Object.DestroyImmediate(nativeAsset); }
            var originalNotes = chart.notes;
            foreach (string bad in new[] {
                Fixture.Replace("Mode:3", "Mode:0"), Fixture.Replace("CircleSize:6", "CircleSize:4"),
                Fixture.Replace("3000:", "2000:"), Fixture.Replace("2500,132", "NaN,132"),
                Fixture.Replace("2500,132", "-1,132"), Fixture.Replace("1000,500", "1000,0"),
                Fixture.Replace("2500,132", "2500,8"), "{}"
            })
            {
                Require(!ChartAutoLoader.TryApplyOsu(bad, chart, out error), "잘못된 입력 거절");
                Require(ReferenceEquals(originalNotes, chart.notes), "실패 시 대상 보존");
            }
            Require(ChartAutoLoader.TryApplyOsz(Archive(false), chart, out error), error);
            Require(ChartAutoLoader.TryApplyOsz(Archive(true), chart, out error), "여러 난이도 기본 선택");
            Require(ChartAutoLoader.TryApplyOsz(Archive(true), chart, out error, "second.osu"), error);
            Require(!ChartAutoLoader.TryApplyOsz(new byte[] { 1, 2 }, chart, out error), "손상 ZIP 거절");
            Require(!ChartAutoLoader.TryApplyOsz(Archive(false), chart, out error, "missing.osu"), "없는 난이도 거절");
            Debug.Log("osu!mania 변환 자체 검사 통과: 레인/탭/홀드/시간/BPM/SV/문화권/실패 보존/OSZ 난이도/손상 파일");
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            UnityEngine.Object.DestroyImmediate(chart);
        }
    }

    private static byte[] Archive(bool multiple)
    {
        using (var buffer = new MemoryStream())
        {
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("first.osu").Open(), Encoding.UTF8)) writer.Write(Fixture);
                if (multiple)
                    using (var writer = new StreamWriter(zip.CreateEntry("second.osu").Open(), Encoding.UTF8)) writer.Write(Fixture);
            }
            return buffer.ToArray();
        }
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("osu!mania 검사 실패: " + message);
    }
}
