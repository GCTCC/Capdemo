using System.Collections;
using System.Collections.Generic;
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
    [Tooltip("BGM·효과음 재생기. 비워 두면 실행할 때 이 오브젝트에 자동으로 붙인다")]
    [SerializeField] private StoryAudio storyAudio;
    [SerializeField] private bool playOnStart = true;

    [Tooltip("에디터 테스트용: 이 번호의 컷(Cuts의 Element 번호)부터 재생한다. 빌드한 게임에서는 항상 처음부터 재생한다.")]
    [SerializeField, Min(0)] private int startCut;

    [Tooltip("화면 전체를 덮는 검은 Image의 CanvasGroup. 비워 두면 페이드 없이 진행한다.")]
    [SerializeField] private CanvasGroup fadeOverlay;
    [SerializeField, Min(0f)] private float fadeInDuration = 1f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 1f;
    [Tooltip("컷의 전환 시간이 0일 때 쓰는 기본 전환 시간(초)")]
    [SerializeField, Min(0f)] private float cutFadeDuration = 0.6f;

    [Tooltip("장면이 끝나 화면이 어두워질 때 BGM도 같이 줄인다")]
    [SerializeField] private bool fadeOutBgmOnFinish = true;

    [Tooltip("마지막 대사까지 끝나고 페이드 아웃한 뒤 호출된다")]
    [SerializeField] private UnityEvent onFinished;

    public bool IsPlaying { get; private set; }
    public UnityEvent OnFinished => onFinished;
    public StoryChapter Chapter => chapter;
    public IllustrationView Illustration => illustration;
    public DialogueBox Dialogue => dialogue;
    public CanvasGroup FadeOverlay => fadeOverlay;
    public StoryAudio Audio => storyAudio;

    // 지금 흘러야 할 BGM과, Cut Count로 정한 BGM이 끝나는 컷 번호·그때 줄어드는 시간
    private AudioClip bgmClip;
    private float bgmVolume;
    private float bgmFadeIn;
    private int bgmLastCut = int.MaxValue;
    private float bgmStopFade;

    private void Awake()
    {
        if (storyAudio) return;
        storyAudio = GetComponent<StoryAudio>();
        if (!storyAudio) storyAudio = gameObject.AddComponent<StoryAudio>();
    }

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
        bgmClip = null;
        bgmLastCut = int.MaxValue;

        var cuts = chapter.Cuts;
        int first = FirstCut(cuts.Count);

        // 중간 컷부터 시작하면 건너뛴 대사들의 스탠딩 상태와, 그때 흐르고 있어야 할 BGM을 이어받는다
        for (int c = 0; c < first; c++)
        {
            foreach (var line in cuts[c].Lines)
                dialogue.ApplyStanding(line);
            ApplyCutBgm(c, cuts[c], play: false);
        }

        if (cuts.Count > 0)
        {
            var (sprite, framing) = chapter.ResolveIllustration(first);
            illustration.ShowImmediate(sprite, framing);
            ApplyCutBgm(first, cuts[first], play: true);
            if (first > 0 && bgmClip && storyAudio.CurrentBgm != bgmClip)
                storyAudio.PlayBgm(bgmClip, bgmVolume, bgmFadeIn);
            PlayCutSounds(cuts[first], CutSoundTiming.TransitionStart);
        }
        yield return Fade(1f, 0f, fadeInDuration);
        if (cuts.Count > 0) PlayCutSounds(cuts[first], CutSoundTiming.CutShown);

        for (int c = first; c < cuts.Count; c++)
        {
            var cut = cuts[c];
            if (c > first)
            {
                ApplyCutBgm(c, cut, play: true);
                PlayCutSounds(cut, CutSoundTiming.TransitionStart);
                var (sprite, framing) = chapter.ResolveIllustration(c);
                if (!illustration.IsShowing(sprite, framing))
                {
                    dialogue.Clear();
                    yield return Transition(cut, sprite, framing);
                }
                PlayCutSounds(cut, CutSoundTiming.CutShown);
            }

            // 대사가 없는 컷은 일러스트만 보여 주고 입력을 기다린다
            if (cut.Lines.Count == 0) yield return WaitForAdvance();

            foreach (var line in cut.Lines)
                yield return PlayLine(cut, line);

            storyAudio.StopLoops(cut);
        }

        if (fadeOutBgmOnFinish) storyAudio.StopBgm(fadeOutDuration);
        yield return Fade(fadeOverlay ? fadeOverlay.alpha : 0f, 1f, fadeOutDuration);
        storyAudio.StopAllLoops();
        IsPlaying = false;
        onFinished.Invoke();
    }

    // 컷 c에 들어설 때: Cut Count가 끝난 BGM을 멈추고, 이 컷의 BGM 지시를 따른다.
    // play가 false면 소리는 내지 않고 "지금 흘러야 할 곡"만 따라간다 (중간 컷부터 시작할 때)
    private void ApplyCutBgm(int c, StoryCut cut, bool play)
    {
        if (c > bgmLastCut)
        {
            if (play) storyAudio.StopBgm(bgmStopFade);
            bgmClip = null;
            bgmLastCut = int.MaxValue;
        }

        var bgm = cut.Bgm;
        if (bgm == null) return;
        switch (bgm.action)
        {
            case BgmAction.Play when bgm.clip:
                bgmClip = bgm.clip;
                bgmVolume = bgm.volume;
                bgmFadeIn = bgm.fadeIn;
                bgmLastCut = bgm.cutCount > 0 ? c + bgm.cutCount - 1 : int.MaxValue;
                bgmStopFade = bgm.fadeOut;
                if (play) storyAudio.PlayBgm(bgm.clip, bgm.volume, bgm.fadeIn);
                break;
            case BgmAction.Stop:
                bgmClip = null;
                bgmLastCut = int.MaxValue;
                if (play) storyAudio.StopBgm(bgm.fadeOut);
                break;
        }
    }

    private void PlayCutSounds(StoryCut cut, CutSoundTiming timing)
    {
        foreach (var sound in cut.Sounds)
            if (sound != null && sound.timing == timing)
                storyAudio.PlaySfx(sound.clip, sound.volume, sound.delay, sound.loop, cut);
    }

    private int FirstCut(int count)
    {
#if UNITY_EDITOR
        return count > 0 ? Mathf.Clamp(startCut, 0, count - 1) : 0;
#else
        return 0;
#endif
    }

    private IEnumerator Transition(StoryCut cut, Sprite target, IllustFraming framing)
    {
        float duration = cut.TransitionDuration > 0f ? cut.TransitionDuration : cutFadeDuration;
        switch (cut.Transition)
        {
            case CutTransition.Crossfade:
                yield return illustration.Show(target, framing, duration);
                break;
            case CutTransition.FadeToBlack:
                // 전환 시간의 절반은 어두워지고, 절반은 밝아진다
                yield return Fade(0f, 1f, duration * 0.5f);
                illustration.ShowImmediate(target, framing);
                yield return Fade(1f, 0f, duration * 0.5f);
                break;
            case CutTransition.Cut:
                illustration.ShowImmediate(target, framing);
                break;
        }
    }

    private IEnumerator PlayLine(StoryCut cut, StoryLine line)
    {
        dialogue.Show(line);

        // 글자·단어 타이밍 효과음은 그 글자가 화면에 나올 때 낸다
        var pending = new List<(LineSound sound, int at)>();
        foreach (var sound in line.Sounds)
        {
            if (sound == null) continue;
            switch (sound.timing)
            {
                case LineSoundTiming.LineStart:
                    PlayLineSound(cut, line, sound);
                    break;
                case LineSoundTiming.AtCharacter:
                    pending.Add((sound, Mathf.Max(sound.atCharacter, 1)));
                    break;
                case LineSoundTiming.AtText:
                    int index = dialogue.FindText(sound.atText);
                    if (index < 0)
                        Debug.LogWarning($"[StoryManager] 효과음 단어 '{sound.atText}'를 대사에서 찾지 못해 대사 시작에 냅니다.", this);
                    pending.Add((sound, index + 1));
                    break;
            }
        }
        PlayPendingSounds(cut, line, pending, dialogue.VisibleCharacters);
        yield return null; // 이전 줄을 넘긴 입력이 이번 줄을 바로 완성하지 않게 한 프레임 쉰다

        while (dialogue.IsTyping)
        {
            if (AdvancePressed()) dialogue.Complete();
            PlayPendingSounds(cut, line, pending, dialogue.VisibleCharacters);
            yield return null;
        }
        PlayPendingSounds(cut, line, pending, int.MaxValue); // 출력을 건너뛰었어도 남은 효과음은 낸다
        PlayLineSounds(cut, line, LineSoundTiming.TypingDone);

        // 이름 입력 줄은 이름을 확정한 엔터로 다음 줄이 넘어가지 않게, 입력을 기다리지 않고 바로 다음 줄로
        if (line.Pause == StoryPause.NameInput && nameInput) yield return nameInput.Ask();
        else yield return WaitForAdvance();

        PlayLineSounds(cut, line, LineSoundTiming.OnNext);
        storyAudio.StopLoops(line);
    }

    private void PlayPendingSounds(StoryCut cut, StoryLine line, List<(LineSound sound, int at)> pending, int visible)
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            if (pending[i].at > visible) continue;
            PlayLineSound(cut, line, pending[i].sound);
            pending.RemoveAt(i);
        }
    }

    private void PlayLineSounds(StoryCut cut, StoryLine line, LineSoundTiming timing)
    {
        foreach (var sound in line.Sounds)
            if (sound != null && sound.timing == timing)
                PlayLineSound(cut, line, sound);
    }

    private void PlayLineSound(StoryCut cut, StoryLine line, LineSound sound)
    {
        object scope = sound.loopUntil == SoundLoopUntil.LineEnd ? line : cut;
        storyAudio.PlaySfx(sound.clip, sound.volume, sound.delay, sound.loop, scope);
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
