using System.Collections.Generic;
using UnityEngine;

// 먹색 카드, 아이보리 타이포와 적색 선택 강조를 사용하는 곡 선택 로비다.
[DisallowMultipleComponent]
public class SongLobbyMenu : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.018f, 0.016f, 0.015f, 0.98f);
    private static readonly Color InkSoft = new Color(0.065f, 0.055f, 0.048f, 0.98f);
    private static readonly Color Cream = new Color(0.94f, 0.88f, 0.75f, 1.0f);
    private static readonly Color CreamDim = new Color(0.68f, 0.62f, 0.52f, 1.0f);
    private static readonly Color DeepRed = new Color(0.55f, 0.075f, 0.08f, 1.0f);
    private static readonly Color Cyan = new Color(0.13f, 0.90f, 0.88f, 1.0f);

    private SongSelectSceneController controller;
    private List<ChartAutoLoader.SongEntry> songs
        = new List<ChartAutoLoader.SongEntry>();
    private Vector2 scrollPosition;
    private bool isVisible;
    private bool wasOpenedFromPause;

    public bool IsVisible
    {
        get { return isVisible; }
    }

    public void Initialize(SongSelectSceneController newController)
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
        DrawRect(new Rect(0.0f, 0.0f, Screen.width, Screen.height), Ink);
        DrawBackdropAccents();

        float panelWidth = Mathf.Min(940.0f, Screen.width * 0.88f);
        float panelHeight = Mathf.Min(720.0f, Screen.height * 0.88f);
        Rect panel = new Rect(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        DrawRect(new Rect(panel.x + 8.0f, panel.y + 10.0f, panel.width, panel.height),
            new Color(0.0f, 0.0f, 0.0f, 0.40f));
        DrawRect(panel, new Color(0.035f, 0.03f, 0.027f, 0.98f));
        DrawRect(new Rect(panel.x, panel.y, 12.0f, panel.height), DeepRed);
        DrawRect(new Rect(panel.x, panel.y, panel.width, 2.0f), CreamDim);

        GUIStyle overlineStyle = MakeStyle(
            Mathf.Max(13, Screen.height / 58),
            CreamDim,
            FontStyle.Bold,
            TextAnchor.MiddleLeft);
        GUIStyle titleStyle = MakeStyle(
            Mathf.Max(34, Screen.height / 21),
            Cream,
            FontStyle.Bold,
            TextAnchor.MiddleLeft);

        GUI.Label(
            new Rect(panel.x + 42.0f, panel.y + 22.0f, panel.width - 84.0f, 26.0f),
            controller.CurrentModeLabel + "  //  MUSIC ARCHIVE",
            overlineStyle);
        GUI.Label(
            new Rect(panel.x + 40.0f, panel.y + 48.0f, panel.width - 80.0f, 58.0f),
            "곡 선택  /  SONG SELECT",
            titleStyle);

        if (songs.Count == 0)
        {
            DrawEmptyLobby(panel);
            return;
        }

        Rect listRect = new Rect(
            panel.x + 40.0f,
            panel.y + 128.0f,
            panel.width - 80.0f,
            panel.height - 210.0f);
        float rowHeight = 94.0f;
        float contentHeight = Mathf.Max(listRect.height, songs.Count * rowHeight);
        scrollPosition = GUI.BeginScrollView(
            listRect,
            scrollPosition,
            new Rect(0.0f, 0.0f, listRect.width - 20.0f, contentHeight));

        GUIStyle songButtonStyle = new GUIStyle(GUI.skin.button);
        songButtonStyle.alignment = TextAnchor.MiddleLeft;
        songButtonStyle.fontSize = Mathf.Max(18, Screen.height / 43);
        songButtonStyle.fontStyle = FontStyle.Bold;
        songButtonStyle.padding = new RectOffset(102, 150, 8, 8);
        songButtonStyle.normal.textColor = Cream;
        songButtonStyle.hover.textColor = Color.white;
        songButtonStyle.active.textColor = Cream;

        GUIStyle stateStyle = MakeStyle(
            Mathf.Max(12, Screen.height / 61),
            CreamDim,
            FontStyle.Bold,
            TextAnchor.MiddleRight);

        for (int i = 0; i < songs.Count; i++)
        {
            ChartAutoLoader.SongEntry song = songs[i];
            Rect row = new Rect(2.0f, i * rowHeight + 3.0f, listRect.width - 30.0f, 82.0f);

            GUI.backgroundColor = song.HasChart
                ? new Color(0.22f, 0.065f, 0.06f, 1.0f)
                : new Color(0.10f, 0.085f, 0.075f, 1.0f);

            if (GUI.Button(row, song.DisplayName, songButtonStyle))
            {
                controller.PlaySelectedSong(song);
            }

            Rect coverRect = new Rect(row.x + 8.0f, row.y + 7.0f, 68.0f, 68.0f);
            DrawAlbumCover(song.albumCover, coverRect);
            DrawRect(new Rect(row.x + 84.0f, row.y + 12.0f, 3.0f, row.height - 24.0f),
                song.HasChart ? DeepRed : CreamDim);

            GUI.Label(
                new Rect(row.xMax - 148.0f, row.y + 17.0f, 132.0f, 48.0f),
                song.HasChart ? "CHART READY" : "RANDOM NOTES",
                stateStyle);
        }

        GUI.backgroundColor = Color.white;
        GUI.EndScrollView();

        GUIStyle refreshStyle = new GUIStyle(GUI.skin.button);
        refreshStyle.fontSize = Mathf.Max(14, Screen.height / 55);
        refreshStyle.fontStyle = FontStyle.Bold;
        refreshStyle.normal.textColor = Cream;
        GUI.backgroundColor = DeepRed;
        if (GUI.Button(
            new Rect(panel.x + 44.0f, panel.yMax - 58.0f, 154.0f, 38.0f),
            "이전 화면으로",
            refreshStyle))
        {
            controller.ReturnToPreviousScene();
        }

        if (GUI.Button(
            new Rect(panel.xMax - 198.0f, panel.yMax - 58.0f, 154.0f, 38.0f),
            "목록 새로고침",
            refreshStyle))
        {
            controller.RefreshLobbySongs(wasOpenedFromPause);
        }
        GUI.backgroundColor = Color.white;
    }

    private void DrawEmptyLobby(Rect panel)
    {
        GUIStyle titleStyle = MakeStyle(
            Mathf.Max(23, Screen.height / 33),
            Cream,
            FontStyle.Bold,
            TextAnchor.MiddleCenter);
        GUIStyle guideStyle = MakeStyle(
            Mathf.Max(15, Screen.height / 52),
            CreamDim,
            FontStyle.Normal,
            TextAnchor.MiddleCenter);

        GUI.Label(
            new Rect(panel.x + 35.0f, panel.center.y - 75.0f, panel.width - 70.0f, 48.0f),
            "등록된 음악이 없습니다.",
            titleStyle);
        GUI.Label(
            new Rect(panel.x + 35.0f, panel.center.y - 28.0f, panel.width - 70.0f, 48.0f),
            "Resources/RhythmSongs에 OGG와 JSON을 추가해 주세요.",
            guideStyle);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = Mathf.Max(18, Screen.height / 42);
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.normal.textColor = Cream;
        GUI.backgroundColor = DeepRed;
        if (GUI.Button(
            new Rect(panel.center.x - 150.0f, panel.center.y + 55.0f, 300.0f, 56.0f),
            "랜덤 모드 시작",
            buttonStyle))
        {
            controller.PlayRandomWithoutMusic();
        }

        if (GUI.Button(
            new Rect(panel.center.x - 150.0f, panel.center.y + 125.0f, 300.0f, 46.0f),
            "이전 화면으로",
            buttonStyle))
        {
            controller.ReturnToPreviousScene();
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawAlbumCover(Texture2D texture, Rect rect)
    {
        DrawRect(new Rect(rect.x - 2.0f, rect.y - 2.0f, rect.width + 4.0f, rect.height + 4.0f),
            CreamDim);

        if (texture != null)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleAndCrop, true);
            return;
        }

        DrawRect(rect, InkSoft);
        DrawRect(new Rect(rect.x, rect.y, rect.width, 12.0f), DeepRed);

        GUIStyle fallbackStyle = MakeStyle(
            11,
            Cream,
            FontStyle.Bold,
            TextAnchor.MiddleCenter);
        GUI.Label(rect, "NO\nCOVER", fallbackStyle);
    }

    private static void DrawBackdropAccents()
    {
        DrawRect(new Rect(0.0f, Screen.height * 0.13f, Screen.width * 0.22f, 2.0f),
            new Color(DeepRed.r, DeepRed.g, DeepRed.b, 0.55f));
        DrawRect(new Rect(Screen.width * 0.80f, Screen.height * 0.84f,
            Screen.width * 0.20f, 2.0f),
            new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
    }

    private static GUIStyle MakeStyle(
        int size,
        Color color,
        FontStyle fontStyle,
        TextAnchor alignment)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = size;
        style.fontStyle = fontStyle;
        style.alignment = alignment;
        style.clipping = TextClipping.Clip;
        style.normal.textColor = color;
        return style;
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
