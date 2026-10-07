using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 대화창 겉모양 묶음. Create > CapDemo > Story Skin으로 만들고 DialogueBox의 Skin 칸에 넣는다.
// 여기서 그림을 바꾸면 열린 씬의 대화창에 바로 반영된다.
[CreateAssetMenu(fileName = "StorySkin", menuName = "CapDemo/Story Skin")]
public class StorySkin : ScriptableObject
{
    [Header("대화창")]
    [Tooltip("대화창 배경 이미지. 비우면 씬에 있는 모양을 그대로 둔다. Sprite Editor에서 Border를 잡으면 모서리가 늘어나지 않게(Sliced) 그린다.")]
    [SerializeField, SpritePreview] private Sprite boxSprite;
    [Tooltip("이미지에 곱해지는 색. 흰색이면 이미지 원본 그대로")]
    [SerializeField] private Color boxColor = Color.white;

    [Header("이름표")]
    [Tooltip("이름표 배경 이미지. 비우면 씬에 있는 모양을 그대로 둔다.")]
    [SerializeField, SpritePreview] private Sprite plateSprite;
    [SerializeField] private Color plateColor = Color.white;

    [Header("글자")]
    [Tooltip("대사 폰트. 비우면 그대로 둔다.")]
    [SerializeField] private TMP_FontAsset bodyFont;
    [SerializeField] private Color bodyColor = Color.white;
    [Tooltip("이름 폰트. 비우면 그대로 둔다.")]
    [SerializeField] private TMP_FontAsset nameFont;
    [SerializeField] private Color nameColor = Color.white;

    [Header("▼ 표시")]
    [SerializeField] private Color indicatorColor = Color.white;

    public Sprite BoxSprite => boxSprite;
    public Color BoxColor => boxColor;
    public Sprite PlateSprite => plateSprite;
    public Color PlateColor => plateColor;
    public TMP_FontAsset BodyFont => bodyFont;
    public Color BodyColor => bodyColor;
    public TMP_FontAsset NameFont => nameFont;
    public Color NameColor => nameColor;
    public Color IndicatorColor => indicatorColor;

#if UNITY_EDITOR
    // 값을 바꾸면 이 스킨을 쓰는 열린 씬의 대화창을 다시 칠한다
    private void OnValidate()
    {
        EditorApplication.delayCall += () =>
        {
            if (!this) return;
            foreach (var box in FindObjectsByType<DialogueBox>(FindObjectsInactive.Include))
                if (box.Skin == this) box.ApplySkin();
        };
    }
#endif
}
