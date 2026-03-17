using UnityEditor;
using UnityEngine;

namespace RhythmRogue.Editor
{
    /// <summary>
    /// Automatically configures imported textures for pixel art:
    ///   - Filter Mode → Point (no filter)
    ///   - Compression → None
    ///   - Sprite Pixels Per Unit → 16 (common pixel art tile size)
    ///
    /// Runs on every texture imported under Assets/_Project/.
    /// Place this script in an Editor folder (e.g. Assets/_Project/Scripts/Editor/).
    /// </summary>
    public class SpriteImportDefaults : AssetPostprocessor
    {
        private const string PixelArtRoot = "Assets/_Project/";
        private const int DefaultPixelsPerUnit = 32;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PixelArtRoot))
                return;

            TextureImporter importer = (TextureImporter)assetImporter;

            // Point filtering — no bilinear/trilinear smoothing
            importer.filterMode = FilterMode.Point;

            // No compression — preserves exact pixel colors
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // Sprite settings
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = DefaultPixelsPerUnit;

            // Disable mipmaps — pixel art doesn't need them
            importer.mipmapEnabled = false;

            // Ensure max size is reasonable for pixel art
            importer.maxTextureSize = 2048;

            // No power-of-two padding
            importer.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
