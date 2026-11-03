using System.Collections;
using UnityEngine;

// 앨범커버 소개, 검은 화면 전환과 플레이필드 페이드 인·아웃을 담당한다.
[DisallowMultipleComponent]
public class RhythmSceneTransition : MonoBehaviour
{
    [Header("Song Intro")]
    [SerializeField, Min(0.0f)] private float coverFadeInDuration = 0.55f;
    [SerializeField, Min(0.0f)] private float coverHoldDuration = 0.90f;
    [SerializeField, Min(0.0f)] private float coverFadeOutDuration = 0.70f;
    [SerializeField, Min(0.0f)] private float playFieldFadeInDuration = 1.00f;
    [SerializeField, Min(0.0f)] private float sceneFadeDuration = 0.75f;

    private static readonly Color Ink = new Color(0.018f, 0.016f, 0.015f, 1.0f);
    private static readonly Color Cream = new Color(0.94f, 0.88f, 0.75f, 1.0f);
    private static readonly Color DeepRed = new Color(0.55f, 0.075f, 0.08f, 1.0f);
    private static readonly Color Cyan = new Color(0.15f, 0.88f, 0.88f, 1.0f);

    private bool isVisible;
    private bool showCover;
    private bool isBusy;
    private float blackAlpha;
    private float coverAlpha;
    private float coverScale = 1.0f;
    private Texture2D coverTexture;
    private string songTitle = "";
    private string modeLabel = "";

    public bool IsBusy
    {
        get { return isBusy; }
    }

    private void Awake()
    {
        // 씬이 처음 렌더링될 때 한 프레임 번쩍이지 않도록 검은 화면에서 시작한다.
        isVisible = true;
        blackAlpha = 1.0f;
    }

    public IEnumerator RevealScene()
    {
        if (isBusy)
        {
            yield break;
        }

        isBusy = true;
        isVisible = true;
        showCover = false;
        blackAlpha = 1.0f;
        yield return FadeBlack(1.0f, 0.0f, sceneFadeDuration);
        isVisible = false;
        isBusy = false;
    }

