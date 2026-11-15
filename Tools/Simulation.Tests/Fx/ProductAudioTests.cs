#nullable enable
using System.Collections.Generic;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Presentation.Audio;
using Xunit;

namespace Shooter.Simulation.Tests.Fx
{
    public sealed class ProductAudioTests
    {
        [Theory]
        [InlineData(44100)]
        [InlineData(48000)]
        [InlineData(96000)]
        public void Device_rate_preserves_recording_duration_and_pitch(int outputRate)
        {
            var samples = new float[4800 * 2];
            for (int i = 0; i < 4800; i++)
                samples[i * 2] = samples[i * 2 + 1] = 0.3f * (float)System.Math.Sin(i * 2 * System.Math.PI * 1000 / 48000);
            var voice = new SynthVoice(SynthVoiceKind.Ui, 1f, 0f, 1f, samples, 48000, false);
            int frames = 0, crossings = 0;
            float previous = 0f;
            while (voice.Active && frames < outputRate)
            {
                voice.ReadSample(1f / outputRate, out float l, out _);
                if (previous <= 0 && l > 0) crossings++;
                previous = l;
                frames++;
            }
            Assert.InRange(frames, outputRate / 10 - 1, outputRate / 10 + 2);
            Assert.InRange(crossings, 99, 101);
        }

        [Fact]
        public void Direction_rotates_with_listener_and_rear_is_centered()
        {
            var listener = new Float3(0, 0, 0);
            var east = new Float3(10, 0, 0);
            Assert.InRange(ProductAudioEventRouter.ComputePan(listener, east, 0f), 0.99f, 1f);
            Assert.InRange(ProductAudioEventRouter.ComputePan(listener, east, (float)System.Math.PI), -1f, -0.99f);
            Assert.InRange(ProductAudioEventRouter.ComputePan(listener, east, (float)System.Math.PI / 2), -0.001f, 0.001f);
        }

        [Fact]
        public void Playing_world_voice_follows_turns_and_local_voice_stays_centered()
        {
            var samples = new float[4800 * 2];
            System.Array.Fill(samples, 0.1f);
            var world = new SynthVoice(SynthVoiceKind.RifleShot, 1f, 1f, 1f, samples, 48000, false,
                sourceX: 10f, sourceZ: 0f);
            world.ReadSample(1f / 48000, out float l, out float r);
            Assert.True(r > l);
            world.UpdateListener((float)System.Math.PI, 0f, 0f);
            world.ReadSample(1f / 48000, out l, out r);
            Assert.True(l > r);
            var local = new SynthVoice(SynthVoiceKind.RifleShot, 1f, 0f, 1f, samples, 48000, false);
            local.UpdateListener((float)System.Math.PI, 0f, 0f);
            local.ReadSample(1f / 48000, out l, out r);
            Assert.Equal(l, r);
        }

        [Fact]
        public void Distance_cull_boundary_fades_to_zero()
        {
            Assert.Equal(0f, CombatAudioDistance.Attenuation(SynthVoiceKind.RifleShot, CombatAudioDistance.GunfireMaxMeters));
            Assert.Equal(0f, CombatAudioDistance.Attenuation(SynthVoiceKind.Footstep, CombatAudioDistance.FootstepMaxMeters));
            Assert.InRange(CombatAudioDistance.Attenuation(SynthVoiceKind.RifleShot, CombatAudioDistance.GunfireMaxMeters - 0.01f), 0f, 0.001f);
        }

        [Fact]
        public void Limiter_preserves_stereo_ratio_under_saturated_mix()
        {
            var samples = new float[4800 * 2];
            for (int i = 0; i < 4800; i++) { samples[i * 2] = 0.8f; samples[i * 2 + 1] = 0.4f; }
            var clips = new float[CombatAudioBank.KindCount][][];
            for (int i = 0; i < clips.Length; i++) clips[i] = new[] { samples };
            var mix = new SynthAudioMixer(new CombatAudioBank(48000, clips, "limiter-test"));
            for (int i = 0; i < SynthAudioMixer.MaxVoices; i++) mix.TryPlay(SynthVoiceKind.Ui, 0f, (ulong)i);
            var buffer = new float[1024];
            mix.Fill(buffer, 2, 48000);
            for (int i = 0; i < buffer.Length; i += 2)
            {
                Assert.InRange(buffer[i], 0f, 0.891f);
                Assert.InRange(buffer[i] / buffer[i + 1], 1.999f, 2.001f);
            }
        }

