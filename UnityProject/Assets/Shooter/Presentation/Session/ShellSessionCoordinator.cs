#nullable enable
using System;
using Shooter.Game.Contracts;

namespace Shooter.Presentation.Session
{
    /// <summary>
    /// Pure start-once / cancel / results-retention policy for the product shell.
    /// Actual BeginLoadAsync/Start/Stop remain on OfflineGameSession; this blocks double Start.
    /// </summary>
    public sealed class ShellSessionCoordinator
    {
        public bool StartInFlight { get; private set; }
        public bool MatchActive { get; private set; }
        public SessionRequest? ActiveRequest { get; private set; }
        public SessionRequest? LastStartedRequest { get; private set; }
        public MatchResults? RetainedResults { get; private set; }
        public ulong LoadEpoch { get; private set; }

        public bool TryBeginStart(SessionRequest request, out string error)
        {
            error = "";
            if (request == null)
            {
                error = "Missing session request.";
                return false;
            }
            if (StartInFlight)
            {
                error = "A match start is already in progress.";
                return false;
            }
            if (MatchActive)
            {
                error = "A match is already active.";
                return false;
            }

            ActiveRequest = request;
            StartInFlight = true;
            LoadEpoch++;
            return true;
        }

        public void CompleteStartSuccess()
        {
            if (!StartInFlight) return;
            StartInFlight = false;
            MatchActive = true;
            LastStartedRequest = ActiveRequest;
        }

        public void CompleteStartFailure()
        {
            StartInFlight = false;
            MatchActive = false;
            ActiveRequest = null;
        }

        public void CancelStart()
        {
            StartInFlight = false;
            MatchActive = false;
            ActiveRequest = null;
        }

        public void RetainResults(MatchResults results)
        {
            RetainedResults = results ?? throw new ArgumentNullException(nameof(results));
        }

        public void MarkMatchFinished(MatchResults? results)
        {
            MatchActive = false;
            StartInFlight = false;
            if (results != null) RetainedResults = results;
        }

        public void LeaveToMenu()
        {
            StartInFlight = false;
            MatchActive = false;
            ActiveRequest = null;
            // RetainedResults kept for display until next successful start replaces them.
        }

        public void PrepareReplay(out SessionRequest? request, out string error)
        {
            request = LastStartedRequest ?? ActiveRequest;
            if (request == null)
            {
                error = "No prior match to replay.";
                return;
            }
            // Tear down prior match bookkeeping so TryBeginStart can accept the same request once.
            StartInFlight = false;
            MatchActive = false;
            ActiveRequest = null;
            error = "";
        }

        public bool SameLoadEpoch(ulong epoch) => epoch == LoadEpoch;
    }
}
