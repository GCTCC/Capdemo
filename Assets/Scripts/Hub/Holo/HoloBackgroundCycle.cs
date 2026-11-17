using UnityEngine;
using UnityEngine.UI;

// 허브 배경 그림 돌려 보기. 허브 씬에 들어올 때마다(다른 메뉴·씬에 갔다 올 때마다) 목록의 다음 그림으로 바뀐다.
// 그림 넣는 법: Background > Parallax > Art 오브젝트를 선택하고, Inspector의 Backgrounds 목록에 그림(Sprite)을 끌어다 놓는다.
//   또는 Assets/Art/UI/HoloHub/Backgrounds 폴더에 그림을 넣고 허브 만들기 메뉴를 다시 실행하면 자동으로 목록에 들어간다.
// 그림은 16:9 크기가 가장 잘 맞는다 (다른 비율이어도 화면을 꽉 채우도록 잘라서 보여 준다).
[RequireComponent(typeof(Image))]
public class HoloBackgroundCycle : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public Sprite image;
        [Tooltip("이 그림에 맞춰 둔 전구 불빛(Lamps)을 켤지. 첫 배경(전구가 있는 그림)에서만 켠다.")]
        public bool showLamps;
    }

    [SerializeField] private Entry[] backgrounds = new Entry[0];
    [Tooltip("그림마다 위치를 맞춘 전구 불빛 묶음")]
    [SerializeField] private GameObject lamps;
    [Tooltip("켜면 순서대로가 아니라 무작위로 고른다 (바로 전 그림은 피한다)")]
    [SerializeField] private bool random = false;

    // 씬이 바뀌어도 남는 "지금 몇 번째 그림인지" (Play를 새로 누르면 처음으로)
    private static int index = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => index = -1;

    private void Awake()
    {
        int count = 0;
        foreach (var b in backgrounds) if (b != null && b.image) count++;
        if (count == 0) return;

        index = NextIndex(index);
        var entry = backgrounds[index];
        var image = GetComponent<Image>();
        image.sprite = entry.image;

        var fitter = GetComponent<AspectRatioFitter>();
        if (fitter && entry.image.rect.height > 0f) fitter.aspectRatio = entry.image.rect.width / entry.image.rect.height;
        if (lamps) lamps.SetActive(entry.showLamps);
    }

    private int NextIndex(int previous)
    {
        int n = backgrounds.Length;
        for (int tries = 0; tries < n * 3; tries++)
        {
            int i = random ? Random.Range(0, n) : (previous + 1 + tries) % n;
            if (random && n > 1 && i == previous) continue;
            if (backgrounds[i] != null && backgrounds[i].image) return i;
        }
        return 0;
    }
}
