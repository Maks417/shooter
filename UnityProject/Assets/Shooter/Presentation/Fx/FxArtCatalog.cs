#if UNITY_5_3_OR_NEWER
#nullable enable
using System;
using System.Linq;
using UnityEngine;

namespace Shooter.Presentation.Fx
{
    /// <summary>
    /// ADR-126 catalog of licensed effect flipbooks. The JSON lives at Resources/FxArt/fx-art.json beside the
    /// hash-pinned sheets; every role must name a sheet that loads, or the catalog throws (no silent fallback to the
    /// generated flipbooks).
    /// </summary>
    public sealed class FxArtCatalog
    {
        public const string ResourcePath = "FxArt/fx-art";

        [Serializable]
        private sealed class SheetJson
        {
            public string role = "", texture = "", blend = "";
            public int columns, rows;
            public float framesPerSecond, brightness, lit, emission;
        }
        [Serializable] private sealed class CatalogJson { public int schemaVersion; public SheetJson[] sheets = Array.Empty<SheetJson>(); }

        public FxArtSheet[] Sheets { get; private set; } = Array.Empty<FxArtSheet>();
        public Texture2D[] Textures { get; private set; } = Array.Empty<Texture2D>();

        public static FxArtCatalog Load()
        {
            var text = Resources.Load<TextAsset>(ResourcePath)
                ?? throw new InvalidOperationException("ADR-126 effect flipbook catalog missing (Resources/" + ResourcePath + ".json); no generated fallback.");
            return Parse(text.text);
        }

        public static FxArtCatalog Parse(string json)
        {
            var data = JsonUtility.FromJson<CatalogJson>(json) ?? throw new InvalidOperationException("Malformed effect flipbook catalog.");
            if (data.schemaVersion != FxArtRoles.SchemaVersion) throw new InvalidOperationException("Unsupported effect flipbook catalog schema " + data.schemaVersion);
            var sheets = FxArtRoles.RequireAll(data.sheets.Select(s => new FxArtSheet(FxArtRoles.ParseRole(s.role), s.texture, s.columns, s.rows,
                FxArtRoles.ParseBlend(s.blend), s.framesPerSecond, s.brightness, s.lit, s.emission)));
            var textures = sheets.Select(s => Resources.Load<Texture2D>(s.Resource)
                ?? throw new InvalidOperationException("Effect flipbook texture missing: " + s.Resource)).ToArray();
            return new FxArtCatalog { Sheets = sheets, Textures = textures };
        }
    }
}
#endif
