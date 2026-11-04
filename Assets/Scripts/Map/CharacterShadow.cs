using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 플레이어·NPC에 붙이는 가짜 그림자.
// Unity 2D 그림자(Shadow Caster 2D)는 위에서 내려다보는 게임용이라, 옆에서 보는 맵에서는 광원이 캐릭터와 겹치면 주변이 통째로 어두워진다.
// 대신 그림 두 장으로 그림자를 흉내 낸다.
//   발밑 그림자: 바닥에 깔리는 부드러운 타원. 늘 옅게 있고, 가까운 조명이 있으면 진해지고 조명 반대쪽으로 조금 밀린다.
//   벽 그림자: 캐릭터 모양을 검게 칠해 배경(벽)에 옅게 띄운다. 가장 가까운 조명 반대쪽으로 밀리고, 조명에서 멀수록 흐려진다.
// 그림자를 만드는 조명은 Shadows가 켜진 Point(Spot) Light 2D뿐이다. 두 그림자 모두 배경 층에 그려서 맵 오브젝트 뒤에 깔린다.
[ExecuteAlways]
[DisallowMultipleComponent]
public class CharacterShadow : MonoBehaviour
{
    private const string FootName = "Shadow_Foot";
    private const string WallName = "Shadow_Wall";
    // 배경 그림(Order 0) 바로 위
    private const int ShadowOrder = 100;
    // 조명 목록을 다시 찾는 간격(초)
    private const float LightRefreshInterval = 0.5f;

    [Tooltip("그림자를 만들 캐릭터 그림. 비워 두면 자식까지 찾아 첫 Sprite Renderer를 쓴다.")]
    [SerializeField] private SpriteRenderer source;

    [Header("발밑 그림자")]
    [Tooltip("조명이 없을 때의 진하기")]
    [SerializeField, Range(0f, 1f)] private float footAlpha = 0.25f;
    [Tooltip("조명 바로 옆일 때의 진하기")]
    [SerializeField, Range(0f, 1f)] private float footLitAlpha = 0.5f;
    [Tooltip("그림 폭에 대한 타원 폭 비율")]
    [SerializeField, Min(0f)] private float footWidth = 0.8f;
    [Tooltip("타원 높이(유닛)")]
    [SerializeField, Min(0f)] private float footHeight = 0.2f;
    [Tooltip("조명 반대쪽으로 밀리는 최대 거리(유닛)")]
    [SerializeField, Min(0f)] private float footShift = 0.25f;
    [Tooltip("점프 중 발밑 그림자를 찾을 바닥 거리(유닛). 이보다 높이 뜨면 그림자가 사라진다.")]
    [SerializeField, Min(0.1f)] private float groundSearch = 3f;

    [Header("벽 그림자")]
    [SerializeField] private bool wallShadow = true;
    [Tooltip("조명 바로 옆일 때의 진하기")]
    [SerializeField, Range(0f, 1f)] private float wallAlpha = 0.35f;
    [Tooltip("조명 반대쪽으로 밀리는 최대 거리(유닛)")]
    [SerializeField, Min(0f)] private float wallShift = 0.4f;
    [Tooltip("조명 바로 옆일 때 커지는 비율 (0.1 = 10% 크게)")]
    [SerializeField, Min(0f)] private float wallGrow = 0.1f;

    [Header("조명")]
    [Tooltip("조명 바깥 반경(Outer Radius)의 몇 배 거리까지 그림자를 만들지")]
    [SerializeField, Min(0.1f)] private float lightReach = 2.5f;

    private static readonly List<Light2D> Lights = new List<Light2D>();
    private static float lastLightRefresh = float.NegativeInfinity;
    private static Sprite footSprite;

    private SpriteRenderer foot;
    private SpriteRenderer wall;

    private void OnEnable()
    {
#if UNITY_EDITOR
        // 프리팹 편집 화면 등 미리보기 씬에서는 만들지 않는다 (프리팹에 그림자 오브젝트가 섞여 저장되지 않게)
        if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene)) return;
