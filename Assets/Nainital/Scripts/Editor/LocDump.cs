using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Every text the built scene holds and the game shows (info boards, people's names and lines, food stalls, place
    /// names, map blips, goons), exactly as stored, to Bridge/loc_scene.tsv (kind TAB text; new lines as \n) - so each has
    /// a line in Tools/loc/strings.tsv (the language table, Loc). Bridge: call LocDump.Run (scene open, Play or not).
    /// </summary>
    public static class LocDump
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            int n = 0;
            void Add(string kind, string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return;
                sb.Append(kind).Append('\t').Append(s.Replace("\r", "").Replace("\n", "\\n")).Append('\n');
                n++;
            }
            const FindObjectsInactive inc = FindObjectsInactive.Include;
            foreach (var s in Object.FindObjectsByType<InfoSign>(inc, FindObjectsSortMode.None)) { Add("title", s.title); Add("body", s.body); }
            foreach (var w in Object.FindObjectsByType<NpcWalker>(inc, FindObjectsSortMode.None))
            {
                Add("npcName", w.npcName);
                if (w.lines != null) foreach (var l in w.lines) Add("line", l);
            }
            foreach (var f in Object.FindObjectsByType<FoodStall>(inc, FindObjectsSortMode.None))
            {
                Add("stall", f.stallName);
                if (f.items != null) foreach (var it in f.items) Add("food", it);
            }
            foreach (var t in Object.FindObjectsByType<LocationTracker>(inc, FindObjectsSortMode.None))
                if (t.names != null) foreach (var s in t.names) Add("place", s);
            foreach (var g in Object.FindObjectsByType<GameManager>(inc, FindObjectsSortMode.None))
                if (g.blipNames != null) foreach (var s in g.blipNames) Add("blip", s);
            foreach (var e in Object.FindObjectsByType<EnemyAI>(inc, FindObjectsSortMode.None)) Add("enemy", e.displayName);
            var mm = Object.FindAnyObjectByType<MissionManager>(inc);
            if (mm && mm.missions != null)
                foreach (var m in mm.missions)
                {
                    Add("mission", m.title);
                    Add("mission", m.intro);
                }
            string p = Path.GetFullPath(Path.Combine(Application.dataPath, "../Bridge/loc_scene.tsv"));
            File.WriteAllText(p, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[Nainital] LocDump: {n} texts -> {p}");
        }
    }
}
