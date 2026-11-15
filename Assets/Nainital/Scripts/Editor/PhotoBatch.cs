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
