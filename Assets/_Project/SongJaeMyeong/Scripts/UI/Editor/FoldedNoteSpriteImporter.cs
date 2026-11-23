using System;
using UnityEditor;

namespace OpenSourceTeam6.UI.Editor
{
    /// <summary>
    /// 우상단 접힌 쪽지 아이콘(Spr_FoldedNote.png)이 항상 투명 배경 Sprite로 임포트되도록 보정한다.
    /// </summary>
    public sealed class FoldedNoteSpriteImporter : AssetPostprocessor
    {
        private const string TargetAssetPath = "Assets/_Project/SongJaeMyeong/Resources/UI/Spr_FoldedNote.png";

        private void OnPreprocessTexture()
        {
            if (!string.Equals(assetPath, TargetAssetPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 512;
        }
    }
}
