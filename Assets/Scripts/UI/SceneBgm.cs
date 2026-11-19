using System.Collections;
using UnityEngine;

// 씬 배경음. 씬에 들어오면 반복 재생하고, 지직 전환(HoloTransition)으로 씬을 떠날 때
// 자연스럽게 작아지며 꺼진다. 전환의 "꺼짐" 연출(약 0.45초)보다 먼저 끝나므로 다음 씬 화면이 나오기 전에 확실히 꺼진다.
// 음량은 설정 창의 배경음 음량(GameSettings.BgmVolume)을 따른다.
// 메뉴: Tools > CapDemo > 허브 > 배경음 넣기 (HoloHubScene)
[RequireComponent(typeof(AudioSource))]
public class SceneBgm : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [Tooltip("배경음 100%일 때 음량")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
    [Tooltip("씬에 들어올 때 커지는 시간(초)")]
    [SerializeField] private float fadeIn = 0.6f;
    [Tooltip("씬을 떠날 때 작아지는 시간(초). 전환의 꺼짐 연출(약 0.45초)보다 짧게")]
    [SerializeField] private float fadeOut = 0.4f;

    private AudioSource source;
    private float level;          // 0~1 (페이드)
    private bool leaving;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        if (clip != null) source.clip = clip;
    }

    private void OnEnable()
    {
        HoloTransition.Leaving += FadeOutNow;
    }

    private void OnDisable()
    {
        HoloTransition.Leaving -= FadeOutNow;
    }

    private void Start()
    {
        if (source.clip == null) return;
        level = 0f;
        Apply();
        source.Play();
    }

    private void Update()
    {
        if (source.clip == null) return;
        float dt = Time.unscaledDeltaTime;
        if (leaving)
        {
            level = Mathf.MoveTowards(level, 0f, dt / Mathf.Max(0.01f, fadeOut));
            if (level <= 0f && source.isPlaying) source.Stop();
        }
        else
        {
            level = Mathf.MoveTowards(level, 1f, dt / Mathf.Max(0.01f, fadeIn));
        }
        Apply();
    }

    private void Apply()
    {
        // 볼륨 곡선을 살짝 휘게 해서 작아지는 느낌이 자연스럽게
        source.volume = volume * GameSettings.BgmVolume * level * level;
    }

    public void FadeOutNow()
    {
        leaving = true;
    }
}
