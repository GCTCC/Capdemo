using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 캐릭터 소개 카드(이름 + 프로필). 머리 옆 이름표(SpeakerNameTag)와 같은 모양을 조금 크게 만든 것.
// 대사 줄의 Intro Card에 내용이 있으면 그 줄이 시작될 때 펼쳐지고, 다음 줄에서 접힌다 (DialogueBox가 부른다).
// 대화창 Canvas 안, DialogueBox 아래에 둔다. Tools > CapDemo > 스토리 > 소개 카드 넣기로 만든다.
//
// 자리 정하기
// - 챕터: 줄에 적은 Position(1920x1080 기준 화면 좌표)에 꼬리 끝 마름모가 찍힌다.
// - 맵 대화: 화자 캐릭터(SpeakerTag) 머리를 따라가고, 머리 옆 이름표 위쪽으로 상자가 뜬다. 줄의 Map Offset만큼 더 옮긴다.
//
// 플레이 중에도 줄의 Position / Map Offset을 매 프레임 다시 읽으므로, 인스펙터에서 값을 바꾸면 바로 따라 움직인다.
// 에디터에서 Play 중에는 Game 뷰에서 카드를 마우스 오른쪽 버튼으로 끌어 옮길 수 있고, 그 값이 대사 줄에 저장된다.
//
// 펼칠 때: 꼬리 끝 마름모 → 비스듬한 선 → 가로 선 → 카드(꼬리 쪽에서부터 드러남). 접을 때는 거꾸로.
public class IntroCard : MonoBehaviour
{
    public const float RefWidth = 1920f;
    public const float RefHeight = 1080f;

    [SerializeField] private RectTransform box;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text profileText;
    [Tooltip("꼬리 끝에서 비스듬히 올라가는 선")]
    [SerializeField] private RectTransform tailSlant;
    [Tooltip("비스듬한 선 끝에서 카드까지 가로로 이어지는 선")]
    [SerializeField] private RectTransform tailFlat;
    [Tooltip("꼬리 끝 마름모(없어도 된다)")]
    [SerializeField] private RectTransform tailEnd;
    [Tooltip("카드 상자를 감싼 마스크. 카드를 꼬리 쪽에서부터 드러낸다. 비어 있으면 가로로 늘려서 펼친다")]
    [SerializeField] private RectMask2D boxMask;

    [Tooltip("꼬리 끝 마름모에서 카드 상자 모서리까지 거리 (1920x1080 기준). 맵에서는 머리 옆 이름표 위로 뜨도록 높게 잡는다")]
    [SerializeField] private Vector2 boxOffset = new Vector2(60f, 100f);
    [Tooltip("가로 선이 카드 상자에 닿는 높이. 0이면 상자 아래 끝, 0.5면 가운데")]
    [SerializeField, Range(0f, 1f)] private float lineHeightOnBox;
    [Tooltip("선 굵기")]
    [SerializeField, Min(1f)] private float lineWidth = 1.7f;
    [Tooltip("꼬리 끝이 화면 이 비율보다 오른쪽에 있으면 카드를 왼쪽으로 띄운다")]
    [SerializeField, Range(0.5f, 1f)] private float flipAt = 0.72f;

    [Header("애니메이션 (초)")]
    [SerializeField, Min(0f)] private float unfoldTime = 0.5f;
    [SerializeField, Min(0f)] private float foldTime = 0.3f;

    // 펼침 진행도(0~1) 중 각 부분이 움직이는 구간
    private const float TipEnd = 0.12f;
    private const float SlantEnd = 0.35f;
    private const float FlatEnd = 0.55f;

    private SpeakerTag target;   // 맵: 따라갈 캐릭터
    private IntroCardInfo info;  // 지금 줄의 카드 내용. 위치 값을 매 프레임 여기서 읽는다
    private Vector2 rawTip;      // 화면 가장자리로 붙잡기 전 꼬리 끝 자리 (1920x1080 기준)
    private Canvas canvas;
    private float progress;
    private bool folding;

    public bool IsFolding => folding && isActiveAndEnabled;

    // 챕터: 화면 좌표(1920x1080 기준)에 꼬리 끝을 찍는다
    public void ShowAt(string title, string profile, IntroCardInfo card)
    {
        target = null;
        info = card;
        Open(title, profile);
    }

    // 맵: 화자 머리를 따라간다
    public void ShowOn(string title, string profile, SpeakerTag speaker, IntroCardInfo card)
    {
        target = speaker;
        info = card;
        Open(title, profile);
    }