    public IEnumerator PlaySongIntro(
        Texture2D albumCover,
        string title,
        string entryModeLabel,
        System.Action revealGameplay)
    {
        if (isBusy)
        {
            yield break;
        }

        isBusy = true;
        isVisible = true;
        showCover = true;
        blackAlpha = 1.0f;
        coverAlpha = 0.0f;
        coverScale = 1.06f;
        coverTexture = albumCover;
        songTitle = string.IsNullOrWhiteSpace(title) ? "UNKNOWN TRACK" : title;
        modeLabel = string.IsNullOrWhiteSpace(entryModeLabel)
            ? "RHYTHM SESSION"
            : entryModeLabel;

        yield return AnimateCover(0.0f, 1.0f, 1.06f, 1.0f, coverFadeInDuration);

        if (coverHoldDuration > 0.0f)
        {
            float elapsed = 0.0f;
            while (elapsed < coverHoldDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        yield return AnimateCover(1.0f, 0.0f, 1.0f, 0.97f, coverFadeOutDuration);
        showCover = false;

        if (revealGameplay != null)
        {
            revealGameplay();
        }

        // 플레이필드가 준비된 다음 프레임부터 검은 막을 걷는다.
        yield return null;
        yield return FadeBlack(1.0f, 0.0f, playFieldFadeInDuration);

        isVisible = false;
        isBusy = false;
    }

    public IEnumerator FadeToBlack()
    {
        if (isBusy)
        {
            yield break;
        }

        isBusy = true;
        isVisible = true;
        showCover = false;
        blackAlpha = 0.0f;
        yield return FadeBlack(0.0f, 1.0f, sceneFadeDuration);
        isBusy = false;
    }

    private IEnumerator FadeBlack(float from, float to, float duration)
    {
        if (duration <= 0.0f)
        {
            blackAlpha = to;
            yield break;
        }

        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            blackAlpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        blackAlpha = to;
    }

    private IEnumerator AnimateCover(
        float fromAlpha,
        float toAlpha,
        float fromScale,
        float toScale,
        float duration)
    {
        if (duration <= 0.0f)
        {
            coverAlpha = toAlpha;
            coverScale = toScale;
            yield break;
        }

        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = 1.0f - Mathf.Pow(1.0f - normalized, 3.0f);
            coverAlpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
            coverScale = Mathf.Lerp(fromScale, toScale, eased);
            yield return null;
        }

        coverAlpha = toAlpha;
        coverScale = toScale;
    }

    private void OnGUI()
    {
        if (!isVisible)
        {
            return;
        }

        // 모든 IMGUI 메뉴보다 앞에 표시한다.
        GUI.depth = -1000;
        DrawRect(
            new Rect(0.0f, 0.0f, Screen.width, Screen.height),
            new Color(Ink.r, Ink.g, Ink.b, blackAlpha));

        if (!showCover || coverAlpha <= 0.001f)
        {
            return;
        }

        DrawCoverPresentation();
    }

    private void DrawCoverPresentation()
    {
        float baseSize = Mathf.Min(Screen.width, Screen.height) * 0.45f;
        float size = baseSize * coverScale;
        Rect coverRect = new Rect(
            (Screen.width - size) * 0.5f,
            (Screen.height - size) * 0.5f - Screen.height * 0.045f,
            size,
            size);

        Color oldColor = GUI.color;
        GUI.color = new Color(1.0f, 1.0f, 1.0f, coverAlpha);

        DrawRect(
            new Rect(coverRect.x - 12.0f, coverRect.y - 12.0f,
                coverRect.width + 24.0f, coverRect.height + 24.0f),
            new Color(DeepRed.r, DeepRed.g, DeepRed.b, 0.85f * coverAlpha));
        DrawRect(
            new Rect(coverRect.x - 4.0f, coverRect.y - 4.0f,
                coverRect.width + 8.0f, coverRect.height + 8.0f),
            new Color(Cream.r, Cream.g, Cream.b, 0.92f * coverAlpha));

        if (coverTexture != null)
        {
            GUI.DrawTexture(coverRect, coverTexture, ScaleMode.ScaleAndCrop, true);
        }
        else
        {
            DrawFallbackCover(coverRect);
        }

        GUIStyle modeStyle = new GUIStyle(GUI.skin.label);
        modeStyle.alignment = TextAnchor.MiddleCenter;
        modeStyle.fontSize = Mathf.Max(13, Screen.height / 58);
        modeStyle.fontStyle = FontStyle.Bold;
        modeStyle.normal.textColor = new Color(Cyan.r, Cyan.g, Cyan.b, coverAlpha);

        GUIStyle titleStyle = new GUIStyle(modeStyle);
        titleStyle.fontSize = Mathf.Max(24, Screen.height / 29);
        titleStyle.normal.textColor = new Color(Cream.r, Cream.g, Cream.b, coverAlpha);

        GUI.Label(
            new Rect(0.0f, coverRect.yMax + 24.0f, Screen.width, 28.0f),
            modeLabel,
            modeStyle);
        GUI.Label(
            new Rect(Screen.width * 0.10f, coverRect.yMax + 50.0f,
                Screen.width * 0.80f, 55.0f),
            songTitle,
            titleStyle);

        GUI.color = oldColor;
    }

    private void DrawFallbackCover(Rect rect)
    {
        DrawRect(rect, new Color(0.035f, 0.03f, 0.028f, coverAlpha));
        DrawRect(
            new Rect(rect.x, rect.y, rect.width, rect.height * 0.22f),
            new Color(DeepRed.r, DeepRed.g, DeepRed.b, coverAlpha));

        for (int i = 0; i < 7; i++)
        {
            float width = rect.width * (0.20f + i * 0.07f);
            DrawRect(
                new Rect(rect.x + rect.width * 0.10f,
                    rect.y + rect.height * (0.34f + i * 0.055f),
                    width,
                    2.0f),
                new Color(Cream.r, Cream.g, Cream.b, 0.45f * coverAlpha));
        }

        GUIStyle fallbackStyle = new GUIStyle(GUI.skin.label);
        fallbackStyle.alignment = TextAnchor.MiddleCenter;
        fallbackStyle.fontSize = Mathf.Max(22, Screen.height / 30);
        fallbackStyle.fontStyle = FontStyle.Bold;
        fallbackStyle.normal.textColor = new Color(Cream.r, Cream.g, Cream.b, coverAlpha);
        GUI.Label(rect, "NO COVER\n// " + songTitle, fallbackStyle);
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
