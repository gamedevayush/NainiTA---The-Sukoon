using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The bus ride from the menu (owner, 23 Nov 2026: "ek wahan bhi option dedo, menu me...sab (not old or kid) ldkiya in
    /// bus..and last row khaali...mujhe second last rkhna"): a bus from B.D. Pandey Hospital (Mallital) by Eco Cave Park and
    /// Lovers Point to Khurpatal, in rain and a heavy fog (WeatherSystem.Kind.RainFog). The player sits in the second-last row on the
    /// left (by the window), every other seat but the last row young women (no girls, no old women), a man driving and a man
    /// on his feet at the door as the conductor; the town's traffic and its lights round it. Each stop is called out; at
    /// Khurpatal it stands and he gets off with E (or jumps off on the way - a ragdoll, PlayerController.BailOut).
    /// HUD tray: "Bus Sawari" (BusTour.Begin).
    /// </summary>
    public class BusTour : MonoBehaviour
    {
        // from B.D. Pandey District Hospital, Mallital (owner, 23 Nov 2026: "bus ab start hogi BD pandey se"; world.json's
        // POI); Mall Road left out - from Mallital it would be 3 km down and back up the lake
        static readonly (string name, Vector2 at, string road)[] Stops =
        {
            ("BD Pandey Hospital", new Vector2(-150.18f, 230.19f), null),
            ("Eco Cave Park", new Vector2(-937.09f, 182.48f), null),
            ("Lovers Point", new Vector2(-1423.75f, -377.59f), null),
            ("Khurpatal", new Vector2(-2618f, -2168f), null),
        };

        public static BusTour Current { get; private set; }

        TrafficCar bus;
        Vector3[] stopAt;
        int next = 1;
        bool arrivedSaid;
        float awayT;

        /// <summary>Start the ride (the tray button). False, with a word on screen, when it cannot start now.</summary>
        public static bool Begin()
        {
            var p = PlayerController.Instance;
            var tm = Object.FindAnyObjectByType<TrafficManager>();
            if (!p || !tm || !tm.network || tm.network.roads == null) { GameEvents.ShowToast("Bus abhi nahi mil rahi", 2f); return false; }
            if (p.CurrentMode != PlayerController.Mode.Normal || p.Boarding || p.Ragdolled || p.InWater)
            {
                GameEvents.ShowToast("Pehle gaadi / sawari se utro, phir Bus Sawari", 2.2f);
                return false;
            }
            if (Current) Current.End();
            var net = tm.network;
            int n = Stops.Length;
            var r = new int[n];
            var idx = new int[n];
            var pts = new Vector3[n];
            for (int k = 0; k < n; k++)
            {
                float best = float.MaxValue;
                for (int ri = 0; ri < net.roads.Length; ri++)
                {
                    var road = net.roads[ri];
                    if (Stops[k].road != null ? road.name != Stops[k].road : road.width < 4.5f) continue;   // a road a bus fits on
                    for (int i = 1; i < road.pts.Length - 1; i++)
                    {
                        float d = new Vector2(road.pts[i].x - Stops[k].at.x, road.pts[i].z - Stops[k].at.y).sqrMagnitude;
                        if (d < best) { best = d; r[k] = ri; idx[k] = i; pts[k] = road.pts[i]; }
                    }
                }
            }
            var steps = new List<(int r, int d)>();
            int way = 0;
            for (int k = 0; k + 1 < n; k++)
            {
                var leg = RoadPlanner.Plan(net, r[k], idx[k], r[k + 1], idx[k + 1], way);   // never turning round at a stop
                if (leg == null) { GameEvents.ShowToast("Bus ka rasta nahi mila (" + Stops[k + 1].name + ")", 2.5f); return false; }
                for (int s = 0; s < leg.Count; s++) if (s > 0 || steps.Count == 0) steps.Add(leg[s]);
                way = leg[leg.Count - 1].d;
            }
            var lib = VehicleLibrary.Instance;
            var go = VehicleFactory.Create(CarType.Bus, Px.CarWhite, tm.material, true, "Bus Sawari", lib ? lib.Pick(CarType.Bus, 0.3f) : -1);
            go.transform.SetParent(tm.transform, true);
            var t = go.AddComponent<TrafficCar>();
            t.type = CarType.Bus;
            t.cruise = 10f;
            t.Begin(net, r[0], idx[0], steps[0].d);
            t.FollowRoute(steps, pts[n - 1]);
            var seat = go.AddComponent<BusPassenger>();
            Physics.SyncTransforms();
            bool aboard = false;
            t.FillForRide(() => aboard = seat.SeatNow(p, 1), true, true);   // him first: the second-last row, left
            if (!aboard) aboard = seat.SeatNow(p, 1);
            if (!aboard) { Destroy(go); GameEvents.ShowToast("Bus mein jagah nahi mili", 2f); return false; }
            var tour = go.AddComponent<BusTour>();
            tour.bus = t;
            tour.stopAt = pts;
            Current = tour;
            // the ride in rain and a heavy fog (owner, 23 Nov 2026: "isko bus journey me connect krdiya?"); the weather goes
            // back to changing by itself when the ride ends
            var ws = WeatherSystem.Instance;
            if (ws) { ws.autoCycle = false; ws.Set(WeatherSystem.Kind.RainFog); }
            GameEvents.ShowToast("Bus Sawari: BD Pandey Hospital → Eco Cave Park → Lovers Point → Khurpatal. V: camera, E: utro", 4.5f);
            Debug.Log($"[Nainital] bus tour: {steps.Count} roads, from {pts[0]:F0} to {pts[n - 1]:F0}");
            return true;
        }

        void Update()
        {
            if (!bus) { End(); return; }
            Vector3 pos = bus.transform.position;
            if (next < stopAt.Length && next < Stops.Length - 1)
            {
                Vector3 d = stopAt[next] - pos;
                d.y = 0f;
                if (d.magnitude < 25f) { GameEvents.ShowToast("Stop: " + Stops[next].name, 2.5f); next++; }
            }
            if (bus.Arrived && !arrivedSaid)
            {
                arrivedSaid = true;
                GameEvents.ShowToast("Khurpatal aa gaya! E se utro", 4f);
            }
            // he got off and went his way: the bus goes too (not in front of him)
            var p = PlayerController.Instance;
            bool aboard = BusPassenger.Carries(gameObject);
            float far = p ? (p.transform.position - pos).magnitude : 999f;
            awayT = !aboard && far > 120f ? awayT + Time.deltaTime : 0f;
            if (awayT > 5f) End();
        }

        public void End()
        {
            BusPassenger.Evict(gameObject);
            if (Current == this) Current = null;
            if (WeatherSystem.Instance) WeatherSystem.Instance.autoCycle = true;   // the ride's rain and fog clear in their time
            Destroy(gameObject);
        }
    }
}
