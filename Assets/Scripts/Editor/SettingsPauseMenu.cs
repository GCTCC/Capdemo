using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 에디터 상단 메뉴 Tools > CapDemo > 설정·일시정지
//   1. 프리팹 만들기: 설정 창·일시정지 창·Auto/일시정지 버튼을 Assets/Prefabs/UI/에 만든다 (다시 실행하면 새로 덮어쓴다)
//   2. 허브 '설정' 버튼에 설정 창 연결: HoloHubScene의 설정 메뉴가 설정 창을 띄우게 한다
//   3. 스토리·맵 대화에 Auto·일시정지 버튼 붙이기: TutoScene, MapStoryCanvas·DialogueCanvas 프리팹에 버튼을 넣는다
// 모양(글꼴·패널 그림)은 홀로그램 허브와 같은 재료를 쓴다.
public static class SettingsPauseMenu
{
    private const string Folder = "Assets/Prefabs/UI/";
    public const string SettingsPrefabPath = Folder + "SettingsPanel.prefab";
    public const string PausePrefabPath = Folder + "PausePanel.prefab";
    public const string ControlsPrefabPath = Folder + "StoryControls.prefab";

    private const string TitleFontPath = "Assets/Art/Fonts/경기천년제목_Bold SDF.asset";
    private const string MonoFontPath = "Assets/Art/Fonts/neodgm SDF.asset";
    private const string OpenSfxPath = "Assets/Audio/SFX/SFX_Hologram_On.wav";
    private const string CloseSfxPath = "Assets/Audio/SFX/SFX_Hologram_Off.wav";
    private const string PreviewSfxPath = "Assets/Audio/SFX/SFX_BouncerScan_OK.wav";
    private const string PreviewBgmFolder = "Assets/Music";

    private const string HubScenePath = "Assets/Scenes/HoloHubScene.unity";
    private const string TutoScenePath = "Assets/Scenes/TutoScene.unity";
    private static readonly string[] StoryPrefabPaths = { "Assets/Prefabs/MapStoryCanvas.prefab", "Assets/Prefabs/Map/DialogueCanvas.prefab" };

    // ── 1. 프리팹 만들기 ──────────────────────────────────────────

