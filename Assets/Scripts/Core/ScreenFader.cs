using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 씬을 넘길 때 화면을 검게 어두웠다가 새 씬에서 다시 밝히는 공용 페이드.
// 씬에 따로 놓을 필요 없다: 처음 쓰일 때 화면 맨 위에 검은 판을 스스로 만들고, 씬이 바뀌어도 남아 있는다.
// 사용: ScreenFader.LoadScene("Area1_");
public class ScreenFader : MonoBehaviour
{
    private const float DefaultFadeOut = 0.35f; // 어두워지는 시간(초)
    private const float DefaultFadeIn = 0.45f;  // 새 씬에서 밝아지는 시간(초)

    private static ScreenFader instance;
    private CanvasGroup group;

    // 지금 씬을 넘기는 중인지 (두 번 넘기지 않게)
    public static bool IsBusy { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        instance = null;
        IsBusy = false;
    }

    public static void LoadScene(string sceneName, float fadeOut = DefaultFadeOut, float fadeIn = DefaultFadeIn)
    {
        if (IsBusy) return;
        Instance.StartCoroutine(Instance.LoadRoutine(sceneName, fadeOut, fadeIn));
    }

    private static ScreenFader Instance
    {
        get
        {
            if (instance) return instance;

            var go = new GameObject("ScreenFader");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // 스토리·대화 UI보다 위

            var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
            image.transform.SetParent(go.transform, false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.GetComponent<Image>().color = Color.black;

            instance = go.AddComponent<ScreenFader>();
            instance.group = go.AddComponent<CanvasGroup>();
            instance.group.alpha = 0f;
            instance.group.blocksRaycasts = false;
            return instance;
        }
    }

    private IEnumerator LoadRoutine(string sceneName, float fadeOut, float fadeIn)
    {
        IsBusy = true;
        group.blocksRaycasts = true;
        yield return Fade(1f, fadeOut);

        var load = SceneManager.LoadSceneAsync(sceneName);
        while (!load.isDone) yield return null;
        yield return null; // 새 씬의 Start(스토리 자동 재생 등)가 한 번 돈 뒤에 밝힌다

        yield return Fade(0f, fadeIn);
        group.blocksRaycasts = false;
        IsBusy = false;
    }

    private IEnumerator Fade(float to, float duration)
    {
        float from = group.alpha;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }
}
