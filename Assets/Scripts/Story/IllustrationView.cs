using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 컷 배경 일러스트를 보여 준다. 화면 전체를 덮는 Image 두 장을 번갈아 쓰며 크로스페이드한다.
// 일러스트가 없을 때(null)는 뒤의 검은 카메라 배경이 그대로 보인다.
public class IllustrationView : MonoBehaviour
{
    [Tooltip("일러스트 Image 두 장. 같은 크기로 겹쳐 둔다.")]
    [SerializeField] private Image front;
    [SerializeField] private Image back;

    public Sprite Current => front && front.enabled ? front.sprite : null;

    private void Awake()
    {
        SetAlpha(front, front && front.sprite ? 1f : 0f);
        SetAlpha(back, 0f);
    }

    // 페이드 없이 바로 바꾼다. null이면 검은 화면
    public void ShowImmediate(Sprite sprite)
    {
        if (sprite) front.sprite = sprite;
        SetAlpha(front, sprite ? 1f : 0f);
        SetAlpha(back, 0f);
    }

    // 새 일러스트를 뒤 Image에 깔고 앞으로 올리며 페이드 인한다. null이면 검은 화면으로 페이드 아웃
    // 검은 화면에서 시작하면 검은 화면 위로 서서히 밝아진다
    public IEnumerator Show(Sprite sprite, float duration)
    {
        if (sprite == Current) yield break;
        if (duration <= 0f)
        {
            ShowImmediate(sprite);
            yield break;
        }
        if (!sprite)
        {
            yield return FadeOut(duration);
            yield break;
        }

        back.sprite = sprite;
        back.transform.SetAsLastSibling();
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            SetAlpha(back, t / duration);
            yield return null;
        }
        SetAlpha(back, 1f);
        SetAlpha(front, 0f);
        (front, back) = (back, front);
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

    private static void SetAlpha(Image image, float alpha)
    {
        if (!image) return;
        var color = image.color;
        color.a = alpha;
        image.color = color;
        image.enabled = alpha > 0f && image.sprite;
    }
}
