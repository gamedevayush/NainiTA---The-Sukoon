using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>Money (Rs) and mission progress, saved with PlayerPrefs.</summary>
    public static class GameState
    {
        public static int Money { get; private set; } = 500;
        static HashSet<string> done = new HashSet<string>();
        static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            Money = PlayerPrefs.GetInt("nt_money", 500);
            done = new HashSet<string>();
            foreach (var s in PlayerPrefs.GetString("nt_done", "").Split('|'))
                if (!string.IsNullOrEmpty(s)) done.Add(s);
        }

        static void Save()
        {
            PlayerPrefs.SetInt("nt_money", Money);
            PlayerPrefs.SetString("nt_done", string.Join("|", done));
            PlayerPrefs.Save();
        }

        public static void AddMoney(int amount)
        {
            Load();
            Money = Mathf.Max(0, Money + amount);
            Save();
        }

        public static bool TrySpend(int amount)
        {
            Load();
            if (Money < amount) return false;
            Money -= amount;
            Save();
            return true;
        }

        public static bool IsDone(string id) { Load(); return done.Contains(id); }
        public static int DoneCount { get { Load(); return done.Count; } }

        public static void MarkDone(string id)
        {
            Load();
            done.Add(id);
            Save();
        }

        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey("nt_money");
            PlayerPrefs.DeleteKey("nt_done");
            loaded = false;
            Load();
        }
    }
}
