using UnityEngine;

// 플레이어가 들어오면 진행 관리자에 "이 구역에 들어왔다"를 알리는 칸. 안내 트리거(구역 진입)에 쓴다.
// 빈 오브젝트에 Box Collider 2D와 함께 붙이고 ID를 적는다.
[RequireComponent(typeof(Collider2D))]
public class ProgressZone : MonoBehaviour
{
    [Tooltip("구역 칸 ID (예: zone.livehouse.stage_front). 첫 안내의 Trigger Id와 같게 적는다")]
    [SerializeField] private string id = "";

    public string Id => id;

    private void Awake() => GetComponent<Collider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody && other.attachedRigidbody.GetComponent<PlayerController2D>())
            Progress.NotifyZoneEntered(id);
    }

    private void OnDrawGizmos()
    {
        if (!TryGetComponent(out BoxCollider2D box)) return;
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.offset, box.size);
    }
}
