#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Generation.Environment;
using Shooter.Presentation.View;
using UnityEngine;

namespace Shooter.Presentation.Rig
{
    /// <summary>
    /// ADR-124 catalog of licensed authored soldier bodies. The JSON lives at Resources/SoldierArt/soldier-skins.json
    /// beside the hash-pinned models; every model and clip it names must load, or the catalog throws (no silent
    /// fallback to the generated body).
    /// </summary>
    public sealed class SoldierSkinCatalog
    {
        public const string ResourcePath = "SoldierArt/soldier-skins";

        [Serializable] private sealed class RoleJson { public string role = "", clip = ""; }
        [Serializable] private sealed class FitJson { public string joint = ""; public float[] offset = Array.Empty<float>(); public float[] scale = Array.Empty<float>(); }
        [Serializable] private sealed class PartJson { public string model = "", renderer = ""; }
        [Serializable] private sealed class GearJson { public int helmet, vest; public bool backpack, kneePads, belt; }
        [Serializable]
        private sealed class VariantJson
        {
            public string id = "", model = "";
            public int[] teams = Array.Empty<int>();
            public GearJson gear = new GearJson();
            public string[] uniformMaterials = Array.Empty<string>(), gearMaterials = Array.Empty<string>(), skinMaterials = Array.Empty<string>(), hideRenderers = Array.Empty<string>(), hideMaterials = Array.Empty<string>(), paintRenderers = Array.Empty<string>();
            public string skinTexture = "";
            public FitJson[] gearFit = Array.Empty<FitJson>();
            public PartJson[] parts = Array.Empty<PartJson>();
        }
        [Serializable]
        private sealed class CatalogJson
        {
            public int schemaVersion;
            public float bodyHeightMeters;
            public string clipResource = "";
            public RoleJson[] roles = Array.Empty<RoleJson>();
            public string[] deaths = Array.Empty<string>();
            public VariantJson[] variants = Array.Empty<VariantJson>();
        }

        public sealed class GearFit
        {
            public Vector3 Offset = Vector3.zero;
            public Vector3 Scale = Vector3.one;
        }

        public sealed class Variant
        {
            public string Id = "";
            public GameObject Model = null!;
            public byte[] Teams = Array.Empty<byte>();
            public SoldierGearSpec Gear;
            public string[] UniformMaterials = Array.Empty<string>();
            /// <summary>Authored materials that take the team webbing/gear colour.</summary>
            public string[] GearMaterials = Array.Empty<string>();
            public string[] SkinMaterials = Array.Empty<string>();
            public string[] HideRenderers = Array.Empty<string>();
            /// <summary>Material slots whose triangles are dropped (hair under a generated helmet).</summary>
            public string[] HideMaterials = Array.Empty<string>();
            /// <summary>Bare base bodies whose surface is painted by bone region: uniform, skin, gloves, boots.</summary>
            public string[] PaintRenderers = Array.Empty<string>();
            /// <summary>Optional skin-tone texture replacing the base map of the skin materials.</summary>
            public Texture? SkinTexture;
            /// <summary>Skinned parts borrowed from other models of the same pack (rebound to this skeleton by bone name).</summary>
            public (GameObject model, string renderer)[] Parts = Array.Empty<(GameObject, string)>();
            /// <summary>Per semantic joint gear fit in character space (T-pose axes), metres.</summary>
            public readonly Dictionary<int, GearFit> Fit = new Dictionary<int, GearFit>();
        }

        public float BodyHeightMeters { get; private set; }
        public AnimationClip?[] Roles { get; } = new AnimationClip?[(int)SoldierClipRole.Count];
        public AnimationClip[] Deaths { get; private set; } = Array.Empty<AnimationClip>();
        public IReadOnlyList<Variant> Variants => variants;
        private readonly List<Variant> variants = new List<Variant>();

        public static SoldierSkinCatalog Load()
        {
            var text = Resources.Load<TextAsset>(ResourcePath)
                ?? throw new InvalidOperationException("ADR-124 soldier skin catalog missing (Resources/" + ResourcePath + ".json); no generated-body fallback.");
            return Parse(text.text);
        }

