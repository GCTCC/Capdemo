using System;
using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;

// Unity의 Asset Pipeline에 원래 확장자를 등록하는 자동 어댑터.
// 선택/해석/ChartData 변환 규칙은 ChartAutoLoader 안에 둔다.
// OSZ에서 .osu 텍스트만 등록하므로 압축 안의 음악은 Resources에 중복 포함되지 않는다.
[ScriptedImporter(1, new[] { "osu", "osz" })]
public class OsuManiaChartImporter : ScriptedImporter
{
    [SerializeField, Tooltip("OSZ 내부 .osu 엔트리 전체 이름. 비워두면 이름순 첫 6K 난이도. 변경 후 Apply.")]
    private string beatmapEntryName = "";

    public override void OnImportAsset(AssetImportContext ctx)
    {
        ChartData validationChart = null;
        try
        {
            string text;
            string error;
            bool zip = Path.GetExtension(ctx.assetPath).Equals(".osz", StringComparison.OrdinalIgnoreCase);
            string selected = null;
            if (zip)
            {
                if (!ChartAutoLoader.TryReadOszChartText(File.ReadAllBytes(ctx.assetPath), out text,
                    out selected, out error, beatmapEntryName))
                { ctx.LogImportError(error); return; }
            }
            else
            {
                if (new FileInfo(ctx.assetPath).Length > 16 * 1024 * 1024)
                { ctx.LogImportError("osu 파일이 16MiB를 넘습니다."); return; }
                text = File.ReadAllText(ctx.assetPath, Encoding.UTF8);
            }
            validationChart = ScriptableObject.CreateInstance<ChartData>();
            if (!ChartAutoLoader.TryApplyOsu(text, validationChart, out error))
            { ctx.LogImportError(error); return; }

            // main asset의 이름 재설정에 의존하지 않고 원본 이름/형식을 함께 보존한다.
            string sourceName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            var asset = new TextAsset(ChartAutoLoader.CreateOsuSourceAssetText(
                text, sourceName, Path.GetExtension(ctx.assetPath))) { name = sourceName };
            ctx.AddObjectToAsset("chart", asset);
            ctx.SetMainObject(asset);
            Debug.Log("[osu 자동 등록] " + ctx.assetPath
                + (selected == null ? "" : " → " + selected)
                + " / " + validationChart.notes.Count + " notes. 동명 외부 음악과 연결됩니다.");
        }
        catch (Exception ex)
        {
            ctx.LogImportError("osu!mania 채보 등록 실패: " + ex.Message);
        }
        finally
        {
            if (validationChart != null) UnityEngine.Object.DestroyImmediate(validationChart);
        }
    }
}
