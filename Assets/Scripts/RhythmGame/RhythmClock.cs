using UnityEngine;

// 음악 재생 시간, 박자, 일시정지와 배속을 한곳에서 관리한다.
[DisallowMultipleComponent]
public class RhythmClock : MonoBehaviour
{
    [Header("Fallback Timing")]
    // ChartData가 연결되지 않았을 때 사용할 기본 BPM이다.
    [SerializeField]
    private float fallbackBpm = 120.0f;

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

    // 판정과 노트 이동이 공통으로 참조할 현재 곡 시간이다.
    public double SongTime
    {
        get
        {
            if (!isPlaying || isPaused)
            {
                return accumulatedSongTime;
            }

            return accumulatedSongTime
                + (AudioSettings.dspTime - referenceDspTime) * playbackScale;
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
            return Mathf.Max(0.0f, (float)((SongTime - offset) * CurrentBpm / 60.0));
        }
    }

    public bool IsPlaying { get { return isPlaying; } }
    public bool IsPaused { get { return isPaused; } }
    public float PlaybackScale { get { return playbackScale; } }



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
        /*
        // 같은 GameObject의 AudioSource를 찾고 없으면 새로 추가한다.
        musicSource = GetComponent<AudioSource>();
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        // 리듬게임 음악은 공간에 따른 거리 감쇠 없이 같은 크기로 들려야 한다.
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0.0f;
        */
        EnsureMusicSource();
    }

    // 전달받은 채보의 음악을 처음부터 재생하고 시계를 시작한다.
    public void StartClock(ChartData newChartData)
    {
        /*
        chartData = newChartData;
        accumulatedSongTime = 0.0;
        referenceDspTime = AudioSettings.dspTime;
        isPaused = false;
        isPlaying = true;

        musicSource.Stop();
        musicSource.clip = chartData != null ? chartData.audioClip : null;
        musicSource.pitch = playbackScale;

        // 음악 파일이 없어도 시계는 진행하므로 랜덤 프로토타입을 시험할 수 있다.
        if (musicSource.clip != null)
        {
            musicSource.Play();
        }
        */

        // RhythmClock.Awake보다 먼저 호출되더라도 AudioSource가 반드시 준비되도록 한다.
        EnsureMusicSource();

        chartData = newChartData;
        accumulatedSongTime = 0.0;
        referenceDspTime = AudioSettings.dspTime;
        isPaused = false;
        isPlaying = true;

        musicSource.Stop();

        // ChartData가 없으면 음악 없이 랜덤 채보를 실행한다.
        musicSource.clip =
            chartData != null ? chartData.audioClip : null;

        musicSource.pitch = playbackScale;

        // 음악 파일이 없어도 리듬게임 시계는 계속 진행한다.
        if (musicSource.clip != null)
        {
            musicSource.Play();
        }
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
        musicSource.Stop();
    }

    // 음악과 게임 진행 속도에 사용할 배율을 변경한다.
    public void SetPlaybackScale(float newScale)
    {
        float clampedScale = Mathf.Clamp(newScale, 0.25f, 2.0f);

        // 배율을 바꾸기 직전의 시간을 누적해 시간 점프를 방지한다.
        if (isPlaying && !isPaused)
        {
            accumulatedSongTime = SongTime;
            referenceDspTime = AudioSettings.dspTime;
        }

        playbackScale = clampedScale;
        musicSource.pitch = playbackScale;
    }
}
