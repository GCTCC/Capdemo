using System.Collections;
using UnityEngine;

// 음악 재생 시간, 박자, 일시정지와 배속을 한곳에서 관리한다.
[DisallowMultipleComponent]
public class RhythmClock : MonoBehaviour
{
    [Header("Fallback Timing")]
    // ChartData가 연결되지 않았을 때 사용할 기본 BPM이다.
    [SerializeField]
    private float fallbackBpm = 120.0f;

    [Header("Audio Synchronization")]
    // OGG 디코딩과 오디오 버퍼가 준비될 시간을 확보한 뒤 음악과 시계를 함께 시작한다.
    [SerializeField]
    [Min(0.05f)]
    private float scheduledStartLeadTime = 0.20f;

    // 양수 값은 노트와 판정을 음악보다 늦춘다.
    // 현재 테스트 환경에서 확인한 7000 / 48000초를 기본 보정값으로 사용한다.
    [SerializeField]
    [Range(-500.0f, 500.0f)]
    private float noteTimingCalibrationMilliseconds = 145.83333f;

    // 음악을 재생하는 AudioSource다. 없으면 Awake에서 자동으로 만든다.
    private AudioSource musicSource;

    // 현재 사용 중인 곡과 채보 데이터다.
    private ChartData chartData;

    // 마지막으로 시간 계산 기준을 갱신한 DSP 시각이다.
    private double referenceDspTime;

    // referenceDspTime 시점까지 누적된 곡 시간이다.
    private double accumulatedSongTime;

    // 음악과 노트 이동에 함께 적용할 전역 재생 배율이다.
    private float playbackScale = 1.0f;

    // 시계가 시작되어 시간을 진행 중인지 나타낸다.
    private bool isPlaying;

    // 사용자가 일시정지한 상태인지 나타낸다.
    private bool isPaused;

    // 오디오 데이터 로딩이 끝난 뒤 DSP 예약 재생을 수행하는 코루틴이다.
    private Coroutine audioStartCoroutine;

    // NoteEditor와 동일하게 AudioSource의 샘플 위치에서 계산한 마지막 곡 시간이다.
    private double lastAudioSongTime;

    // 판정과 노트 이동이 공통으로 참조할 현재 곡 시간이다.
    public double SongTime
    {
        get
        {
            if (!isPlaying || isPaused)
            {
                return accumulatedSongTime;
            }

            // 음악이 있으면 NoteEditor와 동일한 AudioSource.timeSamples를 기준으로 삼는다.
            // DSP 시각만 사용하면 오디오 출력 버퍼와 Editor 환경에 따라 노트가
            // 실제 음악보다 먼저 진행되어 보일 수 있다.
            if (musicSource != null && musicSource.clip != null)
            {
                if (!HasStarted || musicSource.clip.frequency <= 0)
                {
                    return accumulatedSongTime;
                }

                double sampleTime = musicSource.timeSamples
                    / (double)musicSource.clip.frequency;

                // 곡 종료 직후 일부 플랫폼에서 timeSamples가 0으로 돌아가더라도
                // 게임 시간이 뒤로 점프하지 않도록 마지막 정상 시각을 유지한다.
                if (sampleTime >= lastAudioSongTime
                    || lastAudioSongTime - sampleTime < 0.05)
                {
                    lastAudioSongTime = sampleTime;
                }

                return lastAudioSongTime;
            }

            // 음악이 없는 랜덤 모드만 DSP 시각으로 진행한다.
            double elapsedDspTime = AudioSettings.dspTime - referenceDspTime;
            if (elapsedDspTime <= 0.0)
            {
                return accumulatedSongTime;
            }

            return accumulatedSongTime + elapsedDspTime * playbackScale;
        }
    }

    // 채보 노트 이동과 판정에 사용하는 보정된 시간이다.
    // NoteEditor JSON의 원본 offset은 건드리지 않고 Unity 출력 지연만 별도로 보정한다.
    public double ChartTime
    {
        get
        {
            if (chartData == null || !chartData.HasNotes)
            {
                return SongTime;
            }

            return SongTime - noteTimingCalibrationMilliseconds / 1000.0;
        }
    }

    // 현재 곡에서 사용하는 BPM이다.
    public float CurrentBpm
    {
        get { return chartData != null ? chartData.SafeBpm : Mathf.Max(1.0f, fallbackBpm); }
    }

    // 현재 곡 시간을 BPM 기준 박자로 변환한 값이다.
    public double CurrentBeat
    {
        get
        {
            double offset = chartData != null ? chartData.musicOffset : 0.0;
            return Mathf.Max(0.0f, (float)((ChartTime - offset) * CurrentBpm / 60.0));
        }
    }

    public bool IsPlaying { get { return isPlaying; } }
    public bool IsPaused { get { return isPaused; } }
    public float PlaybackScale { get { return playbackScale; } }

    // 음악이 예약된 DSP 시작 시각에 실제로 도달했는지 알려준다.
    // 음악이 없는 랜덤 모드는 StartClock 호출 즉시 true가 된다.
    public bool HasStarted
    {
        get
        {
            return isPlaying
                && (musicSource == null
                    || musicSource.clip == null
                    || accumulatedSongTime > 0.0
                    || AudioSettings.dspTime >= referenceDspTime);
        }
    }



