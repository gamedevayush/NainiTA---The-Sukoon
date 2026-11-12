using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>
        /// Where Mall Road reaches Mallital, as in the user's Street View photo (2026-11-12): the statue on a tall red
        /// pedestal in a small round garden with a low wall and iron fence, shrubs and benches round it, and on the
        /// footpath side the tourist help centre with its blue board and the "Nainital Parking Place" notice. The site
        /// is ahead of the user's Street View point (the MallitalShotMallEnd marker), moved off the carriageway.
        /// </summary>
        static void MallitalEntry(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font)
        {
            var sv = new Vector2(87.5f, 117.2f);   // the Street View point at the Mallital end of Mall Road
            var look = new Vector2(Mathf.Sin(194.7f * Mathf.Deg2Rad), Mathf.Cos(194.7f * Mathf.Deg2Rad));
            var right = new Vector2(look.y, -look.x);

            // the statue garden: about 18 m ahead, a little to the right, wherever it is clear of every road by 4.5 m
            float RoadClear(Vector2 p)
            {
                float best = 99f;
                foreach (var r in ctx.world.roads)
                {
                    if (IsMinor(r.kind)) continue;
                    var line = Geo.ToPoints(r.pts);
                    if (line.Length < 2) continue;
                    best = Mathf.Min(best, Geo.DistanceToPolyline(line, p, false) - r.width * 0.5f);
                }
                return best;
            }
            Vector2 want = sv + look * 18f + right * 3f, site = want;
            float bestScore = float.MaxValue;
            for (float dx = -12f; dx <= 12f; dx += 1.5f)
                for (float dz = -12f; dz <= 12f; dz += 1.5f)
                {
                    var q = want + new Vector2(dx, dz);
                    if (ctx.InLake(q) || RoadClear(q) < 4.5f) continue;
                    float s = (q - want).sqrMagnitude;
                    if (s < bestScore) { bestScore = s; site = q; }
                }
            float y = ctx.H(site);
            Vector2 toRoad = NearestRoadPoint(ctx, site, 30f, r => !IsMinor(r.kind), out var rp, out _) ? (rp - site).normalized : -look;
            var rot = Quaternion.LookRotation(Flat(toRoad));   // the statue looks out over the road
            Vector3 c = V(site, y);
            Rect stone = Atlas.Material(Atlas.MatStone);
            Vector2 cream = Atlas.WallSolid(1), conc = Atlas.MaterialUV(Atlas.MatConcrete), red = Atlas.SolidUV(Pal.TempleRed);
            Vector2 green = Atlas.SolidUV(Pal.IronGreen), grass = Atlas.SolidUV(Pal.Grass), bronze = Atlas.SolidUV(Pal.WoodDark), leaf = Atlas.SolidUV(Pal.Oak);

            // round garden: low stone wall, a lawn, shrubs and a green iron fence on the wall
            const float R = 4.2f;
            const int seg = 20;
            for (int k = 0; k < seg; k++)
            {
                float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg, am = (a0 + a1) * 0.5f;
                Vector3 m = c + new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)) * R;
                var q = Quaternion.LookRotation(new Vector3(-Mathf.Sin(am), 0f, Mathf.Cos(am)));
                TexBox(mb, m + Vector3.up * 0.25f, new Vector3(0.35f, 0.5f, R * (a1 - a0) + 0.05f), q, stone);
                Vector3 p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * R, p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * R;
                mb.Limb(p0 + Vector3.up * 0.5f, p0 + Vector3.up * 1.05f, 0.025f, 0.025f, 4, green, false);
                mb.Limb(p0 + Vector3.up * 1.0f, p1 + Vector3.up * 1.0f, 0.018f, 0.018f, 4, green, false);
                mb.Limb(p0 + Vector3.up * 0.75f, p1 + Vector3.up * 0.75f, 0.015f, 0.015f, 4, green, false);
            }
            mb.Cylinder(c, R - 0.15f, R - 0.15f, 0.42f, seg, grass, true);
            var rnd = new System.Random(31);
            for (int k = 0; k < 7; k++)
            {
                float a = k * 0.9f + 0.3f, rr = R - 1.0f;
                mb.Sphere(c + new Vector3(Mathf.Cos(a) * rr, 0.75f, Mathf.Sin(a) * rr), new Vector3(0.55f, 0.45f, 0.55f) * (0.8f + (float)rnd.NextDouble() * 0.5f), 7, 5, leaf);
            }
            col.Cylinder(c, R + 0.2f, R + 0.2f, 1.1f, 12, Vector2.zero, true);

            // the pedestal: cream plinth, tall red shaft, cream cap
            TexBox(mb, c + Vector3.up * 0.75f, new Vector3(2.0f, 0.7f, 2.0f), rot, stone);
            mb.Box(c + Vector3.up * 2.3f, new Vector3(1.25f, 2.4f, 1.25f), rot, red);
            mb.Box(c + Vector3.up * 3.6f, new Vector3(1.6f, 0.2f, 1.6f), rot, cream);
            mb.Box(c + Vector3.up * 1.25f, new Vector3(1.5f, 0.15f, 1.5f), rot, conc);
            // the figure: a standing leader in a long coat and cap, dark bronze
            using (PropKit.At(mb, c + Vector3.up * 3.7f, rot, 1.2f))
            {
                mb.Limb(new Vector3(0.11f, 0f, 0.04f), new Vector3(0.1f, 0.8f, 0f), 0.065f, 0.08f, 6, bronze);
                mb.Limb(new Vector3(-0.11f, 0f, -0.02f), new Vector3(-0.1f, 0.8f, 0f), 0.065f, 0.08f, 6, bronze);
                mb.Cylinder(new Vector3(0f, 0.45f, 0f), 0.27f, 0.22f, 0.75f, 9, bronze, true);   // the long coat (achkan) to below the knee
                mb.Cylinder(new Vector3(0f, 1.15f, 0f), 0.22f, 0.18f, 0.35f, 9, bronze, true);   // chest
                mb.Limb(new Vector3(-0.2f, 1.48f, 0f), new Vector3(0.2f, 1.48f, 0f), 0.075f, 0.075f, 6, bronze);   // shoulders
                mb.Sphere(new Vector3(0f, 1.67f, 0.02f), new Vector3(0.11f, 0.13f, 0.12f), 8, 6, bronze);   // head
                mb.Box(new Vector3(0f, 1.79f, 0.01f), new Vector3(0.2f, 0.07f, 0.24f), Quaternion.identity, bronze);   // cap
                mb.Limb(new Vector3(0.22f, 1.46f, 0f), new Vector3(0.28f, 1.0f, 0.12f), 0.05f, 0.045f, 5, bronze);   // right arm forward
                mb.Limb(new Vector3(0.28f, 1.0f, 0.12f), new Vector3(0.24f, 1.05f, 0.38f), 0.045f, 0.04f, 5, bronze);
                mb.Limb(new Vector3(-0.22f, 1.46f, 0f), new Vector3(-0.26f, 0.9f, 0.02f), 0.05f, 0.045f, 5, bronze); // left arm down
            }

            // benches round the garden
            for (int k = 0; k < 3; k++)
            {
                float a = Mathf.Atan2(toRoad.y, toRoad.x) + Mathf.PI + (k - 1) * 0.8f;
                Vector2 bp = site + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (R + 1.4f);
                if (RoadClear(bp) < 1.5f) continue;
                Vector2 face = (bp - site).normalized;
                using (PropKit.At(mb, V(bp, ctx.H(bp)), Quaternion.LookRotation(Flat(face)))) PropKit.Bench(mb);
            }

            // the footpath side, left of the Street View point: tourist help centre and the parking notice
            var holder = new GameObject("Mallital entry").transform;
            holder.SetParent(parent, false);
            Vector2 kp = sv - right * 6f + look * 4f;
            for (int tries = 0; tries < 8 && RoadClear(kp) < 1.2f; tries++) kp -= right * 1.2f;
            float ky = ctx.H(kp);
            Vector2 kf = NearestRoadPoint(ctx, kp, 30f, r => !IsMinor(r.kind), out var krp, out _) ? (krp - kp).normalized : right;
            var krot = Quaternion.LookRotation(Flat(kf));
            Vector3 k3 = V(kp, ky), kfront = Flat(kf);
            mb.Box(k3 + Vector3.up * 1.3f, new Vector3(3.4f, 2.6f, 2.4f), krot, cream);
            mb.Box(k3 + Vector3.up * 2.7f, new Vector3(3.8f, 0.2f, 2.8f), krot, red);
            mb.Box(k3 + Vector3.up * 1.2f + kfront * 1.21f, new Vector3(1.0f, 2.0f, 0.04f), krot, Atlas.SolidUV(Pal.WoodDark));   // door
            mb.Box(k3 + Vector3.up * 3.35f + kfront * 0.9f, new Vector3(3.6f, 1.1f, 0.08f), krot, Atlas.SolidUV(Pal.BoatBlue));  // the blue board
            col.Box(k3 + Vector3.up * 1.3f, new Vector3(3.4f, 2.6f, 2.4f), krot, Vector2.zero);
            Label(holder, font, "PARYATAK SAHAYATA KENDRA", k3 + Vector3.up * 3.55f + kfront * 0.95f, kfront, 0.2f, Color.white);
            Label(holder, font, "TOURIST HELP CENTRE  NAINITAL", k3 + Vector3.up * 3.15f + kfront * 0.95f, kfront, 0.17f, new Color(1f, 0.9f, 0.4f));
            // the parking notice on two posts
            Vector2 np = kp + look * 4f;
            float ny = ctx.H(np);
            Vector3 n3 = V(np, ny);
            var nrot = Quaternion.LookRotation(Flat(kf));
            Vector3 nside = Vector3.Cross(Vector3.up, kfront);
            mb.Limb(n3 + nside * 0.55f, n3 + nside * 0.55f + Vector3.up * 2.4f, 0.04f, 0.04f, 5, Atlas.SolidUV(Pal.IronBlack), false);
            mb.Limb(n3 - nside * 0.55f, n3 - nside * 0.55f + Vector3.up * 2.4f, 0.04f, 0.04f, 5, Atlas.SolidUV(Pal.IronBlack), false);
            mb.Box(n3 + Vector3.up * 1.8f + kfront * 0.03f, new Vector3(1.3f, 1.1f, 0.04f), nrot, Atlas.SolidUV(Pal.ShirtWhite));
            Label(holder, font, "NAINITAL", n3 + Vector3.up * 2.2f + kfront * 0.07f, kfront, 0.13f, new Color(0.1f, 0.25f, 0.6f));
            Label(holder, font, "PARKING PLACE", n3 + Vector3.up * 1.95f + kfront * 0.07f, kfront, 0.12f, new Color(0.75f, 0.1f, 0.1f));
            Label(holder, font, "NO PARKING ON MALL ROAD", n3 + Vector3.up * 1.6f + kfront * 0.07f, kfront, 0.07f, Color.black);
            Debug.Log($"[Nainital] Mallital entry: statue garden at {site}, help centre at {kp}");
        }
    }
}
