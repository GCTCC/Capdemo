using TMPro;
using UnityEngine;

// 지직 전환(HoloTransition)의 재료와 시간 설정.
// Assets/Resources/HoloTransitionSettings.asset 하나만 있으면 된다 (허브 만들기 메뉴가 자동으로 만든다).
// 시간·문구·소리를 바꾸고 싶으면 그 파일을 선택해서 Inspector에서 고치면 된다.
[CreateAssetMenu(menuName = "CapDemo/Holo Transition Settings", fileName = "HoloTransitionSettings")]
public class HoloTransitionSettings : ScriptableObject
{
    [Header("재료")]
    [Tooltip("CapDemo/UI/HoloTransition 셰이더로 만든 머티리얼")]
    public Material screenMaterial;
    [Tooltip("로고 큰 글자 폰트")]
    public TMP_FontAsset titleFont;
    [Tooltip("로고 아래 작은 글자 폰트")]
    public TMP_FontAsset monoFont;
    [Tooltip("꺼질 때 소리")]
    public AudioClip offSound;
    [Tooltip("켜질 때 소리")]
    public AudioClip onSound;
    [Range(0f, 1f)] public float volume = 0.8f;

    [Header("로고 문구")]
    public string logoTitle = "RE:WRITE";
    public string logoSub = "TUNING SIGNAL  ·  FM 77.7";

    [Header("꺼질 때 (초) - 파밧! 하고 빠르게")]
    [Min(0f)] public float offGlitchTime = 0.17f;   // 지직거리며 흔들리는 시간
    [Min(0f)] public float offCollapseTime = 0.17f; // 가로줄로 접히는 시간
    [Min(0f)] public float offShrinkTime = 0.11f;   // 줄이 점으로 줄어드는 시간 (셋 합쳐 약 0.45초)
    [Tooltip("꺼질 때 화면이 흔들리는 세기(픽셀)")]
    [Min(0f)] public float shakeStrength = 55f;
    [Tooltip("접히는 순간 번쩍이는 세기")]
    [Range(0f, 1f)] public float flashStrength = 0.55f;

    [Header("로고 (초)")]
    [Min(0f)] public float logoInTime = 0.30f;
    [Min(0f)] public float logoHoldTime = 0.75f; // 로딩이 빨라도 최소 이만큼은 보여준다
    [Min(0f)] public float logoOutTime = 0.22f;

    [Header("켜질 때 (초)")]
    [Min(0f)] public float lineTime = 0.14f;     // 가운데 가로줄이 양옆으로 뻗는 시간
    [Min(0f)] public float openTime = 0.28f;     // 위아래로 벌어지는 시간
    [Min(0f)] public float settleTime = 0.45f;   // 남은 지직거림이 잦아드는 시간
}
