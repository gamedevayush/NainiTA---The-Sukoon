using UnityEngine;
using UnityEngine.InputSystem;

namespace Nainital
{
    /// <summary>
    /// Cheat UDAAN: a jetpack, to fly anywhere. Move with WASD / the stick (camera-relative), Space (jump) to climb,
    /// Ctrl or C (phone: the block button) to sink, Shift (sprint) for full thrust. E, or UDAAN again, takes it off.
    /// While it is on, PlayerController hands its movement over (see Flying).
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public class Jetpack : MonoBehaviour
    {
        public static bool Flying { get; private set; }

        public float cruise = 12f, boost = 32f, climb = 9f, sink = 10f, accel = 18f;

        CharacterMotor motor;
        PlayerController player;
        Transform pack;
        readonly Transform[] flames = new Transform[2];
        float thrust;

        public static void Toggle(PlayerController p)
        {
            var jp = p.GetComponent<Jetpack>();
            if (jp && jp.enabled)
            {
                jp.enabled = false;
                GameEvents.ShowToast("Jetpack utaar diya", 2f);
                return;
            }
            if (p.CurrentMode != PlayerController.Mode.Normal)
            {
                GameEvents.ShowToast("Pehle gaadi / naav / seat se utro", 2.5f);
                return;
            }
            if (!jp) jp = p.gameObject.AddComponent<Jetpack>();
            jp.enabled = true;
            GameEvents.ShowToast(TouchControls.Instance
                ? "Cheat: UDAAN - jetpack! Upar / Neeche buttons, Daudo = tez, Utro = band"
                : "Cheat: UDAAN - jetpack! Space = upar, Ctrl/C = neeche, Shift = tez, E = utaaro", 4.5f);
        }

        void OnEnable()
        {
            motor = GetComponent<CharacterMotor>();
            player = GetComponent<PlayerController>();
            Flying = true;
            if (!pack) BuildPack();
            if (pack) pack.gameObject.SetActive(true);
            if (motor) motor.velocity.y = Mathf.Max(motor.velocity.y, 4f);   // a hop off the ground
        }

        void OnDisable()
        {
            Flying = false;
            if (pack) pack.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!player || !motor || player.CurrentMode != PlayerController.Mode.Normal) { enabled = false; return; }
            float dt = Time.deltaTime;
            bool ui = GameInput.UiBlocking;
            if (!ui && GameInput.InteractPressed)
            {
                enabled = false;
                GameEvents.ShowToast("Jetpack utaar diya", 2f);
                return;
            }
            var kb = Keyboard.current;
            Vector2 mv = ui ? Vector2.zero : GameInput.Move;
            var cam = ThirdPersonCamera.Instance;
            Vector3 f = cam ? cam.FlatForward : transform.forward;
            Vector3 r = new Vector3(f.z, 0f, -f.x);
            Vector3 wish = Vector3.ClampMagnitude(f * mv.y + r * mv.x, 1f);
            bool fast = !ui && GameInput.SprintHeld;
            bool up = !ui && (GameInput.JumpHeld || GameInput.TouchJumpHeld);
            bool down = !ui && (GameInput.BlockHeld || (kb != null && (kb.leftCtrlKey.isPressed || kb.cKey.isPressed)));
            float vy = up ? climb : down ? -sink : 0f;
            if (fast) vy *= 1.6f;

            motor.velocity.y = Mathf.MoveTowards(motor.velocity.y, vy, accel * dt);
            motor.Move(wish * (fast ? boost : cruise), accel, dt, useGravity: false);
            if (wish.sqrMagnitude > 0.01f) motor.FaceTowards(wish, 360f, dt);

            // the flames: longer when climbing or at full thrust, with a flicker
            thrust = Mathf.MoveTowards(thrust, (up ? 1f : 0.5f) + (fast ? 0.4f : 0f), 4f * dt);
            float flick = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 18f, 0.3f);
            foreach (var fl in flames)
                if (fl) fl.localScale = new Vector3(1f, thrust * flick, 1f);

            var rig = player.Rig;
            if (rig)
            {
                rig.grounded = false;
                rig.verticalSpeed = motor.velocity.y;
                rig.speed = 0f;
                rig.swimming = rig.seated = rig.rowing = false;
            }
        }

        /// <summary>Two small tanks on the back with nozzles and flames, from the atlas palette.</summary>
        void BuildPack()
        {
            var gm = FindFirstObjectByType<GameManager>();
            var mat = gm ? gm.atlasMaterial : null;
            if (!mat) return;
            pack = new GameObject("Jetpack").transform;
            pack.SetParent(transform, false);
            pack.localPosition = new Vector3(0f, 1.18f, -0.24f);
            var mb = new MeshBuilder();
            Vector2 dark = Atlas.SolidUV(Pal.IronBlack), steel = Atlas.SolidUV(Pal.Concrete), strap = Atlas.SolidUV(Pal.JacketBlack);
            foreach (float x in new[] { -0.1f, 0.1f })
            {
                mb.Cylinder(new Vector3(x, -0.22f, -0.06f), 0.075f, 0.075f, 0.44f, 10, steel, true);
                mb.Cylinder(new Vector3(x, -0.3f, -0.06f), 0.04f, 0.06f, 0.08f, 8, dark, true);   // nozzle
            }
            mb.Box(new Vector3(0f, 0f, 0.01f), new Vector3(0.3f, 0.36f, 0.05f), dark);
            mb.Box(new Vector3(0f, 0.1f, 0.12f), new Vector3(0.34f, 0.05f, 0.2f), strap);
            AddMesh(pack, "Tanks", mb.ToMesh("JetpackTanks"), mat);
            for (int i = 0; i < 2; i++)
            {
                var fl = new GameObject("Flame").transform;
                fl.SetParent(pack, false);
                fl.localPosition = new Vector3(i == 0 ? -0.1f : 0.1f, -0.3f, -0.06f);
                var fm = new MeshBuilder();
                Vector2 hot = Atlas.ExtraUV(Px.Indicator);
                fm.Cylinder(new Vector3(0f, -0.42f, 0f), 0.005f, 0.05f, 0.42f, 8, hot, false);
                AddMesh(fl, "Flame", fm.ToMesh("JetpackFlame"), mat);
                flames[i] = fl;
            }
        }

        static void AddMesh(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = parent.name == name ? parent.gameObject : new GameObject(name);
            if (go.transform != parent) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
