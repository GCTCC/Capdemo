using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 스토리의 BGM과 효과음을 재생한다. StoryManager가 없으면 실행할 때 자동으로 붙인다.
// BGM은 AudioSource 두 개를 번갈아 쓰며 크로스페이드하고, 효과음은 한 번 재생과 반복 재생(환경음)을 나눠 관리한다.
public class StoryAudio : MonoBehaviour
{
    [Tooltip("BGM 전체 음량. 각 컷의 BGM 음량과 곱해진다 (나중에 설정 메뉴와 연결)")]
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
    [Tooltip("효과음 전체 음량. 각 효과음의 음량과 곱해진다")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private AudioSource bgmCurrent;
    private AudioSource bgmPrevious;
    private AudioSource oneShot;
    private AudioClip currentClip;
    private float currentVolume;
    private Coroutine bgmFade;

    // 반복 재생 중인 효과음과, 그 소리를 멈출 범위(대사나 컷)
    private readonly List<(AudioSource source, object scope)> loops = new List<(AudioSource, object)>();
    private readonly HashSet<object> endedScopes = new HashSet<object>();

    public float BgmVolume
    {
        get => bgmVolume;
        set => bgmVolume = Mathf.Clamp01(value);
    }

    public float SfxVolume
    {
        get => sfxVolume;
        set => sfxVolume = Mathf.Clamp01(value);
    }

    public AudioClip CurrentBgm => currentClip;

    private void Awake()
    {
        bgmCurrent = CreateSource(true);
        bgmPrevious = CreateSource(true);
        oneShot = CreateSource(false);
    }

    private void Update()
    {
        // 전체 음량을 바꾸면 페이드 중이 아닐 때 바로 반영한다
        if (bgmFade == null && currentClip) bgmCurrent.volume = currentVolume * bgmVolume;
    }

    // clip으로 바꾼다. 이미 같은 곡이 흐르고 있으면 음량만 맞춘다
    public void PlayBgm(AudioClip clip, float volume, float fade)
    {
        if (!clip) return;
        currentVolume = volume;
        if (clip == currentClip && bgmCurrent.isPlaying)
        {
            StartBgmFade(bgmCurrent, volume * bgmVolume, bgmPrevious, fade);
            return;
        }

        (bgmCurrent, bgmPrevious) = (bgmPrevious, bgmCurrent);
        currentClip = clip;
        bgmCurrent.clip = clip;
        bgmCurrent.volume = 0f;
        bgmCurrent.Play();
        StartBgmFade(bgmCurrent, volume * bgmVolume, bgmPrevious, fade);
    }

    public void StopBgm(float fade)
    {
        if (!currentClip) return;
        currentClip = null;
        StartBgmFade(null, 0f, bgmCurrent, fade);
    }

    // delay초 뒤에 낸다. loop면 StopLoops(scope)가 불릴 때까지 반복한다
    public void PlaySfx(AudioClip clip, float volume, float delay, bool loop, object scope)
    {
        if (!clip) return;
        if (delay > 0f) StartCoroutine(PlaySfxLater(clip, volume, delay, loop, scope));
        else PlaySfxNow(clip, volume, loop, scope);
    }

    // 이 범위(대사나 컷)에 묶인 반복 효과음을 멈춘다
    public void StopLoops(object scope)
    {
        endedScopes.Add(scope);
        for (int i = loops.Count - 1; i >= 0; i--)
        {
            if (loops[i].scope != scope) continue;
            Destroy(loops[i].source);
            loops.RemoveAt(i);
        }
    }

    public void StopAllLoops()
    {
        foreach (var (source, _) in loops) Destroy(source);
        loops.Clear();
    }

    private IEnumerator PlaySfxLater(AudioClip clip, float volume, float delay, bool loop, object scope)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (loop && endedScopes.Contains(scope)) yield break; // 기다리는 사이 범위가 끝났으면 반복음은 내지 않는다
        PlaySfxNow(clip, volume, loop, scope);
    }

    private void PlaySfxNow(AudioClip clip, float volume, bool loop, object scope)
    {
        if (!loop)
        {
            oneShot.PlayOneShot(clip, volume * sfxVolume);
            return;
        }
        endedScopes.Remove(scope);
        var source = CreateSource(true);
        source.clip = clip;
        source.volume = volume * sfxVolume;
        source.Play();
        loops.Add((source, scope));
    }

    private void StartBgmFade(AudioSource fadeIn, float target, AudioSource fadeOut, float duration)
    {
        if (bgmFade != null)
        {
            // 이전 페이드를 끊으면, 이번 페이드에 들지 않는 쪽 곡은 바로 멈춘다
            StopCoroutine(bgmFade);
            foreach (var source in new[] { bgmCurrent, bgmPrevious })
                if (source != fadeIn && source != fadeOut) source.Stop();
        }
        bgmFade = StartCoroutine(BgmFade(fadeIn, target, fadeOut, duration));
    }

    private IEnumerator BgmFade(AudioSource fadeIn, float target, AudioSource fadeOut, float duration)
    {
        float inFrom = fadeIn ? fadeIn.volume : 0f;
        float outFrom = fadeOut ? fadeOut.volume : 0f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            if (fadeIn) fadeIn.volume = Mathf.Lerp(inFrom, target, k);
            if (fadeOut) fadeOut.volume = Mathf.Lerp(outFrom, 0f, k);
            yield return null;
        }
        if (fadeIn) fadeIn.volume = target;
        if (fadeOut)
        {
            fadeOut.volume = 0f;
            fadeOut.Stop();
        }
        bgmFade = null;
    }

    private AudioSource CreateSource(bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        return source;
    }
}
