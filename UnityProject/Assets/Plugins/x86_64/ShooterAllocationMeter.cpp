// Benchmark-only managed allocation counter. Compiled into GameAssembly by IL2CPP (Standalone Windows only).
// The profiler callback is pure native and allocation-free: it only adds the object size to a process-wide atomic
// and to a thread-local counter. It makes no managed calls, takes no locks and allocates nothing, so IL2CPP's own
// Thread::Attach allocations are merely counted and can never re-enter the callback.
// Signatures verified against the pinned 6000.3.28f1 libil2cpp/il2cpp-api-functions.h and il2cpp-api-types.h.
#include <atomic>
#include <cstdint>

struct Il2CppProfiler;
struct Il2CppObject;
struct Il2CppClass;
typedef void (*Il2CppProfileFunc)(Il2CppProfiler* prof);
typedef void (*Il2CppProfileAllocFunc)(Il2CppProfiler* prof, Il2CppObject* obj, Il2CppClass* klass);
static const int ShooterProfileAllocations = 1 << 7; // IL2CPP_PROFILE_ALLOCATIONS

extern "C"
{
    void il2cpp_profiler_install(Il2CppProfiler* prof, Il2CppProfileFunc shutdown_callback);
    void il2cpp_profiler_install_allocation(Il2CppProfileAllocFunc callback);
    void il2cpp_profiler_set_events(int events);
    uint32_t il2cpp_object_get_size(Il2CppObject* obj);
}

namespace
{
    std::atomic<int64_t> g_totalBytes(0);
    std::atomic<int> g_installed(0);
    thread_local int64_t t_threadBytes = 0;

    void OnAllocation(Il2CppProfiler*, Il2CppObject* obj, Il2CppClass*)
    {
        const int64_t size = (int64_t)il2cpp_object_get_size(obj);
        t_threadBytes += size;
        g_totalBytes.fetch_add(size, std::memory_order_relaxed);
    }
}

extern "C"
{
    // Idempotent. Returns 1 once installed.
    int shooter_alloc_install()
    {
        int expected = 0;
        if (g_installed.compare_exchange_strong(expected, 1))
        {
            il2cpp_profiler_install(reinterpret_cast<Il2CppProfiler*>(static_cast<uintptr_t>(0x53484f54)), nullptr);
            il2cpp_profiler_install_allocation(&OnAllocation);
            il2cpp_profiler_set_events(ShooterProfileAllocations);
        }
        return 1;
    }
    int64_t shooter_alloc_total_bytes() { return g_totalBytes.load(std::memory_order_relaxed); }
    int64_t shooter_alloc_thread_bytes() { return t_threadBytes; }
}
