using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 플레이어 이름 입력 창. StoryManager가 이름 입력 줄에서 Ask()를 부르고, 확인될 때까지 기다린다.
public class NameInputPanel : MonoBehaviour
{
    [SerializeField] private TMP_InputField input;
    [SerializeField] private Button confirmButton;
    [SerializeField, Min(1)] private int maxLength = 8;

    private bool submitted;
    private bool initialized;

    // 창은 꺼진 상태로 시작하므로 Awake 대신 처음 열 때 연결한다
    private void Init()
    {
        if (initialized) return;
        initialized = true;
        input.characterLimit = maxLength;
        input.onSubmit.AddListener(_ => Submit());
        if (confirmButton) confirmButton.onClick.AddListener(Submit);
    }

    public IEnumerator Ask()
    {
        Init();
        submitted = false;
        input.text = SaveData.GetPlayerName();
        gameObject.SetActive(true);
        input.ActivateInputField();

        while (!submitted) yield return null;

        gameObject.SetActive(false);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    private void Submit()
    {
        string name = input.text.Trim();
        if (name.Length == 0)
        {
            input.ActivateInputField();
            return;
        }
        SaveData.SetPlayerName(name);
        submitted = true;
    }
}
