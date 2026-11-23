using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The 101 fire tender (owner, 21 Nov 2026 phone batch). Until the unbranded fire truck model is made (Flow -> Tripo, from
    /// a reference photo) it is the light truck painted fire-red with a red / white light bar on the cab. It drives to the
    /// fire (RouteDriver), two firemen get down, stand ~5 m off and spray water at it; the flames go out (VehicleDamage /
    /// FireSpot.Extinguish), they climb back and the tender leaves.
    /// </summary>
    public class FireTruck : MonoBehaviour
    {
        public const string Model = "truck_fire";   // 24 Nov 2026: the unbranded tender (Flow -> Tripo, Tools/characters/FireTruck_v02.blend)
        public static readonly List<FireTruck> All = new List<FireTruck>();
        public EmergencyLights lights;
        public string State => $"{mode} t{modeT:F0} spray {sprayT:F1} crew {crew.Count} to fire {Vector3.Distance(transform.position, fire):F0} m";

        enum Mode { Drive, Spray, Leave }
        Mode mode;
        RouteDriver drive;
        Vector3 fire, parkGoal;
        Component src;
        readonly List<(Medic m, Vector3 spot, ParticleSystem jet)> crew = new List<(Medic, Vector3, ParticleSystem)>();
        float modeT, sprayT, nearSlowT;
        bool doneMsg;

        /// <summary>A tender already on its way to (or at) a fire near p.</summary>
        public static bool Coming(Vector3 p)
        {
            foreach (var f in All) if (f && f.mode != Mode.Leave && (f.fire - p).sqrMagnitude < 40f * 40f) return true;
            return false;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Red paint and the light bar on the cab roof (measured on the model: the highest point over the cab).</summary>
        public void Dress()
        {
            var mpb = new MaterialPropertyBlock();
            bool painted = false;   // the tender model comes red; only a stand-in (the light truck) gets the paint
            foreach (var r in GetComponentsInChildren<Renderer>()) if (r.sharedMaterial && r.sharedMaterial.name.StartsWith(Model)) painted = true;
            if (!painted)
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                string n = r.name.ToLowerInvariant();
                if (n.StartsWith("wheel") || r.sharedMaterial && r.sharedMaterial.name.Contains("Glass")) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", new Color(1f, 0.16f, 0.10f));
                r.SetPropertyBlock(mpb);
            }
            var info = GetComponent<VehicleInfo>();
            float L = info ? info.length : 5.6f, W = info ? info.width : 2f;
            float roof = CabRoof(gameObject, L * 0.12f, L * 0.48f, info ? info.height : 2.3f);
            lights = EmergencyLights.Fit(gameObject, new Vector3(0f, roof + 0.02f, L * 0.30f), W * 0.72f, false);
        }

        /// <summary>The top of the body over z0..z1 (vehicle space), from the Body mesh (Read/Write on, NewVehicleImport).</summary>
        static float CabRoof(GameObject go, float z0, float z1, float fallback)
        {
            float top = float.MinValue;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh;
                if (!m || !m.isReadable || !mf.name.StartsWith("Body")) continue;
                var vs = m.vertices;
                for (int i = 0; i < vs.Length; i += 3)
                {
                    Vector3 v = go.transform.InverseTransformPoint(mf.transform.TransformPoint(vs[i]));
                    if (v.z >= z0 && v.z <= z1 && Mathf.Abs(v.x) < 0.5f && v.y > top) top = v.y;
                }
            }
            return top > 0.5f ? top : fallback;
        }

        public void Begin(Vector3 at, Component source)
        {
            fire = at;
            src = source;
            drive = GetComponent<RouteDriver>();
            drive.Begin();
            drive.cruise = 13f;
            drive.offRoad = false;
            drive.ram = false;
            drive.arriveDist = 8f;
            int k = RoadRoute.ReadyAny() ? RoadRoute.Nearest(at, 120f) : -1;
            parkGoal = k >= 0 ? RoadRoute.Pos(k) : at;
            drive.SetGoal(parkGoal, true);
            mode = Mode.Drive;
            if (lights) lights.lights = lights.siren = true;
        }

        void Update()
        {
            if (!drive) return;
            float dt = Time.deltaTime;
            modeT += dt;
            switch (mode)
            {
                case Mode.Drive: Driving(dt); break;
                case Mode.Spray: Spraying(dt); break;
                case Mode.Leave: Gone(); break;
            }
        }

        void Driving(float dt)
        {
            float d = Vector3.Distance(transform.position, fire);
            if (Mathf.Abs(drive.Speed) < 0.6f && d < 45f) nearSlowT += dt; else nearSlowT = 0f;
            bool there = drive.Arrived || d < 18f || nearSlowT > 2.5f || (modeT > 150f && d < 80f);
            if (modeT > 240f && !there) { Leave(); return; }
            if (!there) return;
            drive.stop = true;
            if (lights) lights.siren = false;
            mode = Mode.Spray;
            modeT = 0f;
            // two firemen down from the cab, each to a spot ~5 m from the fire on the truck's side of it
            var pm = PoliceManager.Instance;
            Vector3 toTruck = transform.position - fire;
            toTruck.y = 0f;
            Vector3 dir = toTruck.sqrMagnitude > 0.1f ? toTruck.normalized : -transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            for (int i = 0; i < 2; i++)
            {
                Vector3 spot = fire + dir * 5f + side * (i == 0 ? -1.4f : 1.4f);
                spot = Ground(spot);
                var start = Ground(transform.position + transform.right * (i == 0 ? -1.6f : 1.6f));
                var m = pm ? pm.MakeMedic(start) : null;
                if (!m) continue;
                crew.Add((m, spot, null));
            }
            sprayT = 0f;
        }

        void Spraying(float dt)
        {
            bool allThere = crew.Count > 0;
            for (int i = 0; i < crew.Count; i++)
            {
                var c = crew[i];
                if (!c.m || c.m.Dead) { allThere = false; continue; }
                Vector3 at = c.m.transform.position;
                Vector3 to = c.spot - at;
                to.y = 0f;
                Vector3 face = fire - at;
                face.y = 0f;
                if (to.magnitude > 0.4f)
                {
                    allThere = false;
                    Vector3 next = at + to.normalized * Mathf.Min(to.magnitude, 2.2f * dt);
                    c.m.Hold(Ground(next), to, 2.2f);
                    continue;
                }
                c.m.Hold(c.spot, face, 0f);
                if (!c.jet) { c.jet = Jet(c.m.transform, fire); crew[i] = c; }
            }
            if (crew.Count == 0 || modeT > 60f) { Finish(); return; }
            if (!allThere && modeT < 25f) return;
            sprayT += dt;
            if (sprayT > 7f)
            {
                if (src) FireManager.Extinguish(src);
                else if (FireManager.NearestFire(fire, 12f, out _, out var s)) FireManager.Extinguish(s);
                if (!doneMsg)
                {
                    doneMsg = true;
                    var p = PlayerController.Instance;
                    if (p && (p.transform.position - fire).sqrMagnitude < 120f * 120f)
                        GameEvents.ShowToast("101: fire brigade ne aag bujha di", 3.5f);
                }
                if (sprayT > 9.5f) Finish();
            }
        }

        /// <summary>A water jet from the fireman's hands to the fire: a fast arc of pale droplets, then steam at the fire.</summary>
        static ParticleSystem Jet(Transform man, Vector3 target)
        {
            var ps = VehicleDamage.Particles(man, new Vector3(0.1f, 1.2f, 0.45f), "Water jet", Resources.Load<Material>("VehicleFX/M_Exhaust"), 600);
            if (!ps) return null;
            Vector3 from = man.TransformPoint(new Vector3(0.1f, 1.2f, 0.45f));
            Vector3 to = target + Vector3.up * 0.6f - from;
            float dist = new Vector2(to.x, to.z).magnitude;
            const float speed = 13f, g = 9.81f * 0.6f;
            float t = dist / speed;
            float vy = (to.y + 0.5f * g * t * t) / t;
            Vector3 v = new Vector3(to.x, 0f, to.z).normalized * speed + Vector3.up * vy;
            ps.transform.rotation = Quaternion.LookRotation(v.normalized);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = t * 1.05f;
            main.startSpeed = v.magnitude;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startColor = new Color(0.75f, 0.85f, 0.95f, 0.85f);
            main.gravityModifier = 0.6f;
            var em = ps.emission;
            em.rateOverTime = 160f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 2.5f;
            sh.radius = 0.04f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
            ps.Play();
            var au = AudioManager.Instance;
            if (au)
            {
                var src = ps.gameObject.AddComponent<AudioSource>();
                src.clip = au.Clip("amb_rain_heavy");
                src.loop = true;
                src.spatialBlend = 1f;
                src.minDistance = 2f;
                src.maxDistance = 35f;
                src.volume = 0.55f * au.masterVolume;
                if (src.clip) src.Play();
            }
            return ps;
        }

        void Finish()
        {
            foreach (var c in crew) if (c.m) Destroy(c.m.gameObject);
            crew.Clear();
            Leave();
        }

        void Leave()
        {
            mode = Mode.Leave;
            modeT = 0f;
            drive.stop = false;
            drive.arriveDist = 10f;
            if (lights) { lights.lights = true; lights.siren = false; }
            var p = PlayerController.Instance;
            Vector3 away = p ? transform.position - p.transform.position : transform.forward;
            away.y = 0f;
            Vector3 goal = transform.position + (away.sqrMagnitude > 1f ? away.normalized : transform.forward) * 280f;
            var bay = FireStationBay.Instance;
            if (bay) goal = bay.transform.position;   // back to the station
            int k = RoadRoute.ReadyAny() ? RoadRoute.Nearest(goal, 300f) : -1;
            drive.SetGoal(k >= 0 ? RoadRoute.Pos(k) : goal, true);
        }

        void Gone()
        {
            var p = PlayerController.Instance;
            float d = p ? Vector3.Distance(p.transform.position, transform.position) : 999f;
            var cam = Camera.main;
            Vector3 vp = cam ? cam.WorldToViewportPoint(transform.position) : Vector3.back;
            bool seen = vp.z > 0f && vp.z < 200f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
            if ((d > 160f && !seen) || modeT > 80f || drive.Arrived && !seen) Destroy(gameObject);
        }

        static Vector3 Ground(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            float top = t ? t.SampleHeight(p) + t.transform.position.y : p.y;
            if (Physics.Raycast(new Vector3(p.x, Mathf.Max(p.y, top) + 2f, p.z), Vector3.down, out var hit, 6f,
                    ~((1 << 8) | (1 << 9) | (1 << VehicleFactory.VehicleLayer) | (1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore))
                return hit.point;
            return new Vector3(p.x, Mathf.Max(p.y, top), p.z);
        }
    }
}
