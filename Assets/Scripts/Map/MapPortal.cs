using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 다른 구역(씬)으로 넘어가는 문. 플레이어가 범위에 들어오면(또는 들어와서 ↑/W를 누르면)
// Target Scene을 열고, 그 씬에서 Target Spawn과 이름이 같은 도착 지점(MapSpawnPoint)에 플레이어를 놓는다.
// 열 씬은 File > Build Profiles의 씬 목록에 들어 있어야 한다. ("새 구역 씬 만들기" 메뉴로 만든 씬은 자동으로 들어간다)
// Destination(행선지 구역)을 넣으면 이름표에 그 구역 이름이 보이고, 목표가 다른 구역에 있을 때 이 문을 가리킨다.
// Target Scene을 비워 두면 행선지 구역의 씬으로 간다.
[RequireComponent(typeof(Collider2D))]
public class MapPortal : MonoBehaviour
{
    private static readonly List<MapPortal> all = new List<MapPortal>();

    // 잠긴 문에 닿았을 때 (UI가 잠김 문구를 보여 준다)
    public static event Action<MapPortal> LockedTouched;

    [Tooltip("넘어갈 씬 이름 (확장자 없이, 예: Area_Market). 비우면 Destination 구역의 씬")]
    [SerializeField] private string targetScene;
    [Tooltip("넘어간 씬에서 플레이어가 나타날 도착 지점 이름")]
    [SerializeField] private string targetSpawn = "Start";
    [Tooltip("체크하면 범위 안에서 ↑ 또는 W를 눌러야 넘어간다(문). 끄면 닿자마자 넘어간다(맵 끝).")]
    [SerializeField] private bool needKey;

    [Header("이름표")]
    [Tooltip("행선지 구역 (Create > CapDemo > 데이터 > 구역). 이름표에 구역 이름이 보인다")]
    [SerializeField] private AreaData destination;
    [Tooltip("이름표에 보일 행선지 이름. 비우면 구역 이름 (예: 라이브하우스 뒷문)")]
    [SerializeField] private string labelOverride = "";
    [Tooltip("이름표 버튼 문구 (예: 들어가기)")]
    [SerializeField] private string buttonText = "들어가기";
    [Tooltip("이름표·목표 표시를 띄울 높이 (문 기준, 유닛)")]
    [SerializeField] private float markerHeight = 3f;

    [Header("잠김")]
    [Tooltip("이 조건이 모두 맞아야 열린다. 비우면 항상 열림")]
    [SerializeField] private List<Condition> openWhen = new List<Condition>();
    [Tooltip("잠겨 있을 때 이름표에 보일 문구 (예: 잠김 · 아직 갈 수 없다)")]
    [SerializeField] private string lockedText = "잠김";

    private bool playerInside;
    private bool leaving;

    public AreaData Destination => destination;
    public string Label => !string.IsNullOrEmpty(labelOverride) ? labelOverride : destination ? destination.DisplayName : SceneName;
    public string ButtonText => buttonText;
    public string LockedText => lockedText;
    public bool NeedKey => needKey;
    public bool PlayerInside => playerInside;
    public bool IsOpen => Condition.AllMet(openWhen);
    public Vector3 MarkerPosition => transform.position + Vector3.up * markerHeight;
    private string SceneName => !string.IsNullOrEmpty(targetScene) ? targetScene : destination ? destination.SceneName : "";

    // 이 씬에서 그 구역으로 가는 문 (열린 문 우선)
    public static MapPortal FindToward(AreaData area)
    {
        if (!area) return null;
        MapPortal locked = null;
        foreach (var p in all)
        {
            if (!p || p.destination != area) continue;
            if (p.IsOpen) return p;
            if (!locked) locked = p;
        }
        return locked;
    }

    private void Awake() => GetComponent<Collider2D>().isTrigger = true;
    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

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
        if (!IsOpen)
        {
            LockedTouched?.Invoke(this);
            return;
        }
        string scene = SceneName;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"[CapDemo] 포탈 '{name}': 씬 '{scene}' 을(를) 열 수 없습니다. 이름이 맞는지, Build Profiles 씬 목록에 있는지 확인하세요.", this);
            return;
        }
        if (ScreenFader.IsBusy) return;
        leaving = true;
        MapSpawnPoint.NextSpawn = targetSpawn;
        ScreenFader.LoadScene(scene); // 검게 어두워졌다가 새 구역에서 밝아진다
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
