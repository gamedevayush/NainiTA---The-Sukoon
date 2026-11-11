using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// GTA-style mission flow: yellow mission markers around Nainital, objectives with a light-beam target,
    /// timers, pass/fail banners and Rs rewards. Missions are assembled at runtime from the generated world.
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

        void Awake() => Instance = this;

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
                tm.font = GameAssets.Font;
                tm.GetComponent<MeshRenderer>().sharedMaterial = GameAssets.Font.material;
                tm.text = label;
                tm.fontSize = 64;
                tm.characterSize = 0.06f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = new Color(1f, 0.85f, 0.2f);
                t.AddComponent<FaceCamera>();
            }
            return go.transform;
        }

        // ------------------------------------------------------------------ catalog
        static Vector3 Ground(Vector3 p)
        {
            var t = Terrain.activeTerrain;
            if (t) p.y = t.SampleHeight(p) + t.transform.position.y;
            return p;
        }

        static Vector3 IntoLake(Vector3 p)
        {
            var la = LakeArea.Instance;
            if (!la) return p;
            Vector3 c = Vector3.zero;
            foreach (var q in la.innerOutline) c += new Vector3(q.x, 0, q.y);
            c /= la.innerOutline.Length;
            for (int i = 0; i < 40 && !LakeArea.ContainsInner(p); i++) p = Vector3.Lerp(p, c, 0.15f);
            p.y = la.waterY;
            return p;
        }

        void BuildCatalog()
        {
            built = true;
            var gm = FindAnyObjectByType<GameManager>();
            var player = PlayerController.Instance;
            if (!gm || !player) return;
            var marks = new Dictionary<string, Vector3>();
            if (gm.blipNames != null)
                for (int i = 0; i < gm.blipNames.Length; i++) marks[gm.blipNames[i]] = gm.blipPositions[i];
            Vector3 L(string key, Vector3 fallback) => marks.TryGetValue(key, out var v) ? v : fallback;

            Vector3 spawn = player.RespawnPoint;
            Vector3 fwd = player.transform.forward, right = player.transform.right;
            Vector3 temple = L("Naina Devi Mandir", spawn);
            Vector3 bazaar = L("Bada Bazar", spawn);
            Vector3 busStand = L("Tallital Bus Stand", spawn);
            Vector3 snow = L("Snow View", spawn);
            Vector3 tibet = L("Tibetan Market", L("The Flats", spawn));

            // docks
            Transform mallDock = null, tallDock = null;
            foreach (var b in FindObjectsByType<BoatController>(FindObjectsSortMode.None))
            {
                if (!b.dockExit) continue;
                if (!mallDock || b.dockExit.position.z > mallDock.position.z) mallDock = b.dockExit;
                if (!tallDock || b.dockExit.position.z < tallDock.position.z) tallDock = b.dockExit;
            }
            // enemies
            var bazaarGang = new List<EnemyAI>();
            var tallGang = new List<EnemyAI>();
            foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
            {
                if (e.name.Contains("Bada Bazar")) bazaarGang.Add(e);
                else if (e.name.Contains("Tallital")) tallGang.Add(e);
            }
            // nearest food stall to the temple, nearest taxi to the bus stand
            Vector3 stall = spawn;
            float bd = float.MaxValue;
            foreach (var f in FindObjectsByType<FoodStall>(FindObjectsSortMode.None))
            {
                float d = (f.transform.position - temple).sqrMagnitude;
                if (d < bd) { bd = d; stall = f.transform.position; }
            }
            Vector3? taxi = null;
            bd = float.MaxValue;
            foreach (var c in FindObjectsByType<CarController>(FindObjectsSortMode.None))
            {
                float d = (c.transform.position - busStand).sqrMagnitude;
                if (c.type == CarType.Taxi && d < bd) { bd = d; taxi = c.transform.position; }
            }

            var m1 = new Mission { id = "darshan", title = "Nainital Darshan", reward = 300, giver = Ground(spawn + fwd * 2f + right * 4f),
                intro = "Nainital mein pehla din! Pehle Maa Naina Devi ke darshan, phir kuch garma-garam khaana, phir jheel ka nazara." };
            m1.steps.Add(new GoToStep("Naina Devi Mandir jaao aur darshan karo", temple, 12f, pressE: true));
            m1.steps.Add(new CountEventStep("Kisi stall se kuch khao (E)", 1, false, stall));
            m1.steps.Add(new GoToStep("Wapas jheel ke kinare aao", spawn, 8f));
            missions.Add(m1);

            if (bazaarGang.Count > 0)
            {
                var m2 = new Mission { id = "gang", title = "Gunda Raj Khatam", reward = 1000, giver = Ground(bazaar + (spawn - bazaar).normalized * 28f),
                    intro = "Bada Bazar ke dukaandaar pareshaan hain - ek gang hafta vasooli kar raha hai. Unhe sabak sikhao!" };
                m2.steps.Add(new GoToStep("Bada Bazar pahuncho", bazaarGang[0].transform.position, 18f));
                m2.steps.Add(new DefeatStep("Gang ko harao", bazaarGang));
                missions.Add(m2);
            }

            if (mallDock && tallDock && LakeArea.Instance)
            {
                Vector3 a = mallDock.position, b = tallDock.position;
                var m3 = new Mission { id = "boatrace", title = "Naav Race", reward = 800, giver = a + (a - b).normalized * 6f,
                    intro = "Boat club walon ki challenge: Mallital se Tallital tak naav, saare checkpoints, 4 minute mein!" };
                m3.steps.Add(new EnterModeStep("Jetty se naav lo (E)", PlayerController.Mode.Boat, a));
                m3.steps.Add(new GoToStep("Checkpoint 1", IntoLake(Vector3.Lerp(a, b, 0.3f)), 16f, needBoat: true, timeLimit: 240f));
                m3.steps.Add(new GoToStep("Checkpoint 2", IntoLake(Vector3.Lerp(a, b, 0.62f)), 16f, needBoat: true));
                m3.steps.Add(new GoToStep("Tallital jetty pe finish", b, 14f, needBoat: true));
                missions.Add(m3);
            }

            var m4 = new Mission { id = "taxi", title = "Taxi Driver", reward = 1200, giver = Ground(busStand + new Vector3(8f, 0, 8f)),
                intro = "Taxi stand wale bhaiya beemar hain. Unki taxi chalao: Mallital se sawari uthao aur Tallital chhodo." };
            m4.steps.Add(new EnterModeStep("Taxi (ya koi bhi gaadi) mein baitho", PlayerController.Mode.Vehicle, taxi));
            m4.steps.Add(new GoToStep("Mallital se sawari uthao", Ground(tibet), 14f, needCar: true, timeLimit: 200f));
            m4.steps.Add(new GoToStep("Sawari ko Tallital Bus Stand chhodo", Ground(busStand), 16f, needCar: true, timeLimit: 200f));
            missions.Add(m4);

            var m5 = new Mission { id = "parcel", title = "Snow View Parcel", reward = 700, giver = Ground(tibet + new Vector3(-10f, 0, 6f)),
                intro = "Snow View ke cafe wale ka parcel jaldi pahunchana hai - 7 minute hain. Paidal, gaadi, jaise marzi!" };
            m5.steps.Add(new GoToStep("Parcel Snow View pahunchao (E)", snow, 16f, pressE: true, timeLimit: 420f));
            missions.Add(m5);

            var m6 = new Mission { id = "monkeys", title = "Bandar Bhagao", reward = 400, giver = Ground(temple + (spawn - temple).normalized * 16f),
                intro = "Mandir ke bandar prasad cheen rahe hain! Unki taraf daudo (Shift) aur bhagao." };
            m6.steps.Add(new CountEventStep("3 bandar bhagao (Shift se unki taraf daudo)", 3, true, temple));
            missions.Add(m6);

            var places = new List<(string, Vector3)>();
            foreach (var k in new[] { "Naini Peak (China Peak)", "Tiffin Top (Dorothy's Seat)", "Snow View", "Land's End", "Pashan Devi Mandir" })
                if (marks.TryGetValue(k, out var v)) places.Add((k, v));
            if (places.Count >= 3)
            {
                var m7 = new Mission { id = "photo", title = "Nainital Photo Tour", reward = 2000, giver = Ground(spawn - right * 5f + fwd * 2f),
                    intro = "Ek travel blogger ko Nainital ke best viewpoints ki photos chahiye. Sab jagah jaao!" };
                m7.steps.Add(new VisitAllStep("Saare viewpoints ki photo lo", places));
                missions.Add(m7);
            }

            if (tallGang.Count > 0)
            {
                var m8 = new Mission { id = "night", title = "Raat ki Gasht", reward = 1500, nightOnly = true, giver = Ground(busStand + new Vector3(-12f, 0, 10f)),
                    intro = "Raat mein Tallital pe gunde logon ko loot rahe hain. Police late hai - tum sambhalo!" };
                m8.steps.Add(new GoToStep("Tallital ke andhere mein gunde dhundo", tallGang[0].transform.position, 22f));
                m8.steps.Add(new DefeatStep("Gundon ko harao", tallGang));
                missions.Add(m8);
            }

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
                var mk = MakeMarker(m.giver, 1.1f, 3.2f, giverMat, "M");
                var g = mk.gameObject.AddComponent<MissionGiver>();
                g.mission = m;
                Givers.Add((m, mk));
            }
            beam = MakeMarker(Vector3.zero, 3f, 120f, targetMat, null);
            beam.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ flow
        public void Begin(Mission m)
        {
            if (Active != null) return;
            if (m.nightOnly && DayNightCycle.Instance && DayNightCycle.Instance.NightFactor < 0.5f)
            {
                GameEvents.ShowToast("Ye mission sirf raat mein milega. (N dabake raat karo)", 3f);
                return;
            }
            Active = m;
            stepIndex = 0;
            Timed = false;
            GameEvents.ShowBanner(m.title, m.intro, new Color(1f, 0.85f, 0.3f));
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
            Step?.End();
            var m = Active;
            Active = null;
            Timed = false;
            if (beam) beam.gameObject.SetActive(false);
            if (passed)
            {
                int reward = GameState.IsDone(m.id) ? m.reward / 2 : m.reward;
                GameState.AddMoney(reward);
                GameState.MarkDone(m.id);
                var p = PlayerController.Instance;
                if (p) p.Health.Heal(100f);
                GameEvents.ShowBanner("MISSION PASSED!", m.title + "   +Rs " + reward, new Color(0.4f, 1f, 0.5f));
            }
            else GameEvents.ShowBanner("MISSION FAILED", reason, new Color(1f, 0.35f, 0.3f));
        }

        public void Abort() { if (Active != null) Finish(false, "Mission chhod diya"); }

        void Update()
        {
            if (!built)
            {
                buildDelay -= Time.deltaTime;
                if (buildDelay <= 0f) BuildCatalog();
                return;
            }
            // giver markers only while free
            foreach (var (m, mk) in Givers)
                if (mk) mk.gameObject.SetActive(Active == null);

            if (Active == null) return;
            var p = PlayerController.Instance;
            if (p && p.CurrentMode == PlayerController.Mode.Dead) { Finish(false, "Aap behosh ho gaye"); return; }
            if (Timed)
            {
                TimeLeft -= Time.deltaTime;
                if (TimeLeft <= 0f) { Finish(false, "Time khatam!"); return; }
            }
            var s = Step;
            if (s != null && s.Done())
            {
                s.End();
                stepIndex++;
                if (stepIndex >= Active.steps.Count) { Finish(true, null); return; }
                GameEvents.ShowToast("Shabash! Agla kaam: " + Step.text, 2.5f);
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
            "Mission: " + mission.title + "  (Rs " + (GameState.IsDone(mission.id) ? mission.reward / 2 : mission.reward) + (GameState.IsDone(mission.id) ? ", dobara" : "") + ")";
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
