using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    public enum CarType { Hatchback = 0, Sedan = 1, Suv = 2, Taxi = 3, Bus = 4, Pickup = 5 }

    /// <summary>Builds mid-poly vehicles from code (generic designs, no real brands). Right-hand drive like India.</summary>
    public static class VehicleFactory
    {
        public const int VehicleLayer = 10;

        public struct Spec
        {
            public float length, width, height, wheelBase, track, wheelRadius, mass, maxSpeed, engine, steer;
            public Vector3 seat;
        }

        public static Spec GetSpec(CarType t)
        {
            switch (t)
            {
                case CarType.Sedan:
                    return new Spec { length = 4.3f, width = 1.72f, height = 1.42f, wheelBase = 2.6f, track = 1.48f, wheelRadius = 0.32f, mass = 1150f, maxSpeed = 34f, engine = 7800f, steer = 34f, seat = new Vector3(0.38f, 0.06f, -0.15f) };
                case CarType.Suv:
                case CarType.Taxi:
                    return new Spec { length = 4.4f, width = 1.82f, height = 1.85f, wheelBase = 2.7f, track = 1.56f, wheelRadius = 0.38f, mass = 1700f, maxSpeed = 30f, engine = 11500f, steer = 32f, seat = new Vector3(0.42f, 0.3f, -0.05f) };
                case CarType.Pickup:   // Bolero pickup, Tata 407: goods carriers
                    return new Spec { length = 5.3f, width = 1.95f, height = 2.5f, wheelBase = 3.1f, track = 1.6f, wheelRadius = 0.4f, mass = 2600f, maxSpeed = 24f, engine = 16000f, steer = 32f, seat = new Vector3(0.45f, 0.45f, 1.5f) };
                case CarType.Bus:
                    return new Spec { length = 9.6f, width = 2.45f, height = 3.05f, wheelBase = 5.4f, track = 2.0f, wheelRadius = 0.5f, mass = 7500f, maxSpeed = 21f, engine = 40000f, steer = 30f, seat = new Vector3(0.75f, 0.85f, 3.85f) };
                default:
                    return new Spec { length = 3.7f, width = 1.62f, height = 1.5f, wheelBase = 2.4f, track = 1.4f, wheelRadius = 0.3f, mass = 950f, maxSpeed = 31f, engine = 6400f, steer = 36f, seat = new Vector3(0.36f, 0.05f, -0.1f) };
            }
        }

        static Vector2 X(int i) => Atlas.ExtraUV(i);

        static readonly Dictionary<long, Mesh> bodyCache = new Dictionary<long, Mesh>();
        static readonly Dictionary<int, Mesh> wheelCache = new Dictionary<int, Mesh>();

        // ------------------------------------------------------------------ geometry
        /// <summary>Extrude a side profile (z forward, y up) across the car width.</summary>
        static void Extrude(MeshBuilder mb, Vector2[] prof, float halfW, Vector2 sideUV, System.Func<int, Vector2> stripUV)
        {
            int n = prof.Length;
            Vector2 c = Vector2.zero;
            foreach (var p in prof) c += p;
            c /= n;
            var tris = Geo.Triangulate(prof);
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector2 a = prof[tris[t]], b = prof[tris[t + 1]], d = prof[tris[t + 2]];
                mb.Tri(new Vector3(halfW, a.y, a.x), new Vector3(halfW, b.y, b.x), new Vector3(halfW, d.y, d.x), sideUV, Vector3.right);
                mb.Tri(new Vector3(-halfW, a.y, a.x), new Vector3(-halfW, b.y, b.x), new Vector3(-halfW, d.y, d.x), sideUV, Vector3.left);
            }
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = prof[i], p1 = prof[(i + 1) % n];
                Vector2 e = p1 - p0;
                Vector2 nn = new Vector2(e.y, -e.x);
                if (Vector2.Dot(nn, (p0 + p1) * 0.5f - c) < 0) nn = -nn;
                mb.Quad(new Vector3(-halfW, p0.y, p0.x), new Vector3(halfW, p0.y, p0.x), new Vector3(halfW, p1.y, p1.x), new Vector3(-halfW, p1.y, p1.x),
                        stripUV(i), new Vector3(0, nn.y, nn.x));
            }
        }

        static Vector2[] P(params float[] v)
        {
            var r = new Vector2[v.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = new Vector2(v[2 * i], v[2 * i + 1]);
            return r;
        }

        static void Lights(MeshBuilder mb, float frontZ, float rearZ, float y, float halfW, bool taxi)
        {
            mb.Box(new Vector3(-halfW * 0.62f, y, frontZ), new Vector3(0.32f, 0.11f, 0.06f), X(Px.Headlight));
            mb.Box(new Vector3(halfW * 0.62f, y, frontZ), new Vector3(0.32f, 0.11f, 0.06f), X(Px.Headlight));
            mb.Box(new Vector3(-halfW * 0.72f, y + 0.08f, rearZ), new Vector3(0.26f, 0.14f, 0.06f), X(Px.TailLight));
            mb.Box(new Vector3(halfW * 0.72f, y + 0.08f, rearZ), new Vector3(0.26f, 0.14f, 0.06f), X(Px.TailLight));
            Vector2 plate = taxi ? X(Px.Plate) : X(Px.CarWhite);
            mb.Box(new Vector3(0, y - 0.2f, frontZ + 0.02f), new Vector3(0.5f, 0.12f, 0.03f), plate);
            mb.Box(new Vector3(0, y - 0.1f, rearZ - 0.02f), new Vector3(0.5f, 0.12f, 0.03f), plate);
        }

        public static Mesh BodyMesh(CarType type, int paint)
        {
            long key = (long)type * 1000 + paint;
            if (bodyCache.TryGetValue(key, out var cached) && cached) return cached;
            var s = GetSpec(type);
            var mb = new MeshBuilder();
            Vector2 paintUV = X(paint), glass = X(Px.CarGlass), black = X(Px.CarBlack), chrome = X(Px.Chrome);
            float hw = s.width * 0.5f;
            switch (type)
            {
                case CarType.Sedan:
                    Extrude(mb, P(-2.15f, 0.3f, 2.12f, 0.3f, 2.18f, 0.52f, 2.05f, 0.7f, 1.0f, 0.82f, -1.35f, 0.85f, -2.1f, 0.82f, -2.18f, 0.55f), hw, paintUV, i => paintUV);
                    Extrude(mb, P(1.0f, 0.82f, 0.25f, 1.36f, -0.95f, 1.38f, -1.5f, 0.85f), hw * 0.86f, glass, i => i == 1 ? paintUV : glass);
                    Lights(mb, 2.15f, -2.17f, 0.6f, hw, false);
                    break;
                case CarType.Suv:
                case CarType.Taxi:
                    Extrude(mb, P(-2.2f, 0.42f, 2.15f, 0.42f, 2.2f, 0.78f, 2.02f, 1.05f, 1.25f, 1.12f, -2.15f, 1.15f, -2.22f, 0.92f), hw, paintUV, i => paintUV);
                    Extrude(mb, P(1.25f, 1.12f, 0.72f, 1.78f, -2.08f, 1.8f, -2.15f, 1.15f), hw * 0.9f, glass, i => i == 1 ? paintUV : glass);
                    mb.Box(new Vector3(0, 1.84f, -0.6f), new Vector3(s.width * 0.8f, 0.05f, 2.2f), black);   // roof rack
                    mb.Cylinder(new Vector3(0, 0.62f, -2.36f), 0.36f, 0.36f, 0.22f, 12, X(Px.Tyre), true, true);
                    if (type == CarType.Taxi) mb.Box(new Vector3(0, 1.93f, 0.2f), new Vector3(0.7f, 0.16f, 0.3f), X(Px.TaxiYellow));
                    Lights(mb, 2.2f, -2.23f, 0.85f, hw, type == CarType.Taxi);
                    break;
                case CarType.Bus:
                    Extrude(mb, P(-4.8f, 0.48f, 4.8f, 0.48f, 4.86f, 1.55f, -4.8f, 1.55f), hw, X(Px.BusBlue), i => X(Px.BusBlue));
                    Extrude(mb, P(-4.8f, 1.55f, 4.86f, 1.55f, 4.7f, 2.65f, -4.8f, 2.65f), hw * 0.985f, glass, i => glass);
                    Extrude(mb, P(-4.8f, 2.65f, 4.7f, 2.65f, 4.5f, 3.02f, -4.75f, 3.02f), hw, X(Px.BusWhite), i => X(Px.BusWhite));
                    for (float z = -4.2f; z < 4.3f; z += 1.15f)
                        foreach (float sx in new[] { -hw, hw })
                            mb.Box(new Vector3(sx, 2.1f, z), new Vector3(0.05f, 1.1f, 0.12f), X(Px.BusWhite));
                    mb.Box(new Vector3(0, 2.75f, 4.72f), new Vector3(1.6f, 0.25f, 0.05f), X(Px.Indicator));     // destination board
                    mb.Box(new Vector3(0, 0.65f, 4.88f), new Vector3(s.width * 0.95f, 0.3f, 0.08f), black);
                    Lights(mb, 4.88f, -4.84f, 0.95f, hw, true);
                    break;
                default: // hatchback
                    Extrude(mb, P(-1.85f, 0.28f, 1.82f, 0.28f, 1.88f, 0.5f, 1.78f, 0.72f, 1.1f, 0.86f, -1.7f, 0.9f, -1.86f, 0.72f), hw, paintUV, i => paintUV);
                    Extrude(mb, P(1.1f, 0.86f, 0.35f, 1.38f, -1.25f, 1.42f, -1.72f, 0.9f), hw * 0.88f, glass, i => i == 1 ? paintUV : glass);
                    Lights(mb, 1.86f, -1.88f, 0.62f, hw, false);
                    break;
            }
            if (type != CarType.Bus)
            {
                float fz = s.length * 0.5f, by = type == CarType.Suv || type == CarType.Taxi ? 0.5f : 0.38f;
                mb.Box(new Vector3(0, by, fz - 0.02f), new Vector3(s.width * 0.96f, 0.18f, 0.14f), black);   // bumpers
                mb.Box(new Vector3(0, by, -fz + 0.02f), new Vector3(s.width * 0.96f, 0.18f, 0.14f), black);
                mb.Box(new Vector3(hw + 0.08f, by + 0.62f, 0.8f), new Vector3(0.14f, 0.1f, 0.08f), black);   // mirrors
                mb.Box(new Vector3(-hw - 0.08f, by + 0.62f, 0.8f), new Vector3(0.14f, 0.1f, 0.08f), black);
                mb.Box(new Vector3(0, by + 0.35f, fz - 0.01f), new Vector3(0.6f, 0.08f, 0.04f), chrome);    // grille
            }
            var mesh = mb.ToMesh($"Car_{type}_{paint}");
            if (!Application.isPlaying) mesh.hideFlags = HideFlags.DontSave;
            bodyCache[key] = mesh;
            return mesh;
        }

        public static Mesh WheelMesh(float r, float w)
        {
            int key = Mathf.RoundToInt(r * 1000) * 10 + Mathf.RoundToInt(w * 10);
            if (wheelCache.TryGetValue(key, out var cached) && cached) return cached;
            var mb = new MeshBuilder { M = Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90)) };
            mb.Cylinder(new Vector3(0, -w / 2, 0), r, r, w, 14, X(Px.Tyre), true, true);
            mb.Cylinder(new Vector3(0, -w / 2 - 0.006f, 0), r * 0.58f, r * 0.58f, w + 0.012f, 10, X(Px.Rim), true, true);
            var mesh = mb.ToMesh("Wheel");
            if (!Application.isPlaying) mesh.hideFlags = HideFlags.DontSave;
            wheelCache[key] = mesh;
            return mesh;
        }

        public static readonly int[] CarPaints = { Px.CarWhite, Px.CarSilver, Px.CarRed, Px.CarBlue, Px.CarBlack, Px.CarGreen, Px.CarWhite, Px.CarSilver };

        // ------------------------------------------------------------------ assembly
        /// <summary>
        /// Creates a vehicle GameObject. gameplay=true adds Rigidbody + CarController (drivable).
        /// Wheels order: FL, FR, RL, RR.
        /// </summary>
        public static GameObject Create(CarType type, int paint, Material mat, bool gameplay, string name = null, int model = -1)
        {
            var lib = VehicleLibrary.Instance;
            if (lib && lib.entries.Length > 0)
            {
                if (model < 0 || model >= lib.entries.Length) model = lib.Pick(type, Random.value);
                if (model >= 0) return CreateModel(lib.entries[model], type, paint, gameplay, name);
            }
            var s = GetSpec(type);
            var root = new GameObject(name ?? type.ToString()) { layer = VehicleLayer };
            var body = new GameObject("Body") { layer = VehicleLayer };
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = BodyMesh(type, paint);
            body.AddComponent<MeshRenderer>().sharedMaterial = mat;

            var wheels = new Transform[4];
            var wm = WheelMesh(s.wheelRadius, type == CarType.Bus ? 0.32f : 0.22f);
            float[] xs = { -s.track / 2, s.track / 2, -s.track / 2, s.track / 2 };
            float[] zs = { s.wheelBase / 2, s.wheelBase / 2, -s.wheelBase / 2, -s.wheelBase / 2 };
            for (int i = 0; i < 4; i++)
            {
                var w = new GameObject("Wheel" + i) { layer = VehicleLayer };
                w.transform.SetParent(root.transform, false);
                w.transform.localPosition = new Vector3(xs[i], s.wheelRadius, zs[i]);
                w.AddComponent<MeshFilter>().sharedMesh = wm;
                w.AddComponent<MeshRenderer>().sharedMaterial = mat;
                wheels[i] = w.transform;
            }

            var seat = new GameObject("Seat").transform;
            seat.SetParent(root.transform, false);
            seat.localPosition = s.seat;

            var box = root.AddComponent<BoxCollider>();
            float bottom = s.wheelRadius * 0.9f;
            box.center = new Vector3(0, (bottom + s.height) * 0.5f, 0);
            box.size = new Vector3(s.width, s.height - bottom, s.length);

            if (!Application.isPlaying)
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;

            if (gameplay)
            {
                var rb = root.AddComponent<Rigidbody>();
                rb.mass = s.mass;
                rb.isKinematic = true;
                var lights = new Light[2];
                for (int i = 0; i < 2; i++)
                {
                    var lg = new GameObject("Headlight" + i);
                    lg.transform.SetParent(root.transform, false);
                    lg.transform.localPosition = new Vector3((i == 0 ? -1 : 1) * s.width * 0.3f, s.wheelRadius + 0.45f, s.length * 0.5f);
                    lg.transform.localRotation = Quaternion.Euler(6, 0, 0);
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.range = 60f;
                    l.spotAngle = 62f;
                    l.intensity = 16f;
                    l.color = new Color(1f, 0.95f, 0.85f);
                    l.enabled = false;
                    lights[i] = l;
                }
                root.AddComponent<CarController>().Setup(type, wheels, seat, lights);
                root.AddComponent<VehicleDamage>();   // knocks: smoke, fire, blast
            }
            return root;
        }

        static Material exhaustMat;

        /// <summary>A small smoke emitter at the exhaust: grey puffs that drift up and fade (rate set by VehicleFX).</summary>
        static ParticleSystem Exhaust(Transform at, Vector3 local)
        {
            if (!exhaustMat) exhaustMat = Resources.Load<Material>("VehicleFX/M_Exhaust");
            if (!exhaustMat || !Application.isPlaying) return null;
            var go = new GameObject("Smoke");
            go.transform.SetParent(at, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(-10f, 180f, 0f);   // out of the back
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            main.startColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 12f;
            sh.radius = 0.04f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = exhaustMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        /// <summary>The paint as a colour multiplied onto a white-bodied model (null = leave it as modelled).</summary>
        static Color? Tint(int paint)
        {
            if (paint == Px.CarSilver) return new Color(0.74f, 0.76f, 0.79f);
            if (paint == Px.CarRed) return new Color(0.62f, 0.13f, 0.11f);
            if (paint == Px.CarBlue) return new Color(0.2f, 0.3f, 0.56f);
            if (paint == Px.CarBlack) return new Color(0.16f, 0.16f, 0.18f);
            if (paint == Px.CarGreen) return new Color(0.22f, 0.38f, 0.26f);
            return null;
        }

        /// <summary>
        /// A vehicle from the real model library: the model is the body, the wheels are empty pivots where the model's own
        /// wheels are (CarController and TrafficCar steer by them), the box collider is the model's size.
        /// </summary>
        public static GameObject CreateModel(VehicleLibrary.Entry e, CarType type, int paint, bool gameplay, string name)
        {
            var s = GetSpec(type);
            var root = new GameObject(name ?? e.name) { layer = VehicleLayer };
            var body = Object.Instantiate(e.prefab, root.transform, false);
            body.name = "Body";
            foreach (var t in body.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = VehicleLayer;
            var tint = e.tint ? Tint(paint) : null;
            if (tint.HasValue)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", tint.Value);
                foreach (var r in body.GetComponentsInChildren<Renderer>(true)) r.SetPropertyBlock(mpb);
            }

            float L = e.size.z, W = e.size.x, H = e.size.y;
            var wheels = new Transform[4];
            var found = new List<Transform>();
            foreach (var t in body.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Wheel_")) found.Add(t);
            float R = e.wheelRadius > 0f ? e.wheelRadius : s.wheelRadius;
            if (found.Count == 4)   // the model's own tyres, cut out by make_vehicle_lods.py: these spin and steer
            {
                found.Sort((a, b) => b.position.z.CompareTo(a.position.z));   // front pair first (root at the origin here)
                if (found[0].position.x > found[1].position.x) (found[0], found[1]) = (found[1], found[0]);
                if (found[2].position.x > found[3].position.x) (found[2], found[3]) = (found[3], found[2]);
                R = 0f;
                for (int i = 0; i < 4; i++)
                {
                    found[i].SetParent(root.transform, true);
                    found[i].localRotation = Quaternion.identity;
                    wheels[i] = found[i];
                    R += found[i].localPosition.y * 0.25f;
                }
            }
            else
            {
                float wb = L * (L > 6.5f ? 0.56f : 0.6f), tr = Mathf.Min(W * 0.8f, 2.1f);
                float[] xs = { -tr / 2, tr / 2, -tr / 2, tr / 2 };
                float[] zs = { wb / 2, wb / 2, -wb / 2, -wb / 2 };
                for (int i = 0; i < 4; i++)
                {
                    var w = new GameObject("Wheel" + i) { layer = VehicleLayer };
                    w.transform.SetParent(root.transform, false);
                    w.transform.localPosition = new Vector3(xs[i], R, zs[i]);
                    wheels[i] = w.transform;
                }
            }
            var info = root.AddComponent<VehicleInfo>();
            info.wheelRadius = R;
            info.length = L;
            info.width = W;
            info.height = H;
            var lampTf = body.transform.Find("Lamps");
            if (lampTf)
            {
                var lr = lampTf.GetComponent<Renderer>();
                lr.enabled = false;
                root.AddComponent<VehicleLamps>().lamps = lr;
            }
            var fx = root.AddComponent<VehicleFX>();
            var tailTf = body.transform.Find("Tail");
            if (tailTf) { fx.tail = tailTf.GetComponent<Renderer>(); fx.tail.enabled = false; }
            var exTf = body.transform.Find("Exhaust");
            fx.exhaust = Exhaust(exTf ? exTf : root.transform, exTf ? Vector3.zero : new Vector3(W * 0.25f, 0.35f, -L * 0.5f));
            var eng = new GameObject("Engine");
            eng.transform.SetParent(root.transform, false);
            fx.engine = eng.AddComponent<AudioSource>();
            fx.engine.loop = true;
            fx.engine.playOnAwake = false;
            fx.engine.spatialBlend = 1f;
            fx.engine.minDistance = 4f;
            fx.engine.maxDistance = 55f;
            fx.engine.rolloffMode = AudioRolloffMode.Linear;
            fx.engine.dopplerLevel = 0.3f;
            fx.engine.volume = 0f;
            var seat = new GameObject("Seat").transform;
            seat.SetParent(root.transform, false);
            seat.localPosition = new Vector3(W * 0.2f, Mathf.Min(0.45f, H * 0.18f), L > 6.5f ? L * 0.38f : -0.1f);

            var box = root.AddComponent<BoxCollider>();
            float bottom = R * 0.9f;
            box.center = new Vector3(0, (bottom + H) * 0.5f, 0);
            box.size = new Vector3(W * 0.88f, H - bottom, L * 0.97f);

            if (!Application.isPlaying)
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;

            if (gameplay)
            {
                var rb = root.AddComponent<Rigidbody>();
                rb.mass = s.mass;
                rb.isKinematic = true;
                var lights = new Light[2];
                for (int i = 0; i < 2; i++)
                {
                    var lg = new GameObject("Headlight" + i);
                    lg.transform.SetParent(root.transform, false);
                    lg.transform.localPosition = new Vector3((i == 0 ? -1 : 1) * W * 0.3f, R + 0.45f, L * 0.5f + 0.1f);
                    lg.transform.localRotation = Quaternion.Euler(6, 0, 0);
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.range = 60f;
                    l.spotAngle = 62f;
                    l.intensity = 16f;
                    l.color = new Color(1f, 0.95f, 0.85f);
                    l.enabled = false;
                    lights[i] = l;
                }
                root.AddComponent<CarController>().Setup(type, wheels, seat, lights);
                root.AddComponent<VehicleDamage>();   // knocks: smoke, fire, blast
            }
            return root;
        }
    }
}
