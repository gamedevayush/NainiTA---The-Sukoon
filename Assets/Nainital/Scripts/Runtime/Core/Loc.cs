using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The game's language (owner, 22 Nov 2026: "default will be english ... can be changable to hindi .. with hindi font ..
    /// and everywhere"). English by default; Hindi in Devanagari. The code still writes its text in Hinglish (the key);
    /// every place that shows text passes it through <see cref="T"/> (or <see cref="L"/> for a Hinglish / English pair).
    ///
    /// The table is Tools/loc/strings.tsv (key, English, Hindi), built by Tools/loc/build_loc.py into
    /// Resources/Loc/strings.json. uGUI cannot shape Devanagari, so the Hindi there is already shaped: Private Use Area
    /// characters that the game's Rajdhani fonts (Resources/Fonts, extended by the same tool) draw as the right glyphs.
    /// Numbers in a key are '#' (the shown text's numbers go back in, in order); patterns below cover text built from parts.
    /// A string with no entry: English shows it as it is, Hindi shows the English; in the editor each one is listed in
    /// Bridge/loc_missing.txt so it can be added.
    /// </summary>
    public static class Loc
    {
        public enum Lang { English = 0, Hindi = 1 }

        static int lang = -1;   // lazy: PlayerPrefs must not be read from a static initialiser
        public static Lang Current
        {
            get
            {
                if (lang < 0) lang = Mathf.Clamp(PlayerPrefs.GetInt("nt_lang", 0), 0, 1);   // (the old "nt_lang_en" Hinglish choice is not read: English by default)
                return (Lang)lang;
            }
        }
        public static bool Hindi => Current == Lang.Hindi;
        public static bool English => !Hindi;
        public static event System.Action Changed;

        public static void Set(Lang l)
        {
            lang = (int)l;
            PlayerPrefs.SetInt("nt_lang", lang);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
        public static void SetEnglish(bool en) => Set(en ? Lang.English : Lang.Hindi);

        // ------------------------------------------------------------------ the table
        [System.Serializable] class Table { public string[] keys, en, hi; }
        static Dictionary<string, (string en, string hi)> table;

        static void Load()
        {
            if (table != null) return;
            table = new Dictionary<string, (string, string)>();
            var ta = Resources.Load<TextAsset>("Loc/strings");
            if (!ta) { Debug.LogWarning("[Loc] Resources/Loc/strings.json missing"); return; }
            var t = JsonUtility.FromJson<Table>(ta.text);
            if (t?.keys == null) return;
            for (int i = 0; i < t.keys.Length; i++)
                table[t.keys[i]] = (i < t.en.Length ? t.en[i] : t.keys[i], i < t.hi.Length ? t.hi[i] : "");
        }

        static bool Lookup(string key, out string s)
        {
            Load();
            if (table.TryGetValue(key, out var e))
            {
                s = Hindi && !string.IsNullOrEmpty(e.hi) ? e.hi : e.en;
                if (Hindi && string.IsNullOrEmpty(e.hi)) Missing(key, "no hindi");
                return true;
            }
            s = null;
            return false;
        }

        static readonly Regex Num = new Regex(@"\d+(?:[.,]\d+)*");

        // text built from parts: the pattern's key (with {0}, {1}) is in the table like any other
        static readonly (Regex re, string key)[] P =
        {
            (new Regex(@"^Padho: (.+)$"), "Padho: {0}"),
            (new Regex(@"^(.+) se baat karo$"), "{0} se baat karo"),
            (new Regex(@"^(.+) se khao \(Rs (\d+)\)$"), "{0} se khao (Rs {1})"),
            (new Regex(@"^(.+) khaya! \+(\d+) health  \(-Rs (\d+)\)$"), "{0} khaya! +{1} health  (-Rs {2})"),
            (new Regex(@"^Mission: (.+)  \(Rs (\d+), dobara\)$"), "Mission: {0}  (Rs {1}, dobara)"),
            (new Regex(@"^Mission: (.+)  \(Rs (\d+)\)$"), "Mission: {0}  (Rs {1})"),
            (new Regex(@"^Shabash! Agla kaam: (.+)$"), "Shabash! Agla kaam: {0}"),
            (new Regex(@"^Photo kheench li: (.+)$"), "Photo kheench li: {0}"),
            (new Regex(@"^(.+) dher ho gaya!$"), "{0} dher ho gaya!"),
            (new Regex(@"^(.+)   \+Rs (\d+)$"), "{0}   +Rs {1}"),
            (new Regex("^(.+): \"(.+)\"$"), "{0}: \"{1}\""),
            (new Regex(@"^(.+)  \((\*+)\)$"), "{0}  ({1})"),   // a wanted toast with its stars (WantedSystem)
            (new Regex(@"^Pahunch gaye: (.+)$"), "Pahunch gaye: {0}"),
            (new Regex(@"^(.+) - dukaan dekho$"), "{0} - dukaan dekho"),   // a shop's prompt (Shop.cs)
            (new Regex(@"^Cheat: (.+) aa gayi - paas jaake (.+) dabao$"), "Cheat: {0} aa gayi - paas jaake {1} dabao"),
            (new Regex(@"^Galat code: (.+)$"), "Galat code: {0}"),
        };

        /// <summary>The text in the current language.</summary>
        public static string T(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (Lookup(s, out var r)) return r;
            // numbers: "Rs 500 jurmana" is the key "Rs # jurmana"
            if (Num.IsMatch(s))
            {
                var nums = new List<string>();
                string key = Num.Replace(s, m => { nums.Add(m.Value); return "#"; });
                if (Lookup(key, out r))
                {
                    int k = 0;
                    return Regex.Replace(r, "#", m => k < nums.Count ? nums[k++] : "#");
                }
            }
            foreach (var (re, key) in P)
            {
                var m = re.Match(s);
                if (!m.Success || !Lookup(key, out var fmt)) continue;
                var args = new object[m.Groups.Count - 1];
                for (int i = 1; i < m.Groups.Count; i++) args[i - 1] = T(m.Groups[i].Value);
                try { return string.Format(fmt, args); } catch { return s; }
            }
            if (HasWords(s)) Missing(s, "no entry");
            return s;
        }

        /// <summary>A Hinglish / English pair written in the code: English as given, Hindi from the table (key = the Hinglish).</summary>
        public static string L(string hinglish, string english)
        {
            string r;
            if (!Hindi) return Lookup(english, out r) ? r : english;   // (an English text can itself be a key: "HINDI_NAME" shows its own script)
            if (Lookup(hinglish, out r) && r != null && r != hinglish) return r;
            if (Lookup(english, out r)) return r;
            Missing(hinglish, "pair: " + english);
            return english;
        }

        /// <summary>T(key) with {0}, {1} filled (each argument shown as it is - translate it first if it is text).</summary>
        public static string F(string key, params object[] args)
        {
            string fmt = T(key);
            try { return string.Format(fmt, args); } catch { return fmt; }
        }

        static bool HasWords(string s)
        {
            int letters = 0;
            foreach (char c in s) if (char.IsLetter(c) && c < 0x2000) { if (++letters >= 2) return true; }
            return false;
        }

        // ------------------------------------------------------------------ gaps, for the editor
        static readonly HashSet<string> missing = new HashSet<string>();
        static void Missing(string key, string why)
        {
            if (!Application.isEditor || !missing.Add(key)) return;
            try
            {
                string p = System.IO.Path.Combine(Application.dataPath, "../Bridge/loc_missing.txt");
                System.IO.File.AppendAllText(p, key.Replace("\n", "\\n") + "\t" + why + "\n");
            }
            catch { }
        }
    }
}
