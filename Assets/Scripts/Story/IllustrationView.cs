using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 컷 배경 일러스트를 보여 준다. 화면 전체를 덮는 Image 두 장을 번갈아 쓰며 크로스페이드한다.
// 일러스트가 없을 때(null, Black Screen 컷)는 맨 뒤에 자동으로 까는 바탕(기본 검정)이 보인다.
// 바탕은 실행 중에만 만들어지고 씬에는 저장되지 않으므로, 카메라 배경색과 상관없이 검게 보인다.
// 컷의 연출 값(IllustFraming)에 따라 맞춤 방식·확대·위치·색을 정한다.
public class IllustrationView : MonoBehaviour
{
    [Tooltip("일러스트 Image 두 장. 같은 크기로 겹쳐 둔다.")]
    [SerializeField] private Image front;
    [SerializeField] private Image back;

    [Tooltip("일러스트가 없을 때(Black Screen 컷, 비율 차이로 남는 가장자리) 보일 바탕색")]
    [SerializeField] private Color emptyColor = Color.black;

    private Image backdrop;

    private IllustFraming frontFraming;
    private IllustFraming backFraming;

    public Sprite Current => front && front.enabled ? front.sprite : null;

    private void Awake()
    {
        EnsureBackdrop();
        SetAlpha(front, front && front.sprite ? 1f : 0f);
        SetAlpha(back, 0f);
    }

    // 지금 이 그림을 이 연출로 보여 주고 있는지
    public bool IsShowing(Sprite sprite, IllustFraming framing) =>
        sprite == Current && (!sprite || frontFraming.SameAs(framing));

    // 페이드 없이 바로 바꾼다. null이면 검은 화면
    public void ShowImmediate(Sprite sprite, IllustFraming framing)
    {
        EnsureBackdrop();
        if (sprite)
        {
            front.sprite = sprite;
            frontFraming = framing;
            ApplyFraming(front, framing);
        }
        SetAlpha(front, sprite ? 1f : 0f);
        SetAlpha(back, 0f);
    }

    // 새 일러스트를 뒤 Image에 깔고 앞으로 올리며 페이드 인한다. null이면 검은 화면으로 페이드 아웃
    // 검은 화면에서 시작하면 검은 화면 위로 서서히 밝아진다. 같은 그림이라도 연출이 다르면 겹치며 바뀐다
    public IEnumerator Show(Sprite sprite, IllustFraming framing, float duration)
    {
        if (IsShowing(sprite, framing)) yield break;
        if (duration <= 0f)
        {
            ShowImmediate(sprite, framing);
            yield break;
        }
        if (!sprite)
        {
            yield return FadeOut(duration);
            yield break;
        }

        back.sprite = sprite;
        backFraming = framing;
        ApplyFraming(back, framing);
        back.transform.SetAsLastSibling();
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            SetAlpha(back, t / duration);
            yield return null;
        }
        SetAlpha(back, 1f);
        SetAlpha(front, 0f);
        (front, back) = (back, front);
        (frontFraming, backFraming) = (backFraming, frontFraming);
    }

    private IEnumerator FadeOut(float duration)
    {
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            SetAlpha(front, 1f - t / duration);
            yield return null;
        }
        SetAlpha(front, 0f);
    }

    // 일러스트 두 장보다 뒤에 화면을 덮는 바탕을 깐다 (저장되지 않는 임시 오브젝트)
    private void EnsureBackdrop()
    {
        if (!backdrop)
        {
            var go = new GameObject("Backdrop (auto)", typeof(RectTransform), typeof(Image))
            {
                hideFlags = HideFlags.DontSave,
            };
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            backdrop = go.GetComponent<Image>();
            backdrop.raycastTarget = false;
        }
        backdrop.color = emptyColor;
        backdrop.transform.SetAsFirstSibling();
    }

    // 에디터 미리보기를 끝낼 때 임시 바탕을 치운다
    public void RemoveBackdrop()
    {
        if (!backdrop) return;
        if (Application.isPlaying) Destroy(backdrop.gameObject);
        else DestroyImmediate(backdrop.gameObject);
        backdrop = null;
    }

    // Image를 부모(화면 전체) 기준으로 맞춘 뒤 확대·이동·색을 적용한다. 알파는 페이드용이라 건드리지 않는다
    private static void ApplyFraming(Image image, IllustFraming framing)
    {
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        image.preserveAspect = framing.fit == IllustFit.Fit;

        // Fill: 그림 비율을 지키며 화면을 덮도록, 모자란 쪽 길이를 늘린다
        var grow = Vector2.zero;
        if (framing.fit == IllustFit.Fill && image.sprite && rect.parent is RectTransform parent)
        {
            var area = parent.rect.size;
            float spriteAspect = image.sprite.rect.width / image.sprite.rect.height;
            if (area.y > 0f && spriteAspect > area.x / area.y) grow.x = area.y * spriteAspect - area.x;
            else if (spriteAspect > 0f) grow.y = area.x / spriteAspect - area.y;
        }
        rect.sizeDelta = grow;
        rect.anchoredPosition = framing.offset;
        rect.localScale = new Vector3(framing.Zoom, framing.Zoom, 1f);

        var tint = framing.Tint;
        tint.a = image.color.a;
        image.color = tint;
    }

    private static void SetAlpha(Image image, float alpha)
    {
        if (!image) return;
        var color = image.color;
        color.a = alpha;
        image.color = color;
        image.enabled = alpha > 0f && image.sprite;
    }
}
