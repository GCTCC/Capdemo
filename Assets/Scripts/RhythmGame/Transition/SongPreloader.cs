using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 곡 선택 → 인게임으로 갈 때 필요한 것을 미리 불러오고 진행률(0~1)을 알려 준다.
//  1) 인게임 씬 LoadSceneAsync (allowSceneActivation = false, 0.9에서 멈추므로 0.9 = 100%)
//  2) 곡 오디오 LoadAudioData
//  3) 채보 JSON 파싱 (원본 ChartAutoLoader를 그대로 써서 읽기만 한다)
//  4) 무대(씬) 준비 확인   5) 배속·싱크 값 준비
// 곡 선택에서 한 곡에 1초 이상 머물면 Warm()으로 오디오·채보를 미리 읽어 둔다 (체감 로딩 줄이기).
public class SongPreloader : MonoBehaviour
{
    // 단계별 가중치 (합 1)
    private const float WMount = 0.05f, WAudio = 0.35f, WChart = 0.2f, WScene = 0.3f, WApply = 0.1f;

    public float Progress { get; private set; }
    public bool IsDone { get; private set; }
    public bool SceneActivated { get; private set; }

    // 로그용 정보
    public bool MountDone { get; private set; }
    public bool AudioDone { get; private set; }
    public bool ChartDone { get; private set; }
    public bool SceneDone { get; private set; }
    public bool ApplyDone { get; private set; }
    public string AudioName { get; private set; }
    public float AudioMegabytes { get; private set; }
    public string ChartFileName { get; private set; }
    public int NoteCount { get; private set; }
    public float Bpm { get; private set; }
    public float Length { get; private set; }

    private ChartAutoLoader.SongEntry song;
    private ChartData chart;               // 이 프리로더가 가진 복사본
    private AsyncOperation sceneOp;
    private float audioP, chartP, sceneP, applyP, mountP;
    private ThreadPriority previousPriority = ThreadPriority.BelowNormal;

    public ChartAutoLoader.SongEntry Song { get { return song; } }

    // 인게임 컨트롤러가 가져갈 채보 (새 복사본. 받은 쪽이 다 쓰면 Destroy)
    public ChartData TakeChartCopy()
    {
        if (chart == null) return null;
        var copy = Instantiate(chart);
        copy.name = chart.name;
        return copy;
    }

    public void Begin(ChartAutoLoader.SongEntry newSong, string sceneName, float extraDelay)
    {
        song = newSong;
        StartCoroutine(Run(sceneName, Mathf.Max(0f, extraDelay)));
    }

    // 레코드가 판정선에 눕기 시작할 때 부른다: 인게임 씬을 켠다
    public void ActivateScene()
    {
        if (sceneOp != null && !SceneActivated)
        {
            SceneActivated = true;
            sceneOp.allowSceneActivation = true;
        }
    }

