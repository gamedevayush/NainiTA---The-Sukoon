using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Tallital, from the user's photos: the bus station in the fork of the Bhowali and Haldwani roads (long flat
    /// concrete canopy, green and white board, buses in front), the grey stone school with a spired tower across the
    /// highway, Gandhi ji's statue on a pedestal at the lake edge and the white "I ♥ NAINITAL" letters by the water.</summary>
    public static partial class WorldBuilder
    {
        sealed class TallitalPlan
        {
            public bool ok;
            public Vector2 corner, station, apron, school, schoolFront, statue, statueFace, letters, lettersAlong, lettersFace;
            public Vector2 A, N;   // A: along the fork, away from the lake; N: across it, towards the Bhowali road
            public List<Vector2> shore;
            public int c0, dir, kEnd;
        }

        static WorldCtx tallitalFor;
        static TallitalPlan tallital;

        static TallitalPlan Tallital(WorldCtx ctx)
        {
            if (tallitalFor == ctx) return tallital;
            tallitalFor = ctx;
            var t = tallital = new TallitalPlan();
            // The roadways station is ~50 m SW of the lake-corner junction, beside the road along the south shore,
            // with the spired stone school just behind it (user's Street View check). Both are POIs from process_data.py.
            var bus = ctx.Poi("Tallital Roadways Bus Station") ?? ctx.Poi("New Bus Station");
            if (!bus.HasValue) return t;
            Vector2 B = bus.Value;
            t.corner = ctx.tallitalShore;
            // it faces the junction plaza where the buses stand; a 10.5 m yard in front of a 9 m deep canopy
            t.N = (t.corner - B).normalized;
            t.A = new Vector2(t.N.y, -t.N.x);
            if (NearestRoadPoint(ctx, B, 30f, r => r.name == "Nainital - Haldwani Highway", out var hw, out _))
                B -= t.A * (Vector2.Dot(hw - B, t.A) > 0f ? 3f : -3f);   // keep the canopy off the highway beside it
            t.station = B;
            t.apron = B + t.N * 9.75f;
            var school = ctx.Poi("Bishop Shaw School");
            if (school.HasValue)
            {
                t.school = school.Value;
                t.schoolFront = (t.corner - t.school).normalized;   // its front looks towards the junction
            }
            else
            {
                t.school = t.station - t.N * 24f;
                t.schoolFront = t.N;
            }

            // along the south shore, west of the corner: the statue, then the letters
            var shore = Resample(ctx.lake, 1f, true);
            int c = NearestIndex(shore, t.corner), n = shore.Count;
            int dir = shore[(c + 10) % n].x < shore[(c - 10 + n) % n].x ? 1 : -1;
            Vector2 Along(int k) => shore[((c + dir * k) % n + n) % n];
            bool Clear(Vector2 p) => !NearestRoadPoint(ctx, p, 6f, r => !IsMinor(r.kind), out _, out _);
            int ks = 3;   // by the corner, at the east end of the paved terrace (user's photo and Street View)
            Vector2 Stat(int k) => Along(k) + OutwardAt(ctx, Along(k)) * 3.2f;
            while (ks < 60 && !Clear(Stat(ks))) ks += 2;
            t.statue = Stat(ks);
            t.statueFace = OutwardAt(ctx, Along(ks));
            int kl = ks + 33;   // west end of the terrace, out of the Street View north view
            Vector2 Let(int k) => Along(k) + OutwardAt(ctx, Along(k)) * 1.9f;
            while (kl < 110 && !Clear(Let(kl))) kl += 2;
            t.letters = Let(kl);
            t.lettersFace = OutwardAt(ctx, Along(kl));
            t.lettersAlong = (Along(kl + 6) - Along(kl - 6)).normalized;
            t.shore = shore;
            t.c0 = c;
            t.dir = dir;
            t.kEnd = kl + 14;
            t.ok = true;
            return t;
        }

        /// <summary>Ground pads (centre, radius) levelled before the roads are laid.</summary>
        static IEnumerable<(Vector2, float)> TallitalPads(WorldCtx ctx)
        {
            var t = Tallital(ctx);
            if (!t.ok) yield break;
            yield return (t.station + t.N * 2f, 17f);
            yield return (t.school, 13f);
        }

        static void PlanTallital(WorldCtx ctx)
        {
            var t = Tallital(ctx);
            if (!t.ok) return;
            ctx.keepClear.Add((t.station, 15f));
            ctx.keepClear.Add((t.apron + t.A * 12f, 9f));
            ctx.keepClear.Add((t.apron - t.A * 12f, 9f));
            Vector2 side = new Vector2(t.schoolFront.y, -t.schoolFront.x);   // the hall, the tower and the side wing
            ctx.keepClear.Add((t.school, 16f));
            ctx.keepClear.Add((t.school + side * 10f, 9f));
            ctx.keepClear.Add((t.school - side * 16f, 10f));
            ctx.keepClear.Add((t.statue, 4f));
            ctx.keepClear.Add((t.letters, 7f));
            float y = ctx.H(t.apron);
            ctx.busStand = new Vector3(t.apron.x, y, t.apron.y);
            ctx.busStandRot = Quaternion.LookRotation(Flat(-t.N));   // buses park nose-in towards the canopy
            ctx.hasBusStand = true;
            ctx.landmarkSites["Tallital Bus Stand"] = new Vector3(t.station.x, ctx.H(t.station), t.station.y);
            ctx.Zone("Tallital Bus Stand", new Vector3(t.station.x, y, t.station.y), 45f);
        }

        // ------------------------------------------------------------------ build
        static void BuildTallital(WorldCtx ctx)
        {
            var t = Tallital(ctx);
            if (!t.ok) { Debug.LogWarning("[Nainital] Tallital: no 'New Bus Station' POI"); return; }
            var parent = new GameObject("Tallital").transform;
            parent.SetParent(ctx.worldRoot, false);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var station = new MeshBuilder();
            var stationCol = new MeshBuilder();
            BusStation(ctx, parent, station, stationCol, font, t);
            var go = ctx.MeshObject("Tallital Bus Stand", parent, station, ctx.mats.atlas, stationCol);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            TownGround(ctx, parent, t);
            Apron(ctx, parent, t);
            ChowkPaving(ctx, parent, t);
            TerraceEdge(ctx, parent, t);
            TallitalWires(ctx, parent, t);
            TrafficPost(ctx, parent);

            var school = new MeshBuilder();
            var schoolCol = new MeshBuilder();
            StoneSchool(ctx, parent, school, schoolCol, font, t);
            ctx.MeshObject("Bishop Shaw School", parent, school, ctx.mats.atlas, schoolCol);

            var chowk = new MeshBuilder();
            var chowkCol = new MeshBuilder();
            GandhiStatue(ctx, chowk, chowkCol, t);
            NainitalLetters(ctx, chowk, chowkCol, t);
            ctx.MeshObject("Tallital chowk", parent, chowk, ctx.mats.atlas, chowkCol);

            ctx.InfoSign("Tallital Bus Station", "Uttarakhand Parivahan ka bus adda - Haldwani, Kathgodam, Delhi ki buses yahin se. Saamne jheel ke kinare Gandhi ji ki murti.",
                         V(t.apron + t.A * 14f + t.N * 5.5f, ctx.H(t.apron + t.A * 14f + t.N * 5.5f)), Quaternion.LookRotation(Flat(t.N)), parent);

            void Marker(string name, Vector2 at, float eye, Vector3 lookAt)
            {
                var m = new GameObject(name);
                m.transform.SetParent(parent, false);
                Vector3 p = V(at, ctx.H(at) + eye);
                m.transform.SetPositionAndRotation(p, Quaternion.LookRotation(lookAt - p));
            }
            // the user's Street View spot (29.38036, 79.46364), looking 212.6 deg: for a side-by-side check
            var sv = new Vector2(838.1f, -1123.7f);
            Marker("TallitalShotStreetView", sv, 2.4f, V(sv + new Vector2(Mathf.Sin(212.6f * Mathf.Deg2Rad), Mathf.Cos(212.6f * Mathf.Deg2Rad)) * 60f, ctx.H(sv) + 2.6f));
            var sv2 = new Vector2(813.7f, -1136.3f);   // the user's second pano (29.38025, 79.46339)
            foreach (var (nm, hd) in new[] { ("TallitalShotSV2SE", 147.6f), ("TallitalShotSV2N", 351.9f) })
                Marker(nm, sv2, 2.4f, V(sv2 + new Vector2(Mathf.Sin(hd * Mathf.Deg2Rad), Mathf.Cos(hd * Mathf.Deg2Rad)) * 60f, ctx.H(sv2) + (hd < 200f ? 6f : 2.4f)));
            Marker("TallitalShotStation", t.station - t.A * 30f + t.N * 9f, 1.7f, V(t.station + t.A * 3f, ctx.H(t.station) + 2.5f));
            Vector2 view = t.statue + t.statueFace * 17f + (t.letters - t.statue).normalized * 6f;
            Marker("TallitalShotChowk", view, 1.7f, V((t.statue + t.letters) * 0.5f, ctx.H(t.statue) + 1.2f));
            Marker("TallitalShotSchool", t.school + t.schoolFront * 21f + new Vector2(t.schoolFront.y, -t.schoolFront.x) * 8f, 1.7f, V(t.school, ctx.H(t.school) + 6f));
            Debug.Log($"[Nainital] Tallital: station at {t.station}, school at {t.school}, statue at {t.statue}");
        }

        // ------------------------------------------------------------------ the bus station
        static void BusStation(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font, TallitalPlan t)
        {
            float y = ctx.H(t.station);
            var rot = Quaternion.LookRotation(Flat(t.N));   // front faces the buses and the Bhowali road
            Vector3 c = V(t.station, y), along = Flat(t.A), front = Flat(t.N);
            Vector2 conc = Atlas.MaterialUV(Atlas.MatConcrete), green = Atlas.SolidUV(Pal.BoatGreen), white = Atlas.SolidUV(Pal.ShirtWhite);
            Vector2 orange = Atlas.SolidUV(Pal.Saffron), dark = Atlas.SolidUV(Pal.IronBlack), stain = Atlas.SolidUV(Pal.StoneGrey);
            const float L = 24f, D = 9f, H = 4.3f;

            mb.Box(c + Vector3.up * 0.1f, new Vector3(L, 0.4f, D), rot, conc);                 // floor slab, a step above the apron
            for (int i = 0; i <= 6; i++)
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 p = c + along * (-L * 0.5f + 1f + i * (L - 2f) / 6f) + front * (s * (D * 0.5f - 0.6f));
                    mb.Box(p + Vector3.up * (H * 0.5f), new Vector3(0.42f, H, 0.42f), rot, conc);
                    col.Box(p + Vector3.up * (H * 0.5f), new Vector3(0.42f, H, 0.42f), rot, Vector2.zero);
                }
            Vector3 roof = c + Vector3.up * (H + 0.2f);
            mb.Box(roof, new Vector3(L + 1.2f, 0.4f, D + 1.6f), rot, conc);                    // flat slab roof
            mb.Box(roof + Vector3.down * 0.22f + front * (D * 0.5f + 0.78f), new Vector3(L + 1.2f, 0.06f, 0.05f), rot, stain);
            // the long sign band on the front fascia: green with a white border
            Vector3 band = roof + Vector3.up * 0.75f + front * (D * 0.5f + 0.82f);
            mb.Box(band - front * 0.04f, new Vector3(L * 0.72f + 0.2f, 1.3f, 0.06f), rot, white);
            mb.Box(band + along * (-L * 0.14f), new Vector3(L * 0.72f, 1.15f, 0.08f), rot, green);
            mb.Box(band + along * (L * 0.36f + 0.5f), new Vector3(L * 0.2f, 1.15f, 0.08f), rot, orange);   // bank / ATM board at the end
            var holder = new GameObject("Bus station boards").transform;
            holder.SetParent(parent, false);
            Label(holder, font, "UTTARAKHAND PARIVAHAN NIGAM", band + along * (-L * 0.14f) + Vector3.up * 0.25f + front * 0.06f, front, 0.42f, Color.white);
            Label(holder, font, "BUS STATION  NAINITAL", band + along * (-L * 0.14f) + Vector3.down * 0.27f + front * 0.06f, front, 0.36f, Color.white);
            Label(holder, font, "ATM", band + along * (L * 0.36f + 0.5f) + front * 0.06f, front, 0.6f, Color.white);

            // ticket office and enquiry at the back, a wall behind the rest
            Vector3 office = c + along * (-L * 0.32f) - front * (D * 0.5f - 1.8f);
            BuildingKit.FacadeWall(mb, new Vector2(office.x, office.z) - t.A * 5f + t.N * 1.6f, new Vector2(office.x, office.z) + t.A * 5f + t.N * 1.6f,
                                   y + 0.3f, y + 3.6f, y + 0.3f, front, new BuildingKit.Style { wall = 1, roof = 0, shop = true, litChance = 0.6f }, ctx.rng, true);
            mb.Box(office + Vector3.up * 1.95f, new Vector3(9.9f, 3.3f, 3f), rot, Atlas.WallSolid(1));
            col.Box(office + Vector3.up * 1.95f, new Vector3(10f, 3.3f, 3.2f), rot, Vector2.zero);
            Label(holder, font, "TICKET / ENQUIRY", office + Vector3.up * 3.3f + front * 1.7f, front, 0.3f, new Color(0.15f, 0.2f, 0.15f));
            Vector3 wall = c - front * (D * 0.5f - 0.15f) + along * (L * 0.18f);
            mb.Box(wall + Vector3.up * 1.4f, new Vector3(L * 0.6f, 2.8f, 0.25f), rot, Atlas.WallSolid(0));
            col.Box(wall + Vector3.up * 1.4f, new Vector3(L * 0.6f, 2.8f, 0.25f), rot, Vector2.zero);

            // benches under the canopy, and a timetable board
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = c + along * (-2f + i * 4.2f) - front * 2.2f + Vector3.up * 0.3f;
                using (PropKit.At(mb, p, rot)) PropKit.Bench(mb);
            }
            Vector3 tt = c + along * (L * 0.42f) - front * 1f;
            mb.Box(tt + Vector3.up * 1.6f, new Vector3(1.8f, 1.2f, 0.08f), rot, white);
            mb.Box(tt + Vector3.up * 0.6f, new Vector3(0.08f, 1.2f, 0.08f), rot, dark);
        }

        /// <summary>The chowk by the water is paved from the lake railing to the road, from the corner past the letters.</summary>
        static void ChowkPaving(WorldCtx ctx, Transform parent, TallitalPlan t)
        {
            int n = t.shore.Count;
            Vector2 Along(int k) => t.shore[((t.c0 + t.dir * k) % n + n) % n];
            var mb = new MeshBuilder();
            var line = new List<Vector2>();
            Vector2 prevIn = default, prevOut = default;
            float prevYi = 0f, prevYo = 0f, v = 0f;
            for (int k = -6; k <= t.kEnd; k++)
            {
                Vector2 p = Along(k), o = OutwardAt(ctx, p);
                float w = 9f;
                if (NearestRoadPoint(ctx, p + o * 2f, 14f, r => !IsMinor(r.kind) && !IsMallDeck(r.name), out var rp, out _))
                    w = Mathf.Clamp(Vector2.Distance(p, rp) - 4.2f, 2.5f, 9f);
                Vector2 pin = p + o * 1.1f, pout = p + o * w;
                float yi = ctx.H(pin) + 0.07f, yo = ctx.H(pout) + 0.07f;
                if (k > -6)
                {
                    float seg = Vector2.Distance(Along(k - 1), p);
                    mb.Quad(V(prevIn, prevYi), V(pin, yi), V(pout, yo), V(prevOut, prevYo), new Rect(0f, v / 2.5f, seg / 2.5f, w / 2.5f), Vector3.up);
                    v += seg;
                }
                line.Add(p + o * (1.1f + w) * 0.5f);
                prevIn = pin; prevOut = pout; prevYi = yi; prevYo = yo;
            }
            // benches in rows facing the water, and twin-lantern lamps along the railing (Street View north view)
            var benches = new MeshBuilder();
            var benchCol = new MeshBuilder();
            var rnd = new System.Random(5150);
            for (int k = 6; k < t.kEnd - 2; k += 4)
            {
                Vector2 p = Along(k), o = OutwardAt(ctx, p);
                var rot = Quaternion.LookRotation(Flat(-o));
                Vector3 at = V(p + o * 3.4f, ctx.H(p + o * 3.4f) + 0.07f);
                using (PropKit.At(benches, at, rot)) PropKit.Bench(benches);
                benchCol.Box(at + Vector3.up * 0.4f, new Vector3(1.6f, 0.8f, 0.6f), rot, Vector2.zero);
                if (k % 12 == 6)
                {
                    Vector3 lamp = V(p + o * 1.6f, ctx.H(p + o * 1.6f));
                    Vector3 bulb;
                    using (PropKit.At(benches, lamp, Quaternion.LookRotation(Flat(new Vector2(o.y, -o.x))))) bulb = MallTwinLamp(benches, rnd);
                    ctx.lampHeads.Add(lamp + Quaternion.LookRotation(Flat(new Vector2(o.y, -o.x))) * bulb);
                }
            }
            ctx.MeshObject("Tallital terrace benches", parent, benches, ctx.mats.atlas, benchCol);
            var cobble = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(Gen + "/M_MallCobble.mat");
            ctx.MeshObject("Tallital chowk paving", parent, mb, cobble ? cobble : ctx.mats.paver, null, false);
            noGrassExtra.Add((line.ToArray(), 5f));
        }

        const float TownRadius = 118f;

        /// <summary>Tallital has no lawns: paved, worn concrete ground everywhere round the chowk (Street View).</summary>
        static void TownGround(WorldCtx ctx, Transform parent, TallitalPlan t) => PaveZone(ctx, parent, t.corner, TownRadius, "Tallital ground");

        static Material TownGroundMaterial(WorldCtx ctx)
        {
            var photo = PhotoMaterial("concrete_floor_worn_001", "M_TownGroundPhoto", new Color(0.82f, 0.81f, 0.78f), 0.4f);
            if (photo) return photo;
            var baseMat = ConcreteMaterial(ctx);
            string path = Gen + "/M_TownGround.mat";
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(baseMat); UnityEditor.AssetDatabase.CreateAsset(m, path); }
            m.CopyPropertiesFromMaterial(baseMat);
            m.SetColor("_BaseColor", new Color(0.6f, 0.59f, 0.57f));   // older, darker and dirtier than the roads
            UnityEditor.EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Paved ground over a town zone (no lawns in the bazaars), leaving the Flats, parks and pitches as they are.</summary>
        static void PaveZone(WorldCtx ctx, Transform parent, Vector2 c, float radius, string name)
        {
            var keepOut = new List<Vector2[]>();
            foreach (var a2 in ctx.world.areas)
                if (a2.kind == "stadium" || a2.kind == "park" || a2.kind == "playground" || a2.kind == "pitch") keepOut.Add(Geo.ToPoints(a2.pts));
            bool Out(Vector2 p) { foreach (var poly in keepOut) if (Geo.PointInPolygon(poly, p)) return true; return false; }
            var chunks = new MallChunks(ctx, parent, name, TownGroundMaterial(ctx), false);
            const float step = 2.5f;
            for (float x = -radius; x < radius; x += step)
                for (float z = -radius; z < radius; z += step)
                {
                    Vector2 p00 = c + new Vector2(x, z), p11 = p00 + new Vector2(step, step);
                    Vector2 mid = (p00 + p11) * 0.5f;
                    if ((mid - c).sqrMagnitude > radius * radius || ctx.InLake(mid) || ctx.LakeDistance(mid) < 1.2f || Out(mid)) continue;
                    Vector2 p10 = new Vector2(p11.x, p00.y), p01 = new Vector2(p00.x, p11.y);
                    var uv = new Rect(p00.x / 6f, p00.y / 6f, step / 6f, step / 6f);
                    chunks.At(mid)[0].Quad(V(p00, ctx.H(p00) + 0.035f), V(p10, ctx.H(p10) + 0.035f), V(p11, ctx.H(p11) + 0.035f), V(p01, ctx.H(p01) + 0.035f), uv, Vector3.up);
                    noGrassCells.Add(((long)Mathf.FloorToInt(mid.x / 4f) << 32) ^ (uint)Mathf.FloorToInt(mid.y / 4f));
                }
            chunks.Flush();
            for (float z = -radius; z <= radius; z += 4f)   // grass mask: strips across the zone, broken where it is kept out
            {
                float hx = Mathf.Sqrt(Mathf.Max(0f, radius * radius - z * z));
                Vector2? start = null, last = null;
                for (float x = -hx; x <= hx; x += 2f)
                {
                    Vector2 q = c + new Vector2(x, z);
                    bool paved = !ctx.InLake(q) && !Out(q);
                    if (paved) { if (start == null) start = q; last = q; }
                    if ((!paved || x + 2f > hx) && start != null)
                    {
                        if ((last.Value - start.Value).sqrMagnitude > 1f) noGrassExtra.Add((new[] { start.Value, last.Value }, 2.7f));
                        start = null;
                    }
                }
            }
        }

        /// <summary>The terrace edge: green iron railing at the water, and on the road side a row of parked motorbikes
        /// behind green-and-white bollards with chains (Street View north view).</summary>
        static void TerraceEdge(WorldCtx ctx, Transform parent, TallitalPlan t)
        {
            int n = t.shore.Count;
            Vector2 Along(int k) => t.shore[((t.c0 + t.dir * k) % n + n) % n];
            var rail = new MeshBuilder();
            var railCol = new MeshBuilder();
            var pts = new List<Vector3>();
            for (int k = -4; k <= t.kEnd; k += 2)
            {
                Vector2 p = Along(k), o = OutwardAt(ctx, p);
                Vector2 q = p + o * 1.0f;
                if (NearJetty(ctx, q, 5f)) { if (pts.Count > 1) PropKit.Railing(rail, railCol, pts); pts.Clear(); continue; }
                pts.Add(V(q, ctx.H(q) + 0.07f));
            }
            if (pts.Count > 1) PropKit.Railing(rail, railCol, pts);
            ctx.MeshObject("Tallital terrace railing", parent, rail, ctx.mats.atlas, railCol, false);

            var bikes = new MeshBuilder();
            var bollards = new MeshBuilder();
            var rnd = new System.Random(4545);
            int[] paints = { Pal.IronBlack, Pal.IronBlack, Pal.JacketRed, Pal.BoatBlue, Pal.StoneGrey, Pal.ShirtWhite };
            Vector2 green = Atlas.SolidUV(Pal.BoatGreen), white = Atlas.SolidUV(Pal.ShirtWhite), chain = Atlas.SolidUV(Pal.IronBlack);
            Vector3 prevTop = default;
            bool havePrev = false;
            int count = 0;
            for (float k = 4; k < t.kEnd - 2; k += 0.95f, count++)
            {
                int ki = Mathf.FloorToInt(k);
                Vector2 p = Along(ki), o = OutwardAt(ctx, p);
                float w = 9f;
                if (NearestRoadPoint(ctx, p + o * 2f, 14f, r => !IsMinor(r.kind) && !IsMallDeck(r.name), out var rp, out _))
                    w = Mathf.Clamp(Vector2.Distance(p, rp) - 4.2f, 2.5f, 9f);
                if (w < 5f) { havePrev = false; continue; }
                Vector2 b = p + o * (w - 1.3f);
                float y = ctx.H(b) + 0.07f;
                if (rnd.NextDouble() < 0.82)
                    using (PropKit.At(bikes, V(b, y), Quaternion.LookRotation(Flat(-o)) * Quaternion.Euler(0, (float)(rnd.NextDouble() - 0.5) * 16f, 0)))
                        Motorbike(bikes, paints[rnd.Next(paints.Length)]);
                if (count % 4 == 0)
                {
                    Vector2 bp = p + o * (w - 2.6f);
                    Vector3 bb = V(bp, ctx.H(bp) + 0.07f);
                    bollards.Cylinder(bb, 0.12f, 0.1f, 0.85f, 8, green, true);
                    bollards.Cylinder(bb + Vector3.up * 0.55f, 0.125f, 0.125f, 0.12f, 8, white, false);
                    Vector3 top = bb + Vector3.up * 0.75f;
                    if (havePrev) { Vector3 mid = (prevTop + top) * 0.5f + Vector3.down * 0.18f; bollards.Limb(prevTop, mid, 0.015f, 0.015f, 3, chain, false); bollards.Limb(mid, top, 0.015f, 0.015f, 3, chain, false); }
                    prevTop = top;
                    havePrev = true;
                }
            }
            ctx.MeshObject("Tallital parked bikes", parent, bikes, ctx.mats.atlas);
            ctx.MeshObject("Tallital bollards", parent, bollards, ctx.mats.atlas);
        }

        /// <summary>A parked motorbike / scooter, 1.9 m long, facing local +Z.</summary>
        static void Motorbike(MeshBuilder mb, int paint)
        {
            Vector2 tyre = Atlas.SolidUV(Pal.IronBlack), body = Atlas.SolidUV(paint), seat = Atlas.SolidUV(Pal.IronBlack), chrome = Atlas.SolidUV(Pal.StoneGrey);
            foreach (float z in new[] { -0.68f, 0.68f })
                using (PropKit.At(mb, new Vector3(0f, 0.3f, z), Quaternion.Euler(0, 0, 90f))) mb.Cylinder(new Vector3(0, -0.05f, 0), 0.3f, 0.3f, 0.1f, 10, tyre, true);
            mb.Box(new Vector3(0f, 0.55f, 0.05f), new Vector3(0.28f, 0.32f, 1.1f), Quaternion.identity, body);
            mb.Box(new Vector3(0f, 0.78f, -0.25f), new Vector3(0.26f, 0.1f, 0.7f), Quaternion.identity, seat);
            mb.Limb(new Vector3(0f, 0.6f, 0.6f), new Vector3(0f, 1.0f, 0.5f), 0.03f, 0.03f, 4, chrome, false);
            mb.Limb(new Vector3(-0.32f, 1.0f, 0.5f), new Vector3(0.32f, 1.0f, 0.5f), 0.02f, 0.02f, 4, chrome, false);
            mb.Box(new Vector3(0f, 0.85f, 0.62f), new Vector3(0.22f, 0.2f, 0.12f), Quaternion.identity, body);
        }

        /// <summary>Poles and the usual tangle of wires along the main roads at Tallital.</summary>
        static void TallitalWires(WorldCtx ctx, Transform parent, TallitalPlan t)
        {
            var poles = new MeshBuilder();
            var wires = new MeshBuilder();
            Vector2 wireUV = Atlas.SolidUV(Pal.IronBlack);
            var rnd = new System.Random(3131);
            foreach (var r in ctx.world.roads)
            {
                if (IsMinor(r.kind) || IsMallDeck(r.name) || r.kind == "residential" || r.kind == "tertiary" || r.kind == "secondary" || r.kind == "unclassified") continue;   // those already have poles
                var line = Resample(Geo.ToPoints(r.pts), 26f, false);
                Vector3[] prev = null;
                for (int i = 1; i < line.Count - 1; i++)
                {
                    Vector2 p = line[i];
                    if ((p - t.corner).sqrMagnitude > TownRadius * TownRadius || ctx.InLake(p)) { prev = null; continue; }
                    Vector2 tan = (line[i + 1] - line[i - 1]).normalized, side = new Vector2(tan.y, -tan.x);
                    Vector2 q = p + side * (r.width * 0.5f + 0.9f);
                    if (ctx.LakeDistance(q) < 3f) q = p - side * (r.width * 0.5f + 0.9f);
                    var basePos = V(q, ctx.H(q));
                    var rot = Quaternion.LookRotation(Flat(tan)) * Quaternion.Euler(0, 90, 0);
                    Vector3[] heads;
                    using (PropKit.At(poles, basePos, rot)) heads = PropKit.ElectricPole(poles);
                    var world = new Vector3[heads.Length];
                    for (int k = 0; k < heads.Length; k++) world[k] = basePos + rot * heads[k];
                    if (prev != null)
                        for (int k = 0; k < world.Length; k++)
                        {
                            SagWire(wires, prev[k], world[k], wireUV);
                            SagWire(wires, prev[k] + Vector3.down * 0.4f, world[k] + Vector3.down * (0.3f + (float)rnd.NextDouble() * 0.3f), wireUV);
                        }
                    Vector2 across = p - side * (r.width * 0.5f + 3.5f);   // wires across the road to the buildings opposite
                    for (int k = 0; k < 2 && k < world.Length; k++) SagWire(wires, world[k], V(across + tan * ((k - 0.5f) * 4f), ctx.H(across) + 6.5f + k * 0.8f), wireUV);
                    prev = world;
                }
            }
            ctx.MeshObject("Tallital poles", parent, poles, ctx.mats.atlas);
            ctx.MeshObject("Tallital wires", parent, wires, ctx.mats.atlas, null, false);
        }

        /// <summary>The police booth on the junction island: blue posts, a red tiered roof, yellow barricades.</summary>
        static void TrafficPost(WorldCtx ctx, Transform parent)
        {
            var post = ctx.Poi("Tallital Traffic Post");
            if (!post.HasValue) return;
            Vector2 p = post.Value;
            float y = ctx.H(p);
            var mb = new MeshBuilder();
            var col = new MeshBuilder();
            Vector3 c = V(p, y);
            Vector2 white = Atlas.SolidUV(Pal.ShirtWhite), red = Atlas.SolidUV(Pal.TempleRed), blue = Atlas.SolidUV(Pal.BoatBlue), yellow = Atlas.SolidUV(Pal.BoatYellow);
            mb.Cylinder(c, 2.6f, 2.6f, 0.22f, 16, white, true);                     // island kerb
            mb.Cylinder(c + Vector3.up * 0.22f, 2.45f, 2.45f, 0.02f, 16, Atlas.MaterialUV(Atlas.MatConcrete), true);
            col.Cylinder(c, 2.6f, 2.6f, 0.4f, 12, Vector2.zero, true);
            for (int k = 0; k < 4; k++)
            {
                Vector3 q = c + Quaternion.Euler(0, 45 + 90 * k, 0) * Vector3.forward * 1.0f;
                mb.Box(q + Vector3.up * 1.6f, new Vector3(0.16f, 3.2f, 0.16f), Quaternion.identity, blue);
            }
            mb.Box(c + Vector3.up * 3.25f, new Vector3(1.9f, 0.12f, 1.9f), Quaternion.identity, blue);   // the booth's floor up on the posts
            mb.Box(c + Vector3.up * 3.75f, new Vector3(1.7f, 0.9f, 1.7f), Quaternion.identity, white);
            mb.Pyramid(c + Vector3.up * 4.2f, 2.6f, 2.6f, 0.7f, red);
            mb.Box(c + Vector3.up * 5.0f, new Vector3(0.9f, 0.5f, 0.9f), Quaternion.identity, red);
            mb.Pyramid(c + Vector3.up * 5.25f, 1.3f, 1.3f, 0.5f, red);
            for (int k = 0; k < 2; k++)   // yellow barricades with red boards around the island
            {
                var rot = Quaternion.Euler(0, 30 + 120 * k, 0);
                Vector3 b = c + rot * Vector3.forward * 3.4f;
                mb.Box(b + Vector3.up * 0.55f, new Vector3(2.2f, 0.08f, 0.08f), rot, yellow);
                mb.Box(b + Vector3.up * 0.95f, new Vector3(2.2f, 0.08f, 0.08f), rot, yellow);
                mb.Box(b + Vector3.up * 0.75f, new Vector3(1.6f, 0.5f, 0.04f), rot, red);
                foreach (float s in new[] { -1f, 1f }) mb.Box(b + rot * Vector3.right * (s * 1.05f) + Vector3.up * 0.5f, new Vector3(0.08f, 1f, 0.5f), rot, yellow);
                col.Box(b + Vector3.up * 0.5f, new Vector3(2.2f, 1f, 0.5f), rot, Vector2.zero);
            }
            ctx.MeshObject("Tallital traffic post", parent, mb, ctx.mats.atlas, col);
        }

        /// <summary>The paved yard in front of the canopy where the buses stand.</summary>
        static void Apron(WorldCtx ctx, Transform parent, TallitalPlan t)
        {
            var mb = new MeshBuilder();
            const float step = 2f, halfL = 20f, n0 = -5.4f, n1 = 5.2f;
            for (float a = -halfL; a < halfL - 0.01f; a += step)
                for (float s = n0; s < n1 - 0.01f; s += step)
                {
                    float s1 = Mathf.Min(s + step, n1);
                    Vector2 p00 = t.apron + t.A * a + t.N * s, p10 = t.apron + t.A * (a + step) + t.N * s;
                    Vector2 p01 = t.apron + t.A * a + t.N * s1, p11 = t.apron + t.A * (a + step) + t.N * s1;
                    var uv = new Rect(a / 6f, s / 6f, step / 6f, (s1 - s) / 6f);
                    mb.Quad(V(p00, ctx.H(p00) + 0.06f), V(p10, ctx.H(p10) + 0.06f), V(p11, ctx.H(p11) + 0.06f), V(p01, ctx.H(p01) + 0.06f), uv, Vector3.up);
                }
            ctx.MeshObject("Bus station yard", parent, mb, ctx.mats.asphalt, null, false);
            noGrassExtra.Add((new[] { t.apron - t.A * halfL, t.apron + t.A * halfL }, 5.5f));
            noGrassExtra.Add((new[] { t.station - t.A * 18f, t.station + t.A * 18f }, 6f));
        }

        // ------------------------------------------------------------------ the stone school with the spired tower
        static void StoneSchool(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font, TallitalPlan t)
        {
            Vector2 f2 = t.schoolFront, r2 = new Vector2(f2.y, -f2.x);
            float y = ctx.H(t.school) - 0.3f;
            var rot = Quaternion.LookRotation(Flat(f2));
            Vector3 c = V(t.school, y), front = Flat(f2), right = Flat(r2);
            Rect stone = Atlas.Material(Atlas.MatStone), darkStone = Atlas.Material(Atlas.MatDarkStone);
            Vector2 green = Atlas.SolidUV(Pal.BoatGreen), glass = Atlas.SolidUV(Pal.IronBlack), spire = Atlas.Roof(5).center;
            Vector2 roofGrey = Atlas.Roof(6).center, conc = Atlas.MaterialUV(Atlas.MatConcrete), red = Atlas.SolidUV(Pal.TempleRed);

            // main hall: stone walls, steep slate-grey gable roof
            const float W = 20f, D = 11f, Hh = 7.5f;
            TexBox(mb, c + Vector3.up * (Hh * 0.5f), new Vector3(W, Hh, D), rot, stone);
            var saved = mb.M;
            mb.M = saved * Matrix4x4.TRS(c + Vector3.up * Hh, rot, Vector3.one);
            mb.GableRoof(Vector3.zero, W, D, D * 0.55f, Atlas.Roof(6), Atlas.WallSolid(7), 0.5f);
            mb.M = saved;
            col.Box(c + Vector3.up * (Hh * 0.5f), new Vector3(W, Hh, D), rot, Vector2.zero);
            // tall arched windows with green frames along the front
            for (int i = 0; i < 4; i++)
            {
                Vector3 w = c + right * (-6.5f + i * 3.6f) + front * (D * 0.5f + 0.06f) + Vector3.up * 3.6f;
                mb.Box(w, new Vector3(1.5f, 3.2f, 0.12f), rot, green);
                mb.Box(w + front * 0.04f, new Vector3(1.1f, 2.8f, 0.1f), rot, glass);
                mb.Box(w + Vector3.up * 1.75f, new Vector3(1.1f, 0.35f, 0.14f), rot, green);
                mb.Box(w + Vector3.up * 2.05f, new Vector3(0.6f, 0.28f, 0.14f), rot, green);
            }
            // the square tower at the front corner and its steep spire
            const float T = 4.4f, Th = 13f;
            Vector3 tower = c + right * (W * 0.5f - 1.4f) + front * (D * 0.5f + 1.2f);
            TexBox(mb, tower + Vector3.up * (Th * 0.5f), new Vector3(T, Th, T), rot, stone);
            col.Box(tower + Vector3.up * (Th * 0.5f), new Vector3(T, Th, T), rot, Vector2.zero);
            mb.Box(tower + Vector3.up * (Th + 0.12f), new Vector3(T + 0.4f, 0.25f, T + 0.4f), rot, Atlas.MaterialUV(Atlas.MatDarkStone));
            mb.Pyramid(tower + Vector3.up * (Th + 0.24f), T + 0.2f, T + 0.2f, 6.5f, spire);
            Vector3 rose = tower + front * (T * 0.5f + 0.05f) + Vector3.up * 8.5f;
            mb.Box(rose, new Vector3(1.3f, 1.3f, 0.12f), rot * Quaternion.Euler(0, 0, 45f), green);
            mb.Box(rose + Flat(f2) * 0.04f, new Vector3(0.9f, 0.9f, 0.1f), rot * Quaternion.Euler(0, 0, 45f), glass);
            for (int k = 0; k < 2; k++)
            {
                Vector3 w = tower + front * (T * 0.5f + 0.06f) + Vector3.up * (3.2f + k * 2.6f);
                mb.Box(w, new Vector3(0.8f, 1.8f, 0.12f), rot, green);
                mb.Box(w + front * 0.04f, new Vector3(0.55f, 1.5f, 0.1f), rot, glass);
            }
            // porch with an arched doorway at the base of the tower
            mb.Box(tower + front * (T * 0.5f + 0.8f) + Vector3.up * 1.6f, new Vector3(2.6f, 3.2f, 1.6f), rot, Atlas.WallSolid(7));
            mb.Box(tower + front * (T * 0.5f + 1.62f) + Vector3.up * 1.2f, new Vector3(1.4f, 2.4f, 0.08f), rot, Atlas.MaterialUV(Atlas.MatWood));
            // a lower stone wing to the side
            Vector3 wing = c - right * (W * 0.5f + 6f) + front * 0.5f;
            TexBox(mb, wing + Vector3.up * 2.6f, new Vector3(12f, 5.2f, 9f), rot, darkStone);
            mb.M = saved * Matrix4x4.TRS(wing + Vector3.up * 5.2f, rot, Vector3.one);
            mb.GableRoof(Vector3.zero, 12f, 9f, 3.2f, Atlas.Roof(6), Atlas.WallSolid(7), 0.4f);
            mb.M = saved;
            col.Box(wing + Vector3.up * 2.6f, new Vector3(12f, 5.2f, 9f), rot, Vector2.zero);
            // the red name board over the ground floor
            Vector3 board = c + front * (D * 0.5f + 0.1f) + Vector3.up * 1.35f + right * (-2f);
            mb.Box(board, new Vector3(9f, 0.8f, 0.08f), rot, red);
            var holder = new GameObject("School board").transform;
            holder.SetParent(parent, false);
            Label(holder, font, "BISHOP SHAW HIGH SCHOOL", board + front * 0.06f, front, 0.42f, new Color(1f, 0.95f, 0.85f));
            // boundary wall with railings towards the road
            for (float s = -W * 0.5f - 12f; s < W * 0.5f + 4f; s += 2.5f)
            {
                Vector3 p = c + right * s + front * (D * 0.5f + 5.5f);
                float gy = ctx.H(new Vector2(p.x, p.z));
                mb.Box(new Vector3(p.x, gy + 0.4f, p.z), new Vector3(2.5f, 0.8f, 0.35f), rot, conc);
                mb.Box(new Vector3(p.x, gy + 1.15f, p.z), new Vector3(2.4f, 0.05f, 0.05f), rot, green);
                mb.Box(new Vector3(p.x, gy + 0.98f, p.z) - right * 1.2f, new Vector3(0.06f, 0.4f, 0.06f), rot, green);
                col.Box(new Vector3(p.x, gy + 0.6f, p.z), new Vector3(2.5f, 1.2f, 0.35f), rot, Vector2.zero);
            }
        }

        // ------------------------------------------------------------------ Gandhi ji at the lake edge
        static void GandhiStatue(WorldCtx ctx, MeshBuilder mb, MeshBuilder col, TallitalPlan t)
        {
            float y = ctx.H(t.statue);
            var rot = Quaternion.LookRotation(Flat(t.statueFace));   // faces the chowk, the lake behind him
            Vector3 c = V(t.statue, y);
            Rect stone = Atlas.Material(Atlas.MatStone);
            Vector2 cream = Atlas.WallSolid(1), cap = Atlas.MaterialUV(Atlas.MatConcrete), bronze = Atlas.SolidUV(Pal.WoodDark), green = Atlas.SolidUV(Pal.IronGreen);
            TexBox(mb, c + Vector3.up * 0.3f, new Vector3(2.8f, 0.6f, 2.8f), rot, stone);
            mb.Box(c + Vector3.up * 1.7f, new Vector3(1.5f, 2.2f, 1.5f), rot, cream);
            mb.Box(c + Vector3.up * 2.9f, new Vector3(1.8f, 0.2f, 1.8f), rot, cap);
            col.Box(c + Vector3.up * 1.5f, new Vector3(2.8f, 3f, 2.8f), rot, Vector2.zero);
            // little iron fence round the base
            for (int k = 0; k < 4; k++)
            {
                Quaternion q = rot * Quaternion.Euler(0, 90 * k, 0);
                Vector3 side = q * Vector3.forward * 1.75f, along = q * Vector3.right * 1.75f;
                mb.Limb(c + side - along + Vector3.up * 0.95f, c + side + along + Vector3.up * 0.95f, 0.025f, 0.025f, 4, green, false);
                mb.Limb(c + side - along + Vector3.up * 0.6f, c + side - along + Vector3.up * 1f, 0.03f, 0.03f, 4, green, false);
            }
            // the walking figure with his staff, in dark bronze
            using (PropKit.At(mb, c + Vector3.up * 3f, rot, 1.15f))
            {
                mb.Limb(new Vector3(0.1f, 0f, 0.18f), new Vector3(0.1f, 0.85f, 0.02f), 0.06f, 0.08f, 6, bronze);
                mb.Limb(new Vector3(-0.1f, 0f, -0.16f), new Vector3(-0.1f, 0.85f, 0f), 0.06f, 0.08f, 6, bronze);
                mb.Cylinder(new Vector3(0f, 0.45f, 0f), 0.27f, 0.2f, 0.6f, 8, bronze, true);                  // dhoti
                mb.Cylinder(new Vector3(0f, 1.0f, 0f), 0.19f, 0.16f, 0.5f, 8, bronze, true);                  // chest and shawl
                mb.Limb(new Vector3(-0.18f, 1.48f, 0f), new Vector3(0.18f, 1.48f, 0f), 0.07f, 0.07f, 6, bronze);
                mb.Sphere(new Vector3(0f, 1.66f, 0.02f), new Vector3(0.11f, 0.13f, 0.12f), 8, 6, bronze);     // head
                mb.Limb(new Vector3(0.2f, 1.45f, 0f), new Vector3(0.34f, 1.05f, 0.24f), 0.045f, 0.04f, 5, bronze);
                mb.Limb(new Vector3(0.36f, 0f, 0.36f), new Vector3(0.34f, 1.75f, 0.2f), 0.022f, 0.022f, 5, bronze);   // staff
                mb.Limb(new Vector3(-0.2f, 1.45f, 0f), new Vector3(-0.3f, 1.0f, 0.06f), 0.045f, 0.04f, 5, bronze);
            }
        }

        // ------------------------------------------------------------------ "I ♥ NAINITAL"
        static readonly Dictionary<char, string[]> Glyph = new Dictionary<char, string[]>
        {
            ['I'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
            ['N'] = new[] { "#...#", "##..#", "#.#.#", "#.#.#", "#..##", "#...#", "#...#" },
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['*'] = new[] { ".##.##.", "#######", "#######", ".#####.", "..###..", "...#...", "......." },
        };

        static void NainitalLetters(WorldCtx ctx, MeshBuilder mb, MeshBuilder col, TallitalPlan t)
        {
            const string text = "I*NAINITAL";
            const float cell = 0.2f, depth = 0.32f;
            Vector3 face = Flat(t.lettersFace);                 // the letters face the chowk, the lake behind
            Vector3 right = Vector3.Cross(Vector3.up, -face);   // left to right for someone looking at them
            var rot = Quaternion.LookRotation(face);
            int cols = 0;
            foreach (char ch in text) cols += Glyph[ch][0].Length + 1;
            cols -= 1;
            float width = cols * cell;
            float y = ctx.H(t.letters) + 0.25f;
            Vector3 start = V(t.letters, y) - right * (width * 0.5f);
            Vector2 white = Atlas.SolidUV(Pal.ShirtWhite), red = Atlas.SolidUV(Pal.JacketRed), dark = Atlas.SolidUV(Pal.IronBlack);
            mb.Box(V(t.letters, y - 0.13f), new Vector3(width + 0.4f, 0.26f, depth + 0.3f), Quaternion.LookRotation(face), dark);
            int x0 = 0;
            foreach (char ch in text)
            {
                var g = Glyph[ch];
                Vector2 uv = ch == '*' ? red : white;
                for (int row = 0; row < 7; row++)
                {
                    string line = g[row];
                    for (int k = 0; k < line.Length;)
                    {
                        if (line[k] != '#') { k++; continue; }
                        int e = k;
                        while (e < line.Length && line[e] == '#') e++;
                        Vector3 centre = start + right * ((x0 + (k + e) * 0.5f) * cell) + Vector3.up * ((6 - row + 0.5f) * cell);
                        mb.Box(centre, new Vector3((e - k) * cell, cell, depth), rot, uv);
                        k = e;
                    }
                }
                x0 += g[0].Length + 1;
            }
            col.Box(V(t.letters, y + 0.7f), new Vector3(width, 1.5f, depth), rot, Vector2.zero);
        }
    }
}
