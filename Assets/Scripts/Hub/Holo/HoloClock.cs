using System;
using System.Globalization;
using TMPro;
using UnityEngine;

// 오른쪽 위 시계: 실제 컴퓨터 시각을 보여 주고 가운데 ':' 이 깜빡인다.
public class HoloClock : MonoBehaviour
{
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text dateText;
    [Tooltip("날짜 모양 (ddd 요일, MMM 달, dd 날짜). 예: MON  ·  NOV 16")]
    [SerializeField] private string dateFormat = "ddd  ·  MMM dd";
    [Tooltip("날짜 뒤에 붙는 글 (날씨 등)")]
    [SerializeField] private string dateSuffix = "";

    private void Update()
    {
        var now = DateTime.Now;
        if (timeText)
        {
            // 서식 태그 없이 ':' 자리를 빈칸으로 바꿔 깜빡인다 (글자 폭이 같은 폰트라 자리가 흔들리지 않는다)
            timeText.richText = false;
            timeText.text = now.ToString("HH") + (now.Millisecond < 500 ? ":" : " ") + now.ToString("mm");
        }
        if (dateText)
            dateText.text = now.ToString(dateFormat, CultureInfo.InvariantCulture).ToUpperInvariant() + dateSuffix;
    }
}
