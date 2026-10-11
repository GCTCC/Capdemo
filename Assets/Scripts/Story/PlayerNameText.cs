using TMPro;
using UnityEngine;

// 아무 TextMeshPro 글자에 붙인다. 글자에 적어 둔 {player}를 저장된 플레이어 이름으로 바꿔 보여 준다.
// 예: "{player} 님의 기록" → "재이 님의 기록". 이름이 바뀌면 바로 다시 그린다.
[RequireComponent(typeof(TMP_Text))]
public class PlayerNameText : MonoBehaviour
{
    private TMP_Text text;
    private string template;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        template = text.text; // 씬에 적어 둔 원래 문장을 틀로 기억한다
    }

    private void OnEnable()
    {
        SaveData.PlayerNameChanged += Refresh;
        Refresh();
    }

    private void OnDisable() => SaveData.PlayerNameChanged -= Refresh;

    public void Refresh() => text.text = StoryText.Format(template);
}
