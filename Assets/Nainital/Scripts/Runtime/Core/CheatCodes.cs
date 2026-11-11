using UnityEngine;
using UnityEngine.InputSystem;

namespace Nainital
{
    /// <summary>
    /// Cheat box: ` (backquote) or F1 on a keyboard, or three quick taps in the top-right corner on a phone.
    /// Codes: JEEVAN full health, AMAR god mode on/off, GAADI a car, TAXI a taxi, BUS a bus, JEEP an SUV,
    /// UDAAN a jetpack on/off, SAFAR a list of places to teleport to (also F2).
    /// </summary>
    public class CheatCodes : MonoBehaviour
    {
        bool open;
        string code = "";
        int taps;
        float tapTime;
        GUIStyle box, field, label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindFirstObjectByType<PlayerController>() && !FindFirstObjectByType<CheatCodes>())
                new GameObject("Cheat codes").AddComponent<CheatCodes>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.backquoteKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame)) Toggle();
            if (kb != null && kb.f2Key.wasPressedThisFrame && !open) TeleportMenu.Open();
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
            {
                Vector2 p = ts.primaryTouch.position.ReadValue();
                if (p.x > Screen.width * 0.88f && p.y > Screen.height * 0.88f)
                {
                    taps = Time.unscaledTime - tapTime < 0.6f ? taps + 1 : 1;
                    tapTime = Time.unscaledTime;
                    if (taps >= 3)
                    {
                        taps = 0;
                        if (TouchControls.Instance) TouchControls.Instance.ToggleCheats();   // buttons: no keyboard needed
                        else Toggle();
                    }
                }
            }
            if (open && kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) Toggle();
                else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Apply();
            }
        }

        void Toggle()
        {
            open = !open;
            code = "";
            GameInput.UiBlocking = open;
            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void OnGUI()
        {
            if (!open) return;
            float s = Mathf.Max(1f, Screen.height / 720f);
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box);
                field = new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleCenter };
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            }
            field.fontSize = Mathf.RoundToInt(26 * s);
            label.fontSize = Mathf.RoundToInt(15 * s);
            var r = new Rect(Screen.width * 0.5f - 230 * s, Screen.height * 0.3f, 460 * s, 150 * s);
            GUI.Box(r, "", box);
            GUI.Label(new Rect(r.x, r.y + 8 * s, r.width, 26 * s), "Cheat code likho, phir Enter", label);
            GUI.SetNextControlName("cheat");
            code = GUI.TextField(new Rect(r.x + 20 * s, r.y + 40 * s, r.width - 40 * s, 46 * s), code, 16, field);
            GUI.FocusControl("cheat");
            if (GUI.Button(new Rect(r.x + 20 * s, r.y + 96 * s, (r.width - 50 * s) * 0.5f, 40 * s), "OK")) Apply();
            if (GUI.Button(new Rect(r.x + 30 * s + (r.width - 50 * s) * 0.5f, r.y + 96 * s, (r.width - 50 * s) * 0.5f, 40 * s), "Band karo")) Toggle();
            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) { Apply(); e.Use(); }
        }

        void Apply()
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in code.ToUpperInvariant()) if (ch >= 'A' && ch <= 'Z') sb.Append(ch);   // drops the ` that opened the box
            string c = sb.ToString();
            Toggle();
            Run(c);
        }

        /// <summary>Carry out one cheat code (typed in the box, or a button in the phone's cheat panel).</summary>
        public static void Run(string c)
        {
            var p = PlayerController.Instance;
            if (!p) return;
            var health = p.GetComponent<Health>();
            switch (c)
            {
                case "JEEVAN":
                    if (health) health.Revive(1f);
                    GameEvents.ShowToast("Cheat: poori health", 2f);
                    break;
                case "AMAR":
                    if (health)
                    {
                        health.invulnerable = !health.invulnerable;
                        health.Revive(1f);
                        GameEvents.ShowToast(health.invulnerable ? "Cheat: AMAR - ab chot nahi lagegi" : "AMAR band", 2.5f);
                    }
                    break;
                case "GAADI": Spawn(p, Random.value < 0.5f ? CarType.Sedan : CarType.Hatchback); break;
                case "TAXI": Spawn(p, CarType.Taxi); break;
                case "JEEP": Spawn(p, CarType.Suv); break;
                case "BUS": Spawn(p, CarType.Bus); break;
                case "UDAAN": Jetpack.Toggle(p); break;
                case "SAFAR": TeleportMenu.Open(); break;
                default:
                    if (c.Length > 0) GameEvents.ShowToast("Galat code: " + c, 2f);
                    break;
            }
        }

        static void Spawn(PlayerController p, CarType type)
        {
            if (CarController.Driving) { GameEvents.ShowToast("Pehle gaadi se utro", 2f); return; }
            var traffic = FindFirstObjectByType<TrafficManager>();
            var mat = traffic ? traffic.material : null;
            if (!mat) { GameEvents.ShowToast("Gaadi nahi ban saki", 2f); return; }
            var spec = VehicleFactory.GetSpec(type);
            Vector3 fwd = p.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 best = Vector3.zero;
            bool found = false;
            foreach (float side in new[] { 0f, 1f, -1f })
            {
                Vector3 at = p.transform.position + fwd * (spec.length * 0.5f + 2.5f) + right * side * (spec.width + 1f);
                if (!Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out var hit, 80f, ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore)) continue;
                Vector3 c = hit.point + Vector3.up * (spec.height * 0.5f + 0.15f);
                var half = new Vector3(spec.width * 0.5f, spec.height * 0.4f, spec.length * 0.5f);
                bool blocked = false;
                foreach (var h in Physics.OverlapBox(c, half, Quaternion.LookRotation(fwd), ~((1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore))
                    if (!(h is TerrainCollider)) { blocked = true; break; }
                if (blocked) continue;
                best = hit.point + Vector3.up * 0.1f;
                found = true;
                break;
            }
            if (!found) { GameEvents.ShowToast("Yahan jagah nahi hai, khuli jagah pe try karo", 2.5f); return; }
            int paint = type == CarType.Taxi ? Px.CarWhite : VehicleFactory.CarPaints[Random.Range(0, VehicleFactory.CarPaints.Length)];
            var go = VehicleFactory.Create(type, paint, mat, true, "Cheat " + type);
            go.transform.SetPositionAndRotation(best, Quaternion.LookRotation(fwd));
            GameEvents.ShowToast("Cheat: " + type + " aa gayi - paas jaake " + (TouchControls.Instance ? "Use" : "E") + " dabao", 2.5f);
        }
    }
}
