using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// "게임 종료" 버튼. 누르면 "정말로 종료하시겠습니까?" 확인 창(HoloQuitDialog)을 띄운다.
// 창에서 [종료]를 누르면 화면이 지직거리며 꺼진 뒤 게임을 끄고, [취소]를 누르면 허브로 돌아온다.
// 창의 글꼴과 패널 그림은 이 버튼의 것(Label·En 글자, Bg·Line 그림)을 그대로 쓴다.
public class HoloQuitButton : MonoBehaviour, IPointerClickHandler
{
    private HoloQuitDialog dialog;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (HoloTransition.IsBusy || HoloQuitDialog.IsOpen || HoloPopup.AnyOpen) return;

        if (!dialog)
        {
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            TMP_FontAsset titleFont = null, monoFont = null;
            foreach (var t in GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.name == "Label") titleFont = t.font;
                else if (t.name == "En") monoFont = t.font;
            }
            if (!monoFont) monoFont = titleFont;
            var bg = transform.Find("Bg")?.GetComponent<Image>();
            var line = transform.Find("Line")?.GetComponent<Image>();
            dialog = HoloQuitDialog.Create(canvas, titleFont, monoFont, bg ? bg.sprite : null, line ? line.sprite : null);
        }
        dialog.Open();
    }
}
