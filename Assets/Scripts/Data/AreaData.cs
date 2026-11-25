using System.Collections.Generic;
using UnityEngine;

// 구역(맵 씬 하나)의 표시 정보. Create > CapDemo > 데이터 > 구역
// 씬이 열리면 Scene Name이 같은 구역을 찾아 "방문"으로 기록하고, Show Banner 설정에 따라 위치 갱신 표시를 띄운다.
[CreateAssetMenu(menuName = "CapDemo/데이터/구역", fileName = "구역_새구역", order = 1)]
public class AreaData : ContentAsset
{
    public enum BannerMode
    {
        [InspectorName("들어올 때마다")] EveryTime,
        [InspectorName("처음 왔을 때만")] FirstVisitOnly,
        [InspectorName("띄우지 않음")] Never,
    }

    [Header("씬")]
    [Tooltip("이 구역의 씬 이름 (확장자 없이, 예: Area2_LiveHouse)")]
    [SerializeField] private string sceneName = "";
    [Tooltip("이 씬에서 포탈로 바로 갈 수 있는 구역들. 다른 씬에 있는 목표까지 길을 찾을 때 쓴다")]
    [SerializeField] private List<AreaData> connectedAreas = new List<AreaData>();

    [Header("위치 갱신 표시")]
    [Tooltip("크게 보이는 구역 이름 (예: 러스트 홀)")]
    [SerializeField] private string displayName = "";
    [Tooltip("이름 위 작은 글자 (예: SECTOR 07 · 공업지대)")]
    [SerializeField] private string topLine = "";
    [Tooltip("이름 아래 정보 줄 (예: 등록 공연장 · 감시 중)")]
    [SerializeField] private string bottomLine = "";
    [Tooltip("켜면 오토네 통제구역 모양(흰빨강, 감시 중 빨간 점), 끄면 지하 비통제 모양(앰버·마젠타, 점선, 낙서)")]
    [SerializeField] private bool controlledByOtone = true;
    [Tooltip("비통제 구역일 때 구석에 뿌려지는 낙서 문구")]
    [SerializeField] private string graffiti = "";
    [Tooltip("위치 갱신 표시를 언제 띄울지")]
    [SerializeField] private BannerMode showBanner = BannerMode.EveryTime;

    public string SceneName => sceneName;
    public IReadOnlyList<AreaData> ConnectedAreas => connectedAreas;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? sceneName : displayName;
    public string TopLine => topLine;
    public string BottomLine => bottomLine;
    public bool ControlledByOtone => controlledByOtone;
    public string Graffiti => graffiti;
    public BannerMode ShowBanner => showBanner;

    // 이번에 들어왔을 때 위치 갱신 표시를 띄울지
    public bool ShouldShowBanner(bool firstVisit) =>
        showBanner == BannerMode.EveryTime || (showBanner == BannerMode.FirstVisitOnly && firstVisit);
}
