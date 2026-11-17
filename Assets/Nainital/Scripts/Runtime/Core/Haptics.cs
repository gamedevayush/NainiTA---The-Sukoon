using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Short taps on the phone (user, 2026-11-17: "basic vibrations wgera"): a tick for a button, a light tap for a punch
    /// or money, a medium one for a hit taken, a heavy one for a crash, and the success / warning / error patterns for a
    /// mission passed, a warning, a mission failed. iPhone: the Taptic Engine (Plugins/iOS/NainitalHaptics.mm). Android:
    /// the vibrator's predefined clicks (API 29+) or a short pulse at a strength. Off in Settings, it does nothing.
    /// </summary>
    public static class Haptics
    {
        public enum Kind { Selection = 0, Light = 1, Medium = 2, Heavy = 3, Success = 4, Warning = 5, Error = 6, Soft = 7, Rigid = 8 }

        static float lastAt = -1f;

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void _NainitalHaptic(int kind);
#endif

        public static void Selection() => Play(Kind.Selection);
        public static void Light() => Play(Kind.Light);
        public static void Medium() => Play(Kind.Medium);
        public static void Heavy() => Play(Kind.Heavy);
        public static void Success() => Play(Kind.Success);
        public static void Warning() => Play(Kind.Warning);
        public static void Error() => Play(Kind.Error);

        public static void Play(Kind k)
        {
            if (!GameSettings.HapticsOn || !Application.isMobilePlatform) return;
            float now = Time.unscaledTime;
            // taps closer than 35 ms blur into one buzz; a pattern always plays
            if (k <= Kind.Heavy && now - lastAt < 0.035f) return;
            lastAt = now;
#if UNITY_IOS && !UNITY_EDITOR
            _NainitalHaptic((int)k);
#elif UNITY_ANDROID && !UNITY_EDITOR
            Android(k);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdk = -1;
        static bool failed;

        static void Android(Kind k)
        {
            if (failed) return;
            try
            {
                if (vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using (var ver = new AndroidJavaClass("android.os.Build$VERSION")) sdk = ver.GetStatic<int>("SDK_INT");
                }
                if (vibrator == null || !vibrator.Call<bool>("hasVibrator")) { failed = true; return; }
                if (sdk >= 29 && k != Kind.Success && k != Kind.Warning && k != Kind.Error)
                {
                    // EFFECT_TICK 2, EFFECT_CLICK 0, EFFECT_HEAVY_CLICK 5
                    int effect = k == Kind.Selection || k == Kind.Soft ? 2 : k == Kind.Light || k == Kind.Rigid ? 0 : 5;
                    if (k == Kind.Heavy) { OneShot(60, 255); return; }
                    using (var ve = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var e = ve.CallStatic<AndroidJavaObject>("createPredefined", effect))
                        vibrator.Call("vibrate", e);
                    return;
                }
                switch (k)
                {
                    case Kind.Selection: case Kind.Soft: OneShot(8, 70); break;
                    case Kind.Light: case Kind.Rigid: OneShot(14, 110); break;
                    case Kind.Medium: OneShot(28, 170); break;
                    case Kind.Heavy: OneShot(60, 255); break;
                    case Kind.Success: Pattern(new long[] { 0, 18, 70, 30 }, new[] { 0, 140, 0, 220 }); break;
                    case Kind.Warning: Pattern(new long[] { 0, 30, 90, 30 }, new[] { 0, 200, 0, 200 }); break;
                    case Kind.Error: Pattern(new long[] { 0, 35, 60, 35, 60, 45 }, new[] { 0, 230, 0, 230, 0, 255 }); break;
                }
            }
            catch (System.Exception)
            {
                failed = true;
                Handheld.Vibrate();   // (also tells Unity the app needs the VIBRATE permission)
            }
        }

        static void OneShot(long ms, int amp)
        {
            if (sdk >= 26)
                using (var ve = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var e = ve.CallStatic<AndroidJavaObject>("createOneShot", ms, amp))
                    vibrator.Call("vibrate", e);
            else vibrator.Call("vibrate", ms);
        }

        static void Pattern(long[] timings, int[] amps)
        {
            if (sdk >= 26)
                using (var ve = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var e = ve.CallStatic<AndroidJavaObject>("createWaveform", timings, amps, -1))
                    vibrator.Call("vibrate", e);
            else vibrator.Call("vibrate", timings, -1);
        }
#endif
    }
}
