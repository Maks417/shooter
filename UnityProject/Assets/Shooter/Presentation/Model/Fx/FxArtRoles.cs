#nullable enable
using System;
using System.Collections.Generic;
using Shooter.Contracts;

namespace Shooter.Presentation.Fx
{
    /// <summary>ADR-126 licensed effect flipbooks, one per role. The catalog must name every role (no generated fallback).</summary>
    public enum FxArtRole : byte
    {
        /// <summary>Grenade fireball that burns out into dark smoke (Explosion01, plays once).</summary>
        BlastFireball = 0,
        /// <summary>Second explosion sheet so two fireballs in one blast do not look identical (Explosion02).</summary>
        BlastFireballAlt = 1,
        /// <summary>Looping hot fireball: blast fire puffs and the muzzle fire ball (FireBall01, additive).</summary>
        FireBall = 2,
        /// <summary>Looping grey puff: gun smoke, blast smoke column, impact and footstep dust (WispySmoke01, tinted).</summary>
        Smoke = 3,
        /// <summary>Looping flame column for burning wrecks (Flame02).</summary>
        Flame = 4,
        /// <summary>Looping rising smoke column above burning wrecks (DiscSmoke01).</summary>
        SmokePlume = 5,
        Count = 6
    }

    public enum FxArtBlend : byte { Alpha = 0, Additive = 1 }

    /// <summary>One validated flipbook sheet from the ADR-126 catalog.</summary>
    public readonly struct FxArtSheet
    {
        public FxArtRole Role { get; }
        public string Resource { get; }
        public int Columns { get; }
        public int Rows { get; }
        public FxArtBlend Blend { get; }
        /// <summary>Playback rate of looping sheets (frames per second); one-shot sheets span the particle lifetime.</summary>
        public float FramesPerSecond { get; }
        public float Brightness { get; }
        /// <summary>0..1: how much scene ambient and sun colour the non-glowing part of the sheet takes.</summary>
        public float Lit { get; }
        /// <summary>HDR boost of the fiery (red over blue) texels, so fire glows while its smoke stays lit.</summary>
        public float Emission { get; }
        public int Frames => Columns * Rows;

        public FxArtSheet(FxArtRole role, string resource, int columns, int rows, FxArtBlend blend, float framesPerSecond, float brightness, float lit, float emission)
        {
            if (role >= FxArtRole.Count) throw new ArgumentOutOfRangeException(nameof(role));
            if (string.IsNullOrWhiteSpace(resource)) throw new ArgumentException("Flipbook resource missing for " + role, nameof(resource));
            if (columns < 1 || columns > 16 || rows < 1 || rows > 16) throw new ArgumentOutOfRangeException(nameof(columns), "Flipbook grid out of range for " + role);
            if (!(framesPerSecond > 0f && framesPerSecond <= 120f)) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
            if (!(brightness > 0f && brightness <= 8f)) throw new ArgumentOutOfRangeException(nameof(brightness));
            if (!(lit >= 0f && lit <= 1f)) throw new ArgumentOutOfRangeException(nameof(lit));
            if (!(emission >= 0f && emission <= 8f)) throw new ArgumentOutOfRangeException(nameof(emission));
            Role = role; Resource = resource; Columns = columns; Rows = rows; Blend = blend;
            FramesPerSecond = framesPerSecond; Brightness = brightness; Lit = lit; Emission = emission;
        }
    }

    public static class FxArtRoles
    {
        public const int SchemaVersion = 1;

        public static FxArtRole ParseRole(string name)
        {
            if (!Enum.TryParse(name, false, out FxArtRole role) || role >= FxArtRole.Count || name != role.ToString())
                throw new InvalidOperationException("Unknown effect flipbook role " + name);
            return role;
        }

        public static FxArtBlend ParseBlend(string name) => name switch
        {
            "Alpha" => FxArtBlend.Alpha,
            "Additive" => FxArtBlend.Additive,
            _ => throw new InvalidOperationException("Unknown effect flipbook blend " + name)
        };

        /// <summary>Orders the sheets by role and throws unless every role appears exactly once.</summary>
        public static FxArtSheet[] RequireAll(IEnumerable<FxArtSheet> sheets)
        {
            var result = new FxArtSheet[(int)FxArtRole.Count];
            var seen = new bool[result.Length];
            foreach (var sheet in sheets)
            {
                int k = (int)sheet.Role;
                if (seen[k]) throw new InvalidOperationException("Duplicate effect flipbook role " + sheet.Role);
                seen[k] = true; result[k] = sheet;
            }
            for (int k = 0; k < seen.Length; k++)
                if (!seen[k]) throw new InvalidOperationException("Effect flipbook catalog misses role " + (FxArtRole)k + "; no generated fallback.");
            return result;
        }

        /// <summary>Explosion sheets play once over the particle life; the others loop at their own frame rate.</summary>
        public static bool Loops(FxArtRole role) => role != FxArtRole.BlastFireball && role != FxArtRole.BlastFireballAlt;

