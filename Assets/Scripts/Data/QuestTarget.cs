using System.Collections.Generic;
using UnityEngine;

// 퀘스트 목표 지점. 씬의 빈 오브젝트에 붙이고 ID를 적는다 (퀘스트 단계의 Target Id와 같게).
// 남은 거리는 플레이어와 이 지점의 가로 거리로 계산한다.
public class QuestTarget : MonoBehaviour
{
    private static readonly List<QuestTarget> all = new List<QuestTarget>();

    [Tooltip("목표 지점 ID (예: target.livehouse.bar)")]
    [SerializeField] private string id = "";
    [Tooltip("▼ 표시 옆에 보일 이름 (예: 바). 비우면 숫자만")]
    [SerializeField] private string displayName = "";
    [Tooltip("▼ 표시를 띄울 높이 (이 지점 기준, 유닛)")]
    [SerializeField] private float markerHeight = 2.4f;

    public string Id => id;
    public string DisplayName => displayName;
    public Vector3 MarkerPosition => transform.position + Vector3.up * markerHeight;

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    public static QuestTarget Find(string targetId)
    {
        if (string.IsNullOrEmpty(targetId)) return null;
        foreach (var t in all)
            if (t && t.id == targetId) return t;
        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
        var p = transform.position;
        Gizmos.DrawLine(p, p + Vector3.up * markerHeight);
        Gizmos.DrawWireCube(MarkerPosition, new Vector3(0.4f, 0.4f, 0f));
    }
}
