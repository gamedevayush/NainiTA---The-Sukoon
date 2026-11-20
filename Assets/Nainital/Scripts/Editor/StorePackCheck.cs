using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The two free Asset Store packs the riders and the drivers' hands use are NOT in git (the repo is public; the
    /// Asset Store EULA forbids sharing them), so every PC imports them itself (owner, 21 Nov 2026: "ye baat wahan ka
    /// claude mujhe btayega na ki package download krna hai?"). On every editor load: if either is missing, a warning in
    /// the Console / the batch log, and once per editor session a dialog with the steps. Nainital > Check Asset Store
    /// packs runs it again.
    /// </summary>
    [InitializeOnLoad]
    static class StorePackCheck
    {
        const string Moto = "Assets/MotoInteractionAnimsFREE", Kevin = "Assets/Kevin Iglesias";

        static StorePackCheck() { EditorApplication.delayCall += () => Check(false); }

        [MenuItem("Nainital/Check Asset Store packs")]
        static void Menu() => Check(true);

        static void Check(bool always)
        {
            bool moto = AssetDatabase.IsValidFolder(Moto), kevin = AssetDatabase.IsValidFolder(Kevin);
            if (moto && kevin)
            {
                if (always) EditorUtility.DisplayDialog("Asset Store packs", "Both packs are in the project. If riders still sit in the old pose, run Nainital > 6b. Build character animator.", "OK");
                return;
            }
            string missing = (!moto ? "\n- Motorcycle Interaction Animset FREE (SpectraMotions)" : "") + (!kevin ? "\n- Human Basic Motions FREE (Kevin Iglesias)" : "");
            string steps = "Not in git (public repo, Asset Store licence), so import them on this PC:" + missing +
                           "\n\n1. Window > Package Manager > My Assets (signed in to the owner's Unity account)" +
                           "\n2. Download + Import each one (all files)" +
                           "\n3. Nainital > 6b. Build character animator" +
                           "\n\nWithout them bike / scooty riders use the old procedural pose and drivers' fingers do not grip. Never commit those folders. (docs/VEHICLES.md section 5)";
            Debug.LogWarning("[Nainital] ASSET STORE PACKS MISSING - " + steps.Replace("\n", " "));
            if (Application.isBatchMode || (!always && SessionState.GetBool("Nainital.StorePackCheck", false))) return;
            SessionState.SetBool("Nainital.StorePackCheck", true);
            EditorUtility.DisplayDialog("Naini Sukoon: Asset Store packs missing", steps, "OK");
        }
    }
}
