using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// One batch-mode run that does a short list of jobs and quits, so the editor need not stay open (user, 2026-11-15:
    /// "unity is using too much RAM and CPU"). Steps come from Bridge/batch.txt, one per line:
    ///   world                  rebuild the world
    ///   call Type.Method       a public static no-argument method in Nainital.EditorTools
    ///   shots 03,38            preview shots whose names start with these (no argument: all)
    /// Unity -batchmode -quit -projectPath ... -executeMethod Nainital.EditorTools.PhotoBatch.Run -logFile Logs/photobatch.log
    /// </summary>
    public static class PhotoBatch
    {
        public static void Run()
        {
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge/batch.txt"));
            try
            {
                foreach (var raw in File.ReadAllLines(file))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int sp = line.IndexOf(' ');
                    string cmd = sp < 0 ? line : line.Substring(0, sp), arg = sp < 0 ? "" : line.Substring(sp + 1).Trim();
                    var t0 = DateTime.Now;
                    Debug.Log("[Batch] " + line);
                    switch (cmd)
                    {
                        case "world": ProjectSetup.Run(); WorldBuilder.Build(); break;
                        case "call":
                        {
                            int dot = arg.LastIndexOf('.');
                            var type = typeof(PhotoBatch).Assembly.GetType("Nainital.EditorTools." + arg.Substring(0, dot));
                            var mi = type?.GetMethod(arg.Substring(dot + 1), BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                            if (mi == null) throw new ArgumentException("no public static " + arg + "()");
                            mi.Invoke(null, null);
                            break;
                        }
                        case "shots":
                            PreviewCapture.Only = string.IsNullOrWhiteSpace(arg) ? null : arg.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            try { PreviewCapture.Capture(); } finally { PreviewCapture.Only = null; }
                            break;
                        case "shot":   // custom cameras: "name;x,y,z;lookX,lookY,lookZ;hour" joined by "|"
                            PreviewCapture.Only = new[] { "-" };
                            PreviewCapture.Custom = arg;
                            try { PreviewCapture.Capture(); } finally { PreviewCapture.Only = null; PreviewCapture.Custom = null; }
                            break;
                        case "shotobj":   // cameras round a built object: "name;Object Name;fx,fy,fz;lx,ly,lz;hour" joined by "|",
                        {                 // from / look in the object's own frame (x right, y up, z its front)
                            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(WorldBuilder.ScenePath);
                            var specs = new System.Collections.Generic.List<string>();
                            var inv = System.Globalization.CultureInfo.InvariantCulture;
                            foreach (var spec in arg.Split('|'))
                            {
                                var f = spec.Split(';');
                                if (f.Length < 5) continue;
                                var go = GameObject.Find(f[1]);
                                if (!go) { Debug.LogWarning("[Batch] shotobj: no object " + f[1]); continue; }
                                Vector3 P(string v) { var n = v.Split(','); return new Vector3(float.Parse(n[0], inv), float.Parse(n[1], inv), float.Parse(n[2], inv)); }
                                Vector3 from = go.transform.TransformPoint(P(f[2])), look = go.transform.TransformPoint(P(f[3]));
                                Debug.Log($"[Batch] shotobj {f[0]}: {f[1]} at {go.transform.position}");
                                specs.Add(string.Format(inv, "{0};{1},{2},{3};{4},{5},{6};{7}", f[0], from.x, from.y, from.z, look.x, look.y, look.z, f[4]));
                            }
                            PreviewCapture.Only = new[] { "-" };
                            PreviewCapture.Custom = string.Join("|", specs);
                            try { PreviewCapture.Capture(); } finally { PreviewCapture.Only = null; PreviewCapture.Custom = null; }
                            break;
                        }
                        default: throw new ArgumentException("unknown step: " + line);
                    }
                    Debug.Log($"[Batch] done {cmd} in {(DateTime.Now - t0).TotalSeconds:F0}s");
                }
                Debug.Log("[Batch] ALL OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.Log("[Batch] FAILED");
                EditorApplication.Exit(1);
            }
        }
    }
}
