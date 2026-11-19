using UnityEngine;

// 화면 위에 지직거리며 열리고 닫히는 홀로그램 창의 공통 부분 (설정 창, 일시정지 창이 물려받는다).
// 창 프리팹 맨 위에는 따로 뜨는 Canvas가 있어서, 어느 씬에 넣어도 1920 x 1080 기준으로 같은 크기로 뜬다.
// 열려 있는 동안 AnyOpen이 true → 허브 메뉴·대사 넘기기 같은 뒤쪽 입력은 이걸 보고 멈춘다.
public abstract class HoloPopup : MonoBehaviour
{
    // 지금 열려 있는 창 수
    public static int OpenCount { get; private set; }
    public static bool AnyOpen => OpenCount > 0;

    [Tooltip("창 전체 투명도")]
    [SerializeField] protected CanvasGroup group;
    [Tooltip("지직거리며 펼쳐질 창 본체")]
    [SerializeField] protected RectTransform panel;
    [SerializeField] protected AudioClip openSfx;
    [SerializeField] protected AudioClip closeSfx;
    [Tooltip("열리고 닫히는 시간(초)")]
    [SerializeField, Min(0.01f)] protected float animTime = 0.18f;

    public bool IsOpen { get; private set; }

    private bool opening;
    private float anim; // 0 닫힘 ~ 1 열림
    private AudioSource sfx;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => OpenCount = 0;

    // 프리팹을 씬에 하나 만든다 (닫힌 상태). 같은 창을 여러 번 쓰려면 돌려받은 것을 들고 있다가 다시 Open한다
    public static T Spawn<T>(T prefab) where T : HoloPopup
    {
        if (!prefab) return null;
        var popup = Instantiate(prefab);
        popup.name = prefab.name;
        popup.HideNow();
        return popup;
    }

    protected virtual void Awake()
    {
        if (!group) group = GetComponent<CanvasGroup>();
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.ignoreListenerPause = true; // 게임이 일시정지돼도 창 소리는 난다
    }

    public void Open()
    {
        if (IsOpen || HoloTransition.IsBusy) return;
        HoloUI.EnsureEventSystem();
        gameObject.SetActive(true);
        IsOpen = true;
        OpenCount++;
        opening = true;
        anim = 0f;
        PlaySfx(openSfx);
        OnOpened();
    }

    public void Close()
    {
        if (!IsOpen || !opening) return;
        opening = false;
        PlaySfx(closeSfx);
        OnClosing();
    }

    // 애니메이션 없이 바로 닫는다 (씬을 넘길 때)
    public void HideNow()
    {
        if (IsOpen)
        {
            IsOpen = false;
            OpenCount = Mathf.Max(0, OpenCount - 1);
        }
        opening = false;
        anim = 0f;
        if (group) group.alpha = 0f;
        gameObject.SetActive(false);
    }

    protected virtual void OnOpened() { }
    protected virtual void OnClosing() { }
    protected virtual void OnClosed() { }
    // 완전히 열려 있는 동안 매 프레임 (키보드 처리)
    protected virtual void HandleKeys() { }

    protected void PlaySfx(AudioClip clip)
    {
        if (!clip || !sfx) return;
        sfx.PlayOneShot(clip, GameSettings.SfxVolume);
    }

    protected virtual void Update()
    {
        if (!IsOpen) return;

        // 지직거리며 열리고 닫힌다 (게임 종료 확인 창과 같은 움직임)
        anim = Mathf.MoveTowards(anim, opening ? 1f : 0f, Time.unscaledDeltaTime / animTime);
        float flicker = anim < 0.999f && Random.value < 0.35f ? Random.Range(0.3f, 0.8f) : 1f;
        if (group)
        {
            group.alpha = anim * flicker;
            group.blocksRaycasts = opening;
        }
        if (panel)
        {
            float sy = Mathf.Lerp(0.02f, 1f, 1f - (1f - anim) * (1f - anim));
            panel.localScale = new Vector3(Mathf.Lerp(0.9f, 1f, anim), sy, 1f);
            panel.anchoredPosition = anim < 0.999f && Random.value < 0.3f ? new Vector2(Random.Range(-8f, 8f), 0f) : Vector2.zero;
        }

        if (!opening && anim <= 0f)
        {
            HideNow();
            OnClosed();
            return;
        }
        if (opening && anim >= 1f) HandleKeys();
    }

    private void OnDestroy()
    {
        if (IsOpen) OpenCount = Mathf.Max(0, OpenCount - 1);
    }
}
