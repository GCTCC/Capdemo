using System;
using System.Collections;
using UnityEngine;

// 맵 씬 위에 스토리(StoryChapter)를 덮어 띄운다. 씬을 바꾸지 않으므로 끝나면 맵이 그대로 이어진다.
// StoryManager와 같은 오브젝트(맵용 StoryCanvas)에 붙인다. Tools > CapDemo > 스토리 > 맵용 스토리 UI 만들기로 만든다.
// 평소에는 일러스트·대사창을 숨겨 두고, Play를 부르면
//   화면이 검게 어두워짐 → 스토리 재생 → 다시 검게 → 일러스트·대사창을 숨기고 맵으로 밝아짐
// 순서로 진행한다. 재생하는 동안 플레이어는 움직이지 않는다(PlayerController2D가 IsPlaying을 본다).
[RequireComponent(typeof(StoryManager))]
public class StoryOverlay : MonoBehaviour
{
    [Tooltip("맵에서 스토리로 넘어갈 때 검게 어두워지는 시간(초)")]
    [SerializeField, Min(0f)] private float enterFade = 0.5f;
    [Tooltip("스토리가 끝나고 맵으로 밝아지는 시간(초)")]
    [SerializeField, Min(0f)] private float exitFade = 0.5f;
    [Tooltip("맵의 BGM AudioSource. 넣어 두면 스토리 동안 멈췄다가 끝나면 이어서 튼다. 비워도 된다")]
    [SerializeField] private AudioSource mapBgm;

    private StoryManager manager;

    // 지금 스토리를 보여 주는 중인지 (어두워지기 시작 ~ 맵이 다시 밝아질 때까지)
    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        manager = GetComponent<StoryManager>();
        manager.PlayOnStart = false; // 맵에서는 NPC가 부를 때만 재생한다
        SetContentVisible(false);
        SetFade(0f);
    }

    // chapter를 재생하고, 맵으로 완전히 돌아오면 onDone을 부른다. 이미 재생 중이면 무시하고 false를 돌려준다.
    // stayBlack이면 끝난 뒤 맵으로 밝아지지 않고 검은 화면인 채로 onDone을 부른다 (바로 다른 씬으로 넘어갈 때)
    public bool Play(StoryChapter chapter, Action onDone = null, bool stayBlack = false)
    {
        if (IsPlaying || !chapter) return false;
        StartCoroutine(PlayRoutine(chapter, onDone, stayBlack));
        return true;
    }

    // 씬이 시작하자마자 스토리를 틀 때, 맵이 한 번 보였다가 어두워지지 않게 처음부터 검게 덮어 둔다
    public void SnapBlack() => SetFade(1f);

    private IEnumerator PlayRoutine(StoryChapter chapter, Action onDone, bool stayBlack)
    {
        IsPlaying = true;
        bool bgmWasPlaying = mapBgm && mapBgm.isPlaying;

        // 맵 → 검은 화면
        yield return FadeTo(1f, enterFade);
        if (bgmWasPlaying) mapBgm.Pause();

        // 검은 화면 뒤에서 일러스트·대사창을 켜고 재생. StoryManager가 검은 화면에서 밝아지며 시작해 다시 검게 끝난다
        SetContentVisible(true);
        manager.Dialogue.ResetStanding(); // 앞에서 본 스토리의 스탠딩이 남지 않게
        manager.Play(chapter);
        while (manager.IsPlaying) yield return null;

        // 검은 화면 뒤에서 숨기고 맵으로 밝아진다
        SetContentVisible(false);
        if (stayBlack)
        {
            SetFade(1f);
            onDone?.Invoke(); // 씬을 넘기는 쪽이 부른다. IsPlaying은 켜 둔 채라 그동안 플레이어가 움직이지 않는다
            yield break;
        }
        if (bgmWasPlaying) mapBgm.UnPause();
        yield return FadeTo(0f, exitFade);

        IsPlaying = false;
        onDone?.Invoke();
    }

    private void SetContentVisible(bool visible)
    {
        if (manager.Illustration) manager.Illustration.gameObject.SetActive(visible);
        if (manager.Dialogue)
        {
            manager.Dialogue.gameObject.SetActive(visible);
            if (!visible) manager.Dialogue.ResetStanding(); // 스탠딩 Image는 대사창 밖에 있어 따로 숨긴다
        }
    }

    private IEnumerator FadeTo(float to, float duration)
    {
        var overlay = manager.FadeOverlay;
        if (!overlay) yield break;

        overlay.blocksRaycasts = true;
        float from = overlay.alpha;
        for (float t = 0f; t < duration; t += GamePause.Delta)
        {
            overlay.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        SetFade(to);
    }

    private void SetFade(float alpha)
    {
        var overlay = manager.FadeOverlay;
        if (!overlay) return;
        overlay.alpha = alpha;
        overlay.blocksRaycasts = alpha > 0f;
    }
}
