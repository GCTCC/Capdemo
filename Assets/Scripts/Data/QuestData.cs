using System;
using System.Collections.Generic;
using UnityEngine;

// 좌상단 목표 패널에 보이는 큰 목표 하나. Create > CapDemo > 데이터 > 퀘스트
// 단계 목록을 위에서부터 하나씩 진행한다. 지금 단계의 "완료 조건"이 모두 맞으면 다음 단계로 넘어간다.
// 마지막 단계가 끝나면 "끝낸 퀘스트"로 저장되고 Next Quest가 바로 시작된다.
// 챕터마다 퀘스트 묶음을 Next Quest로 이어 두고, 챕터 클리어는 마지막 퀘스트 끝에 맞춘다.
[CreateAssetMenu(menuName = "CapDemo/데이터/퀘스트", fileName = "퀘스트_새퀘스트", order = 2)]
public class QuestData : ContentAsset
{
    [Serializable]
    public class Step
    {
        [Tooltip("목표 패널 한 줄 (예: 라이브하우스 바에서 판매자 기다리기). {F} 같은 키 토큰을 쓸 수 있다")]
        public string text = "";
        [Tooltip("이 조건이 모두 맞으면 이 단계가 끝난다")]
        public List<Condition> completeWhen = new List<Condition>();
        [Tooltip("남은 거리를 잴 목표 지점 ID (씬에 놓은 QuestTarget의 ID). 비우면 거리 표시 없음")]
        public string targetId = "";
        [Tooltip("목표 지점이 있는 구역. 다른 구역에 있으면 그쪽으로 가는 포탈을 대신 가리킨다")]
        public AreaData targetArea;
        [Tooltip("이 단계에서 말을 걸어야 하는 NPC ID. 그 NPC 위에 노란 ! 표시가 뜬다")]
        public string talkNpcId = "";
    }

    [Tooltip("몇 장의 퀘스트인지 (튜토리얼 = 0)")]
    [SerializeField, Min(0)] private int chapter;
    [Tooltip("목표 패널 제목 (예: CH.0 · 첫 거래)")]
    [SerializeField] private string panelTitle = "";
    [Tooltip("아직 열리지 않은 다음 단계 자리에 보일 문구")]
    [SerializeField] private string unknownText = "???";
    [Tooltip("진행 중인 퀘스트가 없을 때, 이 조건이 모두 맞으면 시작한다. Next Quest로 이어지는 퀘스트는 비워 둔다")]
    [SerializeField] private List<Condition> startWhen = new List<Condition>();
    [SerializeField] private List<Step> steps = new List<Step>();
    [Tooltip("이 퀘스트가 끝나면 바로 시작할 퀘스트")]
    [SerializeField] private QuestData nextQuest;
    [Tooltip("1 이상이면 이 퀘스트가 끝날 때 '챕터 n개 클리어'로 저장한다")]
    [SerializeField, Min(0)] private int clearChapters;

    public int Chapter => chapter;
    public string PanelTitle => panelTitle;
    public string UnknownText => unknownText;
    public List<Condition> StartWhen => startWhen;
    public IReadOnlyList<Step> Steps => steps;
    public QuestData NextQuest => nextQuest;
    public int ClearChapters => clearChapters;

    public Step GetStep(int index) => index >= 0 && index < steps.Count ? steps[index] : null;
}
