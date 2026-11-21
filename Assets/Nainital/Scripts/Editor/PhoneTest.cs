using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Play tests for the phone (22 Nov 2026) through the bridge, after "call PlayTest.Begin":
    /// call PhoneTest.Prank (100, "mazaak tha"), PhoneTest.Fire (a fire 60-90 m off, then 101), PhoneTest.Hurt108 (the
    /// player at 35 % health, then 108 "main khud"), PhoneTest.Shot (camera: one photo), PhoneTest.Selfie, PhoneTest.Open;
    /// then "call PhoneTest.Report" now and then: money, stars, police / tender / ambulance states, fires, photos.
    /// </summary>
    public static class PhoneTest
    {
        static void Need() { if (!EditorApplication.isPlaying) throw new System.Exception("not playing"); }
        static int money0 = -1;
        static float t0;

        static void Talk(string number, params int[] picks)
        {
            var n = EmergencyCalls.Dial(number);
            var sb = new StringBuilder($"[Nainital] call {number}: \"{n.line}\"");
            foreach (int k in picks)
            {
                if (n.End || k >= n.replies.Count) break;
                sb.Append($" -> [{n.replies[k].label}] -> \"");
                n = n.replies[k].next();
                sb.Append(n.line + "\"");
            }
            Debug.Log(sb.ToString());
        }

        static Vector3 PP => PlayerController.Instance ? PlayerController.Instance.transform.position : Vector3.zero;

        public static void Prank()
        {
            Need();
            money0 = GameState.Money;
            t0 = Time.time;
            Talk("100", 2);
        }

        public static void Fire()
        {
            Need();
            t0 = Time.time;
            bool ok = false;
            for (int i = 0; i < 6 && !ok; i++) ok = FireManager.TryStart(PP);
            FireManager.NearestFire(PP, 300f, out var at, out var src);
            Debug.Log($"[Nainital] fire started {ok} at {at} ({(at - PP).magnitude:F0} m) by {(src ? src.GetType().Name : "none")}");
            // the test calls from beside it (the call checks the flames within 90 m)
            Talk("101", 0);
        }

        public static void Hurt108()
        {
            Need();
            t0 = Time.time;
            var h = PlayerController.Instance.Health;
            h.current = h.max * 0.35f;
            Talk("108", 0);
        }

        public static void Open() { Need(); Phone.Instance.Open(); }
        public static void Hindi() { Loc.Set(Loc.Lang.Hindi); }
        static string Arg() { try { return System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(); } catch { return ""; } }
        /// <summary>arg: an app id (phone, sos, gallery, settings, weather, wallet).</summary>
        public static void App() { Need(); Phone.Instance.TestApp(Arg()); }
        /// <summary>arg: digits to put on the keypad.</summary>
        public static void Type() { Need(); Phone.Instance.TestType(Arg()); }
        public static void DialNow() { Need(); Phone.Instance.TestCall(); }
        /// <summary>arg: the reply's index.</summary>
        public static void Reply() { Need(); int.TryParse(Arg(), out int i); Phone.Instance.TestReply(i); }
        public static void English() { Loc.Set(Loc.Lang.English); }
        public static void ToastTest() { Need(); GameEvents.ShowToast("Petrol khatam test", 6f); }
        public static void Shot() { Need(); var c = PhoneCamera.Ensure(); if (!PhoneCamera.IsOpen) c.Open(false); c.Snap(); }
        public static void Selfie() { Need(); var c = PhoneCamera.Ensure(); if (!PhoneCamera.IsOpen) c.Open(true); }
        public static void CloseAll() { Need(); if (PhoneCamera.IsOpen) PhoneCamera.Instance.Close(); if (Phone.IsOpen) Phone.Instance.Close(); }

        public static void Report()
        {
            Need();
            var sb = new StringBuilder();
            sb.Append($"[Nainital] phone report t+{Time.time - t0:F0}s: money {GameState.Money}" + (money0 >= 0 ? $" (was {money0})" : ""));
            var w = WantedSystem.Instance;
            sb.Append($", stars {(w ? w.Stars : 0)}, health {(PlayerController.Instance ? PlayerController.Instance.Health.Fraction : 0f):F2}");
            var pm = PoliceManager.Instance;
            if (pm)
            {
                foreach (var c in pm.Cars) if (c) sb.Append($"\n  police {c.Current} {(c.transform.position - PP).magnitude:F0} m sceneDone {c.sceneDone}");
                foreach (var a in pm.Ambulances) if (a) sb.Append($"\n  ambulance {a.Phase} {(a.transform.position - PP).magnitude:F0} m");
            }
            foreach (var f in FireTruck.All) if (f) sb.Append($"\n  fire tender {f.State} {(f.transform.position - PP).magnitude:F0} m");
            foreach (var f in FireSpot.All) if (f) sb.Append($"\n  fire spot burning {f.Burning} {(f.transform.position - PP).magnitude:F0} m");
            var photos = PhoneCamera.Photos();
            sb.Append($"\n  photos {photos.Count}" + (photos.Count > 0 ? " newest " + System.IO.Path.GetFileName(photos[0]) : ""));
            Debug.Log(sb.ToString());
        }
    }
}
