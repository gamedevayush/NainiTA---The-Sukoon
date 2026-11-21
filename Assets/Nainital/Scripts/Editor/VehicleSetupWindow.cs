using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Vehicle Setup tool (user, 2026-11-19): "saari vehicles ki setting ... headlights place, backlights place, driver
    /// pose with proper position and rotation, with another person sitting in same vehicle ... ek ek krke vehicles
    /// aayein, with their total capacity ... main sabko set krdunga ... door alag krke launga and 5 side damage, to vo
    /// bhi wahan preview ho jaaye ... same for bikes and scooty ... player kaise bethega, kaise puraane ko hatayega,
    /// tyres kaise ghumenge, aage and peeche dono".
    ///
    /// Nainital > Vehicle Setup, in the VehicleSetup scene, in Play mode (the people animate only in Play). The vehicles
    /// come one at a time (Prev / Next): the new ones exported from Blender to Vehicles/New (Body, Wheel_FL.., Door_..,
    /// shape keys Dmg_Front/Rear/Left/Right/Top) and, ticked, the old library. Each gets as many people as it carries,
    /// in their poses; the user moves and turns them (and their green entry points, and the lamps) in the Scene view.
    /// Previews: night lamps, every door, the five damage keys, the wheels rolling forward / back and steering, and the
    /// player taking the seat from the one in it. Save writes Resources/VehicleSetup/&lt;model&gt;.json (it survives
    /// leaving Play); OK marks it approved, and the game then seats people from it.
    /// </summary>
    public class VehicleSetupWindow : EditorWindow
    {
        const string ScenePath = "Assets/Nainital/Scenes/VehicleSetup.unity";
        const string NewDir = "Assets/Nainital/Vehicles/New";
        const string SaveDir = "Assets/Resources/VehicleSetup";

        [SerializeField] int index;
        [SerializeField] bool showOld;
        [SerializeField] bool night;
        [SerializeField] float driveSpeed, steer;
        [SerializeField] int enterSeat;
        Vector2 scroll;

        class Item { public string name; public GameObject source; public bool isNew; }
        List<Item> items = new List<Item>();

        // what is in the scene for the current vehicle
        GameObject root, vehicle, player;
        VehicleSetupData data;
        readonly List<GameObject> seats = new List<GameObject>(), entries = new List<GameObject>();
        readonly List<GameObject> heads = new List<GameObject>(), tails = new List<GameObject>();
        /// <summary>A wheel: its rest rotation, the axle and the up axis in its parent's space at rest, and whether it
        /// already steers through a parent (a bike's front wheel under the handlebar, user 2026-11-20).</summary>
        class Wheel { public Transform t; public Quaternion rest; public bool front, viaBar; public Vector3 axle, up; }
        readonly List<Wheel> wheels = new List<Wheel>();
        readonly List<(Transform t, Quaternion rest)> bars = new List<(Transform, Quaternion)>();
        readonly List<string> checks = new List<string>();
        /// <summary>The car's steering wheel (a separate object Steering*, origin at its centre, its up = the column),
        /// turned 3x the road wheels' angle in the preview.</summary>
        readonly List<(Transform t, Quaternion rest)> steeringWheels = new List<(Transform, Quaternion)>();
        /// <summary>Per seat: the wrist markers (driver / rider only; null otherwise).</summary>
        readonly List<(GameObject l, GameObject r)> hands = new List<(GameObject, GameObject)>();
        /// <summary>Per seat: the ankle markers (everyone: the floor, the pedals, the footpegs).</summary>
        readonly List<(GameObject l, GameObject r)> feet = new List<(GameObject, GameObject)>();
        readonly List<(Transform t, Quaternion rest, VehicleSetupData.Door d)> doors = new List<(Transform, Quaternion, VehicleSetupData.Door)>();
        readonly Dictionary<string, float> doorOpen = new Dictionary<string, float>();
        // doors that come with their own opening animation in the FBX (the buses' folding doors, 21 Nov): the slider scrubs
        // the clip instead of swinging the door about a hinge
        readonly Dictionary<string, AnimationClip> doorClips = new Dictionary<string, AnimationClip>();
        readonly List<(SkinnedMeshRenderer r, int i, string name)> damage = new List<(SkinnedMeshRenderer, int, string)>();
        float spin, wheelR = 0.33f;
        Bounds box;
        string status = "";
        Material headMat, tailMat;

        [MenuItem("Nainital/Vehicle Setup")]
        static void Open() => GetWindow<VehicleSetupWindow>("Vehicle Setup");

        /// <summary>URP materials for the models in Vehicles/New from the PNGs export_unity.py writes next to them
        /// (Textures/&lt;name&gt;_color|normal.png): Unity left the FBX-embedded textures off its materials (19 Nov).
        /// Every material of the FBX is remapped to Materials/&lt;name&gt;.mat. Batch: call VehicleSetupWindow.FixMaterials</summary>
        public static void FixMaterials()
        {
            string matDir = NewDir + "/Materials";
            if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(NewDir, "Materials");
            int n = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { NewDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g), name = Path.GetFileNameWithoutExtension(p);
                string col = $"{NewDir}/Textures/{name}_color.png", nor = $"{NewDir}/Textures/{name}_normal.png";
                var colTex = AssetDatabase.LoadAssetAtPath<Texture2D>(col);
                if (!colTex) { Debug.LogWarning("[Nainital] vehicle materials: no " + col); continue; }
                if (AssetImporter.GetAtPath(nor) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
                string mp = $"{matDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, mp); }
                mat.SetTexture("_BaseMap", colTex);
                mat.SetColor("_BaseColor", Color.white);
                var norTex = AssetDatabase.LoadAssetAtPath<Texture2D>(nor);
                if (norTex) { mat.SetTexture("_BumpMap", norTex); mat.EnableKeyword("_NORMALMAP"); }
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.35f);
                EditorUtility.SetDirty(mat);
                var imp = (ModelImporter)AssetImporter.GetAtPath(p);
                foreach (var src in AssetDatabase.LoadAllAssetRepresentationsAtPath(p).OfType<Material>())
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), mat);
                foreach (var kv in imp.GetExternalObjectMap().ToList())
                    if (kv.Key.type == typeof(Material)) imp.AddRemap(kv.Key, mat);
                imp.SaveAndReimport();
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Nainital] vehicle materials: " + n + " models remapped to URP Lit with their textures");
        }

        /// <summary>Every model in Vehicles/New side by side in an empty scene, photographed from the front-left to
        /// Shots/vehicles_new.png (textures, facing, scale at a glance). Batch: call VehicleSetupWindow.SheetShot (last:
        /// it leaves an unsaved scene open).</summary>
        public static void SheetShot()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.4f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.68f);
            float x = 0f;
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { NewDir }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
            {
                var inst = (GameObject)Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(g));
                var b = LocalBounds(inst);
                inst.transform.position = new Vector3(x + b.size.x * 0.5f, 0f, 0f);
                x += b.size.x + 1.0f;
            }
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.78f, 0.8f, 0.83f);
            cam.fieldOfView = 14f;
            Vector3 c = new Vector3(x * 0.5f, 1.2f, 0f);
            cam.transform.position = c + new Vector3(-x * 0.08f, x * 0.36f, x * 1.25f);
            cam.transform.LookAt(c);
            var rt = new RenderTexture(2600, 760, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string outP = Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots/vehicles_new.png"));
            File.WriteAllBytes(outP, tex.EncodeToPNG());
            Debug.Log("[Nainital] vehicle sheet " + outP);
        }

        /// <summary>Batch check of the models in Vehicles/New (PhotoBatch "call VehicleSetupWindow.ImportCheck"): size
        /// (the length must lie along z, the front at +z), materials with a texture, the parts the tool looks for.</summary>
        public static void ImportCheck()
        {
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { NewDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                var inst = (GameObject)Instantiate(go);
                var b = LocalBounds(inst);
                var rs = inst.GetComponentsInChildren<Renderer>();
                var mats = rs.SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().ToArray();
                string mat = string.Join(",", mats.Select(m => m.shader.name.Replace("Universal Render Pipeline/", "URP/") + (m.mainTexture ? "+tex" : "-NOTEX")));
                var parts = inst.GetComponentsInChildren<Transform>().Select(t => t.name).Where(n => n.StartsWith("Wheel_") || n.StartsWith("Door_")).ToArray();
                Debug.Log($"[Nainital] vehicle {Path.GetFileNameWithoutExtension(p)}: {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} (x,y,z) " +
                          $"centre z {b.center.z:F2} lengthAlongZ={b.size.z > b.size.x} mats [{mat}] parts [{string.Join(",", parts)}]");
                DestroyImmediate(inst);
            }
        }

        static VehicleSetupWindow instance;

        /// <summary>The models in Vehicles/New by name (VehicleSetupShots goes through them).</summary>
        public static List<string> ModelNames() =>
            AssetDatabase.FindAssets("t:Model", new[] { NewDir }).Select(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g))).OrderBy(n => n).ToList();

        /// <summary>Put a model on show by its name (Play mode; opens the window if it is closed).</summary>
        public static void Goto(string model)
        {
            var w = instance ? instance : GetWindow<VehicleSetupWindow>("Vehicle Setup");
            w.Refresh();
            int i = w.items.FindIndex(x => x.name == model);
            if (i < 0) throw new System.Exception("no vehicle " + model);
            w.Show(i);
        }

        /// <summary>Bridge: call VehicleSetupWindow.SteerArg - the Steer slider (degrees, from Bridge/arg.txt), and a log of
        /// where each lamp hangs.</summary>
        public static void SteerArg()
        {
            if (!instance || !instance.vehicle) throw new System.Exception("no vehicle on show");
            float.TryParse(File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out instance.steer);
            var V = instance.vehicle.transform;
            Debug.Log($"[Nainital] {instance.Current} steer {instance.steer:F0}: " + string.Join("; ", instance.heads.Concat(instance.tails).Where(g => g)
                .Select(g => $"{g.name} on {(g.transform.parent == V ? "body" : g.transform.parent.name)} at {V.InverseTransformPoint(g.transform.position):F2}")));
        }

        /// <summary>The data of seat i of the vehicle on show (null if none): VehicleSetupShots reads its measures.</summary>
        public static VehicleSetupData.Seat SeatData(int i) =>
            instance && instance.data != null && i >= 0 && i < instance.data.seats.Length ? instance.data.seats[i] : null;

        /// <summary>Bridge: call VehicleSetupWindow.SnapAll - the "Snap everyone" button on the vehicle on show.</summary>
        public static void SnapAll()
        {
            if (!instance || !instance.vehicle) throw new System.Exception("no vehicle on show");
            instance.Collect();
            instance.SnapSeats(0, instance.seats.Count - 1);
            Debug.Log("[Nainital] " + instance.status);
        }

        void OnEnable() { instance = this; EditorApplication.update += Tick; EditorApplication.playModeStateChanged += PlayChanged; Refresh(); }
        void OnDisable() { if (instance == this) instance = null; EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayChanged; }
        // the lamp / seat fields follow what is moved in the Scene view
        void OnInspectorUpdate() { if (Application.isPlaying && vehicle) Repaint(); }
        // only in its own scene: Play in the game scene with this window open put a setup vehicle and its people at the
        // town's origin (21 Nov)
        void PlayChanged(PlayModeStateChange s) { if (s == PlayModeStateChange.EnteredPlayMode && SceneManager.GetActiveScene().path == ScenePath) { Refresh(); Show(index); } }

        // ------------------------------------------------------------------ list
        void Refresh()
        {
            items.Clear();
            if (Directory.Exists(NewDir))
                foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { NewDir }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    items.Add(new Item { name = Path.GetFileNameWithoutExtension(p), source = AssetDatabase.LoadAssetAtPath<GameObject>(p), isNew = true });
                }
            items = items.OrderBy(i => i.name).ToList();
            if (showOld && VehicleLibrary.Instance)
            {
                // the library now lists the new set too (21 Nov): only the old models here
                foreach (var e in VehicleLibrary.Instance.entries) if (e != null && e.prefab && !e.parts) items.Add(new Item { name = e.name, source = e.prefab });
                foreach (var e in VehicleLibrary.Instance.bikes) if (e != null && e.prefab && !e.parts) items.Add(new Item { name = e.name, source = e.prefab });
            }
            index = Mathf.Clamp(index, 0, Mathf.Max(0, items.Count - 1));
        }

        string Current => items.Count > 0 ? items[index].name : null;

        // ------------------------------------------------------------------ GUI
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorGUILayout.HelpBox("Vehicle Setup works in its own scene.", MessageType.Info);
                if (GUILayout.Button("Open / make the VehicleSetup scene", GUILayout.Height(28))) OpenScene();
                EditorGUILayout.EndScrollView();
                return;
            }
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Press Play: the people sit and animate only in Play mode. Saving works in Play.", MessageType.Info);
                if (GUILayout.Button("Play", GUILayout.Height(28))) EditorApplication.isPlaying = true;
                EditorGUILayout.EndScrollView();
                return;
            }
            bool old = EditorGUILayout.ToggleLeft("Also the old library vehicles", showOld);
            if (old != showOld) { showOld = old; Refresh(); }
            if (items.Count == 0) { EditorGUILayout.HelpBox("No vehicles in " + NewDir, MessageType.Warning); EditorGUILayout.EndScrollView(); return; }

            // vehicle picker
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ Prev", GUILayout.Width(70))) Show(index - 1);
            var names = items.Select(i => (Approved(i.name) ? "✓ " : Saved(i.name) ? "• " : "   ") + i.name).ToArray();
            int pick = EditorGUILayout.Popup(index, names);
            if (pick != index) Show(pick);
            if (GUILayout.Button("Next ▶", GUILayout.Width(70))) Show(index + 1);
            EditorGUILayout.EndHorizontal();
            int done = items.Count(i => Approved(i.name));
            EditorGUILayout.LabelField($"{index + 1} / {items.Count}   approved {done}   ✓ OK  • saved", EditorStyles.miniLabel);
            if (!vehicle) { if (GUILayout.Button("Load")) Show(index); EditorGUILayout.EndScrollView(); return; }
            EditorGUILayout.LabelField($"{Current}:  {box.size.z:F2} m long, {box.size.x:F2} wide, {box.size.y:F2} high", EditorStyles.boldLabel);

            Section("People  (" + seats.Count + " seats)");
            for (int i = 0; i < seats.Count; i++)
            {
                if (!seats[i]) continue;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField((i == 0 ? "Driver" : "Seat " + i), GUILayout.Width(52));
                var s = data.seats[i];
                var np = (SeatPose)EditorGUILayout.EnumPopup(s.pose, GUILayout.Width(90));
                if (np != s.pose) { Collect(); s.pose = np; Build(); GUIUtility.ExitGUI(); }
                if (GUILayout.Button("Select", GUILayout.Width(52))) Pick(seats[i]);
                if (GUILayout.Button("Entry", GUILayout.Width(46))) Pick(entries[i]);
                if (GUILayout.Button("Face", GUILayout.Width(40))) Reroll(i);
                if (i < hands.Count && hands[i].l)
                {
                    if (GUILayout.Button(new GUIContent("L", "left hand (blue)"), GUILayout.Width(22))) Pick(hands[i].l);
                    if (GUILayout.Button(new GUIContent("R", "right hand (orange)"), GUILayout.Width(22))) Pick(hands[i].r);
                }
                if (i < feet.Count && feet[i].l)
                {
                    if (GUILayout.Button(new GUIContent("FL", "left foot (cyan)"), GUILayout.Width(28))) Pick(feet[i].l);
                    if (GUILayout.Button(new GUIContent("FR", "right foot (pink)"), GUILayout.Width(28))) Pick(feet[i].r);
                }
                if (s.pose == SeatPose.Ride || s.pose == SeatPose.Pillion)
                {
                    // the back's lean (owner, 20 Nov night: "pehla itna jhuka kyun hai")
                    float b0 = VehicleSetup.DefaultBend(s);
                    float b1 = EditorGUILayout.FloatField(new GUIContent("", "Bend: how far his back may lean forward, degrees (rider 12, pillion 5 by default). He still leans further if his hands cannot reach the grips: then move him forward or the L / R balls back."), b0, GUILayout.Width(34));
                    if (!Mathf.Approximately(b1, b0))
                    {
                        s.bendSet = true;
                        s.bend = Mathf.Clamp(b1, 0f, 60f);
                        var pik = seats[i] ? seats[i].GetComponent<HandIK>() : null;
                        if (pik) pik.maxBend = s.bend;
                    }
                    GUILayout.Label("°", GUILayout.Width(10));
                }
                if (GUILayout.Button(new GUIContent("Snap", "Sets him at seat height over this model's floor (car 0.27 m, bus / truck / auto 0.42 m; a bike: on its seat) and both feet on the floor under the foot markers"), GUILayout.Width(44))) { Collect(); SnapSeats(i, i); }
                if (i > 0 && GUILayout.Button("✕", GUILayout.Width(22))) { Collect(); RemoveAt(ref data.seats, i); Build(); GUIUtility.ExitGUI(); }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Seat")) { Collect(); AddSeat(); Build(); }
            if (GUILayout.Button("Mirror selected seat")) MirrorSelected();
            if (GUILayout.Button(new GUIContent("Snap everyone", "Everyone at seat height over the floor, feet on the floor (new vehicles get this by themselves)"))) { Collect(); SnapSeats(0, seats.Count - 1); }
            if (GUILayout.Button(new GUIContent("Feet to default", "Both feet of every person back to the starting spots (pedals / floor / pegs)"))) { Collect(); foreach (var s in data.seats) DefaultFeet(s); Build(); }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Hands: blue L / orange R on the wheel or the grips. Feet: cyan FL / pink FR on the floor, the pedals or the pegs - move them in the Scene view; the legs bend to them.", EditorStyles.wordWrappedMiniLabel);

            Section("Player gets in");
            EditorGUILayout.BeginHorizontal();
            enterSeat = Mathf.Clamp(EditorGUILayout.IntField("Seat", enterSeat), 0, Mathf.Max(0, seats.Count - 1));
            if (GUILayout.Button("Play it")) EnterPreview();
            if (GUILayout.Button("Reset")) { if (player) Destroy(player); Build(); }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("The one in the seat climbs out to his entry point and walks off; the player walks up and takes the seat.", EditorStyles.wordWrappedMiniLabel);

            Section("Lamps");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Head lamp")) { Collect(); Add(ref data.head, DefaultLamp(true)); Build(); }
            if (GUILayout.Button("+ Tail lamp")) { Collect(); Add(ref data.tail, DefaultLamp(false)); Build(); }
            EditorGUILayout.EndHorizontal();
            LampRows(heads, ref data.head, "Head");
            LampRows(tails, ref data.tail, "Tail");
            bool n = EditorGUILayout.Toggle("Night preview", night);
            if (n != night) { night = n; ApplyNight(); }

            Section("Wheels  (" + wheels.Count + ")");
            if (wheels.Count == 0) EditorGUILayout.HelpBox("No wheels in this model: cut them in Blender as Wheel_FL / Wheel_FR / Wheel_RL / Wheel_RR (bikes: Wheel_F / Wheel_R), origin at the wheel's centre.", MessageType.None);
            else
            {
                driveSpeed = EditorGUILayout.Slider("Speed m/s (- = back)", driveSpeed, -12f, 12f);
                steer = EditorGUILayout.Slider("Steer °", steer, -38f, 38f);
                if (GUILayout.Button("Stop")) { driveSpeed = 0f; steer = 0f; }
            }
            if (checks.Count > 0)
            {
                EditorGUILayout.LabelField("Checks (the model as exported from Blender)", EditorStyles.miniBoldLabel);
                foreach (var c in checks)
                    EditorGUILayout.LabelField(c, c.StartsWith("✓") ? EditorStyles.wordWrappedMiniLabel : EditorStyles.wordWrappedLabel);
            }

            Section("Doors  (" + doors.Count + ")");
            if (doors.Count == 0) EditorGUILayout.HelpBox("No doors in this model: in Blender make each a separate object Door_FL / Door_FR / Door_RL / Door_RR / Door_Back with its origin on the hinge.", MessageType.None);
            foreach (var (t, rest, d) in doors)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(d.name, GUILayout.Width(80));
                d.hinge = (DoorHinge)EditorGUILayout.EnumPopup(d.hinge, GUILayout.Width(64));
                d.flip = EditorGUILayout.ToggleLeft("flip", d.flip, GUILayout.Width(40));
                d.openDeg = EditorGUILayout.FloatField(d.openDeg, GUILayout.Width(36));
                doorOpen.TryGetValue(d.name, out float o);
                doorOpen[d.name] = GUILayout.HorizontalSlider(o, 0f, 1f);
                EditorGUILayout.EndHorizontal();
            }

            Section("Damage  (" + damage.Count + ")");
            if (damage.Count == 0) EditorGUILayout.HelpBox("No damage keys: on the Body in Blender add shape keys Dmg_Front, Dmg_Rear, Dmg_Left, Dmg_Right, Dmg_Top.", MessageType.None);
            foreach (var (r, i, name) in damage)
                if (r) r.SetBlendShapeWeight(i, EditorGUILayout.Slider(name, r.GetBlendShapeWeight(i), 0f, 100f));

            Section("Save");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", GUILayout.Height(30))) Save(false);
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button(data != null && data.approved ? "OK ✓ (saved)" : "OK - approve", GUILayout.Height(30))) Save(true);
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Back to default", GUILayout.Height(30)) && EditorUtility.DisplayDialog("Vehicle Setup", "Throw away this vehicle's setup and start from the default seats and lamps?", "Yes", "No")) { data = Default(); Build(); SnapSeats(0, seats.Count - 1); }
            EditorGUILayout.EndHorizontal();
            if (status.Length > 0) EditorGUILayout.HelpBox(status, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        static void Section(string t) { EditorGUILayout.Space(6); EditorGUILayout.LabelField(t, EditorStyles.boldLabel); }
        static void Pick(GameObject g) { if (!g) return; Selection.activeGameObject = g; SceneView.lastActiveSceneView?.FrameSelected(); }

        /// <summary>One block per lamp (user, 2026-11-20: "vehicle light transform position ka option nahi diya"): mirror,
        /// size, Select, ✕ on the first line, then its position and rotation in the vehicle's space (x right, y up, z
        /// forward; drag the X / Y / Z labels to slide it), and Snap: onto the body's surface straight ahead of / behind
        /// it. A mirrored lamp's twin follows at -x.</summary>
        void LampRows(List<GameObject> objs, ref VehicleSetupData.Lamp[] arr, string label)
        {
            for (int i = 0; i < arr.Length && i < objs.Count; i++)
            {
                var l = arr[i];
                var g = objs[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(label + " " + i, EditorStyles.boldLabel, GUILayout.Width(52));
                bool m = EditorGUILayout.ToggleLeft("mirror", l.mirror, GUILayout.Width(58));
                if (m != l.mirror) { Collect(); l.mirror = m; BuildLamps(); GUIUtility.ExitGUI(); }
                if (bars.Count > 0 && bars[0].t)
                {
                    bool onBar = !string.IsNullOrEmpty(l.on);
                    bool nb = EditorGUILayout.ToggleLeft(new GUIContent("handle", "Fixed to " + bars[0].t.name + ": turns with the steering (a bike's / scooty's headlight). Off = on the body."), onBar, GUILayout.Width(62));
                    if (nb != onBar) { Collect(); l.on = nb ? bars[0].t.name : ""; BuildLamps(); GUIUtility.ExitGUI(); }
                }
                EditorGUILayout.LabelField("size", GUILayout.Width(28));
                var sz = EditorGUILayout.Vector2Field(GUIContent.none, l.size, GUILayout.Width(120));
                if (sz != l.size) { l.size = sz; if (g) g.transform.localScale = new Vector3(sz.x, sz.y, 1f); }
                if (GUILayout.Button("Select", GUILayout.Width(52))) Pick(g);
                if (GUILayout.Button("✕", GUILayout.Width(22))) { Collect(); RemoveAt(ref arr, i); Build(); GUIUtility.ExitGUI(); }
                EditorGUILayout.EndHorizontal();
                if (!g) continue;
                var t = g.transform;
                var V = vehicle.transform;
                EditorGUI.indentLevel++;
                // in the vehicle's space whatever it hangs on (the body or the handlebar)
                Vector3 lp = V.InverseTransformPoint(t.position);
                var p = EditorGUILayout.Vector3Field("Position (m)", lp);
                if (p != lp) { t.position = V.TransformPoint(p); l.pos = p; SceneView.RepaintAll(); }
                Vector3 le = (Quaternion.Inverse(V.rotation) * t.rotation).eulerAngles;
                var e = EditorGUILayout.Vector3Field("Rotation (°)", le);
                if (e != le) { t.rotation = V.rotation * Quaternion.Euler(e); l.euler = e; SceneView.RepaintAll(); }
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(18);
                if (GUILayout.Button(new GUIContent("Snap to body", "Moves the lamp along the vehicle's length onto the body's surface in front of it (head) / behind it (tail), keeping its height and side, and turns it to face out"))) SnapLamp(g, label == "Head");
                if (GUILayout.Button("x ↔ 0", GUILayout.Width(52))) { Vector3 q = V.InverseTransformPoint(t.position); q.x = 0f; t.position = V.TransformPoint(q); l.pos = q; }
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>The lamp onto the body: a ray from 3 m out ahead of (head) / behind (tail) the lamp, along the vehicle's
        /// length, at the lamp's own height and side, against the body's meshes (temporary colliders); the lamp sits 5 mm
        /// proud of the hit and faces along the surface's normal.</summary>
        void SnapLamp(GameObject g, bool head)
        {
            if (!g || !vehicle) return;
            var temp = new List<MeshCollider>();
            foreach (var mf in vehicle.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || mf.GetComponentInParent<SetupHandle>() || mf.gameObject.layer == 9) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                temp.Add(mc);
            }
            foreach (var sk in vehicle.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!sk.sharedMesh) continue;
                var baked = new Mesh();
                sk.BakeMesh(baked, true);
                var mc = sk.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = baked;
                temp.Add(mc);
            }
            Physics.SyncTransforms();
            Vector3 fwd = vehicle.transform.forward * (head ? 1f : -1f);
            Vector3 from = g.transform.position + fwd * 3f;
            bool hitIt = false;
            Transform onPart = null;
            foreach (var h in Physics.RaycastAll(from, -fwd, 6f).OrderBy(h => h.distance))
            {
                if (!temp.Contains(h.collider as MeshCollider)) continue;
                g.transform.position = h.point + h.normal * 0.005f;
                // the quad's face toward the outside (its local -z along the normal; the lamp material draws both faces)
                g.transform.rotation = Quaternion.LookRotation(-h.normal, vehicle.transform.up);
                // on the handlebar's mesh (or a part under it): the lamp turns with the handlebar
                for (var x = h.collider.transform; x && x != vehicle.transform; x = x.parent)
                    if (bars.Any(bb => bb.t == x)) { onPart = x; break; }
                g.transform.SetParent(onPart ? onPart : vehicle.transform, true);
                hitIt = true;
                break;
            }
            foreach (var mc in temp) Destroy(mc);
            string where = onPart ? "the handlebar (" + onPart.name + ", turns with it)" : "the body";
            string gn = g.name;
            Vector3 at = vehicle.transform.InverseTransformPoint(g.transform.position);
            Collect();
            BuildLamps();   // the mirrored twin onto the same part
            status = hitIt ? $"{gn} snapped onto {where} at {at:F3}" : $"{gn}: no body surface in line with it - move it over the lamp first";
        }

        // ------------------------------------------------------------------ scene
        static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            var gm = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.36f, 0.36f, 0.38f) };
            AssetDatabase.CreateAsset(gm, "Assets/Nainital/Scenes/VehicleSetup_Ground.mat");
            ground.GetComponent<Renderer>().sharedMaterial = gm;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, 130f, 0f);
            var cam = new GameObject("Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            cam.transform.SetPositionAndRotation(new Vector3(-5.5f, 3.2f, 6.5f), Quaternion.Euler(20f, 140f, 0f));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);
            EditorSceneManager.SaveScene(sc, ScenePath);
        }

        void Show(int i)
        {
            if (items.Count == 0) return;
            index = (i % items.Count + items.Count) % items.Count;
            if (!Application.isPlaying) return;
            if (root) Destroy(root);
            if (player) Destroy(player);
            root = new GameObject("VEHICLE SETUP");
            vehicle = (GameObject)Instantiate(items[index].source, root.transform);
            vehicle.name = Current;
            foreach (var c in vehicle.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var lg in vehicle.GetComponentsInChildren<LODGroup>()) lg.ForceLOD(0);
            box = LocalBounds(vehicle);
            var saved = Load(Current);
            data = saved ?? Default();
            Scan();
            Build();
            status = "";
            if (saved == null)
            {
                SnapSeats(0, seats.Count - 1);   // a new one: the default seats onto this model's floor / seat
                // and on a handlebar vehicle its headlights onto the front, on the handlebar if that is where they sit
                if (bars.Count > 0)
                    for (int h = 0; h < data.head.Length; h++)
                        if (h < heads.Count && heads[h]) SnapLamp(heads[h], true);
                status = "";
            }
            if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.Frame(new Bounds(box.center, box.size * 1.3f), false);
            Repaint();
        }

        static Bounds LocalBounds(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(Vector3.up, Vector3.one * 2f);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;   // the vehicle stands at the origin unrotated: world = vehicle space
        }

        /// <summary>A wheel's radius from its mesh: the larger of the mesh's own half height and half length, in metres. (The
        /// renderer's world box of a wheel turned about its axle is a turned box - up to 40 % too big: the buses and bike
        /// 13 read 0.68 / 0.37 m for 0.50 / 0.27 m wheels, 21 Nov.)</summary>
        static float WheelRadius(Transform t)
        {
            var mf = t.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) return 0f;
            Vector3 e = mf.sharedMesh.bounds.extents, s = t.lossyScale;
            float[] r = { Mathf.Abs(e.x * s.x), Mathf.Abs(e.y * s.y), Mathf.Abs(e.z * s.z) };
            System.Array.Sort(r);
            return r[1];   // a wheel is a disc: two of its extents are the radius, the smallest is half its width
        }

        /// <summary>Wheels, handlebar, doors and damage keys of the model, by their names.</summary>
        void Scan()
        {
            wheels.Clear(); bars.Clear(); steeringWheels.Clear(); doors.Clear(); damage.Clear(); doorOpen.Clear(); doorClips.Clear(); spin = 0f;
            string modelPath = items.Count > 0 && items[index].source ? AssetDatabase.GetAssetPath(items[index].source) : null;
            if (!string.IsNullOrEmpty(modelPath))
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>())
                {
                    if (clip.name.StartsWith("__preview__")) continue;
                    foreach (var b in AnimationUtility.GetCurveBindings(clip))
                    {
                        string leaf = b.path.Substring(b.path.LastIndexOf('/') + 1);
                        if (!leaf.StartsWith("Door_", System.StringComparison.OrdinalIgnoreCase)) continue;
                        // only a door whose own curves change: a baked clip also carries flat keys for the other parts
                        var c = AnimationUtility.GetEditorCurve(clip, b);
                        if (c != null && c.keys.Length > 1 && Mathf.Abs(c.keys[0].value - c.keys[c.keys.Length - 1].value) > 1e-3f)
                            doorClips[leaf] = clip;
                    }
                }
            var saved = data.doors.ToDictionary(d => d.name, d => d);
            var newDoors = new List<VehicleSetupData.Door>();
            foreach (var t in vehicle.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("Wheel_", System.StringComparison.OrdinalIgnoreCase))
                {
                    string k = n.Substring(6).ToUpperInvariant();
                    bool front = k.StartsWith("F") || k == "0" || k == "1";
                    // axle and up as the parent sees them at rest: a wheel under the handlebar spins about the axle the
                    // handlebar carries round with it
                    var inv = t.parent ? Quaternion.Inverse(t.parent.rotation) : Quaternion.identity;
                    wheels.Add(new Wheel { t = t, rest = t.localRotation, front = front, axle = inv * vehicle.transform.right, up = inv * vehicle.transform.up });
                    float wr = WheelRadius(t);
                    if (wr > 0f) wheelR = Mathf.Max(0.15f, wr);
                }
                else if (n.StartsWith("Handle", System.StringComparison.OrdinalIgnoreCase) || n.StartsWith("Fork", System.StringComparison.OrdinalIgnoreCase))
                    bars.Add((t, t.localRotation));
                else if (n.StartsWith("Steering", System.StringComparison.OrdinalIgnoreCase))
                    steeringWheels.Add((t, t.localRotation));
                else if (n.StartsWith("Door_", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!saved.TryGetValue(n, out var d))
                    {
                        d = new VehicleSetupData.Door { name = n, hinge = n.ToLowerInvariant().Contains("back") ? DoorHinge.Top : DoorHinge.Side };
                        var r = t.GetComponent<Renderer>();
                        // a door on the left opens with +angle about up (its free edge swings out to -x)
                        if (r && d.hinge == DoorHinge.Side) d.flip = r.bounds.center.x > 0f;
                    }
                    newDoors.Add(d);
                    doors.Add((t, t.localRotation, d));
                }
                var sk = t.GetComponent<SkinnedMeshRenderer>();
                if (sk && sk.sharedMesh)
                    for (int i = 0; i < sk.sharedMesh.blendShapeCount; i++)
                    {
                        string bn = sk.sharedMesh.GetBlendShapeName(i);
                        if (bn.ToLowerInvariant().Contains("dmg")) damage.Add((sk, i, bn.Substring(bn.LastIndexOf('.') + 1)));
                    }
            }
            data.doors = newDoors.ToArray();
            CheckParts();
        }

        /// <summary>What the Checks list shows: are the parts set up the way the preview and the game expect.</summary>
        void CheckParts()
        {
            checks.Clear();
            bool two = VehicleSetup.TwoWheeler(Current), three = VehicleSetup.ThreeWheeler(Current), barSteer = two || three;
            foreach (var w in wheels)
            {
                for (var p = w.t.parent; p && p != vehicle.transform; p = p.parent)
                    if (bars.Any(b => b.t == p)) { w.viaBar = true; break; }
                var r = w.t.GetComponent<Renderer>();
                if (r)
                {
                    float off = Vector3.ProjectOnPlane(r.bounds.center - w.t.position, vehicle.transform.right).magnitude;   // along the axle does not wobble
                    checks.Add(off > 0.04f ? $"⚠ {w.t.name}: origin {off * 100f:F0} cm off the wheel's centre - it will wobble (Blender: Origin to Geometry)"
                                           : $"✓ {w.t.name}: origin at the centre, radius {WheelRadius(w.t):F2} m" + (w.viaBar ? ", steers with " + w.t.parent.name : ""));
                }
            }
            if (barSteer)
            {
                // bikes, scooties, the auto and the e-rickshaw: ONE front wheel, turned by the handlebar it hangs under
                var front = wheels.FirstOrDefault(w => w.front);
                if (wheels.Count(w => w.front) > 1) checks.Add("⚠ more than one front wheel: a handlebar vehicle has one, Wheel_F");
                if (wheels.Count == 0) { }
                else if (front == null) checks.Add("⚠ no front wheel: name it Wheel_F");
                else if (bars.Count == 0) checks.Add("⚠ no handlebar: a separate object Handlebar (or Handle / Fork...), origin on the steering head, with Wheel_F as its child");
                else if (!front.viaBar) checks.Add($"⚠ {front.t.name} is not under the handlebar: make it a child of {bars[0].t.name} (Ctrl+P, Keep Transform), so it steers with it and still spins");
                else
                {
                    var bar = bars[0].t;
                    float dz = Mathf.Abs(bar.position.z - front.t.position.z);
                    checks.Add(dz > 0.7f ? $"⚠ {bar.name}'s origin is {dz:F2} m in front of / behind the front wheel - put it on the steering head"
                                         : $"✓ {bar.name} steers {front.t.name} (handlebar origin {dz:F2} m from the axle along the vehicle)");
                }
                if (two && wheels.Count > 0 && wheels.Count != 2) checks.Add($"⚠ {wheels.Count} wheels on a two-wheeler (Wheel_F, Wheel_R)");
                if (three && wheels.Count > 0 && (wheels.Count(w => w.front) != 1 || wheels.Count(w => !w.front) < 1))
                    checks.Add($"⚠ three-wheeler: one Wheel_F under the handlebar and the rear wheels (Wheel_R, both in one, or Wheel_RL + Wheel_RR)");
            }
            else if (wheels.Count > 0 && (wheels.Count(w => w.front) != 2 || wheels.Count(w => !w.front) < 1))
                checks.Add($"⚠ wheels: {wheels.Count(w => w.front)} front (want Wheel_FL + Wheel_FR), {wheels.Count(w => !w.front)} rear (Wheel_R, or Wheel_RL + Wheel_RR)");
            if (!barSteer && bars.Count == 0)
                checks.Add(steeringWheels.Count > 0 ? $"✓ {steeringWheels[0].t.name}: the driver's hands turn with it"
                                                    : "ℹ no steering wheel object: cut it out as Steering (origin at its centre, local Z along the column) and the driver's hands will turn with it");
            foreach (var (t, rest, d) in doors)
            {
                var r = t.GetComponent<Renderer>();
                if (!r) continue;
                if (doorClips.TryGetValue(t.name, out var dc)) { checks.Add($"✓ {t.name}: opens with its own animation ({dc.name}, {dc.length:F2} s) - the slider plays it"); continue; }
                Vector3 c = r.bounds.center, e = r.bounds.extents;
                Vector3 o = t.position - c;
                bool onEdge = Mathf.Abs(o.z) > e.z * 0.6f || Mathf.Abs(o.y) > e.y * 0.6f || Mathf.Abs(o.x) > e.x * 0.6f;
                checks.Add(onEdge ? $"✓ {t.name}: origin on an edge (the hinge)" : $"⚠ {t.name}: origin near the door's middle - set it on the hinge line or it will spin in place");
            }
        }

        // ------------------------------------------------------------------ people, lamps
        void Build()
        {
            foreach (var g in seats.Concat(entries).Concat(heads).Concat(tails)) if (g) Destroy(g);
            foreach (var (l, r) in hands.Concat(feet)) { if (l) Destroy(l); if (r) Destroy(r); }
            seats.Clear(); entries.Clear(); heads.Clear(); tails.Clear(); hands.Clear(); feet.Clear();
            SteerAtRest();   // the wrist points are kept with the steering straight
            for (int i = 0; i < data.seats.Length; i++)
            {
                var s = data.seats[i];
                // the characters the game would seat here (VehicleFits: measured to fit this seat), else anyone
                bool fem = i > 0 && Random.value < 0.45f;
                var p = Person("Seat " + i + (i == 0 ? " (driver)" : ""), fem, VehicleSetup.SeatFit(Current, i) != null ? VehicleSetup.PickLook(Current, i, fem) : null);
                p.transform.SetParent(vehicle.transform, false);
                var h = p.AddComponent<SetupHandle>(); h.kind = SetupHandle.Kind.Seat; h.index = i;
                seats.Add(p);
                var e = Marker("Entry " + i, new Color(0.2f, 0.9f, 0.3f));
                var eh = e.AddComponent<SetupHandle>(); eh.kind = SetupHandle.Kind.Entry; eh.index = i;
                e.transform.SetParent(vehicle.transform, false);
                e.transform.localPosition = s.entry;
                entries.Add(e);
                Seat(i);
                var ik = p.AddComponent<HandIK>();
                ik.vehicle = vehicle.transform;
                ik.maxBend = VehicleSetup.DefaultBend(s);
                // hands on the wheel / the grips: the driver and the rider
                if (s.pose == SeatPose.Drive || s.pose == SeatPose.Ride)
                {
                    if (!s.hands) DefaultHands(s);
                    var on = HandsParent();
                    s.handsOn = on == vehicle.transform ? "" : on.name;
                    var hl = Marker("Hand L " + i, new Color(0.3f, 0.5f, 1f), 0.07f);
                    var hr = Marker("Hand R " + i, new Color(1f, 0.55f, 0.1f), 0.07f);
                    var hlh = hl.AddComponent<SetupHandle>(); hlh.kind = SetupHandle.Kind.HandL; hlh.index = i;
                    var hrh = hr.AddComponent<SetupHandle>(); hrh.kind = SetupHandle.Kind.HandR; hrh.index = i;
                    hl.transform.position = vehicle.transform.TransformPoint(s.handL);
                    hr.transform.position = vehicle.transform.TransformPoint(s.handR);
                    hl.transform.SetParent(on, true);
                    hr.transform.SetParent(on, true);
                    ik.left = hl.transform; ik.right = hr.transform;
                    hands.Add((hl, hr));
                }
                else hands.Add((null, null));
                // feet on the floor / pedals / pegs: everyone
                if (!s.feet) DefaultFeet(s);
                var fl = Marker("Foot L " + i, new Color(0.2f, 0.9f, 0.9f), 0.06f);
                var fr = Marker("Foot R " + i, new Color(0.95f, 0.3f, 0.85f), 0.06f);
                var flh = fl.AddComponent<SetupHandle>(); flh.kind = SetupHandle.Kind.FootL; flh.index = i;
                var frh = fr.AddComponent<SetupHandle>(); frh.kind = SetupHandle.Kind.FootR; frh.index = i;
                fl.transform.SetParent(vehicle.transform, false);
                fr.transform.SetParent(vehicle.transform, false);
                fl.transform.localPosition = s.footL;
                fr.transform.localPosition = s.footR;
                fl.transform.localRotation = fr.transform.localRotation = Quaternion.Euler(s.euler);
                ik.footLeft = fl.transform; ik.footRight = fr.transform;
                feet.Add((fl, fr));
            }
            BuildLamps();
        }

        /// <summary>The lamps alone (re)made from the data, each on its part (the body or the handlebar), the steering
        /// straight while they are placed. The people are left as they are.</summary>
        void BuildLamps()
        {
            foreach (var g in heads.Concat(tails)) if (g) Destroy(g);
            heads.Clear(); tails.Clear();
            SteerAtRest();
            for (int i = 0; i < data.head.Length; i++) heads.Add(LampObj("Head " + i, data.head[i], true, i));
            for (int i = 0; i < data.tail.Length; i++) tails.Add(LampObj("Tail " + i, data.tail[i], false, i));
            ApplyNight();
        }

        void Seat(int i)
        {
            var s = data.seats[i];
            var p = seats[i];
            p.transform.localPosition = s.pos;
            p.transform.localEulerAngles = s.euler;
            VehicleSetup.Pose(p.GetComponentInChildren<HumanoidRig>(true), s.pose);
        }

        static GameObject Person(string name, bool female, CharacterLibrary.Entry look = null)
        {
            var go = new GameObject(name) { layer = 9 };
            var rigGo = new GameObject("Rig") { layer = 9 };
            rigGo.transform.SetParent(go.transform, false);
            rigGo.SetActive(false);
            var rig = rigGo.AddComponent<HumanoidRig>();
            if (look != null) female = look.Female;
            rig.female = female;
            rig.bodyType = female ? HumanoidRig.BodyType.Woman : HumanoidRig.BodyType.Man;
            var lib = CharacterLibrary.Instance;
            if (look == null) look = lib ? lib.Pick(female, false, null) : null;
            if (look != null) CharacterLibrary.Apply(look, rig, null);
            rigGo.SetActive(true);
            return go;
        }

        /// <summary>Bridge (VehicleSetupShots.RunFits): seat i's person becomes this character (a CharacterLibrary id),
        /// hands and feet on the same targets.</summary>
        public static void SetLook(int i, string id)
        {
            if (!instance || i < 0 || i >= instance.seats.Count || !instance.seats[i]) return;
            var look = CharacterLibrary.Instance ? CharacterLibrary.Instance.Find(id) : null;
            if (look != null) instance.Reroll(i, look);
        }

        /// <summary>How many seats the vehicle on show has.</summary>
        public static int SeatsShown => instance ? instance.seats.Count : 0;

        void Reroll(int i, CharacterLibrary.Entry look = null)
        {
            Collect();
            var old = seats[i];
            var rig = old.GetComponentInChildren<HumanoidRig>(true);
            var p = Person(old.name, rig && !rig.female, look);
            p.transform.SetParent(vehicle.transform, false);
            var h = p.AddComponent<SetupHandle>(); h.kind = SetupHandle.Kind.Seat; h.index = i;
            // the new face keeps the hands and the feet where they were (the old one's IK targets)
            var oik = old.GetComponent<HandIK>();
            if (oik)
            {
                var nik = p.AddComponent<HandIK>();
                nik.left = oik.left; nik.right = oik.right; nik.footLeft = oik.footLeft; nik.footRight = oik.footRight; nik.vehicle = oik.vehicle;
                nik.maxBend = oik.maxBend;   // the seat's back limit (a rider's 12 deg) goes with the seat, not the face
            }
            seats[i] = p;
            Destroy(old);
            Seat(i);
        }

        static GameObject Marker(string name, Color c, float sphere = 0f)
        {
            var g = GameObject.CreatePrimitive(sphere > 0f ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            g.transform.localScale = sphere > 0f ? Vector3.one * sphere : new Vector3(0.45f, 0.02f, 0.45f);
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        GameObject LampObj(string name, VehicleSetupData.Lamp l, bool head, int i)
        {
            if (!headMat) { headMat = LampMat(new Color(1f, 0.96f, 0.82f)); tailMat = LampMat(new Color(0.9f, 0.06f, 0.04f)); }
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            g.name = name;
            Destroy(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().sharedMaterial = head ? headMat : tailMat;
            var h = g.AddComponent<SetupHandle>(); h.kind = head ? SetupHandle.Kind.Head : SetupHandle.Kind.Tail; h.index = i;
            g.transform.SetParent(vehicle.transform, false);
            g.transform.localPosition = l.pos;
            g.transform.localEulerAngles = l.euler;
            g.transform.localScale = new Vector3(l.size.x, l.size.y, 1f);
            // on the handlebar (a bike's headlight): hung from it where it stands, so it turns with the steering
            if (!string.IsNullOrEmpty(l.on))
            {
                var part = vehicle.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == l.on);
                if (part) g.transform.SetParent(part, true);
            }
            if (l.mirror)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Quad);
                m.name = name + " (mirror)";
                Destroy(m.GetComponent<Collider>());
                m.GetComponent<Renderer>().sharedMaterial = g.GetComponent<Renderer>().sharedMaterial;
                m.transform.SetParent(g.transform.parent, false);
                m.hideFlags = HideFlags.NotEditable;
                g.AddComponent<SetupMirror>().twin = m.transform;
            }
            return g;
        }

        static Material LampMat(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Cull", 0f);   // both faces: the quad shows whichever way it was turned
            return m;
        }

        void ApplyNight()
        {
            if (!Application.isPlaying) return;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.intensity = night ? 0.05f : 1.6f;
            RenderSettings.ambientLight = night ? new Color(0.06f, 0.07f, 0.1f) : new Color(0.55f, 0.57f, 0.6f);
            if (headMat) { headMat.SetColor("_BaseColor", night ? new Color(4f, 3.8f, 3.2f) : new Color(1f, 0.96f, 0.82f)); tailMat.SetColor("_BaseColor", night ? new Color(3f, 0.1f, 0.05f) : new Color(0.9f, 0.06f, 0.04f)); }
            foreach (var g in heads.Concat(tails))
            {
                if (!g) continue;
                var old = g.transform.Find("Light");
                if (old) Destroy(old.gameObject);
                if (!night) continue;
                bool head = g.GetComponent<SetupHandle>().kind == SetupHandle.Kind.Head;
                var lo = new GameObject("Light");
                lo.transform.SetParent(g.transform, false);
                lo.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, g.transform.localScale.x), 1f / Mathf.Max(0.01f, g.transform.localScale.y), 1f);
                var L = lo.AddComponent<Light>();
                if (head)
                {
                    L.type = LightType.Spot; L.range = 30f; L.spotAngle = 55f; L.intensity = 6f; L.color = new Color(1f, 0.95f, 0.85f);
                    lo.transform.rotation = vehicle.transform.rotation * Quaternion.Euler(8f, 0f, 0f);
                }
                else { L.type = LightType.Point; L.range = 2.5f; L.intensity = 1.5f; L.color = new Color(1f, 0.1f, 0.05f); }
            }
        }

        // ------------------------------------------------------------------ every frame (Play)
        void Tick()
        {
            if (!Application.isPlaying || !vehicle) return;
            float dt = 1f / 60f;
            // each person's head under this seat's roof (HumanoidRig.SeatLock), wherever the seat was dragged to
            for (int i = 0; i < seats.Count && data != null && i < data.seats.Length; i++)
            {
                var rig = seats[i] ? seats[i].GetComponentInChildren<HumanoidRig>() : null;
                if (rig) rig.seatRoof = data.seats[i].roof >= 0f ? vehicle.transform.TransformPoint(0f, data.seats[i].roof, 0f).y - rig.transform.position.y : float.NaN;
            }
            // the doors with their own clip first: sampling a baked clip puts every part it carries back at rest, so the
            // wheels, the handlebar and the hinged doors are posed after it
            foreach (var clip in doorClips.Values.Distinct())
            {
                float o = 0f;
                foreach (var kv in doorClips) if (kv.Value == clip && doorOpen.TryGetValue(kv.Key, out float v)) o = Mathf.Max(o, v);
                clip.SampleAnimation(vehicle, o * clip.length);
            }
            if (wheels.Count > 0)
            {
                spin += driveSpeed / wheelR * Mathf.Rad2Deg * dt;
                // the handlebar (and the fork, and the front wheel under it) turns about its OWN up axis: in Blender its
                // object rotation can tilt that axis along the steering head (the rake)
                foreach (var (t, rest) in bars) if (t) t.localRotation = rest * Quaternion.AngleAxis(steer, Vector3.up);
                foreach (var (t, rest) in steeringWheels) if (t) t.localRotation = rest * Quaternion.AngleAxis(steer * 3f, Vector3.up);
                foreach (var w in wheels)
                {
                    if (!w.t) continue;
                    // under the handlebar it already steers with it: only the spin; a car's front wheel steers by itself
                    float s = w.front && !w.viaBar ? steer : 0f;
                    w.t.localRotation = Quaternion.AngleAxis(s, w.up) * Quaternion.AngleAxis(spin, w.axle) * w.rest;
                }
            }
            foreach (var (t, rest, d) in doors)
            {
                if (!t || doorClips.ContainsKey(d.name)) continue;
                doorOpen.TryGetValue(d.name, out float o);
                float a = d.openDeg * o * (d.flip ? -1f : 1f);
                Vector3 axis = d.hinge == DoorHinge.Side ? Vector3.up : Vector3.right;
                if (d.hinge == DoorHinge.Bottom) a = -a;
                t.localRotation = Quaternion.AngleAxis(a, axis) * rest;
            }
            // a two-wheeler's people lean with the steering (the motorbike pack's lean loops; Steer +38 = full right)
            if (VehicleSetup.TwoWheeler(Current))
                foreach (var p in seats)
                {
                    var rig = p ? p.GetComponentInChildren<HumanoidRig>() : null;
                    if (rig) rig.lean = steer / 38f;
                }
            if (driveSpeed != 0f || steer != 0f) SceneView.RepaintAll();
        }

        void EnterPreview()
        {
            if (enterSeat >= seats.Count) return;
            Collect();
            if (player) Destroy(player);
            player = Person("PLAYER", false);
            player.transform.SetParent(root.transform, false);
            var s = data.seats[enterSeat];
            var sitter = seats[enterSeat];
            // the player takes the wheel / the grips from the one he replaces (HandIK on once he is seated)
            var oldIk = sitter ? sitter.GetComponent<HandIK>() : null;
            if (oldIk)
            {
                var pik = player.AddComponent<HandIK>();
                pik.left = oldIk.left; pik.right = oldIk.right; pik.footLeft = oldIk.footLeft; pik.footRight = oldIk.footRight; pik.vehicle = oldIk.vehicle;
                pik.enabled = false;
            }
            var runner = root.GetComponent<SetupEnterPreview>();
            if (!runner) runner = root.AddComponent<SetupEnterPreview>();
            runner.StartCoroutine(SetupEnterPreview.Run(vehicle.transform, sitter ? sitter.transform : null,
                sitter ? sitter.GetComponentInChildren<HumanoidRig>(true) : null, player.transform, player.GetComponentInChildren<HumanoidRig>(true),
                s.pos, Quaternion.Euler(s.euler), s.entry, s.pose, !VehicleSetup.HandlebarSteered(Current)));
            seats[enterSeat] = null;   // the old sitter walked off; Reset puts everyone back from the data
            status = "Preview running. Reset puts everyone back.";
        }

        // ------------------------------------------------------------------ data
        /// <summary>Reads the scene (what the user moved) back into the data.</summary>
        void Collect()
        {
            if (data == null || !vehicle) return;
            for (int i = 0; i < data.seats.Length && i < seats.Count; i++)
            {
                if (seats[i]) { data.seats[i].pos = seats[i].transform.localPosition; data.seats[i].euler = seats[i].transform.localEulerAngles; }
                if (i < entries.Count && entries[i]) data.seats[i].entry = entries[i].transform.localPosition;
            }
            // the lamps and the wrists, read with the steering straight (the next frame turns it back to the slider)
            SteerAtRest();
            for (int i = 0; i < data.head.Length && i < heads.Count; i++) if (heads[i]) ReadLamp(heads[i], data.head[i]);
            for (int i = 0; i < data.tail.Length && i < tails.Count; i++) if (tails[i]) ReadLamp(tails[i], data.tail[i]);
            for (int i = 0; i < data.seats.Length && i < hands.Count; i++)
            {
                var (l, r) = hands[i];
                if (!l || !r) continue;
                data.seats[i].hands = true;
                data.seats[i].handL = vehicle.transform.InverseTransformPoint(l.transform.position);
                data.seats[i].handR = vehicle.transform.InverseTransformPoint(r.transform.position);
            }
            for (int i = 0; i < data.seats.Length && i < feet.Count; i++)
            {
                var (l, r) = feet[i];
                if (!l || !r) continue;
                data.seats[i].feet = true;
                data.seats[i].footL = vehicle.transform.InverseTransformPoint(l.transform.position);
                data.seats[i].footR = vehicle.transform.InverseTransformPoint(r.transform.position);
            }
        }

        /// <summary>Bus, truck, pickup, van, auto, e-rickshaw: chair-height seats (the hips ~0.42 m over the floor, shins
        /// down); a car's seat is low (~0.27 m, legs out ahead).</summary>
        bool ChairSeats()
        {
            string m = (Current ?? "").ToLowerInvariant();
            return box.size.z > 6.5f || m.Contains("truck") || m.Contains("pickup") || m.Contains("van") || VehicleSetup.ThreeWheeler(Current);
        }

        /// <summary>Where the ankles start, from the seat (the hips HipsOverRoot over its root) and the kind of vehicle: a
        /// car's driver on the pedals (well ahead: car seats are low and the legs go out), car passengers on the floor
        /// ahead, bus / truck / van / auto seats (chair height) under the knees, a motorbike rider on the footpegs just ahead
        /// of the hips, a scooty rider on the floorboard well ahead, the pillion on the rear pegs, side-saddle both on the
        /// left. Snap then sets them on the vehicle's real floor.</summary>
        void DefaultFeet(VehicleSetupData.Seat s)
        {
            s.feet = true;
            var rot = Quaternion.Euler(s.euler);
            Vector3 hips = s.pos + Vector3.up * VehicleSetup.HipsOverRoot(s.pose);
            Vector3 R = rot * Vector3.right, F = rot * Vector3.forward;
            string m = (Current ?? "").ToLowerInvariant();
            bool scooty = m.Contains("scooty"), two = VehicleSetup.TwoWheeler(Current), chair = ChairSeats();
            float half = 0.15f, down, ahead;
            float? absY = null;
            switch (s.pose)
            {
                case SeatPose.Ride:
                    half = scooty ? 0.13f : 0.19f; ahead = scooty ? 0.40f : 0.14f; down = 0f; absY = scooty ? 0.26f : 0.32f; break;
                case SeatPose.Pillion:
                    // the rear pegs sit under the pillion's knees: straight under the hips made the legs hang straight
                    // and he looked stood up (street160 / scooty, 20 Nov)
                    half = 0.21f; ahead = 0.28f; down = 0f; absY = scooty ? 0.30f : 0.40f; break;
                case SeatPose.SideSaddle:
                    s.footL = hips - R * 0.28f + F * 0.12f; s.footL.y = two ? 0.30f : hips.y - 0.34f;
                    s.footR = hips - R * 0.30f - F * 0.08f; s.footR.y = s.footL.y;
                    return;
                case SeatPose.Drive:
                    // 0.75 m ahead was out of the leg's reach in 4 of the cars (21-24 cm short, 20 Nov)
                    ahead = chair ? 0.40f : 0.62f; down = chair ? 0.34f : 0.19f; break;
                default:
                    ahead = chair ? 0.32f : 0.52f; down = chair ? 0.34f : 0.19f; break;
            }
            if (two && s.pose != SeatPose.Ride && s.pose != SeatPose.Pillion) absY = 0.32f;
            Vector3 c = hips + F * ahead - Vector3.up * down;
            if (absY.HasValue) c.y = absY.Value;
            c.y = Mathf.Max(c.y, 0.1f);
            s.footL = c - R * half;
            s.footR = c + R * half;
        }

        /// <summary>Snap (seats a..b) onto the vehicle's own shape the way Tools/vehicles/fit_vehicle_seats.py fits them
        /// (docs/VEHICLES.md section 0 item 4, 21 Nov 2026), measured by rays straight down through its meshes every 5 cm
        /// along the seat's line (every surface, back faces too - VehicleSetupShots.AllHits):
        /// a cushion under the hips (the highest up-facing surface from 0.6 m under to 0.25 m over them, a run shorter than
        /// 1.2 m along the seat's line): the hips HumanoidRig.NominalButt over it, 0.17 m in front of its rear edge (the
        /// backrest's foot) - further forward while the floor is out of the shortest legs' reach (0.50 m) - and the feet on
        /// the floor just past its front edge (a driver 10 cm further, on the pedals), ankles HumanoidRig.NominalSoleSeated
        /// over it; no cushion (an empty Tripo shell): the hips 0.27 m (a car) / 0.42 m (chair seats) over the underbody's
        /// inside, each foot on the floor under it; a two-wheeler: the hips NominalButt over the seat's top on the centre
        /// line, the feet stay (the pegs). The sitting surface and the roof's inside are stored in the seat (surface /
        /// roof: the game keeps each head under the roof). Per-model hand fixes (no seat in the model, two people in one
        /// seat) are in the fitter's MOVES / OVR, not here: a vehicle the owner sets up anew should still go through it.</summary>
        void SnapSeats(int a, int b)
        {
            if (!vehicle) return;
            var temp = new List<Collider>();
            foreach (var mf in vehicle.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || mf.GetComponentInParent<SetupHandle>() || mf.gameObject.layer == 9) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                temp.Add(mc);
            }
            foreach (var sk in vehicle.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!sk.sharedMesh || sk.GetComponentInParent<SetupHandle>() || sk.gameObject.layer == 9) continue;
                var baked = new Mesh();
                sk.BakeMesh(baked, true);
                var mc = sk.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = baked;
                temp.Add(mc);
            }
            Physics.SyncTransforms();
            var V = vehicle.transform;
            bool two = VehicleSetup.TwoWheeler(Current), chair = ChairSeats();
            const float NB = HumanoidRig.NominalButt, SOLE = HumanoidRig.NominalSoleSeated, REACH = 0.50f, DRIVE_EXTRA = 0.10f;
            float top = V.InverseTransformPoint(box.max).y + 0.5f;
            float zMin = V.InverseTransformPoint(box.min).z, zMax = V.InverseTransformPoint(box.max).z;
            if (zMin > zMax) (zMin, zMax) = (zMax, zMin);
            // every surface straight down at (x, z) of the vehicle's space: (height, back face, part)
            var cache = new Dictionary<(int, int), List<(float y, bool back, string part)>>();
            List<(float y, bool back, string part)> Col(float x, float z)
            {
                var key = (Mathf.RoundToInt(x * 100f), Mathf.RoundToInt(z * 100f));
                if (cache.TryGetValue(key, out var c)) return c;
                c = new List<(float, bool, string)>();
                foreach (var h in VehicleSetupShots.AllHits(V.TransformPoint(new Vector3(x, top, z)), -V.up, top + 1f, temp))
                    c.Add((V.InverseTransformPoint(h.point).y, Vector3.Dot(h.normal, V.up) < 0f, h.collider.name));
                cache[key] = c;
                return c;
            }
            List<float> Ups(List<(float y, bool back, string part)> hs, float lo, float hi) =>
                hs.Where(h => !h.back && h.y >= lo && h.y <= hi && !h.part.StartsWith("Wheel")).Select(h => h.y).ToList();
            float RoofIn(List<(float y, bool back, string part)> hs, float above)
            {
                var r = hs.Where(h => h.back && h.y > above).Select(h => h.y).ToList();
                return r.Count > 0 ? r.Max() : -1f;
            }
            // the run of 5 cm steps along z (through z0) where an up surface stands within 3.5 cm of c
            (float lo, float hi)? Run(float x, float z0, float c)
            {
                bool On(float z) => Col(x, z).Any(h => !h.back && Mathf.Abs(h.y - c) <= 0.035f);
                if (!On(z0)) return null;
                float lo = z0, hi = z0;
                while (lo - 0.05f >= zMin && On(lo - 0.05f)) lo -= 0.05f;
                while (hi + 0.05f <= zMax && On(hi + 0.05f)) hi += 0.05f;
                return (lo, hi);
            }
            int seated = 0, footed = 0;
            for (int i = Mathf.Max(0, a); i <= b && i < data.seats.Length && i < seats.Count; i++)
            {
                var s = data.seats[i];
                var p = seats[i];
                if (!p) continue;
                float over = VehicleSetup.HipsOverRoot(s.pose);
                float x = s.pos.x, z = s.pos.z, hips = s.pos.y + over;
                int face = Mathf.Abs(Mathf.DeltaAngle(s.euler.y, 180f)) < 30f ? -1 : 1;
                var hs = Col(x, z);
                if (two)
                {
                    var tops = Col(0f, z).Where(h => !h.back && !h.part.StartsWith("Handle") && !h.part.StartsWith("Wheel")).Select(h => h.y).ToList();
                    if (tops.Count == 0) continue;
                    float surf = tops.Max();
                    s.pos.y = surf + NB - over; s.surface = surf; s.roof = -1f;
                    seated++;
                    continue;
                }
                var cand = Ups(hs, hips - 0.6f, hips + 0.25f);
                float? cushion = cand.Count > 0 ? cand.Max() : (float?)null;
                var run = cushion.HasValue ? Run(x, z, cushion.Value) : null;
                if (cushion.HasValue && run.HasValue && run.Value.hi - run.Value.lo > 1.2f) cushion = null;   // the floor itself
                if (cushion.HasValue && run.HasValue)
                {
                    float cu = cushion.Value;
                    float rear = face > 0 ? run.Value.lo : run.Value.hi, front = face > 0 ? run.Value.hi : run.Value.lo, depth = run.Value.hi - run.Value.lo;
                    float? floorY = null;
                    float fz = 0f;
                    for (int k = 1; k < 14; k++)
                    {
                        float zz = front + face * 0.05f * k;
                        var fl = Ups(Col(x, zz), cu - 0.9f, cu - 0.08f);
                        if (fl.Count > 0) { fz = zz; floorY = fl.Max(); break; }
                    }
                    if (!floorY.HasValue) { floorY = cu - 0.42f; fz = front + face * 0.08f; }
                    float floor = floorY.Value;
                    float footZ = front + face * (0.07f + (s.pose == SeatPose.Drive ? DRIVE_EXTRA : 0f));
                    if (face * (footZ - fz) < 0f) footZ = fz;   // not before the floor starts
                    float hipsY = cu + NB, hz = rear + face * 0.17f;
                    float Dist(float hz_) => Mathf.Sqrt((footZ - hz_) * (footZ - hz_) + (hipsY - 0.07f - (floor + SOLE)) * (hipsY - 0.07f - (floor + SOLE)) + 0.06f * 0.06f);
                    while (Dist(hz) > REACH && face * (hz - rear) < depth * 0.6f) hz += face * 0.01f;
                    if (Dist(hz) > REACH && s.pose == SeatPose.Drive)
                        while (Dist(hz) > REACH && face * (footZ - front) > 0.04f) footZ -= face * 0.01f;
                    if (Mathf.Abs(s.entry.z - s.pos.z) < 0.02f) s.entry.z = hz;
                    s.pos.y = hipsY - over;
                    s.pos.z = hz;
                    s.surface = cu;
                    s.roof = RoofIn(hs, hipsY + 0.3f);
                    float spread = s.feet ? Mathf.Abs(s.footR.x - s.footL.x) * 0.5f : 0.15f;
                    if (spread < 0.10f || spread > 0.22f) spread = 0.15f;
                    s.feet = true;
                    s.footL = new Vector3(x - face * spread, floor + SOLE, footZ);
                    s.footR = new Vector3(x + face * spread, floor + SOLE, footZ);
                    seated++; footed += 2;
                }
                else
                {
                    // an empty shell: the floor = the underbody's inside under the hips
                    var backs = hs.Where(h => h.back && h.y < hips).Select(h => h.y).ToList();
                    if (backs.Count == 0) continue;
                    float fl = backs.Min();
                    float want = fl + (chair ? 0.42f : 0.27f);
                    s.pos.y = want - over;
                    s.surface = want - NB;
                    s.roof = RoofIn(hs, want + 0.3f);
                    seated++;
                    if (!s.feet) DefaultFeet(s);
                    foreach (bool left in new[] { true, false })
                    {
                        Vector3 f = left ? s.footL : s.footR;
                        var frow = Col(f.x, f.z);
                        var fu = frow.Where(h => !h.back && h.y < want - 0.1f && !h.part.StartsWith("Wheel")).Select(h => h.y).ToList();
                        var fb = frow.Where(h => h.back && h.y < want).Select(h => h.y).ToList();
                        float fy = fu.Count > 0 ? fu.Max() : fb.Count > 0 ? fb.Min() : fl;
                        f.y = fy + SOLE;
                        if (left) s.footL = f; else s.footR = f;
                        footed++;
                    }
                }
            }
            foreach (var mc in temp) Destroy(mc);
            // the people and the markers onto the new numbers (Collect reads the scene back)
            for (int i = Mathf.Max(0, a); i <= b && i < data.seats.Length && i < seats.Count; i++)
            {
                if (!seats[i]) continue;
                Seat(i);
                if (i < entries.Count && entries[i]) entries[i].transform.localPosition = data.seats[i].entry;
                if (i < feet.Count && feet[i].l && feet[i].r)
                {
                    feet[i].l.transform.position = vehicle.transform.TransformPoint(data.seats[i].footL);
                    feet[i].r.transform.position = vehicle.transform.TransformPoint(data.seats[i].footR);
                }
            }
            Collect();
            status = $"Snap (the fitter's rule): {seated} people on their seats, {footed} feet on the floor - surface / roof stored";
        }

        /// <summary>Handlebar / steering wheel back to straight (the preview turns them every frame from the slider).</summary>
        void SteerAtRest()
        {
            foreach (var (t, rest) in bars) if (t) t.localRotation = rest;
            foreach (var (t, rest) in steeringWheels) if (t) t.localRotation = rest;
        }

        /// <summary>What the hands hold: the handlebar (two-wheelers, the tuktuk), else the steering wheel, else the body.</summary>
        Transform HandsParent() => bars.Count > 0 && bars[0].t ? bars[0].t : steeringWheels.Count > 0 && steeringWheels[0].t ? steeringWheels[0].t : vehicle.transform;

        /// <summary>Where the wrists start: the grips (handlebar), the rim at ten-to-two (steering wheel), else in front
        /// of the sitter at a guess.</summary>
        void DefaultHands(VehicleSetupData.Seat s)
        {
            s.hands = true;
            var on = HandsParent();
            Vector3 c;
            float half;
            if (bars.Count > 0 && on == bars[0].t)
            {
                // the grips: the handlebar mesh's widest points (the bar's origin is on the steering head, low and well
                // ahead of the grips - the scooty rider reached down to the apron for them, 20 Nov)
                if (Grips(on, out var gl, out var gr)) { s.handL = gl; s.handR = gr; return; }
                c = vehicle.transform.InverseTransformPoint(on.position) + new Vector3(0f, 0.1f, -0.1f); half = 0.33f;
            }
            else if (steeringWheels.Count > 0 && on == steeringWheels[0].t) { c = vehicle.transform.InverseTransformPoint(on.position) + new Vector3(0f, 0.05f, 0f); half = 0.17f; }
            else if (VehicleSetup.TwoWheeler(Current)) { c = s.pos + new Vector3(0f, 0.95f, 0.55f); half = 0.32f; }
            else { c = s.pos + new Vector3(0f, VehicleSeats.HipAboveRoot + 0.42f, 0.42f); half = VehicleSetup.ThreeWheeler(Current) ? 0.3f : 0.18f; }   // an auto's grips are wide
            s.handL = c + new Vector3(-half, 0f, 0f);
            s.handR = c + new Vector3(half, 0f, 0f);
        }

        /// <summary>Both grips of a handlebar, in vehicle space: on each side the widest vertices of the handlebar's meshes
        /// (the parts under it too, the front wheel left out), below its top 12 cm (rear-view mirror heads stand above the
        /// grips), then 6 cm in from the bar end and 2 cm down to where a fist holds it. False if the bar has no mesh.</summary>
        bool Grips(Transform bar, out Vector3 l, out Vector3 r)
        {
            l = r = Vector3.zero;
            var V = vehicle.transform;
            var pts = new List<Vector3>();
            foreach (var mf in bar.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || mf.name.StartsWith("Wheel", System.StringComparison.OrdinalIgnoreCase)) continue;
                // the FBX meshes are not Read/Write: the editor's read-only mesh data still has them
                using (var md = Mesh.AcquireReadOnlyMeshData(mf.sharedMesh))
                {
                    var vs = new Unity.Collections.NativeArray<Vector3>(md[0].vertexCount, Unity.Collections.Allocator.Temp);
                    md[0].GetVertices(vs);
                    foreach (var v in vs) pts.Add(V.InverseTransformPoint(mf.transform.TransformPoint(v)));
                    vs.Dispose();
                }
            }
            if (pts.Count < 8) return false;
            float top = pts.Max(p => p.y);
            // the grips sit in a band under the top: a scooty's "Handlebar" carries its front cowl and apron, whose low
            // edges are wider than the grips (the left target landed mid-apron, 20 Nov)
            var low = pts.Where(p => p.y < top - 0.08f && p.y > top - 0.40f).ToList();
            if (low.Count < 8) low = pts;
            Vector3 Side(int sign)
            {
                var side = low.Where(p => p.x * sign > 0.05f).ToList();
                if (side.Count == 0) return Vector3.zero;
                float w = side.Max(p => p.x * sign);
                var end = side.Where(p => p.x * sign > w - 0.03f).ToList();
                var m = new Vector3(end.Average(p => p.x), end.Average(p => p.y), end.Average(p => p.z));
                return m + new Vector3(-sign * 0.06f, -0.02f, 0f);
            }
            l = Side(-1); r = Side(1);
            return l != Vector3.zero && r != Vector3.zero;
        }

        /// <summary>A lamp back into the data: in the vehicle's space (whatever part it hangs on) and the part's name.</summary>
        void ReadLamp(GameObject g, VehicleSetupData.Lamp l)
        {
            var V = vehicle.transform;
            l.pos = V.InverseTransformPoint(g.transform.position);
            l.euler = (Quaternion.Inverse(V.rotation) * g.transform.rotation).eulerAngles;
            Vector3 s = g.transform.lossyScale, vs = V.lossyScale;
            l.size = new Vector2(s.x / Mathf.Max(1e-4f, vs.x), s.y / Mathf.Max(1e-4f, vs.y));
            l.on = g.transform.parent && g.transform.parent != V ? g.transform.parent.name : "";
        }

        void Save(bool ok)
        {
            Collect();
            data.model = Current;
            if (ok) data.approved = true;
            data.saved = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            Directory.CreateDirectory(SaveDir);
            string path = SaveDir + "/" + Current + ".json";
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
            AssetDatabase.ImportAsset(path);
            VehicleSetup.Forget(Current);
            status = (ok ? "Approved and saved: " : "Saved: ") + path;
            Debug.Log("[Nainital] vehicle setup " + status);
        }

        static VehicleSetupData Load(string model)
        {
            string path = SaveDir + "/" + model + ".json";
            return File.Exists(path) ? JsonUtility.FromJson<VehicleSetupData>(File.ReadAllText(path)) : null;
        }

        static bool Saved(string model) => File.Exists(SaveDir + "/" + model + ".json");
        static bool Approved(string model) { var d = Load(model); return d != null && d.approved; }

        void MirrorSelected()
        {
            var g = Selection.activeGameObject;
            var h = g ? g.GetComponent<SetupHandle>() : null;
            if (!h || h.kind != SetupHandle.Kind.Seat) { status = "Select a seat (a person) first."; return; }
            Collect();
            var s = data.seats[h.index];
            var m = new VehicleSetupData.Seat { pose = s.pose == SeatPose.Drive ? SeatPose.Sit : s.pose, pos = new Vector3(-s.pos.x, s.pos.y, s.pos.z), euler = new Vector3(s.euler.x, -s.euler.y, -s.euler.z), entry = new Vector3(-s.entry.x, s.entry.y, s.entry.z) };
            Add(ref data.seats, m);
            Build();
        }

        void AddSeat()
        {
            var last = data.seats.Length > 0 ? data.seats[data.seats.Length - 1] : null;
            var s = new VehicleSetupData.Seat { pose = SeatPose.Sit };
            if (last != null) { s.pos = last.pos + new Vector3(0f, 0f, -0.8f); s.euler = last.euler; s.entry = last.entry + new Vector3(0f, 0f, -0.8f); }
            if (s.pose == SeatPose.Drive) s.pose = SeatPose.Sit;
            Add(ref data.seats, s);
        }

        VehicleSetupData.Lamp DefaultLamp(bool head)
        {
            bool two = VehicleSetup.TwoWheeler(Current);
            float L = box.size.z, W = box.size.x, H = box.size.y;
            if (two) return new VehicleSetupData.Lamp { pos = new Vector3(0f, head ? H * 0.78f : H * 0.68f, head ? box.max.z - 0.2f : box.min.z + 0.08f), size = new Vector2(0.14f, 0.1f), mirror = false };
            return new VehicleSetupData.Lamp { pos = new Vector3(-(W * 0.5f - 0.32f), head ? 0.78f : 0.9f, head ? box.max.z - 0.03f : box.min.z + 0.03f), size = new Vector2(0.24f, 0.12f), mirror = true };
        }

        /// <summary>A starting point: as many seats as the vehicle carries, rows from the front, right-hand drive.</summary>
        VehicleSetupData Default()
        {
            var d = new VehicleSetupData { model = Current };
            string m = Current.ToLowerInvariant();
            int cap = VehicleSetup.Capacity(Current);
            float L = box.size.z, W = box.size.x, H = box.size.y, zF = box.max.z, half = W * 0.5f;
            var list = new List<VehicleSetupData.Seat>();
            void S(SeatPose p, float x, float hipY, float z, float yaw = 0f, int side = 0)
            {
                float ex = side != 0 ? side * (half + 0.6f) : (x > 0.05f ? half + 0.6f : -(half + 0.6f));
                // hipY = the Hips bone's height over the road (the clips hold it HipsOverRoot over the root: measured 20 Nov)
                list.Add(new VehicleSetupData.Seat { pose = p, pos = new Vector3(x, hipY - VehicleSetup.HipsOverRoot(p), z), euler = new Vector3(0f, yaw, 0f), entry = new Vector3(ex, 0f, z) });
            }
            if (VehicleSetup.TwoWheeler(Current))
            {
                float seatTop = H * 0.57f;
                // the rider an arm's reach behind the grips (a scooty's long seat left him 0.24 m short of them, the
                // dual-sport 0.37 m, 20 Nov), the pillion 0.38 m behind him; Snap then sets both on the seat's top
                var bar = vehicle ? vehicle.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.StartsWith("Handle", System.StringComparison.OrdinalIgnoreCase) || t.name.StartsWith("Fork", System.StringComparison.OrdinalIgnoreCase)) : null;
                float zr = box.center.z - L * 0.1f;
                // from the grips themselves (the bar's origin is on the steering head, well ahead of them: measuring from
                // it sat the commuter-125 rider low and far back, 20 Nov)
                if (bar && Grips(bar, out var gl, out var gr)) zr = Mathf.Clamp((gl.z + gr.z) * 0.5f - 0.50f, box.center.z - L * 0.25f, box.center.z + L * 0.1f);
                else if (bar) zr = Mathf.Clamp(vehicle.transform.InverseTransformPoint(bar.position).z - 0.1f - 0.62f, box.center.z - L * 0.25f, box.center.z + L * 0.1f);
                list.Add(new VehicleSetupData.Seat { pose = SeatPose.Ride, pos = new Vector3(0f, seatTop + 0.10f - VehicleSetup.HipsOverRoot(SeatPose.Ride), zr), entry = new Vector3(-0.9f, 0f, zr) });
                list.Add(new VehicleSetupData.Seat { pose = SeatPose.Pillion, pos = new Vector3(0f, seatTop + 0.12f - VehicleSetup.HipsOverRoot(SeatPose.Pillion), zr - 0.38f), entry = new Vector3(-0.9f, 0f, zr - 0.38f) });
            }
            else if (m.Contains("tuktuk"))
            {
                S(SeatPose.Drive, 0f, 0.75f, zF - L * 0.3f, 0f, 1);
                for (int k = 0; k < cap - 1; k++) S(SeatPose.Sit, (k - 1) * 0.42f, 0.7f, zF - L * 0.62f, 0f, -1);
            }
            else if (m.Contains("tirri"))
            {
                S(SeatPose.Drive, 0f, 0.8f, zF - L * 0.22f, 0f, 1);
                S(SeatPose.Sit, 0.3f, 0.75f, zF - L * 0.48f, 0f); S(SeatPose.Sit, -0.3f, 0.75f, zF - L * 0.48f, 0f);
                S(SeatPose.Sit, 0.3f, 0.75f, zF - L * 0.78f, 180f); S(SeatPose.Sit, -0.3f, 0.75f, zF - L * 0.78f, 180f);
            }
            else if (L > 6.5f)
            {
                // bus: the driver up front on the right, then rows of 2 + 2 with the aisle in the middle
                float hip = 1.3f;
                S(SeatPose.Drive, half - 0.75f, hip, zF - 1.1f);
                float z = zF - 2.4f;
                int left = cap - 1;
                while (left > 0 && z > box.min.z + 0.5f)
                {
                    foreach (float x in new[] { half - 0.45f, half - 0.95f, -(half - 0.95f), -(half - 0.45f) })
                        if (left-- > 0) S(SeatPose.Sit, x, hip, z, 0f, -1);
                    z -= 0.78f;
                }
            }
            else
            {
                bool cab = m.Contains("pickup") || m.Contains("truck") || m.Contains("van") || m == "tata407" || m == "boleropickup";
                float hip = cab ? Mathf.Min(1.15f, H * 0.45f) : Mathf.Clamp(Mathf.Min(0.33f + H * 0.2f + 0.12f, H - 1.1f), 0.38f, 0.75f);
                float z0 = zF - L * (cab ? 0.22f : 0.47f);
                float xo = half - 0.55f;
                S(SeatPose.Drive, xo, hip, z0);
                int left = cap - 1;
                if (cab) { for (int k = 0; k < left; k++) S(SeatPose.Sit, k == 0 ? -xo : 0f, hip, z0, 0f, -1); }
                else
                {
                    S(SeatPose.Sit, -xo, hip, z0); left--;
                    int[] rows = cap >= 8 ? new[] { 3, 3 } : cap >= 7 ? new[] { 3, 2 } : new[] { 3 };
                    float z = z0;
                    foreach (int n in rows)
                    {
                        z -= 0.88f;
                        for (int k = 0; k < n && left > 0; k++, left--)
                            S(SeatPose.Sit, n == 3 ? (k - 1) * 0.5f : (k == 0 ? 0.35f : -0.35f), hip + 0.03f, z);
                    }
                }
            }
            d.seats = list.ToArray();
            d.head = new[] { DefaultLamp(true) };
            d.tail = new[] { DefaultLamp(false) };
            return d;
        }

        static void Add<T>(ref T[] a, T v) { var l = a.ToList(); l.Add(v); a = l.ToArray(); }
        static void RemoveAt<T>(ref T[] a, int i) { var l = a.ToList(); l.RemoveAt(i); a = l.ToArray(); }
    }
}
