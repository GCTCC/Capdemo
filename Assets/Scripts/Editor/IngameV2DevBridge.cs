using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// [임시 개발 도구] Claude가 인게임 v2 작업 중 컴파일·Play·스크린샷을 확인하려고 만든 파일. 작업이 끝나면 지운다.
// 프로젝트 Temp/ingamev2_cmd.txt 에 명령을 쓰면 실행하고, 결과를 Temp/ingamev2_out.txt 에 남긴다.
[InitializeOnLoad]
public static class IngameV2DevBridge
{
    private static readonly string CmdPath = Path.GetFullPath("Temp/ingamev2_cmd.txt");
    private static readonly string OutPath = Path.GetFullPath("Temp/ingamev2_out.txt");
    private static double nextPoll;

    static IngameV2DevBridge()
    {
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        // Unity 창이 뒤에 있어도 Play가 멈추지 않게 (프로젝트 설정은 바꾸지 않고 이번 Play에서만)
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    private static void OnPlayMode(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode) Application.runInBackground = true;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
        {
            Write("[" + type + "] " + message + (type == LogType.Exception ? "\n" + stack : ""));
        }
    }

    private static void Write(string line)
    {
        try { File.AppendAllText(OutPath, System.DateTime.Now.ToString("HH:mm:ss") + " " + line + "\n"); }
        catch { }
    }

    // 전환 연출 캡처 예약: (시각, 파일 이름)
    private static readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<double, string>> shots =
        new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<double, string>>();

    private static void Poll()
    {
        double now = EditorApplication.timeSinceStartup;
        for (int i = shots.Count - 1; i >= 0; i--)
        {
            if (now >= shots[i].Key)
            {
                string path = Path.GetFullPath("Temp/" + shots[i].Value + ".png");
                ScreenCapture.CaptureScreenshot(path);
                Write("shot " + shots[i].Value + " (late " + (now - shots[i].Key).ToString("0.000") + "s)");
                shots.RemoveAt(i);
            }
        }
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (!File.Exists(CmdPath)) return;
        string[] lines;
        try
        {
            lines = File.ReadAllLines(CmdPath);
            File.Delete(CmdPath);
        }
        catch { return; }

        foreach (var raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            string cmd = line, arg = "";
            int sp = line.IndexOf(' ');
            if (sp > 0) { cmd = line.Substring(0, sp); arg = line.Substring(sp + 1).Trim(); }
            Write("> " + line);
            try { Run(cmd, arg); }
            catch (System.Exception e) { Write("[Exception] " + e); }
        }
    }

