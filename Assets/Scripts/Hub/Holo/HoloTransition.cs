using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 홀로그램 허브의 "지직" 씬 전환.
//   1) 지금 화면을 사진으로 찍어 지직거리게 한다
//   2) 옛날 TV처럼 가로줄로 접히고 → 점으로 사라진다
//   3) 검은 화면에 파형 로고가 지직거리며 떴다가 (그동안 다음 씬을 불러온다)
//   4) 새 씬에서 가로줄이 생기고 위아래로 벌어지며 켜진다
// 씬에 따로 놓을 필요 없다: 처음 쓰일 때 스스로 화면 맨 위에 만들어지고, 씬이 바뀌어도 남는다.
// 사용: HoloTransition.LoadScene("SongSelectScene_v2");
// 맵 이동에 쓰는 검은 페이드(ScreenFader)와는 따로 동작하고, 둘 중 하나가 돌고 있으면 다른 쪽은 시작하지 않는다.
public class HoloTransition : MonoBehaviour
{
    private static HoloTransition instance;

    // 전환 중인지 (두 번 누르지 않게)
    public static bool IsBusy { get; private set; }

    private static readonly int OpenXId = Shader.PropertyToID("_OpenX");
    private static readonly int OpenYId = Shader.PropertyToID("_OpenY");
    private static readonly int GlitchId = Shader.PropertyToID("_Glitch");
    private static readonly int NoiseId = Shader.PropertyToID("_Noise");
    private static readonly int BrightId = Shader.PropertyToID("_Bright");
    private static readonly int UseTexId = Shader.PropertyToID("_UseTex");

    private HoloTransitionSettings settings;
    private Canvas canvas;
    private RawImage screen;
    private Material material;
    private AudioSource audioSource;

