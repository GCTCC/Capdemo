using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Video;

// 타이틀 화면: 검은 화면에서 페이드 인한 뒤, 아무 키나 누르면 페이드 아웃하며 다음 씬으로 넘어간다.
// 튜토리얼 챕터(첫 곡까지) 미완료 → TutoScene (처음부터 다시), 완료 → HoloHubScene
public class TitleScreen : MonoBehaviour
{
    [SerializeField] private string tutorialSceneName = "TutoScene";
    [SerializeField] private string hubSceneName = "HoloHubScene";

    [Tooltip("페이드 인이 끝난 뒤 이 시간(초) 동안은 입력을 무시한다.")]
    [SerializeField, Min(0f)] private float inputDelay = 0.3f;

    [Tooltip("화면 전체를 덮는 검은 Image의 CanvasGroup. Canvas의 맨 마지막 자식(가장 위)에 둔다. 비워 두면 페이드 없이 바로 넘어간다.")]
    [FormerlySerializedAs("fadeGroup")]
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField, Min(0f)] private float fadeInDuration = 1f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.6f;

    [Tooltip("배경 일러스트를 보여 주는 Image. 비워 두면 씬에서 이름이 \"BackGround\"인 Image를 찾는다.\n" +
             "그림은 Tools > CapDemo > 일러스트 관리의 「타이틀 배경」에서 바꾼다 (비어 있으면 Image에 원래 들어 있던 그림 그대로)")]
    [SerializeField] private Image backgroundImage;

    private float readyTime = float.MaxValue;
    private bool started;

    private void Awake()
    {
        if (!backgroundImage)
        {
            var go = GameObject.Find("BackGround");
            if (go) backgroundImage = go.GetComponent<Image>();
        }
        if (backgroundImage)
            backgroundImage.sprite = IllustrationSet.FirstOr(IllustrationSet.Title, backgroundImage.sprite);

        var set = IllustrationSet.Load(IllustrationSet.Title);
        if (set && set.video && backgroundImage) StartBackgroundVideo(set.video);
    }

    // ── 배경 동영상 ──
    // BackGround Image 위에 같은 크기의 RawImage를 깔고, 영상 첫 장면이 나온 뒤에 보이게 한다 (그 전엔 그림이 보임)
    private VideoPlayer videoPlayer;
    private RenderTexture videoTexture;
    private RawImage videoImage;

    private void StartBackgroundVideo(VideoClip clip)
    {
        var go = new GameObject("Background Video", typeof(RectTransform), typeof(RawImage), typeof(VideoPlayer));
        var rect = (RectTransform)go.transform;
        rect.SetParent(backgroundImage.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        videoImage = go.GetComponent<RawImage>();
        videoImage.raycastTarget = false;
        videoImage.enabled = false;

        videoTexture = new RenderTexture((int)clip.width, (int)clip.height, 0);
        videoImage.texture = videoTexture;

        videoPlayer = go.GetComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.clip = clip;
        videoPlayer.isLooping = true;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoTexture;
        videoPlayer.sendFrameReadyEvents = true;
        videoPlayer.frameReady += OnFirstVideoFrame;
        videoPlayer.prepareCompleted += p => p.Play();
        videoPlayer.Prepare();
    }

    private void OnFirstVideoFrame(VideoPlayer player, long frame)
    {
        videoImage.enabled = true;
        player.frameReady -= OnFirstVideoFrame;
        player.sendFrameReadyEvents = false;
    }

    private void OnDestroy()
    {
        if (videoTexture) videoTexture.Release();
        if (videoTexture) Destroy(videoTexture);
    }

    private IEnumerator Start()
    {
        // 검은 화면(알파 1)에서 시작해 투명(알파 0)으로
        yield return Fade(1f, 0f, fadeInDuration);
        readyTime = Time.unscaledTime + inputDelay;
    }

    private void Update()
    {
        if (started || Time.unscaledTime < readyTime || !AnyInputPressed()) return;
        started = true;
        StartCoroutine(StartGame());
    }

    private static bool AnyInputPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;

        var gamepad = Gamepad.current;
        if (gamepad != null)
            foreach (var control in gamepad.allControls)
                if (control is ButtonControl button && button.wasPressedThisFrame) return true;

        return false;
    }

    private IEnumerator StartGame()
    {
        bool tutorialCleared = SaveData.IsTutorialCleared();
        string next = tutorialCleared ? hubSceneName : tutorialSceneName;
        if (!Application.CanStreamedLevelBeLoaded(next))
        {
            Debug.LogError($"[TitleScreen] '{next}' 씬이 Build Profiles 씬 목록에 없습니다.");
            started = false;
            yield break;
        }

        if (!tutorialCleared)
        {
            // 튜토리얼 챕터는 중간 저장이 없다. 중간에 껐다 켜면 이름·NPC 첫 만남까지 지우고 처음부터 한다
            SaveData.ResetProgress();
            TutorialRun.Step = TutorialStep.Prologue;
        }
        else TutorialRun.Step = TutorialStep.Done;

        // 페이드하는 동안 다음 씬을 미리 불러 둔다
        var load = SceneManager.LoadSceneAsync(next);
        load.allowSceneActivation = false;

        // 투명(알파 0)에서 검은 화면(알파 1)으로
        yield return Fade(fadeOverlay ? fadeOverlay.alpha : 0f, 1f, fadeOutDuration);

        load.allowSceneActivation = true;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (!fadeOverlay) yield break;

        // 검은 화면이 보이는 동안은 뒤의 UI 클릭을 막는다
        fadeOverlay.blocksRaycasts = true;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            fadeOverlay.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        fadeOverlay.alpha = to;
        fadeOverlay.blocksRaycasts = to > 0f;
    }
}
