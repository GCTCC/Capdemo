using System.Collections.Generic;
using UnityEngine;

// 설정 창(SettingsPanel)의 값을 저장하고 게임에 반영하는 곳. 설정값은 전부 이 파일을 거친다.
// 저장은 PlayerPrefs(opt_ 로 시작하는 키). SaveData.ResetProgress(진행 초기화)는 이 값들을 지우지 않는다.
//   - 음량: 전체(AudioListener 전체 소리) / 배경음 / 효과음. 배경음·효과음은 StoryAudio, AudioVolumeTag가 곱해 쓴다
//   - 리듬: 배속·싱크. 곡 선택 화면(SongSelectHoloController)과 같은 키를 쓴다. 인게임 반영은 아직 안 함
//   - 화면: 전체화면/창, 해상도, 프레임 제한
public static class GameSettings
{
    private const string MasterKey = "opt_master";
    private const string BgmKey = "opt_bgm";
    private const string SfxKey = "opt_sfx";
    public const string NoteSpeedKey = "opt_noteSpeed"; // SongSelectHoloController와 같은 키
    public const string SyncKey = "opt_syncMs";         // SongSelectHoloController와 같은 키
    private const string FullscreenKey = "opt_fullscreen";
    private const string ResWidthKey = "opt_resW";
    private const string ResHeightKey = "opt_resH";
    private const string FrameLimitKey = "opt_fps";

    // 배속·싱크 범위 (곡 선택 화면과 같게)
    public const float NoteSpeedMin = 1f, NoteSpeedMax = 10f, NoteSpeedStep = 0.5f, NoteSpeedDefault = 8.5f;
    public const int SyncMin = -200, SyncMax = 200, SyncStep = 2, SyncDefault = 12;

    // 프레임 제한 선택지. 0 = 제한 없음
    public static readonly int[] FrameLimits = { 30, 60, 120, 144, 0 };

    // 값이 바뀌면 알린다 (음량을 쓰는 곳들이 바로 다시 맞춘다)
    public static event System.Action Changed;

    private static bool loaded;
    private static float master = 1f, bgm = 1f, sfx = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStart()
    {
        loaded = false; // 도메인 리로드를 꺼 둬서 Play 사이에 남은 값을 버린다
        Load();
        AudioListener.volume = master;
        // 설정 창에서 한 번이라도 고른 적이 있을 때만 적용한다 (그 전에는 원래 프로젝트 설정 그대로)
        if (PlayerPrefs.HasKey(FrameLimitKey)) ApplyFrameLimit(FrameLimit);
#if !UNITY_EDITOR
        // 빌드한 게임에서만 저장된 화면 모드·해상도를 적용한다 (에디터 Game 창은 건드리지 않는다)
        if (PlayerPrefs.HasKey(FullscreenKey) || PlayerPrefs.HasKey(ResWidthKey)) ApplyScreen();
#endif
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;
        master = PlayerPrefs.GetFloat(MasterKey, 1f);
        bgm = PlayerPrefs.GetFloat(BgmKey, 1f);
        sfx = PlayerPrefs.GetFloat(SfxKey, 1f);
    }

    // ── 사운드 ───────────────────────────────────────────

    public static float MasterVolume
    {
        get { Load(); return master; }
        set
        {
            Load();
            master = Mathf.Clamp01(value);
            AudioListener.volume = master;
            PlayerPrefs.SetFloat(MasterKey, master);
            Changed?.Invoke();
        }
    }

    public static float BgmVolume
    {
        get { Load(); return bgm; }
        set
        {
            Load();
            bgm = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(BgmKey, bgm);
            Changed?.Invoke();
        }
    }

    public static float SfxVolume
    {
        get { Load(); return sfx; }
        set
        {
            Load();
            sfx = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxKey, sfx);
            Changed?.Invoke();
        }
    }

    // ── 리듬 (저장만, 인게임 반영은 아직) ─────────────────────

    public static float NoteSpeed
    {
        get => PlayerPrefs.GetFloat(NoteSpeedKey, NoteSpeedDefault);
        set
        {
            float v = Mathf.Clamp(Mathf.Round(value / NoteSpeedStep) * NoteSpeedStep, NoteSpeedMin, NoteSpeedMax);
            PlayerPrefs.SetFloat(NoteSpeedKey, v);
            Changed?.Invoke();
        }
    }

    public static int SyncMs
    {
        get => PlayerPrefs.GetInt(SyncKey, SyncDefault);
        set
        {
            PlayerPrefs.SetInt(SyncKey, Mathf.Clamp(value, SyncMin, SyncMax));
            Changed?.Invoke();
        }
    }

    // ── 화면 ────────────────────────────────────────────

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        set
        {
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            ApplyScreen();
            Changed?.Invoke();
        }
    }

    // 고를 수 있는 해상도 (모니터가 지원하는 것, 같은 크기는 하나만, 작은 것부터)
    public static List<Vector2Int> Resolutions
    {
        get
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (size.x >= 960 && !list.Contains(size)) list.Add(size);
            }
            if (list.Count == 0) list.Add(new Vector2Int(1920, 1080));
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }
    }

    public static Vector2Int Resolution
    {
        get => new Vector2Int(PlayerPrefs.GetInt(ResWidthKey, Screen.width), PlayerPrefs.GetInt(ResHeightKey, Screen.height));
        set
        {
            PlayerPrefs.SetInt(ResWidthKey, value.x);
            PlayerPrefs.SetInt(ResHeightKey, value.y);
            ApplyScreen();
            Changed?.Invoke();
        }
    }

    // 0 = 제한 없음
    public static int FrameLimit
    {
        get => PlayerPrefs.GetInt(FrameLimitKey, 0);
        set
        {
            PlayerPrefs.SetInt(FrameLimitKey, Mathf.Max(0, value));
            ApplyFrameLimit(value);
            Changed?.Invoke();
        }
    }

    private static void ApplyScreen()
    {
        var size = Resolution;
        var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.SetResolution(size.x, size.y, mode);
    }

    private static void ApplyFrameLimit(int fps)
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fps > 0 ? fps : -1;
    }

    // 설정 창을 닫을 때 디스크에 쓴다 (슬라이더를 끄는 동안 매번 쓰지 않게)
    public static void Save() => PlayerPrefs.Save();
}
