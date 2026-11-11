using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>A fast line into a running editor (user, 2026-11-11: "unity se fast connection, cheezein bahut fast ho").
    /// Starting Unity for every change cost ten minutes or more, most of it start-up and rebuilding characters and
    /// prefabs that had not changed; the world itself builds in about ninety seconds. With this the editor stays up
    /// (opened by hand, or headless with -batchmode and no -quit) and takes orders from a file:
    ///
    ///   &lt;project&gt;/Bridge/cmd.txt      one command per line: "&lt;id&gt; &lt;command&gt; [argument]"
    ///   &lt;project&gt;/Bridge/result_&lt;id&gt;.txt   "ok" or "error", the time taken, and the [Nainital] log lines and errors
    ///   &lt;project&gt;/Bridge/status.txt   a heartbeat: idle / busy / compiling, and whether the scripts compiled
    ///
    /// Commands: refresh (import changed files and recompile), world (rebuild the world), shots a,b (preview shots
    /// whose names start with a or b; no argument = all), shot &lt;specs&gt; (custom shots, see PreviewCapture.ParseCustom),
    /// call Type.Method (a public static no-argument method in this namespace), capture name (play mode: the Game view
    /// with its UI to Shots/play_name.png), quit.</summary>
    [InitializeOnLoad]
    public static class EditorBridge
    {
        static readonly string Dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge"));
        static double next;
        static string lastBeat = "";

        static EditorBridge()
        {
            try { Directory.CreateDirectory(Dir); } catch { }
            EditorApplication.update += Tick;
            Beat("loaded");
        }

        static void Beat(string state)
        {
            string s = $"{state} compiling={EditorApplication.isCompiling} compileFailed={EditorUtility.scriptCompilationFailed}";
            if (s == lastBeat && state != "loaded") return;
            lastBeat = s;
            try { File.WriteAllText(Path.Combine(Dir, "status.txt"), DateTime.Now.ToString("HH:mm:ss") + " " + s); } catch { }
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 0.3;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Beat("busy"); return; }
            string cmdPath = Path.Combine(Dir, "cmd.txt");
            if (!File.Exists(cmdPath)) { Beat("idle"); return; }
            string[] lines;
            try { lines = File.ReadAllLines(cmdPath); File.Delete(cmdPath); }
            catch { return; }   // still being written: next tick
            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (line.Length > 0) Run(line);
            }
        }

        static void Run(string line)
        {
            string[] parts = line.Split(new[] { ' ' }, 3);
            string id = parts[0], cmd = parts.Length > 1 ? parts[1] : "", arg = parts.Length > 2 ? parts[2] : "";
            var log = new List<string>();
            void OnLog(string msg, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) log.Add("ERROR " + msg + "\n" + stack);
                else if (msg.StartsWith("[Nainital]") || msg.StartsWith("[Probe]")) log.Add(msg);
            }
            Application.logMessageReceived += OnLog;
            var t0 = DateTime.Now;
            string status = "ok";
            Beat("running " + cmd);
            try
            {
                switch (cmd)
                {
                    case "refresh": AssetDatabase.Refresh(); break;
                    case "world": WorldBuilder.Build(); break;
                    case "shots": Shots(arg, null); break;
                    case "shot": Shots("-", arg); break;
                    case "worldshots": WorldBuilder.Build(); Shots(arg, null); break;
                    case "call": Call(arg); break;
                    case "capture":   // the Game view as the player sees it, UI included (written at the end of the next frame)
                        if (!EditorApplication.isPlaying) throw new InvalidOperationException("capture needs play mode");
                        string shot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots/play_" + (arg.Length > 0 ? arg : "shot") + ".png"));
                        if (File.Exists(shot)) File.Delete(shot);
                        ScreenCapture.CaptureScreenshot(shot);
                        log.Add("[Nainital] capture -> " + shot);
                        break;
                    case "quit": Finish(id, "ok", t0, log); Application.logMessageReceived -= OnLog; EditorApplication.Exit(0); return;
                    default: status = "error unknown command '" + cmd + "'"; break;
                }
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                status = "error " + inner.GetType().Name + ": " + inner.Message + "\n" + inner.StackTrace;
            }
            Application.logMessageReceived -= OnLog;
            Finish(id, status, t0, log);
        }

        static void Finish(string id, string status, DateTime t0, List<string> log)
        {
            string body = status + "\nseconds " + (DateTime.Now - t0).TotalSeconds.ToString("F1") + "\n" + string.Join("\n", log);
            try
            {
                string tmp = Path.Combine(Dir, "result_" + id + ".tmp"), dst = Path.Combine(Dir, "result_" + id + ".txt");
                File.WriteAllText(tmp, body);
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(tmp, dst);   // appears whole, never half written
            }
            catch { }
            Beat("idle");
        }

        static void Shots(string names, string custom)
        {
            PreviewCapture.Only = string.IsNullOrWhiteSpace(names) ? null : names.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            PreviewCapture.Custom = custom;
            try { PreviewCapture.Capture(); }
            finally { PreviewCapture.Only = null; PreviewCapture.Custom = null; }
        }

        static void Call(string name)
        {
            int dot = name.LastIndexOf('.');
            if (dot < 0) throw new ArgumentException("call needs Type.Method");
            string typeName = name.Substring(0, dot), method = name.Substring(dot + 1);
            var type = typeof(EditorBridge).Assembly.GetType("Nainital.EditorTools." + typeName);   // this namespace only
            if (type == null) throw new ArgumentException("no type Nainital.EditorTools." + typeName);
            var mi = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (mi == null) throw new ArgumentException("no public static " + typeName + "." + method + "()");
            mi.Invoke(null, null);
        }
    }
}
