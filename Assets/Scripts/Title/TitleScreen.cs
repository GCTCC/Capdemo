using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// 타이틀 화면: 검은 화면에서 페이드 인한 뒤, 아무 키나 누르면 페이드 아웃하며 다음 씬으로 넘어간다.
// 튜토리얼 미완료 → TutoScene, 완료 → HubScene
public class TitleScreen : MonoBehaviour
{
    [SerializeField] private string tutorialSceneName = "TutoScene";
    [SerializeField] private string hubSceneName = "HubScene";

    [Tooltip("페이드 인이 끝난 뒤 이 시간(초) 동안은 입력을 무시한다.")]
    [SerializeField, Min(0f)] private float inputDelay = 0.3f;

    [Tooltip("화면 전체를 덮는 검은 Image의 CanvasGroup. Canvas의 맨 마지막 자식(가장 위)에 둔다. 비워 두면 페이드 없이 바로 넘어간다.")]
    [FormerlySerializedAs("fadeGroup")]
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField, Min(0f)] private float fadeInDuration = 1f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.6f;

    private float readyTime = float.MaxValue;
    private bool started;

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
        string next = SaveData.IsTutorialCleared() ? hubSceneName : tutorialSceneName;
        if (!Application.CanStreamedLevelBeLoaded(next))
        {
            Debug.LogError($"[TitleScreen] '{next}' 씬이 Build Settings에 없습니다.");
            started = false;
            yield break;
        }

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
