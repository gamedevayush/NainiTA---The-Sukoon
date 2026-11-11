using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nainital
{
    /// <summary>
    /// Cheat SAFAR (or F2): a list of every named place the location tracker knows - the main ones first - and a click
    /// puts the player there, on open ground near its centre (not on a roof, not in the lake).
    /// </summary>
    public class TeleportMenu : MonoBehaviour
    {
        static TeleportMenu inst;

        // the places people go first, in this order; everything else follows alphabetically
        static readonly string[] Main =
        {
            "Mallital", "Tallital", "Mall Road", "Naina Devi Mandir", "The Flats", "Bada Bazar", "Snow View",
            "Naini Peak (China Peak)", "Tiffin Top (Dorothy's Seat)", "Land's End", "Himalaya Darshan", "Eco Cave Garden",
            "Nainital Zoo", "Boat House Club", "Raj Bhavan", "St. Francis Church", "Gurudwara Sri Guru Singh Sabha",
        };

        bool open;
        Vector2 scroll;
        readonly List<string> names = new List<string>();
        readonly List<Vector3> centers = new List<Vector3>();
        GUIStyle btn, head;

        public static void Open()
        {
            if (!inst) inst = new GameObject("Teleport menu").AddComponent<TeleportMenu>();
            inst.Show();
        }

        void Show()
        {
            var lt = FindFirstObjectByType<LocationTracker>();
            if (!lt || lt.names == null || lt.names.Length == 0) { GameEvents.ShowToast("Jagahon ki list nahi mili", 2f); return; }
            names.Clear();
            centers.Clear();
            var seen = new HashSet<string>();
            void Add(int i)
            {
                string n = lt.names[i];
                if (string.IsNullOrEmpty(n) || !seen.Add(n)) return;
                names.Add(n);
                centers.Add(lt.centers[i]);
            }
            foreach (var m in Main)
                for (int i = 0; i < lt.names.Length; i++)
                    if (lt.names[i] == m) { Add(i); break; }
            var rest = new List<int>();
            for (int i = 0; i < lt.names.Length; i++) if (!seen.Contains(lt.names[i])) rest.Add(i);
            rest.Sort((a, b) => string.CompareOrdinal(lt.names[a], lt.names[b]));
            foreach (int i in rest) Add(i);
            if (TouchControls.Instance)   // on screen controls are up: the touch list
            {
                TouchControls.Instance.ShowPlaces(names, Go);   // big buttons in a scrolling list
                return;
            }
            open = true;
            scroll = Vector2.zero;
            GameInput.UiBlocking = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Close()
        {
            open = false;
            GameInput.UiBlocking = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (open && kb != null && kb.escapeKey.wasPressedThisFrame) Close();
        }

        void OnGUI()
        {
            if (!open) return;
            float s = Mathf.Max(1f, Screen.height / 720f);
            if (btn == null)
            {
                btn = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
                head = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            btn.fontSize = Mathf.RoundToInt(16 * s);
            head.fontSize = Mathf.RoundToInt(18 * s);
            var r = new Rect(Screen.width * 0.5f - 230 * s, Screen.height * 0.08f, 460 * s, Screen.height * 0.84f);
            GUI.Box(r, "");
            GUI.Label(new Rect(r.x, r.y + 6 * s, r.width, 30 * s), "Kahan jaana hai? (Safar)", head);
            float rowH = 38 * s;
            var view = new Rect(r.x + 10 * s, r.y + 42 * s, r.width - 20 * s, r.height - 96 * s);
            var content = new Rect(0, 0, view.width - 22 * s, names.Count * (rowH + 4 * s));
            scroll = GUI.BeginScrollView(view, scroll, content);
            for (int i = 0; i < names.Count; i++)
                if (GUI.Button(new Rect(0, i * (rowH + 4 * s), content.width, rowH), "  " + names[i], btn)) { Go(i); break; }
            GUI.EndScrollView();
            if (GUI.Button(new Rect(r.x + r.width * 0.3f, r.yMax - 48 * s, r.width * 0.4f, 40 * s), "Band karo")) Close();
        }

        void Go(int i)
        {
            var p = PlayerController.Instance;
            if (!p) { Close(); return; }
            if (p.CurrentMode != PlayerController.Mode.Normal) { GameEvents.ShowToast("Pehle gaadi / naav / seat se utro", 2.5f); return; }
            if (!FindGround(centers[i], out var at)) { GameEvents.ShowToast("Wahan khuli jagah nahi mili", 2.5f); return; }
            Close();
            var motor = p.GetComponent<CharacterMotor>();
            if (motor) { motor.velocity = Vector3.zero; motor.Teleport(at); }
            else p.transform.position = at;
            GameEvents.ShowToast("Pahunch gaye: " + names[i], 2.5f);
        }

        /// <summary>Open, walkable ground near c: on the terrain or a deck just above it (not a roof), dry, with room to stand.</summary>
        static bool FindGround(Vector3 c, out Vector3 at)
        {
            at = c;
            var terrain = Terrain.activeTerrain;
            int mask = ~((1 << 8) | (1 << 9) | (1 << 2) | (1 << 4));
            for (int ring = 0; ring <= 6; ring++)
            {
                int steps = ring == 0 ? 1 : 10;
                for (int k = 0; k < steps; k++)
                {
                    float a = k * Mathf.PI * 2f / steps + ring * 0.37f;
                    Vector3 q = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (ring * 5f);
                    if (!Physics.Raycast(new Vector3(q.x, c.y + 400f, q.z), Vector3.down, out var hit, 1200f, mask, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < 0.75f) continue;
                    if (terrain && hit.point.y > terrain.SampleHeight(hit.point) + terrain.transform.position.y + 1.6f) continue;   // a roof
                    if (LakeArea.Contains(hit.point) && hit.point.y < LakeArea.WaterY + 0.3f) continue;
                    Vector3 feet = hit.point + Vector3.up * 0.1f;
                    if (Physics.CheckCapsule(feet + Vector3.up * 0.45f, feet + Vector3.up * 1.6f, 0.35f, mask, QueryTriggerInteraction.Ignore)) continue;
                    at = feet;
                    return true;
                }
            }
            return false;
        }
    }
}
