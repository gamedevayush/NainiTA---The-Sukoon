using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nainital.EditorTools
{
    /// <summary>Renders review screenshots of the generated scene into &lt;project&gt;/Shots.</summary>
    public static class PreviewCapture
    {
        [MenuItem("Nainital/3. Capture preview screenshots", priority = 3)]
        public static void CaptureMenu() => Capture();

        public static void BuildAndCaptureBatch()
        {
            try
            {
                ProjectSetup.Run();
                WorldBuilder.Build();
                Capture();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void CaptureBatch()
        {
            try { Capture(); }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>When set, only shots whose names start with one of these are taken ("-" = none of the standard ones).</summary>
        public static string[] Only;

        /// <summary>Extra shots for a quick look: "name;x,y,z;lookX,lookY,lookZ;hour[;g]" joined by "|". With ";g" the two
        /// heights are metres above the ground at those points instead of world heights.</summary>
        public static string Custom;

        static bool Wanted(string name)
        {
            if (Only == null) return true;
            foreach (var o in Only) if (name.StartsWith(o)) return true;
            return false;
        }

        public static void Capture()
        {
            EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
            string dir = Path.GetFullPath("Shots");
            Directory.CreateDirectory(dir);
            var player = GameObject.Find("Player");
            var dn = Object.FindAnyObjectByType<DayNightCycle>();
            var lake = Object.FindAnyObjectByType<LakeArea>();
            Vector3 lakeC = Vector3.zero;
            if (lake && lake.outline != null)
            {
                foreach (var q in lake.outline) lakeC += new Vector3(q.x, 0, q.y);
                lakeC /= lake.outline.Length;
                lakeC.y = lake.waterY;
            }

            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 7200f;
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            cam.targetTexture = rt;

            void Shot(string name, Vector3 pos, Vector3 look, float time, bool clearAir = false)
            {
                if (!Wanted(name)) return;
                if (dn) { dn.timeOfDay = time; dn.Apply(); }
                bool fogWas = RenderSettings.fog;
                if (clearAir) { RenderSettings.fog = false; Shader.SetGlobalFloat("_HazeDensity", 0f); }
                cam.transform.position = pos;
                cam.transform.LookAt(look);
                cam.Render();
                cam.Render();   // second pass so per-camera tree culling caches are warm
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                RenderSettings.fog = fogWas;
                Object.DestroyImmediate(tex);
                Debug.Log("[Nainital] shot " + name);
            }

            Vector3 p = player ? player.transform.position : lakeC;
            Quaternion r = player ? player.transform.rotation : Quaternion.identity;
            Vector3 fwd = r * Vector3.forward, right = r * Vector3.right;
            Shot("01_player_tpv_evening", p - fwd * 4.5f + Vector3.up * 2.1f + right * 0.5f, p + Vector3.up * 1.3f + fwd * 8f, 17.3f);
            Shot("02_lake_aerial_day", lakeC + new Vector3(0, 420, -950), lakeC, 11f);
            Shot("03_mallital_day", p + Vector3.up * 70f - fwd * 140f, p + fwd * 80f, 10.5f);
            Shot("04_night_aerial", lakeC + new Vector3(300, 300, -750), lakeC, 21f);
            Shot("05_promenade_night", p - fwd * 3.5f + Vector3.up * 1.8f - right * 2f, p + fwd * 30f + right * 20f, 21f);
            Vector3 slopeCam = lakeC + new Vector3(-350, 120, -200);       // keep this one above the hillside
            if (Physics.Raycast(slopeCam + Vector3.up * 600f, Vector3.down, out var slopeHit, 1200f)) slopeCam.y = Mathf.Max(slopeCam.y, slopeHit.point.y + 45f);
            Shot("06_town_slope_day", slopeCam, lakeC + new Vector3(200, 60, 150), 15.5f);

            // rigged townspeople on a floor above the town: standing posture, then mid-walk
            Vector3 stage = p + Vector3.up * 300f;
            var people = BuildPeopleLineup(stage, fwd, right, out var lineupRoot, out var floorMat);
            if (people != null)
            {
                // mocap (G.O.A.N. clips) where the character has a Humanoid avatar, else procedural
                foreach (var rig in people)
                    if (rig.Anim)
                    {
                        rig.previewMecanim = true;
                        rig.Anim.Rebind();
                        rig.DriveAnimator(0f, 0f);
                        rig.Anim.Update(0.4f);
                        rig.Step(0.02f);
                    }
                Shot("07_people_standing", stage - fwd * 6.2f + Vector3.up * 1.0f, stage + Vector3.up * 0.85f, 11f);
                int idx = 0;
                foreach (var rig in people)
                {
                    rig.speed = HumanoidRig.WalkSpeedFor(rig.bodyType);
                    if (rig.Anim)
                    {
                        rig.DriveAnimator(rig.speed, 0f);
                        for (int k = 0; k < 12; k++) rig.Anim.Update(0.05f + (idx % 4) * 0.01f);   // let the blend settle mid-stride
                        rig.Step(0.02f);
                    }
                    else for (int k = 0; k < 23; k++) rig.Step(0.04f);
                    idx++;
                }
                Shot("08_people_walking", stage - fwd * 4.8f + right * 3.6f + Vector3.up * 1.1f, stage + Vector3.up * 0.8f + right * 1.2f, 11f);
                Object.DestroyImmediate(lineupRoot);
                Object.DestroyImmediate(floorMat);
            }

            // G.O.A.N. buildings in the world (found by instance name)
            void ShotOf(string file, string name, float dist, float time)
            {
                GameObject best = null;
                float bestD = float.MaxValue;
                foreach (var lg in Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
                {
                    if (!lg.name.StartsWith(name)) continue;
                    float d = (lg.transform.position - p).sqrMagnitude;   // the one nearest the player
                    if (d < bestD) { bestD = d; best = lg.gameObject; }
                }
                if (!best) { Debug.LogWarning("[Nainital] shot " + file + ": no " + name); return; }
                var b = best.GetComponent<LODGroup>().GetLODs()[0].renderers[0].bounds;
                Vector3 c = b.center, f = best.transform.forward;
                Vector3 from = c + (f * 0.8f + best.transform.right * 0.6f).normalized * dist + Vector3.up * dist * 0.35f;
                Shot(file, from, c, time);
            }
            ShotOf("09_goan_gurudwara", "Proto_Gurudwara", 26f, 10.5f);
            ShotOf("10_goan_church", "Proto_Church", 32f, 10.5f);
            ShotOf("11_goan_hospital", "BD Pandey", 42f, 10.5f);
            ShotOf("12_goan_townhouse", "HouseProto_", 24f, 10.5f);
            ShotOf("13_goan_village", "RuralHouse_", 30f, 10.5f);
            ShotOf("14_goan_school", "Sherwood", 45f, 10.5f);

            // street level (eye height) in front of a landmark, to compare with street photos
            void Street(string file, string target, float dist, float side, float time)
            {
                var go = GameObject.Find(target);
                var rend = go ? go.GetComponentInChildren<Renderer>() : null;
                if (!rend) { Debug.LogWarning("[Nainital] shot " + file + ": no " + target); return; }
                // landmark meshes are baked in world space (object at the origin): use the mesh bounds
                Bounds b0 = rend.bounds;
                Vector3 t = new Vector3(b0.center.x, b0.min.y, b0.center.z);
                Vector3 tf = lakeC - t;
                tf.y = 0f;
                tf.Normalize();
                Vector3 tr = Vector3.Cross(Vector3.up, tf);
                Vector3 c0 = t + tf * (dist + Mathf.Max(b0.extents.x, b0.extents.z)) + tr * side;
                if (Physics.Raycast(new Vector3(c0.x, b0.max.y + 80f, c0.z), Vector3.down, out var hit, 400f)) c0.y = hit.point.y;
                Shot(file, c0 + Vector3.up * 1.7f, t + Vector3.up * 5f, time);
            }
            Street("15_street_bada_bazar", "Bada Bazar", 22f, 6f, 10.5f);
            Street("16_street_tallital", "Tallital Bus Stand", 26f, -8f, 15f);
            Street("17_street_naina_devi", "Naina Devi Mandir", 24f, 8f, 9.5f);
            foreach (var (marker, file) in new[] { ("MallShotA", "18_mall_upper_north"), ("MallShotB", "19_mall_upper_south"), ("MallShotLane", "20_mall_lower_lane"), ("MallShotLowerNorth", "21_mall_lower_north"), ("MallShotSplit", "33_mall_split"), ("MallitalShotCapitol", "34_capitol"), ("MallitalShotGate", "35_naina_devi_gate"), ("MallitalShotPlaza", "36_tibetan_market"), ("MallitalShotMallEnd", "37_mallital_mall_end"), ("MallitalShotGhat", "38_mallital_ghat"), ("FlatsShot", "56_flats"), ("TallitalShotStreetView", "30_tallital_streetview"), ("TallitalShotSV2SE", "31_tallital_sv2_se"), ("TallitalShotSV2N", "32_tallital_sv2_n"), ("TallitalShotStation", "27_tallital_station"), ("TallitalShotChowk", "28_tallital_chowk"), ("TallitalShotSchool", "29_tallital_school") })
            {
                var m = GameObject.Find(marker);
                if (!m) { Debug.LogWarning("[Nainital] no " + marker); continue; }
                Vector3 lookAt = m.transform.position + m.transform.forward * 30f;
                Shot(file + "_evening", m.transform.position, lookAt, 18.55f);
                Shot(file + "_day", m.transform.position, lookAt, 11f);
            }
            // the viewpoints: the site from behind and above, then what you see from it
            foreach (var (title, slug) in new[] { ("Snow View", "50_snow_view"), ("Naini Peak (China Peak)", "51_naina_peak"), ("Tiffin Top (Dorothy's Seat)", "52_tiffin_top"),
                                                  ("Land's End", "53_lands_end"), ("Himalaya Darshan", "54_himalaya_darshan"), ("Eco Cave Garden", "55_eco_cave"), ("Tower 360", "57_tower360"),
                                                  ("Lover's Point", "80_lovers_point"), ("Barapathar (pony stand)", "81_barapathar"), ("Sariyatal", "82_sariyatal"),
                                                  ("Sariyatal Falls", "83_sariyatal_falls"), ("Khurpatal Lake View Point", "84_khurpatal_lake_view"),
                                                  ("Khurpatal View Point", "85_khurpatal_view"), ("Khurpatal", "86_khurpatal") })
            {
                var vm = GameObject.Find("ViewShot " + title);
                if (!vm) { Debug.LogWarning("[Nainital] no ViewShot " + title); continue; }
                Vector3 o = vm.transform.position, f = vm.transform.forward, vr = vm.transform.right;
                Vector3 ff = new Vector3(f.x, 0f, f.z);   // level: a marker may look down at a lake or up at a fall
                ff = ff.sqrMagnitude > 1e-4f ? ff.normalized : Vector3.forward;
                vr = Vector3.Cross(Vector3.up, ff);
                Vector3 eye = o - ff * 4f + vr * 17f + Vector3.up * 11f;   // from one side and above the tree crowns
                if (Physics.Raycast(new Vector3(eye.x, eye.y + 80f, eye.z), Vector3.down, out var gh, 300f)) eye.y = Mathf.Max(eye.y, gh.point.y + 2f);
                Shot(slug + "_site", eye, o + ff * 3f - Vector3.up * 1.2f, 11f);
                float edge = title == "Snow View" ? 11f : 3.5f;   // at the railing, where people stand to look
                if (Mathf.Abs(f.y) < 0.05f) Shot(slug + "_view", o + ff * edge + Vector3.up * 0.3f, o + ff * 300f + Vector3.down * 25f, 11f);   // a lookout: at its rail
                else Shot(slug + "_view", o, o + f * 80f, 11f);   // a lake or a fall: where the marker looks
            }
            // straight down over the lake and the town, north up, to lay beside the satellite view
            // ground checks where the terrain was roughest (2026-11-11): standing 30 m off, looking at the spot
            foreach (var (gname, gx, gz) in new[] { ("70_ground_mall_mid", 660f, -380f), ("71_ground_mall_ne", 780f, -180f), ("72_ground_mall_s", 500f, -740f),
                                                    ("73_ground_naina_devi", -100f, -220f), ("74_ground_university", -940f, -60f), ("75_ground_east", 1660f, -820f) })
            {
                Vector3 spot = new Vector3(gx, 0f, gz);
                if (Physics.Raycast(spot + Vector3.up * 3000f, Vector3.down, out var sh, 5000f)) spot = sh.point;
                Vector3 eye = spot + new Vector3(22f, 0f, -22f);
                if (Physics.Raycast(eye + Vector3.up * 3000f, Vector3.down, out var eh, 5000f)) eye = eh.point;
                Shot(gname, eye + Vector3.up * 2f, spot + Vector3.up * 1f, 11f);
            }
            cam.fieldOfView = 54f;
            Shot("60_topdown_town", new Vector3(430f, 1900f, -480f), new Vector3(430f, 0f, -479.9f), 11.5f, true);
            cam.fieldOfView = 60f;
            cam.fieldOfView = 50.5f;   // Street View's 80 deg across a 16:9 frame
            foreach (var (marker, file) in new[] { ("MallShotSVLowerA", "39_mall_sv_lower_a"), ("MallShotSVLowerB", "40_mall_sv_lower_b"), ("MallShotSVUpperMid", "41_mall_sv_upper_mid"), ("MallShotSVUpperWall", "42_mall_sv_upper_wall") })
            {
                var m = GameObject.Find(marker);
                if (!m) { Debug.LogWarning("[Nainital] no " + marker); continue; }
                Shot(file, m.transform.position, m.transform.position + m.transform.forward * 30f, 11f);
            }
            cam.fieldOfView = 60f;
            var lane = GameObject.Find("MallShotLane");
            if (lane)   // Lower Mall seen from across the water, as the terrain LOD sees it
            {
                Vector3 at = lane.transform.position;
                Shot("22_lower_mall_far", at + lane.transform.right * 140f + Vector3.up * 35f + lane.transform.forward * -60f, at, 10.5f);
                Shot("23_mall_night_lake", at + lane.transform.right * 60f + Vector3.up * 6f, at + lane.transform.forward * 80f, 21.5f);
            }
            void FrontOf(string file, string holder, float dist, float time)
            {
                var hgo = GameObject.Find(holder);
                if (!hgo || hgo.transform.childCount == 0) { Debug.LogWarning("[Nainital] shot " + file + ": no " + holder); return; }
                var tt = hgo.transform.GetChild(0);
                Vector3 p = tt.position - tt.forward * dist;
                if (Physics.Raycast(new Vector3(p.x, p.y + 60f, p.z), Vector3.down, out var hh, 200f)) p.y = hh.point.y + 1.7f;
                Shot(file, p, tt.position + Vector3.down * 1.5f, time);
            }
            FrontOf("24_naina_devi_arch", "Arch SHRI NAINA DEVI MANDIR", 14f, 10.5f);
            FrontOf("25_zoo_gate", "Arch PT. G.B. PANT HIGH ALTITUDE ZOO", 16f, 10.5f);
            FrontOf("26_tallital_board", "Road board", 16f, 10.5f);

            if (!string.IsNullOrWhiteSpace(Custom))
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var keep = Only;
                Only = null;
                foreach (var spec in Custom.Split('|'))
                {
                    var f = spec.Trim().Split(';');
                    if (f.Length < 4) { Debug.LogWarning("[Nainital] custom shot needs name;pos;look;hour: " + spec); continue; }
                    Vector3 V3(string t) { var c = t.Split(','); return new Vector3(float.Parse(c[0], inv), float.Parse(c[1], inv), float.Parse(c[2], inv)); }
                    Vector3 pos = V3(f[1]), look = V3(f[2]);
                    if (f.Length > 4 && f[4].Trim() == "g")
                    {
                        if (Physics.Raycast(new Vector3(pos.x, 4000f, pos.z), Vector3.down, out var ph, 6000f)) pos.y += ph.point.y;
                        if (Physics.Raycast(new Vector3(look.x, 4000f, look.z), Vector3.down, out var lh, 6000f)) look.y += lh.point.y;
                    }
                    Shot(f[0].Trim(), pos, look, float.Parse(f[3], inv));
                }
                Only = keep;
            }

            if (dn) { dn.timeOfDay = 17.4f; dn.Apply(); }
            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            rt.Release();
            Debug.Log("[Nainital] screenshots -> " + dir);
        }

        /// <summary>Every CharacterLibrary person in a row on a temporary floor, facing -fwd, posed at rest.</summary>
        static System.Collections.Generic.List<HumanoidRig> BuildPeopleLineup(Vector3 c, Vector3 fwd, Vector3 right,
                                                                             out GameObject root, out Material floorMat)
        {
            root = null;
            floorMat = null;
            var lib = CharacterLibrary.Instance;
            if (!lib || lib.entries == null || lib.entries.Length == 0) return null;
            root = new GameObject("PreviewPeople");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.SetParent(root.transform, false);
            floor.transform.position = c;
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.52f, 0.52f, 0.5f) };
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            var list = new System.Collections.Generic.List<HumanoidRig>();
            int n = lib.entries.Length;
            for (int i = 0; i < n; i++)
            {
                var e = lib.entries[i];
                if (e == null || !e.prefab) continue;
                var go = new GameObject(e.id);
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(c + right * ((i - (n - 1) * 0.5f) * 0.85f), Quaternion.LookRotation(-fwd));
                var rigGo = new GameObject("Rig");
                rigGo.transform.SetParent(go.transform, false);
                rigGo.SetActive(false);
                var rig = rigGo.AddComponent<HumanoidRig>();
                CharacterLibrary.Apply(e, rig, null);
                rigGo.SetActive(true);
                for (int k = 0; k < 25; k++) rig.Step(0.05f);   // settle into the body type's standing posture
                // batch mode never advances a frame: make each Render() re-skin with the current pose
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.forceMatrixRecalculationPerRender = true;
                list.Add(rig);
            }
            return list;
        }
    }
}