    private void Open(string title, string profile)
    {
        progress = 0f;
        folding = false;
        gameObject.SetActive(true);
        if (Application.isPlaying) transform.SetAsLastSibling(); // 참가자 이름표 복제본보다 위에 그린다
        if (nameText) nameText.text = title;
        if (profileText)
        {
            profileText.text = profile;
            profileText.gameObject.SetActive(!string.IsNullOrEmpty(profile));
        }
        if (box) LayoutRebuilder.ForceRebuildLayoutImmediate(box); // 글 길이에 맞춘 상자 크기를 바로 알아야 마스크를 맞춘다
        Follow();
    }

    // 에디터 미리보기용: 애니메이션 없이 다 펼친 모습으로
    public void Snap()
    {
        if (!gameObject.activeSelf) return;
        progress = 1f;
        folding = false;
        if (box) LayoutRebuilder.ForceRebuildLayoutImmediate(box);
        Follow();
    }

    // 거꾸로 접은 뒤 스스로 꺼진다. 에디터(Play 아님)에서는 바로 끈다
    public void Fold()
    {
        if (!gameObject.activeSelf) return;
        if (!Application.isPlaying)
        {
            Hide();
            return;
        }
        folding = true;
    }

    public void Hide()
    {
        target = null;
        folding = false;
        progress = 0f;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        dragging = false;
#endif
        target = null;
        folding = false;
        progress = 0f;
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        if (folding)
        {
            progress -= foldTime > 0f ? dt / foldTime : 1f;
            if (progress <= 0f)
            {
                Hide();
                return;
            }
        }
        else progress = unfoldTime > 0f ? Mathf.Min(1f, progress + dt / unfoldTime) : 1f;
#if UNITY_EDITOR
        DragInGameView();
#endif
        Follow();
    }

    // ---- 화면 좌표(1920x1080 기준) <-> 월드 좌표. 에디터 위치잡기 도구도 쓴다 ----

    private RectTransform CanvasRect
    {
        get
        {
            if (!canvas) canvas = GetComponentInParent<Canvas>(true);
            return canvas ? (RectTransform)canvas.rootCanvas.transform : null;
        }
    }

    public Vector3 ReferenceToWorld(Vector2 refPoint)
    {
        var root = CanvasRect;
        if (!root) return refPoint;
        var size = root.rect.size;
        var local = new Vector2((refPoint.x / RefWidth - 0.5f) * size.x, (refPoint.y / RefHeight - 0.5f) * size.y);
        return root.TransformPoint(local);
    }

    public Vector2 WorldToReference(Vector3 world)
    {
        var root = CanvasRect;
        if (!root) return world;
        Vector2 local = root.InverseTransformPoint(world);
        var size = root.rect.size;
        return new Vector2((local.x / size.x + 0.5f) * RefWidth, (local.y / size.y + 0.5f) * RefHeight);
    }

    // 맵: 캐릭터 머리 위치를 화면 좌표(1920x1080 기준)로
    private bool TryHeadReference(out Vector2 refPoint)
    {
        refPoint = default;
        var cam = Camera.main;
        var root = CanvasRect;
        if (!target || !cam || !root) return false;
        Vector3 screen = cam.WorldToScreenPoint(target.HeadPosition);
        var uiCam = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(root, screen, uiCam, out var world)) return false;
        refPoint = WorldToReference(world);
        return true;
    }

    private void Follow()
    {
        Vector2 tip;
        if (target)
        {
            if (!TryHeadReference(out var head))
            {
                Hide();
                return;
            }
            tip = head + (info != null ? info.mapOffset : Vector2.zero);
        }
        else tip = info != null ? info.position : new Vector2(RefWidth * 0.5f, RefHeight * 0.5f);
        rawTip = tip;

        // 화면 밖으로 나가지 않게 가장자리 안쪽으로 붙잡는다
        float boxHeight = box ? box.rect.height : 0f;
        tip.x = Mathf.Clamp(tip.x, 20f, RefWidth - 20f);
        tip.y = Mathf.Clamp(tip.y, 20f, Mathf.Max(20f, RefHeight - 20f - boxOffset.y - boxHeight * (1f - lineHeightOnBox)));
        transform.position = ReferenceToWorld(tip);
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        float side = tip.x > RefWidth * flipAt ? -1f : 1f;
        var end = new Vector2(boxOffset.x * side, boxOffset.y);
        var bend = new Vector2(end.x * 0.45f, end.y);

        float tipShown = EaseOut(Segment(0f, TipEnd));
        float slant = EaseOut(Segment(TipEnd, SlantEnd));
        float flat = EaseOut(Segment(SlantEnd, FlatEnd));
        float card = EaseOut(Segment(FlatEnd, 1f));

        if (tailEnd)
        {
            tailEnd.anchoredPosition = Vector2.zero;
            tailEnd.localRotation = Quaternion.identity;
            tailEnd.localScale = new Vector3(tipShown * side, tipShown, 1f);
        }
        PlaceLine(tailSlant, Vector2.zero, bend, slant);
        PlaceLine(tailFlat, bend, end, flat);
        PlaceCard(end, side, card);
    }

    // 카드: 꼬리가 붙는 쪽 끝을 기준으로 둔다(오른쪽에 뜨면 왼쪽 끝, 왼쪽에 뜨면 오른쪽 끝)
    private void PlaceCard(Vector2 end, float side, float shown)
    {
        var pivot = new Vector2(side > 0f ? 0f : 1f, lineHeightOnBox);
        if (boxMask)
        {
            var mask = boxMask.rectTransform;
            mask.anchorMin = mask.anchorMax = new Vector2(0.5f, 0.5f);
            mask.pivot = pivot;
            mask.anchoredPosition = end;
            mask.localScale = Vector3.one;
            Vector2 size = Vector2.zero;
            if (box)
            {
                box.anchorMin = box.anchorMax = box.pivot = pivot;
                box.anchoredPosition = Vector2.zero;
                box.localScale = Vector3.one;
                size = box.rect.size;
            }
            mask.sizeDelta = size;
            float hidden = size.x * (1f - shown);
            boxMask.padding = side > 0f ? new Vector4(0f, 0f, hidden, 0f) : new Vector4(hidden, 0f, 0f, 0f);
        }
        else if (box)
        {
            box.pivot = pivot;
            box.anchoredPosition = end;
            box.localScale = new Vector3(shown, 1f, 1f);
        }
    }

