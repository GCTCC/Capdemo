using UnityEngine;

// 다른 구역에서 넘어왔을 때 플레이어가 나타날 자리. 포탈의 Target Spawn과 이름(Id)이 같으면 여기로 옮긴다.
// 씬을 바로 플레이했을 때는 플레이어를 옮기지 않는다(씬에 놓인 자리에서 시작).
// 도착 지점을 포탈 범위 안에 두면 도착하자마자 다시 넘어가니 조금 떨어뜨려 둔다.
[DefaultExecutionOrder(-100)] // 카메라(FollowCamera2D)가 Awake에서 플레이어 위치를 잡기 전에 옮긴다
public class MapSpawnPoint : MonoBehaviour
{
    // 포탈이 씬을 넘기기 직전에 정해 둔다
    public static string NextSpawn;

    [SerializeField] private string id = "Start";

    private void Awake()
    {
        if (string.IsNullOrEmpty(NextSpawn) || NextSpawn != id) return;
        NextSpawn = null;

        var player = FindAnyObjectByType<PlayerController2D>();
        if (!player) return;
        player.transform.position = transform.position;
        if (player.TryGetComponent(out Rigidbody2D body))
        {
            body.position = transform.position;
            body.linearVelocity = Vector2.zero;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.8f);
    }
}
