using System;
using System.Diagnostics;
using System.Globalization;
using Shooter.Contracts;
using Shooter.Simulation;
using Shooter.World;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Shooter.Bootstrap
{
    public sealed class FoundationEntry : MonoBehaviour
    {
#if UNITY_SERVER
        public const bool IsServer = true;
#else
        public const bool IsServer = false;
#endif
        private readonly Stopwatch monotonic = new Stopwatch();
        private FoundationSimulation simulation;
        private UnityWorldQueries world;
        private UnityNavigation navigation;
        private FixedTickClock clock;
        private Action<uint> tickAction;
        private Action<FoundationEntry> viewFactory;
        private long previousTimestamp;
        private double smokeSeconds;
        private bool reportedOverload;
        public bool Ready { get; private set; }
        public uint CompletedTicks => clock == null ? 0 : clock.NextTick;
        public long LastStepAllocatedBytes { get; private set; }
        public double LastStepMilliseconds { get; private set; }
        public event Action<uint, double, long> StepCompleted;
        public void InstallView(Action<FoundationEntry> factory)
        {
            if (Ready || viewFactory != null || IsServer) throw new InvalidOperationException("Invalid client composition order");
            viewFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
        private void Start()
        {
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                if (IsServer) Application.targetFrameRate = 60;
                CreateSolid("Harness floor", new Vector3(0, -.05f, 0), new Vector3(10, .1f, 10), 1, 100);
                CreateSolid("Harness box", new Vector3(0, .5f, 0), Vector3.one, 2, 101);
                Physics.SyncTransforms();
                world = new UnityWorldQueries(1);
                navigation = new UnityNavigation(1, _ => null); // Empty M0 authority has no actors; queries fail MissingContent.
                simulation = new FoundationSimulation();
                CompositionPolicy.RequireReal(world, navigation, simulation);
                clock = new FixedTickClock(IsServer);
                tickAction = Step;
                string seconds = CommandLine("-shooterSmokeSeconds", "0");
                smokeSeconds = double.Parse(seconds, CultureInfo.InvariantCulture);
                if (smokeSeconds < 0 || smokeSeconds > 30 || double.IsNaN(smokeSeconds)) throw new ArgumentException("Invalid smoke duration");
                if (!IsServer)
                {
                    if (viewFactory == null) throw new InvalidOperationException("Client presentation factory is missing");
                    viewFactory(this);
                }
                monotonic.Start();
            }
            catch (Exception error) { Fail(error); }
        }
        public static string CommandLine(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) if (args[i] == name)
            { if (i + 1 >= args.Length) throw new ArgumentException("Missing " + name); return args[i + 1]; }
            return fallback;
        }
        private void CreateSolid(string label, Vector3 position, Vector3 size, ulong stableId, ulong surfaceId)
        {
            var item = new GameObject(label) { layer = 8, isStatic = true };
            item.transform.SetParent(transform, false); item.transform.position = position;
            item.AddComponent<BoxCollider>().size = size;
            item.AddComponent<StableSurface>().Initialize(stableId, surfaceId, 1);
        }
        private void Step(uint tick)
        {
            long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
            simulation.Step(tick, ReadOnlyMemory<InputCommand>.Empty);
            LastStepAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            LastStepMilliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            StepCompleted?.Invoke(tick, LastStepMilliseconds, LastStepAllocatedBytes);
        }
        private void Update()
        {
            if (clock == null) return;
            try
            {
                if (!Ready)
                {
                    // Initial shader/renderer setup is loading. Simulation time starts at the readiness boundary.
                    if (!IsServer && monotonic.Elapsed.TotalSeconds < 2) return;
                    Ready = true; monotonic.Restart(); previousTimestamp = 0;
                    Debug.Log(IsServer ? "SHOOTER_READY_SERVER" : "SHOOTER_READY_CLIENT");
                    return;
                }
                long now = monotonic.ElapsedTicks;
                clock.Advance((now - previousTimestamp) / (double)Stopwatch.Frequency, tickAction);
                previousTimestamp = now;
                if (IsServer && clock.Overloaded && !reportedOverload)
                    Debug.LogWarning("SHOOTER_TICK_OVERLOAD debtSeconds=" + clock.DebtSeconds.ToString("R", CultureInfo.InvariantCulture));
                reportedOverload = clock.Overloaded;
                if (clock.Overloaded && !IsServer) throw new InvalidOperationException("Offline tick overload; simulation paused with debt=" + clock.DebtSeconds);
                if (smokeSeconds > 0 && monotonic.Elapsed.TotalSeconds >= smokeSeconds)
                {
                    if (CompletedTicks < 60) throw new InvalidOperationException("Smoke did not service 60 simulation ticks");
                    Debug.Log("SHOOTER_SMOKE_PASS mode=" + (IsServer ? "Server" : "Client") + " ticks=" + CompletedTicks);
                    Application.Quit(0); enabled = false;
                }
            }
            catch (Exception error) { Fail(error); }
        }
        private void Fail(Exception error)
        { Debug.LogError("SHOOTER_FAILURE: " + error); enabled = false; if (!Application.isEditor) Application.Quit(1); }
        private void OnDestroy() { world?.Dispose(); }
    }
}
