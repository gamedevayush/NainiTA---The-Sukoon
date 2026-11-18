using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Film a character in play mode from the side: 12 frames, 0.06 s apart, in one sheet (Shots/film_&lt;who&gt;.png), to
    /// see how it really moves in the game (the rig's procedural layer included). Bridge: call AnimFilm.Player (the player
    /// running forward; arg "walk" walks, "raw" turns HumanoidRig off on it first so only the Animator moves the bones)
    /// or call AnimFilm.Npc (the nearest walking townsperson).
    /// </summary>
    public static class AnimFilm
    {
        static string Arg() { try { return File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim().ToLowerInvariant(); } catch { return ""; } }

        public static void Player()
        {
            if (!EditorApplication.isPlaying) throw new System.Exception("not playing");
            var p = PlayerController.Instance;
            string a = Arg();
            GameInput.TouchMove = new Vector2(0f, a.Contains("walk") ? 0.3f : 1f);
            Film(p.transform, p.Rig, "player" + (a.Contains("walk") ? "_walk" : "_run") + (a.Contains("raw") ? "_raw" : ""), a.Contains("raw"), 1.2f, () => GameInput.TouchMove = Vector2.zero);
        }

        public static void Npc()
        {
            if (!EditorApplication.isPlaying) throw new System.Exception("not playing");
            var p = PlayerController.Instance;
            NpcWalker best = null;
            float bd = float.MaxValue;
            foreach (var w in Object.FindObjectsByType<NpcWalker>(FindObjectsSortMode.None))
            {
                if (w.InCar || w.idle || w.sit) continue;
                var m = w.GetComponent<CharacterMotor>();
                if (!m || m.HorizontalVelocity.magnitude < 0.6f) continue;
                float d = (w.transform.position - p.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = w; }
            }
            if (!best) throw new System.Exception("no walking townsperson");
            Film(best.transform, best.GetComponentInChildren<HumanoidRig>(), "npc" + (Arg().Contains("raw") ? "_raw" : ""), Arg().Contains("raw"), 0.1f, null);
        }

        static void Film(Transform who, HumanoidRig rig, string name, bool raw, float delay, System.Action done)
        {
            if (raw && rig) rig.enabled = false;
            const int N = 18, W = 300, H = 380;
            var frames = new List<Texture2D>();
            var camGo = new GameObject("FilmCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 60f;
            var rt = new RenderTexture(W, H, 24) { antiAliasing = 2 };
            cam.targetTexture = rt;
            cam.enabled = false;
            double next = EditorApplication.timeSinceStartup + delay;
            string log = "";
            void Tick()
            {
                if (!EditorApplication.isPlaying || !who) { Finish(); return; }
                if (EditorApplication.timeSinceStartup < next) return;
                next += 0.04;
                Vector3 c = who.position + Vector3.up * 0.95f;
                camGo.transform.SetPositionAndRotation(c + who.right * 4.2f, Quaternion.LookRotation(-who.right));
                cam.Render();
                RenderTexture.active = rt;
                var t = new Texture2D(W, H, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                t.Apply();
                RenderTexture.active = null;
                frames.Add(t);
                var an = rig ? rig.Anim : null;
                if (frames.Count == 1 && an)
                {
                    var st = an.GetCurrentAnimatorStateInfo(0);
                    log = $"state Move {st.IsName("Move")} MoveF {st.IsName("MoveF")} Pistol {st.IsName("Pistol")} Air {st.IsName("Air")} speedParam {an.GetFloat("Speed"):F2} animSpeed {an.speed:F2} full {rig.Full} layer1 {an.GetLayerWeight(1):F2} grounded {an.GetBool("Grounded")} ctrl {an.runtimeAnimatorController.name}";
                }
                if (frames.Count >= N) Finish();
            }
            void Finish()
            {
                EditorApplication.update -= Tick;
                if (frames.Count > 0)
                {
                    var sheet = new Texture2D(W * 6, H * 3, TextureFormat.RGB24, false);
                    for (int i = 0; i < frames.Count; i++)
                        sheet.SetPixels((i % 6) * W, (2 - i / 6) * H, W, H, frames[i].GetPixels());
                    sheet.Apply();
                    File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, $"../Shots/film_{name}.png")), sheet.EncodeToPNG());
                }
                foreach (var f in frames) Object.Destroy(f);
                cam.targetTexture = null;
                Object.Destroy(rt);
                Object.Destroy(camGo);
                if (raw && rig) rig.enabled = true;
                done?.Invoke();
                Debug.Log($"[Nainital] film {name}: {frames.Count} frames; {log}");
            }
            EditorApplication.update += Tick;
        }
    }
}
