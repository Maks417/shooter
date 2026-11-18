#nullable enable
using System;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Presentation.Menus;

namespace Shooter.Presentation.Audio
{
    /// <summary>Music slots shipped in <c>Content/Audio/music</c> (ADR-096). The manifest names each slot.</summary>
    public enum MusicCue : byte
    {
        None = 0,
        MenuTheme = 1,
        MatchCalm = 2,
        MatchTension = 3,
        VictoryStinger = 4,
        DefeatStinger = 5,
        DrawStinger = 6,
        ResultsLoop = 7
    }

    /// <summary>What the shell is showing, reduced to what music cares about.</summary>
    public enum MusicContext : byte
    {
        Menu = 0,
        Loading = 1,
        Match = 2,
        Finished = 3
    }

    /// <summary>Local player's view of the final outcome.</summary>
    public enum LocalMatchOutcome : byte
    {
        Unknown = 0,
        Victory = 1,
        Defeat = 2,
        Draw = 3
    }

    /// <summary>Per-frame inputs from the shell; all presentation-side, nothing feeds back into authority.</summary>
    public readonly struct MusicInput
    {
        public MusicContext Context { get; }
        public bool Tension { get; }
        public LocalMatchOutcome Outcome { get; }
        public bool Paused { get; }
        /// <summary>0..1 from <see cref="SynthAudioMixer.LoudCueLevel"/>; beds duck under loud nearby combat.</summary>
        public float Duck { get; }
        /// <summary>True once the output reports the current non-looping cue has played to its end.</summary>
        public bool CurrentCueEnded { get; }

        public MusicInput(MusicContext context, bool tension = false, LocalMatchOutcome outcome = LocalMatchOutcome.Unknown,
            bool paused = false, float duck = 0f, bool currentCueEnded = false)
        {
            Context = context;
            Tension = tension;
            Outcome = outcome;
            Paused = paused;
            Duck = Math.Clamp(duck, 0f, 1f);
            CurrentCueEnded = currentCueEnded;
        }
    }

    /// <summary>What the output should be playing now. A new <see cref="Cue"/> means crossfade to it.</summary>
    public readonly struct MusicCommand : IEquatable<MusicCommand>
    {
        public MusicCue Cue { get; }
        public bool Loop { get; }
        /// <summary>Cue gain before the master × music volume (0..1).</summary>
        public float Gain { get; }
        public float CrossfadeSeconds { get; }

        public MusicCommand(MusicCue cue, bool loop, float gain, float crossfadeSeconds)
        {
            Cue = cue;
            Loop = loop;
            Gain = gain;
            CrossfadeSeconds = crossfadeSeconds;
        }

        public bool Equals(MusicCommand other) =>
            Cue == other.Cue && Loop == other.Loop && Gain == other.Gain && CrossfadeSeconds == other.CrossfadeSeconds;

        public override bool Equals(object? obj) => obj is MusicCommand other && Equals(other);
        public override int GetHashCode() => ((int)Cue * 397) ^ Gain.GetHashCode();
    }

    /// <summary>
    /// Pure music state machine (ADR-096): menu theme → silence while loading → calm bed that latches to a
    /// tension bed near the end → outcome stinger → results loop → menu. Unity-free; the adapter only
    /// crossfades between the cues it returns.
    /// </summary>
    public sealed class MusicDirector
    {
        public const float MenuGain = 0.85f;
        public const float BedGain = 0.5f;
        public const float StingerGain = 1f;
        public const float ResultsGain = 0.6f;
        /// <summary>About −6 dB at full duck.</summary>
        public const float DuckDepth = 0.5f;
        public const float PausedBedScale = 0.6f;
        public const float MenuFadeSeconds = 1.5f;
        public const float LoadingFadeSeconds = 1f;
        public const float TensionFadeSeconds = 3f;
        public const float StingerFadeSeconds = 0.4f;
        public const float ResultsFadeSeconds = 2f;
        /// <summary>The leading team is this close to the score limit.</summary>
        public const uint TensionKillMargin = 15;
        public const uint TensionRemainingActiveTicks = 60 * 60;

        private MusicContext context = (MusicContext)255;
        private MusicCue cue = MusicCue.None;
        private bool tensionLatched;
        private bool stingerDone;

        public MusicCue CurrentCue => cue;
        public bool TensionLatched => tensionLatched;

