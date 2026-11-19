using TMPro;
using UnityEngine;

// 글 속 숫자가 살아 있는 것처럼 가끔 바뀐다 (PING 24ms, 좌표 등). 가짜 값이다.
public class HoloNumberJitter : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [Tooltip("{0} 자리에 숫자가 들어간다")]
    [SerializeField] private string format = "PING {0}ms";
    [SerializeField] private Vector2Int range = new Vector2Int(19, 31);
    [SerializeField] private Vector2 interval = new Vector2(0.8f, 2.2f);

    private float next;

    private void Awake()
    {
        if (!text) text = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (!text || Time.unscaledTime < next) return;
        next = Time.unscaledTime + Random.Range(interval.x, interval.y);
        text.text = string.Format(format, Random.Range(range.x, range.y + 1));
    }
}
