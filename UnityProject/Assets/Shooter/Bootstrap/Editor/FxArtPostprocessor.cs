#nullable enable
using System;
using UnityEditor;

namespace Shooter.Build
{
    /// <summary>
    /// ADR-126: import settings for the licensed effect flipbooks under Assets/Shooter/Art/Fx are code, so the committed
    /// .meta files are reproducible. Sheets import as sRGB colour with their own alpha, mip-mapped, clamped (frames must
    /// not bleed across the atlas edge) and capped at 1024 so the 2048-wide sheets cost the same memory as the 1024 ones.
    /// </summary>
    public sealed class FxArtPostprocessor : AssetPostprocessor
    {
        public const string Root = "Assets/Shooter/Art/Fx/";
        public const int MaxSize = 1024;
        public override uint GetVersion() => 1;

        private static bool Owned(string path) => path.Replace('\\', '/').StartsWith(Root, StringComparison.Ordinal);

        private void OnPreprocessTexture()
        {
            if (!Owned(assetPath) || assetImporter is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.anisoLevel = 0;
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
