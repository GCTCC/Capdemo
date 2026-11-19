using UnityEngine;
using UnityEngine.UI;

// 낡은 전구처럼 불빛이 일렁이고, 가끔 툭 꺼졌다 켜진다 (배경 전구 빛 등)
public class HoloFlicker : MonoBehaviour
{
    [SerializeField] private Graphic target;
    [SerializeField, Range(0f, 1f)] private float wobble = 0.15f;
    [Tooltip("1초에 툭 꺼질 확률")]
    [SerializeField, Range(0f, 1f)] private float dropChance = 0.15f;

    private float baseAlpha;
    private float seed;
    private float dropLeft;

    private void Awake()
    {
        if (!target) target = GetComponent<Graphic>();
        if (target) baseAlpha = target.color.a;
        seed = Random.value * 50f;
    }

    private void Update()
    {
        if (!target) return;
        float dt = Time.unscaledDeltaTime;
        if (dropLeft > 0f) dropLeft -= dt;
        else if (Random.value < dropChance * dt) dropLeft = Random.Range(0.04f, 0.14f);

        float n = Mathf.PerlinNoise(seed, Time.unscaledTime * 6f);
        float a = baseAlpha * (1f - wobble + wobble * 2f * n) * (dropLeft > 0f ? 0.25f : 1f);
        var c = target.color;
        c.a = a;
        target.color = c;
    }
}
