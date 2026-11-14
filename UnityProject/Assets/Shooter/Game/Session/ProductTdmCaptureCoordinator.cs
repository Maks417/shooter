#nullable enable
using System;
using Shooter.Game.Contracts;

namespace Shooter.Game.Session
{
    /// <summary>
    /// Pure finish / results / replay policy for product TDM measurement (T070).
    /// Does not alter score or time-limit rules; soak reaches capture ticks by replaying finished matches.
    /// </summary>
    public sealed class ProductTdmCaptureCoordinator
    {
        public enum Phase
        {
            Idle = 0,
            Loading = 1,
            Playing = 2,
            Finished = 3,
            Results = 4,
            Replaying = 5
        }

        public Phase Current { get; private set; } = Phase.Idle;
        public SessionRequest? ActiveRequest { get; private set; }
        public SessionRequest? LastStartedRequest { get; private set; }
        public MatchResults? RetainedResults { get; private set; }
        public int MatchesCompleted { get; private set; }
        public int MatchIndex { get; private set; }
        public ulong LoadEpoch { get; private set; }
        public long CumulativeActiveTicks { get; private set; }

        public bool TryBeginLoad(SessionRequest request, out string error)
        {
            error = "";
            if (request == null)
            {
                error = "Missing session request.";
                return false;
            }
            if (Current == Phase.Loading || Current == Phase.Replaying)
            {
                error = "A match load is already in progress.";
                return false;
            }
            if (Current == Phase.Playing)
            {
                error = "A match is already playing.";
                return false;
            }

            ActiveRequest = request;
            LoadEpoch++;
            MatchIndex = MatchesCompleted + 1;
            Current = Phase.Loading;
            return true;
        }

        public void CompleteLoadSuccess()
        {
            if (Current != Phase.Loading && Current != Phase.Replaying) return;
            LastStartedRequest = ActiveRequest;
            Current = Phase.Playing;
        }

        public void CompleteLoadFailure()
        {
            Current = Phase.Idle;
            ActiveRequest = null;
        }

        public void MarkFinished(MatchResults? results, long matchActiveTicks)
        {
            if (Current != Phase.Playing) return;
            if (results != null) RetainedResults = results;
            if (matchActiveTicks > 0) CumulativeActiveTicks += matchActiveTicks;
            MatchesCompleted++;
            Current = Phase.Finished;
        }

        public void EnterResults()
        {
            if (Current != Phase.Finished) return;
            Current = Phase.Results;
        }

        public bool NeedsReplay(int captureTicks, long completedClockTicks, bool soak)
        {
            if (LastStartedRequest == null) return false;
            // Keep replaying until the capture clock window is filled (never by extending match rules).
            if (completedClockTicks < captureTicks) return true;
            // Soak also requires having entered a second match; MatchIndex>=2 is enough (may still be playing).
            if (soak && MatchIndex < 2) return true;
            return false;
        }

        public bool TryPrepareReplay(out SessionRequest? request, out string error)
        {
            request = LastStartedRequest ?? ActiveRequest;
            if (request == null)
            {
                error = "No prior match to replay.";
                return false;
            }
            if (Current != Phase.Results && Current != Phase.Finished)
            {
                error = "Replay requires a finished match.";
                return false;
            }

            ActiveRequest = null;
            Current = Phase.Idle;
            error = "";
            return TryBeginLoad(request, out error) && MoveToReplaying();
        }

        private bool MoveToReplaying()
        {
            if (Current != Phase.Loading) return false;
            Current = Phase.Replaying;
            return true;
        }

        public bool SameLoadEpoch(ulong epoch) => epoch == LoadEpoch;
    }
}
