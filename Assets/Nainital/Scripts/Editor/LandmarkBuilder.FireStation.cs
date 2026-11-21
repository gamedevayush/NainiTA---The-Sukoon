using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Nainital's fire station at the owner's pin (22 Nov 2026: 29.391446, 79.452547 = local (-237.9, 104.9), Mallital;
    /// "check kro street view me"). Street View (Aug 2022, Tools/firestation/refs): a three-storey cream concrete block on
    /// the street, the whole ground floor one open bay with the white police jeep and the red fire jeep inside, a wide pale
    /// blue board over it ("Police Fire Station"), barred windows on the floors above, a flat roof; an old stone house
    /// beside it. Built here as that block, its bay at the road's level facing the road, the board in 3D letters (English:
    /// Devanagari does not shape in 3D letters), a water tank on the roof.
    /// PlanFireStation (before the Buildings step) keeps the footprint clear, so the data's building on the spot - the
    /// station itself - is not built twice. BuildFireStation (Landmarks) builds it and leaves a "FireStationBay" marker
    /// (Runtime/Police/FireStationBay.cs) where the 101 tender drives out from and back to.
    /// </summary>
    public static partial class LandmarkBuilder
    {
        static readonly Vector2 FirePin = new Vector2(-237.9f, 104.9f);
        const float FsW = 11f, FsD = 9f, FsFloor = 3.3f;
        static bool fsPlanned;
        static Vector2 fsO, fsAlong, fsAway;

        /// <summary>Where the station's front stands (on the road edge nearest the pin) - and nothing else may stand there.</summary>
        internal static void PlanFireStation(WorldCtx ctx)
        {
            fsPlanned = false;
            if (!WorldBuilder.NearestMotorRoad(ctx, FirePin, 60f, out Vector2 best, out Vector2 tan)) { Debug.LogWarning("[Nainital] fire station: no road near the pin"); return; }
            tan = tan.normalized;
            Vector2 n = new Vector2(-tan.y, tan.x);
            if (Vector2.Dot(FirePin - best, n) < 0f) n = -n;
            var road = NearestRoadData(ctx, best);
            float half = road != null ? road.width * 0.5f : 3f;
            fsO = best + n * (half + 0.4f);
            fsAlong = tan;
            fsAway = n;
            fsPlanned = true;
            ctx.keepClear.Add((fsO + fsAway * (FsD * 0.5f), FsW * 0.5f + 0.5f));
            Debug.Log($"[Nainital] fire station planned: front at ({fsO.x:F1}, {fsO.y:F1}) facing the road ({-fsAway.x:F2}, {-fsAway.y:F2}), {Vector2.Distance(fsO, FirePin):F1} m from the pin");
        }

        internal static void BuildFireStation(WorldCtx ctx, Transform parent)
        {
            if (!fsPlanned) PlanFireStation(ctx);
            if (!fsPlanned) return;
            Vector2 o = fsO, along = fsAlong, away = fsAway;
            float y0 = ctx.H(o - away * 0.6f);   // the road's level at the door
            Vector3 A = new Vector3(along.x, 0f, along.y), Z = new Vector3(away.x, 0f, away.y);
            var q = Quaternion.LookRotation(Z);
            Vector3 P(float x, float y, float z) => new Vector3(o.x, y0, o.y) + A * x + Vector3.up * y + Z * z;

            // the ground: level at the bay, eased back into the street and the slope
            Vector2 c2 = o + away * (FsD * 0.5f);
            float R = Mathf.Sqrt(FsW * FsW + FsD * FsD) * 0.5f + 4f;
            ShapeTerrain(ctx, c2 - Vector2.one * R, c2 + Vector2.one * R, (p, y) =>
            {
                Vector2 d = p - o;
                float lx = Mathf.Abs(Vector2.Dot(d, along)) - FsW * 0.5f, lz = Vector2.Dot(d, away);
                if (lz < -0.3f) return y;   // the road
                float outd = Mathf.Max(0f, Mathf.Max(lx, lz - FsD));
                float k = Mathf.Clamp01(outd / 4f);
                k = k * k * (3f - 2f * k);
                return Mathf.Lerp(y0 - 0.03f, y, k);
            });
            var paint = new MeshBuilder();
            paint.Box(P(0f, 0f, FsD * 0.5f), new Vector3(FsW - 0.4f, 0.02f, FsD - 0.4f), q, S(Pal.Concrete));
            WorldBuilder.PaintRoadSurface(paint, 1);

            var sm = new SiteMeshes();
            int wall = Pal.TempleWhite, trim = Pal.StoneGrey, glass = Pal.IronBlack, dark = Pal.Concrete;
            float H = FsFloor * 3f + 0.9f;
            const float bayW = 7.4f, bayH = 3.4f, bayX = -0.9f;   // the bay, a little to the left of the middle
            float lw = (bayX - bayW * 0.5f) + FsW * 0.5f, rw = FsW * 0.5f - (bayX + bayW * 0.5f);
            // the ground floor: two piers either side of the bay, the bay's back and side walls and ceiling
            sm.atlas.Box(P(-FsW * 0.5f + lw * 0.5f, bayH * 0.5f, 0.2f), new Vector3(lw, bayH, 0.4f), q, S(wall));
            sm.atlas.Box(P(FsW * 0.5f - rw * 0.5f, bayH * 0.5f, 0.2f), new Vector3(rw, bayH, 0.4f), q, S(wall));
            sm.atlas.Box(P(0f, bayH * 0.5f, FsD - 0.2f), new Vector3(FsW, bayH, 0.4f), q, S(dark));
            sm.atlas.Box(P(-FsW * 0.5f + 0.2f, bayH * 0.5f, FsD * 0.5f), new Vector3(0.4f, bayH, FsD), q, S(wall));
            sm.atlas.Box(P(FsW * 0.5f - 0.2f, bayH * 0.5f, FsD * 0.5f), new Vector3(0.4f, bayH, FsD), q, S(wall));
            sm.atlas.Box(P(0f, bayH + 0.05f, FsD * 0.5f), new Vector3(FsW, 0.1f, FsD), q, S(dark));
            // an office corner at the back right of the bay (the counter and the notice board of the photos)
            sm.atlas.Box(P(FsW * 0.5f - 1.6f, 1.2f, FsD - 2.0f), new Vector3(2.6f, 2.4f, 3.2f), q, S(Pal.SailWhite));
            sm.atlas.Box(P(FsW * 0.5f - 2.9f, 1.05f, FsD - 2.0f), new Vector3(0.05f, 2.1f, 0.9f), q, S(Pal.WoodDark));
            sm.atlas.Box(P(FsW * 0.5f - 2.92f, 1.55f, FsD - 3.1f), new Vector3(0.03f, 0.8f, 1.0f), q, S(Pal.JacketBlue));
            // the floors above: a solid block with barred windows on the front and the sides
            sm.atlas.Box(P(0f, bayH + (H - bayH) * 0.5f + 0.1f, FsD * 0.5f), new Vector3(FsW, H - bayH - 0.2f, FsD), q, S(wall));
            sm.atlas.Box(P(0f, bayH + 0.15f, -0.02f), new Vector3(FsW + 0.1f, 0.3f, 0.05f), q, S(trim));   // the beam over the bay
            for (int f = 1; f < 3; f++)
                for (int w = 0; w < 4; w++)
                {
                    float x = -FsW * 0.5f + 1.5f + w * 2.65f, y = f * FsFloor + 1.6f + 0.2f;
                    Vector3 wc = P(x, y, -0.03f);
                    sm.atlas.Box(wc, new Vector3(1.5f, 1.25f, 0.04f), q, S(trim));
                    sm.atlas.Box(wc - Z * 0.02f, new Vector3(1.3f, 1.05f, 0.03f), q, S(glass));
                    for (int b = 0; b < 6; b++)
                        sm.atlas.Box(wc - Z * 0.05f + A * (-0.55f + b * 0.22f), new Vector3(0.025f, 1.05f, 0.025f), q, S(Pal.IronGreen));
                    sm.atlas.Box(wc - Z * 0.06f + Vector3.down * 0.7f, new Vector3(1.6f, 0.08f, 0.18f), q, S(trim));   // the sill
                }
            // the roof: a parapet and a black water tank
            sm.atlas.Box(P(0f, H + 0.35f, 0.1f), new Vector3(FsW, 0.7f, 0.2f), q, S(wall));
            sm.atlas.Box(P(-FsW * 0.5f + 0.1f, H + 0.35f, FsD * 0.5f), new Vector3(0.2f, 0.7f, FsD), q, S(wall));
            sm.atlas.Box(P(FsW * 0.5f - 0.1f, H + 0.35f, FsD * 0.5f), new Vector3(0.2f, 0.7f, FsD), q, S(wall));
            sm.atlas.Cylinder(P(2.5f, H, FsD * 0.6f), 0.8f, 0.8f, 1.4f, 12, S(Pal.WaterTank), true);
            // the board over the bay: pale blue, the words in 3D letters
            float yaw = Quaternion.LookRotation(-Z).eulerAngles.y;
            Board(sm, P(bayX, bayH + 1.2f, -0.12f), yaw, 6.2f, 1.3f, Pal.BoatBlue, new[] { "POLICE FIRE STATION", "NAINITAL" }, new[] { 0.4f, 0.24f }, Pal.ShirtWhite, Pal.BoatBlue, y0, Glyph3D.Face.Block);
            // colliders: the piers, the back, the sides, the office, the floors above
            sm.col.Box(P(-FsW * 0.5f + lw * 0.5f, bayH * 0.5f, 0.2f), new Vector3(lw, bayH, 0.4f), q, Vector2.zero);
            sm.col.Box(P(FsW * 0.5f - rw * 0.5f, bayH * 0.5f, 0.2f), new Vector3(rw, bayH, 0.4f), q, Vector2.zero);
            sm.col.Box(P(0f, bayH * 0.5f, FsD - 0.2f), new Vector3(FsW, bayH, 0.4f), q, Vector2.zero);
            sm.col.Box(P(-FsW * 0.5f + 0.2f, bayH * 0.5f, FsD * 0.5f), new Vector3(0.4f, bayH, FsD), q, Vector2.zero);
            sm.col.Box(P(FsW * 0.5f - 0.2f, bayH * 0.5f, FsD * 0.5f), new Vector3(0.4f, bayH, FsD), q, Vector2.zero);
            sm.col.Box(P(FsW * 0.5f - 1.6f, 1.2f, FsD - 2.0f), new Vector3(2.6f, 2.4f, 3.2f), q, Vector2.zero);
            sm.col.Box(P(0f, bayH + (H - bayH) * 0.5f, FsD * 0.5f), new Vector3(FsW, H - bayH, FsD), q, Vector2.zero);
            sm.Emit(ctx, "Fire Station", parent);

            // where the tender stands and drives out from (Runtime/Police/FireStationBay.cs)
            var bay = new GameObject("FireStationBay");
            bay.transform.SetParent(parent, false);
            bay.transform.SetPositionAndRotation(P(bayX, 0.05f, 2.4f), Quaternion.LookRotation(-Z));
            bay.AddComponent<FireStationBay>();
            ctx.Zone("Fire Station", P(0f, 0f, FsD * 0.5f), 22f);
            ctx.landmarkSites["Fire Station"] = P(0f, 0f, FsD * 0.5f);
            ctx.lampHeads.Add(P(bayX, bayH - 0.2f, 1.2f));
            Debug.Log($"[Nainital] fire station built at ({o.x:F1}, {o.y:F1}), level {y0:F1}");
        }
    }
}
