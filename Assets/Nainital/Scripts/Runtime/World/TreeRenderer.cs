using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nainital
{
    /// <summary>
    /// Draws ~80k trees with Graphics.RenderMeshInstanced in a few big batches, with 2 LODs and
    /// frustum + distance culling per chunk. Hooks every camera (game, scene view, lake reflection
    /// probe) so the forest is visible in the Editor too. In play mode a small pool of trunk
    /// colliders follows the player.
    /// </summary>
    [ExecuteAlways]
    public class TreeRenderer : MonoBehaviour
    {
        public TreeField field;
        public Material material;
        public Material leafMaterial, barkMaterial, impostorMaterial;   // for the card trees (TreeField.cards)
        public float chunkSize = 100f;
        public float lod0Distance = 110f;
        public float lod1Distance = 420f;
        public float maxDistance = 1500f;

        class Chunk
        {
            public Bounds bounds;
            public Matrix4x4[][] byType;
        }

        class CamCache
        {
            public Vector3 pos = new Vector3(1e9f, 0, 0);
            public Quaternion rot;
            public Matrix4x4[][] lod0, lod1, lod2;
            public int[] n0, n1, n2;
        }

        Chunk[] chunks;
        int types;
        readonly Dictionary<int, CamCache> caches = new Dictionary<int, CamCache>();
        readonly Plane[] planes = new Plane[6];
        const int Batch = 1023;
        CapsuleCollider[] colliderPool;
        float colliderTimer;

        void OnEnable()
        {
            Init();
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            caches.Clear();
        }

        void Init()
        {
            if (!field || field.Count == 0 || field.lod0 == null) { chunks = null; return; }
            if (Application.isMobilePlatform) { maxDistance = Mathf.Min(maxDistance, 900f); lod0Distance = 80f; }
            types = field.lod0.Length;
            var map = new Dictionary<Vector2Int, List<Matrix4x4>[]>();
            var d = field.data;
            for (int i = 0; i < field.Count; i++)
            {
                Vector3 p = new Vector3(d[i * 6], d[i * 6 + 1], d[i * 6 + 2]);
                int t = Mathf.Clamp((int)d[i * 6 + 3], 0, types - 1);
                var key = new Vector2Int(Mathf.FloorToInt(p.x / chunkSize), Mathf.FloorToInt(p.z / chunkSize));
                if (!map.TryGetValue(key, out var lists))
                {
                    lists = new List<Matrix4x4>[types];
                    for (int k = 0; k < types; k++) lists[k] = new List<Matrix4x4>();
                    map[key] = lists;
                }
                lists[t].Add(Matrix4x4.TRS(p, Quaternion.Euler(0, d[i * 6 + 5], 0), Vector3.one * d[i * 6 + 4]));
            }
            var list = new List<Chunk>(map.Count);
            foreach (var kv in map)
            {
                var c = new Chunk { byType = new Matrix4x4[types][] };
                Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), max = -min;
                for (int k = 0; k < types; k++)
                {
                    c.byType[k] = kv.Value[k].ToArray();
                    foreach (var m in c.byType[k])
                    {
                        Vector3 pos = m.GetColumn(3);
                        min = Vector3.Min(min, pos);
                        max = Vector3.Max(max, pos + Vector3.up * 22f);
                    }
                }
                c.bounds = new Bounds((min + max) * 0.5f, max - min + Vector3.one * 8f);
                list.Add(c);
            }
            chunks = list.ToArray();
            caches.Clear();
        }

        static void Push(ref Matrix4x4[] buf, ref int count, Matrix4x4[] src)
        {
            if (count + src.Length > buf.Length) System.Array.Resize(ref buf, Mathf.NextPowerOfTwo(count + src.Length));
            System.Array.Copy(src, 0, buf, count, src.Length);
            count += src.Length;
        }

        CamCache Visible(Camera cam)
        {
            int id = cam.GetInstanceID();
            if (!caches.TryGetValue(id, out var cc))
            {
                cc = new CamCache { lod0 = new Matrix4x4[types][], lod1 = new Matrix4x4[types][], lod2 = new Matrix4x4[types][], n0 = new int[types], n1 = new int[types], n2 = new int[types] };
                for (int k = 0; k < types; k++) { cc.lod0[k] = new Matrix4x4[2048]; cc.lod1[k] = new Matrix4x4[4096]; cc.lod2[k] = new Matrix4x4[8192]; }
                caches[id] = cc;
            }
            Transform ct = cam.transform;
            bool reflection = cam.cameraType == CameraType.Reflection;
            if (!reflection && (ct.position - cc.pos).sqrMagnitude < 16f && Quaternion.Angle(ct.rotation, cc.rot) < 4f) return cc;
            cc.pos = ct.position;
            cc.rot = ct.rotation;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            for (int k = 0; k < types; k++) { cc.n0[k] = 0; cc.n1[k] = 0; cc.n2[k] = 0; }
            float max2 = maxDistance * maxDistance, l0 = lod0Distance * lod0Distance, l1 = lod1Distance * lod1Distance;
            bool hasLod2 = field.lod2 != null && field.lod2.Length == types;
            foreach (var c in chunks)
            {
                float d2 = c.bounds.SqrDistance(cc.pos);
                if (d2 > max2 || (!reflection && !GeometryUtility.TestPlanesAABB(planes, c.bounds))) continue;
                int lod = reflection ? 2 : d2 < l0 ? 0 : d2 < l1 ? 1 : 2;
                if (lod == 2 && !hasLod2) lod = 1;
                for (int k = 0; k < types; k++)
                {
                    if (c.byType[k].Length == 0) continue;
                    if (lod == 0) Push(ref cc.lod0[k], ref cc.n0[k], c.byType[k]);
                    else if (lod == 1) Push(ref cc.lod1[k], ref cc.n1[k], c.byType[k]);
                    else Push(ref cc.lod2[k], ref cc.n2[k], c.byType[k]);
                }
            }
            return cc;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (chunks == null || !material || cam.cameraType == CameraType.Preview) return;
            var cc = Visible(cam);
            var bounds = new Bounds(cam.transform.position, Vector3.one * maxDistance * 2f);
            var rpNear = new RenderParams(material) { camera = cam, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, worldBounds = bounds };
            var rpFar = new RenderParams(material) { camera = cam, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, worldBounds = bounds };
            var rpLeaf = leafMaterial ? new RenderParams(leafMaterial) { camera = cam, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, worldBounds = bounds } : rpNear;
            var rpBark = barkMaterial ? new RenderParams(barkMaterial) { camera = cam, shadowCastingMode = ShadowCastingMode.On, receiveShadows = true, worldBounds = bounds } : rpNear;
            var rpImp = impostorMaterial ? new RenderParams(impostorMaterial) { camera = cam, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, worldBounds = bounds } : rpFar;
            for (int k = 0; k < types; k++)
            {
                bool card = field.cards != null && k < field.cards.Length && field.cards[k];
                bool leaves = card && field.lod0[k].subMeshCount > 1;
                for (int s = 0; s < cc.n0[k]; s += Batch)
                {
                    Graphics.RenderMeshInstanced(card ? rpBark : rpNear, field.lod0[k], 0, cc.lod0[k], Mathf.Min(Batch, cc.n0[k] - s), s);
                    if (leaves) Graphics.RenderMeshInstanced(rpLeaf, field.lod0[k], 1, cc.lod0[k], Mathf.Min(Batch, cc.n0[k] - s), s);
                }
                for (int s = 0; s < cc.n1[k]; s += Batch)
                    Graphics.RenderMeshInstanced(card ? rpImp : rpFar, field.lod1[k], 0, cc.lod1[k], Mathf.Min(Batch, cc.n1[k] - s), s);
                for (int s = 0; s < cc.n2[k]; s += Batch)
                    Graphics.RenderMeshInstanced(card ? rpImp : rpFar, field.lod2[k], 0, cc.lod2[k], Mathf.Min(Batch, cc.n2[k] - s), s);
            }
        }

        void Update()
        {
            if (Application.isPlaying) UpdateColliders();
        }

        void UpdateColliders()
        {
            if (chunks == null) return;
            if (colliderPool == null)
            {
                colliderPool = new CapsuleCollider[48];
                for (int i = 0; i < colliderPool.Length; i++)
                {
                    var go = new GameObject("TreeCollider");
                    go.transform.SetParent(transform, false);
                    var c = go.AddComponent<CapsuleCollider>();
                    c.enabled = false;
                    colliderPool[i] = c;
                }
            }
            colliderTimer -= Time.deltaTime;
            var p = PlayerController.Instance;
            if (colliderTimer > 0f || !p) return;
            colliderTimer = 0.25f;
            Vector3 pp = p.transform.position;
            int used = 0;
            foreach (var c in chunks)
            {
                if (used >= colliderPool.Length) break;
                if (c.bounds.SqrDistance(pp) > 900f) continue;
                for (int k = 0; k < types && used < colliderPool.Length; k++)
                    foreach (var m in c.byType[k])
                    {
                        Vector3 tp = m.GetColumn(3);
                        if ((tp - pp).sqrMagnitude > 400f) continue;
                        float sc = m.lossyScale.y;
                        var col = colliderPool[used++];
                        col.transform.position = tp;
                        col.radius = field.trunkRadius[k] * sc;
                        col.height = field.trunkHeight[k] * sc;
                        col.center = Vector3.up * col.height * 0.5f;
                        col.enabled = true;
                        if (used >= colliderPool.Length) break;
                    }
            }
            for (int i = used; i < colliderPool.Length; i++) colliderPool[i].enabled = false;
        }
    }
}
