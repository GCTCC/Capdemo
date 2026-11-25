using UnityEngine;

// 목표까지 남은 거리 표시 설정. Create > CapDemo > 데이터 > 거리 설정 (게임에 하나)
// 기준: 주인공 키(약 1.7 유닛) = 1.7m 이므로 1 유닛 = 1m.
[CreateAssetMenu(menuName = "CapDemo/데이터/거리 설정", fileName = "거리설정", order = 6)]
public class DistanceSettings : ScriptableObject
{
    [Tooltip("Unity 1 유닛이 몇 m인지")]
    [SerializeField, Min(0.01f)] private float metersPerUnit = 1f;
    [Tooltip("표시 형식. {0} 자리에 숫자 (예: {0}m)")]
    [SerializeField] private string format = "{0}m";
    [Tooltip("이 거리(m)보다 가까우면 숫자 대신 Arrived Text를 보인다")]
    [SerializeField, Min(0f)] private float arrivedBelow = 1.5f;
    [SerializeField] private string arrivedText = "도착";
    [Tooltip("다른 구역 목표일 때 포탈 위에 붙는 문구. {0} 자리에 구역 이름 (예: {0} 방향)")]
    [SerializeField] private string otherAreaFormat = "{0} 방향";

    public float MetersPerUnit => metersPerUnit;
    public float ArrivedBelow => arrivedBelow;

    public string FormatMeters(float meters) =>
        meters < arrivedBelow ? arrivedText : string.Format(format, Mathf.RoundToInt(meters));

    public string FormatOtherArea(string areaName) => string.Format(otherAreaFormat, areaName);
}
