using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The owner's own test (22 Nov 2026: "heavy traffic lgaake.. bus stand se mallroad hote huye, eco cave park, then
    /// lovers points hote huye poora khurpatal tk pahuchaao... bus ko.. full traffic"): a traffic bus driven by the
    /// traffic's own AI through those places in that order, the town kept full round it. Every stop, crawl and hit is
    /// logged and photographed. It passes only when it arrives; being put on its lane (a hop) is counted against it.
    /// Bridge (Play): call TrafficBusTest.Run -> Bridge/traffic_bus.txt, Shots/play_bus_*.png. call TrafficBusTest.Stop.
    /// </summary>
    public static class TrafficBusTest
    {
        sealed class Place
        {
            public string name, road;   // road: only that one (Upper, not Lower Mall)
            public Vector2 at;
            public int r, i;
            public Vector3 p;
            public bool passed;
        }

        // the POIs of Data/world.json; the Mall: a point half way up Upper Mall
        static Place[] MakePlaces() => new[]
        {
            new Place { name = "New Bus Station", at = new Vector2(874.58f, -1183.56f) },
            new Place { name = "Mall Road", at = new Vector2(703.7f, -458f), road = "Upper Mall Road" },
            new Place { name = "Eco Cave Park", at = new Vector2(-937.09f, 182.48f) },
            new Place { name = "Lovers Point", at = new Vector2(-1423.75f, -377.59f) },
            new Place { name = "Khurpatal", at = new Vector2(-2618f, -2168f) },   // all the way: the end of its road down by the lake (#71), not the highway's view point
        };

        const int FullCars = 45, FullBikes = 20;   // on the road round the bus, in the daytime (the game's own: about 10 + 5)
        const float TimeLimit = 2700f, NoProgress = 120f;

        static Place[] places;
        static TrafficCar bus;
        static TrafficManager manager;
        static MethodInfo spawnCar, spawnBike;
        static FieldInfo carList, bikeList;
        static float t0, nextTick, nextFill, nextShot, nextFeed, lastLog;
        static float driven, stillFor, slowFor, slowTotal, stillTotal, progressT;
        static Vector3 lastPos, progressAt;
        static int stops, slows, hitsSeen, hopsSeen, offRoute, samples, nearSum, nearMax, liveSum, liveMax, shots;
        static List<(int r, int d)> steps;

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string LogPath => Path.Combine(Root, "Bridge/traffic_bus.txt");

        static void Out(string s)
        {
            Debug.Log("[Nainital] bus " + s);
            File.AppendAllText(LogPath, s + "\n");
        }

        static void Shot(string name)
        {
            if (shots >= 70) return;
            shots++;
            ScreenCapture.CaptureScreenshot(Path.Combine(Root, $"Shots/play_bus_{shots:00}_{name}.png"));
        }

        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new System.Exception("enter Play first (PlayTest.Begin)");
            Stop();
            manager = Object.FindAnyObjectByType<TrafficManager>();
            var net = manager.network;
            File.WriteAllText(LogPath, "");
            places = MakePlaces();

            foreach (var pl in places)
            {
                float best = float.MaxValue;
                for (int r = 0; r < net.roads.Length; r++)
                {
                    var road = net.roads[r];
                    if (pl.road != null ? road.name != pl.road : road.width < 4.5f) continue;   // a road a bus fits on
                    for (int i = 1; i < road.pts.Length - 1; i++)
                    {
                        float d = new Vector2(road.pts[i].x - pl.at.x, road.pts[i].z - pl.at.y).sqrMagnitude;
                        if (d < best) { best = d; pl.r = r; pl.i = i; pl.p = road.pts[i]; }
                    }
                }
                Out($"place {pl.name}: road #{pl.r} '{net.roads[pl.r].name}' w{net.roads[pl.r].width} pt {pl.i}, {Mathf.Sqrt(best):F0} m from it at ({pl.p.x:F0},{pl.p.z:F0})");
            }

            steps = new List<(int r, int d)>();
            int way = 0;
            for (int k = 0; k + 1 < places.Length; k++)
            {
                var a = places[k];
                var b = places[k + 1];
                var leg = TrafficLab.Plan(net, a.r, a.i, b.r, b.i, way);   // never turning round at a place on the way
                if (leg == null) { Out($"bus test done: NO ROUTE {a.name} -> {b.name}"); return; }
                for (int s = 0; s < leg.Count; s++) if (s > 0 || steps.Count == 0) steps.Add(leg[s]);
                way = leg[leg.Count - 1].d;
            }
            var sb = new StringBuilder($"route, {steps.Count} roads:");
            foreach (var s in steps) sb.Append($" [#{s.r} {net.roads[s.r].name} {(s.d > 0 ? "+" : "-")} w{net.roads[s.r].width}]");
            Out(sb.ToString());

            var lib = VehicleLibrary.Instance;
            var go = VehicleFactory.Create(CarType.Bus, Px.CarWhite, manager.material, true, "BusTest Bus", lib ? lib.Pick(CarType.Bus, 0.3f) : -1);
            go.transform.SetParent(manager.transform, true);
            bus = go.AddComponent<TrafficCar>();
            bus.type = CarType.Bus;
            bus.cruise = 10f;
            bus.Begin(net, places[0].r, places[0].i, steps[0].d);
            bus.FollowRoute(steps, places[places.Length - 1].p);
            places[0].passed = true;
            var info = go.GetComponent<VehicleInfo>();
            Out($"the bus: {go.name}, {(info ? info.length : 0f):F1} x {(info ? info.width : 0f):F2} m");

            // the player rides in it as a passenger (owner, 22 Nov 2026: "bus me as passenger bethne ka bhi rkho...phir bus
            // stand se khurpatal tk ka simulation"): the town's traffic and people come up round him all the way
            var pc = PlayerController.Instance;
            if (Jetpack.Flying) Jetpack.Toggle(pc);
            var seatIn = go.AddComponent<BusPassenger>();
            Physics.SyncTransforms();
            bool aboard = seatIn.SeatNow(pc);
            Out($"the player {(aboard ? "sits in it as a passenger" : "could NOT be seated (no free seat / no setup) - flying over it")}");
            if (!aboard && !Jetpack.Flying) Jetpack.Toggle(pc);
            if (ThirdPersonCamera.Instance) ThirdPersonCamera.Instance.SetOverride(go.transform, 17f);

            // full: the manager's own spawner and rules, only a higher number and asked more often
            manager.maxCars = Mathf.CeilToInt(FullCars / Mathf.Max(0.05f, TownClock.Traffic(TownClock.Hour)));
            manager.maxBikes = Mathf.CeilToInt(FullBikes / Mathf.Max(0.05f, TownClock.Traffic(TownClock.Hour)));
            manager.spawnMin = 45f;
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            spawnCar = typeof(TrafficManager).GetMethod("TrySpawn", Private);
            spawnBike = typeof(TrafficManager).GetMethod("TrySpawnBike", Private);
            carList = typeof(TrafficManager).GetField("active", Private);
            bikeList = typeof(TrafficManager).GetField("bikes", Private);

            t0 = Time.time;
            nextTick = t0 + 1f;
            nextFill = t0;
            nextShot = t0 + 8f;
            nextFeed = t0;
            lastLog = -99f;
            driven = stillFor = slowFor = slowTotal = stillTotal = progressT = 0f;
            stops = slows = hitsSeen = hopsSeen = offRoute = samples = nearSum = nearMax = liveSum = liveMax = shots = 0;
            lastPos = progressAt = go.transform.position;
            Out("bus test started");
            EditorApplication.update += Tick;
        }

        public static void Stop()
        {
            EditorApplication.update -= Tick;
            if (bus) { BusPassenger.Evict(bus.gameObject); Object.Destroy(bus.gameObject); }   // the player off it first
            bus = null;
        }

        static int Live<T>(FieldInfo f) where T : Object
        {
            int n = 0;
            foreach (var x in (List<T>)f.GetValue(manager)) if (x) n++;
            return n;
        }

        static void Fill()
        {
            var cam = Camera.main;
            var player = PlayerController.Instance;
            if (!cam || !player) return;
            var args = new object[] { player.transform.position, cam };
            int cars = Live<TrafficCar>(carList), bikes = Live<TrafficBike>(bikeList);
            for (int k = 0; k < 14 && cars < FullCars; k++)
            {
                spawnCar.Invoke(manager, args);
                cars = Live<TrafficCar>(carList);
                Physics.SyncTransforms();   // the next one sees the one just made
            }
            for (int k = 0; k < 8 && bikes < FullBikes; k++)
            {
                spawnBike.Invoke(manager, args);
                bikes = Live<TrafficBike>(bikeList);
                Physics.SyncTransforms();
            }
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            if (!bus) { EditorApplication.update -= Tick; Out("bus test done: FAIL, the bus is gone (left / destroyed)"); return; }
            float now = Time.time, el = now - t0;
            if (now >= nextFill) { nextFill = now + 0.5f; Fill(); }
            if (now < nextTick) return;
            nextTick = now + 1f;

            var t = bus.transform;
            Vector3 pos = t.position, step = pos - lastPos;
            step.y = 0f;
            driven += step.magnitude;
            lastPos = pos;
            if (now >= nextFeed) { nextFeed = now + 60f; PlayerStats.DebugHunger(1f); PlayerStats.DebugEnergy(1f); }
            // the player hangs over it, so the town's traffic and people are round the bus
            var pc = PlayerController.Instance;
            if (pc && Jetpack.Flying) pc.GetComponent<CharacterMotor>().Teleport(pos + Vector3.up * 22f - t.forward * 8f);

            int near = 0, live = 0;
            foreach (var c in (List<TrafficCar>)carList.GetValue(manager)) if (c) { live++; if ((c.transform.position - pos).sqrMagnitude < 120f * 120f) near++; }
            foreach (var b in (List<TrafficBike>)bikeList.GetValue(manager)) if (b) { live++; if ((b.transform.position - pos).sqrMagnitude < 120f * 120f) near++; }
            samples++;
            nearSum += near; nearMax = Mathf.Max(nearMax, near);
            liveSum += live; liveMax = Mathf.Max(liveMax, live);

            float sp = bus.Speed;
            string here = $"{el:F0}s ({pos.x:F1},{pos.y:F1},{pos.z:F1}) {sp:F1} m/s, {driven:F0} m, vehicles round it {near}";

            for (int k = 1; k < places.Length; k++)
            {
                var pl = places[k];
                if (pl.passed || !places[k - 1].passed) continue;
                bool last = k == places.Length - 1;
                if (last ? bus.Arrived : new Vector2(pos.x - pl.p.x, pos.z - pl.p.z).sqrMagnitude < 18f * 18f)
                {
                    pl.passed = true;
                    Out($"  REACHED {pl.name} at {here}; stops so far {stops}, hits {bus.HitsHere}");
                    Shot("reached_" + pl.name.Replace(' ', '_'));
                }
            }

            if (bus.HitsHere > hitsSeen) { hitsSeen = bus.HitsHere; Out($"  HIT {hitsSeen} {here}: {bus.LastHit}"); Shot("hit" + hitsSeen); }
            if (bus.HopsHere > hopsSeen) { hopsSeen = bus.HopsHere; Out($"  HOP {hopsSeen} (put on its lane) {here}"); Shot("hop" + hopsSeen); }
            if (!steps.Contains((bus.Road, bus.Dir)))
            {
                offRoute++;
                if (offRoute % 10 == 1) Out($"  OFF ROUTE {here}: on road #{bus.Road} '{manager.network.roads[bus.Road].name}' dir {bus.Dir}");
            }

            stillFor = sp < 0.5f && !bus.Arrived ? stillFor + 1f : 0f;
            if (stillFor > 0f) stillTotal += 1f;
            slowFor = sp < 2f && !bus.Arrived ? slowFor + 1f : 0f;
            if (slowFor > 0f) slowTotal += 1f;
            if (stillFor == 4f || (stillFor > 0f && stillFor % 30f == 0f))
            {
                if (stillFor == 4f) stops++;
                Out($"  STOP {stops}, {stillFor:F0} s, {here} | {bus.RouteInfo} | {bus.State} | in front: {TrafficLab.InFront(bus)} | last hit {bus.LastHit}");
                Shot("stop" + stops);
            }
            else if (slowFor == 12f && stillFor == 0f)
            {
                slows++;
                Out($"  SLOW {slows} {here} | {bus.RouteInfo} | {bus.State} | behind someone on foot {bus.BehindWalker:F0} s | in front: {TrafficLab.InFront(bus)}");
                Shot("slow" + slows);
            }
            if (now >= nextShot) { nextShot = now + 60f; Shot($"t{el:0000}"); }
            if (el - lastLog >= 15f) { lastLog = el; Out($"  . {here}, live {live} | road #{bus.Road} idx {bus.Idx}"); }

            if ((pos - progressAt).sqrMagnitude > 8f * 8f) { progressAt = pos; progressT = el; }
            string end = bus.Arrived ? "ARRIVED"
                       : el - progressT > NoProgress ? $"FAIL, no progress for {NoProgress:F0} s at ({pos.x:F1},{pos.z:F1})"
                       : el > TimeLimit ? "FAIL, out of time" : null;
            if (end == null) return;

            EditorApplication.update -= Tick;
            Shot("end");
            var missed = new StringBuilder();
            foreach (var pl in places) if (!pl.passed) missed.Append(' ').Append(pl.name);
            Out($"  traffic round it (120 m): mean {nearSum / Mathf.Max(1, samples)}, most {nearMax}; on the road in all: mean {liveSum / Mathf.Max(1, samples)}, most {liveMax}");
            Out($"  standing {stillTotal:F0} s, under 2 m/s {slowTotal:F0} s, stops of 4 s+ {stops}, slow stretches {slows}, hits {bus.HitsHere}, most tries {bus.MostTries}, hops {bus.HopsHere}, off route {offRoute} s, passes {bus.PassesHere}");
            Out($"bus test done: {end} after {el:F0} s, {driven:F0} m; places not reached:{(missed.Length == 0 ? " none" : missed.ToString())}");
        }
    }
}
