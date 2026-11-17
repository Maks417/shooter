using System;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Hud;
using Shooter.Presentation.View;
using Xunit;

namespace Shooter.Simulation.Tests.Hud
{
    /// <summary>ADR-103 damage feedback v2: view flinch, damage-scaled flash and the low-health grade.</summary>
    public sealed class DamageFeedbackV2Tests
    {
        [Fact]
        public void PunchScalesWithDamageHeadshotAndTurnsAwayFromAttacker()
        {
            Assert.Equal(FirstPersonDamagePunch.MinPitchDegrees, FirstPersonDamagePunch.PitchKickDegrees(0, false), 4);
            Assert.Equal(FirstPersonDamagePunch.MaxPitchDegrees, FirstPersonDamagePunch.PitchKickDegrees(100, false), 4);
            Assert.True(FirstPersonDamagePunch.PitchKickDegrees(40, true) > FirstPersonDamagePunch.PitchKickDegrees(40, false));
            // Attacker on the right (positive bearing) turns the view left (negative yaw), and vice versa.
            Assert.True(FirstPersonDamagePunch.YawKickDegrees(50, false, (float)(Math.PI / 2)) < 0f);
            Assert.True(FirstPersonDamagePunch.YawKickDegrees(50, false, -(float)(Math.PI / 2)) > 0f);
            Assert.Equal(0f, FirstPersonDamagePunch.YawKickDegrees(50, false, 0f), 4);
        }

        [Fact]
        public void PunchRisesPeaksAndSettles()
        {
            var punch = new FirstPersonDamagePunch();
            punch.Kick(60, headshot: false, relativeYawRadians: 1f, reducedMotion: false);
            punch.Step(FirstPersonDamagePunch.RiseSeconds, reducedMotion: false);
            float peak = punch.PitchRadians;
            Assert.True(peak < 0f); // looks up
            Assert.Equal(-FirstPersonDamagePunch.PitchKickDegrees(60, false) * Math.PI / 180.0, peak, 3);
            Assert.True(punch.YawRadians < 0f);
            punch.Step(FirstPersonDamagePunch.LifeSeconds, reducedMotion: false);
            Assert.Equal(0f, punch.PitchRadians);
            Assert.Equal(0f, punch.YawRadians);
        }

        [Fact]
        public void ReducedMotionDisablesPunch()
        {
            var punch = new FirstPersonDamagePunch();
            punch.Kick(80, true, 1f, reducedMotion: true);
            punch.Step(FirstPersonDamagePunch.RiseSeconds, reducedMotion: false);
            Assert.Equal(0f, punch.PitchRadians);
            punch.Kick(80, true, 1f, reducedMotion: false);
            punch.Step(FirstPersonDamagePunch.RiseSeconds, reducedMotion: true);
            Assert.Equal(0f, punch.PitchRadians);
        }

        [Fact]
        public void FlashScalesWithDamageAndStaysQuietUnderReducedFlash()
        {
            float window = FirstPersonHitFeedback.DamageFlashSeconds;
            float graze = FirstPersonHitFeedback.DamageFlashStrength(window, false, 10);
            float heavy = FirstPersonHitFeedback.DamageFlashStrength(window, false, 80);
            Assert.True(heavy > graze);
            Assert.True(heavy <= 0.38f + 1e-5f);
            Assert.Equal(0.10f, FirstPersonHitFeedback.DamageFlashStrength(window, true, 80), 4);
            Assert.Equal(0f, FirstPersonHitFeedback.DamageFlashStrength(0f, false, 80));
        }

        [Fact]
        public void LowHealthGradeEscalatesAndPulses()
        {
            Assert.Equal(0f, CombatHudVisual.LowHealth(100, 0f, 0f, false).Weight);
            Assert.Equal(0f, CombatHudVisual.LowHealth(0, 0f, 1f, false).Weight); // dead: KIA view owns the screen
            var wounded = CombatHudVisual.LowHealth(35, 0.5f, 0f, false);
            var critical = CombatHudVisual.LowHealth(10, 0.5f, 0f, false);
            Assert.Equal(1f, critical.Weight);
            Assert.True(critical.Saturation < wounded.Saturation);
            Assert.True(critical.Saturation >= -55f - 18f);
            Assert.True(critical.Vignette > wounded.Vignette - 0.1f);
            Assert.InRange(critical.Vignette, 0.25f, 0.6f);
            // A fresh hit at full health still gives a short grade.
            Assert.True(CombatHudVisual.LowHealth(100, 0f, CombatHudVisual.HitGradeSeconds, false).Vignette > 0f);
            // Heartbeat: envelope peaks at the beat and is faster when critical.
            Assert.True(CombatHudVisual.HeartbeatEnvelope(0f) > 0.9f);
            Assert.True(CombatHudVisual.HeartbeatEnvelope(0.6f) < 0.05f);
            Assert.True(CombatHudVisual.HeartbeatRateHz(10) > CombatHudVisual.HeartbeatRateHz(39));
        }

        [Fact]
        public void ReducedFlashHalvesGradeWithoutPulseOrHitPunch()
        {
            var full = CombatHudVisual.LowHealth(10, 0f, 0f, false);
            var reduced = CombatHudVisual.LowHealth(10, 0f, CombatHudVisual.HitGradeSeconds, true);
            Assert.Equal(0f, reduced.Heartbeat);
            Assert.True(reduced.Vignette < full.Vignette);
            Assert.True(reduced.Saturation > full.Saturation);
            Assert.Equal(reduced.Vignette, CombatHudVisual.LowHealth(10, 0.3f, 0f, true).Vignette, 5);
        }

        [Fact]
        public void LowHealthAudioIsSilentAtFullHealthAndAudibleWhenCriticalOrHit()
        {
            var fx = new LowHealthAudioFx();
            var buffer = new float[48000 * 2];
            fx.SetState(0f, 1.1f, 1f);
            fx.Process(buffer, 2, 48000);
            Assert.All(buffer, v => Assert.Equal(0f, v));

            fx.SetState(0.75f, CombatHudVisual.HeartbeatRateHz(10), 1f);
            Array.Clear(buffer, 0, buffer.Length);
            fx.Process(buffer, 2, 48000);
            Assert.Contains(buffer, v => Math.Abs(v) > 0.05f);
            Assert.All(buffer, v => Assert.InRange(v, -1f, 1f));

            var hitOnly = new LowHealthAudioFx();
            hitOnly.SetState(0f, 1.1f, 1f);
            hitOnly.Hit(40);
            var hitBuffer = new float[4800 * 2];
            hitOnly.Process(hitBuffer, 2, 48000);
            Assert.Contains(hitBuffer, v => Math.Abs(v) > 0.05f);

            var muted = new LowHealthAudioFx();
            muted.SetState(1f, 1.8f, 0f);
            muted.Hit(80);
            var mutedBuffer = new float[4800 * 2];
            muted.Process(mutedBuffer, 2, 48000);
            Assert.All(mutedBuffer, v => Assert.Equal(0f, v));
        }
    }
}
