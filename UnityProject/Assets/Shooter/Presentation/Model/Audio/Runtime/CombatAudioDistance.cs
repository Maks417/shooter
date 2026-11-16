#nullable enable
using System;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Per-kind audible range and attenuation (T089). Footsteps must not remain map-audible.
    /// </summary>
    public static class CombatAudioDistance
    {
        public const float FootstepMaxMeters = 18f;
        public const float SoftCueMaxMeters = 35f;
        public const float GunfireMaxMeters = 80f;

        public static float MaxAudibleMeters(SynthVoiceKind kind) => Normalize(kind) switch
        {
            SynthVoiceKind.Footstep => FootstepMaxMeters,
            SynthVoiceKind.ReloadStart => SoftCueMaxMeters,
            SynthVoiceKind.ReloadComplete => SoftCueMaxMeters,
            SynthVoiceKind.Death => SoftCueMaxMeters,
            SynthVoiceKind.Impact => SoftCueMaxMeters,
            SynthVoiceKind.DamageTaken => SoftCueMaxMeters,
            SynthVoiceKind.KnifeSwing => SoftCueMaxMeters,
            SynthVoiceKind.GrenadeThrow => SoftCueMaxMeters,
            SynthVoiceKind.GrenadeExplode => GunfireMaxMeters,
            SynthVoiceKind.Ui => 0f,
            SynthVoiceKind.Ambient => 0f,
            _ => GunfireMaxMeters
        };

        public static float Attenuation(SynthVoiceKind kind, float distanceMeters)
        {
            if (distanceMeters < 0f) throw new ArgumentOutOfRangeException(nameof(distanceMeters));
            var playKind = Normalize(kind);
            float max = MaxAudibleMeters(playKind);
            if (max > 0f && distanceMeters > max) return 0f;

            float atten = playKind switch
            {
                SynthVoiceKind.Footstep => 0.45f / (1f + distanceMeters * 0.45f),
                // Slightly hotter than other soft cues so local reload reads under residual gunfire.
                SynthVoiceKind.ReloadStart => 1.35f / (1f + distanceMeters * 0.10f),
                SynthVoiceKind.ReloadComplete => 1.25f / (1f + distanceMeters * 0.10f),
                SynthVoiceKind.Death => 1f / (1f + distanceMeters * 0.12f),
                SynthVoiceKind.Impact => 1f / (1f + distanceMeters * 0.12f),
                SynthVoiceKind.DamageTaken => 1f / (1f + distanceMeters * 0.12f),
                SynthVoiceKind.KnifeSwing => 0.85f / (1f + distanceMeters * 0.14f),
                SynthVoiceKind.GrenadeThrow => 0.75f / (1f + distanceMeters * 0.12f),
                SynthVoiceKind.GrenadeExplode => 1.15f / (1f + distanceMeters * 0.045f),
                SynthVoiceKind.Ambient => 0.35f,
                SynthVoiceKind.Ui => 1f,
                _ => 1f / (1f + distanceMeters * 0.05f)
            };
            // Fade to silence before the hard cull so crossing the radius does not pop.
            if (max > 0f) atten *= Math.Clamp((max - distanceMeters) / (max * 0.25f), 0f, 1f);
            return atten;
        }

        public static float LowPassCoeff(float distanceMeters, float maxMeters)
        {
            float denom = maxMeters > 1f ? maxMeters : GunfireMaxMeters;
            float far = Math.Clamp(distanceMeters / denom, 0f, 1f);
            float lp = 1f - far * 0.85f;
            return lp < 0.08f ? 0.08f : lp;
        }

        private static SynthVoiceKind Normalize(SynthVoiceKind kind) =>
            kind == SynthVoiceKind.Shot ? SynthVoiceKind.RifleShot : kind;
    }
}
