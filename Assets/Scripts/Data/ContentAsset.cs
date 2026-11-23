using UnityEngine;

// 내용 에셋(구역·퀘스트·안내·조작 안내)의 공통 부분: ID와 메모.
// 진행 기록은 파일 이름이 아니라 ID로 저장하므로, 파일 이름은 마음대로 바꿔도 되지만 ID는 한 번 정하면 바꾸지 않는다.
// ID 규칙: 영문 소문자·숫자·밑줄과 점. "종류.챕터.이름" 모양 (예: quest.ch0.enter_livehouse)
public abstract class ContentAsset : ScriptableObject
{
    [Tooltip("진행 기록에 쓰는 이름. 영문 소문자와 점 (예: area.livehouse). 한 번 정하면 바꾸지 않는다")]
    [SerializeField] private string id = "";
    [Tooltip("메모. 게임에는 안 나온다")]
    [SerializeField, TextArea(1, 4)] private string memo = "";

    public string Id => id;
    public string Memo => memo;

#if UNITY_EDITOR
    // 에디터 메뉴가 새 에셋을 만들 때 ID를 채워 넣는다
    public void EditorSetId(string value) => id = value;
#endif
}