#endif
        if (!source) source = FindSource();
        if (!source) return;
        foot = CreateShadow(FootName, FootSprite());
        wall = CreateShadow(WallName, null);
        lastLightRefresh = float.NegativeInfinity;
    }

    // 그림자는 씬에 저장하지 않고 켤 때마다 새로 만든다
    private void OnDisable()
    {
        DestroyShadow(foot);
        DestroyShadow(wall);
        foot = wall = null;
    }

    private void LateUpdate()
    {
        if (!source || !foot || !wall) return;
        var bounds = source.bounds;
        float strength = FindLight(bounds.center, out Vector2 away, out float nearness);
        UpdateFoot(bounds, strength, away);
        UpdateWall(strength, away, nearness);
    }

    private SpriteRenderer FindSource()
    {
        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer.name != FootName && renderer.name != WallName) return renderer;
        return null;
    }

    private SpriteRenderer CreateShadow(string shadowName, Sprite sprite)
    {
        var go = new GameObject(shadowName, typeof(SpriteRenderer)) { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(source.transform, false);
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = MapBackground.SortingLayer;
        renderer.sortingOrder = ShadowOrder;
        renderer.color = Color.clear;
        return renderer;
    }

    private static void DestroyShadow(SpriteRenderer shadow)
    {
        if (!shadow) return;
        if (Application.isPlaying) Destroy(shadow.gameObject);
        else DestroyImmediate(shadow.gameObject);
    }

    // 그림 가운데에서 가장 세게 비추는 조명을 찾는다. 세기(0~1), 조명에서 캐릭터 쪽 방향, 가까운 정도(1 = 조명 위)를 돌려준다
    private float FindLight(Vector2 center, out Vector2 away, out float nearness)
    {
        RefreshLights();
        float best = 0f;
        away = Vector2.zero;
        nearness = 0f;
        foreach (var light in Lights)
        {
            if (!light || !light.isActiveAndEnabled || !light.shadowsEnabled) continue;
            if (light.lightType != Light2D.LightType.Point) continue;
            float reach = light.pointLightOuterRadius * lightReach;
            if (reach <= 0f) continue;
            Vector2 offset = center - (Vector2)light.transform.position;
            float near = 1f - offset.magnitude / reach;
            if (near <= 0f) continue;
            float strength = near * Mathf.Clamp01(light.intensity);
            if (strength <= best) continue;
            best = strength;
            away = offset / reach;  // 길이가 조명에서 먼 정도(0~1)
            nearness = near;
        }
        return best;
    }

    private static void RefreshLights()
    {
        float now = Time.realtimeSinceStartup;
        if (now - lastLightRefresh < LightRefreshInterval && Lights.Count > 0) return;
        lastLightRefresh = now;
        Lights.Clear();
        Lights.AddRange(FindObjectsByType<Light2D>(FindObjectsSortMode.None));
    }

    private void UpdateFoot(Bounds bounds, float strength, Vector2 away)
    {
        // 플레이 중에는 발 아래 바닥을 찾아 그림자를 바닥에 둔다. 높이 뜰수록 옅고 작아진다
        float groundY = bounds.min.y;
        float lift = 0f;
        if (Application.isPlaying && TryFindGround(new Vector2(bounds.center.x, bounds.min.y + 0.05f), out float hitY))
        {
            lift = Mathf.Max(0f, bounds.min.y - hitY);
            groundY = hitY;
        }
        else if (Application.isPlaying)
        {
            foot.color = Color.clear;
            return;
        }
        float fade = 1f - Mathf.Clamp01(lift / groundSearch);

        float width = bounds.size.x * footWidth * (1f + 0.3f * strength) * Mathf.Lerp(0.5f, 1f, fade);
        float shiftX = Mathf.Sign(away.x) * Mathf.Min(Mathf.Abs(away.x) * 2f, 1f) * footShift * strength;
        SetWorld(foot.transform, new Vector3(bounds.center.x + shiftX, groundY, 0f),
            new Vector2(width / footSprite.bounds.size.x, footHeight / footSprite.bounds.size.y));
        float alpha = Mathf.Lerp(footAlpha, footLitAlpha, strength) * fade;
        foot.color = new Color(0f, 0f, 0f, alpha);
    }

    private void UpdateWall(float strength, Vector2 away, float nearness)
    {
        if (!wallShadow || strength <= 0f || !source.sprite)
        {
            wall.color = Color.clear;
            return;
        }
        if (wall.sprite != source.sprite) wall.sprite = source.sprite;
        wall.flipX = source.flipX;
        wall.flipY = source.flipY;
        wall.drawMode = source.drawMode;
        if (source.drawMode != SpriteDrawMode.Simple) wall.size = source.size;

        // 조명 바로 위면 그림자는 캐릭터 뒤에 숨고, 멀어질수록 반대쪽으로 밀린다
        Vector2 shift = away.normalized * (wallShift * Mathf.Clamp01(away.magnitude * 2f));
        var t = wall.transform;
        t.localPosition = source.transform.InverseTransformVector(shift);
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one * (1f + wallGrow * nearness);
        wall.color = new Color(0f, 0f, 0f, wallAlpha * strength);
    }

    private bool TryFindGround(Vector2 origin, out float y)
    {
        y = 0f;
        foreach (var hit in Physics2D.RaycastAll(origin, Vector2.down, groundSearch))
        {
            if (hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform)) continue;
            y = hit.point.y;
            return true;
        }
        return false;
    }

    // 월드 위치·크기를 부모(캐릭터 그림) 기준 값으로 바꿔 넣는다
    private void SetWorld(Transform t, Vector3 position, Vector2 worldScale)
    {
        var parent = source.transform;
        t.localPosition = parent.InverseTransformPoint(position);
        t.localRotation = Quaternion.identity;
        var parentScale = parent.lossyScale;
        t.localScale = new Vector3(worldScale.x / NonZero(parentScale.x), worldScale.y / NonZero(parentScale.y), 1f);
    }

    private static float NonZero(float value) => Mathf.Abs(value) < 1e-5f ? 1f : Mathf.Abs(value);

    // 가운데가 진하고 가장자리로 갈수록 투명해지는 흰 타원. 색은 Sprite Renderer의 Color로 칠한다
    private static Sprite FootSprite()
    {
        if (footSprite) return footSprite;
        const int w = 64, h = 32;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
            pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * a * (3f - 2f * a) * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        footSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        footSprite.hideFlags = HideFlags.DontSave;
        return footSprite;
    }
}
