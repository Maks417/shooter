using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Shooter.Bootstrap
{
    /// <summary>Allocation events, not heap-size deltas. Calibrated before any measured workload.</summary>
    public static class AllocationMeter
    {
        private static bool initialized, nativeAvailable, threadAvailable;
        private static object controlRoot;
        public static bool AllThreadsAvailable => nativeAvailable;
        public static bool ThreadAvailable => threadAvailable;
        public static long MainControlBytes { get; private set; }
        public static long WorkerControlBytes { get; private set; }
        public static string Method { get; private set; } = "Unavailable";
        public static string Diagnostic { get; private set; } = "Not calibrated";
        public static long TotalBytes =>
#if ENABLE_IL2CPP && UNITY_STANDALONE_WIN && !UNITY_EDITOR
            nativeAvailable ? shooter_alloc_total_bytes() : -1;
#else
            -1;
#endif
        public static long ThreadBytes =>
#if ENABLE_IL2CPP && UNITY_STANDALONE_WIN && !UNITY_EDITOR
            !threadAvailable ? -1 : shooter_alloc_thread_bytes();
#else
            !threadAvailable ? -1 : GC.GetAllocatedBytesForCurrentThread();
#endif

#if ENABLE_IL2CPP && UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // Pure native counter (Assets/Plugins/x86_64/ShooterAllocationMeter.cpp, compiled into GameAssembly): no managed callback exists.
        [DllImport("__Internal")] private static extern int shooter_alloc_install();
        [DllImport("__Internal")] private static extern long shooter_alloc_total_bytes();
        [DllImport("__Internal")] private static extern long shooter_alloc_thread_bytes();
#endif
        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            try
            {
#if ENABLE_IL2CPP && UNITY_STANDALONE_WIN && !UNITY_EDITOR
                // One bounded, process-lifetime native registration; the callback is pure C++ (no managed re-entry).
                shooter_alloc_install();
                long before = shooter_alloc_thread_bytes();
                AllocateControls();
                MainControlBytes = shooter_alloc_thread_bytes() - before;
                long worker = 0, allBefore = shooter_alloc_total_bytes();
                var thread = new Thread(() => { long start = shooter_alloc_thread_bytes(); AllocateControls(); worker = shooter_alloc_thread_bytes() - start; });
                thread.Start(); thread.Join();
                WorkerControlBytes = worker;
                nativeAvailable = MainControlBytes >= 4352 && worker >= 4352 && shooter_alloc_total_bytes() - allBefore >= worker;
                threadAvailable = nativeAvailable;
                Method = "Native C++ IL2CPP allocation callback (no managed callback): object sizes, all threads plus thread-local tick counter";
                Diagnostic = nativeAvailable ? "Object/string/array and worker-thread positive controls passed" : "Native positive control failed; zeros are not measurements";
#else
                long before = GC.GetAllocatedBytesForCurrentThread();
                AllocateControls();
                MainControlBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                threadAvailable = MainControlBytes >= 4352;
                Method = "Calibrated BCL thread counter only; whole-frame/all-thread allocation unavailable";
                Diagnostic = threadAvailable ? "Thread positive control passed" : "Thread positive control failed; zeros are not measurements";
#endif
            }
            catch (Exception error)
            {
                nativeAvailable = threadAvailable = false;
                Diagnostic = error.GetType().Name + ": " + error.Message;
            }
            controlRoot = null;
        }
        private static void AllocateControls()
        {
            controlRoot = new byte[4096]; GC.KeepAlive(controlRoot);
            controlRoot = new string('x', 128); GC.KeepAlive(controlRoot);
            controlRoot = new object(); GC.KeepAlive(controlRoot);
        }
    }
}