    private IEnumerator Run(string sceneName, float extra)
    {
        // 1) 씬 비동기 로딩 시작 (다른 단계와 함께 진행)
        // 백그라운드 로딩이 한 프레임에 쓰는 시간을 줄여서 연출이 멈칫하지 않게 한다 (끝나면 되돌림)
        previousPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        sceneOp = SceneManager.LoadSceneAsync(sceneName);
        if (sceneOp != null) sceneOp.allowSceneActivation = false;

        // mount
        yield return Wait(extra * WMount, v => mountP = v);
        mountP = 1f;
        MountDone = true;

        // 2) 오디오
        AudioClip clip = song != null ? song.audioClip : null;
        AudioName = clip != null ? clip.name + ".ogg" : "-";
        if (clip != null)
        {
            if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
            float t = 0f;
            while (clip.loadState == AudioDataLoadState.Loading)
            {
                t += Time.unscaledDeltaTime;
                audioP = Mathf.Min(0.9f, t / 1.5f); // 진행률을 알 수 없어서 시간으로 흉내
                yield return null;
            }
            AudioMegabytes = clip.samples * (float)clip.channels * 2f / (1024f * 1024f);
            Length = clip.length;
        }
        yield return Wait(extra * WAudio, v => audioP = Mathf.Max(audioP, v));
        audioP = 1f;
        AudioDone = true;

        // 3) 채보
        ChartData parsed = GetOrParse(song);
        if (parsed != null)
        {
            chart = Instantiate(parsed);
            chart.name = parsed.name;
            chart.hideFlags = HideFlags.DontUnloadUnusedAsset;
            NoteCount = chart.notes != null ? chart.notes.Count : 0;
            Bpm = chart.SafeBpm;
        }
        ChartFileName = song != null && song.chartFile != null ? song.chartFile.name + ".json" : "(random)";
        yield return Wait(extra * WChart, v => chartP = v);
        chartP = 1f;
        ChartDone = true;

        // 4) 씬(무대) 준비: Unity는 allowSceneActivation=false면 0.9에서 멈춘다
        while (sceneOp != null && sceneOp.progress < 0.9f)
        {
            sceneP = sceneOp.progress / 0.9f;
            yield return null;
        }
        yield return Wait(extra * WScene, v => sceneP = Mathf.Max(sceneP, v));
        sceneP = 1f;
        SceneDone = true;

        // 5) 배속·싱크 값 준비 (실제 적용은 인게임 컨트롤러가 GameSettings에서 읽는다)
        yield return Wait(extra * WApply, v => applyP = v);
        applyP = 1f;
        ApplyDone = true;
        IsDone = true;
    }

    private static IEnumerator Wait(float seconds, System.Action<float> report)
    {
        if (seconds <= 0f) yield break;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            report(t / seconds);
            yield return null;
        }
    }

    private void Update()
    {
        if (sceneOp != null && !SceneDone) sceneP = Mathf.Max(sceneP, Mathf.Clamp01(sceneOp.progress / 0.9f) * 0.95f);
        Progress = Mathf.Clamp01(WMount * mountP + WAudio * audioP + WChart * chartP + WScene * sceneP + WApply * applyP);
        if (IsDone) Progress = 1f;
    }

    private void OnDestroy()
    {
        Application.backgroundLoadingPriority = previousPriority;
        if (chart != null) Destroy(chart);
    }

    // ── 미리 읽어 두기 (곡 선택에서 1초 이상 머문 곡) ──
    private static readonly Dictionary<string, ChartData> cache = new Dictionary<string, ChartData>();
    private static readonly List<string> cacheOrder = new List<string>();
    private static ChartAutoLoader helper;
    private const int CacheSize = 3;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        cache.Clear();
        cacheOrder.Clear();
        helper = null;
    }

    public static void Warm(ChartAutoLoader.SongEntry entry)
    {
        if (entry == null) return;
        if (entry.audioClip != null && entry.audioClip.loadState == AudioDataLoadState.Unloaded) entry.audioClip.LoadAudioData();
        GetOrParse(entry);
    }

    private static ChartData GetOrParse(ChartAutoLoader.SongEntry entry)
    {
        if (entry == null) return null;
        string key = entry.DisplayName;
        ChartData found;
        if (cache.TryGetValue(key, out found) && found != null) return found;

        if (helper == null)
        {
            var go = new GameObject("Song Preload Helper");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            helper = go.AddComponent<ChartAutoLoader>();
        }
        ChartData parsed;
        if (!helper.TryLoadSong(entry, out parsed) || parsed == null) return null;
        // ChartAutoLoader는 다음 곡을 읽을 때 이전 채보를 지우므로 복사해 둔다
        var copy = Object.Instantiate(parsed);
        copy.name = parsed.name;
        copy.hideFlags = HideFlags.DontUnloadUnusedAsset;
        cache[key] = copy;
        cacheOrder.Remove(key);
        cacheOrder.Add(key);
        while (cacheOrder.Count > CacheSize)
        {
            string old = cacheOrder[0];
            cacheOrder.RemoveAt(0);
            ChartData c;
            if (cache.TryGetValue(old, out c) && c != null) Object.Destroy(c);
            cache.Remove(old);
        }
        return copy;
    }
}
