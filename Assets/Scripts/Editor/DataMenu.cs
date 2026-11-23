using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Tools > CapDemo > 데이터
// 튜토리얼·퀘스트·구역 같은 내용 에셋(Assets/Data)을 만들고, 검사하고, 진행 저장을 테스트하는 메뉴.
// 작성 방법: Assets/Data/데이터_작성_가이드.md
public static class DataMenu
{
    private const string Menu = "Tools/CapDemo/데이터/";
    public const string DataFolder = "Assets/Data";
    private const string CatalogPath = "Assets/Resources/DataCatalog.asset";
    private const string GuidePath = DataFolder + "/데이터_작성_가이드.md";
    private static readonly Regex IdRule = new Regex(@"^[a-z0-9_]+(\.[a-z0-9_]+)*$");
    private static readonly Regex TokenRule = new Regex(@"\{([^{}]+)\}");

    // ── 열기 ──

    [MenuItem(Menu + "데이터 폴더 열기", false, 0)]
    private static void OpenFolder()
    {
        EnsureFolder(DataFolder);
        var folder = AssetDatabase.LoadAssetAtPath<Object>(DataFolder);
        Selection.activeObject = folder;
        EditorGUIUtility.PingObject(folder);
    }

    [MenuItem(Menu + "작성 가이드 열기", false, 1)]
    private static void OpenGuide()
    {
        var guide = AssetDatabase.LoadAssetAtPath<Object>(GuidePath);
        if (guide) AssetDatabase.OpenAsset(guide);
        else Debug.LogWarning($"[데이터] 작성 가이드가 없습니다: {GuidePath}");
    }

    // ── 만들기 ──

    [MenuItem(Menu + "기본 에셋 만들기 (없는 것만)", false, 20)]
    private static void CreateDefaults()
    {
        EnsureFolder(DataFolder + "/Areas");
        EnsureFolder(DataFolder + "/Quests");
        EnsureFolder(DataFolder + "/Guides");
        EnsureFolder(DataFolder + "/UI");
        int made = 0;

        // 키 아이콘 표
        made += Make<KeyIconTable>(DataFolder + "/UI/키아이콘표.asset", null, so =>
        {
            var list = so.FindProperty("entries");
            string[,] keys =
            {
                { "F", "F" }, { "W", "W" }, { "A", "A" }, { "S", "S" }, { "D", "D" },
                { "Up", "↑" }, { "Down", "↓" }, { "Left", "←" }, { "Right", "→" },
                { "Space", "SPACE" }, { "Esc", "ESC" }, { "Enter", "ENTER" },
            };
            list.arraySize = keys.GetLength(0);
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("token").stringValue = keys[i, 0];
                e.FindPropertyRelative("label").stringValue = keys[i, 1];
            }
        });

        // 거리 설정
        made += Make<DistanceSettings>(DataFolder + "/UI/거리설정.asset", null, null);

