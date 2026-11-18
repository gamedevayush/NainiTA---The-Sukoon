using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>A body lying where someone died, until the 108 ambulance takes it (IncidentManager).</summary>
    public class Casualty : MonoBehaviour
    {
        public HumanoidRig rig;
        public System.Action collected;
        public bool onStretcher;
        public bool gone;
        public float diedAt;

        /// <summary>Taken away: the pedestrian goes back to the pool, a policeman is removed.</summary>
        public void Collect()
        {
            if (gone) return;
            gone = true;
            var a = collected;
            collected = null;
            a?.Invoke();
        }
    }

    /// <summary>
    /// What happens when somebody dies in the town (user, 2026-11-18: "agar koi mrta hai to police aayegi phir
    /// ambulance.. traffic rukjaeyega.. and ambulance gaadi me daalke lejayegi"):
    /// the traffic stops short of the spot (TrafficCar / TrafficBike ask HoldAhead) and people nearby stop and stare;
    /// a police Bolero comes with its siren and one constable stands guard by the body; then the 108 ambulance comes,
    /// two of its crew carry the body to it on a stretcher, and it drives off with its siren; the traffic goes on.
    /// Bodies within 25 m of each other are one incident (the crew make a trip for each). At most two incidents at a time
    /// get the full response; any other body is taken away quietly once nobody is looking. The player far away (260 m):
    /// it is all cleared at once.
    /// </summary>
    public class IncidentManager : MonoBehaviour
    {
        public static IncidentManager Instance { get; private set; }

        public class Incident
        {
            public Vector3 pos;
            public readonly List<Casualty> bodies = new List<Casualty>();
            public float age, crowdT;
            public PoliceCar police;
            public Ambulance ambulance;
            public bool policeSent, ambulanceSent, done;
            public int ambulanceTries;
        }

        public readonly List<Incident> Active = new List<Incident>();
        readonly List<Casualty> quiet = new List<Casualty>();
        readonly List<(float at, System.Action a)> later = new List<(float, System.Action)>();
        float timer;
        static readonly Collider[] near = new Collider[48];

        void Awake() => Instance = this;

        /// <summary>Someone died here (NpcWalker, PoliceOfficer, a medic).</summary>
        public static void Report(GameObject body, HumanoidRig rig, System.Action collected)
        {
            if (!body) return;
            var im = Instance ? Instance : WantedSystem.Ensure().GetComponent<IncidentManager>();
            var c = body.GetComponent<Casualty>();
            if (!c) c = body.AddComponent<Casualty>();
            c.rig = rig;
            c.collected = collected;
            c.gone = false;
            c.onStretcher = false;
            c.diedAt = Time.time;
            im.Add(c);
        }

        void Add(Casualty c)
        {
            Vector3 p = c.transform.position;
            foreach (var inc in Active)
                if (!inc.done && (inc.pos - p).sqrMagnitude < 25f * 25f) { inc.bodies.Add(c); return; }
            if (Active.Count >= 2) { quiet.Add(c); return; }
            var n = new Incident { pos = p };
            n.bodies.Add(c);
            Active.Add(n);
            Gawk(n);
        }

        /// <summary>Run this after a delay (bring a townsperson back to his post a while after the body was taken).</summary>
        public void Later(float seconds, System.Action a) => later.Add((Time.time + seconds, a));

        /// <summary>Is there an accident ahead of a vehicle at pos going fwd (within 30 m, on its road)? dist = how far.</summary>
        public static bool HoldAhead(Vector3 pos, Vector3 fwd, out float dist)
        {
            dist = 0f;
            var im = Instance;
            if (!im) return false;
            fwd.y = 0f;
            foreach (var inc in im.Active)
            {
                if (inc.done) continue;
                Vector3 rel = inc.pos - pos;
                rel.y = 0f;
                float along = Vector3.Dot(rel, fwd);
                if (along < 0f || along > 30f) continue;
                if ((rel - fwd * along).magnitude > 7f) continue;
                dist = along;
                return true;
            }
            return false;
        }

        /// <summary>People nearby stop and look (a few come closer).</summary>
        void Gawk(Incident inc)
        {
            int n = Physics.OverlapSphereNonAlloc(inc.pos, 30f, near, 1 << 9, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var w = near[i].GetComponent<NpcWalker>();
                if (w && !w.InCar) w.Watch(inc.pos, Random.Range(18f, 35f));
            }
        }

        static bool OnScreen(Vector3 p)
        {
            var cam = Camera.main;
            if (!cam) return false;
            Vector3 vp = cam.WorldToViewportPoint(p);
            return vp.z > 0f && vp.z < 150f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
        }

        void Update()
        {
            for (int i = later.Count - 1; i >= 0; i--)
                if (Time.time >= later[i].at) { var a = later[i].a; later.RemoveAt(i); try { a(); } catch (System.Exception e) { Debug.LogException(e); } }

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            const float step = 0.5f;
            timer = step;
            var p = PlayerController.Instance;
            Vector3 pp = p ? p.transform.position : Vector3.zero;

            // bodies with no full response: gone once nobody is looking (or the player is far)
            for (int i = quiet.Count - 1; i >= 0; i--)
            {
                var c = quiet[i];
                if (!c || c.gone) { quiet.RemoveAt(i); continue; }
                bool far = (c.transform.position - pp).sqrMagnitude > 200f * 200f;
                if (far || (Time.time - c.diedAt > 40f && !OnScreen(c.transform.position))) { c.Collect(); quiet.RemoveAt(i); }
            }

            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var inc = Active[i];
                inc.age += step;
                inc.bodies.RemoveAll(b => !b || b.gone);
                bool far = (inc.pos - pp).sqrMagnitude > 260f * 260f;
                if (far || inc.age > 200f)
                {
                    // the player has gone, or it took too long: clear what nobody can see
                    foreach (var b in inc.bodies) if (b && !b.onStretcher && (far || !OnScreen(b.transform.position))) b.Collect();
                    inc.bodies.RemoveAll(b => !b || b.gone);
                }
                if (inc.bodies.Count == 0 && !inc.done)
                {
                    inc.done = true;
                    if (inc.police) inc.police.sceneDone = true;
                }
                if (inc.done)
                {
                    if (!inc.ambulance || inc.ambulance.Leaving) Active.RemoveAt(i);
                    continue;
                }

                inc.crowdT += step;
                if (inc.crowdT > 6f && inc.age < 40f) { inc.crowdT = 0f; Gawk(inc); }

                // the police first ...
                if (!inc.policeSent && inc.age > 4f)
                {
                    inc.policeSent = true;
                    var pm = PoliceManager.Instance;
                    if (pm && !pm.PoliceNear(inc.pos, 60f)) inc.police = pm.SpawnSceneCar(inc.pos);
                }
                // ... then the 108 ambulance (once the police are there, or after a while anyway)
                if (!inc.ambulanceSent && (inc.age > 24f || (inc.police && inc.police.Driver && inc.police.Driver.Arrived && inc.age > 8f)))
                {
                    var pm = PoliceManager.Instance;
                    inc.ambulance = pm ? pm.SpawnAmbulance(inc) : null;
                    inc.ambulanceTries++;
                    inc.ambulanceSent = inc.ambulance || inc.ambulanceTries > 6;
                    if (inc.ambulance) GameEvents.ShowToast("108 ambulance aa rahi hai", 2.5f);
                }
            }
        }

        /// <summary>The next body of this incident nobody has picked up yet (nearest to `from`).</summary>
        public static Casualty NextBody(Incident inc, Vector3 from)
        {
            Casualty best = null;
            float bd = float.MaxValue;
            foreach (var b in inc.bodies)
            {
                if (!b || b.gone || b.onStretcher) continue;
                float d = (b.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }
    }
}
