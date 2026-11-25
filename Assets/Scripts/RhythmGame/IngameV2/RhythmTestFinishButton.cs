#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 에디터 전용 테스트 버튼: 인게임 v2(RhythmScene_v2)에서 곡을 바로 끝내고 결과 화면으로 넘어간다.
//  - Play 중 곡이 진행되고 있으면 Game 화면 왼쪽 아래에 "곡 바로 끝내기 (테스트)" 버튼이 보인다
//  - 위 메뉴 Tools > CapDemo > 리듬게임 > 테스트: 지금 곡 바로 끝내기 로도 할 수 있다
//  - 버튼을 숨기려면 Tools > CapDemo > 리듬게임 > 테스트: 곡 끝내기 버튼 보이기 체크를 끈다
// 파일 전체가 #if UNITY_EDITOR 안에 있어서 빌드한 게임에는 들어가지 않는다.
// (Editor 폴더 안 스크립트는 Play 중 오브젝트에 붙일 수 없어서, 이 파일만 Editor 폴더 밖에 둔다) 인게임 스크립트는 고치지 않고,
// 곡이 정상으로 끝날 때 쓰는 FinishSong을 그대로 불러서 결과 화면 전환(레코드 착지 연출 포함)도 똑같이 탄다.
// 지금까지 친 판정·점수 그대로 결과로 간다 (남은 노트는 치지 않은 것으로 남는다).
public class RhythmTestFinishButton : MonoBehaviour
{
    private const string ShowPref = "CapDemo.RhythmTestFinishButton.Show";
    private const string MenuShow = "Tools/CapDemo/리듬게임/테스트: 곡 끝내기 버튼 보이기";
    private const string MenuFinish = "Tools/CapDemo/리듬게임/테스트: 지금 곡 바로 끝내기";

    private static RhythmTestFinishButton instance;

    [InitializeOnLoadMethod]
    private static void Init()
    {
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) Spawn();
    }

    private static void Spawn()
    {
        if (instance != null) return;
        var go = new GameObject("[Editor] Rhythm Test Finish Button");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go); // Play를 멈추면 같이 사라진다
        instance = go.AddComponent<RhythmTestFinishButton>();
    }

    private static bool Show
    {
        get { return EditorPrefs.GetBool(ShowPref, true); }
        set { EditorPrefs.SetBool(ShowPref, value); }
    }

    [MenuItem(MenuShow, priority = 90)]
    private static void ToggleShow()
    {
        Show = !Show;
    }

    [MenuItem(MenuShow, true)]
    private static bool ToggleShowValidate()
    {
        Menu.SetChecked(MenuShow, Show);
        return true;
    }

    [MenuItem(MenuFinish, priority = 91)]
    private static void FinishFromMenu()
    {
        if (!TryFinish(FindController())) Debug.Log("[테스트] 지금 진행 중인 곡이 없어요. 인게임에서 곡이 시작된 뒤에 눌러 주세요.");
    }

    [MenuItem(MenuFinish, true)]
    private static bool FinishFromMenuValidate()
    {
        return EditorApplication.isPlaying;
    }

    private static RhythmGameControllerV2 FindController()
    {
        return Object.FindAnyObjectByType<RhythmGameControllerV2>();
    }

    private static bool TryFinish(RhythmGameControllerV2 c)
    {
        if (c == null || !c.IsGameplayActive) return false;
        MethodInfo mi = typeof(RhythmGameControllerV2).GetMethod("FinishSong", BindingFlags.NonPublic | BindingFlags.Instance);
        if (mi == null)
        {
            Debug.LogWarning("[테스트] RhythmGameControllerV2.FinishSong을 찾지 못했어요.");
            return false;
        }
        object[] args = mi.GetParameters().Length == 1 ? new object[] { false } : new object[0];
        var routine = mi.Invoke(c, args) as IEnumerator;
        if (routine == null) return false;
        c.StartCoroutine(routine);
        Debug.Log("[테스트] 곡을 바로 끝내고 결과 화면으로 넘어가요.");
        return true;
    }

    // ── Game 화면 버튼 ──
    private RhythmGameControllerV2 controller;
    private float nextLookup;
    private GUIStyle style;

    private void OnGUI()
    {
        if (!Show) return;
        if (controller == null && Time.unscaledTime >= nextLookup)
        {
            controller = FindController();
            nextLookup = Time.unscaledTime + 0.5f;
        }
        if (controller == null || !controller.IsGameplayActive) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
            style.normal.textColor = new Color(1f, 0.85f, 0.4f);
        }
        GUI.depth = -3000;
        var rect = new Rect(12, Screen.height - 44, 190, 32);
        if (GUI.Button(rect, "▶▶  곡 바로 끝내기 (테스트)", style)) TryFinish(controller);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
#endif
