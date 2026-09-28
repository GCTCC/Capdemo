using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 에디터 상단 메뉴 Tools > CapDemo
public static class CapDemoMenu
{
    private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
    private const string TutoScenePath = "Assets/Scenes/TutoScene.unity";
    private const string HubScenePath = "Assets/Scenes/HubScene.unity";
    private const string PressAnyKeyFontPath = "Assets/neodgm SDF.asset";

    [MenuItem("Tools/CapDemo/플레이 데이터 초기화")]
    private static void ResetProgress()
    {
        SaveData.ResetProgress();
        Debug.Log("[CapDemo] 플레이 데이터를 초기화했습니다. 다음 시작은 TutoScene입니다.");
    }

    [MenuItem("Tools/CapDemo/튜토리얼 완료로 표시")]
    private static void MarkTutorialCleared()
    {
        SaveData.MarkTutorialCleared();
        Debug.Log("[CapDemo] 튜토리얼 완료로 표시했습니다. 다음 시작은 HubScene입니다.");
    }

    [MenuItem("Tools/CapDemo/플레이 데이터 확인")]
    private static void LogProgress()
    {
        Debug.Log($"[CapDemo] 튜토리얼 완료: {SaveData.IsTutorialCleared()}");
    }

    [MenuItem("Tools/CapDemo/타이틀·튜토리얼 씬 만들기")]
    private static void CreateScenes()
    {
        var existing = new[] { TitleScenePath, TutoScenePath }.Where(File.Exists).ToArray();
        if (existing.Length > 0 && !EditorUtility.DisplayDialog("씬 덮어쓰기",
                "이미 있는 씬을 새로 만듭니다:\n" + string.Join("\n", existing), "덮어쓰기", "취소"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        CreateTutoScene();
        CreateTitleScene();
        RegisterBuildScenes();
        EditorSceneManager.OpenScene(TitleScenePath);
        Debug.Log("[CapDemo] TitleScene, TutoScene을 만들고 Build Settings에 등록했습니다.");
    }

    private static void CreateTutoScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        EditorSceneManager.SaveScene(scene, TutoScenePath);
    }

    private static void CreateTitleScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        // TitleCanvas: 1920x1080 기준으로 해상도에 맞춰 늘어난다
        var canvasGo = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(CanvasGroup), typeof(TitleScreen));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // PressAnyKey: 화면 아래 가운데
        var textGo = new GameObject("PressAnyKey", typeof(RectTransform), typeof(CanvasGroup), typeof(TextMeshProUGUI),
            typeof(PressAnyKeyBlink));
        var rect = textGo.GetComponent<RectTransform>();
        rect.SetParent(canvasGo.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 150f);
        rect.sizeDelta = new Vector2(1200f, 100f);

        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = "아무 키나 눌러 시작";
        text.fontSize = 44;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PressAnyKeyFontPath);
        if (font) text.font = font;
        else Debug.LogWarning($"[CapDemo] {PressAnyKeyFontPath} 폰트를 찾지 못해 기본 폰트를 사용합니다.");

        // FadeOverlay: 화면 전체를 덮는 검은 Image. 맨 마지막 자식이라 가장 위에 그려진다
        var overlayGo = new GameObject("FadeOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        var overlayRect = overlayGo.GetComponent<RectTransform>();
        overlayRect.SetParent(canvasGo.transform, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;
        overlayGo.GetComponent<Image>().color = Color.black;

        var title = new SerializedObject(canvasGo.GetComponent<TitleScreen>());
        title.FindProperty("fadeOverlay").objectReferenceValue = overlayGo.GetComponent<CanvasGroup>();
        title.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, TitleScenePath);
    }

    private static void CreateCamera()
    {
        var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        go.transform.position = new Vector3(0f, 0f, -10f);
        var cam = go.GetComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
    }

    // TitleScene(0) → TutoScene(1) → HubScene(2) 순서로 두고, 기존에 있던 씬은 뒤에 유지한다
    private static void RegisterBuildScenes()
    {
        var ordered = new[] { TitleScenePath, TutoScenePath, HubScenePath };
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (var path in ordered)
            if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
        scenes.AddRange(EditorBuildSettings.scenes.Where(s => !ordered.Contains(s.path)));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