        [Fact]
        public void Local_weapon_cue_bypasses_world_occlusion_query()
        {
            var router = new ProductAudioEventRouter(new SynthAudioMixer());
            int probes = 0;
            router.ObstructionProbe = (from, to) => { probes++; return 1f; };
            router.AttachSession(1001);
            var local = new EntityId(5);
            var shot = new CombatEvent(new EventId(1001, 3, local, 1), CombatEventKind.ShotAccepted, local, default,
                new ShotPayload(new DefinitionId("weapons/rifle"), 1, new Float3(0, 1.5f, 0), new Float3(0, 0, 1), 42, 1));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { shot }, default, local));
            Assert.Equal(0, probes);
        }

        [Fact]
        public void Local_shot_replaces_lower_priority_voice_without_exceeding_cap()
        {
            var mix = new SynthAudioMixer();
            for (int i = 0; i < SynthAudioMixer.MaxVoices; i++)
                Assert.True(mix.TryPlay(SynthVoiceKind.Footstep, 5f, (ulong)i));
            Assert.True(mix.TryPlay(SynthVoiceKind.RifleShot, 0f, 0f, 99, true, 0f, 0f));
            Assert.Equal(1, mix.StolenVoices);
            mix.Mix(1f / 48000);
            Assert.Equal(SynthAudioMixer.MaxVoices, mix.ActiveVoices);
            mix.StopAll();
            for (int i = 0; i < SynthAudioMixer.MaxVoices; i++)
                Assert.True(mix.TryPlay(SynthVoiceKind.RifleShot, 0f, 0f, (ulong)i, true, 0f, 0f));
            Assert.False(mix.TryPlay(SynthVoiceKind.Footstep, 1f, 99));
        }

        [Fact]
        public void Obstruction_reduces_report_and_reflections_extend_tail_but_stop_clears_it()
        {
            var clear = new SynthAudioMixer();
            var blocked = new SynthAudioMixer();
            clear.TryPlay(SynthVoiceKind.RifleShot, 10f, 0f, 1, false, 0f, 0f);
            blocked.TryPlay(SynthVoiceKind.RifleShot, 10f, 0f, 1, false, 1f, 0f);
            var a = new float[4096];
            var b = new float[4096];
            clear.Fill(a, 2, 48000);
            blocked.Fill(b, 2, 48000);
            Assert.True(Energy(a) > Energy(b) * 1.5f);

            var mix = new SynthAudioMixer();
            mix.Bank.TryGetClip(SynthVoiceKind.PistolShot, 0, out var clip);
            mix.TryPlay(SynthVoiceKind.PistolShot, 0f, 0f, 0, false, 0f, 1f);
            mix.Fill(new float[clip.Length], 2, 48000);
            var tail = new float[4096];
            mix.Fill(tail, 2, 48000);
            Assert.True(Energy(tail) > 0f);
            mix.StopAll();
            mix.Fill(tail, 2, 48000);
            Assert.Equal(0f, Energy(tail));
        }

        [Fact]
        public void Teleport_and_vertical_movement_do_not_emit_footstep_bursts()
        {
            var router = new ProductAudioEventRouter(new SynthAudioMixer());
            router.AttachSession(55);
            var actors = new[] { new ActorLifecycleState(new EntityId(7), 1, true, 100, new Float3(0, 0, 0), 0, 0, 1, 30, 90) };
            router.TickFootsteps(actors, default, 1);
            actors[0] = new ActorLifecycleState(new EntityId(7), 1, true, 100, new Float3(0, 2, 0), 0, 0, 1, 30, 90);
            Assert.Equal(0, router.TickFootsteps(actors, default, 2));
            actors[0] = new ActorLifecycleState(new EntityId(7), 1, true, 100, new Float3(8, 0, 0), 0, 0, 1, 30, 90);
            Assert.Equal(0, router.TickFootsteps(actors, default, 3));
        }

        private static float Energy(float[] samples)
        {
            float sum = 0f;
            foreach (float sample in samples) sum += sample * sample;
            return sum;
        }

        [Fact]
        public void Device_rate_fill_peak_limits_and_voice_cap()
        {
            var mix = new SynthAudioMixer();
            mix.SetChannelGains(1f, 1f, 0.7f);
            int accepted = 0;
            for (int i = 0; i < 40; i++)
                if (mix.TryPlay(SynthVoiceKind.Shot, i * 0.5f, (ulong)i)) accepted++;
            Assert.Equal(SynthAudioMixer.MaxVoices, accepted);
            Assert.True(mix.RejectedPlays > 0);

            var buffer = new float[480 * 2];
            mix.Fill(buffer, 2, 48000);
            for (int i = 0; i < buffer.Length; i++)
                Assert.InRange(buffer[i], -SynthAudioMixer.PeakLimit, SynthAudioMixer.PeakLimit);
            Assert.True(mix.ActiveVoices <= SynthAudioMixer.MaxVoices);
        }

        [Fact]
        public void UserSettings_schema2_gains_bind_to_mixer_channels()
        {
            var mix = new SynthAudioMixer();
            var settings = new UserSettings(
                2, 0.5f, 0.25f, 0.1f, 0, 1280, 720, DisplayMode.Windowed, false, 60, 80f, 0.1f, false, 1f, false, false, 1f, false,
                UserSettings.DefaultBindings());
            UserSettingsAudioBinding.ApplyGains(settings, mix);
            Assert.Equal(0.5f, mix.MasterVolume);
            Assert.Equal(0.25f, mix.EffectsVolume);
            Assert.Equal(0.1f, mix.UiVolume);
            Assert.False(mix.MasterMuted);

            var muted = new UserSettings(
                2, 0f, 0f, 0f, 0, 1280, 720, DisplayMode.Windowed, false, 60, 80f, 0.1f, false, 1f, false, false, 1f, false,
                UserSettings.DefaultBindings());
            UserSettingsAudioBinding.ApplyGains(muted, mix);
            Assert.True(mix.MasterMuted);
            Assert.True(mix.EffectsMuted);
            Assert.True(mix.UiMuted);
        }

        [Fact]
        public void Gains_and_mute_apply_to_existing_voices()
        {
            var mix = new SynthAudioMixer();
            mix.SetChannelGains(1f, 1f, 0.7f);
            Assert.True(mix.TryPlay(SynthVoiceKind.Shot, 0f, 7));
            float loud = 0f;
            for (int i = 0; i < 256; i++)
                loud += System.Math.Abs(mix.Mix(1f / 48000f));

            mix.SetChannelGains(0f, 1f, 0.7f, masterMuted: true);
            float muted = System.Math.Abs(mix.Mix(1f / 48000f));
            Assert.True(loud > muted);
            Assert.Equal(0f, muted);

            mix.StopAll();
            mix.SetChannelGains(1f, 0f, 0.7f, effectsMuted: true);
            Assert.True(mix.TryPlay(SynthVoiceKind.Impact, 0f, 8));
            Assert.Equal(0f, mix.Mix(1f / 48000f));

            mix.StopAll();
            mix.SetChannelGains(1f, 1f, 0f, uiMuted: true);
            Assert.True(mix.TryPlay(SynthVoiceKind.Ui, 0f, 9));
            Assert.Equal(0f, mix.Mix(1f / 48000f));
        }

        [Fact]
        public void Distance_attenuation_reduces_far_shots()
        {
            var near = new SynthAudioMixer();
            var far = new SynthAudioMixer();
            near.SetChannelGains(1f, 1f, 0.7f);
            far.SetChannelGains(1f, 1f, 0.7f);
            Assert.True(near.TryPlay(SynthVoiceKind.Shot, 0f, 1));
            Assert.True(far.TryPlay(SynthVoiceKind.Shot, 60f, 1));
            float n = 0, f = 0;
            for (int i = 0; i < 64; i++)
            {
                n += System.Math.Abs(near.Mix(1f / 48000f));
                f += System.Math.Abs(far.Mix(1f / 48000f));
            }
            Assert.True(n > f);
        }

        [Fact]
        public void Combat_events_play_once_by_event_id()
        {
            var mix = new SynthAudioMixer();
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(1001);
            var id = new EventId(1001, 3, new EntityId(5), 1);
            var shot = new CombatEvent(id, CombatEventKind.ShotAccepted, new EntityId(5), default,
                new ShotPayload(new DefinitionId("weapons/rifle"), 1, new Float3(0, 1.5f, 0), new Float3(0, 0, 1), 42, 1));
            var list = new List<CombatEvent> { shot, shot };
            Assert.Equal(1, router.ConsumeCombatEvents(list, new Float3(0, 0, 0)));
            Assert.Equal(0, router.ConsumeCombatEvents(list, new Float3(0, 0, 0)));
            mix.Mix(1f / 48000f);
            // Ambient bed + shot.
            Assert.True(mix.ActiveVoices >= 1);
            Assert.True(mix.ActiveVoices <= 2);
        }

        [Fact]
        public void Stale_session_suppresses_and_stop_clears_voices()
        {
            var mix = new SynthAudioMixer();
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(1001);
            var id = new EventId(1001, 1, new EntityId(2), 1);
            var impact = new CombatEvent(id, CombatEventKind.Impact, new EntityId(2), new EntityId(9),
                new ImpactPayload(new Float3(1, 0, 0), new Float3(0, 1, 0), 1, 1));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { impact }, new Float3(0, 0, 0)));
            mix.Mix(1f / 48000f);
            Assert.True(mix.ActiveVoices > 0);

            router.DetachSession();
            mix.Mix(1f / 48000f);
            Assert.Equal(0, mix.ActiveVoices);
            Assert.Equal(0, router.ConsumeCombatEvents(new[] { impact }, new Float3(0, 0, 0)));
            Assert.False(router.PlayUi(3));
        }

        [Fact]
        public void Footsteps_emit_from_stride_and_ui_uses_ui_channel()
        {
            var mix = new SynthAudioMixer();
            mix.SetChannelGains(1f, 1f, 0.7f);
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(55);
            var living = new List<ActorLifecycleState>
            {
                new ActorLifecycleState(new EntityId(7), 1, true, 100, new Float3(0, 0, 0), 0, 0, 1, 30, 90)
            };
            Assert.Equal(0, router.TickFootsteps(living, new Float3(0, 0, 0), 1));
            living[0] = new ActorLifecycleState(new EntityId(7), 1, true, 100, new Float3(ProductAudioEventRouter.FootstepStrideMeters, 0, 0), 0, 0, 1, 30, 90);
            Assert.Equal(1, router.TickFootsteps(living, new Float3(0, 0, 0), 2));
            Assert.True(router.PlayUi(99));
            mix.Mix(1f / 48000f);
            Assert.True(mix.ActiveVoices >= 1);
        }

        [Fact]
        public void StopAll_detaches_stale_voices_between_sessions()
        {
            var mix = new SynthAudioMixer();
            Assert.True(mix.TryPlay(SynthVoiceKind.Shot, 0f, 1));
            mix.StopAll();
            var buffer = new float[128];
            mix.Fill(buffer, 1, 48000);
            Assert.Equal(0, mix.ActiveVoices);
            for (int i = 0; i < buffer.Length; i++)
                Assert.Equal(0f, buffer[i]);
        }

        [Fact]
        public void Bank_hash_is_stable_and_rifle_differs_from_pistol()
        {
            var a = CombatAudioBankBaker.Bake();
            var b = CombatAudioBankBaker.Bake();
            Assert.Equal(a.ContentHashHex, b.ContentHashHex);
            Assert.Equal(CombatAudioBank.RecipeId, CombatAudioBank.RecipeId);
            Assert.True(a.TryGetClip(SynthVoiceKind.RifleShot, 0, out var rifle));
            Assert.True(a.TryGetClip(SynthVoiceKind.PistolShot, 0, out var pistol));
            Assert.True(rifle.Length > pistol.Length);
            Assert.False(SameSamples(rifle, pistol));
        }

        [Fact]
        public void Reload_death_and_pistol_map_from_combat_events()
        {
            var mix = new SynthAudioMixer();
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(77);
            var reload = new CombatEvent(
                new EventId(77, 1, new EntityId(3), 1), CombatEventKind.ReloadStarted, new EntityId(3), default,
                new ActionPayload(0, 0, 144));
            var done = new CombatEvent(
                new EventId(77, 2, new EntityId(3), 2), CombatEventKind.ReloadCompleted, new EntityId(3), default,
                new ActionPayload(0, 0, 144));
            var death = new CombatEvent(
                new EventId(77, 3, new EntityId(9), 1), CombatEventKind.Death, new EntityId(4), new EntityId(9),
                new DeathPayload(new EntityId(4)));
            var pistol = new CombatEvent(
                new EventId(77, 4, new EntityId(3), 3), CombatEventKind.ShotAccepted, new EntityId(3), default,
                new ShotPayload(new DefinitionId("weapons/pistol"), 1, new Float3(2, 1, 0), new Float3(0, 0, 1), 9, 1));

            Assert.Equal(1, router.ConsumeCombatEvents(new[] { reload }, new Float3(0, 0, 0)));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { done }, new Float3(0, 0, 0)));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { death }, new Float3(0, 0, 0)));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { pistol }, new Float3(0, 0, 0)));
            mix.Mix(1f / 48000f);
            Assert.True(mix.ActiveVoices >= 4);
        }

        [Fact]
        public void Head_region_maps_to_headshot_not_body_hit()
        {
            var mix = new SynthAudioMixer();
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(88);
            var headImpact = new CombatEvent(
                new EventId(88, 1, new EntityId(2), 1), CombatEventKind.Impact, new EntityId(2), new EntityId(9),
                new ImpactPayload(new Float3(1, 1.6f, 0), new Float3(0, 0, 1), 1, ProductAudioEventRouter.HeadRegionId));
            var bodyDamage = new CombatEvent(
                new EventId(88, 2, new EntityId(2), 2), CombatEventKind.DamageApplied, new EntityId(2), new EntityId(9),
                new DamagePayload(30, 70, 2));
            var headDamage = new CombatEvent(
                new EventId(88, 3, new EntityId(2), 3), CombatEventKind.DamageApplied, new EntityId(2), new EntityId(9),
                new DamagePayload(60, 10, ProductAudioEventRouter.HeadRegionId));

            Assert.Equal(1, router.ConsumeCombatEvents(new[] { headImpact }, new Float3(0, 0, 0)));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { bodyDamage }, new Float3(0, 0, 0)));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { headDamage }, new Float3(0, 0, 0)));

            Assert.True(mix.Bank.TryGetClip(SynthVoiceKind.Headshot, 0, out var hs));
            Assert.True(mix.Bank.TryGetClip(SynthVoiceKind.Impact, 0, out var impact));
            Assert.True(mix.Bank.TryGetClip(SynthVoiceKind.DamageTaken, 0, out var body));
            Assert.False(SameSamples(hs, impact));
            Assert.False(SameSamples(hs, body));
        }

        [Fact]
        public void Stereo_fill_pans_left_and_right()
        {
            var mix = new SynthAudioMixer();
            mix.SetChannelGains(1f, 1f, 0.7f);
            Assert.True(mix.TryPlay(SynthVoiceKind.RifleShot, 5f, -1f, 3));
            var leftBuf = new float[256 * 2];
            mix.Fill(leftBuf, 2, 48000);
            float leftEnergy = 0f, rightEnergy = 0f;
            for (int i = 0; i < leftBuf.Length; i += 2)
            {
                leftEnergy += System.Math.Abs(leftBuf[i]);
                rightEnergy += System.Math.Abs(leftBuf[i + 1]);
            }
            Assert.True(leftEnergy > rightEnergy);
        }

        [Fact]
        public void Far_footstep_is_culled_near_footstep_plays()
        {
            var mix = new SynthAudioMixer();
            mix.SetChannelGains(1f, 1f, 0.7f);
            Assert.False(mix.TryPlay(SynthVoiceKind.Footstep, 25f, 1));
            Assert.True(mix.TryPlay(SynthVoiceKind.Footstep, 4f, 2));
            Assert.Equal(0f, CombatAudioDistance.Attenuation(SynthVoiceKind.Footstep, 25f));
            Assert.True(CombatAudioDistance.Attenuation(SynthVoiceKind.Footstep, 4f) >
                        CombatAudioDistance.Attenuation(SynthVoiceKind.Footstep, 12f));
        }

        [Fact]
        public void Router_suppresses_distant_bot_footsteps()
        {
            var mix = new SynthAudioMixer();
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(91);
            var living = new List<ActorLifecycleState>
            {
                new ActorLifecycleState(new EntityId(3), 1, true, 100, new Float3(0, 0, 0), 0, 0, 1, 30, 90)
            };
            Assert.Equal(0, router.TickFootsteps(living, new Float3(0, 0, 0), 1));
            living[0] = new ActorLifecycleState(new EntityId(3), 1, true, 100,
                new Float3(ProductAudioEventRouter.FootstepStrideMeters, 0, 40f), 0, 0, 1, 30, 90);
            Assert.Equal(0, router.TickFootsteps(living, new Float3(0, 0, 0), 2));
        }

        [Fact]
        public void Found_stereo_bank_loads_when_manifest_present()
        {
            string? root = CombatAudioSamplePaths.ResolveCombatSampleRoot();
            Assert.False(string.IsNullOrEmpty(root));
            var bank = CombatAudioBankBaker.Bake(sampleRoot: root);
            Assert.True(bank.LoadedFromFoundSamples);
            Assert.Equal(CombatAudioBank.RecipeId, CombatAudioBank.RecipeId);
            Assert.True(bank.TryGetClip(SynthVoiceKind.RifleShot, 0, out var rifle));
            Assert.True(rifle.Length >= CombatAudioBank.Channels * 100);
            Assert.True((rifle.Length % CombatAudioBank.Channels) == 0);
            // Stereo image: L/R should differ on authored width.
            float left = 0f, right = 0f;
            int frames = System.Math.Min(rifle.Length / 2, 2048);
            for (int i = 0; i < frames; i++)
            {
                left += System.Math.Abs(rifle[i * 2]);
                right += System.Math.Abs(rifle[i * 2 + 1]);
            }
            Assert.True(System.Math.Abs(left - right) > 1e-4f);
        }

        [Fact]
        public void Rifle_and_pistol_found_shots_are_distinct_waveforms()
        {
            string? root = CombatAudioSamplePaths.ResolveCombatSampleRoot();
            Assert.False(string.IsNullOrEmpty(root));
            var bank = CombatAudioBankBaker.Bake(sampleRoot: root);
            Assert.True(bank.TryGetClip(SynthVoiceKind.RifleShot, 0, out var rifle));
            Assert.True(bank.TryGetClip(SynthVoiceKind.PistolShot, 0, out var pistol));
            Assert.True(rifle.Length != pistol.Length || !SameSamples(rifle, pistol));
            // Rifle one-shots are longer outdoor reports than pistol cracks.
            Assert.True(rifle.Length > pistol.Length);
        }

        [Fact]
        public void Knife_swing_and_grenade_cues_map_from_combat_events()
        {
            var mix = new SynthAudioMixer();
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(1201);

            var knife = new CombatEvent(
                new EventId(1201, 1, new EntityId(3), 1), CombatEventKind.ShotAccepted, new EntityId(3), default,
                new ShotPayload(new DefinitionId("weapons/knife"), 1, new Float3(0, 1.5f, 0), new Float3(0, 0, 1), 1, 1));
            var throwEv = new CombatEvent(
                new EventId(1201, 2, new EntityId(3), 2), CombatEventKind.ProjectileSpawned, new EntityId(3), default,
                new ProjectilePayload(new EntityId(99), new Float3(1, 1.5f, 0), new Float3(0, 4, 8), new DefinitionId("weapons/grenade")));
            var boom = new CombatEvent(
                new EventId(1201, 3, new EntityId(3), 3), CombatEventKind.Impact, new EntityId(3), default,
                new ImpactPayload(new Float3(5, 0, 5), new Float3(0, 1, 0), 1, 0));

            Assert.Equal(1, router.ConsumeCombatEvents(new[] { knife }, new Float3(0, 0, 0)));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { throwEv }, new Float3(0, 0, 0)));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { boom }, new Float3(0, 0, 0)));

            Assert.True(mix.Bank.TryGetClip(SynthVoiceKind.KnifeSwing, 0, out var ks));
            Assert.True(mix.Bank.TryGetClip(SynthVoiceKind.GrenadeThrow, 0, out var gt));
            Assert.True(mix.Bank.TryGetClip(SynthVoiceKind.GrenadeExplode, 0, out var ge));
            Assert.False(SameSamples(ks, gt));
            Assert.False(SameSamples(gt, ge));
            Assert.True(ge.Length > ks.Length);
        }

        [Fact]
        public void Manifest_hash_mismatch_falls_back_to_generated()
        {
            string? root = CombatAudioSamplePaths.ResolveCombatSampleRoot();
            Assert.False(string.IsNullOrEmpty(root));
            string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shooter-audio-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(temp);
            try
            {
                foreach (var file in System.IO.Directory.GetFiles(root!))
                    System.IO.File.Copy(file, System.IO.Path.Combine(temp, System.IO.Path.GetFileName(file)), overwrite: true);
                string manPath = System.IO.Path.Combine(temp, CombatAudioSamplePaths.ManifestFileName);
                string json = System.IO.File.ReadAllText(manPath);
                json = json.Replace("\"sha256\": \"", "\"sha256\": \"deadbeef");
                // Force first sha invalid without breaking JSON structure too badly — rewrite one entry.
                var manifest = CombatAudioManifest.ParseFile(manPath);
                Assert.True(manifest.Clips.Count > 0);
                var bad = new System.Text.StringBuilder();
                bad.Append("{\"bankId\":\"combat-audio-bank-v3-stereo\",\"sampleRate\":48000,\"channels\":2,\"clips\":[");
                var c0 = manifest.Clips[0];
                bad.Append("{\"kind\":\"").Append(c0.Kind).Append("\",\"variation\":").Append(c0.Variation)
                    .Append(",\"file\":\"").Append(c0.File).Append("\",\"sha256\":\"0000000000000000000000000000000000000000000000000000000000000000\",")
                    .Append("\"source\":\"test\",\"license\":\"CC0-1.0\"}]}");
                System.IO.File.WriteAllText(manPath, bad.ToString());
                var bank = CombatAudioBankBaker.Bake(sampleRoot: temp);
                Assert.False(bank.LoadedFromFoundSamples);
                Assert.True(bank.TryGetClip(SynthVoiceKind.RifleShot, 0, out _));
            }
            finally
            {
                try { System.IO.Directory.Delete(temp, recursive: true); } catch { /* ignore */ }
            }
        }

        private static bool SameSamples(float[] a, float[] b)
        {
            int n = System.Math.Min(a.Length, b.Length);
            int nCompare = System.Math.Min(n, 512);
            for (int i = 0; i < nCompare; i++)
                if (System.Math.Abs(a[i] - b[i]) > 1e-6f) return false;
            return a.Length == b.Length;
        }
    }
}
