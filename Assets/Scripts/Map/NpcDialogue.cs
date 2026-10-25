using System.Collections.Generic;
using UnityEngine;

// NPC 한 명의 대사. 프로젝트 창 우클릭 > Create > CapDemo > NPC 대화로 만들고 NPC의 Dialogue 칸에 넣는다.
// 대사 형식은 스토리와 같다(StoryLine). 이름 입력(Pause) 설정은 무시한다.
// 화면에 띄우는 대화창은 씬마다 하나(DialogueCanvas)를 모든 NPC가 같이 쓴다.
[CreateAssetMenu(fileName = "대화_", menuName = "CapDemo/NPC 대화")]
public class NpcDialogue : ScriptableObject
{
    [Tooltip("인스펙터에서 알아보기 위한 메모. 게임에는 나오지 않는다.")]
    [SerializeField] private string memo;
    [SerializeField] private List<StoryLine> lines = new List<StoryLine>();

    public IReadOnlyList<StoryLine> Lines => lines;

#if UNITY_EDITOR
    // 인스펙터에서 새로 추가한 효과음은 음량이 0으로 들어오므로, 처음 Clip을 넣을 때 1로 맞춰 준다
    private void OnValidate()
    {
        foreach (var line in lines)
            if (line != null)
                foreach (var sound in line.Sounds) sound?.InitVolume();
    }
#endif
}
