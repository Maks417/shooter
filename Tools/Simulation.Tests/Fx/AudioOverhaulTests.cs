#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Presentation.Audio;
using Shooter.Presentation.Menus;
using Xunit;

namespace Shooter.Simulation.Tests.Fx
{
    /// <summary>ADR-096: bank v5 cues, reload fitting, knife hits, flybys, bounces, music director and manifest.</summary>
    public sealed class AudioOverhaulTests
    {
        private static readonly Float3 Origin = new Float3(0, 0, 0);

        [Fact]
        public void Every_bank_slot_has_a_clip_and_a_unique_folder()
        {
            var bank = CombatAudioBankBaker.BakeGenerated();
            var folders = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < CombatAudioBank.KindCount; i++)
            {
                var kind = CombatAudioBank.KindAt(i);
                Assert.Equal(i, CombatAudioBank.KindIndex(kind));
                Assert.True(folders.Add(CombatAudioBank.KindFolderName(kind)));
                Assert.Equal(i, CombatAudioBank.KindFolderToIndex(CombatAudioBank.KindFolderName(kind)));
                Assert.True(bank.TryGetClip(kind, 0, out var clip), kind.ToString());
                Assert.True(clip.Length >= CombatAudioBank.Channels);
            }
            Assert.Equal(1, bank.VariationCount(SynthVoiceKind.Ambient));
        }

        [Fact]
        public void Found_bank_compacts_to_shipped_variations_and_fills_missing_kinds()
        {
            string dir = Path.Combine(Path.GetTempPath(), "shooter-audio-v5-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string Entry(string kind, int variation)
                {
                    string file = kind + "_" + variation.ToString("00") + ".wav";
                    WriteWav(Path.Combine(dir, file), 0.1f + variation * 0.01f);
                    return "{\"kind\":\"" + kind + "\",\"variation\":" + variation + ",\"file\":\"" + file + "\",\"sha256\":\"" + Sha(Path.Combine(dir, file)) + "\",\"source\":\"test\",\"license\":\"test\"}";
                }
                string clips = string.Join(",", Entry("footstep", 0), Entry("footstep", 1), Entry("footstep", 2), Entry("footstep", 3),
                    Entry("footstep", 4), Entry("footstep", 5), Entry("reload_pistol", 0));
                File.WriteAllText(Path.Combine(dir, "manifest.json"), "{\"bankId\":\"t\",\"sampleRate\":48000,\"channels\":2,\"clips\":[" + clips + "]}");
                var bank = CombatAudioBankBaker.Bake(sampleRoot: dir);
                Assert.True(bank.LoadedFromFoundSamples);
                Assert.Equal(6, bank.VariationCount(SynthVoiceKind.Footstep));
                Assert.Equal(1, bank.VariationCount(SynthVoiceKind.ReloadPistol));
                Assert.Equal(CombatAudioBank.VariationsPerCue, bank.VariationCount(SynthVoiceKind.KnifeHitFlesh));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Theory]
        [InlineData(2.0f, 2.0f, 1.0f)]
        [InlineData(2.2f, 2.0f, 1.1f)]
        [InlineData(3.0f, 2.0f, 1.12f)]
        [InlineData(1.0f, 2.0f, 0.88f)]
        public void Reload_foley_fits_the_authority_window_within_tolerance(float clip, float window, float expected)
        {
            Assert.Equal(expected, ProductAudioEventRouter.FitRate(clip, window), 3);
        }

        [Fact]
        public void Pistol_reload_uses_pistol_foley_and_death_cuts_it()
        {
            var mix = new SynthAudioMixer(CombatAudioBankBaker.BakeGenerated());
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(50);
            var shooter = new EntityId(7);
            var reload = new CombatEvent(new EventId(50, 10, shooter, 1), CombatEventKind.ReloadStarted, shooter, default,
                new ActionPayload(1, 10, 10 + 96));
            Assert.Equal(SynthVoiceKind.ReloadPistol, ProductAudioEventRouter.MapReloadVoice(reload));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { reload }, Origin, shooter));
            Assert.True(mix.IsPlaying(shooter.Value, SynthVoiceKind.ReloadPistol));

