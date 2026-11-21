using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Fires for the 101 fire brigade (owner, 21 Nov 2026: "fires: burning vehicles + random fires"). Burning vehicles come
    /// from VehicleDamage (a burnt-out wreck now keeps burning about 2.5 minutes, so a tender called in time still has work);
    /// now and then a heap of rubbish / a stack of wood by a road catches fire somewhere near the player (FireSpot): black
    /// smoke going up, a toast, and it burns until the fire brigade puts it out or about six minutes pass.
    /// </summary>
    public class FireManager : MonoBehaviour
    {
        public static FireManager Instance { get; private set; }
        float next;

        public static FireManager Ensure()
        {
            if (!Instance) Instance = new GameObject("FireManager").AddComponent<FireManager>();
            return Instance;
        }

        void Awake() { Instance = this; next = Time.time + Random.Range(240f, 420f); }

        /// <summary>The nearest flames (a burning vehicle or a FireSpot) within r of p.</summary>
        public static bool NearestFire(Vector3 p, float r, out Vector3 at, out Component src)
        {
            at = p; src = null;
            float bd = r * r;
            foreach (var v in VehicleDamage.All)
            {
                if (!v || !v.OnFire) continue;
                float d = (v.FirePoint - p).sqrMagnitude;
                if (d < bd) { bd = d; at = v.FirePoint; src = v; }
            }
            foreach (var f in FireSpot.All)
            {
                if (!f || !f.Burning) continue;
                float d = (f.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; at = f.transform.position; src = f; }
            }
            return src;
        }

        public static void Extinguish(Component src)
        {
            if (src is VehicleDamage v) v.Extinguish();
            else if (src is FireSpot f) f.Extinguish();
        }

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + Random.Range(480f, 840f);
            var p = PlayerController.Instance;
            if (!p || FrontEnd.MenuOpen || FireSpot.All.Count > 0) return;
            if (WeatherSystem.Instance && WeatherSystem.Instance.Rain > 0.3f) { next = Time.time + 120f; return; }
            if (TryStart(p.transform.position))
                GameEvents.ShowToast("Kahin aag lagi hai - kaala dhuan uth raha hai! Phone (T) se 101 milao", 5f);
            else next = Time.time + 60f;
        }

        /// <summary>A fire by a road 90-180 m from p, on open ground (not in the lake, not inside a house).</summary>
        public static bool TryStart(Vector3 p)
        {
            if (!RoadRoute.ReadyAny()) return false;
            for (int t = 0; t < 30; t++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(90f, 180f);
                int k = RoadRoute.Nearest(p + new Vector3(r.x, 0f, r.y), 40f);
                if (k < 0) continue;
                Vector3 c = RoadRoute.Pos(k);
                Vector3 f = RoadRoute.Heading(k);
                Vector3 side = Vector3.Cross(Vector3.up, f).normalized * (Random.value < 0.5f ? -1f : 1f);
                Vector3 at = c + side * (RoadRoute.Width(k) * 0.5f + Random.Range(2.5f, 4.5f));
                if (!Ground(ref at)) continue;
                if (Mathf.Abs(at.y - c.y) > 2.5f) continue;   // not down a drop or up a bank
                if (Blocked(at)) continue;   // a wall, a car, a stall (the ground itself does not count)
                if (LakeArea.Contains(at)) continue;
                FireSpot.Create(at, Quaternion.LookRotation(-side));
                return true;
            }
            return false;
        }

        static readonly Collider[] hits = new Collider[16];
        static bool Blocked(Vector3 at)
        {
            int n = Physics.OverlapCapsuleNonAlloc(at + Vector3.up * 1.2f, at + Vector3.up * 2.4f, 1.1f, hits, ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!(hits[i] is TerrainCollider)) return true;
            return false;
        }

        static bool Ground(ref Vector3 p)
        {
            var t = Terrain.activeTerrain;
            float top = t ? t.SampleHeight(p) + t.transform.position.y : p.y;
            if (Physics.Raycast(new Vector3(p.x, top + 6f, p.z), Vector3.down, out var hit, 14f, ~((1 << 2) | (1 << 4) | (1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore))
            {
                if (!(hit.collider is TerrainCollider) && hit.normal.y < 0.8f) return false;   // a roof, a wall top
                p = hit.point;
                return true;
            }
            p.y = top;
            return t;
        }
    }

    /// <summary>A heap (rubbish, old wood, a broken cart) on fire by a road. FireManager starts one; FireTruck puts it out.</summary>
    public class FireSpot : MonoBehaviour
    {
        public static readonly List<FireSpot> All = new List<FireSpot>();
        public bool Burning { get; private set; } = true;
        ParticleSystem flames, smoke;
        Light glow;
        AudioSource loop;
        float age, flicker, outAge = -1f;
        const float Life = 360f;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static FireSpot Create(Vector3 at, Quaternion rot)
        {
            var go = new GameObject("Fire (heap)");
            go.transform.SetPositionAndRotation(at, rot);
            // the heap: charred planks, sacks and a broken crate (atlas colours, no collider worth driving into)
            var mb = new MeshBuilder();
            Vector2 coal = Atlas.ExtraUV(Px.Tyre), wood = Atlas.SolidUV(Pal.WoodDark),
                    sack = Atlas.SolidUV(Pal.PantsKhaki), ash = Atlas.SolidUV(Pal.StoneGrey);
            mb.Box(new Vector3(0f, 0.04f, 0f), new Vector3(3.0f, 0.08f, 2.4f), ash);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 40f;
                var q = Quaternion.Euler(Random.Range(-25f, 25f), a, Random.Range(-15f, 15f));
                Vector3 c = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.25f + (i % 3) * 0.12f, Random.Range(0.1f, 0.9f));
                mb.Box(c, new Vector3(0.14f, 0.12f, Random.Range(1.0f, 1.8f)), q, i % 3 == 0 ? coal : wood);
            }
            mb.Box(new Vector3(0.7f, 0.25f, -0.5f), new Vector3(0.7f, 0.5f, 0.5f), sack);
            mb.Box(new Vector3(-0.8f, 0.3f, 0.4f), new Vector3(0.6f, 0.6f, 0.6f), coal);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Fire heap");
            go.AddComponent<MeshRenderer>().sharedMaterial = GameAssets.AtlasMaterial;
            var fs = go.AddComponent<FireSpot>();
            fs.Light();
            return fs;
        }

        void Light()
        {
            flames = VehicleDamage.Fire(transform, new Vector3(0f, 0.3f, 0f));
            if (flames)
            {
                var main = flames.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.6f);
                var em = flames.emission;
                em.rateOverTime = 110f;
                var sh = flames.shape;
                sh.radius = 0.9f;
            }
            smoke = VehicleDamage.Smoke(transform, new Vector3(0f, 1.6f, 0f), false);
            if (smoke)
            {
                var main = smoke.main;
                main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
                main.maxParticles = 420;
                var em = smoke.emission;
                em.rateOverTime = 42f;
            }
            var lg = new GameObject("Fire light");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = Vector3.up * 1.2f;
            glow = lg.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.55f, 0.18f);
            glow.range = 14f;
            glow.shadows = LightShadows.None;
            var au = AudioManager.Instance;
            if (au)
            {
                loop = gameObject.AddComponent<AudioSource>();
                loop.clip = au.Clip("loop_fire");
                loop.loop = true;
                loop.spatialBlend = 1f;
                loop.minDistance = 3f;
                loop.maxDistance = 50f;
                loop.rolloffMode = AudioRolloffMode.Linear;
                loop.volume = 0.8f * au.masterVolume;
                if (loop.clip) loop.Play();
            }
        }

        public void Extinguish()
        {
            if (!Burning) return;
            Burning = false;
            outAge = 0f;
            if (flames) flames.Stop();
            if (loop) loop.Stop();
            if (smoke)   // white steam, then nothing
            {
                var main = smoke.main;
                main.startColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                var em = smoke.emission;
                em.rateOverTime = 14f;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (Burning)
            {
                flicker += dt * 15f;
                if (glow) glow.intensity = 10f + Mathf.PerlinNoise(flicker, 0.7f) * 10f;
                if (age > Life) Extinguish();   // burnt out by itself
                return;
            }
            outAge += dt;
            if (glow) glow.intensity = Mathf.Max(0f, glow.intensity - dt * 8f);
            if (outAge > 12f && smoke && smoke.isEmitting) smoke.Stop();
            var cam = Camera.main;   // the black heap goes once nobody is looking
            if (outAge > 60f && cam && (cam.transform.position - transform.position).sqrMagnitude > 150f * 150f) Destroy(gameObject);
        }
    }
}
