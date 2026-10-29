using System.Collections.Generic;
using UnityEngine;

// Resources/RhythmSongs에서 찾은 곡을 보여주고 플레이할 곡을 선택받는 로비다.
[DisallowMultipleComponent]
public class SongLobbyMenu : MonoBehaviour
{
    private RhythmGameController controller;
    private List<ChartAutoLoader.SongEntry> songs
        = new List<ChartAutoLoader.SongEntry>();
    private Vector2 scrollPosition;
    private bool isVisible;
    private bool wasOpenedFromPause;

    public bool IsVisible
    {
        get { return isVisible; }
    }

    public void Initialize(RhythmGameController newController)
    {
        controller = newController;
    }

    public void Show(
        List<ChartAutoLoader.SongEntry> availableSongs,
        bool openedFromPause)
    {
        songs = availableSongs ?? new List<ChartAutoLoader.SongEntry>();
        wasOpenedFromPause = openedFromPause;
        scrollPosition = Vector2.zero;
        isVisible = true;
    }

    public void Hide()
    {
        isVisible = false;
    }

    private void OnGUI()
    {
        if (!isVisible || controller == null)
        {
            return;
        }

        GUI.depth = -200;
        DrawScreenRect(
            new Rect(0.0f, 0.0f, Screen.width, Screen.height),
            new Color(0.004f, 0.008f, 0.028f, 0.96f));

        float panelWidth = Mathf.Min(760.0f, Screen.width * 0.86f);
        float panelHeight = Mathf.Min(650.0f, Screen.height * 0.84f);
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        DrawScreenRect(panel, new Color(0.025f, 0.035f, 0.095f, 0.98f));
        DrawScreenRect(
            new Rect(panel.x, panel.y, panel.width, 5.0f),
            new Color(0.08f, 0.95f, 1.0f));
        DrawScreenRect(
            new Rect(panel.x, panel.yMax - 5.0f, panel.width, 5.0f),
            new Color(1.0f, 0.08f, 0.72f));

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.fontSize = Mathf.Max(32, Screen.height / 21);
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(0.32f, 1.0f, 0.96f);

        GUIStyle guideStyle = new GUIStyle(GUI.skin.label);
        guideStyle.alignment = TextAnchor.MiddleCenter;
        guideStyle.fontSize = Mathf.Max(15, Screen.height / 52);
        guideStyle.normal.textColor = new Color(0.70f, 0.82f, 1.0f);

        GUI.Label(
            new Rect(panel.x, panel.y + 22.0f, panel.width, 58.0f),
            "SONG SELECT // 곡 선택",
            titleStyle);
        GUI.Label(
            new Rect(panel.x, panel.y + 76.0f, panel.width, 30.0f),
            "채보 있음: JSON 플레이  ·  채보 없음: 랜덤 노트 플레이",
            guideStyle);

        if (songs.Count == 0)
        {
            DrawEmptyLobby(panel, guideStyle);
            return;
        }

        Rect listRect = new Rect(
            panel.x + 44.0f,
            panel.y + 125.0f,
            panel.width - 88.0f,
            panel.height - 205.0f);
        float contentHeight = Mathf.Max(listRect.height, songs.Count * 76.0f);
        scrollPosition = GUI.BeginScrollView(
            listRect,
            scrollPosition,
            new Rect(0.0f, 0.0f, listRect.width - 20.0f, contentHeight));

        GUIStyle songButtonStyle = new GUIStyle(GUI.skin.button);
        songButtonStyle.alignment = TextAnchor.MiddleLeft;
        songButtonStyle.fontSize = Mathf.Max(18, Screen.height / 42);
        songButtonStyle.fontStyle = FontStyle.Bold;
        songButtonStyle.padding = new RectOffset(22, 14, 6, 6);
        songButtonStyle.normal.textColor = Color.white;
        songButtonStyle.hover.textColor = new Color(0.30f, 1.0f, 0.95f);

        for (int i = 0; i < songs.Count; i++)
        {
            ChartAutoLoader.SongEntry song = songs[i];
            string chartState = song.HasChart
                ? "  [채보 있음]"
                : "  [랜덤 채보]";

            GUI.backgroundColor = song.HasChart
                ? new Color(0.08f, 0.42f, 0.52f)
                : new Color(0.34f, 0.12f, 0.42f);

            if (GUI.Button(
                new Rect(4.0f, i * 76.0f + 4.0f, listRect.width - 32.0f, 62.0f),
                song.DisplayName + chartState,
                songButtonStyle))
            {
                controller.PlaySelectedSong(song);
            }
        }

        GUI.backgroundColor = Color.white;
        GUI.EndScrollView();

        if (GUI.Button(
            new Rect(panel.xMax - 180.0f, panel.yMax - 58.0f, 136.0f, 38.0f),
            "목록 새로고침"))
        {
            controller.RefreshLobbySongs(wasOpenedFromPause);
        }
    }

    private void DrawEmptyLobby(Rect panel, GUIStyle guideStyle)
    {
        GUIStyle emptyStyle = new GUIStyle(guideStyle);
        emptyStyle.fontSize = Mathf.Max(20, Screen.height / 35);
        emptyStyle.normal.textColor = new Color(1.0f, 0.45f, 0.78f);

        GUI.Label(
            new Rect(panel.x + 30.0f, panel.center.y - 70.0f, panel.width - 60.0f, 50.0f),
            "Resources/RhythmSongs에 음악 파일이 없습니다.",
            emptyStyle);

        GUI.Label(
            new Rect(panel.x + 30.0f, panel.center.y - 20.0f, panel.width - 60.0f, 54.0f),
            "음악 없이 랜덤 채보를 다시 시작할 수 있습니다.",
            guideStyle);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = Mathf.Max(20, Screen.height / 38);
        buttonStyle.fontStyle = FontStyle.Bold;

        GUI.backgroundColor = new Color(0.18f, 0.22f, 0.50f);
        if (GUI.Button(
            new Rect(panel.center.x - 150.0f, panel.center.y + 55.0f, 300.0f, 58.0f),
            "랜덤 모드 시작",
            buttonStyle))
        {
            controller.PlayRandomWithoutMusic();
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawScreenRect(Rect rect, Color color)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }
}