        public static SoldierSkinCatalog Parse(string json)
        {
            var data = JsonUtility.FromJson<CatalogJson>(json) ?? throw new InvalidOperationException("Malformed soldier skin catalog.");
            if (data.schemaVersion != 1) throw new InvalidOperationException("Unsupported soldier skin catalog schema " + data.schemaVersion);
            if (!(data.bodyHeightMeters > 1.4f && data.bodyHeightMeters < 2.1f)) throw new InvalidOperationException("Soldier body height out of range.");
            var catalog = new SoldierSkinCatalog { BodyHeightMeters = data.bodyHeightMeters };

            // A clip name may carry its own resource ("SoldierArt/umm/Animations#Run_Left"); otherwise clipResource.
            var sources = new Dictionary<string, AnimationClip[]>(StringComparer.Ordinal);
            AnimationClip[] From(string resource)
            {
                if (sources.TryGetValue(resource, out var found)) return found;
                found = Resources.LoadAll<AnimationClip>(resource).Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
                if (found.Length == 0) throw new InvalidOperationException("Soldier clip resource has no clips: " + resource);
                return sources[resource] = found;
            }
            // FBX takes import as "Armature|Name"; the catalog names the take after the last '|'.
            AnimationClip Clip(string reference)
            {
                int split = reference.IndexOf('#');
                string resource = split < 0 ? data.clipResource : reference.Substring(0, split), name = split < 0 ? reference : reference.Substring(split + 1);
                return From(resource).FirstOrDefault(c => c.name == name || c.name.EndsWith("|" + name, StringComparison.Ordinal))
                    ?? throw new InvalidOperationException("Soldier clip missing: " + name + " in " + resource);
            }
            foreach (var role in data.roles)
            {
                if (!Enum.TryParse(role.role, out SoldierClipRole parsed) || parsed == SoldierClipRole.Count) throw new InvalidOperationException("Unknown soldier clip role " + role.role);
                catalog.Roles[(int)parsed] = Clip(role.clip);
            }
            if (catalog.Roles[(int)SoldierClipRole.Idle] == null) throw new InvalidOperationException("Soldier catalog needs an Idle clip.");
            catalog.Deaths = data.deaths.Select(Clip).ToArray();
            if (catalog.Deaths.Length == 0) throw new InvalidOperationException("Soldier catalog needs at least one death clip.");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var v in data.variants)
            {
                if (string.IsNullOrEmpty(v.id) || !seen.Add(v.id)) throw new InvalidOperationException("Duplicate or empty soldier variant id.");
                var model = Resources.Load<GameObject>(v.model) ?? throw new InvalidOperationException("Soldier model missing: " + v.model);
                var animator = model.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid)
                    throw new InvalidOperationException("Soldier model is not a valid humanoid: " + v.model);
                var variant = new Variant
                {
                    Id = v.id, Model = model,
                    Teams = v.teams.Select(t => t == 1 || t == 2 ? (byte)t : throw new InvalidOperationException("Bad team in variant " + v.id)).ToArray(),
                    Gear = new SoldierGearSpec((byte)v.gear.helmet, (byte)v.gear.vest, v.gear.backpack, v.gear.kneePads, v.gear.belt),
                    UniformMaterials = v.uniformMaterials, GearMaterials = v.gearMaterials, SkinMaterials = v.skinMaterials, HideRenderers = v.hideRenderers, HideMaterials = v.hideMaterials, PaintRenderers = v.paintRenderers,
                    SkinTexture = string.IsNullOrEmpty(v.skinTexture) ? null : Resources.Load<Texture>(v.skinTexture) ?? throw new InvalidOperationException("Soldier skin texture missing: " + v.skinTexture)
                };
                variant.Parts = v.parts.Select(part =>
                {
                    var donor = Resources.Load<GameObject>(part.model) ?? throw new InvalidOperationException("Soldier part model missing: " + part.model);
                    if (!donor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.name == part.renderer))
                        throw new InvalidOperationException("Soldier part renderer missing: " + part.renderer + " in " + part.model);
                    return (donor, part.renderer);
                }).ToArray();
                foreach (var fit in v.gearFit)
                {
                    int joint = Array.IndexOf(Shooter.Generation.SoldierSkeleton.Names.ToArray(), fit.joint);
                    if (joint < 0) throw new InvalidOperationException("Unknown gear fit joint " + fit.joint);
                    variant.Fit[joint] = new GearFit
                    {
                        Offset = fit.offset.Length == 3 ? new Vector3(fit.offset[0], fit.offset[1], fit.offset[2]) : Vector3.zero,
                        Scale = fit.scale.Length == 3 ? new Vector3(fit.scale[0], fit.scale[1], fit.scale[2]) : Vector3.one
                    };
                }
                catalog.variants.Add(variant);
            }
            for (byte team = 1; team <= 2; team++)
                if (!catalog.variants.Any(v => v.Teams.Contains(team))) throw new InvalidOperationException("No soldier variant for team " + team);
            return catalog;
        }

        public Variant Pick(ulong entityId, byte team)
        {
            var eligible = variants.Where(v => v.Teams.Contains(team)).ToArray();
            return eligible[SoldierVariantPicker.Pick(entityId, team, eligible.Length)];
        }
    }
}
