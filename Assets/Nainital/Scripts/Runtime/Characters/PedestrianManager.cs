using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>Keeps a crowd of walkers on footpaths/sidewalks around the player; recycles far ones. The crowd is
    /// thickest where the town is busy (Mall Road, the lake promenade, the bazaars), and near shops some people stand
    /// browsing at the shop fronts (user, 2026-11-11: "people, market").</summary>
    public class PedestrianManager : MonoBehaviour
    {
        public RoadNetwork network;
        public Material material;
        public int maxPeds = 45;
        public float spawnMin = 18f, spawnMax = 120f, despawn = 160f;
        public Vector3[] browseSpots = new Vector3[0];    // where a shopper stands (ground), in front of a shop or stall
        public Vector3[] browseFacing = new Vector3[0];   // which way they look (at the goods)
        public Vector3[] homeSpots = new Vector3[0];      // house doors: where walkers go in at night
        public int maxBrowsers = 14;

        readonly List<NpcWalker> browsers = new List<NpcWalker>();
        float[] cumWeight;
        float totalWeight;

        readonly List<NpcWalker> active = new List<NpcWalker>();
        readonly Queue<NpcWalker> pool = new Queue<NpcWalker>();
        float timer;

        static readonly int[] Tops = { Pal.JacketRed, Pal.JacketBlue, Pal.JacketGreen, Pal.JacketYellow, Pal.JacketBlack, Pal.ShirtWhite, Pal.Shawl, Pal.SareeOrange, Pal.SareePink, Pal.CapWool };
        static readonly int[] LegsM = { Pal.Jeans, Pal.PantsKhaki, Pal.PantsBlack };
        static readonly int[] LegsF = { Pal.SareeOrange, Pal.SareePink, Pal.Shawl, Pal.JacketBlue, Pal.Jeans };
        static readonly int[] Skins = { Pal.SkinLight, Pal.SkinMid, Pal.SkinDark };
        static readonly string[] Names = { "Raahgir", "Tourist", "Local bhaiya", "College student", "Pahadi Dada", "Didi", "Porter (kuli)", "Shopkeeper", "Honeymoon couple", "School bachcha" };

        void Start()
        {
            if (Application.isMobilePlatform) { maxPeds = Mathf.Min(maxPeds, 18); maxBrowsers = Mathf.Min(maxBrowsers, 8); spawnMax = 95f; }
            Physics.IgnoreLayerCollision(9, 9, true);   // walkers pass by each other instead of locking up face to face
        }

        /// <summary>The door nearest to p (a house within reach, else anywhere ahead).</summary>
        Vector3 NearestHome(Vector3 p)
        {
            Vector3 best = p + Random.insideUnitSphere * 20f;
            float bd = float.MaxValue;
            for (int i = 0; i < homeSpots.Length; i++)
            {
                float d = (homeSpots[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = homeSpots[i]; }
            }
            best.y = p.y;
            return best;
        }

        /// <summary>Send walkers home until no more than keep are left out (the nearest house door each).</summary>
        void SendHome(List<NpcWalker> list, int keep, Vector3 c)
        {
            int out_ = 0;
            foreach (var w in list) if (w && !w.GoingHome) out_++;
            int extra = out_ - keep;
            for (int i = list.Count - 1; i >= 0 && extra > 0; i--)
            {
                var w = list[i];
                if (!w || w.GoingHome) continue;
                w.GoHome(NearestHome(w.transform.position));
                extra--;
            }
        }

        void Recycle(List<NpcWalker> list, Vector3 c)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var w = list[i];
                if (!w) { list.RemoveAt(i); continue; }
                if (w.Home || (w.transform.position - c).sqrMagnitude > despawn * despawn)
                {
                    w.idle = false;
                    w.ResetState();
                    w.gameObject.SetActive(false);
                    pool.Enqueue(w);
                    list.RemoveAt(i);
                }
            }
        }

        /// <summary>How busy each walkway is: the Mall's crowd lanes and the promenade far more than a hill path.</summary>
        void BuildWeights()
        {
            var w = network.walks;
            cumWeight = new float[w.Length];
            totalWeight = 0f;
            for (int i = 0; i < w.Length; i++)
            {
                string n = w[i].name ?? "";
                float k = n.Contains("crowd") ? 5f : n.Contains("promenade") ? 4f : n.Contains("Mall") ? 2.5f
                        : n.Contains("Bazar") || n.Contains("Bazaar") || n.Contains("Market") ? 3f : n.Contains("footpath") ? 1f : 0.6f;
                totalWeight += k;
                cumWeight[i] = totalWeight;
            }
        }

        int PickWalk()
        {
            if (cumWeight == null || cumWeight.Length != network.walks.Length) BuildWeights();
            float x = Random.value * totalWeight;
            int lo = 0, hi = cumWeight.Length - 1;
            while (lo < hi) { int mid = (lo + hi) / 2; if (cumWeight[mid] < x) lo = mid + 1; else hi = mid; }
            return lo;
        }

        NpcWalker Create()
        {
            bool female = Random.value < 0.45f;
            bool child = Random.value < 0.12f;
            var go = new GameObject("Pedestrian") { layer = 9 };
            go.transform.SetParent(transform, false);
            var cc = go.AddComponent<CharacterController>();
            float s = child ? 0.7f : 1f;
            cc.height = 1.8f * s;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.92f * s, 0);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 52f;
            go.AddComponent<CharacterMotor>();
            var rigGo = new GameObject("Rig") { layer = 9 };
            rigGo.transform.SetParent(go.transform, false);
            rigGo.SetActive(false);
            var rig = rigGo.AddComponent<HumanoidRig>();
            rig.skin = Skins[Random.Range(0, Skins.Length)];
            rig.hair = Random.value < 0.15f ? Pal.HairBrown : Pal.HairBlack;
            rig.top = Tops[Random.Range(0, Tops.Length)];
            rig.legs = female ? LegsF[Random.Range(0, LegsF.Length)] : LegsM[Random.Range(0, LegsM.Length)];
            rig.shoes = Random.value < 0.5f ? Pal.ShoeBrown : Pal.ShoeWhite;
            rig.female = female;
            rig.woolCap = Random.value < 0.25f;
            rig.shawl = female && Random.value < 0.4f;
            rig.scale = s;
            rig.material = material;
            rig.bodyType = child ? (female ? HumanoidRig.BodyType.Girl : HumanoidRig.BodyType.Boy)
                                 : (female ? HumanoidRig.BodyType.Woman : HumanoidRig.BodyType.Man);
            // rigged townspeople when the library is built: about 1 in 5 is elderly, 1 in 8 a child
            var lib = CharacterLibrary.Instance;
            var look = lib ? lib.Pick(female, child ? true : (bool?)false, child ? (bool?)null : Random.value < 0.22f) : null;
            if (look != null) CharacterLibrary.Apply(look, rig, cc);
            rigGo.SetActive(true);
            var w = go.AddComponent<NpcWalker>();
            w.npcName = look != null ? look.displayName : Names[Random.Range(0, Names.Length)];
            w.speed = HumanoidRig.WalkSpeedFor(rig.bodyType) * Random.Range(0.85f, 1.15f);
            return w;
        }

        void Update()
        {
            if (!network || network.walks == null || network.walks.Length == 0) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.4f;
            var p = PlayerController.Instance;
            var cam = Camera.main;
            if (!p || !cam) return;
            Vector3 c = p.transform.position;

            Recycle(active, c);
            float hour = TownClock.Hour;
            int target = Mathf.RoundToInt(maxPeds * TownClock.Crowd(hour));
            SendHome(active, target, c);
            int out_ = 0;
            foreach (var w in active) if (w && !w.GoingHome) out_++;
            for (int k = 0; k < 3 && out_ < target; k++) { if (TrySpawn(c, cam)) out_++; }
            UpdateBrowsers(c, cam, hour);
        }

        NpcWalker Take()
        {
            var w = pool.Count > 0 ? pool.Dequeue() : Create();
            w.ResetState();
            return w;
        }

        void UpdateBrowsers(Vector3 c, Camera cam, float hour)
        {
            Recycle(browsers, c);
            int keep = TownClock.ShopsOpen(hour) ? Mathf.RoundToInt(maxBrowsers * Mathf.Max(0.5f, TownClock.Crowd(hour))) : 0;
            SendHome(browsers, keep, c);
            int out_ = 0;
            foreach (var b in browsers) if (b && !b.GoingHome) out_++;
            if (browseSpots == null || browseSpots.Length == 0 || out_ >= keep) return;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int i = Random.Range(0, browseSpots.Length);
                Vector3 pos = browseSpots[i];
                float d = Vector3.Distance(pos, c);
                if (d < 12f || d > 75f) continue;
                Vector3 vp = cam.WorldToViewportPoint(pos);
                if (d < 45f && vp.z > 0 && vp.x > 0 && vp.x < 1 && vp.y > 0 && vp.y < 1) continue;
                bool taken = false;
                foreach (var b in browsers) if (b && (b.transform.position - pos).sqrMagnitude < 1.2f) { taken = true; break; }
                if (taken) continue;
                var w = Take();
                w.gameObject.SetActive(true);
                w.GetComponent<CharacterMotor>().Teleport(pos + Vector3.up * 0.1f);
                w.path = null;
                w.idle = true;
                w.faceDirection = i < browseFacing.Length ? browseFacing[i] : Vector3.forward;
                browsers.Add(w);
                return;
            }
        }

        bool TrySpawn(Vector3 c, Camera cam)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                var walk = network.walks[PickWalk()];
                if (walk.pts == null || walk.pts.Length < 3) continue;
                int i = Random.Range(0, walk.pts.Length);
                Vector3 pos = walk.pts[i];
                float d = Vector3.Distance(pos, c);
                if (d < spawnMin || d > spawnMax) continue;
                Vector3 vp = cam.WorldToViewportPoint(pos);
                if (d < 60f && vp.z > 0 && vp.x > 0 && vp.x < 1 && vp.y > 0 && vp.y < 1) continue;
                var w = Take();
                w.idle = false;
                w.gameObject.SetActive(true);
                w.GetComponent<CharacterMotor>().Teleport(pos + Vector3.up * 0.1f);
                w.path = walk.pts;
                w.index = Mathf.Clamp(i + 1, 0, walk.pts.Length - 1);
                w.direction = Random.value < 0.5f ? 1 : -1;
                active.Add(w);
                return true;
            }
            return false;
        }
    }
}
