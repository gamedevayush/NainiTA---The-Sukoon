using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nainital
{
    /// <summary>
    /// Small flat-shaded ("mid-poly") mesh builder. Every primitive takes an outward hint so the
    /// triangle winding is always correct for Unity's clockwise front faces.
    /// </summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<int> T = new List<int>();
        public Matrix4x4 M = Matrix4x4.identity;

        public int VertexCount => V.Count;

        public void Clear()
        {
            V.Clear(); N.Clear(); UV.Clear(); T.Clear();
            M = Matrix4x4.identity;
        }

        Vector3 P(Vector3 p) => M.MultiplyPoint3x4(p);
        Vector3 D(Vector3 d) => M.MultiplyVector(d);

        // ------------------------------------------------------------------ triangles & quads
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 outward)
        {
            a = P(a); b = P(b); c = P(c); outward = D(outward);
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, outward) < 0f)
            {
                (b, c) = (c, b);
                (ub, uc) = (uc, ub);
                n = -n;
            }
            n.Normalize();
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            N.Add(n); N.Add(n); N.Add(n);
            UV.Add(ua); UV.Add(ub); UV.Add(uc);
            T.Add(i); T.Add(i + 1); T.Add(i + 2);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 uv, Vector3 outward) => Tri(a, b, c, uv, uv, uv, outward);

        /// <summary>Quad with corners bl, br, tr, tl (as seen from the outward side).</summary>
        public void Quad(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Rect uv, Vector3 outward)
        {
            bl = P(bl); br = P(br); tr = P(tr); tl = P(tl); outward = D(outward);
            Vector3 n = Vector3.Cross(tl - bl, tr - bl);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(tr - bl, br - bl);
            if (n.sqrMagnitude < 1e-12f) return;
            Vector2 ubl = new Vector2(uv.xMin, uv.yMin), ubr = new Vector2(uv.xMax, uv.yMin),
                    utr = new Vector2(uv.xMax, uv.yMax), utl = new Vector2(uv.xMin, uv.yMax);
            if (Vector3.Dot(n, outward) < 0f)
            {
                // mirror horizontally so the texture still reads correctly from the outside
                (bl, br) = (br, bl); (tl, tr) = (tr, tl);
                n = -n;
            }
            n.Normalize();
            int i = V.Count;
            V.Add(bl); V.Add(br); V.Add(tr); V.Add(tl);
            N.Add(n); N.Add(n); N.Add(n); N.Add(n);
            UV.Add(ubl); UV.Add(ubr); UV.Add(utr); UV.Add(utl);
            T.Add(i); T.Add(i + 3); T.Add(i + 2);
            T.Add(i); T.Add(i + 2); T.Add(i + 1);
        }

        public void Quad(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Vector2 uv, Vector3 outward) =>
            Quad(bl, br, tr, tl, new Rect(uv.x, uv.y, 0f, 0f), outward);

        /// <summary>Two-sided quad (e.g. sails, signs, roof overhangs).</summary>
        public void QuadDouble(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Rect uv, Vector3 outward)
        {
            Quad(bl, br, tr, tl, uv, outward);
            Quad(br, bl, tl, tr, uv, -outward);
        }

        // ------------------------------------------------------------------ solids
        public void Box(Vector3 center, Vector3 size, Vector2 uv) => Box(center, size, Quaternion.identity, uv);

        public void Box(Vector3 center, Vector3 size, Quaternion rot, Vector2 uv)
        {
            Vector3 h = size * 0.5f;
            Vector3 X = rot * Vector3.right * h.x, Y = rot * Vector3.up * h.y, Z = rot * Vector3.forward * h.z;
            Vector3 c = center;
            // +Z, -Z, +X, -X, +Y, -Y
            Quad(c + Z - X - Y, c + Z + X - Y, c + Z + X + Y, c + Z - X + Y, uv, Z);
            Quad(c - Z + X - Y, c - Z - X - Y, c - Z - X + Y, c - Z + X + Y, uv, -Z);
            Quad(c + X + Z - Y, c + X - Z - Y, c + X - Z + Y, c + X + Z + Y, uv, X);
            Quad(c - X - Z - Y, c - X + Z - Y, c - X + Z + Y, c - X - Z + Y, uv, -X);
            Quad(c + Y - X - Z, c + Y + X - Z, c + Y + X + Z, c + Y - X + Z, uv, Y);
            Quad(c - Y - X + Z, c - Y + X + Z, c - Y + X - Z, c - Y - X - Z, uv, -Y);
        }

        /// <summary>Upright (optionally tapered) cylinder; rTop = 0 gives a cone.</summary>
        public void Cylinder(Vector3 baseCenter, float rBottom, float rTop, float height, int seg, Vector2 uv,
                             bool capTop = true, bool capBottom = false, float squashZ = 1f, float startAngle = 0f)
        {
            Vector3 top = baseCenter + Vector3.up * height;
            for (int i = 0; i < seg; i++)
            {
                float a0 = startAngle + i * Mathf.PI * 2f / seg, a1 = startAngle + (i + 1) * Mathf.PI * 2f / seg;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0) * squashZ);
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1) * squashZ);
                Vector3 b0 = baseCenter + d0 * rBottom, b1 = baseCenter + d1 * rBottom;
                Vector3 t0 = top + d0 * rTop, t1 = top + d1 * rTop;
                Vector3 outward = (d0 + d1).normalized + Vector3.up * ((rBottom - rTop) / Mathf.Max(0.001f, height));
                if (rTop < 1e-4f) Tri(b0, b1, top, uv, outward);
                else Quad(b0, b1, t1, t0, uv, outward);
                if (capTop && rTop > 1e-4f) Tri(top, t0, t1, uv, Vector3.up);
                if (capBottom && rBottom > 1e-4f) Tri(baseCenter, b0, b1, uv, Vector3.down);
            }
        }

        /// <summary>Cylinder between two arbitrary points (limbs, posts, cables).</summary>
        public void Limb(Vector3 from, Vector3 to, float rFrom, float rTo, int seg, Vector2 uv, bool caps = true)
        {
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 1e-5f) return;
            Matrix4x4 saved = M;
            M = M * Matrix4x4.TRS(from, Quaternion.FromToRotation(Vector3.up, dir / len), Vector3.one);
            Cylinder(Vector3.zero, rFrom, rTo, len, seg, uv, caps, caps);
            M = saved;
        }

        public void Sphere(Vector3 center, Vector3 radii, int seg, int rings, Vector2 uv, float minLat = -90f, float maxLat = 90f)
        {
            for (int r = 0; r < rings; r++)
            {
                float lat0 = Mathf.Deg2Rad * Mathf.Lerp(minLat, maxLat, (float)r / rings);
                float lat1 = Mathf.Deg2Rad * Mathf.Lerp(minLat, maxLat, (float)(r + 1) / rings);
                for (int s = 0; s < seg; s++)
                {
                    float lo0 = s * Mathf.PI * 2f / seg, lo1 = (s + 1) * Mathf.PI * 2f / seg;
                    Vector3 p00 = SP(lat0, lo0), p01 = SP(lat0, lo1), p10 = SP(lat1, lo0), p11 = SP(lat1, lo1);
                    Vector3 o = ((p00 + p01 + p10 + p11) * 0.25f);
                    Vector3 a = center + Vector3.Scale(p00, radii), b = center + Vector3.Scale(p01, radii);
                    Vector3 c = center + Vector3.Scale(p11, radii), d = center + Vector3.Scale(p10, radii);
                    if (r == rings - 1 && Mathf.Approximately(maxLat, 90f)) Tri(a, b, c, uv, o);
                    else if (r == 0 && Mathf.Approximately(minLat, -90f)) Tri(a, c, d, uv, o);
                    else Quad(a, b, c, d, uv, o);
                }
            }
        }

        static Vector3 SP(float lat, float lon) =>
            new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));

        /// <summary>Gable roof prism: ridge along local X. Origin = centre of the eave rectangle.</summary>
        public void GableRoof(Vector3 center, float lengthX, float depthZ, float rise, Rect roofUV, Vector2 gableUV, float overhang = 0.35f)
        {
            float hx = lengthX * 0.5f + overhang, hz = depthZ * 0.5f + overhang;
            Vector3 c = center;
            Vector3 ridgeA = c + new Vector3(-hx, rise, 0), ridgeB = c + new Vector3(hx, rise, 0);
            Vector3 fl = c + new Vector3(-hx, 0, hz), fr = c + new Vector3(hx, 0, hz);
            Vector3 bl = c + new Vector3(-hx, 0, -hz), br = c + new Vector3(hx, 0, -hz);
            Vector3 nFront = new Vector3(0, hz, rise).normalized, nBack = new Vector3(0, hz, -rise).normalized;
            Quad(fr, fl, ridgeA, ridgeB, roofUV, nFront);
            Quad(bl, br, ridgeB, ridgeA, roofUV, nBack);
            // undersides of the overhang
            Quad(fl, fr, ridgeB, ridgeA, roofUV, -nFront);
            Quad(br, bl, ridgeA, ridgeB, roofUV, -nBack);
            // gable triangles (inset to wall line)
            float wx = lengthX * 0.5f, wz = depthZ * 0.5f;
            float riseAtWall = rise * (wz / hz);
            Tri(c + new Vector3(-wx, 0, wz), c + new Vector3(-wx, 0, -wz), c + new Vector3(-wx, riseAtWall, 0), gableUV, Vector3.left);
            Tri(c + new Vector3(wx, 0, -wz), c + new Vector3(wx, 0, wz), c + new Vector3(wx, riseAtWall, 0), gableUV, Vector3.right);
        }

        /// <summary>Pyramid / hip roof with a square or rectangular base.</summary>
        public void Pyramid(Vector3 baseCenter, float sx, float sz, float height, Vector2 uv)
        {
            Vector3 apex = baseCenter + Vector3.up * height;
            Vector3 a = baseCenter + new Vector3(-sx / 2, 0, -sz / 2), b = baseCenter + new Vector3(sx / 2, 0, -sz / 2);
            Vector3 c = baseCenter + new Vector3(sx / 2, 0, sz / 2), d = baseCenter + new Vector3(-sx / 2, 0, sz / 2);
            Tri(a, b, apex, uv, new Vector3(0, 0.5f, -1));
            Tri(b, c, apex, uv, new Vector3(1, 0.5f, 0));
            Tri(c, d, apex, uv, new Vector3(0, 0.5f, 1));
            Tri(d, a, apex, uv, new Vector3(-1, 0.5f, 0));
        }

        /// <summary>Append another builder's geometry (already in world space of that builder).</summary>
        public void Append(MeshBuilder other)
        {
            int off = V.Count;
            for (int i = 0; i < other.V.Count; i++)
            {
                V.Add(P(other.V[i]));
                N.Add(D(other.N[i]).normalized);
                UV.Add(other.UV[i]);
            }
            for (int i = 0; i < other.T.Count; i++) T.Add(other.T[i] + off);
        }

        /// <summary>Welds shared corners (same position + UV) and recalculates smooth normals: rounder, more organic shapes.</summary>
        public Mesh ToMeshSmooth(string name, float jitter = 0f, int seed = 0)
        {
            var map = new Dictionary<(int, int, int, int, int), int>();
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>(T.Count);
            var rnd = new System.Random(seed);
            for (int i = 0; i < T.Count; i++)
            {
                int vi = T[i];
                Vector3 p = V[vi];
                Vector2 uv = UV[vi];
                var key = (Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f),
                           Mathf.RoundToInt(uv.x * 4096f), Mathf.RoundToInt(uv.y * 4096f));
                if (!map.TryGetValue(key, out int ni))
                {
                    ni = verts.Count;
                    map[key] = ni;
                    if (jitter > 0f) p += new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * jitter;
                    verts.Add(p);
                    uvs.Add(uv);
                }
                tris.Add(ni);
            }
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(V);
            mesh.SetNormals(N);
            mesh.SetUVs(0, UV);
            mesh.SetTriangles(T, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
