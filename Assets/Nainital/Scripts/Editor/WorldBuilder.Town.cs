using System.Collections.Generic;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Town details from walking videos of Tallital, Bada Bazar, Mallital and the zoo: bazaar shop fronts with
    /// tarps and hanging goods, green road direction boards, the Naina Devi entrance arch, the zoo gate, its
    /// chain-link boundary and enclosures.</summary>
    public static partial class WorldBuilder
    {
        public const float TallitalShopRadius = 170f, BazaarShopRadius = 150f;
        /// <summary>Paved places built in code (chowk, bus yard): kept free of grass by FillGrassMask.</summary>
        static readonly List<(Vector2[] line, float half)> noGrassExtra = new List<(Vector2[], float)>();
        static readonly HashSet<long> noGrassCells = new HashSet<long>();
        public const float MallitalShopRadius = 180f;

        static void BuildTownDetails(WorldCtx ctx)
        {
            noGrassExtra.Clear();
            noGrassCells.Clear();
            var parent = new GameObject("Town details").transform;
            parent.SetParent(ctx.worldRoot, false);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var solid = new MeshBuilder();
            var solidCol = new MeshBuilder();

            int fronts = BazaarFronts(ctx, parent);

            // green direction boards at Tallital (texts as on the real boards)
            var tallital = ctx.Poi("New Bus Station");
            int boards = 0;
            if (tallital.HasValue)
            {
                if (RoadSignAt(ctx, parent, solid, solidCol, font, tallital.Value, 160f, r => r.kind == "trunk" || r.kind == "trunk_link",
                               new[] { "Haldwani  42", "Bhowali  13", "Raj Bhawan  2" })) boards++;
            }

            int gates = 0;   // (Naina Devi's entrance waits for recent photos)

            // the zoo: gate on the approach road, chain-link boundary, a few enclosures
            int fencePanels = BuildZoo(ctx, parent, solid, solidCol, font, ref gates);

            ctx.MeshObject("Town details (solid)", parent, solid, ctx.mats.atlas, solidCol);
            Debug.Log($"[Nainital] town details: {fronts} bazaar fronts, {boards} road boards, {gates} gates, {fencePanels} fence panels");
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>The Flats is a bare playing ground: no grass. Adds strips across the stadium area to the grass mask.</summary>
        static void AddFlatsToGrassMask(WorldCtx ctx, List<Vector2> pts, List<int> starts, List<float> half)
        {
            foreach (var (line, h) in noGrassExtra)
            {
                if (line.Length < 2) continue;
                starts.Add(pts.Count);
                pts.AddRange(line);
                half.Add(h);
            }
            foreach (var a in ctx.world.areas)
            {
                if (a.kind != "stadium") continue;
                var poly = Geo.ToPoints(a.pts);
                if (poly.Length < 3) continue;
                float zMin = float.MaxValue, zMax = float.MinValue;
                foreach (var p in poly) { zMin = Mathf.Min(zMin, p.y); zMax = Mathf.Max(zMax, p.y); }
                for (float z = zMin + 1f; z < zMax; z += 4f)
                {
                    var xs = new List<float>();
                    for (int i = 0; i < poly.Length; i++)
                    {
                        Vector2 p0 = poly[i], p1 = poly[(i + 1) % poly.Length];
                        if ((p0.y <= z) != (p1.y <= z)) xs.Add(p0.x + (z - p0.y) / (p1.y - p0.y) * (p1.x - p0.x));
                    }
                    xs.Sort();
                    for (int k = 0; k + 1 < xs.Count; k += 2)
                    {
                        starts.Add(pts.Count);
                        pts.Add(new Vector2(xs[k], z));
                        pts.Add(new Vector2(xs[k + 1], z));
                        half.Add(2.7f);
                    }
                }
            }
        }

        /// <summary>The nearest point on a motor road (LandmarkBuilder: shops by the small lakes).</summary>
        internal static bool NearestMotorRoad(WorldCtx ctx, Vector2 p, float within, out Vector2 best, out Vector2 tangent) =>
            NearestRoadPoint(ctx, p, within, r => VehicleKinds.Contains(r.kind) && !IsMallDeck(r.name), out best, out tangent);

        static bool NearestRoadPoint(WorldCtx ctx, Vector2 p, float within, System.Func<RoadData, bool> keep, out Vector2 best, out Vector2 tangent)
        {
            best = default;
            tangent = Vector2.right;
            float bd = within;
            bool found = false;
            foreach (var r in ctx.world.roads)
            {
                if (!keep(r)) continue;
                var line = Geo.ToPoints(r.pts);
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    Vector2 a = line[i], ab = line[i + 1] - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                    Vector2 q = a + ab * t;
                    float d = Vector2.Distance(p, q);
                    if (d < bd) { bd = d; best = q; tangent = ab.normalized; found = true; }
                }
            }
            return found;
        }

        static void Label(Transform parent, Font font, string text, Vector3 pos, Vector3 facing, float height, Color col)
        {
            if (!font) return;
            var t = new GameObject("Text");
            t.transform.SetParent(parent, false);
            t.transform.SetPositionAndRotation(pos + facing * 0.05f, Quaternion.LookRotation(-facing));
            var tm = t.AddComponent<TextMesh>();
            tm.font = font;
            t.GetComponent<MeshRenderer>().sharedMaterial = SignTextMaterial(font);
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = height / 6.4f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = col;
        }

        /// <summary>A green highway board on two poles beside the road, facing traffic.</summary>
        static bool RoadSignAt(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font, Vector2 near, float within,
                               System.Func<RoadData, bool> keep, string[] lines)
        {
            if (!NearestRoadPoint(ctx, near, within, keep, out var rp, out var tan)) return false;
            Vector2 side = new Vector2(tan.y, -tan.x);   // left side of the road (India drives on the left)
            Vector2 at = rp + side * 5.2f;
            if (ctx.InLake(at)) at = rp - side * 5.2f;
            float y = ctx.H(at);
            Vector3 face = Flat(-tan), right = Vector3.Cross(Vector3.up, face);
            var rot = Quaternion.LookRotation(face);
            float bw = 3.4f, bh = 0.35f + 0.55f * lines.Length;
            Vector3 c = V(at, y) + Vector3.up * (2.6f + bh * 0.5f);
            Vector2 grey = Atlas.SolidUV(Pal.StoneGrey);
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 pole = V(at, y) + right * (s * bw * 0.36f);
                mb.Box(pole + Vector3.up * (1.3f + bh * 0.5f), new Vector3(0.12f, 2.6f + bh, 0.12f), rot, grey);
                col.Box(pole + Vector3.up * 1.4f, new Vector3(0.25f, 2.8f, 0.25f), rot, Vector2.zero);
            }
            mb.Box(c - face * 0.02f, new Vector3(bw + 0.12f, bh + 0.12f, 0.04f), rot, Atlas.SolidUV(Pal.ShirtWhite));
            mb.Box(c, new Vector3(bw, bh, 0.05f), rot, Atlas.SolidUV(Pal.BoatGreen));
            var holder = new GameObject("Road board").transform;
            holder.SetParent(parent, false);
            for (int i = 0; i < lines.Length; i++)
            {
                float ly = bh * 0.5f - 0.175f - 0.55f * (i + 0.5f);
                Label(holder, font, lines[i], c + Vector3.up * ly + face * 0.03f, face, 0.36f, Color.white);
            }
            return true;
        }

        /// <summary>An entrance arch: two pillars, a curved top and a name board.</summary>
        static void Arch(WorldCtx ctx, Transform parent, MeshBuilder mb, MeshBuilder col, Font font, Vector2 at, Vector2 face2, float span,
                         string title, int colour, int textColour)
        {
            float y = Mathf.Min(ctx.H(at + new Vector2(face2.y, -face2.x) * span * 0.5f), ctx.H(at - new Vector2(face2.y, -face2.x) * span * 0.5f));
            Vector3 face = Flat(face2), right = Vector3.Cross(Vector3.up, face);
            var rot = Quaternion.LookRotation(face);
            Rect stone = Atlas.Material(Atlas.MatStone);
            Vector2 paint = Atlas.SolidUV(colour), cap = Atlas.MaterialUV(Atlas.MatConcrete), gold = Atlas.SolidUV(Pal.Saffron);
            const float H = 4.6f;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 p = V(at, y) + right * (s * (span * 0.5f + 0.45f));
                TexBox(mb, p + Vector3.up * (H * 0.5f - 0.3f), new Vector3(0.9f, H + 0.6f, 0.9f), rot, stone);
                mb.Box(p + Vector3.up * (H + 0.35f), new Vector3(1.1f, 0.18f, 1.1f), rot, cap);
                mb.Pyramid(p + Vector3.up * (H + 0.44f), 0.8f, 0.8f, 0.9f, paint);
                col.Box(p + Vector3.up * (H * 0.5f), new Vector3(0.9f, H, 0.9f), rot, Vector2.zero);
            }
            Vector3 last = default;
            for (int k = 0; k <= 12; k++)
            {
                float a = k / 12f * Mathf.PI;
                Vector3 q = V(at, y) + right * (Mathf.Cos(a) * (span * 0.5f + 0.45f)) + Vector3.up * (H + Mathf.Sin(a) * 1.1f);
                if (k > 0)
                {
                    Vector3 mid = (last + q) * 0.5f, d = q - last;
                    mb.Box(mid, new Vector3(d.magnitude + 0.05f, 0.45f, 0.7f), Quaternion.LookRotation(face, Vector3.Cross(face, d.normalized) * -1f) , paint);
                }
                last = q;
            }
            Vector3 board = V(at, y) + Vector3.up * (H + 0.35f) + face * 0.38f;
            mb.Box(board, new Vector3(span * 0.9f, 0.7f, 0.08f), rot, Atlas.SolidUV(textColour == Pal.Saffron ? Pal.TempleRed : Pal.BoatGreen));
            mb.Pyramid(V(at, y) + Vector3.up * (H + 1.5f), 0.7f, 0.7f, 0.8f, gold);
            var holder = new GameObject("Arch " + title).transform;
            holder.SetParent(parent, false);
            Label(holder, font, title, board + face * 0.05f, face, 0.42f, new Color(1f, 0.85f, 0.35f));
            Label(holder, font, title, board - face * 0.85f, -face, 0.42f, new Color(1f, 0.85f, 0.35f));
            mb.Box(board - face * 0.8f, new Vector3(span * 0.9f, 0.7f, 0.08f), rot, Atlas.SolidUV(textColour == Pal.Saffron ? Pal.TempleRed : Pal.BoatGreen));
        }

        // ------------------------------------------------------------------ bazaar fronts (Bada Bazar, Tallital)
        static int BazaarFronts(WorldCtx ctx, Transform parent)
        {
            var bazaar = ctx.Poi("Bada Bazar");
            var tallital = ctx.Poi("New Bus Station");
            var mallital = ctx.Poi("Nainital Stadium");
            var chunks = new MallChunks(ctx, parent, "Bazaar dressing", ctx.mats.atlas, true, 0.05f);
            var rnd = new System.Random(2718);
            int[] tarps = { Pal.BoatBlue, Pal.BoatBlue, Pal.BoatBlue, Pal.Canopy, Pal.Awning, Pal.JacketRed, Pal.BoatYellow };
            int[] goods = { Pal.SareeOrange, Pal.JacketBlue, Pal.ShirtWhite, Pal.SareePink, Pal.JacketYellow, Pal.Jeans, Pal.TempleRed, Pal.Saffron, Pal.BoatGreen };
            Vector2 pole = Atlas.SolidUV(Pal.IronBlack), wood = Atlas.MaterialUV(Atlas.MatWood), tube = Atlas.SolidUV(Pal.LampBulb);
            int done = 0;
            foreach (var (pos, n, width) in ctx.shopFronts)
            {
                var p2 = new Vector2(pos.x, pos.z);
                if (NearMallRoad(ctx, p2, 16f)) continue;   // Mall Road has its own dressing
                bool inBazaar = (bazaar.HasValue && (p2 - bazaar.Value).sqrMagnitude < BazaarShopRadius * BazaarShopRadius)
                             || (tallital.HasValue && (p2 - tallital.Value).sqrMagnitude < TallitalShopRadius * TallitalShopRadius)
                             || (mallital.HasValue && (p2 - mallital.Value).sqrMagnitude < MallitalShopRadius * MallitalShopRadius);
                if (!inBazaar) continue;
                Vector3 nrm = n.normalized, along = Vector3.Cross(Vector3.up, nrm).normalized;
                Vector3 floor = new Vector3(pos.x, pos.y - 2.75f, pos.z) - nrm * 0.62f;
                float w = width * 0.98f;
                var rot = Quaternion.LookRotation(nrm);
                var cell = chunks.At(p2);
                var mb = cell[0];

                // a tarp or tin sheet sloping out over the front, sagging a little, on two thin poles
                float reach = 1.4f + (float)rnd.NextDouble() * 0.9f;
                Vector3 topC = floor + Vector3.up * 2.35f, outC = floor + nrm * reach + Vector3.up * (1.95f + (float)rnd.NextDouble() * 0.2f);
                Vector3 half = along * (w * 0.5f + 0.15f);
                var tarp = new Rect(Atlas.SolidUV(tarps[rnd.Next(tarps.Length)]), Vector2.zero);
                Vector3 sag = Vector3.down * 0.12f;
                mb.QuadDouble(topC - half, topC + half, (topC + outC) * 0.5f + sag + half, (topC + outC) * 0.5f + sag - half, tarp, Vector3.up);
                mb.QuadDouble((topC + outC) * 0.5f + sag - half, (topC + outC) * 0.5f + sag + half, outC + half, outC - half, tarp, Vector3.up);
                if (Mathf.Abs(ctx.H(new Vector2(outC.x, outC.z)) - floor.y) < 1.2f)
                {
                    mb.Limb(outC - half * 0.95f, outC - half * 0.95f - Vector3.up * 2f, 0.025f, 0.025f, 4, pole, false);
                    mb.Limb(outC + half * 0.95f, outC + half * 0.95f - Vector3.up * 2f, 0.025f, 0.025f, 4, pole, false);
                }
                mb.Box(topC + nrm * 0.4f + Vector3.down * 0.14f, new Vector3(w * 0.75f, 0.05f, 0.08f), rot, tube);

                // goods hanging along the edge and down one side of the door
                int hang = Mathf.Clamp(Mathf.RoundToInt(w / 0.45f), 3, 14);
                for (int k = 0; k < hang; k++)
                {
                    if (rnd.NextDouble() < 0.35) continue;
                    float x = (k + 0.5f) / hang - 0.5f, cw = 0.06f + (float)rnd.NextDouble() * 0.09f, ch = 0.35f + (float)rnd.NextDouble() * 0.7f;
                    Vector3 c = outC - nrm * 0.15f + along * (x * w) + Vector3.down * 0.05f;
                    var uv = new Rect(Atlas.SolidUV(goods[rnd.Next(goods.Length)]), Vector2.zero);
                    mb.QuadDouble(c - along * cw + Vector3.down * ch, c + along * cw + Vector3.down * ch, c + along * cw, c - along * cw, uv, nrm);
                }
                float sd = rnd.Next(2) == 0 ? 1f : -1f;
                for (int k = 0; k < 4; k++)
                {
                    Vector3 c = floor + along * (sd * (w * 0.5f - 0.25f)) + nrm * 0.08f + Vector3.up * (2.1f - k * 0.5f);
                    var uv = new Rect(Atlas.SolidUV(goods[rnd.Next(goods.Length)]), Vector2.zero);
                    mb.QuadDouble(c - along * 0.18f + Vector3.down * 0.45f, c + along * 0.18f + Vector3.down * 0.45f, c + along * 0.18f, c - along * 0.18f, uv, nrm);
                }

                // motorbikes parked nose-in at the step of some shops
                if (rnd.NextDouble() < 0.45)
                {
                    int[] paints = { Pal.IronBlack, Pal.IronBlack, Pal.JacketRed, Pal.BoatBlue, Pal.StoneGrey, Pal.ShirtWhite };
                    int bikes = 1 + rnd.Next(3);
                    for (int k = 0; k < bikes; k++)
                    {
                        Vector3 bp = floor + nrm * (reach + 1.4f) + along * ((k - (bikes - 1) * 0.5f) * 0.95f);
                        float by = ctx.H(new Vector2(bp.x, bp.z));
                        if (Mathf.Abs(by - floor.y) > 1.5f) continue;
                        using (PropKit.At(mb, new Vector3(bp.x, by, bp.z), Quaternion.LookRotation(-nrm) * Quaternion.Euler(0, (float)(rnd.NextDouble() - 0.5) * 18f, 0)))
                            Motorbike(mb, paints[rnd.Next(paints.Length)]);
                    }
                }

                // crates / a low table of goods at the step, on most fronts
                if (rnd.NextDouble() < 0.75)
                {
                    Vector3 table = floor + nrm * 0.55f;
                    float tw = w * (0.4f + (float)rnd.NextDouble() * 0.3f);
                    float ty = Mathf.Max(ctx.H(new Vector2(table.x, table.z)), floor.y - 0.6f);
                    float th = floor.y + 0.75f - ty;
                    mb.Box(new Vector3(table.x, ty + th * 0.5f, table.z), new Vector3(tw, th, 0.7f), rot, wood);
                    int piles = Mathf.Clamp(Mathf.RoundToInt(tw / 0.32f), 2, 12);
                    for (int k = 0; k < piles; k++)
                    {
                        float x = ((k + 0.5f) / piles - 0.5f) * tw, h = 0.08f + (float)rnd.NextDouble() * 0.25f;
                        mb.Box(table + along * x + Vector3.up * (0.75f + h * 0.5f), new Vector3(0.28f, h, 0.26f), rot, Atlas.SolidUV(goods[rnd.Next(goods.Length)]));
                    }
                    cell[1].Box(new Vector3(table.x, ty + th * 0.5f, table.z), new Vector3(tw, th, 0.7f), rot, Vector2.zero);
                }
                done++;
            }
            chunks.Flush();
            return done;
        }

        // ------------------------------------------------------------------ the zoo
        static Ink ChainLinkPanel()
        {
            var k = new Ink(512, 256);
            k.Line(5f, new Vector2(0f, 0.97f), new Vector2(1f, 0.97f));
            k.Line(4f, new Vector2(0f, 0.04f), new Vector2(1f, 0.04f));
            for (float a = -0.6f; a < 1.6f; a += 1f / 22f)
            {
                k.Seg(new Vector2(a, 0f), new Vector2(a + 0.5f, 1f), 1.4f);
                k.Seg(new Vector2(a, 0f), new Vector2(a - 0.5f, 1f), 1.4f);
            }
            return k;
        }

        static int FenceLine(WorldCtx ctx, MallChunks panels, MeshBuilder posts, MeshBuilder col, List<Vector2> pts, float h, bool closed, Vector2 skip, float skipR)
        {
            int n = 0;
            Vector2 post = Atlas.SolidUV(Pal.IronBlack);
            int count = closed ? pts.Count : pts.Count - 1;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
                if ((a - skip).sqrMagnitude < skipR * skipR || (b - skip).sqrMagnitude < skipR * skipR || ctx.InLake(a)) continue;
                Vector3 a3 = V(a, ctx.H(a) - 0.05f), b3 = V(b, ctx.H(b) - 0.05f);
                Vector2 d = (b - a).normalized;
                Vector3 outward = Flat(new Vector2(d.y, -d.x));
                Panel(panels.At(a)[0], a3, b3, h, outward);
                posts.Cylinder(a3, 0.05f, 0.045f, h + 0.1f, 5, post, true);
                BlockBothWays(col, a3, b3, b3 + Vector3.up * h, a3 + Vector3.up * h, outward);
                n++;
            }
            return n;
        }

        static int BuildZoo(WorldCtx ctx, Transform parent, MeshBuilder solid, MeshBuilder solidCol, Font font, ref int gates)
        {
            Vector2[] zoo = null;
            foreach (var a in ctx.world.areas) if (a.kind == "zoo") { zoo = Geo.ToPoints(a.pts); break; }
            if (zoo == null || zoo.Length < 3) return 0;
            var chain = IronMaterial("ZooChainLink", ChainLinkPanel(), new Color(0.32f, 0.34f, 0.33f));
            var panels = new MallChunks(ctx, parent, "Zoo fence", chain, true, 0.12f);
            var posts = new MeshBuilder();
            var col = new MeshBuilder();
            Vector2 centre = Vector2.zero;
            foreach (var p in zoo) centre += p;
            centre /= zoo.Length;

            // the gate: at the real entrance when it is known (Google Maps), where the zoo road arrives; a paved approach
            // joins it to the road
            Vector2 gate = centre;
            var entrance = ctx.Poi("Zoo entrance");
            if (entrance.HasValue)
            {
                float bd = float.MaxValue;
                foreach (var q in Resample(zoo, 2f, true))
                    if ((q - entrance.Value).sqrMagnitude < bd) { bd = (q - entrance.Value).sqrMagnitude; gate = q; }
                if (NearestRoadPoint(ctx, gate, 300f, r => r.kind != "footway" && r.kind != "steps" && r.kind != "path" && !IsMallDeck(r.name), out var er, out _))
                {
                    Vector2 face = (er - gate).sqrMagnitude > 1f ? (er - gate).normalized : (gate - centre).normalized;
                    Arch(ctx, parent, solid, solidCol, font, gate, face, 5.5f, "PT. G.B. PANT HIGH ALTITUDE ZOO", Pal.BoatGreen, Pal.ShirtWhite);
                    gates++;
                    float len = Vector2.Distance(gate, er);
                    if (len > 3f)
                    {
                        var ap = new MeshBuilder();
                        Vector2 side = new Vector2(-face.y, face.x) * 3f;
                        int steps = Mathf.CeilToInt(len / 2f);
                        for (int k = 0; k < steps; k++)
                        {
                            Vector2 a = Vector2.Lerp(gate, er, k / (float)steps), b = Vector2.Lerp(gate, er, (k + 1f) / steps);
                            ap.Quad(V(a - side, ctx.H(a - side) + 0.1f), V(a + side, ctx.H(a + side) + 0.1f), V(b + side, ctx.H(b + side) + 0.1f), V(b - side, ctx.H(b - side) + 0.1f),
                                    new Rect(0f, k * 2f / 6f, 1f, 2f / 6f), Vector3.up);
                        }
                        ctx.MeshObject("Zoo gate approach", parent, ap, ctx.mats.asphalt, null, false, true);
                        noGrassExtra.Add((new[] { gate, er }, 3.5f));
                    }
                }
            }
            else if (NearestRoadPoint(ctx, centre, 400f, r => r.kind != "footway" && r.kind != "steps" && !IsMallDeck(r.name), out var rp, out _))
            {
                float bd = float.MaxValue;
                foreach (var q in Resample(zoo, 2f, true))
                    if ((q - rp).sqrMagnitude < bd) { bd = (q - rp).sqrMagnitude; gate = q; }
                Vector2 face = (rp - gate).sqrMagnitude > 1f ? (rp - gate).normalized : (gate - centre).normalized;
                Arch(ctx, parent, solid, solidCol, font, gate, face, 5.5f, "PT. G.B. PANT HIGH ALTITUDE ZOO", Pal.BoatGreen, Pal.ShirtWhite);
                gates++;
            }
            int n = FenceLine(ctx, panels, posts, col, Resample(zoo, 2.4f, true), 2.4f, true, gate, 4f);

            // enclosures inside, with a name board at each
            var rnd = new System.Random(6060);
            string[] names = { "Royal Bengal Tiger", "Leopard", "Himalayan Black Bear", "Sambar Deer", "Pheasants", "Red Panda" };
            var placed = new List<Vector2>();
            for (int tries = 0; tries < 400 && placed.Count < names.Length; tries++)
            {
                Vector2 c = centre + new Vector2((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * 140f;
                if (!Geo.PointInPolygon(zoo, c) || Geo.DistanceToPolyline(zoo, c, true) < 14f || (c - gate).sqrMagnitude < 400f) continue;
                bool clash = false;
                foreach (var q in placed) if ((q - c).sqrMagnitude < 26f * 26f) { clash = true; break; }
                if (clash || NearAnyRoad(ctx, c)) continue;
                float yaw = (float)rnd.NextDouble() * 180f, w = 16f + (float)rnd.NextDouble() * 6f, d = 11f + (float)rnd.NextDouble() * 4f;
                var q2 = Quaternion.Euler(0, yaw, 0);
                Vector3 r3 = q2 * Vector3.right * (w * 0.5f), f3 = q2 * Vector3.forward * (d * 0.5f);
                Vector2 R = new Vector2(r3.x, r3.z), F = new Vector2(f3.x, f3.z);
                var ring = Resample(new[] { c - R - F, c + R - F, c + R + F, c - R + F }, 2.4f, true);
                n += FenceLine(ctx, panels, posts, col, ring, 3f, true, new Vector2(1e6f, 1e6f), 0f);
                Vector2 b = c - F - F.normalized * 1.2f;
                var holder = new GameObject("Zoo board").transform;
                holder.SetParent(parent, false);
                Vector3 face = Flat(-F.normalized);
                Vector3 bc = V(b, ctx.H(b)) + Vector3.up * 1.5f;
                solid.Box(bc, new Vector3(1.9f, 0.55f, 0.06f), Quaternion.LookRotation(face), Atlas.SolidUV(Pal.BoatGreen));
                solid.Box(V(b, ctx.H(b)) + Vector3.up * 0.62f, new Vector3(0.08f, 1.25f, 0.08f), Quaternion.LookRotation(face), Atlas.SolidUV(Pal.IronBlack));
                Label(holder, font, names[placed.Count], bc + face * 0.04f, face, 0.2f, Color.white);
                placed.Add(c);
            }
            panels.Flush();
            ctx.MeshObject("Zoo fence posts", parent, posts, ctx.mats.atlas, col);
            return n;
        }
    }
}
