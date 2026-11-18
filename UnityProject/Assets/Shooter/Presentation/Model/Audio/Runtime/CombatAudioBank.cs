#nullable enable
using System;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Deterministic PCM combat cue bank (ADR-055 / T089 / ADR-096). Interleaved stereo float samples at a fixed rate.
    /// </summary>
    public sealed class CombatAudioBank
    {
        public const string RecipeId = "combat-audio-bank-v7-stereo";
        public const int BankVersion = 7;
        public const int DefaultSampleRate = 48000;
        public const int Channels = 2;
        /// <summary>Variations baked for the generated emergency bank.</summary>
        public const int VariationsPerCue = 4;
        /// <summary>Upper bound on manifest variations per cue (footsteps and impacts ship more takes).</summary>
        public const int MaxVariationsPerCue = 8;

        /// <summary>Bank slot order; the folder name is the manifest <c>kind</c>. Index is stable within a bank version.</summary>
        private static readonly (SynthVoiceKind kind, string folder)[] Slots =
        {
            (SynthVoiceKind.RifleShot, "rifle_shot"),
            (SynthVoiceKind.PistolShot, "pistol_shot"),
            (SynthVoiceKind.Impact, "impact"),
            (SynthVoiceKind.DamageTaken, "damage_taken"),
            (SynthVoiceKind.Footstep, "footstep"),
            (SynthVoiceKind.ReloadStart, "reload_start"),
            (SynthVoiceKind.ReloadComplete, "reload_complete"),
            (SynthVoiceKind.Death, "death"),
            (SynthVoiceKind.Ui, "ui"),
            (SynthVoiceKind.Ambient, "ambient"),
            (SynthVoiceKind.Headshot, "headshot"),
            (SynthVoiceKind.KnifeSwing, "knife_swing"),
            (SynthVoiceKind.GrenadeThrow, "grenade_throw"),
            (SynthVoiceKind.GrenadeExplode, "grenade_explode"),
            (SynthVoiceKind.ReloadPistol, "reload_pistol"),
            (SynthVoiceKind.KnifeHitFlesh, "knife_hit_flesh"),
            (SynthVoiceKind.KnifeHitHard, "knife_hit_hard"),
            (SynthVoiceKind.GrenadePin, "grenade_pin"),
            (SynthVoiceKind.GrenadeBounce, "grenade_bounce"),
            (SynthVoiceKind.GrenadeExplodeFar, "grenade_explode_far"),
            (SynthVoiceKind.DryFire, "dry_fire"),
            (SynthVoiceKind.WeaponSwitch, "weapon_switch"),
            (SynthVoiceKind.HitMarker, "hit_marker"),
            (SynthVoiceKind.KillConfirm, "kill_confirm"),
            (SynthVoiceKind.BulletFlyby, "bullet_flyby"),
            (SynthVoiceKind.DistantBattle, "distant_battle"),
            (SynthVoiceKind.UiHover, "ui_hover"),
            (SynthVoiceKind.UiClick, "ui_click"),
            (SynthVoiceKind.UiBack, "ui_back"),
            // ADR-102 v6: appended so every v5 index is unchanged.
            (SynthVoiceKind.Ak47Shot, "ak47_shot"),
            (SynthVoiceKind.Ak102Shot, "ak102_shot"),
            (SynthVoiceKind.M16Shot, "m16_shot"),
            (SynthVoiceKind.Mk14Shot, "mk14_shot"),
            (SynthVoiceKind.Tac338Shot, "tac338_shot"),
            (SynthVoiceKind.G17Shot, "g17_shot"),
            (SynthVoiceKind.EagleShot, "eagle_shot"),
            (SynthVoiceKind.BoltCycle, "bolt_cycle"),
            (SynthVoiceKind.ReloadAk, "reload_ak"),
            (SynthVoiceKind.ReloadBolt, "reload_bolt"),
        };

        public const int AmbientIndex = 9;

        private readonly float[][][] clips; // [kindIndex][variation][interleaved L,R…]

        public int SampleRate { get; }
        public string ContentHashHex { get; }
        public bool LoadedFromFoundSamples { get; }

        public CombatAudioBank(int sampleRate, float[][][] clips, string contentHashHex, bool loadedFromFoundSamples = false)
        {
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (clips == null) throw new ArgumentNullException(nameof(clips));
            if (string.IsNullOrEmpty(contentHashHex)) throw new ArgumentException("hash required", nameof(contentHashHex));
            SampleRate = sampleRate;
            this.clips = clips;
            ContentHashHex = contentHashHex;
            LoadedFromFoundSamples = loadedFromFoundSamples;
        }

        public bool TryGetClip(SynthVoiceKind kind, ulong seed, out float[] samples)
        {
            int kindIndex = KindIndex(kind);
            if (kindIndex < 0 || kindIndex >= clips.Length || clips[kindIndex] == null || clips[kindIndex].Length == 0)
            {
                samples = Array.Empty<float>();
                return false;
            }
            var vars = clips[kindIndex];
            int variation = (int)(seed % (ulong)vars.Length);
            samples = vars[variation];
            return samples != null && samples.Length >= Channels;
        }

        /// <summary>Number of loaded variations for a kind (0 when absent).</summary>
        public int VariationCount(SynthVoiceKind kind)
        {
            int k = KindIndex(kind);
            return k < 0 || k >= clips.Length || clips[k] == null ? 0 : clips[k].Length;
        }

        public static int KindIndex(SynthVoiceKind kind)
        {
            if (kind == SynthVoiceKind.Shot) kind = SynthVoiceKind.RifleShot;
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i].kind == kind) return i;
            return -1;
        }

        public static SynthVoiceKind KindAt(int index) =>
            index >= 0 && index < Slots.Length ? Slots[index].kind : throw new ArgumentOutOfRangeException(nameof(index));

        public static string KindFolderName(SynthVoiceKind kind)
        {
            int i = KindIndex(kind);
            return i >= 0 ? Slots[i].folder : "unknown";
        }

        public static int KindFolderToIndex(string folder)
        {
            for (int i = 0; i < Slots.Length; i++)
                if (string.Equals(Slots[i].folder, folder, StringComparison.Ordinal)) return i;
            return -1;
        }

        public static int KindCount => Slots.Length;
    }
}