            var death = new CombatEvent(new EventId(50, 20, shooter, 1), CombatEventKind.Death, default, shooter, new DeathPayload(default));
            router.ConsumeCombatEvents(new[] { death }, Origin, shooter);
            Assert.False(mix.IsPlaying(shooter.Value, SynthVoiceKind.ReloadPistol));
            var buffer = new float[2 * 4800];
            mix.Fill(buffer, 2, 48000);
            Assert.False(mix.IsPlaying(shooter.Value, SynthVoiceKind.ReloadPistol));

            var rifle = new CombatEvent(new EventId(50, 30, shooter, 1), CombatEventKind.ReloadStarted, shooter, default, new ActionPayload(0, 30, 150));
            Assert.Equal(SynthVoiceKind.ReloadStart, ProductAudioEventRouter.MapReloadVoice(rifle));
        }

        [Fact]
        public void Knife_hits_pair_with_the_swing_tick_and_local_hits_confirm()
        {
            var mix = new SynthAudioMixer(CombatAudioBankBaker.BakeGenerated());
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(60);
            var me = new EntityId(1);
            var enemy = new EntityId(2);
            var swing = new CombatEvent(new EventId(60, 5, me, 1), CombatEventKind.ShotAccepted, me, default,
                new ShotPayload(new DefinitionId("weapons/knife"), 1, new Float3(0, 1.5f, 0), new Float3(0, 0, 1), 1, 1));
            var flesh = new CombatEvent(new EventId(60, 5, me, 2), CombatEventKind.Impact, me, enemy,
                new ImpactPayload(new Float3(0, 1.2f, 1), new Float3(0, 0, -1), 0, 2));
            var damage = new CombatEvent(new EventId(60, 5, me, 3), CombatEventKind.DamageApplied, me, enemy, new DamagePayload(55, 45, 2));
            // Swing + (flesh Impact is voiced by the damage) + knife flesh hit + local hit marker.
            Assert.Equal(3, router.ConsumeCombatEvents(new[] { swing, flesh, damage }, Origin, me));

            var wall = new CombatEvent(new EventId(60, 9, me, 1), CombatEventKind.ShotAccepted, me, default,
                new ShotPayload(new DefinitionId("weapons/knife"), 2, new Float3(0, 1.5f, 0), new Float3(0, 0, 1), 1, 1));
            var hard = new CombatEvent(new EventId(60, 9, me, 2), CombatEventKind.Impact, me, default,
                new ImpactPayload(new Float3(0, 1.2f, 1), new Float3(0, 0, -1), 3, 0));
            Assert.Equal(2, router.ConsumeCombatEvents(new[] { wall, hard }, Origin, me));

            var kill = new CombatEvent(new EventId(60, 12, me, 1), CombatEventKind.Death, me, enemy, new DeathPayload(me));
            // Death report + kill confirm.
            Assert.Equal(2, router.ConsumeCombatEvents(new[] { kill }, Origin, me));
        }

        [Fact]
        public void Flyby_only_for_close_passes_past_the_muzzle()
        {
            var ears = new Float3(0, 1.35f, 0);
            Assert.True(ProductAudioEventRouter.FlybyGeometry(new Float3(-30, 1.5f, 1f), new Float3(1, 0, 0), ears, out float miss, out _));
            Assert.InRange(miss, 0.9f, 1.1f);
            Assert.False(ProductAudioEventRouter.FlybyGeometry(new Float3(-30, 1.5f, 6f), new Float3(1, 0, 0), ears, out _, out _));
            // Fired away from the listener.
            Assert.False(ProductAudioEventRouter.FlybyGeometry(new Float3(-30, 1.5f, 1f), new Float3(-1, 0, 0), ears, out _, out _));
            // The shooter's own position is covered by the muzzle report.
            Assert.False(ProductAudioEventRouter.FlybyGeometry(new Float3(-2, 1.4f, 0f), new Float3(1, 0, 0), ears, out _, out _));
        }

        [Fact]
        public void Enemy_round_passing_close_plays_flyby_but_own_shots_do_not()
        {
            var mix = new SynthAudioMixer(CombatAudioBankBaker.BakeGenerated());
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(70);
            var me = new EntityId(1);
            var enemy = new EntityId(2);
            var enemyShot = new CombatEvent(new EventId(70, 3, enemy, 1), CombatEventKind.ShotAccepted, enemy, default,
                new ShotPayload(new DefinitionId("weapons/rifle"), 1, new Float3(-30, 1.5f, 1f), new Float3(1, 0, 0), 1, 1));
            Assert.Equal(2, router.ConsumeCombatEvents(new[] { enemyShot }, Origin, me));
            var myShot = new CombatEvent(new EventId(70, 4, me, 1), CombatEventKind.ShotAccepted, me, default,
                new ShotPayload(new DefinitionId("weapons/rifle"), 2, new Float3(-30, 1.5f, 1f), new Float3(1, 0, 0), 1, 1));
            Assert.Equal(1, router.ConsumeCombatEvents(new[] { myShot }, Origin, me));
        }

        [Fact]
        public void Grenade_bounces_land_on_the_arc_and_a_blast_cancels_them()
        {
            Assert.True(ProductAudioEventRouter.TryGroundContact(new Float3(0, 1.5f, 0), new Float3(0, 4, 10), 0f, out float t, out var landing));
            // g = 24 (ADR-111): 1.5 + 4t - 12t^2 = 0 → t = (4 + sqrt(88)) / 24
            Assert.Equal(0.5575f, t, 3);
            Assert.Equal(5.5753f, landing.Z, 2);

            var mix = new SynthAudioMixer(CombatAudioBankBaker.BakeGenerated());
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(80);
            var thrower = new EntityId(4);
            var spawn = new CombatEvent(new EventId(80, 1, thrower, 1), CombatEventKind.ProjectileSpawned, thrower, default,
                new ProjectilePayload(new EntityId(99), new Float3(0, 1.5f, 0), new Float3(0, 4, 10), new DefinitionId("weapons/grenade")));
            router.ConsumeCombatEvents(new[] { spawn }, Origin, thrower);
            Assert.Equal(2, router.PendingBounceCount);
            Assert.Equal(0, router.TickScheduled(0.5f, Origin));
            Assert.Equal(1, router.TickScheduled(0.2f, Origin));
            Assert.Equal(1, router.PendingBounceCount);
            var blast = new CombatEvent(new EventId(80, 50, thrower, 1), CombatEventKind.Impact, thrower, default,
                new ImpactPayload(new Float3(0, 0, 6), new Float3(0, 1, 0), 1, 0));
            router.ConsumeCombatEvents(new[] { blast }, Origin, thrower);
            Assert.Equal(0, router.PendingBounceCount);
        }

        [Fact]
        public void Distant_battle_fires_within_its_interval_and_stops_on_detach()
        {
            var mix = new SynthAudioMixer(CombatAudioBankBaker.BakeGenerated());
            var router = new ProductAudioEventRouter(mix);
            router.AttachSession(90);
            float first = router.DistantBattleSecondsRemaining;
            Assert.InRange(first, ProductAudioEventRouter.DistantBattleMinSeconds, ProductAudioEventRouter.DistantBattleMaxSeconds);
            Assert.False(router.TickDistantBattle(first - 0.5f));
            Assert.True(router.TickDistantBattle(1f));
            router.DetachSession();
            Assert.False(router.TickDistantBattle(60f));
        }

        [Fact]
        public void Ui_kinds_use_the_ui_channel_and_local_feedback_ignores_distance()
        {
            var mix = new SynthAudioMixer(CombatAudioBankBaker.BakeGenerated());
            mix.SetChannelGains(1f, 0f, 1f, effectsMuted: true);
            Assert.True(mix.TryPlay(SynthVoiceKind.UiClick, 0f, 1));
            var buffer = new float[2 * 2400];
            mix.Fill(buffer, 2, 48000);
            Assert.True(Peak(buffer) > 0.01f);
            Assert.True(SynthAudioMixer.IsUiKind(SynthVoiceKind.UiHover));
            Assert.False(SynthAudioMixer.IsUiKind(SynthVoiceKind.HitMarker));
            Assert.Equal(0f, CombatAudioDistance.MaxAudibleMeters(SynthVoiceKind.HitMarker));
        }

        [Fact]
        public void Loud_local_fire_raises_duck_level_which_decays()
        {
            var mix = new SynthAudioMixer(CombatAudioBankBaker.BakeGenerated());
            Assert.Equal(0f, mix.LoudCueLevel);
            Assert.True(mix.TryPlay(SynthVoiceKind.RifleShot, 0f, 0f, 1, true, 0f, 0f));
            Assert.Equal(1f, mix.LoudCueLevel);
            var buffer = new float[2 * 48000];
            mix.Fill(buffer, 2, 48000);
            Assert.InRange(mix.LoudCueLevel, 0.2f, 0.45f);
            // Distant enemy fire does not duck.
            mix.StopAll();
            Assert.True(mix.TryPlay(SynthVoiceKind.RifleShot, 40f, 0f, 2, false, 0f, 0f));
            Assert.Equal(0f, mix.LoudCueLevel);
        }

        [Fact]
        public void Music_director_walks_menu_match_stinger_results_menu()
        {
            var d = new MusicDirector();
            Assert.Equal(MusicCue.MenuTheme, d.Update(new MusicInput(MusicContext.Menu)).Cue);
            Assert.Equal(MusicCue.None, d.Update(new MusicInput(MusicContext.Loading)).Cue);
            var calm = d.Update(new MusicInput(MusicContext.Match));
            Assert.Equal(MusicCue.MatchCalm, calm.Cue);
            Assert.True(calm.Loop);
            var ducked = d.Update(new MusicInput(MusicContext.Match, duck: 1f));
            Assert.Equal(MusicDirector.BedGain * (1f - MusicDirector.DuckDepth), ducked.Gain, 4);
            Assert.Equal(MusicCue.MatchTension, d.Update(new MusicInput(MusicContext.Match, tension: true)).Cue);
            // Tension latches for the rest of the match.
            Assert.Equal(MusicCue.MatchTension, d.Update(new MusicInput(MusicContext.Match, tension: false)).Cue);

            var stinger = d.Update(new MusicInput(MusicContext.Finished, outcome: LocalMatchOutcome.Defeat));
            Assert.Equal(MusicCue.DefeatStinger, stinger.Cue);
            Assert.False(stinger.Loop);
            Assert.Equal(MusicCue.DefeatStinger, d.Update(new MusicInput(MusicContext.Finished, outcome: LocalMatchOutcome.Defeat)).Cue);
            Assert.Equal(MusicCue.ResultsLoop, d.Update(new MusicInput(MusicContext.Finished, outcome: LocalMatchOutcome.Defeat, currentCueEnded: true)).Cue);
            Assert.Equal(MusicCue.ResultsLoop, d.Update(new MusicInput(MusicContext.Finished, outcome: LocalMatchOutcome.Defeat)).Cue);
            Assert.Equal(MusicCue.MenuTheme, d.Update(new MusicInput(MusicContext.Menu)).Cue);
            // A new match starts calm again.
            d.Update(new MusicInput(MusicContext.Loading));
            Assert.Equal(MusicCue.MatchCalm, d.Update(new MusicInput(MusicContext.Match)).Cue);
        }

        [Theory]
        [InlineData(MatchOutcome.Team1, 1, LocalMatchOutcome.Victory)]
        [InlineData(MatchOutcome.Team2, 1, LocalMatchOutcome.Defeat)]
        [InlineData(MatchOutcome.Team2, 2, LocalMatchOutcome.Victory)]
        [InlineData(MatchOutcome.Draw, 1, LocalMatchOutcome.Draw)]
        [InlineData(MatchOutcome.None, 1, LocalMatchOutcome.Unknown)]
        [InlineData(MatchOutcome.Team1, 0, LocalMatchOutcome.Unknown)]
        public void Outcome_is_seen_from_the_local_team(MatchOutcome outcome, byte team, LocalMatchOutcome expected)
        {
            Assert.Equal(expected, MusicDirector.OutcomeFor(outcome, team));
        }

        [Fact]
        public void Tension_starts_near_score_limit_or_final_minute()
        {
            Assert.False(MusicDirector.IsTense(100, 90, 150, 30000));
            Assert.True(MusicDirector.IsTense(135, 90, 150, 30000));
            Assert.True(MusicDirector.IsTense(10, 12, 150, 3600));
            Assert.False(MusicDirector.IsTense(10, 12, 150, 0));
        }

        [Fact]
        public void Screen_context_keeps_the_music_of_where_settings_opened()
        {
            Assert.Equal(MusicContext.Menu, MusicDirector.ContextFor(ShellScreen.Main, ShellScreen.Main));
            Assert.Equal(MusicContext.Menu, MusicDirector.ContextFor(ShellScreen.Settings, ShellScreen.Main));
            Assert.Equal(MusicContext.Match, MusicDirector.ContextFor(ShellScreen.Settings, ShellScreen.Paused));
            Assert.Equal(MusicContext.Match, MusicDirector.ContextFor(ShellScreen.ConfirmLeave, ShellScreen.Main));
            Assert.Equal(MusicContext.Loading, MusicDirector.ContextFor(ShellScreen.Loading, ShellScreen.Main));
            Assert.Equal(MusicContext.Finished, MusicDirector.ContextFor(ShellScreen.Results, ShellScreen.Main));
        }

        [Fact]
        public void Music_manifest_verifies_hashes_and_ignores_unknown_slots()
        {
            string dir = Path.Combine(Path.GetTempPath(), "shooter-music-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllBytes(Path.Combine(dir, "menu.ogg"), new byte[] { 1, 2, 3 });
                File.WriteAllBytes(Path.Combine(dir, "win.ogg"), new byte[] { 4, 5, 6 });
                string good = Sha(Path.Combine(dir, "menu.ogg"));
                File.WriteAllText(Path.Combine(dir, "manifest.json"),
                    "{\"bankId\":\"music-test\",\"tracks\":[" +
                    "{\"cue\":\"menu_theme\",\"file\":\"menu.ogg\",\"sha256\":\"" + good + "\",\"loopStartSeconds\":1.5,\"loopEndSeconds\":60.25,\"source\":\"s\"}," +
                    "{\"cue\":\"victory_stinger\",\"file\":\"win.ogg\",\"sha256\":\"" + new string('0', 64) + "\",\"source\":\"s\"}," +
                    "{\"cue\":\"future_slot\",\"file\":\"x.ogg\",\"sha256\":\"" + new string('1', 64) + "\"}]}");
                var problems = new System.Collections.Generic.List<string>();
                var manifest = MusicManifest.LoadVerified(dir, problems);
                Assert.NotNull(manifest);
                Assert.True(manifest!.TryGet(MusicCue.MenuTheme, out var menu));
                Assert.Equal(1.5f, menu.LoopStartSeconds, 3);
                Assert.Equal(60.25f, menu.LoopEndSeconds, 3);
                Assert.False(manifest.TryGet(MusicCue.VictoryStinger, out _));
                Assert.Single(problems);
                Assert.Throws<ArgumentException>(() => MusicManifest.Parse(
                    "{\"tracks\":[{\"cue\":\"menu_theme\",\"file\":\"../x.ogg\",\"sha256\":\"" + good + "\"}]}"));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Local_weapon_cues_switch_pin_and_dry_fire()
        {
            var tracker = new LocalWeaponCueTracker();
            var cues = new SynthVoiceKind[3];
            var me = new EntityId(1);
            ActorLifecycleState State(byte slot, ushort mag, WeaponAction action, string weapon) =>
                new ActorLifecycleState(me, 1, true, 100, Origin, 0f, 0f, slot, mag, 30, action, new DefinitionId(weapon));

            Assert.Equal(0, tracker.Step(State(0, 30, WeaponAction.Ready, "weapons/rifle"), false, 0.016f, cues));
            Assert.Equal(1, tracker.Step(State(1, 12, WeaponAction.Equipping, "weapons/pistol"), false, 0.016f, cues));
            Assert.Equal(SynthVoiceKind.WeaponSwitch, cues[0]);
            Assert.Equal(1, tracker.Step(State(1, 0, WeaponAction.Ready, "weapons/pistol"), true, 0.016f, cues));
            Assert.Equal(SynthVoiceKind.DryFire, cues[0]);
            // Held trigger within the repeat window stays quiet.
            Assert.Equal(0, tracker.Step(State(1, 0, WeaponAction.Ready, "weapons/pistol"), true, 0.016f, cues));
            Assert.Equal(1, tracker.Step(State(3, 1, WeaponAction.Ready, "weapons/grenade"), false, 0.3f, cues));
            Assert.Equal(1, tracker.Step(State(3, 1, WeaponAction.Cooking, "weapons/grenade"), false, 0.016f, cues));
            Assert.Equal(SynthVoiceKind.GrenadePin, cues[0]);
            // Empty grenade slot is not a gun: no dry fire.
            Assert.Equal(0, tracker.Step(State(3, 0, WeaponAction.Ready, "weapons/grenade"), true, 0.5f, cues));
        }

        [Fact]
        public void Shipped_repo_banks_load_every_cue_from_recordings_and_all_music_tracks()
        {
            string? combat = CombatAudioSamplePaths.ResolveCombatSampleRoot();
            Assert.NotNull(combat);
            var bank = CombatAudioBankBaker.Bake(sampleRoot: combat);
            Assert.True(bank.LoadedFromFoundSamples);
            var manifest = CombatAudioManifest.ParseFile(Path.Combine(combat!, CombatAudioSamplePaths.ManifestFileName));
            Assert.Equal(CombatAudioBank.RecipeId, manifest.BankId);
            for (int i = 0; i < CombatAudioBank.KindCount; i++)
            {
                string folder = CombatAudioBank.KindFolderName(CombatAudioBank.KindAt(i));
                Assert.Contains(manifest.Clips, c => c.Kind == folder);
            }

            var problems = new System.Collections.Generic.List<string>();
            var music = MusicManifest.LoadVerified(MusicManifest.ResolveFolder()!, problems);
            Assert.NotNull(music);
            Assert.Empty(problems);
            for (var cue = MusicCue.MenuTheme; cue <= MusicCue.ResultsLoop; cue++)
                Assert.True(music!.TryGet(cue, out _), cue.ToString());
        }

        private static float Peak(float[] buffer)
        {
            float peak = 0f;
            for (int i = 0; i < buffer.Length; i++) peak = Math.Max(peak, Math.Abs(buffer[i]));
            return peak;
        }

        private static string Sha(string path)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        private static void WriteWav(string path, float amplitude)
        {
            const int rate = 48000, frames = 4800;
            using var w = new BinaryWriter(File.Create(path));
            w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            w.Write(36 + frames * 4);
            w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(rate); w.Write(rate * 4); w.Write((short)4); w.Write((short)16);
            w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            w.Write(frames * 4);
            for (int i = 0; i < frames; i++)
            {
                short s = (short)(Math.Sin(i * 0.05) * amplitude * short.MaxValue);
                w.Write(s); w.Write(s);
            }
        }
    }
}
