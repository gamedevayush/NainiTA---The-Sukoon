using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Renders the open scene from a given camera into Shots/ref_*.png without reopening it, to set beside frames of real
    /// videos (user, 2026-11-14: compare position, rotation and scale with 10 YouTube videos). Called through Unity MCP.
    /// ground = true: the heights are metres above the ground under each point.
    /// </summary>
    public static class RefShots
    {
        public static string Shot(string name, Vector3 pos, Vector3 look, float hour = 11f, float fov = 60f, bool ground = false)
        {
            if (ground)
            {
                // the top surface under each point (a road, a deck, a roof), never below the lake's water
                var lake = Object.FindAnyObjectByType<LakeArea>();
                float Top(Vector3 p)
                {
                    Physics.SyncTransforms();
                    float y = Physics.Raycast(new Vector3(p.x, 3000f, p.z), Vector3.down, out var hit, 5000f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
                    return lake ? Mathf.Max(y, lake.waterY) : y;
                }
                pos.y += Top(pos);
                look.y += Top(look);
            }
            var dn = Object.FindAnyObjectByType<DayNightCycle>();
            float oldHour = dn ? dn.timeOfDay : 0f;
            if (dn) { dn.timeOfDay = hour; dn.Apply(); }
            var go = new GameObject("RefShotCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 7200f;
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            cam.targetTexture = rt;
            go.transform.position = pos;
            go.transform.LookAt(look);
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string dir = Path.GetFullPath("Shots");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "ref_" + name + ".png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            if (dn) { dn.timeOfDay = oldHour; dn.Apply(); }
            return file;
        }

        /// <summary>Batch: opens the scene and renders the comparison set (lake, forest, Snow View, temple, Gurudwara).</summary>
        public static void Batch()
        {
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
                Shot("lake_day", new Vector3(60f, 860f, 120f), new Vector3(520f, 746f, -900f), 11f);
                Shot("lake_night", new Vector3(60f, 800f, 120f), new Vector3(520f, 746f, -900f), 22f);
                Shot("mallital_town", new Vector3(150f, 820f, -250f), new Vector3(-120f, 830f, 150f), 11f);
                Shot("from_ropeway", new Vector3(290f, 960f, 390f), new Vector3(470f, 760f, -650f), 11f, 70f);
                Shot("snowview_aerial", new Vector3(660f, 1130f, 440f), new Vector3(625f, 1070f, 535f), 11f, 65f);
                Shot("snowview_golmarket", new Vector3(655f, 1.7f, 505f), new Vector3(645f, 1.5f, 535f), 11f, 70f, true);
                Shot("snowview_dino", new Vector3(590f, 1.7f, 585f), new Vector3(585f, 1.5f, 550f), 11f, 70f, true);
                Shot("snowview_games", new Vector3(640f, 4f, 560f), new Vector3(615f, 1f, 548f), 11f, 70f, true);
                Shot("temple_lake", new Vector3(-40f, 752f, -110f), new Vector3(-62f, 756f, -152f), 17f, 60f);
                Shot("temple_aerial", new Vector3(-30f, 785f, -115f), new Vector3(-62f, 754f, -150f), 11f, 60f);
                Shot("gurudwara", new Vector3(-39f, 785f, -90f), new Vector3(-39f, 755f, -124f), 11f, 60f);
            }
            catch (System.Exception e) { Debug.LogException(e); UnityEditor.EditorApplication.Exit(1); }
        }

        /// <summary>The Flats' centre, the lake's north and south tips, and a few named places, for framing shots.</summary>
        public static string Places()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var n in new[] { "The Flats", "Naina Devi Mandir", "Jama Masjid", "Gurudwara Sri Guru Singh Sabha", "St. Francis Church",
                                      "Capitol", "Bandstand", "Tallital traffic post", "Mallital boat ghat boats", "Snow View", "Tiffin Top (Dorothy's Seat)" })
            {
                var g = GameObject.Find(n);
                var r = g ? g.GetComponentInChildren<Renderer>() : null;
                sb.AppendLine(n + ": " + (r ? r.bounds.center.ToString("F0") + " size " + r.bounds.size.ToString("F0") : g ? g.transform.position.ToString("F0") : "-"));
            }
            var lake = Object.FindAnyObjectByType<LakeArea>();
            if (lake)
            {
                Vector2 n = lake.outline[0], s = lake.outline[0];
                foreach (var p in lake.outline) { if (p.y > n.y) n = p; if (p.y < s.y) s = p; }
                sb.AppendLine("lake north tip " + n + " south tip " + s + " water " + lake.waterY);
            }
            return sb.ToString();
        }
    }
}
