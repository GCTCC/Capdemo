using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Video;

// Tools > CapDemo > 일러스트 관리
// 로비 배경 / 결과 배경 / 폴라로이드(+문구) / 인게임 배경 / 타이틀 배경을 한 창에서 보고 바꾼다.
// 이 창은 에디터 전용이라 게임 용량·메모리에는 영향이 없다.
// 실제 데이터는 화면마다 따로 있다 (각 화면은 자기 그림만 메모리에 올린다):
//   로비·인게임·타이틀 → Assets/Resources/Illustrations/Lobby·Ingame·Title.asset
//   결과 배경·폴라로이드 → Assets/Resources/ResultV2Settings.asset
// 그림은 프로젝트 창에서 이 창으로 끌어다 놓으면 추가된다.
public class IllustrationManagerWindow : EditorWindow
{
    private const string IllustFolder = "Assets/Resources/Illustrations";
    private const string ResultPath = "Assets/Resources/ResultV2Settings.asset";
    private const float ThumbW = 192f, ThumbH = 108f;
    private const float PolaroidPhotoAspect = 402f / 500f; // 결과 화면 폴라로이드 사진 칸 (ResultV2Controller)
    private const int BigTextureSide = 2048;

    private static readonly string[] Tabs = { "로비 배경", "결과 배경", "폴라로이드", "인게임 배경", "타이틀 배경" };
    private static readonly string[] Where =
    {
        "메인 로비(HoloHubScene) 뒤에 깔리는 그림. 허브에 들어올 때마다 다음 그림으로 바뀐다 (위에서부터 순서대로).\n전구 켜기: 그 그림에 맞춰 둔 전구 불빛을 켤지 (전구가 그려진 첫 그림만).",
        "곡을 끝낸 뒤 결과 화면 뒤에 흐리고 어둡게 깔리는 그림. 여러 장이면 열릴 때마다 랜덤으로 한 장.\n비워 두면 곡 재킷을 흐리게 깐다.",
        "결과 화면 오른쪽 폴라로이드 사진과 그 아래 손글씨 문구. [사진 + 문구] 묶음 중 하나가 랜덤으로 나온다.\n미리보기는 실제 사진 칸 비율로 잘린 모습이다. 인물이 잘리면 가로·세로 초점을 움직인다.",
        "리듬 게임을 하는 동안 노트 뒤에 깔리는 무대 그림 (한 장).",
        "게임을 켜면 처음 나오는 타이틀 화면(MainScene)의 배경 그림 (한 장). 로고는 따로 있다.\n아래에 동영상을 넣으면 그림 대신 영상이 반복 재생된다.",
    };

    private int tab;
    private Vector2 scroll;
    private GUIStyle wrapLabel, captionStyle;

    [MenuItem("Tools/CapDemo/일러스트 관리", priority = 1)]
    public static void Open()
    {
        var w = GetWindow<IllustrationManagerWindow>("일러스트 관리");
        w.minSize = new Vector2(560f, 400f);
    }

    private void OnGUI()
    {
        wrapLabel ??= new GUIStyle(EditorStyles.label) { wordWrap = true };
        captionStyle ??= new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 11, normal = { textColor = new Color(0.05f, 0.23f, 0.2f) } };

