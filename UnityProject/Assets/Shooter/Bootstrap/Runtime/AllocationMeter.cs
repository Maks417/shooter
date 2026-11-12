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
        private static long totalBytes;
        [ThreadStatic] private static long threadBytes;
        public static bool AllThreadsAvailable => nativeAvailable;
        public static bool ThreadAvailable => threadAvailable;
        public static long MainControlBytes { get; private set; }
        public static long WorkerControlBytes { get; private set; }
        public static string Method { get; private set; } = "Unavailable";
        public static string Diagnostic { get; private set; } = "Not calibrated";
        public static long TotalBytes => nativeAvailable ? Interlocked.Read(ref totalBytes) : -1;
        public static long ThreadBytes => !threadAvailable ? -1 : nativeAvailable ? threadBytes : GC.GetAllocatedBytesForCurrentThread();

#if ENABLE_IL2CPP && UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AllocationCallback(IntPtr profiler, IntPtr obj, IntPtr klass);
        private static readonly AllocationCallback callback = Allocated;
        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)] private static extern void il2cpp_profiler_install(IntPtr context, IntPtr shutdown);
        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)] private static extern void il2cpp_profiler_install_allocation(AllocationCallback callback);
        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)] private static extern void il2cpp_profiler_set_events(int events);
        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)] private static extern uint il2cpp_object_get_size(IntPtr obj);
        [AOT.MonoPInvokeCallback(typeof(AllocationCallback))]
        private static void Allocated(IntPtr profiler, IntPtr obj, IntPtr klass)
        {
            // The pinned IL2CPP invokes this for objects, strings and arrays, on the allocating thread.
            // No allocation, Unity API, reflection or logging is permitted inside this callback.
            long bytes = il2cpp_object_get_size(obj);
            threadBytes += bytes;
            Interlocked.Add(ref totalBytes, bytes);
        }
#endif
        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            try
            {
#if ENABLE_IL2CPP && UNITY_STANDALONE_WIN && !UNITY_EDITOR
                // One bounded, process-lifetime profiler registration. No engine/package edits or development build.
                il2cpp_profiler_install(new IntPtr(0x53484f54), IntPtr.Zero);
                il2cpp_profiler_install_allocation(callback);
                il2cpp_profiler_set_events(1 << 7); // IL2CPP_PROFILE_ALLOCATIONS in the pinned il2cpp-api-types.h.
                long before = threadBytes;
                AllocateControls();
                MainControlBytes = threadBytes - before;
                long worker = 0, allBefore = Interlocked.Read(ref totalBytes);
                var thread = new Thread(() => { long start = threadBytes; AllocateControls(); worker = threadBytes - start; });
                thread.Start(); thread.Join();
                WorkerControlBytes = worker;
                nativeAvailable = MainControlBytes >= 4352 && worker >= 4352 && Interlocked.Read(ref totalBytes) - allBefore >= worker;
                threadAvailable = nativeAvailable;
                Method = "Pinned IL2CPP allocation callbacks: object sizes, all threads plus thread-local tick counter";
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
