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
        readonly List<(Transform t, Quaternion rest, VehicleSetupData.Door d)> doors = new List<(Transform, Quaternion, VehicleSetupData.Door)>();
        readonly Dictionary<string, float> doorOpen = new Dictionary<string, float>();
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

        void OnEnable() { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += PlayChanged; Refresh(); }
        void OnDisable() { EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayChanged; }
        void PlayChanged(PlayModeStateChange s) { if (s == PlayModeStateChange.EnteredPlayMode) { Refresh(); Show(index); } }

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
                foreach (var e in VehicleLibrary.Instance.entries) if (e != null && e.prefab) items.Add(new Item { name = e.name, source = e.prefab });
                foreach (var e in VehicleLibrary.Instance.bikes) if (e != null && e.prefab) items.Add(new Item { name = e.name, source = e.prefab });
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
                    if (GUILayout.Button("L", GUILayout.Width(22))) Pick(hands[i].l);
                    if (GUILayout.Button("R", GUILayout.Width(22))) Pick(hands[i].r);
                }
                if (i > 0 && GUILayout.Button("✕", GUILayout.Width(22))) { Collect(); RemoveAt(ref data.seats, i); Build(); GUIUtility.ExitGUI(); }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Seat")) { Collect(); AddSeat(); Build(); }
            if (GUILayout.Button("Mirror selected seat")) MirrorSelected();
            EditorGUILayout.EndHorizontal();

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
            if (GUILayout.Button("Back to default", GUILayout.Height(30)) && EditorUtility.DisplayDialog("Vehicle Setup", "Throw away this vehicle's setup and start from the default seats and lamps?", "Yes", "No")) { data = Default(); Build(); }
            EditorGUILayout.EndHorizontal();
            if (status.Length > 0) EditorGUILayout.HelpBox(status, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        static void Section(string t) { EditorGUILayout.Space(6); EditorGUILayout.LabelField(t, EditorStyles.boldLabel); }
        static void Pick(GameObject g) { if (!g) return; Selection.activeGameObject = g; SceneView.lastActiveSceneView?.FrameSelected(); }

        void LampRows(List<GameObject> objs, ref VehicleSetupData.Lamp[] arr, string label)
        {
            for (int i = 0; i < arr.Length && i < objs.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(label + " " + i, GUILayout.Width(52));
                var l = arr[i];
                bool m = EditorGUILayout.ToggleLeft("mirror", l.mirror, GUILayout.Width(58));
                if (m != l.mirror) { Collect(); l.mirror = m; Build(); GUIUtility.ExitGUI(); }
                var sz = EditorGUILayout.Vector2Field(GUIContent.none, l.size, GUILayout.Width(120));
                if (sz != l.size) { l.size = sz; if (objs[i]) objs[i].transform.localScale = new Vector3(sz.x, sz.y, 1f); }
                if (GUILayout.Button("Select", GUILayout.Width(52))) Pick(objs[i]);
                if (GUILayout.Button("✕", GUILayout.Width(22))) { Collect(); RemoveAt(ref arr, i); Build(); GUIUtility.ExitGUI(); }
                EditorGUILayout.EndHorizontal();
            }
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
            data = Load(Current) ?? Default();
            Scan();
            Build();
            status = "";
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

        /// <summary>Wheels, handlebar, doors and damage keys of the model, by their names.</summary>
        void Scan()
        {
            wheels.Clear(); bars.Clear(); steeringWheels.Clear(); doors.Clear(); damage.Clear(); doorOpen.Clear(); spin = 0f;
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
                    var r = t.GetComponent<Renderer>();
                    if (r) wheelR = Mathf.Max(0.15f, r.bounds.extents.y);
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
                                           : $"✓ {w.t.name}: origin at the centre, radius {r.bounds.extents.y:F2} m" + (w.viaBar ? ", steers with " + w.t.parent.name : ""));
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
            foreach (var (l, r) in hands) { if (l) Destroy(l); if (r) Destroy(r); }
            seats.Clear(); entries.Clear(); heads.Clear(); tails.Clear(); hands.Clear();
            SteerAtRest();   // the wrist points are kept with the steering straight
            for (int i = 0; i < data.seats.Length; i++)
            {
                var s = data.seats[i];
                var p = Person("Seat " + i + (i == 0 ? " (driver)" : ""), i > 0 && Random.value < 0.45f);
                p.transform.SetParent(vehicle.transform, false);
                var h = p.AddComponent<SetupHandle>(); h.kind = SetupHandle.Kind.Seat; h.index = i;
                seats.Add(p);
                var e = Marker("Entry " + i, new Color(0.2f, 0.9f, 0.3f));
                var eh = e.AddComponent<SetupHandle>(); eh.kind = SetupHandle.Kind.Entry; eh.index = i;
                e.transform.SetParent(vehicle.transform, false);
                e.transform.localPosition = s.entry;
                entries.Add(e);
                Seat(i);
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
                    var ik = p.AddComponent<HandIK>();
                    ik.left = hl.transform; ik.right = hr.transform; ik.vehicle = vehicle.transform;
                    hands.Add((hl, hr));
                }
                else hands.Add((null, null));
            }
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

        static GameObject Person(string name, bool female)
        {
            var go = new GameObject(name) { layer = 9 };
            var rigGo = new GameObject("Rig") { layer = 9 };
            rigGo.transform.SetParent(go.transform, false);
            rigGo.SetActive(false);
            var rig = rigGo.AddComponent<HumanoidRig>();
            rig.female = female;
            rig.bodyType = female ? HumanoidRig.BodyType.Woman : HumanoidRig.BodyType.Man;
            var lib = CharacterLibrary.Instance;
            var look = lib ? lib.Pick(female, false, null) : null;
            if (look != null) CharacterLibrary.Apply(look, rig, null);
            rigGo.SetActive(true);
            return go;
        }

        void Reroll(int i)
        {
            Collect();
            var old = seats[i];
            var rig = old.GetComponentInChildren<HumanoidRig>(true);
            var p = Person(old.name, rig && !rig.female);
            p.transform.SetParent(vehicle.transform, false);
            var h = p.AddComponent<SetupHandle>(); h.kind = SetupHandle.Kind.Seat; h.index = i;
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
                if (!t) continue;
                doorOpen.TryGetValue(d.name, out float o);
                float a = d.openDeg * o * (d.flip ? -1f : 1f);
                Vector3 axis = d.hinge == DoorHinge.Side ? Vector3.up : Vector3.right;
                if (d.hinge == DoorHinge.Bottom) a = -a;
                t.localRotation = Quaternion.AngleAxis(a, axis) * rest;
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
                pik.left = oldIk.left; pik.right = oldIk.right; pik.vehicle = oldIk.vehicle;
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
            for (int i = 0; i < data.head.Length && i < heads.Count; i++) if (heads[i]) ReadLamp(heads[i], data.head[i]);
            for (int i = 0; i < data.tail.Length && i < tails.Count; i++) if (tails[i]) ReadLamp(tails[i], data.tail[i]);
            // the wrists, read with the steering straight (the next frame turns it back to the slider)
            SteerAtRest();
            for (int i = 0; i < data.seats.Length && i < hands.Count; i++)
            {
                var (l, r) = hands[i];
                if (!l || !r) continue;
                data.seats[i].hands = true;
                data.seats[i].handL = vehicle.transform.InverseTransformPoint(l.transform.position);
                data.seats[i].handR = vehicle.transform.InverseTransformPoint(r.transform.position);
            }
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
            if (bars.Count > 0 && on == bars[0].t) { c = vehicle.transform.InverseTransformPoint(on.position) + new Vector3(0f, 0.1f, -0.1f); half = 0.33f; }
            else if (steeringWheels.Count > 0 && on == steeringWheels[0].t) { c = vehicle.transform.InverseTransformPoint(on.position) + new Vector3(0f, 0.05f, 0f); half = 0.17f; }
            else if (VehicleSetup.TwoWheeler(Current)) { c = s.pos + new Vector3(0f, 0.95f, 0.55f); half = 0.32f; }
            else { c = s.pos + new Vector3(0f, VehicleSeats.HipAboveRoot + 0.42f, 0.42f); half = VehicleSetup.ThreeWheeler(Current) ? 0.3f : 0.18f; }   // an auto's grips are wide
            s.handL = c + new Vector3(-half, 0f, 0f);
            s.handR = c + new Vector3(half, 0f, 0f);
        }

        static void ReadLamp(GameObject g, VehicleSetupData.Lamp l)
        {
            l.pos = g.transform.localPosition;
            l.euler = g.transform.localEulerAngles;
            l.size = new Vector2(g.transform.localScale.x, g.transform.localScale.y);
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
                list.Add(new VehicleSetupData.Seat { pose = p, pos = new Vector3(x, hipY - VehicleSeats.HipAboveRoot, z), euler = new Vector3(0f, yaw, 0f), entry = new Vector3(ex, 0f, z) });
            }
            if (VehicleSetup.TwoWheeler(Current))
            {
                float seatTop = H * 0.57f;
                // riders: root as BikeRide.SeatLocal (the riding pose holds its own height)
                list.Add(new VehicleSetupData.Seat { pose = SeatPose.Ride, pos = new Vector3(0f, seatTop + 0.05f - 0.51f, box.center.z - L * 0.1f), entry = new Vector3(-0.9f, 0f, box.center.z - L * 0.1f) });
                list.Add(new VehicleSetupData.Seat { pose = SeatPose.Pillion, pos = new Vector3(0f, seatTop + 0.08f - 0.51f, box.center.z - L * 0.1f - 0.38f), entry = new Vector3(-0.9f, 0f, box.center.z - L * 0.3f) });
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
