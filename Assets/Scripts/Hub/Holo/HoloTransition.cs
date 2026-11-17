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
    }

    // ── 밖에서 부르는 것들 ──────────────────────────────────────────

    // 지직거리며 꺼졌다가 로고 → 다음 씬에서 켜진다. 다른 전환이 돌고 있어서 시작하지 못하면 false.
    // showLogo를 false로 주면 OTONE 로고 없이 꺼짐 → 켜짐만 한다 (곡 선택에서 허브로 돌아올 때 등).
    public static bool LoadScene(string sceneName, bool showLogo = true)
    {
        if (IsBusy || ScreenFader.IsBusy) return false;
        Instance.StartCoroutine(Instance.TransitionRoutine(sceneName, showLogo));
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