        /// <summary>
        /// Licensed sheet that draws a pool instance, or null for the kinds that keep their generated look (crisp muzzle
        /// star, tracers, casings, spark streaks, embers, debris chips, shockwave ring, flash core).
        /// </summary>
        public static FxArtRole? For(FxKind kind, int slot) => kind switch
        {
            FxKind.Fireball => (slot & 1) == 0 ? FxArtRole.BlastFireball : FxArtRole.BlastFireballAlt,
            FxKind.FirePuff => FxArtRole.FireBall,
            FxKind.MuzzleFire => FxArtRole.FireBall,
            FxKind.SmokeProxy => FxArtRole.Smoke,
            FxKind.Dust => FxArtRole.Smoke,
            FxKind.DustRing => FxArtRole.Smoke,
            _ => null
        };

        /// <summary>
        /// Texture-sheet playback for a particle that lives <paramref name="lifetime"/> seconds: whole loop cycles plus
        /// the fraction of the sheet each cycle covers (curve end value, at most 1, so the curve never wraps).
        /// </summary>
        public static (int cycles, float curveEnd) Playback(in FxArtSheet sheet, float lifetime)
        {
            if (!Loops(sheet.Role) || !(lifetime > 0f)) return (1, 1f);
            float loops = lifetime * sheet.FramesPerSecond / sheet.Frames;
            int cycles = Math.Max(1, (int)Math.Ceiling(loops - 1e-4f));
            return (cycles, Math.Min(1f, loops / cycles));
        }
    }

    /// <summary>A map object that may burn: a vehicle envelope (centre, size, front sign) from the map plan.</summary>
    public readonly struct AmbientFireSite
    {
        public Float3 Center { get; }
        public Float3 Size { get; }
        public float Facing { get; }
        public AmbientFireSite(Float3 center, Float3 size, float facing) { Center = center; Size = size; Facing = facing >= 0f ? 1f : -1f; }
    }

    /// <summary>Where a cosmetic fire burns: base on top of the object toward its front, flame scale in metres.</summary>
    public readonly struct AmbientFireAnchor
    {
        public Float3 Base { get; }
        public float Scale { get; }
        public int Site { get; }
        public AmbientFireAnchor(Float3 @base, float scale, int site) { Base = @base; Scale = scale; Site = site; }
    }

    /// <summary>
    /// ADR-126 burning wrecks: a few map vehicles burn, picked by a stable hash of the map id so every match on a map
    /// burns the same ones. Presentation only (no damage, collision, navigation or bot sight change); the smoke plume
    /// rises from the roof so it never hides a standing soldier on one side only.
    /// </summary>
    public static class AmbientFireAnchors
    {
        public const int MaxFires = 4;
        public const int MaxFiresLow = 2;
        public const float MinSpacingMeters = 18f;

        public static int Budget(int qualityTier) => qualityTier <= 0 ? MaxFiresLow : MaxFires;

        public static AmbientFireAnchor[] Pick(string mapId, IReadOnlyList<AmbientFireSite> sites, int qualityTier)
        {
            if (mapId == null) throw new ArgumentNullException(nameof(mapId));
            if (sites == null) throw new ArgumentNullException(nameof(sites));
            int budget = Budget(qualityTier);
            var order = new int[sites.Count];
            var keys = new uint[sites.Count];
            uint seed = Fnv(mapId);
            for (int i = 0; i < order.Length; i++) { order[i] = i; keys[i] = Mix(seed ^ (uint)(i * 0x9E3779B9u)); }
            Array.Sort(keys, order);
            var picked = new List<AmbientFireAnchor>(budget);
            for (int k = 0; k < order.Length && picked.Count < budget; k++)
            {
                var anchor = AnchorFor(sites[order[k]], order[k]);
                bool crowded = false;
                foreach (var other in picked)
                {
                    float dx = other.Base.X - anchor.Base.X, dz = other.Base.Z - anchor.Base.Z;
                    if (dx * dx + dz * dz < MinSpacingMeters * MinSpacingMeters) { crowded = true; break; }
                }
                if (!crowded) picked.Add(anchor);
            }
            return picked.ToArray();
        }

        /// <summary>Roof of the envelope, a third of the length toward the front (engine/cab end).</summary>
        public static AmbientFireAnchor AnchorFor(in AmbientFireSite site, int index)
        {
            bool alongX = site.Size.X >= site.Size.Z;
            float length = alongX ? site.Size.X : site.Size.Z, width = alongX ? site.Size.Z : site.Size.X;
            float push = length * 0.3f * site.Facing;
            var top = new Float3(site.Center.X + (alongX ? push : 0f), site.Center.Y + site.Size.Y * 0.5f, site.Center.Z + (alongX ? 0f : push));
            float scale = Math.Max(1.2f, Math.Min(2.6f, width * 0.9f));
            return new AmbientFireAnchor(top, scale, index);
        }

        private static uint Fnv(string text)
        {
            uint h = 2166136261u;
            foreach (char c in text) { h ^= c; h *= 16777619u; }
            return h;
        }

        private static uint Mix(uint h) { unchecked { h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16; return h; } }
    }
}
