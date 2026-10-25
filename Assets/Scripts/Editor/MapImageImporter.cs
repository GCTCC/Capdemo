using UnityEditor;
using UnityEngine;

// Assets/image/Map 아래 폴더에 그림을 넣으면 처음 한 번 도트용 스프라이트 설정을 자동으로 적용한다.
// 이미 들어 있던 그림의 설정을 인스펙터에서 바꾸면 그 설정을 그대로 둔다.
//   Backgrounds: 크기는 MapBackground가 화면 높이에 맞추므로 PPU는 상관없다. 피벗은 가운데.
//   Objects, NPCs: 플레이어 그림과 같은 PPU 128(256px 그림이 2유닛). 피벗은 발밑(아래 가운데)이라 바닥에 놓기 쉽다.
public class MapImageImporter : AssetPostprocessor
{
    public const string Root = "Assets/image/Map";
    public const string BackgroundFolder = Root + "/Backgrounds";
    public const string ObjectFolder = Root + "/Objects";
    public const string NpcFolder = Root + "/NPCs";

    private const float PixelsPerUnit = 128f;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root + "/") || !assetImporter.importSettingsMissing) return;

        var importer = (TextureImporter)assetImporter;
        bool background = assetPath.StartsWith(BackgroundFolder + "/");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = background ? 100f : PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 8192;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)(background ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
        importer.SetTextureSettings(settings);
    }
}