        // 조작 안내 (시안 v6 문구)
        made += Make<ControlsGuide>(DataFolder + "/UI/조작안내.asset", "guide.controls", so =>
        {
            so.FindProperty("code").stringValue = "OPERATION · B-03";
            so.FindProperty("title").stringValue = "조작 안내";
            so.FindProperty("footer").stringValue = "아무 키나 눌러 시작";
            so.FindProperty("showInScene").stringValue = "TutoScene";
            string[,] rows =
            {
                { "{A} {D} / {Left} {Right}", "걷기", "MOVE" },
                { "{F}", "말 걸기", "TALK" },
                { "{Space}", "대사 넘기기", "NEXT" },
                { "{Esc}", "설정 · 일시정지", "MENU" },
            };
            var list = so.FindProperty("rows");
            list.arraySize = rows.GetLength(0);
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                var r = list.GetArrayElementAtIndex(i);
                r.FindPropertyRelative("keys").stringValue = rows[i, 0];
                r.FindPropertyRelative("description").stringValue = rows[i, 1];
                r.FindPropertyRelative("label").stringValue = rows[i, 2];
            }
        });

        // 구역 4개 (Build Profiles에 있는 맵 씬 기준. 문구는 시안 예시)
        made += MakeArea("Area1_", "area.area1", "구역 1", "", "", true, "");
        made += MakeArea("Area2_LiveHouse", "area.livehouse", "러스트 홀", "B2 · UNLISTED", "음향 허가 · 필요 없음", false, "여긴 우리 구역");
        made += MakeArea("Area3_HomeStreet", "area.homestreet", "3번가 홈 스트리트", "OTONE 관리 구역", "음향 허가 등급 · 없음", true, "");
        made += MakeArea("Area4_BlackMarket", "area.blackmarket", "암시장", "", "", false, "");
        // 씬의 포탈에서 확인한 연결만 넣는다 (Area1 → Area2, Area3 → Area2). 나머지는 직접 채운다
        Connect("Area1_", "Area2_LiveHouse");
        Connect("Area3_HomeStreet", "Area2_LiveHouse");

        // 첫 말 걸기 안내 (바운서를 처음 만난 뒤, 다시 가까이 갔을 때 한 번)
        made += Make<GuideData>(DataFolder + "/Guides/안내_말걸기.asset", "guide.talk_first", so =>
        {
            so.FindProperty("memo").stringValue = "예시. 바운서 NPC의 NPC ID를 npc.bouncer로 적어야 동작한다";
            so.FindProperty("code").stringValue = "GUIDE · 01";
            so.FindProperty("body").stringValue = "가까이에서 {F}를 누르면\n말을 걸 수 있어요";
            so.FindProperty("trigger").enumValueIndex = (int)GuideData.Trigger.NpcRange;
            so.FindProperty("triggerId").stringValue = "npc.bouncer";
            so.FindProperty("pointAtNpcId").stringValue = "npc.bouncer";
            var show = so.FindProperty("showWhen");
            show.arraySize = 1;
            var c = show.GetArrayElementAtIndex(0);
            c.FindPropertyRelative("kind").enumValueIndex = (int)Condition.Kind.NpcMet;
            c.FindPropertyRelative("npcId").stringValue = "npc.bouncer";
        });

        // 0장 퀘스트 예시
        made += Make<QuestData>(DataFolder + "/Quests/퀘스트_ch0_입장.asset", "quest.ch0.enter_livehouse", so =>
        {
            so.FindProperty("memo").stringValue = "예시. 문구·조건을 바꿔 쓰기. 2단계 완료 조건은 아직 비어 있음";
            so.FindProperty("panelTitle").stringValue = "현재 목표";
            var start = so.FindProperty("startWhen");
            start.arraySize = 1;
            var sc = start.GetArrayElementAtIndex(0);
            sc.FindPropertyRelative("kind").enumValueIndex = (int)Condition.Kind.TutorialStep;
            sc.FindPropertyRelative("step").intValue = (int)TutorialStep.Exploring;
            sc.FindPropertyRelative("compare").enumValueIndex = (int)Condition.Compare.AtLeast;

            var steps = so.FindProperty("steps");
            steps.arraySize = 2;
            var s1 = steps.GetArrayElementAtIndex(0);
            s1.FindPropertyRelative("text").stringValue = "바운서 입장 검사";
            s1.FindPropertyRelative("talkNpcId").stringValue = "npc.bouncer";
            var done1 = s1.FindPropertyRelative("completeWhen");
            done1.arraySize = 1;
            var d1 = done1.GetArrayElementAtIndex(0);
            d1.FindPropertyRelative("kind").enumValueIndex = (int)Condition.Kind.NpcMet;
            d1.FindPropertyRelative("npcId").stringValue = "npc.bouncer";

            var s2 = steps.GetArrayElementAtIndex(1);
            s2.FindPropertyRelative("text").stringValue = "바에서 거래 상대 기다리기";
            s2.FindPropertyRelative("targetId").stringValue = "target.livehouse.bar";
            s2.FindPropertyRelative("completeWhen").arraySize = 0;
            s2.FindPropertyRelative("talkNpcId").stringValue = "";
        });
        var livehouse = AssetDatabase.LoadAssetAtPath<AreaData>(AreaPath("Area2_LiveHouse"));
        var quest = AssetDatabase.LoadAssetAtPath<QuestData>(DataFolder + "/Quests/퀘스트_ch0_입장.asset");
        if (quest && livehouse)
        {
            var so = new SerializedObject(quest);
            var target = so.FindProperty("steps").GetArrayElementAtIndex(1).FindPropertyRelative("targetArea");
            if (!target.objectReferenceValue)
            {
                target.objectReferenceValue = livehouse;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        AssetDatabase.SaveAssets();
        RebuildCatalog();
        Debug.Log($"[데이터] 기본 에셋 {made}개를 만들었습니다 ({DataFolder}). 이미 있던 것은 그대로 두었습니다.");
        OpenFolder();
    }

    private static string AreaPath(string scene) => $"{DataFolder}/Areas/구역_{scene.TrimEnd('_')}.asset";

    private static int MakeArea(string scene, string id, string display, string top, string bottom, bool controlled, string graffiti) =>
        Make<AreaData>(AreaPath(scene), id, so =>
        {
            so.FindProperty("memo").stringValue = "예시 문구. 재이가 바꿔 쓰기";
            so.FindProperty("sceneName").stringValue = scene;
            so.FindProperty("displayName").stringValue = display;
            so.FindProperty("topLine").stringValue = top;
            so.FindProperty("bottomLine").stringValue = bottom;
            so.FindProperty("controlledByOtone").boolValue = controlled;
            so.FindProperty("graffiti").stringValue = graffiti;
        });

    private static void Connect(string fromScene, string toScene)
    {
        var from = AssetDatabase.LoadAssetAtPath<AreaData>(AreaPath(fromScene));
        var to = AssetDatabase.LoadAssetAtPath<AreaData>(AreaPath(toScene));
        if (!from || !to || from.ConnectedAreas.Contains(to)) return;
        var so = new SerializedObject(from);
        var list = so.FindProperty("connectedAreas");
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = to;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 없을 때만 만든다. 만들었으면 1
    private static int Make<T>(string path, string id, System.Action<SerializedObject> fill) where T : ScriptableObject
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path)) return 0;
        var asset = ScriptableObject.CreateInstance<T>();
        if (asset is ContentAsset content && id != null) content.EditorSetId(id);
        AssetDatabase.CreateAsset(asset, path);
        if (fill != null)
        {
            var so = new SerializedObject(asset);
            fill(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(asset);
        return 1;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ── 목록 (Resources/DataCatalog) ──

    [MenuItem(Menu + "목록 새로고침", false, 21)]
    public static void RebuildCatalog()
    {
        if (!AssetDatabase.IsValidFolder(DataFolder)) return;
        var catalog = AssetDatabase.LoadAssetAtPath<DataCatalog>(CatalogPath);
        if (!catalog)
        {
            EnsureFolder("Assets/Resources");
            catalog = ScriptableObject.CreateInstance<DataCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        catalog.EditorFill(FindAll<AreaData>(), FindAll<QuestData>(), FindAll<GuideData>(), FindAll<ControlsGuide>(),
                           FindAll<KeyIconTable>().FirstOrDefault(), FindAll<DistanceSettings>().FirstOrDefault());
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    private static List<T> FindAll<T>() where T : Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(a => a)
            .OrderBy(a => AssetDatabase.GetAssetPath(a))
            .ToList();

    // ── 검사 ──

    [MenuItem(Menu + "검사", false, 40)]
    private static void Validate()
    {
        RebuildCatalog();
        int problems = 0;
        void Warn(Object context, string message)
        {
            problems++;
            Debug.LogWarning($"[데이터 검사] {(context ? context.name + ": " : "")}{message}", context);
        }

        var contents = FindAll<AreaData>().Cast<ContentAsset>().Concat(FindAll<QuestData>()).Concat(FindAll<GuideData>()).Concat(FindAll<ControlsGuide>()).ToList();
        var keyTable = FindAll<KeyIconTable>().FirstOrDefault();
        if (!keyTable) Warn(null, "키 아이콘 표가 없습니다 (기본 에셋 만들기)");
        if (!FindAll<DistanceSettings>().Any()) Warn(null, "거리 설정이 없습니다 (기본 에셋 만들기)");

        // ID
        var seen = new Dictionary<string, ContentAsset>();
        foreach (var c in contents)
        {
            if (string.IsNullOrWhiteSpace(c.Id)) { Warn(c, "ID가 비어 있습니다"); continue; }
            if (!IdRule.IsMatch(c.Id)) Warn(c, $"ID '{c.Id}'는 영문 소문자·숫자·밑줄과 점만 씁니다");
            if (seen.TryGetValue(c.Id, out var other)) Warn(c, $"ID '{c.Id}'가 {other.name}과 겹칩니다");
            else seen[c.Id] = c;
        }

        // 씬 문자열 (목표 지점·NPC ID가 어느 씬에 있는지 찾는 데 쓴다)
        var buildScenes = EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)).ToList();
        var sceneTexts = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Where(p => !p.Contains("/OldDemo/"))
            .Select(p => File.ReadAllText(p))
            .ToList();
        bool InSomeScene(string value) => sceneTexts.Any(t => t.Contains(value));

        void CheckText(Object context, string text, string where)
        {
            if (string.IsNullOrEmpty(text) || !keyTable) return;
            foreach (Match m in TokenRule.Matches(text))
                if (keyTable.Find(m.Groups[1].Value.Trim()) == null)
                    Warn(context, $"{where}의 {{{m.Groups[1].Value}}}가 키 아이콘 표에 없습니다 (글자 그대로 보임)");
        }

        void CheckConditions(Object context, List<Condition> list, string where)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                string p = list[i] != null ? list[i].Problem() : "빈 줄";
                if (p != null) Warn(context, $"{where} {i + 1}번째 조건: {p}");
                if (list[i] != null && list[i].kind == Condition.Kind.NpcMet && !string.IsNullOrEmpty(list[i].npcId) && !InSomeScene(list[i].npcId))
                    Warn(context, $"{where} {i + 1}번째 조건: NPC ID '{list[i].npcId}'를 가진 NPC가 어느 씬에도 없습니다");
            }
        }

        foreach (var a in FindAll<AreaData>())
        {
            if (string.IsNullOrEmpty(a.SceneName)) Warn(a, "Scene Name이 비어 있습니다");
            else if (!buildScenes.Contains(a.SceneName)) Warn(a, $"씬 '{a.SceneName}'이 Build Profiles 씬 목록에 없습니다");
            if (a.ConnectedAreas.Count == 0) Warn(a, "Connected Areas가 비어 있어 다른 구역 목표로 길을 찾을 수 없습니다");
        }

        foreach (var q in FindAll<QuestData>())
        {
            if (q.Steps.Count == 0) Warn(q, "단계가 하나도 없습니다");
            CheckConditions(q, q.StartWhen, "시작 조건");
            for (int i = 0; i < q.Steps.Count; i++)
            {
                var s = q.Steps[i];
                string where = $"{i + 1}단계";
                if (string.IsNullOrWhiteSpace(s.text)) Warn(q, $"{where} 문구가 비어 있습니다");
                if (s.completeWhen.Count == 0) Warn(q, $"{where} 완료 조건이 없어 이 단계에서 멈춥니다");
                CheckConditions(q, s.completeWhen, where + " 완료 조건");
                CheckText(q, s.text, where + " 문구");
                if (!string.IsNullOrEmpty(s.targetId) && !InSomeScene(s.targetId))
                    Warn(q, $"{where} 목표 지점 '{s.targetId}'(QuestTarget)이 어느 씬에도 없습니다");
                if (!string.IsNullOrEmpty(s.targetId) && !s.targetArea)
                    Warn(q, $"{where} 목표 지점이 있는 구역(Target Area)이 비어 있습니다");
                if (!string.IsNullOrEmpty(s.talkNpcId) && !InSomeScene(s.talkNpcId))
                    Warn(q, $"{where} 대화 NPC '{s.talkNpcId}'를 가진 NPC가 어느 씬에도 없습니다");
            }
            // 이어지는 퀘스트가 자기 자신으로 돌아오는지
            var chain = new HashSet<QuestData>();
            for (var n = q; n; n = n.NextQuest)
            {
                if (!chain.Add(n)) { Warn(q, "Next Quest가 돌고 돌아 다시 이어집니다 (끝나지 않음)"); break; }
            }
        }

        foreach (var g in FindAll<GuideData>())
        {
            if (string.IsNullOrWhiteSpace(g.Body)) Warn(g, "본문이 비어 있습니다");
            if ((g.When == GuideData.Trigger.NpcRange || g.When == GuideData.Trigger.ZoneEnter) && string.IsNullOrEmpty(g.TriggerId))
                Warn(g, "Trigger Id가 비어 있습니다");
            if (g.When == GuideData.Trigger.NpcRange && !string.IsNullOrEmpty(g.TriggerId) && !InSomeScene(g.TriggerId))
                Warn(g, $"NPC ID '{g.TriggerId}'를 가진 NPC가 어느 씬에도 없습니다");
            CheckConditions(g, g.ShowWhen, "띄울 조건");
            CheckConditions(g, g.HideWhen, "사라질 조건");
            CheckText(g, g.Body, "본문");
        }

        foreach (var cg in FindAll<ControlsGuide>())
        {
            if (cg.Rows.Count == 0) Warn(cg, "줄이 하나도 없습니다");
            foreach (var r in cg.Rows) CheckText(cg, r.keys, "키 칸");
            CheckText(cg, cg.Footer, "아래 문구");
            CheckConditions(cg, cg.ShowWhen, "띄울 조건");
        }

        if (problems == 0) Debug.Log("[데이터 검사] 문제 없음");
        else Debug.Log($"[데이터 검사] 확인할 것 {problems}개 (위 노란 줄을 누르면 해당 에셋이 선택됩니다)");
    }

    // ── 퀘스트 흐름 ──

    [MenuItem(Menu + "퀘스트 흐름 보기", false, 41)]
    private static void ShowQuestFlow()
    {
        var quests = FindAll<QuestData>();
        var followers = new HashSet<QuestData>(quests.Where(q => q.NextQuest).Select(q => q.NextQuest));
        var sb = new StringBuilder("[데이터] 퀘스트 흐름\n");
        foreach (var head in quests.Where(q => !followers.Contains(q)).OrderBy(q => q.Chapter))
        {
            var visited = new HashSet<QuestData>();
            for (var q = head; q && visited.Add(q); q = q.NextQuest)
            {
                sb.Append($"{q.Chapter}장 {q.Id} ({q.name})");
                if (q.ClearChapters > 0) sb.Append($"  → 끝나면 챕터 {q.ClearChapters} 클리어");
                sb.Append('\n');
                for (int i = 0; i < q.Steps.Count; i++) sb.Append($"    {i + 1}. {q.Steps[i].text}\n");
            }
        }
        Debug.Log(sb.ToString());
    }

    // ── 진행 저장 ──

    [MenuItem(Menu + "진행 확인", false, 60)]
    private static void LogProgress() => Debug.Log($"[데이터] {SaveData.Summary()}\n파일: {SaveData.FilePath}");

    [MenuItem(Menu + "저장 파일 폴더 열기", false, 61)]
    private static void RevealSave() => EditorUtility.RevealInFinder(Application.persistentDataPath);

    [MenuItem(Menu + "진행 초기화", false, 62)]
    private static void ResetProgress()
    {
        SaveData.ResetProgress();
        Debug.Log("[데이터] 진행을 초기화했습니다 (설정값은 그대로).");
    }

    [MenuItem(Menu + "지금 퀘스트 단계 건너뛰기 (Play 중)", false, 63)]
    private static void SkipStep() => Progress.DebugSkipStep();

    [MenuItem(Menu + "지금 퀘스트 단계 건너뛰기 (Play 중)", true)]
    private static bool CanSkipStep() => Application.isPlaying;

    [MenuItem(Menu + "선택한 퀘스트 처음부터 시작 (Play 중)", false, 64)]
    private static void StartSelectedQuest() => Progress.DebugStartQuest(Selection.activeObject as QuestData, 0);

    [MenuItem(Menu + "선택한 퀘스트 처음부터 시작 (Play 중)", true)]
    private static bool CanStartSelectedQuest() => Application.isPlaying && Selection.activeObject is QuestData;
}

// Assets/Data 안이 바뀌면 목록(Resources/DataCatalog)을 자동으로 다시 채운다
public class DataCatalogWatcher : AssetPostprocessor
{
    private static bool queued;

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (queued) return;
        bool touched = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
            .Any(p => p.StartsWith(DataMenu.DataFolder + "/") && p.EndsWith(".asset"));
        if (!touched) return;
        queued = true;
        EditorApplication.delayCall += () =>
        {
            queued = false;
            DataMenu.RebuildCatalog();
        };
    }
}
