using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Play checks for the street animals (22 Nov 2026) through the bridge, after "call PlayTest.Begin":
    /// call AnimalTest.Spawn (a cow 9 m, a dog 6 m and a cat 4 m in front of the player), AnimalTest.Touch (the nearest
    /// animal pushed against the player: it must run off), AnimalTest.Knock (an impulse as a car's knock gives it: it
    /// must run off), AnimalTest.Report (counts, every animal near: kind, place, fleeing, clip playing and its speed,
    /// clips found; touches / cat hops / cars that waited on a cow so far).
    /// </summary>
    public static class AnimalTest
    {
        static void Need() { if (!EditorApplication.isPlaying) throw new System.Exception("not playing"); }

        public static void Spawn()
        {
            Need();
            var p = PlayerController.Instance;
            var m = AnimalManager.Ensure();
            Vector3 f = p.transform.forward;
            f.y = 0f;
            f.Normalize();
            Vector3 at = p.transform.position;
            var cow = m.SpawnAt(AnimalKind.Cow, at + f * 9f);
            var dog = m.SpawnAt(AnimalKind.Dog, at + f * 6f + Vector3.Cross(Vector3.up, f) * 1.5f);
            var cat = m.SpawnAt(AnimalKind.Cat, at + f * 4f - Vector3.Cross(Vector3.up, f) * 1.2f);
            Debug.Log($"[Nainital] animals spawned: cow {(cow ? "ok" : "no model")}, dog {(dog ? "ok" : "no model")}, cat {(cat ? "ok" : "no model")}");
        }

        static Animal Nearest()
        {
            var p = PlayerController.Instance;
            Animal best = null;
            float bd = float.MaxValue;
            foreach (var a in Animal.Live)
            {
                float d = (a.transform.position - p.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = a; }
            }
            return best;
        }

        public static void Touch()
        {
            Need();
            var a = Nearest();
            if (!a) { Debug.Log("[Nainital] animals: none to touch"); return; }
            var p = PlayerController.Instance;
            a.GetComponent<CharacterMotor>().Teleport(p.transform.position + p.transform.forward * (a.spec.radius + 0.35f));
            Debug.Log($"[Nainital] animals: {a.kind} put against the player at {a.transform.position:F1}");
        }

        public static void Knock()
        {
            Need();
            var a = Nearest();
            if (!a) { Debug.Log("[Nainital] animals: none to knock"); return; }
            a.GetComponent<CharacterMotor>().impulse += a.transform.right * 4f + Vector3.up * 2f;
            Debug.Log($"[Nainital] animals: {a.kind} knocked at {a.transform.position:F1}");
        }

        public static void Report()
        {
            Need();
            var m = AnimalManager.Instance;
            Debug.Log("[Nainital] " + (m ? m.Report() : "no AnimalManager"));
        }
    }
}
