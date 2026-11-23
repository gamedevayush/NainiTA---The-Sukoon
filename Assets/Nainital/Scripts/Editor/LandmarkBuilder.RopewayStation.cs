using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class LandmarkBuilder
    {
        // ------------------------------------------------------------------ the ropeway stations
        // Owner, 23 Nov 2026: "ropeways interactable bnaao... proper both sides entry.. or side concrete barricade bhi hain
        // sayaad real me ..poora recheck kro". The 2024 ride video (D:\Unity\ref\yt_nov23\rope): an ENTRY door into a ticket
        // hall with the rate board, a queue between steel rails, stairs up to the platform where the two red cabins come in
        // under a steel frame, the curved steel guides outside, and a lane between concrete barriers at the door.
        // Both stations are the same building, its +z toward the other station; the cable ends inside at (0, Deck + 3.2, 1).

        internal const float RwDeck = 4.3f, RwGap = 4.4f, RwHalfW = 6f, RwBack = -9f, RwFront = 4.5f, RwRoof = 8.8f;

        /// <summary>A station on the ground at `origin` (its floor level), +z toward the other station. Returns the platform
        /// spots at the two cabins' doors in world space: [0] at local +x, [1] at local -x.</summary>
        static Vector3[] RopewayStation(WorldCtx ctx, Transform parent, string name, Vector3 origin, Quaternion rot, float depth, bool top)
        {
            var mb = new MeshBuilder { M = Matrix4x4.TRS(origin, rot, Vector3.one) };
            var col = new MeshBuilder { M = mb.M };
            Vector2 wall = Atlas.WallSolid(1), cream = S(Pal.ShirtWhite), red = S(Pal.TempleRed), maroon = S(Pal.BoatRed), steel = S(Pal.StoneGrey),
                    dark = S(Pal.IronBlack), conc = M(Atlas.MatConcrete), glass = M(Atlas.MatGlass), floor = M(Atlas.MatMarble), green = S(Pal.IronGreen);
            float W = RwHalfW, B = RwBack, F = RwFront, D = RwDeck, H = RwRoof;

            // the plinth (the ground falls away under part of it), the floor
            mb.Box(new Vector3(0f, -depth * 0.5f - 0.05f, (B + F) * 0.5f), new Vector3(W * 2f + 0.4f, depth + 0.1f, F - B + 0.4f), conc);
            col.Box(new Vector3(0f, -depth * 0.5f, (B + F) * 0.5f), new Vector3(W * 2f + 0.4f, depth, F - B + 0.4f), Vector2.zero);
            mb.Box(new Vector3(0f, 0.02f, (B + F) * 0.5f), new Vector3(W * 2f, 0.04f, F - B), floor);

            // the walls: back with the ENTRY door (2.8 m wide, 3 m high), the two sides, the front below the platform
            void Wall(Vector3 a, Vector3 b, float y0, float y1, Vector2 uv)
            {
                Vector3 m = (a + b) * 0.5f, d = b - a;
                var r = Quaternion.LookRotation(d.normalized);
                mb.Box(new Vector3(m.x, (y0 + y1) * 0.5f, m.z), new Vector3(0.3f, y1 - y0, d.magnitude), r, uv);
                col.Box(new Vector3(m.x, (y0 + y1) * 0.5f, m.z), new Vector3(0.3f, y1 - y0, d.magnitude), r, Vector2.zero);
            }
            Wall(new Vector3(-W, 0, B), new Vector3(-1.4f, 0, B), 0f, H, wall);
            Wall(new Vector3(1.4f, 0, B), new Vector3(W, 0, B), 0f, H, wall);
            Wall(new Vector3(-1.4f, 0, B), new Vector3(1.4f, 0, B), 3f, H, wall);       // over the door
            Wall(new Vector3(-W, 0, B), new Vector3(-W, 0, F), 0f, H, wall);
            Wall(new Vector3(W, 0, B), new Vector3(W, 0, F), 0f, H, wall);
            Wall(new Vector3(-W, 0, F), new Vector3(W, 0, F), 0f, D, wall);
            mb.Box(new Vector3(0f, D * 0.5f, B - 0.17f), new Vector3(W * 2f + 0.1f, 0.6f, 0.06f), red);                 // a red band
            mb.Box(new Vector3(0f, 3.1f, B - 0.2f), new Vector3(3.4f, 0.25f, 0.5f), red);                                 // the door's hood
            for (int k = 0; k < 4; k++)   // windows on the sides, lit at night
                foreach (float s in new[] { -1f, 1f })
                {
                    float z = Mathf.Lerp(B + 1.5f, F - 1.5f, k / 3f);
                    mb.Box(new Vector3(s * (W + 0.16f), 6.2f, z), new Vector3(0.04f, 1.3f, 1.4f), S(Pal.WindowGlow));
                    mb.Box(new Vector3(s * (W + 0.16f), 2f, z), new Vector3(0.04f, 1.2f, 1.2f), glass);
                }
            // the roof: a slab with a red fascia, open over the front where the cables come in
            mb.Box(new Vector3(0f, H + 0.1f, (B + F) * 0.5f), new Vector3(W * 2f + 0.8f, 0.2f, F - B + 0.8f), conc);
            mb.Box(new Vector3(0f, H - 0.15f, F + 0.42f), new Vector3(W * 2f + 0.8f, 0.5f, 0.06f), red);
            mb.Box(new Vector3(0f, H - 0.15f, B - 0.42f), new Vector3(W * 2f + 0.8f, 0.5f, 0.06f), red);

            // the stairs: 26 steps up along the +x wall from the back to the platform (a hole in the platform over them)
            const int steps = 26;
            float sx0 = 3.05f, sx1 = W - 0.15f, z0 = B + 0.5f, z1 = -1f, tread = (z1 - z0) / steps, rise = D / steps;
            for (int k = 0; k < steps; k++)
            {
                float zs = z0 + (k + 0.5f) * tread, top2 = (k + 1) * rise;
                mb.Box(new Vector3((sx0 + sx1) * 0.5f, top2 * 0.5f, zs), new Vector3(sx1 - sx0, top2, tread), conc);
                mb.Box(new Vector3((sx0 + sx1) * 0.5f, top2 - 0.01f, zs + tread * 0.42f), new Vector3(sx1 - sx0, 0.02f, 0.05f), dark);   // nosing
            }
            // the stairs' collider: a ramp the walker climbs smoothly
            {
                Vector3 a = new Vector3((sx0 + sx1) * 0.5f, 0f, z0), b = new Vector3((sx0 + sx1) * 0.5f, D, z1);
                Vector3 m = (a + b) * 0.5f, d = b - a;
                var r = Quaternion.LookRotation(d.normalized);
                col.Box(m + r * Vector3.down * 0.15f, new Vector3(sx1 - sx0, 0.3f, d.magnitude + 0.3f), r, Vector2.zero);
            }
            // the stair's rail on its open side
            mb.Limb(new Vector3(sx0, 1.0f, z0), new Vector3(sx0, D + 1.0f, z1), 0.035f, 0.035f, 5, steel, false);
            for (int k = 0; k <= 6; k++) { float t = k / 6f; Vector3 p = new Vector3(sx0, 0f, Mathf.Lerp(z0, z1, t)) + Vector3.up * (D * t); mb.Limb(p, p + Vector3.up * 1.0f, 0.025f, 0.025f, 4, steel, false); }

            // the platform: the deck over the front, with a hole over the stairs, a railing round the hole
            float dz0 = -5f;
            void Deck(float x0, float x1, float za, float zb)
            {
                var c = new Vector3((x0 + x1) * 0.5f, D - 0.12f, (za + zb) * 0.5f);
                var s = new Vector3(x1 - x0, 0.24f, zb - za);
                mb.Box(c, s, conc);
                mb.Box(c + Vector3.up * 0.125f, new Vector3(s.x, 0.01f, s.z), floor);
                col.Box(c, s, Vector2.zero);
            }
            Deck(-W, sx0, dz0, F);
            Deck(sx0, W, z1, F);
            mb.Limb(new Vector3(sx0 - 0.05f, D + 1.0f, dz0), new Vector3(sx0 - 0.05f, D + 1.0f, z1), 0.035f, 0.035f, 5, steel, false);
            mb.Limb(new Vector3(-W, D + 1.0f, dz0 - 0.05f), new Vector3(sx0, D + 1.0f, dz0 - 0.05f), 0.035f, 0.035f, 5, steel, false);
            col.Box(new Vector3(sx0 - 0.05f, D + 0.55f, (dz0 + z1) * 0.5f), new Vector3(0.08f, 1.1f, z1 - dz0), Vector2.zero);
            col.Box(new Vector3((-W + sx0) * 0.5f, D + 0.55f, dz0 - 0.05f), new Vector3(sx0 + W, 1.1f, 0.08f), Vector2.zero);
            // the front edge: a low steel rail, open in front of each bay
            foreach (float s in new[] { -1f, 1f })
            {
                float bx = s * RwGap * 0.5f;
                mb.Box(new Vector3(bx, D + 0.01f, 1f), new Vector3(2.6f, 0.02f, 3.2f), S(Pal.BoatYellow));   // the bay, edged yellow
                mb.Box(new Vector3(bx, D + 0.015f, 1f), new Vector3(2.3f, 0.02f, 2.9f), floor);
            }
            // the steel frame of the cabin hall, painted red, the bull-wheel housing high at the back of the bays
            foreach (float x in new[] { -W + 0.3f, -0.15f, W - 0.3f })
                foreach (float z in new[] { dz0 + 0.3f, F - 0.3f })
                    mb.Box(new Vector3(x, (D + H) * 0.5f, z), new Vector3(0.3f, H - D, 0.3f), red);
            mb.Box(new Vector3(0f, H - 0.6f, (dz0 + F) * 0.5f), new Vector3(W * 2f - 0.6f, 0.3f, 0.3f), red);
            mb.Cylinder(new Vector3(0f, D + 3.0f, -2.2f), 2.6f, 2.6f, 0.35f, 20, dark, true);               // the big wheel, flat
            mb.Box(new Vector3(0f, D + 2.2f, -2.2f), new Vector3(1.2f, 1.6f, 1.2f), steel);

            // the hall below: the ticket counter (rate board over it), the queue rails from the door to the stairs
            mb.Box(new Vector3(-3.8f, 0.55f, B + 2.2f), new Vector3(3.6f, 1.1f, 0.6f), maroon);
            mb.Box(new Vector3(-3.8f, 1.6f, B + 2.2f), new Vector3(3.6f, 1.0f, 0.04f), glass);
            mb.Box(new Vector3(-3.8f, 2.4f, B + 2.2f), new Vector3(3.8f, 0.35f, 0.2f), red);
            mb.Box(new Vector3(-W + 0.18f, 2.2f, -4f), new Vector3(0.04f, 1.6f, 2.4f), cream);                 // the rate board
            col.Box(new Vector3(-3.8f, 1.2f, B + 2.2f), new Vector3(3.6f, 2.4f, 0.6f), Vector2.zero);
            void Rail(Vector3 a, Vector3 b)
            {
                mb.Limb(a + Vector3.up * 0.95f, b + Vector3.up * 0.95f, 0.025f, 0.025f, 4, steel, false);
                mb.Limb(a + Vector3.up * 0.5f, b + Vector3.up * 0.5f, 0.02f, 0.02f, 4, steel, false);
                for (float t = 0f; t <= 1.001f; t += 0.25f) { Vector3 p = Vector3.Lerp(a, b, t); mb.Limb(p, p + Vector3.up * 0.95f, 0.025f, 0.025f, 4, steel, false); }
                Vector3 m = (a + b) * 0.5f, d = b - a;
                col.Box(m + Vector3.up * 0.5f, new Vector3(0.06f, 1f, d.magnitude), Quaternion.LookRotation(d.normalized), Vector2.zero);
            }
            Rail(new Vector3(-1.2f, 0f, B + 1.2f), new Vector3(-1.2f, 0f, -3.5f));
            Rail(new Vector3(1.4f, 0f, B + 2.4f), new Vector3(1.4f, 0f, -3.5f));
            Rail(new Vector3(1.4f, 0f, B + 2.4f), new Vector3(2.9f, 0f, B + 2.4f));
            // benches for the wait upstairs (red seats, the top station's hall)
            for (int k = 0; k < 3; k++) mb.Box(new Vector3(-W + 0.6f, D + 0.45f, -4f + k * 1.6f), new Vector3(0.5f, 0.45f, 1.3f), red);

            // the curved steel guides in front of the bays (the arcs in the video), and a light on each bay
            foreach (float s in new[] { -1f, 1f })
            {
                float bx = s * RwGap * 0.5f;
                Vector3 last = default;
                for (int k = 0; k <= 12; k++)
                {
                    float t = k / 12f;
                    Vector3 p = new Vector3(bx + s * Mathf.Sin(t * Mathf.PI) * 1.4f, D + 0.6f + Mathf.Sin(t * Mathf.PI) * 1.2f, F + t * 7f);
                    if (k > 0) mb.Limb(last, p, 0.09f, 0.09f, 6, steel, false);
                    last = p;
                }
                WorldBuilder.Accent(mb.M.MultiplyPoint3x4(new Vector3(bx, D + 3.6f, 1f)), new Color(0.95f, 0.97f, 1f), 7f, 10f);
            }
            WorldBuilder.Accent(mb.M.MultiplyPoint3x4(new Vector3(-2f, 3.4f, B + 3f)), new Color(1f, 0.9f, 0.75f), 8f, 9f);   // the hall
            WorldBuilder.Accent(mb.M.MultiplyPoint3x4(new Vector3(0f, 3.2f, B - 1.2f)), new Color(1f, 0.85f, 0.6f), 6f, 7f);   // over the door
            // an LED line along the roof's front and back edges, the station name lit
            WorldBuilder.Led(mb.M.MultiplyPoint3x4(new Vector3(-W - 0.4f, H + 0.22f, F + 0.45f)), mb.M.MultiplyPoint3x4(new Vector3(W + 0.4f, H + 0.22f, F + 0.45f)), WorldBuilder.LedWarm, 0.12f, 0.05f, 2.4f);
            WorldBuilder.Led(mb.M.MultiplyPoint3x4(new Vector3(-W - 0.4f, H + 0.22f, B - 0.45f)), mb.M.MultiplyPoint3x4(new Vector3(W + 0.4f, H + 0.22f, B - 0.45f)), WorldBuilder.LedWarm, 0.12f, 0.05f, 2.4f);

            var go = ctx.MeshObject(name, parent, mb, ctx.mats.atlas, col);
            var rainRoof = new GameObject(name + " rain roof") { layer = VehicleFactory.VehicleLayer };   // the rain stops on it (WeatherSystem)
            rainRoof.transform.SetParent(go.transform, false);
            rainRoof.transform.SetPositionAndRotation(origin + rot * new Vector3(0f, H + 0.35f, (B + F) * 0.5f), rot);
            rainRoof.AddComponent<BoxCollider>().size = new Vector3(W * 2f + 0.8f, 0.3f, F - B + 0.8f);
            // the hall's echo (owner, 23 Nov: "reverb")
            var echo = new GameObject(name + " echo").AddComponent<AudioReverbZone>();
            echo.transform.SetParent(go.transform, false);
            echo.transform.position = origin + rot * new Vector3(0f, 3f, (B + F) * 0.5f);
            echo.reverbPreset = AudioReverbPreset.Hallway;
            echo.minDistance = Mathf.Max(W, (F - B) * 0.5f) * 0.8f;
            echo.maxDistance = echo.minDistance + 6f;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Vector3 back = rot * Vector3.back;
            WorldBuilder.Label(go.transform, font, "ENTRY", origin + rot * new Vector3(0f, 3.55f, B - 0.48f), back, 0.42f, Color.white);
            WorldBuilder.Label(go.transform, font, top ? "SNOW VIEW ROPEWAY STATION" : "NAINITAL ROPEWAY - MALLITAL", origin + rot * new Vector3(0f, 6.4f, B - 0.18f), back, 0.5f, new Color(1f, 0.92f, 0.5f));
            WorldBuilder.Label(go.transform, font, "KUMAON MANDAL VIKAS NIGAM", origin + rot * new Vector3(0f, 5.7f, B - 0.18f), back, 0.3f, Color.white);
            WorldBuilder.Label(go.transform, font, "TICKET", origin + rot * new Vector3(-3.8f, 2.42f, B + 2.08f), rot * Vector3.back, 0.22f, Color.white);

            // outside the door: the steps down to the ground if the floor stands above it, and the lane between concrete
            // barriers (jersey blocks) out to the street, each left out where something already stands
            var outside = new MeshBuilder { M = mb.M };
            var outCol = new MeshBuilder { M = mb.M };
            Vector2 Wd(Vector3 local) { Vector3 w = mb.M.MultiplyPoint3x4(local); return new Vector2(w.x, w.z); }
            float doorGround = ctx.H(Wd(new Vector3(0f, 0f, B - 1.2f))) - origin.y;
            if (doorGround < -0.12f)
            {
                int n = Mathf.Clamp(Mathf.CeilToInt(-doorGround / 0.17f), 1, 14);
                for (int k = 0; k < n; k++)
                {
                    float topY = -k * (-doorGround / n);
                    outside.Box(new Vector3(0f, (topY + doorGround) * 0.5f, B - 0.3f - k * 0.3f - 0.15f), new Vector3(3f, topY - doorGround, 0.3f), conc);
                    outCol.Box(new Vector3(0f, (topY + doorGround) * 0.5f, B - 0.3f - k * 0.3f - 0.15f), new Vector3(3f, topY - doorGround, 0.3f), Vector2.zero);
                }
            }
            int blocks = 0;
            Physics.SyncTransforms();
            var spots = new List<Vector3>();
            foreach (float s in new[] { -1f, 1f })
            {
                for (int k = 0; k < 6; k++) spots.Add(new Vector3(s * 2.2f, 0f, B - 2.4f - k * 2.1f));   // the door's lane
                for (int k = 0; k < 6; k++) spots.Add(new Vector3(s * (W + 1.0f), 0f, B + 1.2f + k * 2.2f));   // along the side walls
            }
            foreach (var spot in spots)
                {
                    Vector3 local = spot;
                    Vector3 w = mb.M.MultiplyPoint3x4(local);
                    float gy = ctx.H(new Vector2(w.x, w.z));
                    var center = new Vector3(w.x, gy + 0.45f, w.z);
                    bool hitSomething = false;   // the ground itself does not count (a slope cut the box before)
                    foreach (var hc in Physics.OverlapBox(center + Vector3.up * 0.1f, new Vector3(0.35f, 0.35f, 0.95f), rot, ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 10)), QueryTriggerInteraction.Ignore))
                        if (!(hc is TerrainCollider)) { hitSomething = true; break; }
                    if (hitSomething) continue;
                    if (WorldBuilder.Clearance(ctx).Blocked(new Vector2(w.x, w.z), 0.5f)) continue;
                    float ly = gy - origin.y;
                    // a jersey barrier: a wide foot, a sloping waist, a narrow top; white with a red band
                    outside.Box(local + new Vector3(0f, ly + 0.12f, 0f), new Vector3(0.6f, 0.24f, 1.9f), conc);
                    outside.Box(local + new Vector3(0f, ly + 0.45f, 0f), new Vector3(0.4f, 0.42f, 1.9f), cream);
                    outside.Box(local + new Vector3(0f, ly + 0.76f, 0f), new Vector3(0.24f, 0.2f, 1.9f), red);
                    outCol.Box(local + new Vector3(0f, ly + 0.43f, 0f), new Vector3(0.5f, 0.86f, 1.9f), Vector2.zero);
                    blocks++;
                }
            ctx.MeshObject(name + " - door and barriers", parent, outside, ctx.mats.atlas, outCol);
            ctx.Zone(name, origin, 18f);

            Vector3 Door(float s) => mb.M.MultiplyPoint3x4(new Vector3(s * (RwGap * 0.5f - 1.65f), D + 0.05f, 1f));
            Debug.Log($"[Nainital] {name}: floor {origin.y:F1}, platform {origin.y + D:F1}, door {doorGround:F2} m below, {blocks} barriers");
            return new[] { Door(1f), Door(-1f) };
        }
    }
}
