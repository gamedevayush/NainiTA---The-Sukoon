using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Batch check of the animals' death ragdoll (24 Nov 2026), no Play: "call AnimalRagdollCheck.Run". A ground plane, the
    /// four animals as AnimalManager builds them (CharacterController, CharacterMotor, Health, Animal), each killed by
    /// AnimalRagdoll.Kill with a sideways throw (4 m/s), physics stepped by hand for 2.5 s. Writes Logs/ragdollcheck.txt
    /// (bodies / joints made, hips height and drift, the lowest collider vs the ground, any NaN or a body flown off) and
    /// Shots/animal_ragdoll.png (before / after, side view).
    /// </summary>
    public static class AnimalRagdollCheck
    {
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            var sun = new GameObject("sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f; sun.transform.rotation = Quaternion.Euler(45f, 120f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var gm = new Material(lit); gm.SetColor("_BaseColor", new Color(0.35f, 0.38f, 0.35f));
            ground.GetComponent<Renderer>().sharedMaterial = gm;

            var kinds = new[] { AnimalKind.Cow, AnimalKind.Dog, AnimalKind.Cat, AnimalKind.Monkey };
            var animals = new Animal[kinds.Length];
            var sb = new StringBuilder();
            for (int i = 0; i < kinds.Length; i++)
            {
                string name = kinds[i].ToString().ToLowerInvariant();
                var prefab = Resources.Load<GameObject>("Animals/" + name);
                if (!prefab) { sb.AppendLine(name + ": no model"); continue; }
                var spec = Animal.SpecOf(kinds[i]);
                var go = new GameObject(name) { layer = 9 };
                go.transform.position = new Vector3(i * 3f - 4.5f, 0f, 0f);
                var cc = go.AddComponent<CharacterController>();
                cc.radius = spec.radius; cc.height = Mathf.Max(spec.height, spec.radius * 2f + 0.06f); cc.center = Vector3.up * (cc.height * 0.5f + 0.02f);
                go.AddComponent<CharacterMotor>();
                go.AddComponent<Health>();
                var model = Object.Instantiate(prefab, go.transform, false);
                model.name = "Model";
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // AnimalManager.modelYaw
                var tex = Resources.Load<Texture2D>("Animals/" + name + "_color");
                var m = new Material(lit); if (tex) m.SetTexture("_BaseMap", tex);
                foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
                var a = go.AddComponent<Animal>();
                a.kind = kinds[i];
                a.spec = spec;
                animals[i] = a;
            }
            var cam = new GameObject("cam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.75f, 0.78f, 0.82f);
            cam.transform.position = new Vector3(0f, 2.2f, -11f); cam.transform.LookAt(new Vector3(0f, 0.5f, 0f)); cam.fieldOfView = 40f;
            var sheet = new Texture2D(1200, 900, TextureFormat.RGB24, false);
            Snap(cam, sheet, 450);

            var start = new Vector3[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                if (!animals[i]) continue;
                var body = AnimalRagdoll.Kill(animals[i], new Vector3(0f, 1.5f, 4f));
                var rbs = animals[i].GetComponentsInChildren<Rigidbody>();
                var js = animals[i].GetComponentsInChildren<CharacterJoint>();
                start[i] = body && body.Hips ? body.Hips.position : animals[i].transform.position;
                sb.Append($"{kinds[i]}: ragdoll {(body ? "ok" : "FAILED")} bodies {rbs.Length} joints {js.Length} mass {SumMass(rbs):F0} kg\n");
                animals[i].enabled = false;
            }
            var mode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            for (int s = 0; s < 125; s++) Physics.Simulate(0.02f);
            Physics.simulationMode = mode;
            for (int i = 0; i < kinds.Length; i++)
            {
                if (!animals[i]) continue;
                var rbs = animals[i].GetComponentsInChildren<Rigidbody>();
                float minY = float.MaxValue, maxSp = 0f; bool nan = false;
                foreach (var c in animals[i].GetComponentsInChildren<Collider>()) if (!(c is CharacterController)) minY = Mathf.Min(minY, c.bounds.min.y);
                foreach (var r in rbs) { maxSp = Mathf.Max(maxSp, r.linearVelocity.magnitude); if (float.IsNaN(r.position.x)) nan = true; }
                var hips = rbs.Length > 0 ? rbs[0].transform.position : Vector3.zero;
                sb.Append($"{kinds[i]} after 2.5 s: hips y {hips.y:F2} moved {Vector3.Distance(hips, start[i]):F2} m, lowest collider {minY:F2}, max speed {maxSp:F2}, NaN {nan}\n");
            }
            // edit mode does not re-skin before a manual Render: bake each skin in its fallen pose and draw that instead
            foreach (var smr in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            {
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var go = new GameObject(smr.name + "_baked");
                go.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                go.AddComponent<MeshFilter>().sharedMesh = baked;
                go.AddComponent<MeshRenderer>().sharedMaterial = smr.sharedMaterial;
                smr.enabled = false;
            }
            Snap(cam, sheet, 0);
            sheet.Apply();
            System.IO.Directory.CreateDirectory("Shots");
            System.IO.File.WriteAllBytes("Shots/animal_ragdoll.png", sheet.EncodeToPNG());
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/ragdollcheck.txt", sb.ToString());
            Debug.Log("[Nainital] AnimalRagdollCheck\n" + sb);
        }

        static float SumMass(Rigidbody[] rbs) { float m = 0f; foreach (var r in rbs) m += r.mass; return m; }

        static void Snap(Camera cam, Texture2D sheet, int y)
        {
            var rt = new RenderTexture(1200, 450, 24);
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var t = new Texture2D(1200, 450, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, 1200, 450), 0, 0); t.Apply();
            sheet.SetPixels(0, y, 1200, 450, t.GetPixels());
            RenderTexture.active = null; cam.targetTexture = null;
        }
    }
}
