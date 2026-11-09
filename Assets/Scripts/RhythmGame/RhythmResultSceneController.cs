using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 결과 화면은 가상 1600×900 좌표계를 사용해 해상도에 관계없이 배치를 유지한다.
[RequireComponent(typeof(RhythmSceneTransition))]
public class RhythmResultSceneController : MonoBehaviour
{
    [SerializeField] private string songSelectSceneName = "SongSelectScene";
    [SerializeField] private string hubSceneName = "NewHubDemoScene";
    private RhythmSceneTransition transition;
    private PlayResult result;
    private string songTitle;
    private bool loading;
    private static readonly Color Ink = new Color(0.018f, 0.016f, 0.015f);
    private static readonly Color Cream = new Color(0.94f, 0.88f, 0.75f);
    private static readonly Color Dim = new Color(0.62f, 0.56f, 0.48f);
    private static readonly Color Red = new Color(0.55f, 0.075f, 0.08f);

    private void Awake()
    {
        result = RhythmResultContext.Result ?? new PlayResult();
        songTitle = string.IsNullOrWhiteSpace(RhythmResultContext.SongTitle)
            ? "NO PLAY DATA" : RhythmResultContext.SongTitle;
        transition = GetComponent<RhythmSceneTransition>();
        // UI 전용 씬에도 화면을 지우는 카메라를 두어 이전 프레임 잔상을 방지한다.
        if (Camera.main == null)
        {
            GameObject cameraObject = new GameObject("Result Camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink;
            camera.cullingMask = 0;
        }
    }

    private IEnumerator Start()
    {
        yield return transition.RevealScene();
    }

    private void OnGUI()
    {
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        Color oldBackground = GUI.backgroundColor;
        bool oldEnabled = GUI.enabled;
        GUI.depth = -200;
        float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        RectColor(new Rect(0, 0, Screen.width, Screen.height), Ink);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600 * scale) / 2,
            (Screen.height - 900 * scale) / 2, 0), Quaternion.identity,
            new Vector3(scale, scale, 1));

        // 적색 프레임과 얇은 라인으로 기존 허브/HUD의 소재감을 이어간다.
        RectColor(new Rect(64, 58, 1472, 2), Red);
        Label(new Rect(70, 76, 1200, 30), "BROKEN STAGE  //  SESSION REPORT", 19, Dim);
        Label(new Rect(64, 112, 1350, 82), "PLAY RESULT", 64, Cream);
        RectColor(new Rect(64, 212, 1472, 82), new Color(0.075f, 0.055f, 0.048f));
        RectColor(new Rect(64, 212, 8, 82), Red);
        Label(new Rect(92, 230, 1410, 46), Ellipsis(songTitle, 1370, 30), 30, Cream);

        Panel(new Rect(64, 330, 840, 216));
        Label(new Rect(96, 352, 750, 34), "TOTAL SCORE", 22, Dim);
        Label(new Rect(92, 396, 770, 112), result.score.ToString("D8"), 88, Cream);
        Panel(new Rect(64, 566, 840, 164));
        Label(new Rect(96, 590, 370, 32), "MAX COMBO", 22, Dim);
        Label(new Rect(94, 626, 370, 78), result.maxCombo.ToString(), 58, Cream);
        int total = result.perfectCount + result.greatCount + result.goodCount + result.missCount;
        string status = total == 0 ? "NO JUDGEMENTS"
            : result.isFullCombo ? "FULL COMBO" : "SESSION COMPLETE";
        Label(new Rect(470, 620, 400, 70), status, 25, Cream);

        Panel(new Rect(936, 330, 600, 400));
        Label(new Rect(968, 350, 520, 32), "JUDGEMENT BREAKDOWN", 21, Dim);
        Row(406, "PERFECT", result.perfectCount, Color.cyan);
        Row(480, "GREAT", result.greatCount, Color.magenta);
        Row(554, "GOOD", result.goodCount, new Color(0.94f, 0.85f, 0.48f));
        Row(628, "MISS", result.missCount, new Color(0.6f, 0.6f, 0.62f));

        Label(new Rect(64, 804, 700, 38), "6 LANES  /  A S D — J K L", 18, Dim);
        GUI.enabled = !loading && !transition.IsBusy;
        GUIStyle button = Style(24, Cream);
        button.alignment = TextAnchor.MiddleCenter;
        // 우측 하단 두 버튼은 가로 배치로 고정한다.
        if (Button(new Rect(936, 788, 280, 72), "곡 선택", Red, button))
            StartCoroutine(Leave(songSelectSceneName, true));
        if (Button(new Rect(1236, 788, 300, 72), "로비로 돌아가기",
            new Color(0.14f, 0.12f, 0.10f), button))
            StartCoroutine(Leave(hubSceneName, false));
        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
        GUI.backgroundColor = oldBackground;
        GUI.enabled = oldEnabled;
    }

    private IEnumerator Leave(string scene, bool selectSong)
    {
        if (loading || !Application.CanStreamedLevelBeLoaded(scene))
        {
            if (!loading) Debug.LogError("결과 화면 이동 씬을 찾지 못했습니다: " + scene);
            yield break;
        }
        loading = true;
        yield return transition.FadeToBlack();
        // 결과 화면의 곡 선택은 싱글 플레이로 시작한다. 스토리 요청을 잘못 재사용하지 않는다.
        if (selectSong) RhythmLaunchContext.RequestSingle(string.Empty, hubSceneName);
        else RhythmLaunchContext.Clear();
        SceneManager.LoadScene(scene);
    }

    private static void Row(float y, string title, int count, Color color)
    {
        RectColor(new Rect(968, y + 57, 536, 1), new Color(0.18f, 0.15f, 0.13f));
        Label(new Rect(968, y, 310, 54), title, 28, color);
        GUIStyle value = Style(34, Cream);
        value.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(1280, y, 220, 54), count.ToString("D4"), value);
    }

    private static GUIStyle Style(int size, Color color)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = size;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleLeft;
        style.wordWrap = false;
        style.normal.textColor = color;
        return style;
    }

    private static void Label(Rect rect, string text, int size, Color color)
    {
        GUI.Label(rect, text, Style(size, color));
    }

    private static string Ellipsis(string text, float width, int size)
    {
        GUIStyle style = Style(size, Cream);
        if (style.CalcSize(new GUIContent(text)).x <= width) return text;
        while (text.Length > 0 && style.CalcSize(new GUIContent(text + "…")).x > width)
            text = text.Substring(0, text.Length - 1);
        return text + "…";
    }

    private static bool Button(Rect rect, string title, Color background, GUIStyle style)
    {
        RectColor(rect, rect.Contains(Event.current.mousePosition)
            ? background * 1.25f : background);
        return GUI.Button(rect, title, style);
    }

    private static void Panel(Rect rect)
    {
        RectColor(rect, new Color(0.045f, 0.038f, 0.033f));
        RectColor(new Rect(rect.x, rect.y, rect.width, 2), Dim * 0.55f);
    }

    private static void RectColor(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
