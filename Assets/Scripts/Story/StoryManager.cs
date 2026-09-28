using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

// StoryChapter를 처음부터 끝까지 재생한다.
// 컷마다 배경 일러스트를 바꾸고, 대사를 한 줄씩 보여 준다.
// 클릭/스페이스/엔터: 출력 중이면 대사를 바로 완성, 완성 상태면 다음 줄(컷의 마지막 줄이면 다음 컷)
public class StoryManager : MonoBehaviour
{
    [SerializeField] private StoryChapter chapter;
    [SerializeField] private IllustrationView illustration;
    [SerializeField] private DialogueBox dialogue;
    [Tooltip("이름 입력 줄이 없으면 비워 둬도 된다")]
    [SerializeField] private NameInputPanel nameInput;
    [SerializeField] private bool playOnStart = true;

    [Tooltip("화면 전체를 덮는 검은 Image의 CanvasGroup. 비워 두면 페이드 없이 진행한다.")]
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField, Min(0f)] private float fadeInDuration = 1f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 1f;
    [Tooltip("컷의 전환 시간이 0일 때 쓰는 기본 전환 시간(초)")]
    [SerializeField, Min(0f)] private float cutFadeDuration = 0.6f;

    [Tooltip("마지막 대사까지 끝나고 페이드 아웃한 뒤 호출된다")]
    [SerializeField] private UnityEvent onFinished;

    public bool IsPlaying { get; private set; }
    public UnityEvent OnFinished => onFinished;

    private void Start()
    {
        if (!playOnStart) return;
        if (chapter)
        {
            Play(chapter);
            return;
        }

        // 연결된 스토리가 없으면 검은 화면만 남으므로 걷어 내고 알린다
        Debug.LogWarning("[StoryManager] Chapter가 비어 있어 재생할 스토리가 없습니다. 인스펙터에서 스토리 에셋을 연결하세요.", this);
        if (fadeOverlay)
        {
            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = false;
        }
    }

    public void Play(StoryChapter target)
    {
        if (IsPlaying || !target) return;
        chapter = target;
        StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        IsPlaying = true;
        dialogue.Clear();

        var cuts = chapter.Cuts;
        if (cuts.Count > 0) illustration.ShowImmediate(TargetOf(cuts[0]));
        yield return Fade(1f, 0f, fadeInDuration);

        for (int c = 0; c < cuts.Count; c++)
        {
            var cut = cuts[c];
            if (c > 0 && TargetOf(cut) != illustration.Current)
            {
                dialogue.Clear();
                yield return Transition(cut);
            }

            // 대사가 없는 컷은 일러스트만 보여 주고 입력을 기다린다
            if (cut.Lines.Count == 0)
            {
                yield return WaitForAdvance();
                continue;
            }

            foreach (var line in cut.Lines)
                yield return PlayLine(line);
        }

        yield return Fade(fadeOverlay ? fadeOverlay.alpha : 0f, 1f, fadeOutDuration);
        IsPlaying = false;
        onFinished.Invoke();
    }

    // 이 컷에서 보여 줄 일러스트. 검은 화면이면 null, 비워 두었으면 지금 일러스트 유지
    private Sprite TargetOf(StoryCut cut)
    {
        if (cut.BlackScreen) return null;
        return cut.Illustration ? cut.Illustration : illustration.Current;
    }

    private IEnumerator Transition(StoryCut cut)
    {
        var target = TargetOf(cut);
        float duration = cut.TransitionDuration > 0f ? cut.TransitionDuration : cutFadeDuration;
        switch (cut.Transition)
        {
            case CutTransition.Crossfade:
                yield return illustration.Show(target, duration);
                break;
            case CutTransition.FadeToBlack:
                // 전환 시간의 절반은 어두워지고, 절반은 밝아진다
                yield return Fade(0f, 1f, duration * 0.5f);
                illustration.ShowImmediate(target);
                yield return Fade(1f, 0f, duration * 0.5f);
                break;
            case CutTransition.Cut:
                illustration.ShowImmediate(target);
                break;
        }
    }

    private IEnumerator PlayLine(StoryLine line)
    {
        dialogue.Show(line);
        yield return null; // 이전 줄을 넘긴 입력이 이번 줄을 바로 완성하지 않게 한 프레임 쉰다

        while (dialogue.IsTyping)
        {
            if (AdvancePressed()) dialogue.Complete();
            yield return null;
        }

        if (line.Pause == StoryPause.NameInput && nameInput)
        {
            yield return nameInput.Ask();
            yield break; // 이름을 확정한 엔터로 다음 줄이 넘어가지 않게, 입력을 기다리지 않고 바로 다음 줄로
        }

        yield return WaitForAdvance();
    }

    private IEnumerator WaitForAdvance()
    {
        yield return null;
        while (!AdvancePressed()) yield return null;
    }

    private static bool AdvancePressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
                                 keyboard.numpadEnterKey.wasPressedThisFrame))
            return true;

        var mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (!fadeOverlay) yield break;

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
