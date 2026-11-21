using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Sends the police the wanted level asks for (WantedSystem) and makes the people and vehicles of the police and the
    /// 108 ambulance. Units appear on a road out of sight 110-220 m off (on foot 45-80 m) and come in:
    /// 1 star: one constable, on the bike (6 in 10) or running.
    /// 2 stars: a police Bolero with two men (the 1-star man stays on it).
    /// 3 stars: two Bolero and a bike.
    /// Units far behind (over 300 m) are dropped and sent again nearer. Wanted level gone: they all go.
    /// </summary>
    public class PoliceManager : MonoBehaviour
    {
        public static PoliceManager Instance { get; private set; }

        public readonly List<PoliceCar> Cars = new List<PoliceCar>();
        public readonly List<PoliceBike> Bikes = new List<PoliceBike>();
        public readonly List<Ambulance> Ambulances = new List<Ambulance>();
        float timer, firstDelay;
        int lastStars;
        bool footSent;

        void Awake() => Instance = this;

        void Start()
        {
            var ws = WantedSystem.Instance;
            if (ws) ws.StarsChanged += s =>
            {
                if (s > 0 && lastStars == 0) { firstDelay = Time.time + Random.Range(4f, 7f); footSent = false; }
                lastStars = s;
            };
        }

        public static Material Mat
        {
            get
            {
                var tm = FindAnyObjectByType<TrafficManager>();
                return tm && tm.material ? tm.material : GameAssets.AtlasMaterial;
            }
        }

        // ------------------------------------------------------------------ dispatch
        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 1f;
            Cars.RemoveAll(c => !c);
            Bikes.RemoveAll(b => !b);
            Ambulances.RemoveAll(a => !a);
            var ws = WantedSystem.Instance;
            int stars = ws ? ws.Stars : 0;
            var p = PlayerController.Instance;
            var cam = Camera.main;
            if (p && cam && stars == 0) Tidy(p.transform.position, cam);
            if (!p || !cam || stars == 0 || Time.time < firstDelay) return;
            if (p.CurrentMode == PlayerController.Mode.Dead) return;
            if (!RoadRoute.ReadyAny()) return;
            Vector3 pp = p.transform.position;

            // drop units left far behind (they are sent again, nearer)
            foreach (var c in Cars) if (c && c.Current == PoliceCar.Mode.Chase && (c.transform.position - pp).sqrMagnitude > 300f * 300f && c.AllAboard) Destroy(c.gameObject);
            foreach (var b in Bikes) if (b && b.Current == PoliceBike.Mode.Ride && (b.transform.position - pp).sqrMagnitude > 300f * 300f && b.AllAboard) Destroy(b.gameObject);

            int cars = 0, bikes = 0, foot = 0;
            foreach (var c in Cars) if (c && c.Current != PoliceCar.Mode.Leave && c.Current != PoliceCar.Mode.Scene && c.Usable && c.Living > 0) cars++;
            foreach (var b in Bikes) if (b && b.Current != PoliceBike.Mode.Leave && b.Usable && b.Living > 0) bikes++;
            foreach (var o in PoliceOfficer.All) if (o && !o.Dead && !o.ride && o.Current != PoliceOfficer.State.Leave) foot++;

            if (stars == 1)
            {
                if (cars + bikes + foot == 0 && !footSent)
                {
                    footSent = true;
                    bool onBike = Random.value < 0.6f || p.CurrentMode == PlayerController.Mode.Vehicle;
                    if (!(onBike && SpawnBike(pp, cam))) SpawnRunner(pp, cam);
                }
                return;
            }
            int wantCars = stars >= 3 ? 2 : 1, wantBikes = stars >= 3 ? 1 : 0;
            if (cars < wantCars) { SpawnCar(pp, cam); return; }
            if (bikes < wantBikes) SpawnBike(pp, cam);
        }

        static bool Visible(Camera cam, Vector3 w)
        {
            Vector3 vp = cam.WorldToViewportPoint(w);
            return vp.z > 0f && vp.z < 220f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
        }

        /// <summary>Nobody wanted: units left far behind and out of sight (the player respawned at the hospital or the
        /// police station, a car whose men could not get back to it) are removed with their crews.</summary>
        void Tidy(Vector3 pp, Camera cam)
        {
            foreach (var c in Cars)
                if (c && c.Current != PoliceCar.Mode.Scene && (c.transform.position - pp).sqrMagnitude > 230f * 230f && !Visible(cam, c.transform.position))
                {
                    foreach (var o in c.crew) if (o && !o.Seated) Destroy(o.gameObject);
                    Destroy(c.gameObject);
                }
            foreach (var b in Bikes)
                if (b && (b.transform.position - pp).sqrMagnitude > 230f * 230f && !Visible(cam, b.transform.position))
                {
                    foreach (var o in b.crew) if (o && !o.Seated) Destroy(o.gameObject);
                    Destroy(b.gameObject);
                }
            foreach (var o in PoliceOfficer.All.ToArray())
                if (o && !o.Dead && !o.ride && !o.guarding && (o.transform.position - pp).sqrMagnitude > 200f * 200f && !Visible(cam, o.transform.position))
                    Destroy(o.gameObject);
        }

        /// <summary>Is a police vehicle already near p (an accident needs no second car)?</summary>
        public bool PoliceNear(Vector3 p, float r)
        {
            foreach (var c in Cars) if (c && c.Current != PoliceCar.Mode.Leave && (c.transform.position - p).sqrMagnitude < r * r) return true;
            return false;
        }

        // ------------------------------------------------------------------ people
        static CharacterLibrary.Entry Look(string[] ids)
        {
            var lib = CharacterLibrary.Instance;
            if (!lib || ids == null || ids.Length == 0) return null;
            for (int k = 0; k < 4; k++)
            {
                var e = lib.Find(ids[Random.Range(0, ids.Length)]);
                if (e != null) return e;
            }
            foreach (var id in ids) { var e = lib.Find(id); if (e != null) return e; }
            return null;
        }

        GameObject Person(string name, CharacterLibrary.Entry look, Vector3 pos, int top, int legs)
        {
            var go = new GameObject(name) { layer = 9 };
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.92f, 0);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 52f;
            go.AddComponent<CharacterMotor>();
            var h = go.AddComponent<Health>();
            h.max = h.current = 100f;
            var rigGo = new GameObject("Rig") { layer = 9 };
            rigGo.transform.SetParent(go.transform, false);
            rigGo.SetActive(false);
            var rig = rigGo.AddComponent<HumanoidRig>();
            rig.material = Mat;
            rig.skin = Pal.SkinMid;
            rig.hair = Pal.HairBlack;
            rig.top = top;
            rig.legs = legs;
            rig.shoes = Pal.ShoeBrown;
            rig.bodyType = HumanoidRig.BodyType.Man;
            if (look != null) CharacterLibrary.Apply(look, rig, cc);
            rigGo.SetActive(true);
            return go;
        }

        /// <summary>A constable (police_1 / police_2 from the CharacterLibrary; a khaki code-built man without it).</summary>
        public PoliceOfficer MakeOfficer(Vector3 pos)
        {
            var kit = PoliceKit.Instance;
            var look = Look(kit ? kit.officerIds : new[] { "police_1", "police_2" });
            var go = Person("Police", look, pos, Pal.PantsKhaki, Pal.PantsKhaki);
            var o = go.AddComponent<PoliceOfficer>();
            o.displayName = look != null && look.id == "police_2" ? "Havaldar" : "Constable";
            return o;
        }

        /// <summary>One of the 108 crew.</summary>
        /// <summary>A townsperson in a random look (restaurant diners, 22 Nov 2026): no script moves them.</summary>
        public GameObject MakePerson(string name, Vector3 pos)
        {
            CharacterLibrary.Entry look = null;
            var lib = CharacterLibrary.Instance;
            if (lib && lib.entries.Length > 0)
                for (int k = 0; k < 8 && look == null; k++)
                {
                    var e = lib.entries[Random.Range(0, lib.entries.Length)];
                    if (e != null && !string.IsNullOrEmpty(e.id) && !e.id.StartsWith("police")) look = e;
                }
            var go = Person(name, look, pos, Pal.ShirtWhite, Pal.Jeans);
            go.transform.SetParent(null, true);
            return go;
        }

        /// <summary>A restaurant's waiter: a townsperson's look (not the 108 crew's uniform) that walks where it is told
        /// (the Medic walker; Restaurant.Bring).</summary>
        public Medic MakeWaiter(Vector3 pos)
        {
            var go = MakePerson("Waiter", pos);
            var m = go.AddComponent<Medic>();
            m.rig = go.GetComponentInChildren<HumanoidRig>(true);
            return m;
        }

        public Medic MakeMedic(Vector3 pos)
        {
            var kit = PoliceKit.Instance;
            var look = Look(kit ? kit.medicIds : null);
            var go = Person("108 crew", look, pos, Pal.ShirtWhite, Pal.PantsBlack);
            var m = go.AddComponent<Medic>();
            m.rig = go.GetComponentInChildren<HumanoidRig>(true);
            return m;
        }

        // ------------------------------------------------------------------ vehicles
        static int ModelIndex(string name)
        {
            var lib = VehicleLibrary.Instance;
            if (!lib) return -1;
            for (int i = 0; i < lib.entries.Length; i++) if (lib.entries[i] != null && lib.entries[i].prefab && lib.entries[i].name == name) return i;
            return -1;
        }

        static void Letters(GameObject car, Mesh mesh)
        {
            if (!mesh) return;
            var go = new GameObject("Letters") { layer = car.layer };
            go.transform.SetParent(car.transform.Find("Body") ?? car.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GameAssets.AtlasMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static readonly List<Vector3> spawnRoute = new List<Vector3>();

        /// <summary>A vehicle on road point `node`, facing the way its route to `toward` starts (no turning round first).</summary>
        GameObject MakeCarAt(int node, string model, CarType type, string name, Vector3 toward)
        {
            Vector3 p = RoadRoute.Pos(node);
            Vector3 f = RoadRoute.Heading(node);
            if (RoadRoute.Find(node, RoadRoute.Nearest(toward, 400f), spawnRoute) && spawnRoute.Count > 2)
            {
                Vector3 r = spawnRoute[Mathf.Min(3, spawnRoute.Count - 1)] - spawnRoute[0];
                r.y = 0f;
                if (r.sqrMagnitude > 0.5f) f = r.normalized;
            }
            else if (Vector3.Dot(f, toward - p) < 0f) f = -f;
            if (Physics.CheckBox(p + Vector3.up * 1.2f, new Vector3(1.2f, 0.8f, 3f), Quaternion.LookRotation(f), 1 << VehicleFactory.VehicleLayer, QueryTriggerInteraction.Ignore))
                return null;
            var go = VehicleFactory.Create(type, Px.CarWhite, Mat, true, name, ModelIndex(model));
            go.transform.SetParent(transform, true);
            go.transform.SetPositionAndRotation(p + Vector3.up * 0.4f, Quaternion.LookRotation(f, Vector3.up));
            Physics.SyncTransforms();
            var rb = go.GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.linearVelocity = f * 6f;
            var lamps = go.GetComponent<VehicleLamps>();
            if (lamps) lamps.always = true;
            return go;
        }

        PoliceCar NewPoliceCar(int node, PoliceCar.Mode mode, Vector3 scene)
        {
            var kit = PoliceKit.Instance;
            var pl = PlayerController.Instance;
            var go = MakeCarAt(node, kit ? kit.policeCarModel : "Bolero", CarType.Suv, "Police Bolero", mode == PoliceCar.Mode.Scene ? scene : pl ? pl.transform.position : scene);
            if (!go) return null;
            go.AddComponent<RouteDriver>();
            Letters(go, kit ? kit.policeLetters : null);
            var car = go.AddComponent<PoliceCar>();
            car.lights = EmergencyLights.Fit(go, kit ? kit.policeRoof : new Vector3(0f, 1.95f, -0.1f), kit ? kit.policeRoofWidth : 1.3f, true);
            car.Begin(mode, scene);
            for (int s = 0; s < 2; s++) MakeOfficer(go.transform.position + Vector3.up * 3f).SitIn(car, s);
            Cars.Add(car);
            return car;
        }

        bool SpawnCar(Vector3 pp, Camera cam)
        {
            int k = RoadRoute.SpawnNode(pp, 110f, 220f, cam);
            return k >= 0 && NewPoliceCar(k, PoliceCar.Mode.Chase, Vector3.zero);
        }

        /// <summary>A police car for an accident (no one wanted): it stops by the body and a man stands guard.</summary>
        public PoliceCar SpawnSceneCar(Vector3 at)
        {
            if (!RoadRoute.ReadyAny()) return null;
            var cam = Camera.main;
            int k = RoadRoute.SpawnNode(at, 120f, 220f, cam);
            return k >= 0 ? NewPoliceCar(k, PoliceCar.Mode.Scene, at) : null;
        }

        bool SpawnBike(Vector3 pp, Camera cam)
        {
            var lib = VehicleLibrary.Instance;
            var kit = PoliceKit.Instance;
            VehicleLibrary.Entry e = lib ? lib.Bike(kit ? kit.bikeModel : "Splendor") : null;
            if (e == null) return false;
            int k = RoadRoute.SpawnNode(pp, 90f, 170f, cam);
            if (k < 0) return false;
            var go = BikeRide.Create(e, "Police bike");
            go.transform.SetParent(transform, true);
            var ride = go.GetComponent<BikeRide>();
            Vector3 f = RoadRoute.Heading(k);
            if (Vector3.Dot(f, pp - RoadRoute.Pos(k)) < 0f) f = -f;
            ride.Place(RoadRoute.Pos(k), f);
            var pb = go.AddComponent<PoliceBike>();
            pb.Begin();
            pb.lights = EmergencyLights.Fit(go, new Vector3(0f, ride.seatTop + 0.42f, -ride.length * 0.42f), 0.5f, true);
            pb.lights.transform.Find("LightBar").SetParent(ride.model, true);   // the bar leans with the bike
            MakeOfficer(go.transform.position + Vector3.up * 3f).SitIn(pb, 0);
            Bikes.Add(pb);
            return true;
        }

        /// <summary>A constable who comes running (1 star, from a footpath or road out of sight).</summary>
        bool SpawnRunner(Vector3 pp, Camera cam)
        {
            int k = RoadRoute.SpawnNode(pp, 45f, 80f, cam);
            if (k < 0) return false;
            var o = MakeOfficer(RoadRoute.Pos(k) + Vector3.up * 0.3f);
            return o;
        }

        /// <summary>The 108 ambulance the player called for themselves (the phone): it comes and a medic treats them.</summary>
        public Ambulance SpawnAmbulanceForPlayer(Vector3 at)
        {
            if (!RoadRoute.ReadyAny()) return null;
            var kit = PoliceKit.Instance;
            int k = RoadRoute.SpawnNode(at, 150f, 260f, Camera.main);
            if (k < 0) return null;
            var go = MakeCarAt(k, kit ? kit.ambulanceModel : "Ambulance108", CarType.Suv, "108 Ambulance", at);
            if (!go) return null;
            go.AddComponent<RouteDriver>();
            Letters(go, kit ? kit.ambulanceLetters : null);
            var a = go.AddComponent<Ambulance>();
            a.lights = EmergencyLights.Fit(go, kit ? kit.ambulanceRoof : new Vector3(0f, 2.75f, 1.6f), kit ? kit.ambulanceRoofWidth : 1.6f, false);
            a.BeginForPlayer(at);
            Ambulances.Add(a);
            return a;
        }

        /// <summary>The 101 fire tender (the phone's call): the light truck in red with a light bar, from a road out of sight.</summary>
        public FireTruck SpawnFireTruck(Vector3 at, Component src)
        {
            if (!RoadRoute.ReadyAny()) return null;
            // from the fire station when the fire is a fair way off and nobody is looking at its bay; else a road out of sight
            int k = -1;
            var bay = FireStationBay.Instance;
            if (bay && !bay.Seen() && Vector3.Distance(bay.transform.position, at) > 100f) k = RoadRoute.Nearest(bay.transform.position, 40f);
            if (k < 0) k = RoadRoute.SpawnNode(at, 150f, 260f, Camera.main);
            if (k < 0) return null;
            var go = MakeCarAt(k, FireTruck.Model, CarType.Pickup, "101 Fire Tender", at);
            if (!go) return null;
            go.AddComponent<RouteDriver>();
            var ft = go.AddComponent<FireTruck>();
            ft.Dress();
            ft.Begin(at, src);
            return ft;
        }

        /// <summary>The 108 ambulance for an accident, from a road out of sight.</summary>
        public Ambulance SpawnAmbulance(IncidentManager.Incident inc)
        {
            if (!RoadRoute.ReadyAny()) return null;
            var kit = PoliceKit.Instance;
            int k = RoadRoute.SpawnNode(inc.pos, 150f, 260f, Camera.main);
            if (k < 0) return null;
            var go = MakeCarAt(k, kit ? kit.ambulanceModel : "Ambulance108", CarType.Suv, "108 Ambulance", inc.pos);
            if (!go) return null;
            go.AddComponent<RouteDriver>();
            Letters(go, kit ? kit.ambulanceLetters : null);
            var a = go.AddComponent<Ambulance>();
            a.lights = EmergencyLights.Fit(go, kit ? kit.ambulanceRoof : new Vector3(0f, 2.75f, 1.6f), kit ? kit.ambulanceRoofWidth : 1.6f, false);
            a.Begin(inc);
            Ambulances.Add(a);
            return a;
        }
    }
}
