using UnityEngine;
using UnityEngine.UI;

// 조명 아래 먼지·불티가 천천히 떠오르며 반짝인다. 이 오브젝트의 사각형 안에서만 떠다닌다.
public class HoloDust : MonoBehaviour
{
    [SerializeField] private Sprite sprite;
    [SerializeField, Min(1)] private int count = 28;
    [SerializeField] private Vector2 size = new Vector2(2f, 6f);
    [SerializeField] private Vector2 riseSpeed = new Vector2(6f, 22f);
    [SerializeField] private Color color = new Color(1f, 0.85f, 0.6f, 0.55f);

    private class Mote
    {
        public RectTransform rt;
        public Image img;
        public float speed, sway, phase, alpha;
    }

    private Mote[] motes;
    private RectTransform area;

    private void Start()
    {
        area = (RectTransform)transform;
        motes = new Mote[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Mote", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var m = new Mote { rt = (RectTransform)go.transform, img = go.GetComponent<Image>() };
            m.img.sprite = sprite;
            m.img.raycastTarget = false;
            Respawn(m, true);
            motes[i] = m;
        }
    }

    private void Respawn(Mote m, bool anywhere)
    {
        var r = area.rect;
        float s = Random.Range(size.x, size.y);
        m.rt.sizeDelta = new Vector2(s, s);
        m.rt.anchoredPosition = new Vector2(Random.Range(r.xMin, r.xMax), anywhere ? Random.Range(r.yMin, r.yMax) : r.yMin);
        m.speed = Random.Range(riseSpeed.x, riseSpeed.y);
        m.sway = Random.Range(4f, 16f);
        m.phase = Random.value * 10f;
        m.alpha = Random.Range(0.3f, 1f);
    }

    private void Update()
    {
        if (motes == null) return;
        var r = area.rect;
        float dt = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;
        foreach (var m in motes)
        {
            var p = m.rt.anchoredPosition;
            p.y += m.speed * dt;
            p.x += Mathf.Sin(time * 0.8f + m.phase) * m.sway * dt;
            m.rt.anchoredPosition = p;
            float twinkle = 0.6f + 0.4f * Mathf.Sin(time * 3f + m.phase * 3f);
            float edge = Mathf.Clamp01((r.yMax - p.y) / 60f) * Mathf.Clamp01((p.y - r.yMin) / 30f);
            var c = color;
            c.a *= m.alpha * twinkle * edge;
            m.img.color = c;
            if (p.y > r.yMax) Respawn(m, false);
        }
    }
}