    [MenuItem("Tools/CapDemo/설정·일시정지/1. 프리팹 만들기 (설정 창·일시정지 창·Auto 버튼)", priority = 0)]
    private static void CreatePrefabs()
    {
        var style = LoadStyle();
        if (style == null) return;
        Directory.CreateDirectory(Folder);

        // 일시정지 창 → Auto 버튼(일시정지 창을 가리킴) → 설정 창 순서
        var pause = PausePanel.Build(style);
        var pausePrefab = Save(pause.gameObject, PausePrefabPath).GetComponent<PausePanel>();

        var controls = StoryControls.Build(style, pausePrefab);
        Save(controls.gameObject, ControlsPrefabPath);

        var settings = SettingsPanel.Build(style, FindPreviewBgm(), AssetDatabase.LoadAssetAtPath<AudioClip>(PreviewSfxPath));
        Save(settings.gameObject, SettingsPrefabPath);

        AssetDatabase.SaveAssets();
        Debug.Log($"[설정·일시정지] 프리팹을 만들었어요: {SettingsPrefabPath}, {PausePrefabPath}, {ControlsPrefabPath}\n" +
                  "다음: 2번(허브 설정 버튼 연결), 3번(스토리·맵 대화에 버튼 붙이기) 메뉴를 실행하세요.");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(SettingsPrefabPath));
    }

    private static GameObject Save(GameObject go, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static HoloUIStyle LoadStyle()
    {
        if (!HoloHubArt.Load("panel_tlbr14") || !HoloHubArt.Load("panel_br10")) HoloHubArt.GenerateAll();
        var style = new HoloUIStyle
        {
            titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath),
            monoFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MonoFontPath),
            panel = HoloHubArt.Load("panel_tlbr14"),
            panelLine = HoloHubArt.Load("panel_tlbr14_line"),
            button = HoloHubArt.Load("panel_br10"),
            buttonLine = HoloHubArt.Load("panel_br10_line"),
            openSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(OpenSfxPath),
            closeSfx = AssetDatabase.LoadAssetAtPath<AudioClip>(CloseSfxPath),
        };
        if (!style.titleFont || !style.monoFont)
        {
            EditorUtility.DisplayDialog("글꼴 없음", $"허브와 같은 글꼴을 찾지 못했어요.\n{TitleFontPath}\n{MonoFontPath}", "확인");
            return null;
        }
        FixFonts(style.titleFont, style.monoFont);
        if (!style.panel) Debug.LogWarning("[설정·일시정지] 허브 패널 그림을 찾지 못해 네모 패널로 만듭니다.");
        return style;
    }

    // 글자가 네모(□)로 나오지 않게:
    //  1) 글자판 여러 장 쓰기: 한글이 많으면 글자판 한 장이 꽉 차서 새 글자가 네모로 나온다 → 꽉 차면 새 장을 쓴다
    //  2) neodgm(작은 영문·숫자 글꼴)에 없는 글자(← → 등)는 경기천년제목에서 빌려 온다
    private static void FixFonts(TMP_FontAsset title, TMP_FontAsset mono)
    {
        foreach (var f in new[] { title, mono })
        {
            if (!f || f.isMultiAtlasTexturesEnabled) continue;
            f.isMultiAtlasTexturesEnabled = true;
            EditorUtility.SetDirty(f);
            Debug.Log($"[설정·일시정지] {f.name}: 글자판 여러 장 쓰기(Multi Atlas Textures)를 켰어요.");
        }
        if (title && mono && title != mono)
        {
            mono.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (!mono.fallbackFontAssetTable.Contains(title))
            {
                mono.fallbackFontAssetTable.Add(title);
                EditorUtility.SetDirty(mono);
                Debug.Log($"[설정·일시정지] {mono.name}에 없는 글자는 {title.name}에서 빌려 오게 했어요 (Fallback).");
            }
        }
        AssetDatabase.SaveAssets();
    }

    // 배경음 미리듣기 곡: Assets/Music 폴더의 첫 곡
    private static AudioClip FindPreviewBgm()
    {
        if (!AssetDatabase.IsValidFolder(PreviewBgmFolder)) return null;
        var guid = AssetDatabase.FindAssets("t:AudioClip", new[] { PreviewBgmFolder }).FirstOrDefault();
        return guid == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
    }

    // ── 2. 허브 설정 버튼 연결 ────────────────────────────────────

    [MenuItem("Tools/CapDemo/설정·일시정지/2. 허브 '설정' 버튼에 설정 창 연결", priority = 1)]
    private static void HookHubSettings()
    {
        var panel = AssetDatabase.LoadAssetAtPath<SettingsPanel>(SettingsPrefabPath);
        if (!panel)
        {
            EditorUtility.DisplayDialog("설정 창 없음", "먼저 '1. 프리팹 만들기'를 실행하세요.", "확인");
            return;
        }
        if (!OpenScene(HubScenePath)) return;

        int count = 0;
        foreach (var item in Object.FindObjectsByType<HoloMenuItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item.name != "Item_SETTINGS") continue;
            var so = new SerializedObject(item);
            so.FindProperty("panelPrefab").objectReferenceValue = panel;
            so.ApplyModifiedProperties();
            count++;
        }
        if (count == 0)
        {
            EditorUtility.DisplayDialog("설정 버튼 없음", "HoloHubScene에서 'Item_SETTINGS' 메뉴를 찾지 못했어요.", "확인");
            return;
        }
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[설정·일시정지] HoloHubScene의 '설정' 메뉴에 설정 창을 연결하고 씬을 저장했어요. Play → 설정을 눌러 보세요.");
    }

    // ── 3. 스토리·맵 대화에 Auto·일시정지 버튼 ─────────────────────

    [MenuItem("Tools/CapDemo/설정·일시정지/3. 스토리·맵 대화에 Auto·일시정지 버튼 붙이기", priority = 2)]
    private static void AttachStoryControls()
    {
        var controls = AssetDatabase.LoadAssetAtPath<GameObject>(ControlsPrefabPath);
        if (!controls)
        {
            EditorUtility.DisplayDialog("버튼 프리팹 없음", "먼저 '1. 프리팹 만들기'를 실행하세요.", "확인");
            return;
        }

        var done = new List<string>();

        // 맵 위 스토리(MapStoryCanvas)와 맵 NPC 대화(DialogueCanvas) 프리팹: 프리팹을 고치면 모든 맵 씬에 들어간다
        foreach (var path in StoryPrefabPaths)
        {
            if (!File.Exists(path)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            if (AddTo(root.GetComponentInChildren<Canvas>(true), controls)) done.Add(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 챕터 씬(TutoScene)의 StoryCanvas
        if (File.Exists(TutoScenePath) && OpenScene(TutoScenePath))
        {
            bool added = false;
            foreach (var manager in Object.FindObjectsByType<StoryManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var canvas = manager.GetComponentInParent<Canvas>(true);
                if (!canvas || PrefabUtility.IsPartOfPrefabInstance(canvas)) continue; // 프리팹은 위에서 처리
                added |= AddTo(canvas.rootCanvas ? canvas.rootCanvas : canvas, controls);
            }
            var scene = SceneManager.GetActiveScene();
            if (added)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                done.Add(TutoScenePath);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log(done.Count > 0
            ? "[설정·일시정지] Auto·일시정지 버튼을 붙였어요: " + string.Join(", ", done)
            : "[설정·일시정지] 새로 붙일 곳이 없어요 (이미 붙어 있음).");
    }

    // canvas 맨 위(마지막 자식)에 버튼을 넣는다. 이미 있으면 맨 위로만 옮긴다
    private static bool AddTo(Canvas canvas, GameObject controlsPrefab)
    {
        if (!canvas) return false;
        if (!canvas.GetComponent<GraphicRaycaster>()) canvas.gameObject.AddComponent<GraphicRaycaster>(); // 버튼 클릭을 받으려면 필요
        var existing = canvas.GetComponentInChildren<StoryControls>(true);
        if (existing)
        {
            existing.transform.SetAsLastSibling();
            return false;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(controlsPrefab, canvas.transform);
        go.transform.SetAsLastSibling();
        return true;
    }

    private static bool OpenScene(string path)
    {
        if (SceneManager.GetActiveScene().path == path) return true;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return true;
    }
}