    private static void Run(string cmd, string arg)
    {
        switch (cmd)
        {
            case "ping":
                Write("pong playing=" + EditorApplication.isPlaying + " compiling=" + EditorApplication.isCompiling
                    + " scene=" + EditorSceneManager.GetActiveScene().path);
                break;
            case "refresh":
                AssetDatabase.Refresh();
                Write("refreshed");
                break;
            case "build":
                IngameV2Menu.Create();
                Write("built");
                break;
            case "open":
                EditorSceneManager.OpenScene(arg, OpenSceneMode.Single);
                Write("opened " + arg);
                break;
            case "play":
                EditorApplication.isPlaying = true;
                break;
            case "stop":
                EditorApplication.isPlaying = false;
                break;
            case "auto":
                foreach (var c in Object.FindObjectsByType<RhythmGameControllerV2>(FindObjectsSortMode.None))
                {
                    var f = typeof(RhythmGameControllerV2).GetField("autoPlay",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    f.SetValue(c, arg == "on");
                    Write("autoPlay=" + arg);
                }
                break;
            case "shot":
                string path = Path.GetFullPath("Temp/" + arg + ".png");
                ScreenCapture.CaptureScreenshot(path);
                Write("shot " + path);
                break;
            case "gameview":
                // Game 창을 1920x1080 고정 해상도로 (가능하면)
                var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor");
                var win = EditorWindow.GetWindow(gv);
                Write("gameview size " + win.position);
                break;
            case "bgm":
                {
                    var b = Object.FindAnyObjectByType<SceneBgm>();
                    var src = b ? b.GetComponent<AudioSource>() : null;
                    Write(src ? ("bgm playing=" + src.isPlaying + " vol=" + src.volume.ToString("0.000") + " t=" + src.time.ToString("0.0")) : "no bgm");
                }
                break;
            case "goto":
                Write("goto " + HoloTransition.LoadScene(arg));
                break;
            case "hubbgm":
                HubBgmMenu.Apply();
                Write("hub bgm done");
                break;
            case "tsettings":
                TransitionV2Menu.Create();
                Write("transition settings ready");
                break;
            case "delay":
                {
                    var st = AssetDatabase.LoadAssetAtPath<SongTransitionSettings>("Assets/Resources/SongTransitionSettings.asset");
                    if (st != null) { st.testExtraLoadSeconds = float.Parse(arg); EditorUtility.SetDirty(st); }
                    Write("delay=" + arg);
                }
                break;
            case "press":
                {
                    // press 이름 0.3,0.75,... : 곡 선택에서 플레이를 누르고, 그 뒤 지정한 초마다 캡처
                    var parts = arg.Split(' ');
                    var sel = Object.FindAnyObjectByType<SongSelectHoloController>();
                    if (sel == null) { Write("no song select"); break; }
                    double t0 = EditorApplication.timeSinceStartup;
                    sel.PlaySelected();
                    if (parts.Length > 1)
                    {
                        foreach (var tok in parts[1].Split(','))
                        {
                            float tt = float.Parse(tok);
                            shots.Add(new System.Collections.Generic.KeyValuePair<double, string>(t0 + tt, parts[0] + "_" + tok.Replace('.', 'p')));
                        }
                    }
                    Write("pressed");
                }
                break;
            case "finish":
                {
                    // finish 이름 0.5,1.0,... : 곡을 바로 끝내(결과 전환 시작) 지정한 초마다 캡처
                    var parts = arg.Split(' ');
                    var c = Object.FindAnyObjectByType<RhythmGameControllerV2>();
                    if (c == null) { Write("no controller"); break; }
                    var mi = typeof(RhythmGameControllerV2).GetMethod("FinishSong", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    double t0 = EditorApplication.timeSinceStartup;
                    c.StartCoroutine((System.Collections.IEnumerator)mi.Invoke(c, new object[] { false }));
                    if (parts.Length > 1)
                        foreach (var tok in parts[1].Split(','))
                            shots.Add(new System.Collections.Generic.KeyValuePair<double, string>(t0 + float.Parse(tok), parts[0] + "_" + tok.Replace('.', 'p')));
                    Write("finishing");
                }
                break;
            case "opensettings":
                foreach (var it in Object.FindObjectsByType<HoloMenuItem>(FindObjectsSortMode.None))
                    if (it.PanelPrefab != null) { it.OpenPanel(); Write("opened " + it.name); }
                break;
            case "escdown":
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Escape));
                Write("esc down");
                break;
            case "escup":
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(UnityEngine.InputSystem.Keyboard.current,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState());
                Write("esc up");
                break;
            case "popups":
                foreach (var pp in Object.FindObjectsByType<HoloPopup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    Write(pp.name + " open=" + pp.IsOpen + " active=" + pp.gameObject.activeInHierarchy + " anyOpen=" + HoloPopup.AnyOpen + " kb=" + (UnityEngine.InputSystem.Keyboard.current != null));
                break;
            case "fontmulti":
                foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Art/Fonts" }))
                {
                    var fa = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    if (fa == null || fa.atlasPopulationMode != TMPro.AtlasPopulationMode.Dynamic) continue;
                    if (!fa.isMultiAtlasTexturesEnabled) { fa.isMultiAtlasTexturesEnabled = true; EditorUtility.SetDirty(fa); Write("multi atlas on: " + fa.name); }
                }
                AssetDatabase.SaveAssets();
                break;
            case "tstate":
                {
                    var ov = SongTransitionOverlay.Active;
                    Write(ov == null ? "no overlay" : ("overlay song=" + ov.SongName + " progress=" + ov.Preloader.Progress.ToString("0.00")
                        + " done=" + ov.Preloader.IsDone + " fired=" + ov.FirstBeatFired));
                }
                break;
            case "tdebug":
                {
                    var ov = SongTransitionOverlay.Active;
                    if (ov == null) { Write("no overlay"); break; }
                    foreach (var g in ov.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                    {
                        if (!(g is PerspectiveQuadGraphic) && !(g is ScreenShapeGraphic)) continue;
                        var cr = g.canvasRenderer;
                        Write(g.name + " active=" + g.isActiveAndEnabled + " a=" + g.color.a.ToString("0.00") + " rect=" + g.rectTransform.rect
                            + " cull=" + cr.cull + " mat=" + (g.materialForRendering ? g.materialForRendering.shader.name : "null")
                            + " tex=" + (g.mainTexture ? g.mainTexture.name : "null") + " depth=" + cr.absoluteDepth + " crA=" + cr.GetAlpha()
                            + " clip=" + cr.hasRectClipping + " pos=" + g.rectTransform.position);
                    }
                }
                break;
            case "pause":
                foreach (var pm in Object.FindObjectsByType<PauseMenuV2>(FindObjectsSortMode.None)) pm.OpenPauseMenu();
                Write("paused");
                break;
            case "resume":
                foreach (var pm in Object.FindObjectsByType<PauseMenuV2>(FindObjectsSortMode.None)) pm.BeginResumeCountdown();
                Write("resuming");
                break;
            case "state":
                foreach (var c in Object.FindObjectsByType<RhythmGameControllerV2>(FindObjectsSortMode.None))
                {
                    Write("active=" + c.IsGameplayActive + " combo=" + c.Scores.Combo + " score=" + c.Scores.Score
                        + " t=" + c.Clock.SongTime.ToString("0.00") + " fps=" + (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0"));
                }
                break;
            default:
                Write("unknown command " + cmd);
                break;
        }
    }
}
