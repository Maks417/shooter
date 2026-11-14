using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Shooter.Content.Maps;
using Shooter.Contracts;
using Shooter.Game.Contracts;
using Shooter.Game.Session;
using Shooter.Generation;
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
        private ISimulation simulation;
        private UnityWorldQueries world;
        private UnityNavigation navigation;
        private FixedTickClock clock;
        private Action<uint> tickAction;
        private Action<FoundationEntry> viewFactory;
        private long previousTimestamp;
        private double smokeSeconds;
        private bool reportedOverload;
        private bool diagnosticMode;
        private OfflineGameSession productSession;
        private CompoundMapRuntime productMap;
        private IMapCatalog? productCatalog;
        private Func<uint, InputCommand?> productHumanSampler;
        private bool hasApplicationFocus = true;
        private bool pausedForFocusLoss;
        public bool Ready { get; private set; }
        public bool Failed { get; private set; }
        public bool ProductMode => !diagnosticMode;
        public OfflineGameSession ProductSession => productSession;
        /// <summary>Read-only package-load seam for product presentation (T055/T056). Presentation must not mutate collision.</summary>
        public CompoundMapRuntime? ProductMap => productMap;
        /// <summary>Release catalog for shell setup (T059). Shell owns match Start; Bootstrap only prepares services.</summary>
        public IMapCatalog ProductCatalog =>
            productCatalog ?? throw new InvalidOperationException("Product catalog is only available in product mode.");
        public double LastElapsedSeconds { get; private set; }
        public double TickDebtSeconds => clock == null ? 0 : clock.DebtSeconds;
        public int LastAdvanceTicks => clock == null ? 0 : clock.LastAdvanceTicks;
        public uint CompletedTicks => clock == null ? 0 : clock.NextTick;
        public long LastStepAllocatedBytes { get; private set; }
        public double LastStepMilliseconds { get; private set; }
        public event Action<uint, double, long> StepCompleted;
        public event Action<Exception> SessionFailed;

        public void InstallView(Action<FoundationEntry> factory)
        {
            if (Ready || viewFactory != null || IsServer) throw new InvalidOperationException("Invalid client composition order");
            viewFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public void InstallProductHumanSampler(Func<uint, InputCommand?> sampler)
        {
            productHumanSampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
        }

        public GameOperationResult PauseProduct()
        {
            if (diagnosticMode || productSession == null)
                return new GameOperationResult(GameErrorCode.Busy, "no-product-session", "No product session is active.");
            var result = productSession.Pause();
            if (result.Succeeded) SuspendProductClock(true);
            return result;
        }

        public GameOperationResult ResumeProduct()
        {
            if (diagnosticMode || productSession == null)
                return new GameOperationResult(GameErrorCode.Busy, "no-product-session", "No product session is active.");
            var result = productSession.Resume();
            if (result.Succeeded) SuspendProductClock(false);
            return result;
        }

        private void Start()
        {
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                hasApplicationFocus = IsServer || Application.isFocused;
                if (IsServer) Application.targetFrameRate = 60;
                smokeSeconds = double.Parse(CommandLine("-shooterSmokeSeconds", "0"), CultureInfo.InvariantCulture);
                if (smokeSeconds < 0 || smokeSeconds > 30 || double.IsNaN(smokeSeconds)) throw new ArgumentException("Invalid smoke duration");
                string benchmarkWorkload = CommandLine("-shooterBenchmark", "");
                bool productTdmBenchmark = ProductTdmWorkloads.IsProductWorkload(benchmarkWorkload);
                diagnosticMode = smokeSeconds > 0 || CommandLine("-shooterDiagnostic", "0") == "1"
                    || CommandLine("-shooterContentGallery", "").Length != 0
                    || CommandLine("-shooterSoldierGallery", "").Length != 0
                    || (benchmarkWorkload.Length != 0 && !productTdmBenchmark);

                if (diagnosticMode) StartDiagnostic();
                else StartProduct();

                clock = new FixedTickClock(IsServer);
                clock.SetSuspended(!CanAdvanceProduct());
                AllocationMeter.Initialize();
                tickAction = Step;
                if (!IsServer)
                {
                    if (viewFactory == null) throw new InvalidOperationException("Client presentation factory is missing");
                    viewFactory(this);
                }
                monotonic.Start();
            }
            catch (Exception error) { Fail(error); }
        }

        private void StartDiagnostic()
        {
            CreateSolid("Harness floor", new Vector3(0, -.05f, 0), new Vector3(10, .1f, 10), 1, 100);
            CreateSolid("Harness box", new Vector3(0, .5f, 0), Vector3.one, 2, 101);
            Physics.SyncTransforms();
            world = new UnityWorldQueries(1);
            navigation = new UnityNavigation(1, _ => null);
            simulation = new CombatHarnessSimulation(world);
            CompositionPolicy.RequireReal(world, navigation, simulation);
            Debug.Log("SHOOTER_COMPOSITION mode=diagnostic");
        }

        private void StartProduct()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string lockPath = CommandLine("-shooterToolchainLock", Path.Combine(repositoryRoot, "toolchain-lock.json"));
            if (!File.Exists(lockPath)) throw new FileNotFoundException("toolchain-lock.json required for verified map packages.", lockPath);
            var toolchain = ContentIdentity.Hash(File.ReadAllBytes(lockPath));
            var catalog = StaticMapCatalog.CreateReleaseSeed(toolchain);
            productCatalog = catalog;
            string mapCache = CommandLine("-shooterMapCache", Path.Combine(repositoryRoot, "Artifacts", "map-packages"));
            mapCache = Path.GetFullPath(mapCache);
            if (!Directory.Exists(mapCache))
                throw new DirectoryNotFoundException("Verified map package cache missing. Run Tools/Compile-GameContent.ps1 first: " + mapCache);
            var packageStore = new VerifiedMapPackageStore(mapCache, toolchain, catalog.GameplayRootHashes);
            productMap = new CompoundMapRuntime(transform, packageStore, id =>
            {
                if (!catalog.TryGet(id, out var catalogEntry)) throw new InvalidOperationException("Unknown map " + id.Value);
                return catalogEntry.GameplayRootHash;
            });
            productSession = new OfflineGameSession(catalog, (_, __) => productMap, allowFakeMapRuntime: false);
            CompositionPolicy.RequireReal(productMap);
            // T059: launch to main menu with no active match. Shell owns BeginLoad/Start/replay.
            Debug.Log("SHOOTER_COMPOSITION mode=product shellOwned=1 phase=Stopped");
        }

        /// <summary>Bind actor feet lookup after a shell-owned map load (T056/T059).</summary>
        public void WireProductMapActors()
        {
            if (productMap == null || productSession == null) return;
            productMap.ActorLocator = id =>
            {
                if (productSession.Lifecycle != null && productSession.Lifecycle.TryGetActorState(id, out var state))
                    return state.Feet;
                return null;
            };
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
            long before = AllocationMeter.ThreadBytes, start = Stopwatch.GetTimestamp();
            if (diagnosticMode)
            {
                simulation.Step(tick, ReadOnlyMemory<InputCommand>.Empty);
            }
            else
            {
                InputCommand? human = productHumanSampler?.Invoke(tick);
                var step = productSession.Step(human);
                if (!step.Succeeded && productSession.Phase != GameMatchPhase.Finished)
                    throw new InvalidOperationException("Product step failed: " + step.Diagnostic);
            }
            LastStepAllocatedBytes = before < 0 ? -1 : AllocationMeter.ThreadBytes - before;
            LastStepMilliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            StepCompleted?.Invoke(tick, LastStepMilliseconds, LastStepAllocatedBytes);
        }

        private void Update()
        {
            if (clock == null || Failed) return;
            try
            {
                if (!Ready)
                {
                    if (!IsServer && monotonic.Elapsed.TotalSeconds < 2) return;
                    Ready = true; monotonic.Restart(); previousTimestamp = 0;
                    Debug.Log(IsServer ? "SHOOTER_READY_SERVER" : "SHOOTER_READY_CLIENT");
                    return;
                }
                long now = monotonic.ElapsedTicks;
                LastElapsedSeconds = (now - previousTimestamp) / (double)Stopwatch.Frequency;
                bool canAdvance = diagnosticMode || CanAdvanceProduct();
                clock.SetSuspended(!canAdvance);
                if (!canAdvance)
                {
                    LastElapsedSeconds = 0;
                    previousTimestamp = now;
                    return;
                }
                clock.Advance(LastElapsedSeconds, tickAction);
                previousTimestamp = now;
                if (IsServer && clock.Overloaded && !reportedOverload)
                    Debug.LogWarning("SHOOTER_TICK_OVERLOAD debtSeconds=" + clock.DebtSeconds.ToString("R", CultureInfo.InvariantCulture));
                reportedOverload = clock.Overloaded;
                if (clock.Overloaded && !IsServer) throw new InvalidOperationException("Offline tick overload; simulation paused with debt=" + clock.DebtSeconds.ToString("R", CultureInfo.InvariantCulture)
                    + " elapsed=" + LastElapsedSeconds.ToString("R", CultureInfo.InvariantCulture) + " serviced=" + clock.LastAdvanceTicks + " nextTick=" + clock.NextTick);
                if (smokeSeconds > 0 && monotonic.Elapsed.TotalSeconds >= smokeSeconds)
                {
                    if (CompletedTicks < 60) throw new InvalidOperationException("Smoke did not service 60 simulation ticks");
                    Debug.Log("SHOOTER_SMOKE_PASS mode=" + (IsServer ? "Server" : "Client") + " ticks=" + CompletedTicks);
                    Application.Quit(0); enabled = false;
                }
            }
            catch (Exception error) { Fail(error); }
        }

        private bool CanAdvanceProduct()
        {
            return diagnosticMode
                || productSession != null
                && productSession.Phase == GameMatchPhase.Playing
                && productSession.PauseState == SessionPauseState.Running
                && (IsServer || hasApplicationFocus);
        }

        private void SuspendProductClock(bool suspended)
        {
            if (clock != null) clock.SetSuspended(suspended);
            if (monotonic.IsRunning) previousTimestamp = monotonic.ElapsedTicks;
            LastElapsedSeconds = 0;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            hasApplicationFocus = hasFocus;
            if (diagnosticMode || IsServer || productSession == null) return;
            if (!hasFocus)
            {
                pausedForFocusLoss = productSession.Phase == GameMatchPhase.Playing
                    && productSession.PauseState == SessionPauseState.Running
                    && PauseProduct().Succeeded;
                SuspendProductClock(true);
            }
            else
            {
                if (pausedForFocusLoss) ResumeProduct();
                pausedForFocusLoss = false;
                SuspendProductClock(!CanAdvanceProduct());
            }
        }

        public void Fail(Exception error)
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            if (Failed) return;
            Failed = true; enabled = false;
            Debug.LogError("SHOOTER_FAILURE: " + error);
            if (SessionFailed != null)
                foreach (Action<Exception> observer in SessionFailed.GetInvocationList())
                    try { observer(error); } catch (Exception cleanup) { Debug.LogError("SHOOTER_CLEANUP_FAILURE: " + cleanup); }
            if (!Application.isEditor) Application.Quit(1);
        }

        private void OnDestroy()
        {
            productSession?.Dispose();
            productMap?.Dispose();
            world?.Dispose();
        }
    }
}
