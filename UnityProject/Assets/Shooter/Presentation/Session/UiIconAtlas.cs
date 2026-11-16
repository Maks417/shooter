#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Generation.Icons;
using UnityEngine;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// ADR-085 runtime icon atlas: built in memory from the code glyph library plus silhouettes traced from the
    /// packaged weapon meshes (registered by the actor view once the weapon package is decoded). No image file is
    /// loaded or shipped; the texture lives only in memory.
    /// </summary>
    public static class UiIconAtlas
    {
        private static IconAtlas? atlas;
        private static Texture2D? texture;
        private static IReadOnlyList<WeaponSilhouetteSource>? weapons;
        private static bool failed;

        /// <summary>True once weapon silhouettes from the packaged meshes are in the atlas.</summary>
        public static bool HasWeapons => weapons != null && weapons.Count > 0 && atlas != null;

        /// <summary>Atlas texture (R8, linear, bilinear) or null when the build failed.</summary>
        public static Texture2D? Texture { get { Ensure(); return texture; } }

        public static bool TryGet(string name, out IconAtlasEntry entry)
        {
            entry = default;
            Ensure();
            return atlas != null && atlas.TryGet(name, out entry);
        }

        /// <summary>Rebuilds the atlas with silhouettes of the packaged weapon meshes.</summary>
        public static void RegisterWeapons(IReadOnlyList<WeaponSilhouetteSource> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            if (atlas != null && SameSources(weapons, sources)) return;
            weapons = sources;
            failed = false;
            Rebuild();
        }

        // Menu showcase and gameplay both decode the same package; LOD0 silhouettes are identical.
        private static bool SameSources(IReadOnlyList<WeaponSilhouetteSource>? a, IReadOnlyList<WeaponSilhouetteSource> b)
        {
            if (a == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].MeshKey != b[i].MeshKey || a[i].Meshes.Count != b[i].Meshes.Count) return false;
                for (int m = 0; m < a[i].Meshes.Count; m++)
                    if (a[i].Meshes[m].Positions.Count != b[i].Meshes[m].Positions.Count ||
                        a[i].Meshes[m].Indices.Count != b[i].Meshes[m].Indices.Count) return false;
            }
            return true;
        }

        private static void Ensure()
        {
            if (atlas != null || failed) return;
            Rebuild();
        }

        private static void Rebuild()
        {
            try
            {
                var started = Time.realtimeSinceStartup;
                var built = IconAtlas.Build(IconLibrary.All, weapons);
                var tex = texture;
                if (tex == null || tex.width != built.Width || tex.height != built.Height)
                {
                    if (tex != null)
                    {
                        if (Application.isPlaying) UnityEngine.Object.Destroy(tex);
                        else UnityEngine.Object.DestroyImmediate(tex);
                    }
                    tex = new Texture2D(built.Width, built.Height, TextureFormat.R8, false, true)
                    {
                        name = "UiIconAtlas",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
                // Atlas rows are top-down; Unity texture rows are bottom-up.
                var flipped = new byte[built.Pixels.Length];
                for (int y = 0; y < built.Height; y++)
                    Buffer.BlockCopy(built.Pixels, y * built.Width, flipped, (built.Height - 1 - y) * built.Width, built.Width);
                tex.LoadRawTextureData(flipped);
                tex.Apply(false, true);
                texture = tex;
                atlas = built;
                Debug.Log("SHOOTER_UI_ICONS ready icons=" + built.Names.Count + " weapons=" + (weapons?.Count ?? 0) +
                          " size=" + built.Width + "x" + built.Height + " ms=" + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0"));
            }
            catch (Exception e)
            {
                failed = true;
                atlas = null;
                Debug.LogWarning("SHOOTER_UI_ICONS unavailable: " + e.Message);
            }
        }
    }
}
