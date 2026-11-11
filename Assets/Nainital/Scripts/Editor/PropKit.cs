using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Mid-poly props: Mall Road lamps, benches, stalls, signs, boats, yachts, jetty, railing, trees.</summary>
    public static class PropKit
    {
        sealed class AtScope : IDisposable
        {
            readonly MeshBuilder mb; readonly Matrix4x4 saved;
            public AtScope(MeshBuilder mb, Vector3 p, Quaternion r, float s) { this.mb = mb; saved = mb.M; mb.M = saved * Matrix4x4.TRS(p, r, Vector3.one * s); }
            public void Dispose() => mb.M = saved;
        }

        public static IDisposable At(MeshBuilder mb, Vector3 pos, Quaternion rot, float scale = 1f) => new AtScope(mb, pos, rot, scale);

        static Vector2 S(int pal) => Atlas.SolidUV(pal);

        // ------------------------------------------------------------------ street furniture
        /// <summary>Victorian style Mall Road lamp. Returns local position of the bulb.</summary>
        public static Vector3 MallLamp(MeshBuilder mb)
        {
            Vector2 iron = S(Pal.IronGreen), bulb = S(Pal.LampBulb), dark = S(Pal.IronBlack);
            mb.Cylinder(Vector3.zero, 0.2f, 0.13f, 0.55f, 8, iron, true);
            mb.Cylinder(new Vector3(0, 0.55f, 0), 0.075f, 0.06f, 3.3f, 6, iron, true);
            mb.Sphere(new Vector3(0, 3.9f, 0), Vector3.one * 0.1f, 6, 3, iron);
            mb.Box(new Vector3(0, 4.05f, 0), new Vector3(0.36f, 0.06f, 0.36f), dark);
            mb.Sphere(new Vector3(0, 4.28f, 0), new Vector3(0.17f, 0.21f, 0.17f), 8, 5, bulb);
            mb.Pyramid(new Vector3(0, 4.5f, 0), 0.46f, 0.46f, 0.3f, dark);
            return new Vector3(0, 4.28f, 0);
        }

        /// <summary>Simple road lamp pole with an arm over the road (+Z).</summary>
        public static Vector3 RoadLamp(MeshBuilder mb)
        {
            Vector2 grey = S(Pal.StoneGrey), bulb = S(Pal.LampBulb);
            mb.Cylinder(Vector3.zero, 0.09f, 0.06f, 5.6f, 6, grey, true);
            mb.Limb(new Vector3(0, 5.5f, 0), new Vector3(0, 5.75f, 1.1f), 0.045f, 0.04f, 5, grey);
            mb.Box(new Vector3(0, 5.7f, 1.25f), new Vector3(0.22f, 0.1f, 0.5f), grey);
            mb.Box(new Vector3(0, 5.62f, 1.25f), new Vector3(0.16f, 0.05f, 0.4f), bulb);
            return new Vector3(0, 5.55f, 1.25f);
        }

        public static void Bench(MeshBuilder mb)
        {
            Vector2 wood = S(Pal.WoodLight), iron = S(Pal.IronBlack);
            for (int i = 0; i < 3; i++) mb.Box(new Vector3(0, 0.45f, -0.12f + i * 0.13f), new Vector3(1.8f, 0.05f, 0.11f), wood);
            mb.Box(new Vector3(0, 0.78f, -0.27f), new Vector3(1.8f, 0.12f, 0.04f), Quaternion.Euler(-12, 0, 0), wood);
            mb.Box(new Vector3(0, 0.62f, -0.25f), new Vector3(1.8f, 0.1f, 0.04f), Quaternion.Euler(-12, 0, 0), wood);
            foreach (float x in new[] { -0.8f, 0.8f })
            {
                mb.Box(new Vector3(x, 0.22f, 0.12f), new Vector3(0.06f, 0.45f, 0.06f), iron);
                mb.Box(new Vector3(x, 0.45f, -0.27f), new Vector3(0.06f, 0.9f, 0.06f), iron);
            }
        }

        public static void InfoBoard(MeshBuilder mb)
        {
            Vector2 post = S(Pal.WoodDark), board = S(Pal.WoodLight), title = S(Pal.Saffron);
            mb.Box(new Vector3(-0.65f, 0.75f, 0), new Vector3(0.1f, 1.5f, 0.1f), post);
            mb.Box(new Vector3(0.65f, 0.75f, 0), new Vector3(0.1f, 1.5f, 0.1f), post);
            mb.Box(new Vector3(0, 1.45f, 0), new Vector3(1.5f, 0.95f, 0.07f), board);
            mb.Box(new Vector3(0, 1.82f, 0.04f), new Vector3(1.4f, 0.16f, 0.02f), title);
            mb.Pyramid(new Vector3(0, 1.95f, 0), 1.7f, 0.3f, 0.2f, post);
        }

        public static void FoodStall(MeshBuilder mb, int awning)
        {
            Vector2 wood = S(Pal.WoodLight), dark = S(Pal.WoodDark), aw = S(awning), steel = S(Pal.Concrete), sign = S(Pal.Saffron);
            mb.Box(new Vector3(0, 0.5f, 0), new Vector3(2.2f, 1f, 1f), wood);
            mb.Box(new Vector3(0, 1.02f, 0), new Vector3(2.3f, 0.05f, 1.1f), dark);
            foreach (float x in new[] { -1.05f, 1.05f })
                foreach (float z in new[] { -0.45f, 0.45f })
                    mb.Box(new Vector3(x, 1.5f, z), new Vector3(0.06f, 1f, 0.06f), dark);
            mb.QuadDouble(new Vector3(-1.25f, 2.0f, 0.85f), new Vector3(1.25f, 2.0f, 0.85f), new Vector3(1.25f, 2.25f, -0.6f), new Vector3(-1.25f, 2.25f, -0.6f),
                          new Rect(aw.x, aw.y, 0, 0), Vector3.up);
            mb.Cylinder(new Vector3(-0.5f, 1.05f, 0), 0.25f, 0.25f, 0.45f, 8, steel, true);   // momo steamer
            mb.Cylinder(new Vector3(-0.5f, 1.5f, 0), 0.27f, 0.2f, 0.1f, 8, steel, true);
            mb.Box(new Vector3(0.5f, 1.12f, 0.1f), new Vector3(0.5f, 0.15f, 0.35f), S(Pal.ShirtWhite));
            mb.Box(new Vector3(0, 2.35f, 0.86f), new Vector3(1.6f, 0.3f, 0.04f), sign);
        }

        // ------------------------------------------------------------------ lake craft
        static readonly Vector2[] BoatRim =
        {
            new Vector2(0f, 2.1f), new Vector2(0.62f, 1.2f), new Vector2(0.74f, 0f), new Vector2(0.64f, -1.4f), new Vector2(0.5f, -1.95f),
            new Vector2(-0.5f, -1.95f), new Vector2(-0.64f, -1.4f), new Vector2(-0.74f, 0f), new Vector2(-0.62f, 1.2f),
        };

        static void Hull(MeshBuilder mb, Vector2[] rim, float scale, float yRim, float yKeel, Vector2 hullUV, Vector2 trimUV, Vector2 floorUV)
        {
            int n = rim.Length;
            var top = new Vector3[n];
            var keel = new Vector3[n];
            var inner = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 r = rim[i] * scale;
                top[i] = new Vector3(r.x, yRim, r.y);
                keel[i] = new Vector3(r.x * 0.5f, yKeel, r.y * 0.85f);
                inner[i] = new Vector3(r.x * 0.82f, yKeel + 0.18f, r.y * 0.9f);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 mid = (top[i] + top[j]) * 0.5f;
                Vector3 outward = new Vector3(mid.x, -0.3f, mid.z);
                mb.Quad(keel[i], keel[j], top[j], top[i], hullUV, outward);
                mb.Quad(inner[i], inner[j], top[j], top[i], floorUV, -new Vector3(mid.x, 0, mid.z));     // inside wall
                Vector3 trimA = top[i] + Vector3.up * 0.04f, trimB = top[j] + Vector3.up * 0.04f;
                mb.Quad(top[i], top[j], trimB, trimA, trimUV, outward);
                mb.Tri(Vector3.up * yKeel, keel[j], keel[i], hullUV, Vector3.down);
                mb.Tri(Vector3.up * (yKeel + 0.18f), inner[i], inner[j], floorUV, Vector3.up);
            }
        }

        public static void RowBoat(MeshBuilder mb, int hullColor, bool canopy)
        {
            Vector2 hull = S(hullColor), trim = S(Pal.SailWhite), wood = S(Pal.WoodLight), dark = S(Pal.WoodDark);
            Hull(mb, BoatRim, 1f, 0.45f, -0.18f, hull, trim, wood);
            mb.Box(new Vector3(0, 0.3f, -0.45f), new Vector3(1.25f, 0.05f, 0.32f), wood);
            mb.Box(new Vector3(0, 0.3f, 0.75f), new Vector3(1.1f, 0.05f, 0.3f), wood);
            mb.Limb(new Vector3(-0.7f, 0.42f, -0.2f), new Vector3(-1.9f, -0.05f, -0.6f), 0.025f, 0.025f, 4, dark);
            mb.Limb(new Vector3(0.7f, 0.42f, -0.2f), new Vector3(1.9f, -0.05f, -0.6f), 0.025f, 0.025f, 4, dark);
            if (!canopy) return;
            Vector2 can = S(Pal.Canopy);
            foreach (float x in new[] { -0.55f, 0.55f })
                foreach (float z in new[] { -1.45f, 1.0f })
                    mb.Limb(new Vector3(x, 0.4f, z), new Vector3(x, 1.75f, z), 0.025f, 0.025f, 4, trim);
            mb.Box(new Vector3(0, 1.78f, -0.22f), new Vector3(1.35f, 0.05f, 2.75f), can);
            mb.Box(new Vector3(0.68f, 1.68f, -0.22f), new Vector3(0.02f, 0.18f, 2.75f), trim);
            mb.Box(new Vector3(-0.68f, 1.68f, -0.22f), new Vector3(0.02f, 0.18f, 2.75f), trim);
        }

        public static void Yacht(MeshBuilder mb, int sailColor)
        {
            Vector2 white = S(Pal.SailWhite), stripe = S(Pal.BoatBlue), wood = S(Pal.WoodLight), mast = S(Pal.StoneGrey), sail = S(sailColor);
            Hull(mb, BoatRim, 1.3f, 0.5f, -0.3f, white, stripe, wood);
            mb.Limb(new Vector3(0, 0.3f, 0.5f), new Vector3(0, 7.2f, 0.5f), 0.06f, 0.04f, 5, mast);
            mb.Limb(new Vector3(0, 1.25f, 0.45f), new Vector3(0, 1.25f, -2.2f), 0.04f, 0.04f, 4, mast);
            Vector3 a = new Vector3(0, 1.32f, 0.42f), b = new Vector3(0, 7.0f, 0.42f), c = new Vector3(0, 1.32f, -2.15f);
            mb.Tri(a, b, c, sail, Vector3.right);
            mb.Tri(a, b, c, sail, Vector3.left);
            Vector3 j1 = new Vector3(0, 0.7f, 2.6f), j2 = new Vector3(0, 6.3f, 0.6f), j3 = new Vector3(0, 0.9f, 0.7f);
            mb.Tri(j1, j2, j3, white, Vector3.right);
            mb.Tri(j1, j2, j3, white, Vector3.left);
        }

        /// <summary>Wooden jetty along +Z from the origin: ramp down for the first 5 m then flat deck.</summary>
        public static void Jetty(MeshBuilder mb, MeshBuilder col, float length, float bankRise)
        {
            Vector2 wood = Atlas.MaterialUV(Atlas.MatWood), post = S(Pal.WoodDark);
            Rect woodR = Atlas.Material(Atlas.MatWood);
            float w = 3f, ramp = 5f;
            mb.Quad(new Vector3(-w / 2, bankRise, 0), new Vector3(w / 2, bankRise, 0), new Vector3(w / 2, 0, ramp), new Vector3(-w / 2, 0, ramp), woodR, Vector3.up);
            mb.Quad(new Vector3(-w / 2, 0, ramp), new Vector3(w / 2, 0, ramp), new Vector3(w / 2, 0, length), new Vector3(-w / 2, 0, length), woodR, Vector3.up);
            mb.Quad(new Vector3(-w / 2, -0.25f, ramp), new Vector3(-w / 2, -0.25f, length), new Vector3(-w / 2, 0, length), new Vector3(-w / 2, 0, ramp), wood, Vector3.left);
            mb.Quad(new Vector3(w / 2, -0.25f, length), new Vector3(w / 2, -0.25f, ramp), new Vector3(w / 2, 0, ramp), new Vector3(w / 2, 0, length), wood, Vector3.right);
            mb.Quad(new Vector3(-w / 2, -0.25f, length), new Vector3(w / 2, -0.25f, length), new Vector3(w / 2, 0, length), new Vector3(-w / 2, 0, length), wood, Vector3.forward);
            for (float z = ramp; z <= length + 0.01f; z += 3f)
                foreach (float x in new[] { -w / 2 + 0.1f, w / 2 - 0.1f })
                {
                    mb.Cylinder(new Vector3(x, -3f, z), 0.12f, 0.12f, 3.6f, 6, post, true);
                }
            // collider: ramp + deck as thin boxes
            Vector3 rampMid = new Vector3(0, bankRise * 0.5f - 0.15f, ramp * 0.5f);
            float rampLen = Mathf.Sqrt(ramp * ramp + bankRise * bankRise);
            col.Box(rampMid, new Vector3(w, 0.3f, rampLen), Quaternion.Euler(Mathf.Atan2(bankRise, ramp) * Mathf.Rad2Deg, 0, 0), Vector2.zero);
            col.Box(new Vector3(0, -0.15f, (ramp + length) * 0.5f), new Vector3(w, 0.3f, length - ramp), Vector2.zero);
        }

        /// <summary>Iron railing along a polyline (posts every ~2.5 m, two rails) + an invisible collision strip.</summary>
        public static void Railing(MeshBuilder mb, MeshBuilder col, List<Vector3> pts)
        {
            Vector2 iron = S(Pal.IronGreen);
            for (int i = 0; i < pts.Count; i++)
            {
                mb.Cylinder(pts[i], 0.045f, 0.04f, 1.05f, 5, iron, true);
                if (i == 0) continue;
                Vector3 a = pts[i - 1], b = pts[i];
                if ((b - a).sqrMagnitude > 25f) continue;   // gap (jetty)
                mb.Limb(a + Vector3.up * 1.0f, b + Vector3.up * 1.0f, 0.03f, 0.03f, 4, iron, false);
                mb.Limb(a + Vector3.up * 0.55f, b + Vector3.up * 0.55f, 0.022f, 0.022f, 4, iron, false);
                Vector3 side = Vector3.Cross(Vector3.up, b - a).normalized;
                col.Quad(a, b, b + Vector3.up * 1.1f, a + Vector3.up * 1.1f, Vector2.zero, side);
                col.Quad(b, a, a + Vector3.up * 1.1f, b + Vector3.up * 1.1f, Vector2.zero, -side);
            }
        }

        // ------------------------------------------------------------------ trees
        public static readonly float[] TrunkRadius = { 0.35f, 0.3f, 0.35f, 0.22f, 0.25f, 1.1f, 0.9f };
        public static readonly float[] TrunkHeight = { 3f, 8f, 4f, 2f, 3f, 1.4f, 1.2f };

        /// <summary>Weathered rock / boulder for steep Kumaon hillsides (types 5 and 6), smooth + jittered.</summary>
        public static Mesh Rock(int variant, int lod)
        {
            var mb = new MeshBuilder();
            Vector2 uv = Atlas.MaterialUV(variant == 0 ? Atlas.MatStone : Atlas.MatDarkStone);
            int seg = lod == 0 ? 12 : lod == 1 ? 7 : 4, rings = lod == 0 ? 8 : lod == 1 ? 5 : 3;
            if (variant == 0)
            {
                mb.Sphere(new Vector3(0, 0.35f, 0), new Vector3(1.5f, 0.95f, 1.2f), seg, rings, uv);
                if (lod < 2) mb.Sphere(new Vector3(0.8f, 0.2f, 0.5f), new Vector3(0.8f, 0.55f, 0.7f), seg - 2, rings - 1, uv);
            }
            else
            {
                mb.Sphere(new Vector3(0, 0.5f, 0), new Vector3(1.0f, 1.25f, 0.9f), seg, rings, uv);
                if (lod < 2) mb.Sphere(new Vector3(-0.6f, 0.25f, -0.3f), new Vector3(0.7f, 0.5f, 0.6f), seg - 2, rings - 1, uv);
            }
            return mb.ToMeshSmooth($"Rock{variant}_LOD{lod}", lod == 0 ? 0.32f : 0.25f, 91 + variant * 13 + lod);
        }

        /// <summary>Buransh (rhododendron), Uttarakhand's state tree, with red blossoms.</summary>
        public static Mesh Rhododendron(int lod)
        {
            var mb = new MeshBuilder();
            Vector2 trunk = S(Pal.Trunk), leaf = S(Pal.Oak), flower = Atlas.ExtraUV(Px.Bougainvillea), red = S(Pal.TempleRed);
            if (lod == 0)
            {
                mb.Cylinder(Vector3.zero, 0.28f, 0.16f, 3.2f, 6, trunk, false);
                mb.Limb(new Vector3(0, 2.6f, 0), new Vector3(1.1f, 4.2f, 0.3f), 0.12f, 0.07f, 5, trunk);
                mb.Limb(new Vector3(0, 2.4f, 0), new Vector3(-1f, 3.9f, -0.4f), 0.12f, 0.07f, 5, trunk);
                mb.Sphere(new Vector3(0, 4.9f, 0), new Vector3(2.4f, 1.7f, 2.3f), 8, 5, leaf);
                mb.Sphere(new Vector3(1.2f, 4.4f, 0.5f), new Vector3(1.4f, 1.1f, 1.3f), 7, 4, leaf);
                var rnd = new System.Random(4);
                for (int i = 0; i < 18; i++)
                {
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2f, e = (float)rnd.NextDouble() * 1.2f;
                    Vector3 p = new Vector3(Mathf.Cos(a) * 2.1f * Mathf.Cos(e * 0.6f), 4.9f + Mathf.Sin(e) * 1.4f, Mathf.Sin(a) * 2f * Mathf.Cos(e * 0.6f));
                    mb.Sphere(p, Vector3.one * 0.32f, 5, 3, i % 3 == 0 ? red : flower);
                }
            }
            else if (lod == 1)
            {
                mb.Cylinder(Vector3.zero, 0.25f, 0.18f, 3.4f, 4, trunk, false);
                mb.Sphere(new Vector3(0, 4.8f, 0), new Vector3(2.5f, 1.9f, 2.5f), 6, 4, flower);
            }
            else mb.Sphere(new Vector3(0, 4.6f, 0), new Vector3(2.5f, 2f, 2.5f), 4, 2, flower);
            return mb.ToMeshSmooth("Tree4_LOD" + lod, 0.1f, 40 + lod);
        }

        /// <summary>Concrete electricity pole with crossarm; returns the three wire attachment points (local).</summary>
        public static Vector3[] ElectricPole(MeshBuilder mb)
        {
            Vector2 conc = S(Pal.Concrete), dark = S(Pal.IronBlack);
            mb.Cylinder(Vector3.zero, 0.13f, 0.08f, 8.2f, 6, conc, true);
            mb.Box(new Vector3(0, 7.7f, 0), new Vector3(1.6f, 0.1f, 0.1f), dark);
            mb.Box(new Vector3(0.3f, 6.2f, 0.12f), new Vector3(0.35f, 0.45f, 0.25f), S(Pal.StoneGrey));   // transformer-ish box
            return new[] { new Vector3(-0.7f, 7.78f, 0), new Vector3(0, 7.78f, 0), new Vector3(0.7f, 7.78f, 0) };
        }

        /// <summary>LOD2: a handful of triangles for trees hundreds of metres away.</summary>
        public static Mesh TreeMeshFar(int type)
        {
            var mb = new MeshBuilder();
            switch (type)
            {
                case 0: mb.Cylinder(new Vector3(0, 1.5f, 0), 3.4f, 0f, 15f, 4, S(Pal.Deodar), false, true, 1f, 0.4f); break;
                case 1: mb.Cylinder(new Vector3(0, 7f, 0), 2.4f, 0f, 7f, 4, S(Pal.Pine), false, true); break;
                case 2: mb.Sphere(new Vector3(0, 6.5f, 0), new Vector3(3.4f, 3f, 3.4f), 4, 2, S(Pal.Oak)); break;
                default: mb.Cylinder(new Vector3(0, 0.6f, 0), 1.2f, 0f, 11f, 4, S(Pal.Cypress), false, true); break;
            }
            return mb.ToMeshSmooth($"Tree{type}_LOD2", 0.25f, type);
        }

        public static Mesh TreeMesh(int type, bool lod0)
        {
            var mb = new MeshBuilder();
            Vector2 trunk = S(Pal.Trunk);
            switch (type)
            {
                case 0: // Deodar (Himalayan cedar): drooping layered cone
                    if (lod0)
                    {
                        mb.Cylinder(Vector3.zero, 0.38f, 0.16f, 3.2f, 6, trunk, false);
                        for (int i = 0; i < 6; i++)
                        {
                            float y = 2.2f + i * 2.05f, r = 3.6f * (1f - i / 6.8f);
                            mb.Cylinder(new Vector3(0, y, 0), r, r * 0.18f, 3.1f, 8, S(i % 2 == 0 ? Pal.Deodar : Pal.DeodarLight), false, true, 1f, i * 0.4f);
                        }
                        mb.Cylinder(new Vector3(0, 14.4f, 0), 0.5f, 0f, 2.2f, 6, S(Pal.Deodar), false);
                    }
                    else
                    {
                        mb.Cylinder(Vector3.zero, 0.3f, 0.2f, 2.5f, 4, trunk, false);
                        mb.Cylinder(new Vector3(0, 2f, 0), 3.4f, 0f, 14.5f, 6, S(Pal.Deodar), false, true);
                    }
                    break;
                case 1: // Chir pine: tall bare trunk, open crown
                    if (lod0)
                    {
                        mb.Cylinder(Vector3.zero, 0.32f, 0.16f, 10f, 6, trunk, false);
                        var tufts = new System.Random(23);
                        for (int i = 0; i < 12; i++)
                        {
                            float a = (float)tufts.NextDouble() * Mathf.PI * 2f, rr = 0.4f + (float)tufts.NextDouble() * 2.0f;
                            Vector3 c = new Vector3(Mathf.Cos(a) * rr, 9.4f + (float)tufts.NextDouble() * 4.2f, Mathf.Sin(a) * rr);
                            mb.Limb(new Vector3(0, c.y - 0.6f, 0), c, 0.07f, 0.04f, 4, trunk, false);
                            mb.Sphere(c, new Vector3(1.0f + (float)tufts.NextDouble() * 0.5f, 0.55f + (float)tufts.NextDouble() * 0.25f, 1.0f + (float)tufts.NextDouble() * 0.5f), 6, 3, S(i % 3 == 0 ? Pal.Oak : Pal.Pine));
                        }
                    }
                    else
                    {
                        mb.Cylinder(Vector3.zero, 0.3f, 0.18f, 9.5f, 4, trunk, false);
                        mb.Sphere(new Vector3(0, 11f, 0), new Vector3(2.4f, 2.2f, 2.4f), 5, 3, S(Pal.Pine));
                    }
                    break;
                case 2: // Banj oak / broadleaf: round clustered crown
                    if (lod0)
                    {
                        mb.Cylinder(Vector3.zero, 0.38f, 0.22f, 4.2f, 6, trunk, false);
                        mb.Limb(new Vector3(0, 3.5f, 0), new Vector3(1.4f, 5.6f, 0.4f), 0.16f, 0.09f, 5, trunk);
                        mb.Limb(new Vector3(0, 3.2f, 0), new Vector3(-1.2f, 5.2f, -0.6f), 0.16f, 0.09f, 5, trunk);
                        mb.Limb(new Vector3(0, 3.8f, 0), new Vector3(0.3f, 6.2f, 1.5f), 0.14f, 0.07f, 5, trunk);
                        mb.Sphere(new Vector3(0, 6.7f, 0), new Vector3(2.3f, 1.8f, 2.3f), 7, 4, S(Pal.Oak));   // dense core
                        var crown = new System.Random(19);
                        int[] greens = { Pal.Oak, Pal.OakLight, Pal.Oak, Pal.Pine };
                        for (int i = 0; i < 18; i++)
                        {
                            float a = (float)crown.NextDouble() * Mathf.PI * 2f, el = (float)crown.NextDouble() * 1.2f - 0.35f;
                            var dir = new Vector3(Mathf.Cos(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(a) * Mathf.Cos(el));
                            Vector3 c = new Vector3(0, 6.6f, 0) + Vector3.Scale(dir, new Vector3(2.7f, 2.0f, 2.6f));
                            float r = 0.9f + (float)crown.NextDouble() * 0.75f;
                            mb.Sphere(c, new Vector3(r, r * 0.78f, r * (0.85f + (float)crown.NextDouble() * 0.3f)), 6, 4, S(greens[i % greens.Length]));
                        }
                    }
                    else
                    {
                        mb.Cylinder(Vector3.zero, 0.35f, 0.25f, 4.5f, 4, trunk, false);
                        mb.Sphere(new Vector3(0, 6.8f, 0), new Vector3(3.4f, 2.8f, 3.4f), 6, 4, S(Pal.Oak));
                    }
                    break;
                default: // Cypress: narrow tall cone
                    if (lod0)
                    {
                        mb.Cylinder(Vector3.zero, 0.24f, 0.14f, 1.6f, 6, trunk, false);
                        mb.Cylinder(new Vector3(0, 1.2f, 0), 1.25f, 0.9f, 3.5f, 8, S(Pal.Cypress), false);
                        mb.Cylinder(new Vector3(0, 4.7f, 0), 0.9f, 0f, 7.2f, 8, S(Pal.Cypress), false);
                    }
                    else
                    {
                        mb.Cylinder(new Vector3(0, 0.6f, 0), 1.2f, 0f, 11.2f, 5, S(Pal.Cypress), false, true);
                    }
                    break;
            }
            return mb.ToMeshSmooth($"Tree{type}_LOD{(lod0 ? 0 : 1)}", lod0 ? 0.12f : 0.2f, type * 7 + 1);
        }
    }
}