        public MusicCommand Update(in MusicInput input)
        {
            if (input.Context != context)
            {
                context = input.Context;
                if (context != MusicContext.Match) tensionLatched = false;
                stingerDone = false;
            }

            float crossfade;
            MusicCue next;
            bool loop;
            float gain;
            switch (context)
            {
                case MusicContext.Menu:
                    next = MusicCue.MenuTheme; loop = true; gain = MenuGain; crossfade = MenuFadeSeconds;
                    break;
                case MusicContext.Loading:
                    next = MusicCue.None; loop = false; gain = 0f; crossfade = LoadingFadeSeconds;
                    break;
                case MusicContext.Match:
                    if (input.Tension) tensionLatched = true;
                    next = tensionLatched ? MusicCue.MatchTension : MusicCue.MatchCalm;
                    loop = true;
                    gain = BedGain * (1f - DuckDepth * input.Duck) * (input.Paused ? PausedBedScale : 1f);
                    crossfade = cue == MusicCue.None ? LoadingFadeSeconds : TensionFadeSeconds;
                    break;
                default:
                    if (cue == StingerFor(input.Outcome) && input.CurrentCueEnded) stingerDone = true;
                    if (!stingerDone && input.Outcome != LocalMatchOutcome.Unknown)
                    {
                        next = StingerFor(input.Outcome); loop = false; gain = StingerGain; crossfade = StingerFadeSeconds;
                    }
                    else
                    {
                        next = MusicCue.ResultsLoop; loop = true; gain = ResultsGain; crossfade = ResultsFadeSeconds;
                    }
                    break;
            }
            cue = next;
            return new MusicCommand(next, loop, gain, crossfade);
        }

        public static MusicCue StingerFor(LocalMatchOutcome outcome) => outcome switch
        {
            LocalMatchOutcome.Victory => MusicCue.VictoryStinger,
            LocalMatchOutcome.Defeat => MusicCue.DefeatStinger,
            LocalMatchOutcome.Draw => MusicCue.DrawStinger,
            _ => MusicCue.None
        };

        /// <summary>Shell screen → music context. Settings and confirm dialogs keep the music of where they were opened.</summary>
        public static MusicContext ContextFor(ShellScreen screen, ShellScreen settingsReturn) => screen switch
        {
            ShellScreen.Loading => MusicContext.Loading,
            ShellScreen.Playing => MusicContext.Match,
            ShellScreen.Paused => MusicContext.Match,
            ShellScreen.ConfirmLeave => MusicContext.Match,
            ShellScreen.ConfirmRestart => MusicContext.Match,
            ShellScreen.Results => MusicContext.Finished,
            ShellScreen.Settings => settingsReturn == ShellScreen.Paused ? MusicContext.Match : MusicContext.Menu,
            ShellScreen.Armory => MusicContext.Menu,
            _ => MusicContext.Menu
        };

        /// <summary>Late-match tension: the leading team is near the score limit or the clock is nearly out.</summary>
        public static bool IsTense(uint team1Kills, uint team2Kills, int scoreLimit, uint remainingActiveTicks)
        {
            uint lead = Math.Max(team1Kills, team2Kills);
            bool nearScore = scoreLimit > 0 && lead + TensionKillMargin >= (uint)scoreLimit;
            bool nearTime = remainingActiveTicks > 0 && remainingActiveTicks <= TensionRemainingActiveTicks;
            return nearScore || nearTime;
        }

        /// <summary>Victory/defeat from the human's team; draw and unknown map through unchanged.</summary>
        public static LocalMatchOutcome OutcomeFor(MatchOutcome outcome, byte localTeam)
        {
            if (outcome == MatchOutcome.Draw) return LocalMatchOutcome.Draw;
            if (outcome == MatchOutcome.None || localTeam == 0) return LocalMatchOutcome.Unknown;
            return (byte)outcome == localTeam ? LocalMatchOutcome.Victory : LocalMatchOutcome.Defeat;
        }

        /// <summary>Team of the participant whose entity is the local human, or 0.</summary>
        public static byte LocalTeam(MatchResults results, EntityId human)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            for (int i = 0; i < results.FinalRows.Count; i++)
            {
                var row = results.FinalRows[i];
                if (row.IsHuman && (!human.IsValid || row.EntityId == human)) return row.Team;
            }
            return 0;
        }
    }
}
