using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 다른 구역(씬)으로 넘어가는 문. 플레이어가 범위에 들어오면(또는 들어와서 ↑/W를 누르면)
// Target Scene을 열고, 그 씬에서 Target Spawn과 이름이 같은 도착 지점(MapSpawnPoint)에 플레이어를 놓는다.
// 열 씬은 File > Build Profiles의 씬 목록에 들어 있어야 한다. ("새 구역 씬 만들기" 메뉴로 만든 씬은 자동으로 들어간다)
[RequireComponent(typeof(Collider2D))]
public class MapPortal : MonoBehaviour
{
    [Tooltip("넘어갈 씬 이름 (확장자 없이, 예: Area_Market)")]
    [SerializeField] private string targetScene;
    [Tooltip("넘어간 씬에서 플레이어가 나타날 도착 지점 이름")]
    [SerializeField] private string targetSpawn = "Start";
    [Tooltip("체크하면 범위 안에서 ↑ 또는 W를 눌러야 넘어간다(문). 끄면 닿자마자 넘어간다(맵 끝).")]
    [SerializeField] private bool needKey;

    private bool playerInside;
    private bool leaving;

    private void Awake() => GetComponent<Collider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;
        playerInside = true;
        if (!needKey) Go();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other)) playerInside = false;
    }

    private void Update()
    {
        if (!needKey || !playerInside) return;
        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)) Go();
    }

    private void Go()
    {
        if (leaving) return;
        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError($"[CapDemo] 포탈 '{name}': 씬 '{targetScene}' 을(를) 열 수 없습니다. 이름이 맞는지, Build Profiles 씬 목록에 있는지 확인하세요.", this);
            return;
        }
        leaving = true;
        MapSpawnPoint.NextSpawn = targetSpawn;
        SceneManager.LoadScene(targetScene);
    }

    private static bool IsPlayer(Collider2D other) =>
        other.attachedRigidbody && other.attachedRigidbody.GetComponent<PlayerController2D>();

    private void OnDrawGizmos()
    {
        if (!TryGetComponent(out BoxCollider2D box)) return;
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.offset, box.size);
    }
}
