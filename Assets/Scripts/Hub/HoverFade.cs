using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// UI Image에 붙인다. 마우스를 올리면 색과 투명도가 부드럽게 바뀌고, 치우면 원래대로 돌아온다.
[RequireComponent(typeof(Graphic))]
public class HoverFade : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("마우스를 올렸을 때 곱해질 색. 흰색이면 색은 그대로이고, A(알파)로 투명도를 정한다.")]
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 0.6f);

    [Tooltip("바뀌는 데 걸리는 시간(초). 0이면 바로 바뀐다.")]
    [SerializeField, Min(0f)] private float duration = 0.15f;

    [Tooltip("자식 Image와 글자도 같이 바뀌게 할지")]
    [SerializeField] private bool includeChildren = true;

    [Tooltip("같이 바뀔 다른 그래픽 (예: 이 종이의 _Shadow)")]
    [SerializeField] private Graphic[] extraTargets;

    private Graphic[] targets;
    private Color[] baseColors;
    private float blend;
    private bool hovered;

    private void Awake()
    {
        var own = includeChildren ? GetComponentsInChildren<Graphic>(true) : new[] { GetComponent<Graphic>() };
        int extra = extraTargets != null ? extraTargets.Length : 0;
        targets = new Graphic[own.Length + extra];
        own.CopyTo(targets, 0);
        if (extra > 0) extraTargets.CopyTo(targets, own.Length);

        baseColors = new Color[targets.Length];
        for (int i = 0; i < targets.Length; i++)
            if (targets[i]) baseColors[i] = targets[i].color;
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    private void Update()
    {
        float goal = hovered ? 1f : 0f;
        if (Mathf.Approximately(blend, goal)) return;
        blend = duration > 0f ? Mathf.MoveTowards(blend, goal, Time.unscaledDeltaTime / duration) : goal;
        Apply();
    }

    private void OnDisable()
    {
        hovered = false;
        blend = 0f;
        if (targets != null) Apply();
    }

    private void Apply()
    {
        for (int i = 0; i < targets.Length; i++)
            if (targets[i]) targets[i].color = Color.Lerp(baseColors[i], baseColors[i] * hoverColor, blend);
    }
}
