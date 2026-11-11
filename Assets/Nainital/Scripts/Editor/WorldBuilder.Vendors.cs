using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    public static partial class WorldBuilder
    {
        /// <summary>Hand-cart vendors along the lake promenade, as on any evening in Nainital: roasted bhutta (corn) over
        /// a coal brazier, chai and momos under a small umbrella, balloons and toys. Each cart has its seller, who calls out
        /// when spoken to (user, 2026-11-11: "people, market").</summary>
        static void BuildVendors(WorldCtx ctx, Transform root)
        {
            if (ctx.promenadePath == null || ctx.promenadePath.Count < 40) return;
            var parent = new GameObject("Vendors (lake promenade)").transform;
            parent.SetParent(root, false);
            var rnd = new System.Random(717);
            string[][] lines =
            {
                new[] { "Garam garam bhutta! Nimbu masala laga ke doon?", "Koyle pe sika hai bhaiya, ek baar kha ke dekho.", "Pachees rupaye ka ek, do lo toh pachaas." },
                new[] { "Chai, adrak wali! Thand mein garam chai pi lo.", "Momos bhi hain, steam wale, laal chutney ke saath.", "Maggi bana doon? Pahadon pe Maggi ka maza alag hai." },
                new[] { "Gubbare lo, bachche ke liye!", "Ye seeti wala khilona dekho, sab le jaate hain.", "Jheel ke saath photo khinchwaoge? Mere bhai ka camera hai." },
            };
            int kinds = lines.Length, made = 0;
            var path = ctx.promenadePath;
            int step = Mathf.Max(10, path.Count / 9);
            for (int i = step / 2; i < path.Count - 2 && made < 8; i += step)
            {
                Vector3 a = path[i], b = path[Mathf.Min(i + 2, path.Count - 1)];
                Vector2 p2 = new Vector2(a.x, a.z);
                bool nearJetty = false;
                foreach (var j in ctx.jettyRoots) if ((j - p2).sqrMagnitude < 12f * 12f) { nearJetty = true; break; }
                if (nearJetty) continue;
                Vector3 along = b - a;
                along.y = 0f;
                if (along.sqrMagnitude < 0.01f) continue;
                along.Normalize();
                Vector2 toLake = (ctx.lakeCenter - p2).normalized;
                Vector3 side = new Vector3(-toLake.x, 0f, -toLake.y);   // the town side of the walk, back to the shops
                Vector3 at = a + side * 1.6f;
                Vector2 q = new Vector2(at.x, at.z);
                if (ctx.InLake(q)) continue;
                at.y = ctx.H(q);
                int kind = made % kinds;
                var mb = new MeshBuilder();
                Cart(mb, kind, rnd);
                var go = ctx.MeshObject(kind == 0 ? "Bhutta cart" : kind == 1 ? "Chai-momo cart" : "Balloon cart", parent, mb, ctx.mats.atlas);
                go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-side, Vector3.up));
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.5f, 0f);
                box.size = new Vector3(1.5f, 1f, 0.9f);
                // the seller stands behind the cart, facing the walkers
                Vector3 sp = at + side * 0.95f + along * 0.2f;
                sp.y = ctx.H(new Vector2(sp.x, sp.z)) + 0.05f;
                var npc = Local(ctx, parent, kind == 0 ? "Bhutte wale bhaiya" : kind == 1 ? "Chai wale" : "Gubbare wala", sp, -side, false, false, rnd);
                npc.GetComponent<NpcWalker>().lines = lines[kind];
                made++;
            }
            Debug.Log($"[Nainital] vendors: {made} carts on the promenade");
        }

        /// <summary>A hand cart, about 1.4 x 0.8 m, its long side facing +Z (the customer).</summary>
        static void Cart(MeshBuilder mb, int kind, System.Random rnd)
        {
            Vector2 wood = Atlas.SolidUV(Pal.WoodLight), dark = Atlas.SolidUV(Pal.WoodDark), iron = Atlas.SolidUV(Pal.IronBlack);
            mb.Box(new Vector3(0f, 0.78f, 0f), new Vector3(1.4f, 0.08f, 0.8f), wood);                // the board
            mb.Box(new Vector3(0f, 0.66f, 0f), new Vector3(1.36f, 0.16f, 0.76f), dark);              // its frame
            foreach (float x in new[] { -0.62f, 0.62f })
                foreach (float z in new[] { -0.32f, 0.32f })
                    mb.Box(new Vector3(x, 0.3f, z), new Vector3(0.05f, 0.6f, 0.05f), dark);          // legs
            var saved = mb.M;
            foreach (float z in new[] { -0.44f, 0.44f })                                             // two cycle wheels, upright
            {
                mb.M = saved * Matrix4x4.TRS(new Vector3(-0.3f, 0.3f, z), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
                mb.Cylinder(new Vector3(0f, -0.02f, 0f), 0.3f, 0.3f, 0.04f, 10, iron, true);
            }
            mb.M = saved;
            mb.Box(new Vector3(0.95f, 0.8f, 0f), new Vector3(0.5f, 0.03f, 0.03f), dark);              // the handle
            if (kind == 0)   // bhutta: a coal brazier and cobs
            {
                mb.Box(new Vector3(-0.3f, 0.9f, 0f), new Vector3(0.5f, 0.16f, 0.36f), iron);
                mb.Box(new Vector3(-0.3f, 0.985f, 0f), new Vector3(0.44f, 0.02f, 0.3f), Atlas.SolidUV(Pal.SareeOrange));   // glowing coals
                for (int k = 0; k < 9; k++)
                    mb.Box(new Vector3(0.2f + (k % 3) * 0.14f, 0.86f, -0.2f + (k / 3) * 0.16f), new Vector3(0.05f, 0.05f, 0.17f),
                           Atlas.SolidUV(k % 4 == 0 ? Pal.Gold : Pal.JacketYellow));
            }
            else if (kind == 1)   // chai and momos: kettle, steamer, glasses, an umbrella
            {
                mb.Cylinder(new Vector3(-0.35f, 0.82f, 0f), 0.2f, 0.2f, 0.34f, 10, Atlas.SolidUV(Pal.StoneGrey), true);    // steamer
                mb.Cylinder(new Vector3(-0.35f, 1.16f, 0f), 0.2f, 0.05f, 0.08f, 10, Atlas.SolidUV(Pal.StoneGrey), true);
                mb.Cylinder(new Vector3(0.15f, 0.82f, 0.1f), 0.11f, 0.08f, 0.2f, 8, Atlas.SolidUV(Pal.IronBlack), true);  // kettle
                for (int k = 0; k < 6; k++)
                    mb.Cylinder(new Vector3(0.35f + (k % 3) * 0.08f, 0.82f, -0.2f + (k / 3) * 0.08f), 0.025f, 0.03f, 0.08f, 6, Atlas.SolidUV(Pal.ShirtWhite), true);
                mb.Box(new Vector3(0.62f, 1.4f, -0.3f), new Vector3(0.03f, 1.25f, 0.03f), iron);                          // umbrella pole
                mb.Cylinder(new Vector3(0.62f, 2.0f, -0.3f), 0.95f, 0.05f, 0.35f, 12, Atlas.SolidUV(rnd.NextDouble() < 0.5 ? Pal.BoatRed : Pal.BoatBlue), true);
            }
            else   // balloons and toys: a pole with bunches of balloons
            {
                mb.Box(new Vector3(0f, 1.3f, 0f), new Vector3(0.04f, 1.05f, 0.04f), dark);
                int[] cols = { Pal.BoatRed, Pal.BoatBlue, Pal.BoatYellow, Pal.BoatGreen, Pal.SareePink, Pal.SareeOrange };
                for (int k = 0; k < 14; k++)
                {
                    float a = k * 2.4f, r = 0.18f + (k % 3) * 0.12f;
                    var c = new Vector3(Mathf.Cos(a) * r, 2.0f + (k % 4) * 0.16f, Mathf.Sin(a) * r);
                    mb.Sphere(c, new Vector3(0.13f, 0.16f, 0.13f), 7, 5, Atlas.SolidUV(cols[k % cols.Length]));
                }
                for (int k = 0; k < 5; k++)
                    mb.Box(new Vector3(-0.5f + k * 0.22f, 0.88f, 0.15f), new Vector3(0.14f, 0.12f, 0.14f), Atlas.SolidUV(cols[(k + 2) % cols.Length]));
            }
        }
    }
}
