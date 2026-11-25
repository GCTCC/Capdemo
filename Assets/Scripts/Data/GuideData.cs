using System.Collections.Generic;
using UnityEngine;

// 주황 경고 홀로그램으로 한 번 뜨는 첫 안내 (예: 첫 말 걸기 안내). Create > CapDemo > 데이터 > 첫 안내
// Trigger가 일어난 순간 Show When 조건이 모두 맞으면 뜬다. Once면 닫을 때 "본 안내"로 저장되어 다시 안 뜬다.
[CreateAssetMenu(menuName = "CapDemo/데이터/첫 안내", fileName = "안내_새안내", order = 3)]
public class GuideData : ContentAsset
{
    public enum Trigger
    {
        [InspectorName("씬이 열렸을 때")] SceneStart,
        [InspectorName("특정 NPC 범위 안에 들어갔을 때")] NpcRange,
        [InspectorName("구역(ProgressZone)에 들어갔을 때")] ZoneEnter,
        [InspectorName("대화·스토리가 끝났을 때")] DialogueEnd,
        [InspectorName("퀘스트 단계가 바뀌었을 때")] QuestStepChanged,
    }

    [Header("문구")]
    [Tooltip("패널 위 작은 코드 (예: GUIDE · 01)")]
    [SerializeField] private string code = "GUIDE · 01";
    [Tooltip("본문. {F} 같은 토큰은 키 아이콘으로 바뀐다")]
    [SerializeField, TextArea(2, 5)] private string body = "";

    [Header("언제")]
    [SerializeField] private Trigger trigger = Trigger.NpcRange;
    [Tooltip("NPC 범위: NPC ID / 구역: ProgressZone ID / 씬이 열렸을 때: 씬 이름(비우면 아무 씬)")]
    [SerializeField] private string triggerId = "";
    [Tooltip("대화·스토리가 끝났을 때: 그 대화 에셋. 비우면 아무 대화나")]
    [SerializeField] private ScriptableObject triggerDialogue;
    [Tooltip("이 조건이 모두 맞을 때만 뜬다")]
    [SerializeField] private List<Condition> showWhen = new List<Condition>();
    [Tooltip("떠 있는 동안 이 조건이 모두 맞게 되면 사라진다. 비우면 플레이어가 닫거나 시간이 지나야 사라진다")]
    [SerializeField] private List<Condition> hideWhen = new List<Condition>();
    [Tooltip("켜면 한 번 보고 나면 다시 안 뜬다")]
    [SerializeField] private bool once = true;

    [Header("모양")]
    [Tooltip("주황 점선으로 가리킬 NPC ID. 비우면 가리키지 않음")]
    [SerializeField] private string pointAtNpcId = "";
    [Tooltip("0보다 크면 이 시간(초) 뒤 저절로 닫힌다")]
    [SerializeField, Min(0f)] private float autoCloseSeconds;

    public string Code => code;
    public string Body => body;
    public Trigger When => trigger;
    public string TriggerId => triggerId;
    public ScriptableObject TriggerDialogue => triggerDialogue;
    public List<Condition> ShowWhen => showWhen;
    public List<Condition> HideWhen => hideWhen;
    public bool Once => once;
    public string PointAtNpcId => pointAtNpcId;
    public float AutoCloseSeconds => autoCloseSeconds;
}
