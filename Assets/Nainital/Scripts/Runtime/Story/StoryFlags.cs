using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// What the story remembers: choices made in dialogue, missions passed, who he has met (owner, 23 Nov 2026: "cutscene,
    /// dialogue and choice UI system bnaalo proper"). Text flags, kept in one PlayerPrefs key ("nt_story", a small JSON) so a
    /// save keeps them; PlayTest snapshots it with the rest of the save (Editor/TestSave).
    /// </summary>
    public static class StoryFlags
    {
        public const string Key = "nt_story";

        [System.Serializable] class Row { public string k, v; }
        [System.Serializable] class Table { public List<Row> rows = new List<Row>(); }

        static Dictionary<string, string> map;

        static Dictionary<string, string> Map
        {
            get
            {
                if (map != null) return map;
                map = new Dictionary<string, string>();
                try
                {
                    var t = JsonUtility.FromJson<Table>(PlayerPrefs.GetString(Key, "{}"));
                    if (t != null && t.rows != null) foreach (var r in t.rows) if (!string.IsNullOrEmpty(r.k)) map[r.k] = r.v ?? "";
                }
                catch { }
                return map;
            }
        }

        public static string Get(string k, string fallback = "") => !string.IsNullOrEmpty(k) && Map.TryGetValue(k, out var v) ? v : fallback;
        public static bool Has(string k) => !string.IsNullOrEmpty(k) && Map.ContainsKey(k);
        public static int GetInt(string k, int fallback = 0) => int.TryParse(Get(k), out int i) ? i : fallback;

        public static void Set(string k, string v)
        {
            if (string.IsNullOrEmpty(k)) return;
            Map[k] = v ?? "";
            Save();
        }

        public static void Clear(string k) { if (Map.Remove(k ?? "")) Save(); }

        /// <summary>Everything forgotten (a new game).</summary>
        public static void ResetAll() { map = new Dictionary<string, string>(); PlayerPrefs.DeleteKey(Key); }

        /// <summary>Read again from PlayerPrefs (after the test save is put back).</summary>
        public static void Reload() => map = null;

        static void Save()
        {
            var t = new Table();
            foreach (var kv in Map) t.rows.Add(new Row { k = kv.Key, v = kv.Value });
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(t));
        }
    }
}
