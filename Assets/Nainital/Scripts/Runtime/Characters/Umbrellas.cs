using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Townsfolk under umbrellas in the rain (owner, 21 Nov 2026, FREEROAM #10). When WeatherSystem's rain passes ~0.2,
    /// about half the walking townsfolk near the camera (NpcWalker, not in a vehicle, not knocked down) open one: the right
    /// hand comes up to the chest (HandIK on the walker, a target in front of the right shoulder) and holds a shaft
    /// straight up with an eight-panel canopy over the head - black, maroon, navy or a striped one. A few seconds after the
    /// rain stops they fold away. Four shared meshes, no per-frame allocation; walkers far from the camera drop theirs.
    /// </summary>
    public class Umbrellas : MonoBehaviour
    {
        static Umbrellas inst;
        public static void Ensure() { if (!inst) inst = new GameObject("Umbrellas").AddComponent<Umbrellas>(); }

        class Held { public GameObject umbrella; public HandIK ik; public Transform target; public bool addedIk; }
        readonly Dictionary<NpcWalker, Held> held = new Dictionary<NpcWalker, Held>();
        readonly List<NpcWalker> drop = new List<NpcWalker>();
        static Mesh[] meshes;
        float timer, dryFor;
        bool raining;

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.5f;
            var ws = WeatherSystem.Instance;
            float rain = ws ? ws.Rain : 0f;
            if (rain > 0.22f) { raining = true; dryFor = 0f; }
            else if (rain < 0.12f) dryFor += 0.5f;
            if (raining && dryFor > 4f) raining = false;
            var cam = Camera.main;
            Vector3 c = cam ? cam.transform.position : Vector3.zero;

            // drop what no longer fits
            drop.Clear();
            foreach (var kv in held)
            {
                var w = kv.Key;
                if (!raining || !w || !w.isActiveAndEnabled || w.InCar || Down(w) || (w.transform.position - c).sqrMagnitude > 130f * 130f) drop.Add(w);
            }
            foreach (var w in drop) Fold(w);
            if (!raining) return;

            // and give one to about half the walkers near the camera
            foreach (var it in Interactable.All)
            {
                if (!(it is NpcWalker w) || held.ContainsKey(w)) continue;
                if (!w.isActiveAndEnabled || w.InCar || Down(w)) continue;
                if ((Mathf.Abs(w.GetInstanceID()) & 1) == 0) continue;
                if ((w.transform.position - c).sqrMagnitude > 90f * 90f) continue;
                Open(w);
            }
        }

        static bool Down(NpcWalker w)
        {
            var rig = w.GetComponentInChildren<HumanoidRig>();
            return !rig || rig.dead || rig.seated;
        }

        void Open(NpcWalker w)
        {
            if (meshes == null) meshes = new[] { Make(Pal.JacketBlack, Pal.JacketBlack), Make(Pal.BoatRed, Pal.BoatRed), Make(Pal.JacketBlue, Pal.JacketBlue), Make(Pal.BoatYellow, Pal.BoatBlue) };
            var h = new Held();
            h.target = new GameObject("Umbrella hand").transform;
            h.target.SetParent(w.transform, false);
            h.target.localPosition = new Vector3(0.2f, 1.2f, 0.26f);   // the right hand at the chest, a little forward
            h.ik = w.GetComponent<HandIK>();
            if (!h.ik) { h.ik = w.gameObject.AddComponent<HandIK>(); h.addedIk = true; }
            h.ik.right = h.target;
            h.ik.weight = 1f;
            var go = new GameObject("Umbrella");
            go.transform.SetParent(h.target, false);
            go.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = meshes[Mathf.Abs(w.GetInstanceID() / 2) % meshes.Length];
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GameAssets.AtlasMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            h.umbrella = go;
            held[w] = h;
        }

        void Fold(NpcWalker w)
        {
            if (held.TryGetValue(w, out var h))
            {
                if (h.umbrella) Destroy(h.umbrella);
                if (h.ik) { if (h.addedIk) Destroy(h.ik); else if (h.ik.right == h.target) h.ik.right = null; }
                if (h.target) Destroy(h.target.gameObject);
            }
            held.Remove(w);
        }

        /// <summary>An umbrella with its origin at the hand: the shaft up to an eight-panel canopy (both sides, so it reads
        /// from below), a curved handle under the hand. Panels alternate between two colours.</summary>
        static Mesh Make(int pa, int pb)
        {
            var mb = new MeshBuilder();
            Vector2 shaft = Atlas.SolidUV(Pal.IronBlack), a = Atlas.SolidUV(pa), b = Atlas.SolidUV(pb), wood = Atlas.SolidUV(Pal.WoodDark);
            mb.Cylinder(new Vector3(0f, -0.05f, 0f), 0.011f, 0.011f, 1.05f, 6, shaft, true);
            mb.Cylinder(new Vector3(0f, -0.2f, 0f), 0.02f, 0.02f, 0.16f, 6, wood, true);   // the grip
            mb.Limb(new Vector3(0f, -0.2f, 0f), new Vector3(0.05f, -0.26f, 0f), 0.018f, 0.018f, 5, wood, true);   // the J
            const int n = 8;
            const float R = 0.56f, apexY = 1.06f, rimY = 0.84f;
            Vector3 apex = new Vector3(0f, apexY, 0f);
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * R, rimY, Mathf.Sin(a0) * R), p1 = new Vector3(Mathf.Cos(a1) * R, rimY, Mathf.Sin(a1) * R);
                Vector3 mid = (p0 + p1) * 0.5f + Vector3.down * 0.03f;   // a little sag between the ribs
                Vector2 uv = i % 2 == 0 ? a : b;
                mb.Tri(apex, p1, mid, uv, Vector3.up); mb.Tri(apex, mid, p0, uv, Vector3.up);
                mb.Tri(apex, p1, mid, uv, Vector3.down); mb.Tri(apex, mid, p0, uv, Vector3.down);   // the underside
                mb.Limb(p0 + Vector3.up * 0.005f, apex, 0.006f, 0.006f, 3, shaft, false);   // the rib
            }
            return mb.ToMesh("Umbrella");
        }
    }
}
