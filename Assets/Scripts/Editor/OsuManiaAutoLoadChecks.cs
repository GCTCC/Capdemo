using System;
using UnityEditor;
using UnityEngine;

// 실제 Resources → SongEntry → ChartData 연결을 Unity Play 중 검사한다.
public static class OsuManiaAutoLoadChecks
{
    [MenuItem("Tools/CapDemo/리듬게임/osu 자동 연결 검사 (Play 중)")]
    private static void Run()
    {
        var go = new GameObject("osu Auto Load Check");
        var loader = go.AddComponent<ChartAutoLoader>();
        int tested = 0;
        try
        {
            foreach (var song in loader.GetAvailableSongs())
            {
                float bpm;
                int count;
                if (!ChartAutoLoader.TryGetOsuSummary(song.chartFile, out bpm, out count)) continue;
                ChartData chart;
                if (!loader.TryLoadSong(song, out chart) || chart == null || !loader.HasMatchedChart)
                    throw new InvalidOperationException("채보 로드 실패: " + song.DisplayName + " / " + loader.LastLoadMessage);
                if (chart.audioClip != song.audioClip || chart.notes.Count != count || count == 0)
                    throw new InvalidOperationException("음악/노트 연결 불일치: " + song.DisplayName);
                Debug.Log("[osu 자동 연결 검사] " + song.DisplayName + " → " + song.chartFile.name
                    + " → " + count + " notes / AudioClip 동일 확인");
                tested++;
            }
            if (tested == 0) throw new InvalidOperationException("검사할 동명 음악+osu/osz 조합이 없습니다. Resources/RhythmSong(s)의 이름/Import 오류를 확인하세요.");
            Debug.Log("[osu 자동 연결 검사] 통과: " + tested + "곡");
        }
        finally { UnityEngine.Object.Destroy(go); }
    }

    [MenuItem("Tools/CapDemo/리듬게임/osu 자동 연결 검사 (Play 중)", true)]
    private static bool CanRun() { return EditorApplication.isPlaying; }
}