#if UNITY_EDITOR
    // ---- 에디터 Play 중 위치잡기: Game 뷰에서 오른쪽 버튼으로 카드(상자나 꼬리 끝)를 끈다 ----
    private bool dragging;
    private Vector2 grabOffset;

    private void DragInGameView()
    {
        var mouse = Mouse.current;
        if (mouse == null || info == null || folding) return;
        if (!dragging)
        {
            if (!mouse.rightButton.wasPressedThisFrame) return;
            if (!TryMouseReference(mouse, out var start) || !Grabbed(mouse, start)) return;
            dragging = true;
            grabOffset = rawTip - start;
            return;
        }

        if (mouse.rightButton.isPressed)
        {
            if (!TryMouseReference(mouse, out var now)) return;
            var tip = now + grabOffset;
            tip = new Vector2(Mathf.Round(tip.x), Mathf.Round(tip.y));
            if (target && TryHeadReference(out var head)) info.mapOffset = tip - head;
            else info.position = tip;
            return;
        }

        dragging = false;
        MarkOwnerDirty();
        Debug.Log(target
            ? $"[IntroCard] '{(nameText ? nameText.text : "")}' Map Offset = {info.mapOffset}"
            : $"[IntroCard] '{(nameText ? nameText.text : "")}' Position = {info.position}");
    }

    private Camera UiCamera => canvas && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;

    private bool TryMouseReference(Mouse mouse, out Vector2 refPoint)
    {
        refPoint = default;
        var root = CanvasRect;
        if (!root) return false;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(root, mouse.position.ReadValue(), UiCamera, out var world)) return false;
        refPoint = WorldToReference(world);
        return true;
    }

    // 카드 상자 안이나 꼬리 끝 근처를 눌렀는지
    private bool Grabbed(Mouse mouse, Vector2 mouseRef)
    {
        if (box && RectTransformUtility.RectangleContainsScreenPoint(box, mouse.position.ReadValue(), UiCamera)) return true;
        return Vector2.Distance(mouseRef, rawTip) < 40f;
    }

    // 끈 값이 Play를 끝낸 뒤에도 남도록, 이 줄이 들어 있는 대사 에셋을 저장 대상으로 표시한다.
    // (NPC 오브젝트의 Lines에 직접 적은 대사는 씬 값이라 Play를 끝내면 되돌아간다)
    private void MarkOwnerDirty()
    {
        foreach (var chapter in Resources.FindObjectsOfTypeAll<StoryChapter>())
            foreach (var cut in chapter.Cuts)
                foreach (var line in cut.Lines)
                    if (line != null && line.IntroCard == info) UnityEditor.EditorUtility.SetDirty(chapter);
        foreach (var dialogue in Resources.FindObjectsOfTypeAll<NpcDialogue>())
            foreach (var line in dialogue.Lines)
                if (line != null && line.IntroCard == info) UnityEditor.EditorUtility.SetDirty(dialogue);
    }
#endif

    private float Segment(float from, float to) => Mathf.Clamp01((progress - from) / (to - from));

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

    private void PlaceLine(RectTransform line, Vector2 from, Vector2 to, float shown)
    {
        if (!line) return;
        var delta = to - from;
        line.pivot = new Vector2(0f, 0.5f);
        line.anchoredPosition = from;
        line.sizeDelta = new Vector2(delta.magnitude * shown, lineWidth);
        line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }
}
