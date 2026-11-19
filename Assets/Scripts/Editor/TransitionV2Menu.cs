using UnityEditor;
using UnityEngine;

// 에디터 상단 메뉴 Tools > CapDemo > 리듬게임 > 전환 연출 설정 만들기 (곡 선택 → 인게임, A)
// Assets/Resources/SongTransitionSettings.asset 을 만들고 글꼴·셰이더·NoCover를 채운다. 이미 있으면 빈 칸만 채운다.
// 대사(밴드 인서트), 아티스트 글씨, 드럼 클릭음, 테스트용 로딩 지연은 이 에셋의 인스펙터에서 바꾼다.
public static class TransitionV2Menu
{
    private const string AssetPath = "Assets/Resources/SongTransitionSettings.asset";
    private const string FontFolder = "Assets/Art/Fonts/IngameV2/";

    [MenuItem("Tools/CapDemo/리듬게임/전환 연출 설정 만들기 (곡 선택 → 인게임, A)", priority = 82)]
    public static void Create()
    {
        var settings = AssetDatabase.LoadAssetAtPath<SongTransitionSettings>(AssetPath);
        if (settings == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            settings = ScriptableObject.CreateInstance<SongTransitionSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
        }

        if (!settings.oxaniumBold) settings.oxaniumBold = Font("Oxanium-Bold.ttf");
        if (!settings.oxaniumSemiBold) settings.oxaniumSemiBold = Font("Oxanium-SemiBold.ttf");
        if (!settings.oxaniumExtraBold) settings.oxaniumExtraBold = Font("Oxanium-ExtraBold.ttf");
        if (!settings.sairaCondensed) settings.sairaCondensed = Font("SairaCondensed-ExtraBold.ttf");
        if (!settings.jetBrainsMono) settings.jetBrainsMono = Font("JetBrainsMono-Regular.ttf");
        if (!settings.notoSansKr) settings.notoSansKr = Font("NotoSansKR-Bold.ttf");
        if (!settings.noCover) settings.noCover = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/SongSelect/NoCover.png");
        if (!settings.discShader) settings.discShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/TransitionDisc.shader");

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Selection.activeObject = settings;
        Debug.Log("[TransitionV2] " + AssetPath + " 준비 완료. 곡 선택 v2에서 플레이를 누르면 전환 연출 A가 나와요.");
    }

    private static Font Font(string file)
    {
        string path = FontFolder + file;
        var importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
        if (importer != null && !importer.includeFontData)
        {
            importer.includeFontData = true; // 실행 중 TMP 글꼴을 만들 때 필요
            importer.SaveAndReimport();
        }
        var font = AssetDatabase.LoadAssetAtPath<Font>(path);
        if (!font) Debug.LogWarning("[TransitionV2] 글꼴이 없어요: " + path);
        return font;
    }
}