    // AudioSource가 아직 준비되지 않았다면 찾아오거나 새로 만든다.
    private void EnsureMusicSource()
    {
        // 이미 준비되어 있다면 다시 생성하지 않는다.
        if (musicSource != null)
        {
            return;
        }

        // 같은 GameObject에 AudioSource가 있는지 먼저 확인한다.
        musicSource = GetComponent<AudioSource>();

        // AudioSource가 없다면 런타임에 새로 추가한다.
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        // 리듬게임 음악 재생용 설정을 적용한다.
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0.0f;
    }

    private void Awake()
    {
        EnsureMusicSource();
    }

    // 전달받은 채보의 음악을 처음부터 재생하고 시계를 시작한다.
    public void StartClock(ChartData newChartData)
    {
        // RhythmClock.Awake보다 먼저 호출되더라도 AudioSource가 반드시 준비되도록 한다.
        EnsureMusicSource();

        chartData = newChartData;
        accumulatedSongTime = 0.0;
        lastAudioSongTime = 0.0;
        isPaused = false;
        isPlaying = true;

        musicSource.Stop();

        if (audioStartCoroutine != null)
        {
            StopCoroutine(audioStartCoroutine);
            audioStartCoroutine = null;
        }

        // ChartData가 없으면 음악 없이 랜덤 채보를 실행한다.
        musicSource.clip =
            chartData != null ? chartData.audioClip : null;

        musicSource.pitch = playbackScale;

        // 음악이 있으면 AudioSource.Play()로 즉시 시작하지 않는다.
        // 동일한 DSP 시각에 음악과 게임 시계를 예약해야 OGG 준비 지연 때문에
        // 노트만 먼저 진행되는 현상을 막을 수 있다.
        if (musicSource.clip != null)
        {
            if (musicSource.clip.loadState == AudioDataLoadState.Unloaded)
            {
                musicSource.clip.LoadAudioData();
            }

            // 로딩 중에는 무한대 시각을 사용하여 게임 시간이 진행되지 않게 한다.
            referenceDspTime = double.PositiveInfinity;
            audioStartCoroutine = StartCoroutine(ScheduleAudioWhenLoaded());
        }
        else
        {
            // 음악이 없는 랜덤 모드는 기다릴 필요 없이 즉시 진행한다.
            referenceDspTime = AudioSettings.dspTime;
        }
    }

    // 압축 음원의 준비가 끝난 다음 음악과 게임 시계를 같은 DSP 시각에 예약한다.
    private IEnumerator ScheduleAudioWhenLoaded()
    {
        AudioClip requestedClip = musicSource.clip;

        while (requestedClip != null
            && requestedClip.loadState == AudioDataLoadState.Loading)
        {
            yield return null;
        }

        // 대기 중 다른 곡으로 바뀌거나 게임을 나갔다면 이전 요청을 취소한다.
        if (!isPlaying || requestedClip == null || musicSource.clip != requestedClip)
        {
            audioStartCoroutine = null;
            yield break;
        }

        if (requestedClip.loadState == AudioDataLoadState.Failed)
        {
            Debug.LogError("RhythmClock: 음악 데이터를 불러오지 못했습니다. "
                + requestedClip.name);
            musicSource.clip = null;
            referenceDspTime = AudioSettings.dspTime;
            audioStartCoroutine = null;
            yield break;
        }

        // 로딩 중 일시정지 메뉴가 열렸다면 재개할 때까지 예약을 보류한다.
        while (isPaused)
        {
            yield return null;
        }

        referenceDspTime = AudioSettings.dspTime
            + Mathf.Max(0.05f, scheduledStartLeadTime);
        musicSource.PlayScheduled(referenceDspTime);
        audioStartCoroutine = null;

        Debug.Log("RhythmClock: 음악과 채보를 DSP 시각 "
            + referenceDspTime.ToString("0.000")
            + "에 함께 시작하도록 예약했습니다. 이후 게임 시간은 AudioSource.timeSamples를 사용합니다. "
            + "Unity 판정 보정="
            + noteTimingCalibrationMilliseconds.ToString("0.###")
            + "ms");
    }

    // 현재 곡 시간을 고정하고 음악을 일시정지한다.
    public void PauseClock()
    {
        if (!isPlaying || isPaused)
        {
            return;
        }

        accumulatedSongTime = SongTime;
        isPaused = true;
        musicSource.Pause();
    }

    // 고정했던 곡 시간을 기준으로 음악과 시계를 다시 진행한다.
    public void ResumeClock()
    {
        if (!isPlaying || !isPaused)
        {
            return;
        }

        referenceDspTime = AudioSettings.dspTime;
        isPaused = false;
        musicSource.UnPause();
    }

    // 시계와 음악을 완전히 정지한다.
    public void StopClock()
    {
        if (isPlaying && !isPaused)
        {
            accumulatedSongTime = SongTime;
        }

        isPlaying = false;
        isPaused = false;

        if (audioStartCoroutine != null)
        {
            StopCoroutine(audioStartCoroutine);
            audioStartCoroutine = null;
        }

        musicSource.Stop();
    }

    // 음악과 게임 진행 속도에 사용할 배율을 변경한다.
    public void SetPlaybackScale(float newScale)
    {
        float clampedScale = Mathf.Clamp(newScale, 0.25f, 2.0f);

        // 배율을 바꾸기 직전의 시간을 누적해 시간 점프를 방지한다.
        if (isPlaying && !isPaused && HasStarted)
        {
            accumulatedSongTime = SongTime;
            referenceDspTime = AudioSettings.dspTime;
        }

        playbackScale = clampedScale;
        musicSource.pitch = playbackScale;
    }
}
