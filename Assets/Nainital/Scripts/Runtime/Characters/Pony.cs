using UnityEngine;

namespace Nainital
{
    /// <summary>Hill pony (ghoda) - wanders at viewpoints; pay and ride it with WASD (Shift = gallop), E to get off.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CharacterMotor))]
    public class Pony : Interactable
    {
        public static Pony Ridden { get; private set; }

        public int coat = Pal.HairBrown;
        public Material material;
        public int price = 150;

        Transform model, seat;
        readonly Transform[] legs = new Transform[4];
        Transform neck;
        CharacterMotor motor;
        PlayerController rider;
        Vector3 home, target;
        float phase, think;

        public override string Prompt => rider ? "Ghode se utro" : "Ghode ki sawari (Rs " + price + ")";
        public override bool CanInteract(PlayerController p) => !rider && Ridden == null;
        public override Vector3 Point => transform.position + Vector3.up * 0.5f;

        protected override void OnEnable()
        {
            base.OnEnable();
            Build();
            if (Application.isPlaying) { motor = GetComponent<CharacterMotor>(); home = target = transform.position; radius = 2.6f; }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (!Application.isPlaying && model) { DestroyImmediate(model.gameObject); model = null; }
        }

        void Part(Transform parent, MeshBuilder mb, string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            var m = mb.ToMesh(n);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = material ? material : GameAssets.AtlasMaterial;
            if (!Application.isPlaying) { go.hideFlags = HideFlags.DontSave; m.hideFlags = HideFlags.DontSave; }
            mb.Clear();
        }

        void Build()
        {
            if (model) return;
            var old = transform.Find("PonyModel");
            if (old) { if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
            model = new GameObject("PonyModel").transform;
            model.SetParent(transform, false);
            if (!Application.isPlaying) model.gameObject.hideFlags = HideFlags.DontSave;
            Vector2 c = Atlas.SolidUV(coat), dark = Atlas.SolidUV(Pal.HairBlack), saddle = Atlas.SolidUV(Pal.JacketRed), cloth = Atlas.SolidUV(Pal.Saffron);
            var mb = new MeshBuilder();
            mb.Sphere(new Vector3(0, 1.02f, 0), new Vector3(0.32f, 0.36f, 0.78f), 9, 6, c);
            mb.Box(new Vector3(0, 1.4f, -0.05f), new Vector3(0.5f, 0.1f, 0.55f), saddle);
            mb.Box(new Vector3(0, 1.2f, -0.05f), new Vector3(0.66f, 0.42f, 0.5f), cloth);
            mb.Limb(new Vector3(0, 1.05f, -0.72f), new Vector3(0, 0.55f, -0.95f), 0.07f, 0.03f, 5, dark);
            Part(model, mb, "Body");
            neck = new GameObject("Neck").transform;
            neck.SetParent(model, false);
            neck.localPosition = new Vector3(0, 1.15f, 0.55f);
            if (!Application.isPlaying) neck.gameObject.hideFlags = HideFlags.DontSave;
            mb.Limb(Vector3.zero, new Vector3(0, 0.5f, 0.32f), 0.16f, 0.11f, 7, c);
            mb.Box(new Vector3(0, 0.55f, 0.55f), new Vector3(0.18f, 0.2f, 0.5f), Quaternion.Euler(25, 0, 0), c);
            mb.Box(new Vector3(0, 0.62f, 0.12f), new Vector3(0.06f, 0.35f, 0.3f), Quaternion.Euler(-20, 0, 0), dark);   // mane
            mb.Box(new Vector3(-0.06f, 0.72f, 0.36f), new Vector3(0.04f, 0.12f, 0.04f), c);
            mb.Box(new Vector3(0.06f, 0.72f, 0.36f), new Vector3(0.04f, 0.12f, 0.04f), c);
            Part(neck, mb, "Head");
            float[] xs = { -0.18f, 0.18f, -0.18f, 0.18f }, zs = { 0.5f, 0.5f, -0.5f, -0.5f };
            for (int i = 0; i < 4; i++)
            {
                legs[i] = new GameObject("Leg" + i).transform;
                legs[i].SetParent(model, false);
                legs[i].localPosition = new Vector3(xs[i], 0.95f, zs[i]);
                if (!Application.isPlaying) legs[i].gameObject.hideFlags = HideFlags.DontSave;
                mb.Limb(Vector3.zero, new Vector3(0, -0.85f, 0), 0.08f, 0.05f, 6, c);
                mb.Box(new Vector3(0, -0.9f, 0.02f), new Vector3(0.1f, 0.1f, 0.13f), dark);
                Part(legs[i], mb, "Leg");
            }
            seat = new GameObject("Seat").transform;
            seat.SetParent(transform, false);
            seat.localPosition = new Vector3(0, 0.92f, -0.05f);
            if (!Application.isPlaying) seat.gameObject.hideFlags = HideFlags.DontSave;
        }

        public override void Interact(PlayerController p)
        {
            if (!rider)
            {
                if (!GameState.TrySpend(price)) { GameEvents.ShowToast("Sawari ke liye Rs " + price + " chahiye!", 2.5f); return; }
                rider = p;
                Ridden = this;
                p.EnterMount(seat, transform);
                GameEvents.ShowToast("WASD se chalao, Shift se sarpat daudao, E se utro", 3f);
                return;
            }
            var r = rider;
            rider = null;
            Ridden = null;
            r.ExitMount(transform.position + transform.right * 1.2f + Vector3.up * 0.3f);
        }

        void Update()
        {
            if (!Application.isPlaying || !motor) return;
            float dt = Time.deltaTime;
            Vector3 desired = Vector3.zero;
            float speed = 1.1f;
            if (rider)
            {
                Vector2 mv = GameInput.UiBlocking ? Vector2.zero : GameInput.Move;
                var cam = ThirdPersonCamera.Instance;
                Vector3 f = cam ? cam.FlatForward : transform.forward;
                Vector3 r = new Vector3(f.z, 0, -f.x);
                desired = (f * mv.y + r * mv.x);
                speed = GameInput.SprintHeld ? 7.5f : 3.2f;
            }
            else
            {
                think -= dt;
                if (think <= 0f)
                {
                    think = Random.Range(4f, 10f);
                    Vector2 rr = Random.insideUnitCircle * 10f;
                    target = home + new Vector3(rr.x, 0, rr.y);
                }
                Vector3 to = target - transform.position;
                to.y = 0;
                if (to.magnitude > 1f) desired = to.normalized * 0.6f;
            }
            motor.Move(Vector3.ClampMagnitude(desired, 1f) * speed, 6f, dt);
            if (desired.sqrMagnitude > 0.01f) motor.FaceTowards(desired, 160f, dt);
            float v = motor.HorizontalVelocity.magnitude;
            phase += dt * v * 2.6f;
            float amp = Mathf.Clamp01(v / 2f) * (v > 4f ? 38f : 24f);
            for (int i = 0; i < 4; i++)
            {
                float off = (i == 0 || i == 3) ? 0f : Mathf.PI;
                if (legs[i]) legs[i].localRotation = Quaternion.Euler(Mathf.Sin(phase + off) * amp, 0, 0);
            }
            if (neck) neck.localRotation = Quaternion.Euler(Mathf.Sin(phase * 2f) * 4f * Mathf.Clamp01(v / 3f) + (rider ? 0f : 18f * (1f - Mathf.Clamp01(v)) ), 0, 0);
            if (model) model.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(phase)) * (v > 4f ? 0.12f : 0.04f);
        }
    }
}
