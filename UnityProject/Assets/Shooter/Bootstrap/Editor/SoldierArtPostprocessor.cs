#nullable enable
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Shooter.Build
{
    /// <summary>
    /// ADR-124: import settings for licensed authored soldier art are code, not hand-tuned inspector state, so the
    /// committed .meta files are reproducible. Models under Assets/Shooter/Art/Soldiers import as humanoids with
    /// visible bones and embedded materials; their clips import in place (root baked into the pose) and loop when the
    /// clip name says so; textures import as mip-mapped sRGB colour or normal maps by name.
    /// </summary>
    public sealed class SoldierArtPostprocessor : AssetPostprocessor
    {
        public const string Root = "Assets/Shooter/Art/Soldiers/";
        public override uint GetVersion() => 2;

        private static bool Owned(string path) => path.Replace('\\', '/').StartsWith(Root, StringComparison.Ordinal);

        private void OnPreprocessModel()
        {
            if (!Owned(assetPath) || assetImporter is not ModelImporter importer) return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            // Readable: the runtime paints bare base bodies by bone region and bakes the T-pose to measure height.
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            // Low-poly packs ship hard per-face normals; recompute them so curved surfaces shade smoothly while real
            // creases (soles, visors, plate edges) above the angle stay hard.
            importer.importNormals = ModelImporterNormals.Calculate;
            importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
            importer.normalSmoothingSource = ModelImporterNormalSmoothingSource.FromAngle;
            importer.normalSmoothingAngle = 70f;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        private void OnPreprocessAnimation()
        {
            if (!Owned(assetPath) || assetImporter is not ModelImporter importer) return;
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                bool loop = Loops(clip.name);
                clip.loopTime = loop;
                clip.loopPose = loop;
                // In place: the body follows the authoritative feet, never root motion.
                clip.lockRootRotation = true; clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true; clip.keepOriginalPositionY = true; clip.heightFromFeet = false;
                clip.lockRootPositionXZ = true; clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }

        /// <summary>Clip names that cycle (locomotion, idles); one-shots such as deaths and jumps do not.</summary>
        public static bool Loops(string clipName)
        {
            string n = clipName.ToLowerInvariant();
            if (new[] { "death", "die", "dead", "hit", "jump_start", "jump_land", "land", "start", "stop", "reload", "shoot" }.Any(n.Contains)) return false;
            return new[] { "loop", "idle", "walk", "run", "jog", "sprint", "crouch", "strafe", "fall" }.Any(n.Contains);
        }

        private void OnPreprocessTexture()
        {
            if (!Owned(assetPath) || assetImporter is not TextureImporter importer) return;
            string n = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            bool normal = n.EndsWith("_normal") || n.EndsWith("_n") || n.Contains("normal");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
        }
    }
}
