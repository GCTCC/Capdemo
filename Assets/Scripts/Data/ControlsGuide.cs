using System;
using System.Collections.Generic;
using UnityEngine;

// TutoScene 시작 때 한 번 뜨는 조작 안내 패널. Create > CapDemo > 데이터 > 조작 안내
// 아무 키나 누르면 닫히고 "본 안내"로 저장된다.
[CreateAssetMenu(menuName = "CapDemo/데이터/조작 안내", fileName = "조작안내", order = 4)]
public class ControlsGuide : ContentAsset
{
    [Serializable]
    public class Row
    {
        [Tooltip("키 토큰을 띄어 적는다 (예: {A} {D} 또는 {Left} {Right})")]
        public string keys = "";
        [Tooltip("설명 (예: 이동)")]
        public string description = "";
        [Tooltip("오른쪽 작은 라벨 (예: MOVE). 비워도 된다")]
        public string label = "";
    }

    [Tooltip("패널 위 작은 코드 (예: SYS · CONTROLS)")]
    [SerializeField] private string code = "SYS · CONTROLS";
    [SerializeField] private string title = "조작 안내";
    [Tooltip("줄 목록. 순서대로 위에서부터 보인다")]
    [SerializeField] private List<Row> rows = new List<Row>();
    [Tooltip("맨 아래 문구 (예: 아무 키나 눌러 닫기)")]
    [SerializeField] private string footer = "아무 키나 눌러 닫기";
    [Tooltip("이 씬이 열릴 때 띄운다 (예: TutoScene)")]
    [SerializeField] private string showInScene = "TutoScene";
    [Tooltip("이 조건이 모두 맞을 때만 뜬다")]
    [SerializeField] private List<Condition> showWhen = new List<Condition>();

    public string Code => code;
    public string Title => title;
    public IReadOnlyList<Row> Rows => rows;
    public string Footer => footer;
    public string ShowInScene => showInScene;
    public List<Condition> ShowWhen => showWhen;
}
