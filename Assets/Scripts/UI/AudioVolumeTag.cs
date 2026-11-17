using UnityEngine;

// AudioSource에 붙이면 설정 창의 배경음/효과음 음량을 따른다.
// 맵 BGM처럼 씬에 직접 둔 AudioSource에 붙인다. Inspector에 적어 둔 Volume이 "100%일 때 음량"이 된다.
// (전체 음량은 AudioListener가 모든 소리에 곱하므로 따로 붙일 필요 없다)
[RequireComponent(typeof(AudioSource))]
public class AudioVolumeTag : MonoBehaviour
{
    public enum Category { Bgm, Sfx }

    [SerializeField] private Category category = Category.Bgm;

    private AudioSource source;
    private float baseVolume;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
        Apply();
    }

    private void OnEnable() => GameSettings.Changed += Apply;
    private void OnDisable() => GameSettings.Changed -= Apply;

    private void Apply()
    {
        if (!source) return;
        source.volume = baseVolume * (category == Category.Bgm ? GameSettings.BgmVolume : GameSettings.SfxVolume);
    }
}
