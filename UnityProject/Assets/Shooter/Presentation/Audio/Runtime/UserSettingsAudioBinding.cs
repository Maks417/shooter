#nullable enable
using System;
using Shooter.Game.Contracts;

namespace Shooter.Presentation.Audio
{
    /// <summary>
    /// Pure FIX09/FIX10 bridge: ADR-036 UserSettings schema 2 volumes → mixer channel gains/mutes.
    /// Volume 0 mutes the channel; applies to subsequent and existing voices at mix time.
    /// </summary>
    public static class UserSettingsAudioBinding
    {
        public static void ApplyGains(UserSettings settings, SynthAudioMixer mixer)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (mixer == null) throw new ArgumentNullException(nameof(mixer));
            mixer.SetChannelGains(
                settings.MasterVolume,
                settings.EffectsVolume,
                settings.UiVolume,
                masterMuted: settings.MasterVolume <= 0f,
                effectsMuted: settings.EffectsVolume <= 0f,
                uiMuted: settings.UiVolume <= 0f);
        }
    }
}
