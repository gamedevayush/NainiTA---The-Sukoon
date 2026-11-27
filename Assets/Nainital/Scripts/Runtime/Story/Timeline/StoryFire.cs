using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A fire in a story set (the crashed car's engine): flames, smoke, a flickering orange light and its roar, growing
    /// over `grow` seconds from the moment it is switched on (an Activation track starts it). Play mode only; the editor
    /// shows an orange marker where it is.
    /// </summary>
    public class StoryFire : MonoBehaviour
    {
        public float grow = 30f;
        ParticleSystem flames;
        Light glow;
        AudioSource loop;
        float age;

        void OnEnable()
        {
            age = 0f;
            if (flames) return;
            flames = VehicleDamage.Fire(transform, Vector3.zero);
            VehicleDamage.Smoke(transform, new Vector3(0f, 0.6f, -0.2f), true);
            var lg = new GameObject("Fire light");
            lg.transform.SetParent(transform, false);
            lg.transform.localPosition = Vector3.up * 0.8f;
            glow = lg.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.55f, 0.18f);
            glow.range = 12f;
            loop = gameObject.AddComponent<AudioSource>();
            loop.clip = Resources.Load<AudioClip>("Audio/loop_fire");
            loop.loop = true; loop.spatialBlend = 1f; loop.minDistance = 3f; loop.maxDistance = 60f;
            if (loop.clip) loop.Play();
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true)) { var mn = ps.main; mn.useUnscaledTime = true; }
            var ign = Resources.Load<AudioClip>("Audio/fire_ignite");
            if (ign) AudioSource.PlayClipAtPoint(ign, transform.position, GameSettings.Master);
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(age / Mathf.Max(0.1f, grow));
            if (flames)
            {
                var em = flames.emission; em.rateOverTime = Mathf.Lerp(25f, 140f, u);
                var main = flames.main; main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.4f, 1.0f, u), Mathf.Lerp(0.9f, 2.0f, u));
            }
            if (glow) glow.intensity = Mathf.Lerp(1.5f, 6f, u) * (0.85f + Mathf.PerlinNoise(Time.unscaledTime * 9f, 0.3f) * 0.3f);
            if (loop) loop.volume = Mathf.Lerp(0.3f, 0.9f, u) * GameSettings.Master;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }
}