        tab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(26f));
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(Where[tab], MessageType.None);

        Object data = tab switch
        {
            1 or 2 => AssetDatabase.LoadAssetAtPath<ResultV2Settings>(ResultPath),
            _ => LoadSet(SetName(tab)),
        };

        if (!data)
        {
            DrawMissing();
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"데이터 파일: {AssetDatabase.GetAssetPath(data)}", EditorStyles.miniLabel);
            if (GUILayout.Button("파일 보기", EditorStyles.miniButton, GUILayout.Width(70f))) EditorGUIUtility.PingObject(data);
        }
        EditorGUILayout.LabelField($"이 화면에서 메모리에 올라가는 그림: 약 {Mb(TotalBytes(data)):0.0} MB", EditorStyles.miniBoldLabel);
        EditorGUILayout.Space(4f);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUI.BeginChangeCheck();
        Undo.RecordObject(data, "일러스트 바꾸기");
        switch (tab)
        {
            case 0: DrawLobby((IllustrationSet)data); break;
            case 1: DrawResultBackgrounds((ResultV2Settings)data); break;
            case 2: DrawPolaroids((ResultV2Settings)data); break;
            default: DrawSingle((IllustrationSet)data); break;
        }
        if (EditorGUI.EndChangeCheck()) Save(data);
        EditorGUILayout.EndScrollView();

        HandleDrop(data);
    }

    // ── 탭마다 ──
    private void DrawLobby(IllustrationSet set)
    {
        for (int i = 0; i < set.entries.Count; i++)
        {
            var e = set.entries[i] ??= new IllustrationSet.Entry();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawThumb(e.image, ThumbW, ThumbH, null);
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField($"{i + 1}번째", EditorStyles.boldLabel);
                    e.image = SpriteField(e.image);
                    e.showLamps = EditorGUILayout.ToggleLeft("전구 불빛 켜기", e.showLamps);
                    DrawInfo(e.image);
                }
                if (RowButtons(set.entries, i)) { Save(set); GUIUtility.ExitGUI(); }
            }
        }
        if (GUILayout.Button("+ 그림 칸 추가 (또는 그림을 이 창에 끌어다 놓기)")) set.entries.Add(new IllustrationSet.Entry());
    }

    private void DrawResultBackgrounds(ResultV2Settings s)
    {
        for (int i = 0; i < s.backgrounds.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawThumb(s.backgrounds[i], ThumbW, ThumbH, null);
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField($"{i + 1}번째", EditorStyles.boldLabel);
                    s.backgrounds[i] = SpriteField(s.backgrounds[i]);
                    DrawInfo(s.backgrounds[i]);
                }
                if (RowButtons(s.backgrounds, i)) { Save(s); GUIUtility.ExitGUI(); }
            }
        }
        if (GUILayout.Button("+ 그림 칸 추가 (또는 그림을 이 창에 끌어다 놓기)")) s.backgrounds.Add(null);
        s.avoidRepeat = EditorGUILayout.ToggleLeft("바로 전에 나온 배경·폴라로이드는 다음 번에 피하기", s.avoidRepeat);
    }

    private void DrawPolaroids(ResultV2Settings s)
    {
        for (int i = 0; i < s.polaroids.Count; i++)
        {
            var p = s.polaroids[i] ??= new ResultV2Settings.PolaroidSet();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawPolaroidPreview(p);
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField($"{i + 1}번째 묶음", EditorStyles.boldLabel);
                    p.photo = SpriteField(p.photo);
                    EditorGUILayout.LabelField("문구 (엔터로 줄바꿈, 두 줄 정도)", EditorStyles.miniLabel);
                    p.caption = EditorGUILayout.TextArea(p.caption ?? "", GUILayout.MinHeight(38f));
                    p.focusX = EditorGUILayout.Slider("가로 초점 (왼↔오)", p.focusX, 0f, 1f);
                    p.focusY = EditorGUILayout.Slider("세로 초점 (아래↔위)", p.focusY, 0f, 1f);
                    DrawInfo(p.photo);
                }
                if (RowButtons(s.polaroids, i)) { Save(s); GUIUtility.ExitGUI(); }
            }
        }
        if (GUILayout.Button("+ 폴라로이드 묶음 추가 (또는 사진을 이 창에 끌어다 놓기)")) s.polaroids.Add(new ResultV2Settings.PolaroidSet());
    }

    private void DrawSingle(IllustrationSet set)
    {
        if (set.entries.Count == 0) set.entries.Add(new IllustrationSet.Entry());
        var e = set.entries[0] ??= new IllustrationSet.Entry();
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            DrawThumb(e.image, ThumbW * 1.5f, ThumbH * 1.5f, null);
            using (new EditorGUILayout.VerticalScope())
            {
                e.image = SpriteField(e.image);
                DrawInfo(e.image);
                if (!e.image)
                    EditorGUILayout.LabelField("비어 있으면 씬 Inspector에 원래 넣어 둔 그림이 나온다.", wrapLabel);
            }
        }
        if (set.entries.Count > 1)
            EditorGUILayout.HelpBox("이 화면은 첫 번째 그림 한 장만 쓴다. 나머지 칸은 무시된다.", MessageType.Info);

        if (tab == 4) DrawTitleVideo(set);
    }

    private void DrawTitleVideo(IllustrationSet set)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("배경 동영상 (선택)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("동영상을 넣으면 위 그림 대신 영상이 소리 없이 계속 반복된다. 위 그림은 영상이 준비되기 전 첫 순간에만 보인다. 영상 첫 장면과 비슷한 그림이면 자연스럽다.", wrapLabel);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            var preview = set.video ? AssetPreview.GetAssetPreview(set.video) : null;
            var box = GUILayoutUtility.GetRect(ThumbW, ThumbH, GUILayout.Width(ThumbW), GUILayout.Height(ThumbH));
            EditorGUI.DrawRect(box, new Color(0f, 0f, 0f, 0.35f));
            if (preview) GUI.DrawTexture(Fit(box, (float)preview.width / Mathf.Max(1, preview.height)), preview);
            else GUI.Label(box, set.video ? "미리보기 준비 중" : "동영상 없음", new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter });

            using (new EditorGUILayout.VerticalScope())
            {
                set.video = (VideoClip)EditorGUILayout.ObjectField(set.video, typeof(VideoClip), false);
                if (set.video)
                {
                    string path = AssetDatabase.GetAssetPath(set.video);
                    long fileBytes = new System.IO.FileInfo(path).Length;
                    EditorGUILayout.LabelField($"{System.IO.Path.GetFileName(path)}  ·  {set.video.width}×{set.video.height}  ·  {set.video.length:0.0}초  ·  파일 {Mb(fileBytes):0.0} MB", EditorStyles.miniLabel);
                    if (fileBytes > 30L * 1024 * 1024)
                        EditorGUILayout.HelpBox("영상 파일이 30MB보다 커요. 빌드 용량이 커지니 더 짧은 루프나 낮은 화질을 고려해 보세요.", MessageType.Warning);
                    if (GUILayout.Button("동영상 빼기", GUILayout.Width(90f))) set.video = null;
                }
                else EditorGUILayout.LabelField("mp4를 이 창에 끌어다 놓거나 위 칸에 넣는다.", EditorStyles.miniLabel);
            }
        }
    }

    // ── 그리기 도우미 ──
    private static Sprite SpriteField(Sprite s) => (Sprite)EditorGUILayout.ObjectField(s, typeof(Sprite), false);

    // 비율을 지켜 박스 안에 그린다. uv가 null이면 그림 전체
    private static void DrawThumb(Sprite s, float w, float h, Rect? uv)
    {
        var box = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
        EditorGUI.DrawRect(box, new Color(0f, 0f, 0f, 0.35f));
        if (!s)
        {
            GUI.Label(box, "그림 없음", new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter });
            return;
        }
        Rect coords = uv ?? SpriteUv(s);
        float aspect = coords.width * s.texture.width / Mathf.Max(1f, coords.height * s.texture.height);
        var r = Fit(box, aspect);
        GUI.DrawTextureWithTexCoords(r, s.texture, coords);
        if (Event.current.type == EventType.MouseDown && box.Contains(Event.current.mousePosition))
        {
            EditorGUIUtility.PingObject(s);
            Event.current.Use();
        }
    }

    private void DrawPolaroidPreview(ResultV2Settings.PolaroidSet p)
    {
        const float cardW = 150f, pad = 8f;
        float photoW = cardW - pad * 2f, photoH = photoW / PolaroidPhotoAspect;
        float capH = 40f;
        var card = GUILayoutUtility.GetRect(cardW, pad + photoH + 6f + capH, GUILayout.Width(cardW));
        EditorGUI.DrawRect(card, new Color(0.925f, 0.914f, 0.875f));
        var photoRect = new Rect(card.x + pad, card.y + pad, photoW, photoH);
        EditorGUI.DrawRect(photoRect, new Color(0f, 0f, 0f, 0.25f));
        if (p.photo) GUI.DrawTextureWithTexCoords(photoRect, p.photo.texture, CoverUv(p.photo, PolaroidPhotoAspect, p.focusX, p.focusY));
        GUI.Label(new Rect(card.x + pad, photoRect.yMax + 6f, photoW, capH), (p.caption ?? "").Replace("\r", ""), captionStyle);
    }

    private void DrawInfo(Sprite s)
    {
        if (!s) return;
        var tex = s.texture;
        string path = AssetDatabase.GetAssetPath(s);
        EditorGUILayout.LabelField($"{System.IO.Path.GetFileName(path)}  ·  {tex.width}×{tex.height}  ·  약 {Mb(Bytes(tex)):0.0} MB", EditorStyles.miniLabel);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (Mathf.Max(tex.width, tex.height) > BigTextureSide)
            EditorGUILayout.HelpBox($"그림이 {BigTextureSide}보다 커요. 그림을 선택하고 Inspector의 Max Size를 {BigTextureSide}로 낮추면 메모리를 아낄 수 있어요.", MessageType.Warning);
        else if (importer && importer.textureCompression == TextureImporterCompression.Uncompressed)
            EditorGUILayout.HelpBox("압축이 꺼져 있어 메모리를 많이 써요. 그림 Inspector의 Compression을 Normal Quality로 바꾸면 줄어들어요.", MessageType.Warning);
    }

    // ▲ ▼ ✕ 버튼. 목록이 바뀌었으면 true
    private static bool RowButtons<T>(List<T> list, int i)
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(26f)))
        {
            using (new EditorGUI.DisabledScope(i == 0))
                if (GUILayout.Button("▲")) { (list[i - 1], list[i]) = (list[i], list[i - 1]); return true; }
            using (new EditorGUI.DisabledScope(i == list.Count - 1))
                if (GUILayout.Button("▼")) { (list[i + 1], list[i]) = (list[i], list[i + 1]); return true; }
            if (GUILayout.Button("✕")) { list.RemoveAt(i); return true; }
        }
        return false;
    }

    // ── 끌어다 놓기 ──
    private void HandleDrop(Object data)
    {
        var ev = Event.current;
        if (ev.type != EventType.DragUpdated && ev.type != EventType.DragPerform) return;

        var sprites = new List<Sprite>();
        VideoClip droppedVideo = null;
        foreach (var o in DragAndDrop.objectReferences)
        {
            if (o is VideoClip vc) { if (tab == 4) droppedVideo = vc; }
            else if (o is Sprite sp) sprites.Add(sp);
            else if (o is Texture2D t)
            {
                var asSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(t));
                if (asSprite) sprites.Add(asSprite);
                else if (ev.type == EventType.DragPerform)
                    Debug.LogWarning($"[일러스트 관리] {t.name}: Texture Type이 Sprite가 아니라서 넣지 못했어요. 그림 Inspector에서 Texture Type을 Sprite (2D and UI)로 바꾸고 Apply 해 주세요.", t);
            }
        }
        if (sprites.Count == 0 && !droppedVideo) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (ev.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            Undo.RecordObject(data, "일러스트 추가");
            if (droppedVideo) ((IllustrationSet)data).video = droppedVideo;
            foreach (var sp in sprites)
            {
                switch (tab)
                {
                    case 0: ((IllustrationSet)data).entries.Add(new IllustrationSet.Entry { image = sp }); break;
                    case 1: ((ResultV2Settings)data).backgrounds.Add(sp); break;
                    case 2: ((ResultV2Settings)data).polaroids.Add(new ResultV2Settings.PolaroidSet { photo = sp }); break;
                    default:
                        var set = (IllustrationSet)data;
                        if (set.entries.Count == 0) set.entries.Add(new IllustrationSet.Entry());
                        set.entries[0] = new IllustrationSet.Entry { image = sp };
                        break;
                }
                if (tab >= 3) break; // 한 장짜리 화면은 첫 그림만
            }
            Save(data);
        }
        ev.Use();
    }

    // ── 파일 ──
    private static string SetName(int t) => t switch { 0 => IllustrationSet.Lobby, 3 => IllustrationSet.Ingame, _ => IllustrationSet.Title };
    private static IllustrationSet LoadSet(string name) => AssetDatabase.LoadAssetAtPath<IllustrationSet>($"{IllustFolder}/{name}.asset");

    private void DrawMissing()
    {
        if (tab is 1 or 2)
        {
            EditorGUILayout.HelpBox($"{ResultPath} 파일이 없어요.", MessageType.Error);
            return;
        }
        string name = SetName(tab);
        EditorGUILayout.HelpBox($"{IllustFolder}/{name}.asset 파일이 없어요. 지금은 씬 Inspector에 넣어 둔 그림이 나와요.", MessageType.Info);
        if (GUILayout.Button("파일 만들기"))
        {
            if (!AssetDatabase.IsValidFolder(IllustFolder)) AssetDatabase.CreateFolder("Assets/Resources", "Illustrations");
            var set = CreateInstance<IllustrationSet>();
            AssetDatabase.CreateAsset(set, $"{IllustFolder}/{name}.asset");
            AssetDatabase.SaveAssets();
        }
    }

    private static void Save(Object data)
    {
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
    }

    // ── 크기 계산 ──
    private static long Bytes(Texture t) => t ? Profiler.GetRuntimeMemorySizeLong(t) : 0;
    private static float Mb(long bytes) => bytes / (1024f * 1024f);

    private static long TotalBytes(Object data)
    {
        var textures = new HashSet<Texture>();
        if (data is IllustrationSet set)
        {
            var list = set.Valid();
            if (set.name != IllustrationSet.Lobby && list.Count > 0) list = list.GetRange(0, 1);
            foreach (var e in list) textures.Add(e.image.texture);
        }
        else if (data is ResultV2Settings s)
        {
            foreach (var b in s.backgrounds) if (b) textures.Add(b.texture);
            foreach (var p in s.polaroids) if (p != null && p.photo) textures.Add(p.photo.texture);
        }
        long sum = 0;
        foreach (var t in textures) sum += Bytes(t);
        return sum;
    }

    private static Rect SpriteUv(Sprite s)
    {
        var r = s.textureRect;
        var t = s.texture;
        return new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height);
    }

    // ResultV2Controller.CoverUv와 같은 계산 (사진을 칸에 꽉 차게 자르기)
    private static Rect CoverUv(Sprite sprite, float targetAspect, float focusX, float focusY)
    {
        Texture2D tex = sprite.texture;
        Rect r = sprite.textureRect;
        float aspect = r.width / Mathf.Max(1f, r.height);
        float uw = 1f, vh = 1f;
        if (aspect > targetAspect) uw = targetAspect / aspect;
        else vh = aspect / targetAspect;
        float ux = (1f - uw) * focusX, vy = (1f - vh) * focusY;
        return new Rect((r.x + ux * r.width) / tex.width, (r.y + vy * r.height) / tex.height,
            uw * r.width / tex.width, vh * r.height / tex.height);
    }

    private static Rect Fit(Rect box, float aspect)
    {
        float w = box.width, h = w / aspect;
        if (h > box.height) { h = box.height; w = h * aspect; }
        return new Rect(box.x + (box.width - w) * 0.5f, box.y + (box.height - h) * 0.5f, w, h);
    }
}
