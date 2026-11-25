using System.Collections.Generic;
using UnityEngine;

// 게임이 실행 중에 내용 에셋을 찾는 목록. Assets/Resources/DataCatalog.asset 하나.
// 직접 고칠 필요 없음: Assets/Data 안의 에셋이 바뀌면 에디터가 자동으로 다시 채운다
// (수동: Tools > CapDemo > 데이터 > 목록 새로고침).
public class DataCatalog : ScriptableObject
{
    public const string ResourcePath = "DataCatalog";

    [SerializeField] private List<AreaData> areas = new List<AreaData>();
    [SerializeField] private List<QuestData> quests = new List<QuestData>();
    [SerializeField] private List<GuideData> guides = new List<GuideData>();
    [SerializeField] private List<ControlsGuide> controlsGuides = new List<ControlsGuide>();
    [SerializeField] private KeyIconTable keyIcons;
    [SerializeField] private DistanceSettings distance;

    public IReadOnlyList<AreaData> Areas => areas;
    public IReadOnlyList<QuestData> Quests => quests;
    public IReadOnlyList<GuideData> Guides => guides;
    public IReadOnlyList<ControlsGuide> ControlsGuides => controlsGuides;
    public KeyIconTable KeyIcons => keyIcons;
    public DistanceSettings Distance => distance;

    public AreaData FindAreaByScene(string sceneName)
    {
        foreach (var a in areas)
            if (a && a.SceneName == sceneName) return a;
        return null;
    }

    public QuestData FindQuest(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var q in quests)
            if (q && q.Id == id) return q;
        return null;
    }

#if UNITY_EDITOR
    public void EditorFill(List<AreaData> a, List<QuestData> q, List<GuideData> g, List<ControlsGuide> c,
                           KeyIconTable k, DistanceSettings d)
    {
        areas = a;
        quests = q;
        guides = g;
        controlsGuides = c;
        keyIcons = k;
        distance = d;
    }
#endif
}
