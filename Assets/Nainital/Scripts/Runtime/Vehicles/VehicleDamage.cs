using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Crash damage for every drivable vehicle (AI traffic, parked cars, the player's car). User, 2026-11-12: "4-5 hit pe
    /// black smoke and 7-8 pe fire or blast". Each real knock counts once (a change of speed of more than ~2.5 m/s,
    /// not the wheels landing): after 4 or 5 of them black smoke pours out of the bonnet, after 7 or 8 the engine bay
    /// catches fire (the driver jumps out and runs), and some seconds later the car blows up: a fireball, a bang, the
    /// cars and people near it thrown about, then a burnt-out shell that smoulders. A hard knock counts double.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleDamage : MonoBehaviour
    {
        public int hits;
        public bool Burning { get; private set; }
        public bool Wrecked { get; private set; }

        int smokeAt, fireAt;
        float cooldown, burnLeft, flicker, wreckAge;
        Rigidbody rb;
        CarController car;
        ParticleSystem smoke, fire;
        Light fireLight;
        AudioSource fireLoop;
        Vector3 bonnet;

        static Material smokeMat, fireMat;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            car = GetComponent<CarController>();
            smokeAt = Random.Range(4, 6);   // 4 or 5
            fireAt = Random.Range(7, 9);    // 7 or 8
            var info = GetComponent<VehicleInfo>();
            float L = info ? info.length : 4.2f, H = info ? info.height : 1.5f;
            bonnet = new Vector3(0f, H * 0.72f, L * 0.36f);
        }

        void OnCollisionEnter(Collision c)
        {
            if (Wrecked || rb.isKinematic && c.rigidbody == null) return;
            int layer = c.collider.gameObject.layer;
            if (layer == 9 || layer == 8) return;   // people do not dent a car
            float dv = c.impulse.magnitude / Mathf.Max(rb.mass, 1f);
            if (dv < 0.5f) dv = Vector3.Project(c.relativeVelocity, c.contactCount > 0 ? c.GetContact(0).normal : Vector3.forward).magnitude * 0.5f;
            if (c.contactCount > 0 && c.GetContact(0).normal.y > 0.75f && c.rigidbody == null) return;   // landing on the road, not a knock
            if (dv < 2.5f || cooldown > 0f) return;
            cooldown = 0.6f;
            Vector3 at = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            var au = AudioManager.Instance;
            if (au) au.PlayAt(au.Clip("crash_1"), at, Mathf.Clamp01(0.35f + dv * 0.06f), Random.Range(0.85f, 1.15f), 90f);
            Hit(dv > 9f ? 2 : 1);
            var tc = GetComponent<TrafficCar>();
            if (tc && !Burning) tc.Shaken();
        }

        /// <summary>Count knocks (also from a nearby blast).</summary>
        public void Hit(int n)
        {
            if (Wrecked) return;
            hits += n;
            if (hits >= smokeAt && !smoke) smoke = Smoke(transform, bonnet, false);
            if (hits >= fireAt && !Burning) Ignite();
        }

        void Ignite()
        {
            Burning = true;
            burnLeft = Random.Range(5f, 8f);
            fire = Fire(transform, bonnet);
            var lg = new GameObject("Fire light");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = bonnet + Vector3.up * 0.6f;
            fireLight = lg.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.55f, 0.18f);
            fireLight.range = 12f;
            fireLight.intensity = 0f;
            fireLight.shadows = LightShadows.None;
            var au = AudioManager.Instance;
            if (au)
            {
                au.PlayAt(au.Clip("fire_ignite"), transform.TransformPoint(bonnet), 0.8f, 1f, 80f);
                fireLoop = gameObject.AddComponent<AudioSource>();
                fireLoop.clip = au.Clip("loop_fire");
                fireLoop.loop = true;
                fireLoop.spatialBlend = 1f;
                fireLoop.minDistance = 3f;
                fireLoop.maxDistance = 45f;
                fireLoop.rolloffMode = AudioRolloffMode.Linear;
                fireLoop.volume = 0.7f * au.masterVolume;
                if (fireLoop.clip) fireLoop.Play();
            }
            var tc = GetComponent<TrafficCar>();
            if (tc) tc.Wreck();
            if (car && car.HasDriver) GameEvents.ShowToast("Gaadi mein aag! Jaldi bahar niklo (E)", 3f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            cooldown -= dt;
            if (Burning && !Wrecked)
            {
                burnLeft -= dt;
                flicker += dt * 17f;
                if (fireLight) fireLight.intensity = 9f + Mathf.PerlinNoise(flicker, 0.3f) * 9f;
                if (burnLeft <= 0f) Explode();
            }
            else if (Wrecked)
            {
                wreckAge += dt;
                if (fireLight) fireLight.intensity = Mathf.Max(0f, fireLight.intensity - dt * 2f);
                if (wreckAge > 14f && fire && fire.isEmitting) fire.Stop();
                if (wreckAge > 14f && fireLoop && fireLoop.isPlaying) fireLoop.Stop();
                var cam = Camera.main;   // a burnt shell is taken away once nobody is looking
                if (wreckAge > 45f && cam && (cam.transform.position - transform.position).sqrMagnitude > 160f * 160f) Destroy(gameObject);
            }
        }

        void Explode()
        {
            Wrecked = true;
            Vector3 c = transform.TransformPoint(bonnet * 0.5f);
            var au = AudioManager.Instance;
            if (au) au.PlayAt(au.Clip("explode_1"), c, 1f, Random.Range(0.92f, 1.05f), 320f);
            Fireball(c);

            // the driver's seat is no place to be
            if (car && car.HasDriver)
            {
                car.ForceExit();
                var p = PlayerController.Instance;
                var h = p ? p.GetComponent<Health>() : null;
                if (h) h.Damage(45f, c, gameObject);
            }
            if (car) { car.wrecked = true; car.ai = false; }

            // throw things about
            var cols = Physics.OverlapSphere(c, 10f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var col in cols)
            {
                var body = col.attachedRigidbody;
                if (body && body != rb)
                {
                    if (body.isKinematic && body.GetComponent<CarController>()) body.isKinematic = false;
                    if (!body.isKinematic) body.AddExplosionForce(body.mass * 9f, c, 10f, 1.2f, ForceMode.Impulse);
                    var other = body.GetComponent<VehicleDamage>();
                    if (other && Vector3.Distance(body.position, c) < 6.5f) other.Hit(3);
                }
                float d = Vector3.Distance(col.transform.position, c);
                var npc = col.GetComponent<NpcWalker>();
                if (npc && d < 9f) npc.Knock((col.transform.position - c).normalized * 7f + Vector3.up * 5f);
                var hp = col.GetComponent<Health>();
                if (hp && d < 8f && !npc) hp.Damage(Mathf.Lerp(60f, 10f, d / 8f), c, gameObject);
            }
            rb.AddForce(Vector3.up * rb.mass * 5.5f, ForceMode.Impulse);
            rb.AddTorque(Random.onUnitSphere * rb.mass * 1.5f, ForceMode.Impulse);

            // a burnt shell
            var mpb = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(ColorId, new Color(0.09f, 0.08f, 0.075f));
                r.SetPropertyBlock(mpb);
            }
            foreach (var l in GetComponentsInChildren<Light>()) if (l != fireLight) l.enabled = false;
            var fx = GetComponent<VehicleFX>();
            if (fx) { if (fx.engine) fx.engine.Stop(); fx.enabled = false; }
            var lamps = GetComponent<VehicleLamps>();
            if (lamps) lamps.enabled = false;
            if (fireLight) fireLight.intensity = 30f;
            if (smoke) smoke.Stop();
            smoke = Smoke(transform, bonnet, true);   // it smoulders
        }

        // ------------------------------------------------------------------ particles
        static Material SmokeMat() => smokeMat ? smokeMat : (smokeMat = Resources.Load<Material>("VehicleFX/M_Exhaust"));
        static Material FireMat() => fireMat ? fireMat : (fireMat = Resources.Load<Material>("VehicleFX/M_Fire") ?? SmokeMat());

        static ParticleSystem Particles(Transform parent, Vector3 local, string name, Material mat, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.rotation = Quaternion.LookRotation(Vector3.up);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static ParticleSystem Smoke(Transform parent, Vector3 local, bool wreck)
        {
            var mat = SmokeMat();
            if (!mat) return null;
            var ps = Particles(parent, local, wreck ? "Smoulder" : "Engine smoke", mat, 200);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 4.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startColor = wreck ? new Color(0.45f, 0.43f, 0.42f, 1f) : new Color(0.16f, 0.16f, 0.16f, 1f);   // the material is half grey, a third opaque
            main.gravityModifier = -0.04f;
            var em = ps.emission;
            em.rateOverTime = wreck ? 18f : 34f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 14f;
            sh.radius = 0.35f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 4.5f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.4f;
            ps.Play();
            return ps;
        }

        static ParticleSystem Fire(Transform parent, Vector3 local)
        {
            var mat = FireMat();
            if (!mat) return null;
            var ps = Particles(parent, local, "Engine fire", mat, 160);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f, 1f), new Color(1f, 0.38f, 0.08f, 1f));
            main.gravityModifier = -0.15f;
            var em = ps.emission;
            em.rateOverTime = 70f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(1.0f, 0.9f, 0.1f);   // across the bonnet (the box faces up: z is height)
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.5f), new GradientColorKey(new Color(0.5f, 0.12f, 0.03f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ps.Play();
            return ps;
        }

        static void Fireball(Vector3 at)
        {
            var root = new GameObject("Explosion");
            root.transform.position = at;
            var fm = FireMat();
            if (fm)
            {
                var ps = Particles(root.transform, Vector3.zero, "Fireball", fm, 120);
                var main = ps.main;
                main.loop = false;
                main.duration = 0.4f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f);
                main.startSize = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.4f, 0.08f));
                main.gravityModifier = -0.2f;
                var em = ps.emission;
                em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 90) });
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.8f;
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.5f), new GradientColorKey(new Color(0.3f, 0.08f, 0.02f), 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                ps.Play();
            }
            var sm = SmokeMat();
            if (sm)
            {
                var ps = Particles(root.transform, Vector3.zero, "Blast smoke", sm, 80);
                var main = ps.main;
                main.loop = false;
                main.duration = 0.6f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
                main.startSize = new ParticleSystem.MinMaxCurve(2f, 3.5f);
                main.startColor = new Color(0.08f, 0.075f, 0.07f, 0.85f);
                main.gravityModifier = -0.05f;
                var em = ps.emission;
                em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 60) });
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 1.2f;
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 2.6f));
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                ps.Play();
            }
            var lg = new GameObject("Flash");
            lg.transform.SetParent(root.transform, false);
            lg.transform.localPosition = Vector3.up * 1.5f;
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.62f, 0.25f);
            l.range = 32f;
            l.intensity = 120f;
            l.shadows = LightShadows.None;
            root.AddComponent<FadeLight>().flash = l;
            Destroy(root, 6f);
        }
    }

    /// <summary>A flash that dies away (the explosion).</summary>
    public class FadeLight : MonoBehaviour
    {
        public Light flash;
        void Update() { if (flash) flash.intensity = Mathf.Max(0f, flash.intensity - Time.deltaTime * 200f); }
    }
}
