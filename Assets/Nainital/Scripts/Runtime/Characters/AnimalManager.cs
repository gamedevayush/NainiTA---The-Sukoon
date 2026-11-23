using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Street animals round the player (free-roam batch #8, 22 Nov 2026): cows on the roads at the town's edge and in
    /// Tallital / on the zoo road, dogs in the town's lanes and on Mall Road, cats at house doors and shop fronts.
    /// Pooled and phone-cheap: at most 10 near the player on a PC, 6 on a phone, spawned out of sight 30-95 m away,
    /// taken away again past ~135 m. The models are the rigged Tripo FBX in Assets/Nainital/Animals/Resources/Animals
    /// (cow / dog / cat + their _color textures, legacy clips: Tools/animals). A kind whose model is not there yet is
    /// simply not spawned.
    /// Cows stop the traffic: <see cref="CowAhead"/> is TrafficCar's check (one hook line there).
    /// </summary>
    public class AnimalManager : MonoBehaviour
    {
        public static AnimalManager Instance { get; private set; }

        /// <summary>Checks (AnimalTest.Report): touches that made one run, cat hops onto walls, cars that waited on a cow.</summary>
        public static int Touches, Hops, CowWaits, Spawned;

        public int maxTotal = 12;
        /// <summary>How many of the numbers below are used (owner, 24 Nov 2026: "animals count divide by 10 krdo"): 0.1 =
        /// a tenth - 12 -> 1 at a time on a PC, 6 -> 1 on a phone. Raise it for more animals.</summary>
        public float density = 0.1f;
        public int[] caps = { 4, 5, 3, 4 };   // cow, dog, cat, monkey
        public float spawnMin = 30f, spawnMax = 95f, despawn = 135f;
        /// <summary>A turn about Y for a model that comes in facing the wrong way (set after a look in Play).</summary>
        // 180: rig_animal.py faces the animals -Y in Blender, which lands on -Z in Unity (AnimalImportCheck, 24 Nov 2026)
        public float[] modelYaw = { 180f, 180f, 180f, 180f };

        static readonly string[] Names = { "cow", "dog", "cat", "monkey" };
        const int Kinds = 4;
        // coat colours: the textures are a light coat, tinted darker (multiply) for variety
        static readonly Color[][] Coats =
        {
            new[] { Color.white, new Color(0.82f, 0.64f, 0.45f), new Color(0.5f, 0.36f, 0.26f), new Color(0.3f, 0.28f, 0.27f), new Color(0.72f, 0.72f, 0.7f) },
            new[] { Color.white, new Color(0.85f, 0.7f, 0.52f), new Color(0.6f, 0.45f, 0.32f), new Color(0.26f, 0.24f, 0.23f) },
            new[] { Color.white, new Color(1f, 0.82f, 0.6f), new Color(0.72f, 0.72f, 0.76f), new Color(0.4f, 0.38f, 0.37f) },
            new[] { Color.white, new Color(0.9f, 0.85f, 0.78f), new Color(0.78f, 0.74f, 0.7f) },
        };
        static readonly Vector2 Tallital = new Vector2(760f, -1130f);   // the bus stand / lake end (local x, z)
        // where the monkey troops live (world.json pois): Naina Devi temple, Snow View, Tiffin Top - x, z, radius, weight
        static readonly Vector4[] MonkeyHaunts =
        {
            new Vector4(-56f, -154f, 160f, 3f), new Vector4(706f, 582f, 220f, 3f), new Vector4(-431f, -762f, 200f, 2f),
        };

        class Spot
        {
            public Vector3 p, along;
            public float half;   // half the road's width (0: a point, a door, a shop front)
            public float cow, dog, cat, monkey;
            public bool eatery;   // by a restaurant's tables: its dogs sit there, waiting to be fed
        }

        readonly List<Spot> spots = new List<Spot>();
        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        const float Cell = 64f;
        bool built;
        readonly GameObject[] prefabs = new GameObject[Kinds];
        readonly Material[] mats = new Material[Kinds];
        readonly List<Animal>[] active = { new List<Animal>(), new List<Animal>(), new List<Animal>(), new List<Animal>() };
        readonly Queue<Animal>[] pool = { new Queue<Animal>(), new Queue<Animal>(), new Queue<Animal>(), new Queue<Animal>() };
        readonly List<int> ring = new List<int>();
        readonly List<Animal> dead = new List<Animal>();
        float timer, buildTry;
        MaterialPropertyBlock mpb;
        static bool warned;

        /// <summary>One AnimalManager (GameManager.Awake).</summary>
        public static AnimalManager Ensure()
        {
            if (Instance) return Instance;
            return new GameObject("Animals").AddComponent<AnimalManager>();
        }

        void Awake()
        {
            Instance = this;
            GameEvents.Horn += OnHorn;
        }

        void OnDestroy()
        {
            GameEvents.Horn -= OnHorn;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            if (Application.isMobilePlatform) { maxTotal = 6; caps = new[] { 2, 3, 2, 2 }; spawnMax = 75f; despawn = 110f; }
            maxTotal = Mathf.Max(1, Mathf.RoundToInt(maxTotal * density));
            for (int k = 0; k < caps.Length; k++) caps[k] = Mathf.Max(1, Mathf.RoundToInt(caps[k] * density));
            var missing = new List<string>();
            for (int k = 0; k < Kinds; k++)
            {
                prefabs[k] = Resources.Load<GameObject>("Animals/" + Names[k]);
                if (!prefabs[k]) missing.Add(Names[k]);
            }
            if (missing.Count > 0 && !warned)
            {
                warned = true;
                Debug.Log("[Nainital] animals: no model yet for " + string.Join(", ", missing) + " (Assets/Nainital/Animals/Resources/Animals) - those are not spawned");
            }
        }

        // ------------------------------------------------------------------ where they live
        static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;
        static long KeyOf(Vector3 p) => Key(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));

        /// <summary>Spots every ~12 m along the roads and walkways (weighted per kind by how built-up the place is and its
        /// name), the house doors and the shop fronts (cats). Once, when the road network is there.</summary>
        bool Build()
        {
            var pm = PedestrianManager.Instance;
            RoadNetwork net = pm && pm.network ? pm.network : RoadRoute.Net;
            if (!net || net.roads == null) return false;
            Vector3[] homes = pm ? pm.homeSpots : new Vector3[0];
            Vector3[] shops = pm ? pm.browseSpots : new Vector3[0];

            // houses per 45 m (how built-up a spot is)
            var hgrid = new Dictionary<long, List<Vector3>>();
            const float HC = 45f;
            foreach (var h in homes)
            {
                long k = Key(Mathf.FloorToInt(h.x / HC), Mathf.FloorToInt(h.z / HC));
                if (!hgrid.TryGetValue(k, out var l)) hgrid[k] = l = new List<Vector3>();
                l.Add(h);
            }
            int Density(Vector3 p)
            {
                int cx = Mathf.FloorToInt(p.x / HC), cz = Mathf.FloorToInt(p.z / HC), n = 0;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (hgrid.TryGetValue(Key(cx + dx, cz + dz), out var l))
                            foreach (var h in l)
                                if ((h.x - p.x) * (h.x - p.x) + (h.z - p.z) * (h.z - p.z) < HC * HC) n++;
                return n;
            }

            spots.Clear();
            grid.Clear();
            foreach (var r in net.roads)
            {
                if (r?.pts == null || r.pts.Length < 2) continue;
                string n = r.name ?? "";
                bool mall = n.Contains("Mall"), zoo = n.Contains("Zoo");
                for (int i = 1; i < r.pts.Length - 1; i += 3)
                {
                    Vector3 p = r.pts[i];
                    int d = Density(p);
                    float cow = d == 0 ? 0.5f : d <= 12 ? 1.6f : 0.25f;
                    if (zoo) cow += 3f;
                    if (Vector2.Distance(new Vector2(p.x, p.z), Tallital) < 450f) cow += 2f;
                    if (mall) cow *= 0.25f;
                    float dog = mall ? 1.5f : r.width <= 5f && d >= 3 ? 1f : 0.25f;
                    Add(new Spot { p = p, along = (r.pts[i + 1] - r.pts[i - 1]).normalized, half = r.width * 0.5f, cow = cow, dog = dog, cat = 0f,
                                   monkey = Haunt(p) + (d <= 2 ? 0.3f : 0f) });
                }
            }
            if (net.walks != null)
                foreach (var w in net.walks)
                {
                    if (w?.pts == null || w.pts.Length < 2) continue;
                    string n = w.name ?? "";
                    for (int i = 1; i < w.pts.Length - 1; i += 3)
                    {
                        Vector3 p = w.pts[i];
                        int d = Density(p);
                        float dog = d < 3 ? 0.3f : n.Contains("crowd") ? 0.8f : n.Contains("Mall") ? 2.5f : 1f;
                        Add(new Spot { p = p, along = (w.pts[i + 1] - w.pts[i - 1]).normalized, half = Mathf.Max(0.5f, w.width * 0.5f), dog = dog, cat = 0.15f,
                                       monkey = Haunt(p) + (n.Contains("Mall") ? 0.3f : 0f) });
                    }
                }
            foreach (var h in homes) Add(new Spot { p = h, along = Vector3.forward, cat = 1f, dog = 0.1f });
            foreach (var s in shops) Add(new Spot { p = s, along = Vector3.forward, cat = 1.5f, dog = 0.3f, monkey = 0.2f + Haunt(s) * 0.5f });
            // dogs mostly by the restaurants' tables, by the customers (owner, 24 Nov 2026: "mostly kutte restaurant ke seat ke
            // pass customer ke pass bethe honge..like they feed them"): a spot 1 m out from every seat, weighted well above a road
            int eat = 0;
            foreach (var seat in FindObjectsByType<RestaurantSeat>(FindObjectsSortMode.None))
            {
                Vector3 out1 = seat.table ? (seat.transform.position - seat.table.position) : -seat.transform.forward;
                out1.y = 0f;
                out1 = out1.sqrMagnitude > 1e-4f ? out1.normalized : Vector3.forward;
                Add(new Spot { p = seat.transform.position + out1 * 0.9f, along = out1, dog = 6f, cat = 0.4f, eatery = true });
                eat++;
            }

            Debug.Log("[Nainital] animals: " + spots.Count + " spots (" + homes.Length + " doors, " + shops.Length + " shop fronts, " + eat + " restaurant seats)");
            return true;
        }

        /// <summary>How much a place is monkey country: the troops' haunts, fading out to their edge.</summary>
        static float Haunt(Vector3 p)
        {
            float w = 0f;
            foreach (var h in MonkeyHaunts)
            {
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(h.x, h.y));
                if (d < h.z) w += h.w * (1f - d / h.z);
            }
            return w;
        }

        void Add(Spot s)
        {
            spots.Add(s);
            long k = KeyOf(s.p);
            if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
            l.Add(spots.Count - 1);
        }

        // ------------------------------------------------------------------ spawn / recycle
        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 0.5f;
            var p = PlayerController.Instance;
            var cam = Camera.main;
            if (!p || !cam) return;
            if (!built)
            {
                if (Time.time < buildTry) return;
                buildTry = Time.time + 3f;
                built = Build();
                if (!built) return;
            }
            Vector3 c = p.transform.position;
            int total = 0;
            for (int k = 0; k < Kinds; k++)
            {
                var list = active[k];
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var a = list[i];
                    if (!a) { list.RemoveAt(i); continue; }
                    if (a.Dead) { list.RemoveAt(i); dead.Add(a); continue; }   // a body: off the count, cleared below
                    float d = Vector3.Distance(a.transform.position, c);
                    if (d > despawn * 1.4f || (d > despawn && !Visible(cam, a.transform.position)) || a.transform.position.y < c.y - 300f)
                    {
                        a.gameObject.SetActive(false);
                        pool[k].Enqueue(a);
                        list.RemoveAt(i);
                    }
                }
                total += list.Count;
            }
            // the dead lie where they fell until out of sight (or 3 min), then go (never back to the pool: the bones carry
            // rigid bodies now)
            for (int i = dead.Count - 1; i >= 0; i--)
            {
                var a = dead[i];
                if (!a) { dead.RemoveAt(i); continue; }
                var hp = a.Corpse && a.Corpse.Hips ? a.Corpse.Hips.position : a.transform.position;
                float age = a.Corpse ? Time.time - a.Corpse.DiedAt : 999f;
                if (age > 180f || (age > 20f && !Visible(cam, hp)) || Vector3.Distance(hp, c) > despawn * 1.4f)
                {
                    Destroy(a.gameObject);
                    dead.RemoveAt(i);
                }
            }
            if (total >= maxTotal) return;
            TrySpawn(c, cam, total);
        }

        static bool Visible(Camera cam, Vector3 pos)
        {
            Vector3 vp = cam.WorldToViewportPoint(pos);
            return vp.z > 0 && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
        }

        void TrySpawn(Vector3 c, Camera cam, int total)
        {
            // the spots in the ring round the player
            ring.Clear();
            int r = Mathf.CeilToInt(spawnMax / Cell);
            int cx = Mathf.FloorToInt(c.x / Cell), cz = Mathf.FloorToInt(c.z / Cell);
            var avail = new float[Kinds];
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    if (!grid.TryGetValue(Key(cx + dx, cz + dz), out var l)) continue;
                    foreach (int i in l)
                    {
                        var s = spots[i];
                        float d = Vector3.Distance(s.p, c);
                        if (d < spawnMin || d > spawnMax || Mathf.Abs(s.p.y - c.y) > 40f) continue;
                        ring.Add(i);
                        avail[0] += s.cow; avail[1] += s.dog; avail[2] += s.cat; avail[3] += s.monkey;
                    }
                }
            if (ring.Count == 0) return;
            float hour = TownClock.Hour;
            bool night = hour < 5.5f || hour > 21.5f;
            // which kind: the one furthest under its share where this part of town has places for it
            var want = new float[Kinds];
            float sum = 0f;
            for (int k = 0; k < Kinds; k++)
            {
                if (!prefabs[k] || active[k].Count >= caps[k] || avail[k] < 0.5f) continue;
                want[k] = (caps[k] - active[k].Count) * Mathf.Clamp01(avail[k] / 4f) * (k == 0 && night ? 0.6f : k == 3 && night ? 0.1f : 1f);   // monkeys sleep at night
                sum += want[k];
            }
            if (sum <= 0f) return;
            float x = Random.value * sum;
            int kind = 0;
            for (; kind < Kinds - 1; kind++) { if (x < want[kind]) break; x -= want[kind]; }

            for (int attempt = 0; attempt < 8; attempt++)
            {
                var s = PickSpot(kind, avail[kind]);
                if (s == null) return;
                Vector3 pos = s.p;
                if (kind == 0 && s.half > 0f)   // a cow: anywhere across the road, often right in the lane
                    pos += Vector3.Cross(Vector3.up, s.along).normalized * Random.Range(-s.half + 0.5f, s.half - 0.5f);
                else if ((kind == 2 || kind == 3) && s.half == 0f)   // a cat / monkey by a door / shop: a step to one side
                {
                    Vector2 o = Random.insideUnitCircle.normalized * Random.Range(0.6f, 1.4f);
                    pos += new Vector3(o.x, 0f, o.y);
                }
                if (Vector3.Distance(pos, c) < 45f && Visible(cam, pos)) continue;
                if (!Ground(pos, out pos)) continue;
                if (TooClose(pos, kind == 1 || kind == 3 ? 3f : 7f)) continue;
                var a = Spawn((AnimalKind)kind, pos, s);
                // street dogs come in twos and threes
                if (a && kind == 1 && Random.value < 0.35f && active[1].Count < caps[1] && total + 1 < maxTotal)
                {
                    Vector2 o = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3f);
                    if (Ground(pos + new Vector3(o.x, 0f, o.y), out var q)) Spawn(AnimalKind.Dog, q, s);
                }
                // monkeys come in troops of two to four
                if (a && kind == 3)
                    for (int m = Random.Range(1, 4); m > 0 && active[3].Count < caps[3] && total + active[3].Count < maxTotal; m--)
                    {
                        Vector2 o = Random.insideUnitCircle.normalized * Random.Range(1f, 3.5f);
                        if (Ground(pos + new Vector3(o.x, 0f, o.y), out var q)) Spawn(AnimalKind.Monkey, q, s);
                    }
                return;
            }
        }

        Spot PickSpot(int kind, float totalW)
        {
            float x = Random.value * totalW;
            foreach (int i in ring)
            {
                var s = spots[i];
                float w = kind == 0 ? s.cow : kind == 1 ? s.dog : kind == 2 ? s.cat : s.monkey;
                if (w <= 0f) continue;
                if (x < w) return s;
                x -= w;
            }
            return null;
        }

        static int groundMask = -1;

        /// <summary>The ground (terrain, a road deck, steps, a wall top) under p, not the lake.</summary>
        static bool Ground(Vector3 p, out Vector3 g)
        {
            if (groundMask < 0) groundMask = ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer));
            g = p;
            if (!Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var h, 9f, groundMask, QueryTriggerInteraction.Ignore)) return false;
            if (h.normal.y < 0.7f || h.point.y < LakeArea.WaterY + 0.2f) return false;
            g = h.point;
            return true;
        }

        bool TooClose(Vector3 p, float d)
        {
            foreach (var list in active)
                foreach (var a in list)
                    if (a && (a.transform.position - p).sqrMagnitude < d * d) return true;
            return false;
        }

        Animal Spawn(AnimalKind kind, Vector3 pos, Spot s)
        {
            int k = (int)kind;
            Animal a = null;
            while (pool[k].Count > 0 && !a) a = pool[k].Dequeue();
            if (!a) a = Create(kind);
            if (!a) return null;
            var motor = a.GetComponent<CharacterMotor>();
            if (motor.Controller) motor.Controller.enabled = true;
            a.gameObject.SetActive(true);
            motor.Teleport(pos + Vector3.up * 0.05f);
            Vector3 f = kind == AnimalKind.Cow && s.half > 0f && Random.value < 0.6f ? s.along * (Random.value < 0.5f ? 1f : -1f)
                      : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.01f) a.transform.rotation = Quaternion.LookRotation(f.normalized);
            Tint(a, kind);
            a.beggar = kind == AnimalKind.Dog && s.eatery;
            // a cow keeps to its stretch of road, a dog its lane, a cat its doorstep
            a.Begin(kind, pos, a.beggar ? 1.5f : kind == AnimalKind.Cow ? 10f : kind == AnimalKind.Dog ? 8f : kind == AnimalKind.Monkey ? 6f : 2.5f);
            active[k].Add(a);
            Spawned++;
            return a;
        }

        Animal Create(AnimalKind kind)
        {
            int k = (int)kind;
            if (!prefabs[k]) return null;
            var spec = Animal.SpecOf(kind);
            var go = new GameObject(Names[k]) { layer = 9 };
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var cc = go.AddComponent<CharacterController>();
            cc.radius = spec.radius;
            cc.height = Mathf.Max(spec.height, spec.radius * 2f + 0.06f);
            cc.center = new Vector3(0f, cc.height * 0.5f + 0.02f, 0f);
            cc.stepOffset = Mathf.Min(kind == AnimalKind.Cow ? 0.42f : 0.3f, cc.height * 0.9f);
            cc.slopeLimit = 55f;
            cc.skinWidth = Mathf.Min(0.08f, spec.radius * 0.25f);
            go.AddComponent<CharacterMotor>();
            go.AddComponent<Health>();   // before Animal: its Awake hooks the hurt / died events
            var model = Instantiate(prefabs[k], go.transform, false);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, modelYaw[k], 0f) * model.transform.localRotation;
            model.transform.localScale *= Random.Range(0.92f, 1.08f);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 9;
            bool phone = Application.isMobilePlatform;
            foreach (var rend in model.GetComponentsInChildren<Renderer>(true))
            {
                rend.sharedMaterial = MaterialFor(k, rend.sharedMaterial);
                if (rend is SkinnedMeshRenderer smr)
                {
                    smr.updateWhenOffscreen = false;
                    if (phone) smr.quality = SkinQuality.Bone2;
                }
                if (phone && kind == AnimalKind.Cat) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var a = go.AddComponent<Animal>();
            a.kind = kind;
            a.pooled = true;
            return a;
        }

        /// <summary>The kind's material: the imported one (URP Lit when the FBX brings none) with its texture from
        /// Resources (Animals/&lt;name&gt;_color), matte.</summary>
        Material MaterialFor(int k, Material imported)
        {
            if (mats[k]) return mats[k];
            var tex = Resources.Load<Texture2D>("Animals/" + Names[k] + "_color");
            Material m;
            if (imported && imported.shader && imported.shader.name.Contains("Universal")) m = new Material(imported);
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                m = sh ? new Material(sh) : imported ? new Material(imported) : null;
            }
            if (!m) return imported;
            m.name = "M_" + Names[k];
            if (tex)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            mats[k] = m;
            return m;
        }

        void Tint(Animal a, AnimalKind kind)
        {
            var coats = Coats[(int)kind];
            Color c = coats[Random.Range(0, coats.Length)];
            mpb ??= new MaterialPropertyBlock();
            foreach (var rend in a.GetComponentsInChildren<Renderer>(true))
            {
                rend.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_Color", c);
                rend.SetPropertyBlock(mpb);
            }
        }

        // ------------------------------------------------------------------ the player's horn
        void OnHorn(Vector3 at)
        {
            var p = PlayerController.Instance;
            Vector3 fwd = p && p.Car ? p.Car.transform.forward : p ? p.transform.forward : Vector3.forward;
            fwd.y = 0f;
            foreach (var a in Animal.Live)
                if (a && (a.transform.position - at).sqrMagnitude < 30f * 30f) a.HornNear(at, fwd.normalized);
        }

        // ------------------------------------------------------------------ cows stop the traffic
        /// <summary>
        /// TrafficCar's check, right after its obstacle cast: a cow anywhere in the lane ahead (its whole 2 m body, not only
        /// the capsule the cast can see) holds the car ~3 m short of it - the car stands, honks and flashes (TrafficCar.Manners
        /// does that for anything that holds it) and does not go round it: a cow it saw as a "standing person" is taken
        /// off that list (it would overtake after 6 s). The cow, waited on, ambles off to the roadside after a few seconds
        /// of horns (Animal.WaitedOn). Returns the speed to keep.
        /// </summary>
        public static float CowAhead(Transform car, float carWidth, float carLength, float look, float targetSpeed, ref Transform standingPerson)
        {
            if (standingPerson && standingPerson.GetComponent<Animal>()) standingPerson = null;
            var live = Animal.Live;
            if (live.Count == 0) return targetSpeed;
            Vector3 fwd = car.forward;
            fwd.y = 0f;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 front = car.position + fwd * (carLength * 0.5f);
            float half = carWidth * 0.5f + 0.5f;
            float best = float.MaxValue;
            Animal hold = null;
            for (int i = 0; i < live.Count; i++)
            {
                var a = live[i];
                if (!a || a.kind != AnimalKind.Cow || a.Fleeing) continue;
                Vector3 rel0 = a.transform.position - front;
                if (Mathf.Abs(rel0.y) > 3f || rel0.sqrMagnitude > (look + 4f) * (look + 4f)) continue;
                Vector3 af = a.transform.forward;
                af.y = 0f;
                af.Normalize();
                float hl = a.spec != null ? a.spec.length * 0.5f : 1.1f;
                for (int s = -1; s <= 1; s++)   // its rump, middle and head
                {
                    Vector3 rel = rel0 + af * (hl * s);
                    float along = Vector3.Dot(rel, fwd);
                    if (along < -0.5f || along > look + 2f) continue;
                    if (Mathf.Abs(Vector3.Dot(rel, right)) > half) continue;
                    if (along < best) { best = along; hold = a; }
                }
            }
            if (!hold) return targetSpeed;
            float gap = best - 3f;
            hold.WaitedOn(car);
            if (gap < 1f) CowWaits++;
            return Mathf.Min(targetSpeed, gap < 1f ? 0f : gap * 0.45f);
        }

        // ------------------------------------------------------------------ checks
        /// <summary>One line per animal near the player (AnimalTest.Report).</summary>
        public string Report()
        {
            var sb = new System.Text.StringBuilder();
            var p = PlayerController.Instance;
            Vector3 c = p ? p.transform.position : Vector3.zero;
            sb.Append($"animals: spots {spots.Count}, spawned {Spawned}, touches {Touches}, hops {Hops}, cow waits {CowWaits}, killed {AnimalRagdoll.Killed}, bodies {dead.Count}; models ");
            for (int k = 0; k < Kinds; k++) sb.Append(Names[k]).Append(prefabs[k] ? " ok " : " MISSING ");
            foreach (var list in active)
                foreach (var a in list)
                {
                    if (!a) continue;
                    var anim = a.GetComponentInChildren<Animation>();
                    string clip = "-";
                    if (anim) foreach (AnimationState st in anim) if (anim.IsPlaying(st.name)) { clip = st.name + " x" + st.speed.ToString("F2"); break; }
                    sb.Append($"\n  {a.kind} at {a.transform.position:F1} ({Vector3.Distance(a.transform.position, c):F0} m) fleeing {a.Fleeing} clip {clip} clips {(anim ? anim.GetClipCount() : 0)}");
                }
            return sb.ToString();
        }

        /// <summary>A test: one animal of that kind this far in front of the player, on the ground (AnimalTest.Spawn).</summary>
        public Animal SpawnAt(AnimalKind kind, Vector3 pos)
        {
            if (!prefabs[(int)kind]) return null;
            if (!Ground(pos, out var g)) g = pos;
            return Spawn(kind, g, new Spot { p = g, along = Vector3.forward, half = 0f });
        }
    }
}
