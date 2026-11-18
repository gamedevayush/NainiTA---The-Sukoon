using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// A policeman's service pistol (user, 2026-11-18: "police will have basic pistol"): a small black gun in the right
    /// hand while he aims (hidden in the holster otherwise), a muzzle flash, a short tracer and the shot's sound
    /// (Mixkit "Game gun shot", Resources/Audio/gun_pistol). Placed after the rig has posed the arm, every frame.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class Pistol : MonoBehaviour
    {
        public HumanoidRig rig;
        public bool drawn;
        Transform gun, flash;
        LineRenderer tracer;
        float flashT, tracerT;
        static Mesh gunMesh, quadMesh;
        static AudioClip shot;

        public Vector3 Muzzle => gun ? gun.TransformPoint(new Vector3(0f, 0.045f, 0.2f)) : transform.position + Vector3.up * 1.4f;

        public static Pistol Give(GameObject owner, HumanoidRig rig)
        {
            var p = owner.AddComponent<Pistol>();
            p.rig = rig;
            p.Build();
            return p;
        }

        static Mesh GunMesh()
        {
            if (gunMesh) return gunMesh;
            var mb = new MeshBuilder();
            Vector2 black = Atlas.ExtraUV(Px.Tyre), steel = Atlas.ExtraUV(Px.CarBlack);
            mb.Box(new Vector3(0f, 0.045f, 0.075f), new Vector3(0.032f, 0.032f, 0.19f), steel);        // slide
            mb.Box(new Vector3(0f, 0.022f, 0.06f), new Vector3(0.028f, 0.018f, 0.15f), black);         // frame
            mb.Box(new Vector3(0f, -0.03f, 0f), new Vector3(0.03f, 0.1f, 0.045f), Quaternion.Euler(-14f, 0f, 0f), black);   // grip
            mb.Box(new Vector3(0f, 0.008f, 0.035f), new Vector3(0.008f, 0.02f, 0.04f), black);         // trigger guard
            gunMesh = mb.ToMesh("Pistol");
            return gunMesh;
        }

        static Mesh QuadMesh()
        {
            if (quadMesh) return quadMesh;
            quadMesh = new Mesh { name = "FlashQuad" };
            quadMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quadMesh.RecalculateBounds();
            return quadMesh;
        }

        void Build()
        {
            var kit = PoliceKit.Instance;
            gun = new GameObject("Pistol").transform;
            gun.SetParent(transform, false);
            gun.gameObject.AddComponent<MeshFilter>().sharedMesh = GunMesh();
            var mr = gun.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GameAssets.AtlasMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gun.gameObject.SetActive(false);

            flash = new GameObject("Flash").transform;
            flash.SetParent(gun, false);
            flash.localPosition = new Vector3(0f, 0.045f, 0.24f);
            flash.gameObject.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            var fr = flash.gameObject.AddComponent<MeshRenderer>();
            fr.sharedMaterial = kit && kit.muzzle ? kit.muzzle : GameAssets.AtlasMaterial;
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flash.gameObject.SetActive(false);

            var tg = new GameObject("Tracer");
            tg.transform.SetParent(transform, false);
            tracer = tg.AddComponent<LineRenderer>();
            tracer.positionCount = 2;
            tracer.useWorldSpace = true;
            tracer.startWidth = 0.025f;
            tracer.endWidth = 0.008f;
            tracer.sharedMaterial = kit && kit.lampWhite ? kit.lampWhite : GameAssets.AtlasMaterial;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            tracer.enabled = false;
            if (!shot) shot = Resources.Load<AudioClip>("Audio/gun_pistol");
        }

        void LateUpdate()
        {
            bool show = drawn && rig && !rig.dead;
            if (gun.gameObject.activeSelf != show) gun.gameObject.SetActive(show);
            if (show)
            {
                // in the palm, along the arm's aim (the character's facing, tipped by the rig's aim pitch)
                Transform hand = rig.RightHand;
                Vector3 fwd = Quaternion.AngleAxis(-rig.aimPitch, transform.right) * transform.forward;
                Vector3 at = hand.position;
                if (!rig.HandIsBone) at -= hand.up * 0.27f;   // the code-built body's forearm joint: the hand is further down
                gun.SetPositionAndRotation(at + fwd * 0.08f + Vector3.up * 0.01f, Quaternion.LookRotation(fwd, Vector3.up));
            }
            if (flashT > 0f)
            {
                flashT -= Time.deltaTime;
                var cam = Camera.main;
                if (cam) flash.rotation = Quaternion.LookRotation(flash.position - cam.transform.position);
                flash.localScale = Vector3.one * Random.Range(0.16f, 0.24f);
                if (flashT <= 0f) flash.gameObject.SetActive(false);
            }
            if (tracerT > 0f)
            {
                tracerT -= Time.deltaTime;
                if (tracerT <= 0f) tracer.enabled = false;
            }
        }

        /// <summary>Bang: flash, sound, a tracer to `end` (where the bullet went).</summary>
        public void Shoot(Vector3 end)
        {
            Vector3 m = Muzzle;
            if (gun.gameObject.activeSelf)
            {
                flash.gameObject.SetActive(true);
                flashT = 0.06f;
            }
            tracer.SetPosition(0, m + (end - m).normalized * 0.4f);
            tracer.SetPosition(1, end);
            tracer.enabled = true;
            tracerT = 0.05f;
            if (AudioManager.Instance && shot) AudioManager.Instance.PlayAt(shot, m, 1f, Random.Range(0.94f, 1.06f), 220f);
        }
    }
}
