using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// GTA-style mission flow: yellow mission markers around Nainital, objectives with a light-beam target,
    /// timers, pass/fail banners and Rs rewards. The missions are the story's (owner, 26 Nov 2026: the 8 old demo
    /// missions are gone; the chain of docs/Nainital_Story_Mission_Bible.docx is built by StoryMissions): only the next
    /// one shows its marker, one that follows straight on starts by itself, and a failed one starts again from its last
    /// checkpoint step.
    /// </summary>
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        public readonly List<Mission> missions = new List<Mission>();
        public Mission Active { get; private set; }
        public MissionStep Step => Active != null && stepIndex < Active.steps.Count ? Active.steps[stepIndex] : null;
        public float TimeLeft { get; private set; }
        public bool Timed { get; private set; }
        public Vector3? CurrentTarget => Step?.Target;
        public readonly List<(Mission m, Transform marker)> Givers = new List<(Mission, Transform)>();

        int stepIndex;
        float buildDelay = 1f;
        bool built;
        Transform beam;
        Material giverMat, targetMat;

        // ------------------------------------------------------------------ markers
        static Mesh beamMesh;

        static Mesh BeamMesh()
        {
            if (beamMesh) return beamMesh;
            var mb = new MeshBuilder();
            mb.Cylinder(Vector3.zero, 1f, 1f, 1f, 20, Vector2.zero, false, false);
            mb.Cylinder(Vector3.zero, 1f, 1f, 1f, 20, Vector2.zero, false, false);
            // inner faces so the beam is visible from inside too
            var inner = new MeshBuilder();
            inner.Cylinder(Vector3.zero, 1f, 1f, 1f, 20, Vector2.zero, false, false);
            for (int i = 0; i < inner.N.Count; i++) inner.N[i] = -inner.N[i];
            for (int t = 0; t < inner.T.Count; t += 3) { int a = inner.T[t + 1]; inner.T[t + 1] = inner.T[t + 2]; inner.T[t + 2] = a; }
            mb.Append(inner);
            beamMesh = mb.ToMesh("Beam");
            return beamMesh;
        }

        Transform MakeMarker(Vector3 pos, float radius, float height, Material mat, string label)
        {
            var go = new GameObject("Marker " + label);
            go.transform.position = pos;
            var b = new GameObject("Beam");
            b.transform.SetParent(go.transform, false);
            b.transform.localScale = new Vector3(radius, height, radius);
            b.AddComponent<MeshFilter>().sharedMesh = BeamMesh();
            var mr = b.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.AddComponent<MarkerFade>();   // see-through when the camera is up close, so it never fills the screen
            if (!string.IsNullOrEmpty(label) && GameAssets.Font)
            {
                var t = new GameObject("Label");
                t.transform.SetParent(go.transform, false);
                t.transform.localPosition = Vector3.up * (height + 0.8f);
                var tm = t.AddComponent<TextMesh>();
                tm.font = HudStyle.Bold;   // Rajdhani: English and Hindi (Loc)
                tm.GetComponent<MeshRenderer>().sharedMaterial = HudStyle.Bold.material;
                tm.text = Loc.T(label);
                tm.fontSize = 64;
                tm.characterSize = 0.06f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = new Color(1f, 0.85f, 0.2f);
                t.AddComponent<FaceCamera>();
            }
            return go.transform;
        }

        // ------------------------------------------------------------------ catalog
        void Awake()
        {
            Instance = this;
            if (!GetComponent<StoryMissions>()) gameObject.AddComponent<StoryMissions>();
        }

        void BuildCatalog()
        {
            built = true;
            missions.Clear();
            StoryMissions.Build(missions);
            var baseMat = GameAssets.MarkerMaterial;
            if (baseMat)
            {
                giverMat = new Material(baseMat);
                giverMat.SetColor("_BaseColor", new Color(1f, 0.82f, 0.15f, 0.35f));
                targetMat = new Material(baseMat);
                targetMat.SetColor("_BaseColor", new Color(0.3f, 1f, 0.45f, 0.28f));
            }
            foreach (var m in missions)
            {
                if (m.giver == Vector3.zero) continue;
                var mk = MakeMarker(m.giver, 1.1f, 3.2f, giverMat, "M");
                var g = mk.gameObject.AddComponent<MissionGiver>();
                g.mission = m;
                Givers.Add((m, mk));
            }
            beam = MakeMarker(Vector3.zero, 3f, 120f, targetMat, null);
            beam.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ flow
        public void Begin(Mission m) => Begin(m, 0);

        public void Begin(Mission m, int fromStep)
        {
            if (Active != null || m == null) return;
            if (m.nightOnly && DayNightCycle.Instance && DayNightCycle.Instance.NightFactor < 0.5f)
            {
                GameEvents.ShowToast("Ye mission sirf raat mein milega. (N dabake raat karo)", 3f);
                return;
            }
            Active = m;
            stepIndex = Mathf.Clamp(fromStep, 0, Mathf.Max(0, m.steps.Count - 1));
            Timed = false;
            retryAt = -1f;
            if (fromStep == 0 && !m.quiet) GameEvents.ShowBanner(Loc.T(m.title), Loc.T(m.intro), new Color(1f, 0.85f, 0.3f));
            m.onBegin?.Invoke(stepIndex);
            Debug.Log($"[Nainital] mission {m.id} begins at step {stepIndex}");
            BeginStep();
        }

        void BeginStep()
        {
            var s = Step;
            if (s == null) return;
            s.Begin();
            if (s.timeLimit > 0f) { TimeLeft = s.timeLimit; Timed = true; }
        }

        void Finish(bool passed, string reason)
        {
            failedStep = stepIndex;
            Step?.End();
            var m = Active;
            Active = null;
            Timed = false;
            if (beam) beam.gameObject.SetActive(false);
            if (passed)
            {
                int reward = GameState.IsDone(m.id) ? m.reward / 2 : m.reward;
                if (reward > 0) GameState.AddMoney(reward);
                GameState.MarkDone(m.id);
                var p = PlayerController.Instance;
                if (p) p.Health.Heal(100f);
                if (!m.quiet) GameEvents.ShowBanner(Loc.L("MISSION PASSED!", "MISSION PASSED!"), Loc.T(m.title) + (reward > 0 ? "   +Rs " + reward : "") + (string.IsNullOrEmpty(m.passNote) ? "" : "\n" + Loc.T(m.passNote)), new Color(0.4f, 1f, 0.5f));
                Debug.Log($"[Nainital] mission {m.id} passed");
                m.onEnd?.Invoke(true);
                StoryMissions.Passed(m);
            }
            else
            {
                GameEvents.ShowBanner(Loc.L("MISSION FAILED", "MISSION FAILED"), Loc.T(reason ?? ""), new Color(1f, 0.35f, 0.3f));
                Debug.Log($"[Nainital] mission {m.id} failed at step {failedStep}: {reason}");
                m.onEnd?.Invoke(false);
                if (m.story && !aborting)
                {
                    // again from the last checkpoint at or before the failed step (GTA: "retry")
                    int cp = 0;
                    for (int i = 0; i <= Mathf.Min(failedStep, m.steps.Count - 1); i++) if (m.steps[i].checkpoint) cp = i;
                    retryMission = m; retryStep = cp; retryAt = Time.time + 3.2f;
                }
            }
        }

        Mission retryMission;
        int retryStep, failedStep;
        float retryAt = -1f;
        bool aborting;

        public void Abort()
        {
            retryAt = -1f; retryMission = null;
            if (Active == null) return;
            aborting = true;
            try { Finish(false, "Mission chhod diya"); } finally { aborting = false; }
        }

        void Update()
        {
            if (!built)
            {
                buildDelay -= Time.deltaTime;
                if (buildDelay <= 0f) BuildCatalog();
                return;
            }
            if (Active == null && retryMission != null && retryAt > 0f && Time.time >= retryAt && !Cutscene.Playing)
            {
                var rm = retryMission; retryMission = null; retryAt = -1f;
                Begin(rm, retryStep);
            }
            // giver markers only while free, and only the story's next mission
            foreach (var (m, mk) in Givers)
                if (mk) mk.gameObject.SetActive(Active == null && retryMission == null && StoryMissions.IsNext(m));

            if (Active == null) return;
            var p = PlayerController.Instance;
            if (p && p.CurrentMode == PlayerController.Mode.Dead) { Finish(false, "Aap behosh ho gaye"); return; }
            if (Timed)
            {
                TimeLeft -= Time.deltaTime;
                if (TimeLeft <= 0f) { Finish(false, "Time khatam!"); return; }
            }
            var s = Step;
            string why = s?.Failed;
            if (!string.IsNullOrEmpty(why)) { Finish(false, why); return; }
            if (s != null && s.Done())
            {
                s.End();
                stepIndex++;
                if (stepIndex >= Active.steps.Count) { Finish(true, null); return; }
                if (!Active.quiet && !string.IsNullOrEmpty(Step.text) && !(Step is CutsceneStep)) GameEvents.ShowToast(Loc.L("Shabash! Agla kaam: ", "Nice! Next: ") + Loc.T(Step.text), 2.5f);
                BeginStep();
            }
            var t = CurrentTarget;
            if (beam)
            {
                beam.gameObject.SetActive(t.HasValue);
                if (t.HasValue)
                {
                    beam.position = t.Value + Vector3.down * 2f;
                    beam.localScale = Vector3.one * Mathf.Max(0.6f, Step.Radius / 3f);
                }
            }
        }
    }

    /// <summary>Walk into a yellow marker and press E to start its mission.</summary>
    public class MissionGiver : Interactable
    {
        public Mission mission;
        public override string Prompt => mission == null ? "" :
            "Mission: " + Loc.T(mission.title) + (mission.reward > 0 ? "  (Rs " + mission.reward + ")" : "");
        public override bool CanInteract(PlayerController p) => MissionManager.Instance && MissionManager.Instance.Active == null;
        public override void Interact(PlayerController p) => MissionManager.Instance.Begin(mission);
    }

    /// <summary>Billboard helper for floating labels.</summary>
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var c = Camera.main;
            if (!c) return;
            transform.rotation = Quaternion.LookRotation(transform.position - c.transform.position);
            transform.localPosition = new Vector3(0, transform.localPosition.y + Mathf.Sin(Time.time * 2f) * 0.002f, 0);
        }
    }
}
