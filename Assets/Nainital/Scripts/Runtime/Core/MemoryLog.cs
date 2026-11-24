using UnityEngine;
using UnityEngine.Profiling;

namespace Nainital
{
    /// <summary>
    /// Prints the app's memory to the device log (24 Nov 2026: iOS killed the game a few seconds into play,
    /// "exceeded mem limit: ActiveHard 3072 MB", with no crash report). On iOS the number is the physical footprint
    /// (Plugins/iOS/NainitalMemory.mm), the one iOS checks against the limit; elsewhere Unity's reserved memory.
    /// Every 2 s for the first 3 minutes, then every 15 s, and on each new peak. On a low-memory warning it unloads
    /// unused assets. Read it with idevicesyslog: lines "[Nainital] mem ...".
    /// </summary>
    public class MemoryLog : MonoBehaviour
    {
#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern long _NainitalFootprintBytes();
        static long Footprint() => _NainitalFootprintBytes();
#else
        static long Footprint() => Profiler.GetTotalReservedMemoryLong();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<MemoryLog>()) return;
            var go = new GameObject("MemoryLog") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<MemoryLog>();
        }

        float next;
        long peak, loggedPeak;

        static long Mb(long b) => b / (1024 * 1024);

        void OnEnable() { Application.lowMemory += Low; }
        void OnDisable() { Application.lowMemory -= Low; }

        void Low()
        {
            Debug.Log($"[Nainital] mem LOW MEMORY warning at {Mb(Footprint())} MB: unloading unused assets");
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }

        void Update()
        {
            long f = Footprint();
            if (f > peak) peak = f;
            bool newPeak = peak > loggedPeak + (64L << 20);   // a jump of 64 MB since the last line: log it now
            if (!newPeak && Time.realtimeSinceStartup < next) return;
            loggedPeak = peak;
            next = Time.realtimeSinceStartup + (Time.realtimeSinceStartup < 180f ? 2f : 15f);
            Debug.Log($"[Nainital] mem {Mb(f)} MB (peak {Mb(peak)}) unity {Mb(Profiler.GetTotalAllocatedMemoryLong())} " +
                      $"mono {Mb(Profiler.GetMonoUsedSizeLong())} t {Time.realtimeSinceStartup:F0}s scene " +
                      UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }
}