    private RectTransform logo;
    private CanvasGroup logoGroup;
    private RectTransform[] logoBars;
    private TMP_Text logoTitle, logoTitleRed, logoTitleCyan, logoSub;
    private float logoGlitch; // 0~1, 로고가 지직거리는 정도

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        instance = null;
        IsBusy = false;
        Leaving = null;
    }

    // ── 밖에서 부르는 것들 ──────────────────────────────────────────

    // 지직거리며 꺼졌다가 로고 → 다음 씬에서 켜진다. 다른 전환이 돌고 있어서 시작하지 못하면 false.
    // showLogo를 false로 주면 OTONE 로고 없이 꺼짐 → 켜짐만 한다 (곡 선택에서 허브로 돌아올 때 등).
    // 전환을 시작해 지금 씬을 떠날 때 알린다 (배경음 페이드아웃 등). SceneBgm이 듣는다
    public static event System.Action Leaving;

    public static bool LoadScene(string sceneName, bool showLogo = true)
    {
        if (IsBusy || ScreenFader.IsBusy) return false;
        Leaving?.Invoke();
        Instance.StartCoroutine(Instance.TransitionRoutine(sceneName, showLogo));
        return true;
    }

    // 스토리 모드 전용 전환: 화면이 잠깐 지직 → 위아래 검은 막이 영화처럼 닫힘 →
    // 검은 화면에 "프롤로그" 와 부제가 타자기처럼 찍힘 (그동안 다음 씬을 불러온다) → 새 씬에서 막이 천천히 열린다.
    public static bool LoadStory(string sceneName)
    {
        if (IsBusy || ScreenFader.IsBusy) return false;
        Leaving?.Invoke();
        Instance.StartCoroutine(Instance.StoryRoutine(sceneName));
        return true;
    }

    // 씬을 처음 열었을 때, 꺼진 화면이 지직거리며 켜지는 연출만 (허브에 바로 들어왔을 때 등)
    public static void PlayBootIn()
    {
        if (IsBusy || ScreenFader.IsBusy) return;
        Instance.StartCoroutine(Instance.BootInRoutine());
    }

    // 지직거리며 꺼진 뒤 게임을 끈다 (에디터에서는 Play를 멈춘다)
    public static void QuitGame()
    {
        if (IsBusy || ScreenFader.IsBusy) return;
        Leaving?.Invoke();
        Instance.StartCoroutine(Instance.QuitRoutine());
    }

    // ── 만들기 ─────────────────────────────────────────────────────

    private static HoloTransition Instance
    {
        get
        {
            if (instance) return instance;
            var go = new GameObject("HoloTransition");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HoloTransition>();
            instance.Build();
            return instance;
        }
    }

    private void Build()
    {
        settings = Resources.Load<HoloTransitionSettings>("HoloTransitionSettings");
        if (!settings)
        {
            settings = ScriptableObject.CreateInstance<HoloTransitionSettings>();
            Debug.LogWarning("[HoloTransition] Resources/HoloTransitionSettings가 없어 기본값으로 동작합니다. (Tools > CapDemo > 허브 메뉴로 만들 수 있어요)");
        }

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1100; // ScreenFader(1000)보다도 위
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>(); // 전환 중에는 클릭을 막는다
        canvas.enabled = false;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // 화면 전체를 덮는 판
        var screenGo = new GameObject("Screen", typeof(RectTransform), typeof(RawImage));
        screenGo.transform.SetParent(transform, false);
        Stretch((RectTransform)screenGo.transform);
        screen = screenGo.GetComponent<RawImage>();
        var source = settings.screenMaterial;
        if (!source)
        {
            var shader = Shader.Find("CapDemo/UI/HoloTransition");
            if (shader) source = new Material(shader);
        }
        if (source)
        {
            material = new Material(source);
            screen.material = material;
        }
        else
        {
            Debug.LogWarning("[HoloTransition] HoloTransition 셰이더를 찾지 못해 검은 화면으로만 전환합니다.");
            screen.color = Color.black;
        }

        BuildLogo();
    }

    private void BuildLogo()
    {
        var logoGo = new GameObject("Logo", typeof(RectTransform), typeof(CanvasGroup));
        logoGo.transform.SetParent(transform, false);
        logo = (RectTransform)logoGo.transform;
        logo.sizeDelta = new Vector2(900, 260);
        logoGroup = logoGo.GetComponent<CanvasGroup>();
        logoGroup.alpha = 0f;
        logoGroup.blocksRaycasts = false;

        // 파형 막대 9개
        logoBars = new RectTransform[9];
        for (int i = 0; i < logoBars.Length; i++)
        {
            var bar = new GameObject("Bar" + i, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(logo, false);
            var rt = (RectTransform)bar.transform;
            rt.sizeDelta = new Vector2(7, 30);
            rt.anchoredPosition = new Vector2(-250 + i * 13, 34);
            var img = bar.GetComponent<Image>();
            img.color = HoloPalette.Teal;
            img.raycastTarget = false;
            logoBars[i] = rt;
        }

        logoTitleRed = MakeLogoText("TitleRed", settings.titleFont, 92, new Color(1f, 0.27f, 0.43f, 0.55f), new Vector2(60, 34));
        logoTitleCyan = MakeLogoText("TitleCyan", settings.titleFont, 92, HoloPalette.Holo.WithAlpha(0.55f), new Vector2(60, 34));
        logoTitle = MakeLogoText("Title", settings.titleFont, 92, HoloPalette.Ink, new Vector2(60, 34));
        logoTitle.text = logoTitleRed.text = logoTitleCyan.text = settings.logoTitle;
        logoSub = MakeLogoText("Sub", settings.monoFont, 22, HoloPalette.Teal, new Vector2(0, -48));
        logoSub.characterSpacing = 18f;
        logoSub.text = settings.logoSub;

        // 제목 길이에 맞춰 [파형 막대 + 간격 + 제목] 묶음이 화면 가운데 오도록 자리를 잡는다
        logoTitle.ForceMeshUpdate();
        float titleW = logoTitle.preferredWidth;
        const float barStep = 13f, barsW = 13f * 8 + 7f, gap = 30f;
        float left = -(barsW + gap + titleW) * 0.5f;
        for (int i = 0; i < logoBars.Length; i++)
            logoBars[i].anchoredPosition = new Vector2(left + 3.5f + i * barStep, 34);
        titleX = left + barsW + gap + titleW * 0.5f;
        logoTitle.rectTransform.anchoredPosition = new Vector2(titleX, 34);
        logoTitleRed.rectTransform.anchoredPosition = new Vector2(titleX, 34);
        logoTitleCyan.rectTransform.anchoredPosition = new Vector2(titleX, 34);
    }

    private float titleX = 60f;

    private TMP_Text MakeLogoText(string name, TMP_FontAsset font, float size, Color color, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(logo, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(900, 120);
        rt.anchoredPosition = pos;
        var text = go.GetComponent<TextMeshProUGUI>();
        if (font) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.characterSpacing = 6f;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ── 흐름 ──────────────────────────────────────────────────────

    private IEnumerator TransitionRoutine(string sceneName, bool showLogo)
    {
        IsBusy = true;
        yield return TurnOff();

        // (로고가 있으면) 로고를 띄운 채로 다음 씬을 불러온다
        if (showLogo) yield return LogoIn();
        var load = SceneManager.LoadSceneAsync(sceneName);
        if (load == null)
        {
            Debug.LogWarning($"[HoloTransition] '{sceneName}' 씬을 열 수 없습니다. 이름과 Build Settings(빌드 프로필 씬 목록)를 확인하세요.");
            if (showLogo) yield return LogoOut();
            yield return TurnOn();
            Finish();
            yield break;
        }
        load.allowSceneActivation = false;
        float minHold = showLogo ? settings.logoHoldTime : 0.12f; // 로고 없으면 검은 화면은 잠깐만
        float shown = 0f;
        while (shown < minHold || load.progress < 0.9f)
        {
            shown += Time.unscaledDeltaTime;
            yield return null;
        }
        load.allowSceneActivation = true;
        while (!load.isDone) yield return null;
        yield return null; // 새 씬의 Start가 한 번 돈 뒤에 켠다

        if (showLogo) yield return LogoOut();
        yield return TurnOn();
        Finish();
    }

    // ── 스토리 전환 ────────────────────────────────────────────────

    private RectTransform storyRoot;
    private RectTransform barTop, barBottom;
    private CanvasGroup cardGroup;
    private TMP_Text storyLabelText, storyTitleText, storySubText;
    private RectTransform storyLine;

    private void BuildStory()
    {
        if (storyRoot) return;
        var rootGo = new GameObject("Story", typeof(RectTransform));
        rootGo.transform.SetParent(transform, false);
        storyRoot = (RectTransform)rootGo.transform;
        Stretch(storyRoot);

        barTop = MakeBar("BarTop", true);
        barBottom = MakeBar("BarBottom", false);

        var cardGo = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup));
        cardGo.transform.SetParent(storyRoot, false);
        var card = (RectTransform)cardGo.transform;
        card.sizeDelta = new Vector2(1400, 400);
        cardGroup = cardGo.GetComponent<CanvasGroup>();
        cardGroup.alpha = 0f;
        cardGroup.blocksRaycasts = false;

        storyLabelText = MakeStoryText(card, "Label", settings.monoFont, 22, HoloPalette.Teal, new Vector2(0, 92));
        storyLabelText.characterSpacing = 16f;
        var lineGo = new GameObject("Line", typeof(RectTransform), typeof(Image));
        lineGo.transform.SetParent(card, false);
        storyLine = (RectTransform)lineGo.transform;
        storyLine.sizeDelta = new Vector2(0, 1.5f);
        storyLine.anchoredPosition = new Vector2(0, 66);
        var lineImg = lineGo.GetComponent<Image>();
        lineImg.color = HoloPalette.Teal.WithAlpha(0.7f);
        lineImg.raycastTarget = false;
        storyTitleText = MakeStoryText(card, "Title", settings.titleFont, 84, HoloPalette.Ink, new Vector2(0, 4));
        storyTitleText.characterSpacing = 10f;
        storySubText = MakeStoryText(card, "Sub", settings.monoFont, 30, HoloPalette.Ink.WithAlpha(0.75f), new Vector2(0, -78));
        storySubText.characterSpacing = 4f;
    }

    // 위(또는 아래)에서 내려오는 검은 막. 안쪽 끝에 민트색 가는 선이 붙어 있다.
    private RectTransform MakeBar(string name, bool top)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(storyRoot, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(0f, 0f);
        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = true; // 막이 덮여 있는 동안 클릭을 막는다

        var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        edge.transform.SetParent(rt, false);
        var ert = (RectTransform)edge.transform;
        ert.anchorMin = new Vector2(0f, top ? 0f : 1f);
        ert.anchorMax = new Vector2(1f, top ? 0f : 1f);
        ert.pivot = new Vector2(0.5f, 0.5f);
        ert.sizeDelta = new Vector2(0f, 2f);
        var eimg = edge.GetComponent<Image>();
        eimg.color = HoloPalette.Teal.WithAlpha(0.8f);
        eimg.raycastTarget = false;
        return rt;
    }

    private TMP_Text MakeStoryText(RectTransform parent, string name, TMP_FontAsset font, float size, Color color, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(1400, 110);
        rt.anchoredPosition = pos;
        var text = go.GetComponent<TextMeshProUGUI>();
        if (font) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private void SetBars(float k) // 0 = 열림, 1 = 화면 전체를 덮음
    {
        float half = ((RectTransform)transform).rect.height * 0.5f + 4f;
        barTop.sizeDelta = new Vector2(0f, half * k);
        barBottom.sizeDelta = new Vector2(0f, half * k);
    }

    private static float EaseInOut(float k) => k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) * 0.5f;

    private IEnumerator StoryRoutine(string sceneName)
    {
        IsBusy = true;
        BuildStory();
        storyRoot.gameObject.SetActive(true);
        SetBars(0f);
        cardGroup.alpha = 0f;

        // 1) 지금 화면을 찍어 두고, 잠깐 지직 (신호가 바뀌는 느낌)
        canvas.enabled = false;
        yield return new WaitForEndOfFrame();
        var shot = ScreenCapture.CaptureScreenshotAsTexture();
        screen.texture = shot;
        SetScreen(useTex: 1f, openX: 1f, openY: 1f, glitch: 0.8f, bright: 0.25f, noise: 0.3f);
        canvas.enabled = true;
        Play(settings.offSound);
        var screenRect = screen.rectTransform;
        for (float t = 0f; t < 0.14f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.14f;
            Set(GlitchId, Mathf.Lerp(0.8f, 0f, k) * (Random.value < 0.8f ? 1f : 0.2f));
            Set(NoiseId, Mathf.Lerp(0.3f, 0f, k));
            Set(BrightId, Mathf.Lerp(0.25f, 0f, k));
            yield return null;
        }
        SetScreen(useTex: 1f, openX: 1f, openY: 1f, glitch: 0f, bright: 0f, noise: 0f);

        // 2) 화면이 천천히 다가오며 위아래 검은 막이 닫힌다
        float close = Mathf.Max(0.0001f, settings.storyCloseTime);
        for (float t = 0f; t < close; t += Time.unscaledDeltaTime)
        {
            float k = t / close;
            SetBars(EaseInOut(k));
            screenRect.localScale = Vector3.one * Mathf.Lerp(1f, 1.08f, k);
            yield return null;
        }
        SetBars(1f);
        screenRect.localScale = Vector3.one;
        screen.texture = null;
        Destroy(shot);
        screen.enabled = false; // 이제부터는 검은 막이 화면을 덮는다

        // 3) 검은 화면에 제목 카드. 그동안 다음 씬을 불러온다
        var load = SceneManager.LoadSceneAsync(sceneName);
        if (load != null) load.allowSceneActivation = false;
        else Debug.LogWarning($"[HoloTransition] '{sceneName}' 씬을 열 수 없습니다. 이름과 빌드 프로필 씬 목록을 확인하세요.");

        storyLabelText.text = settings.storyLabel;
        storyTitleText.text = settings.storyTitle;
        storySubText.text = "";
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.5f;
            cardGroup.alpha = Random.value < 0.3f + k ? k : k * 0.3f; // 살짝 깜빡이며 켜짐
            storyLine.sizeDelta = new Vector2(Mathf.Lerp(0f, 520f, EaseInOut(k)), 1.5f);
            yield return null;
        }
        cardGroup.alpha = 1f;
        storyLine.sizeDelta = new Vector2(520f, 1.5f);

        // 부제가 한 글자씩 찍힌다 (끝에 커서 깜빡임)
        string sub = settings.storySubtitle ?? "";
        for (int i = 1; i <= sub.Length; i++)
        {
            storySubText.text = sub.Substring(0, i) + "<alpha=#AA>_";
            yield return new WaitForSecondsRealtime(sub[i - 1] == ' ' ? settings.storyTypeSpeed * 0.5f : settings.storyTypeSpeed);
        }
        float hold = 0f;
        while (hold < settings.storyHoldTime || (load != null && load.progress < 0.9f))
        {
            hold += Time.unscaledDeltaTime;
            bool cursorOn = (int)(Time.unscaledTime * 2.5f) % 2 == 0;
            storySubText.text = sub + (cursorOn ? "<alpha=#AA>_" : "<alpha=#00>_");
            yield return null;
        }

        // 카드가 사라진다
        for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
        {
            cardGroup.alpha = 1f - t / 0.45f;
            yield return null;
        }
        cardGroup.alpha = 0f;

        if (load != null)
        {
            load.allowSceneActivation = true;
            while (!load.isDone) yield return null;
        }
        yield return null; // 새 씬의 Start가 한 번 돈 뒤에 연다
        yield return new WaitForSecondsRealtime(0.15f);

        // 4) 새 씬에서 막이 위아래로 천천히 열린다
        float open = Mathf.Max(0.0001f, settings.storyOpenTime);
        for (float t = 0f; t < open; t += Time.unscaledDeltaTime)
        {
            float k = t / open;
            SetBars(1f - (1f - Mathf.Pow(1f - k, 3f)));
            yield return null;
        }
        SetBars(0f);

        storyRoot.gameObject.SetActive(false);
        screen.enabled = true;
        SetScreen(useTex: 0f, openX: 1f, openY: 1f, glitch: 0f, bright: 0f, noise: 0f);
        Finish();
    }

    private IEnumerator BootInRoutine()
    {
        IsBusy = true;
        canvas.enabled = true;
        SetScreen(useTex: 0f, openX: 0f, openY: 0f, glitch: 0f, bright: 0f, noise: 0f);
        yield return new WaitForSecondsRealtime(0.12f);
        yield return TurnOn();
        Finish();
    }

    private IEnumerator QuitRoutine()
    {
        IsBusy = true;
        yield return TurnOff();
        yield return new WaitForSecondsRealtime(0.25f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Finish()
    {
        canvas.enabled = false;
        logoGroup.alpha = 0f;
        IsBusy = false;
    }

    // 화면을 찍어 지직 → 가로줄로 접힘 → 점으로 사라짐
    private IEnumerator TurnOff()
    {
        canvas.enabled = false;
        yield return new WaitForEndOfFrame();
        var shot = ScreenCapture.CaptureScreenshotAsTexture();
        screen.texture = shot;
        SetScreen(useTex: 1f, openX: 1f, openY: 1f, glitch: 0f, bright: 0f, noise: 0f);
        canvas.enabled = true;
        Play(settings.offSound);

        // 흔들어도 가장자리가 비지 않게 화면 사진을 살짝 크게
        var screenRect = screen.rectTransform;
        screenRect.localScale = Vector3.one * 1.05f;

        // 1) 파밧! 번쩍이며 세게 흔들리고 지직
        for (float t = 0f; t < settings.offGlitchTime; t += Time.unscaledDeltaTime)
        {
            float k = t / Mathf.Max(0.0001f, settings.offGlitchTime);
            Shake(screenRect, settings.shakeStrength * (1f - k * 0.3f));
            Set(GlitchId, Mathf.Lerp(0.6f, 1f, k));
            Set(NoiseId, 0.25f + k * 0.25f);
            Set(BrightId, settings.flashStrength * (1f - k) * (Random.value < 0.5f ? 1f : 0.4f));
            yield return null;
        }

        // 2) 세로로 확 눌려서 가로줄이 된다 (흔들림은 잦아든다)
        for (float t = 0f; t < settings.offCollapseTime; t += Time.unscaledDeltaTime)
        {
            float k = t / Mathf.Max(0.0001f, settings.offCollapseTime);
            Shake(screenRect, settings.shakeStrength * 0.6f * (1f - k));
            Set(OpenYId, Mathf.Lerp(1f, 0f, k * k));
            Set(BrightId, k);
            yield return null;
        }
        Set(OpenYId, 0f);
        screenRect.anchoredPosition = Vector2.zero;
        screenRect.localScale = Vector3.one;

        // 3) 가로줄이 가운데 점으로 줄어든다
        for (float t = 0f; t < settings.offShrinkTime; t += Time.unscaledDeltaTime)
        {
            float k = t / Mathf.Max(0.0001f, settings.offShrinkTime);
            Set(OpenXId, Mathf.Lerp(1f, 0.004f, k * k));
            yield return null;
        }
        Set(OpenXId, 0f);

        screen.texture = null;
        Destroy(shot);
        SetScreen(useTex: 0f, openX: 0f, openY: 0f, glitch: 0f, bright: 0f, noise: 0f);
    }

    // 가운데 가로줄 → 위아래로 벌어지며 새 화면이 드러남 → 남은 지직거림이 잦아듦
    private IEnumerator TurnOn()
    {
        Play(settings.onSound);
        SetScreen(useTex: 0f, openX: 0f, openY: 0f, glitch: 1f, bright: 0f, noise: 0.4f);

        for (float t = 0f; t < settings.lineTime; t += Time.unscaledDeltaTime)
        {
            float k = t / Mathf.Max(0.0001f, settings.lineTime);
            Set(OpenXId, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        Set(OpenXId, 1f);

        float total = settings.openTime + settings.settleTime;
        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            float open = Mathf.Clamp01(t / Mathf.Max(0.0001f, settings.openTime));
            // 살짝 튀어 오르듯 벌어진다
            float e = 1f - Mathf.Pow(1f - open, 3f);
            if (open < 1f && Random.value < 0.18f) e *= Random.Range(0.75f, 1f); // 치직: 순간 덜 열림
            Set(OpenYId, e);

            float fade = Mathf.Clamp01(t / Mathf.Max(0.0001f, total));
            Set(GlitchId, Mathf.Lerp(1f, 0f, fade) * (Random.value < 0.85f ? 1f : 0.2f));
            Set(NoiseId, Mathf.Lerp(0.4f, 0f, fade * 1.5f));
            yield return null;
        }
        SetScreen(useTex: 0f, openX: 1f, openY: 1f, glitch: 0f, bright: 0f, noise: 0f);
    }

    private IEnumerator LogoIn()
    {
        logoGlitch = 1f;
        for (float t = 0f; t < settings.logoInTime; t += Time.unscaledDeltaTime)
        {
            float k = t / Mathf.Max(0.0001f, settings.logoInTime);
            logoGroup.alpha = Random.value < 0.35f + k * 0.6f ? Mathf.Lerp(0.4f, 1f, k) : 0.1f; // 깜빡이며 켜짐
            logoGlitch = 1f - k * 0.85f;
            yield return null;
        }
        logoGroup.alpha = 1f;
        logoGlitch = 0.15f;
    }

    private IEnumerator LogoOut()
    {
        for (float t = 0f; t < settings.logoOutTime; t += Time.unscaledDeltaTime)
        {
            float k = t / Mathf.Max(0.0001f, settings.logoOutTime);
            logoGlitch = 0.15f + k;
            logoGroup.alpha = Random.value < 0.6f - k * 0.5f ? 1f - k * 0.6f : 0f;
            yield return null;
        }
        logoGroup.alpha = 0f;
    }

    // 로고는 보일 때 항상 살아 움직인다 (파형 막대 출렁 + 지직)
    private void Update()
    {
        if (!canvas || !canvas.enabled || logoGroup.alpha <= 0f) return;
        float time = Time.unscaledTime;

        for (int i = 0; i < logoBars.Length; i++)
        {
            float n = Mathf.PerlinNoise(time * 3.2f + i * 1.7f, i * 0.37f);
            float wave = Mathf.Sin(time * 6f + i * 0.8f) * 0.5f + 0.5f;
            logoBars[i].sizeDelta = new Vector2(7, Mathf.Lerp(10f, 70f, n * 0.7f + wave * 0.3f));
        }

        // 지직: 로고 전체가 옆으로 튀고, 빨강·하늘 그림자가 벌어진다
        bool jump = Random.value < logoGlitch * 0.5f;
        logo.anchoredPosition = jump ? new Vector2(Random.Range(-14f, 14f) * logoGlitch, 0f) : Vector2.zero;
        float split = 2.5f + logoGlitch * 12f * (Random.value < 0.5f ? 1f : 0.3f);
        logoTitleRed.rectTransform.anchoredPosition = new Vector2(titleX - split, 34);
        logoTitleCyan.rectTransform.anchoredPosition = new Vector2(titleX + split, 34);

        // 아래 글자: 점이 하나씩 늘어난다
        int dots = (int)(time * 3f) % 4;
        logoSub.text = settings.logoSub + new string('.', dots) + new string(' ', 3 - dots);
    }

    // ── 도움 함수 ─────────────────────────────────────────────────

    private void SetScreen(float useTex, float openX, float openY, float glitch, float bright, float noise)
    {
        Set(UseTexId, useTex);
        Set(OpenXId, openX);
        Set(OpenYId, openY);
        Set(GlitchId, glitch);
        Set(BrightId, bright);
        Set(NoiseId, noise);
    }

    // 화면 사진을 이리저리 튕긴다 (가로로 더 세게)
    private static void Shake(RectTransform rt, float strength)
    {
        rt.anchoredPosition = new Vector2(Random.Range(-1f, 1f) * strength, Random.Range(-1f, 1f) * strength * 0.5f);
    }

    private void Set(int id, float value)
    {
        if (material) material.SetFloat(id, value);
    }

    private void Play(AudioClip clip)
    {
        if (clip) audioSource.PlayOneShot(clip, settings.volume * GameSettings.SfxVolume); // 설정 창 효과음 음량
    }
}
