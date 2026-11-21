using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>One line of a phone call and the player's possible replies (Phone shows it).</summary>
    public class CallNode
    {
        public string who, line;
        public readonly List<(string label, Func<CallNode> next)> replies = new List<(string, Func<CallNode>)>();
        public CallNode(string who, string line) { this.who = who; this.line = line; }
        public CallNode Reply(string label, Func<CallNode> next) { replies.Add((label, next)); return this; }
        public bool End => replies.Count == 0;
    }

    /// <summary>
    /// 112 / 100 / 108 / 101 from the phone (owner, 21 Nov 2026: "112 + 100 police / 108 ambulance / 101 fire; UI dialogue
    /// with if/else answers ... prank call -> police angry, fine / 1 star"). The operator asks, the player picks a reply, and
    /// what happens depends on the real game state at that moment: an accident near the player, someone hurt, the player's
    /// own health, flames within reach. A call about nothing is a prank: the police come anyway and, finding nothing, fine
    /// the player (Rs 500; a second prank within 10 minutes, or no money: one star as well).
    /// </summary>
    public static class EmergencyCalls
    {
        public const float NearAccident = 60f, NearFire = 220f;   // a fire is called in from where its smoke is seen (FireManager lights them 90-180 m off)
        static readonly List<float> prankTimes = new List<float>();

        static Vector3 PlayerPos => PlayerController.Instance ? PlayerController.Instance.transform.position : Vector3.zero;

        public static CallNode Dial(string number)
        {
            switch (number)
            {
                case "112":
                    return new CallNode("112 Emergency", "112 Emergency Response. Aapko kis cheez ki madad chahiye?")
                        .Reply("Police", Police)
                        .Reply("Ambulance", Ambulance)
                        .Reply("Fire brigade", Fire);
                case "100": return Police();
                case "108": return Ambulance();
                case "101": return Fire();
            }
            return new CallNode("", "Aapke dwara dial kiya gaya number maujood nahi hai.");
        }

        // ------------------------------------------------------------------ 100 police
        static CallNode Police() => new CallNode("100 Police Control Room", "Police control room, Nainital. Bataiye, kya hua hai?")
            .Reply("Accident hua hai", PoliceAccident)
            .Reply("Maar-peet / chori ho rahi hai", PoliceFight)
            .Reply("Kuch nahi... mazaak tha", () => Prank("Mazaak? Ye emergency line hai! Aapki location trace ho gayi hai, gaadi aa rahi hai."));

        static CallNode PoliceAccident()
        {
            var inc = NearestIncident(PlayerPos, NearAccident);
            if (inc != null)
            {
                if (inc.police) return Bye("100 Police Control Room", "Is accident ki report mil chuki hai. PCR gaadi raste mein hai, wahin rukiye.");
                inc.policeSent = true;
                var pm = PoliceManager.Instance;
                inc.police = pm ? pm.SpawnSceneCar(inc.pos) : null;
                if (!inc.police) EmergencyDispatch.Get().Police(inc.pos, false, "accident");
                return Bye("100 Police Control Room", "Location mil gayi. PCR gaadi bhej rahe hain. Ghayal ko hilaiye mat, 108 bhi aa rahi hai.");
            }
            if (NearestWreck(PlayerPos, NearAccident, out var wreck))
            {
                EmergencyDispatch.Get().Police(wreck, false, "wreck");
                bool fire = FireManager.NearestFire(PlayerPos, NearFire, out _, out _);
                return Bye("100 Police Control Room", fire ? "Gaadi bhej rahe hain. Aag lagi hai to 101 pe fire brigade ko bhi bataiye, aur gaadi se door rahiye."
                                                          : "Gaadi bhej rahe hain. Wahin rukiye, traffic mat rokiye.");
            }
            return Prank("Theek hai, gaadi bhej rahe hain. Wahin rukiye.");
        }

        static CallNode PoliceFight()
        {
            var p = PlayerController.Instance;
            bool hurtLately = p && p.Health && Time.time - p.Health.LastDamageTime < 90f && p.Health.LastDamageTime > 0f;
            bool goons = EnemyNear(PlayerPos, 45f);
            if (WantedSystem.Instance && WantedSystem.Instance.Stars > 0)
                return Bye("100 Police Control Room", "Aapki location pe team pehle se hai. Haath upar rakhiye aur sahyog kijiye.");
            if (hurtLately || goons)
            {
                EmergencyDispatch.Get().Police(PlayerPos, false, "fight");
                return Bye("100 Police Control Room", "Ghabraiye mat. Beat constable aur PCR gaadi bhej rahe hain, kisi bheed wali jagah chale jaiye.");
            }
            return Prank("Theek hai, gaadi bhej rahe hain. Wahin rukiye.");
        }

        // ------------------------------------------------------------------ 108 ambulance
        static CallNode Ambulance()
        {
            var p = PlayerController.Instance;
            float hp = p && p.Health ? p.Health.Fraction : 1f;
            var inc = NearestIncident(PlayerPos, NearAccident);
            var n = new CallNode("108 Ambulance", "108 Emergency. Kaun ghayal hai, aur kitni chot hai?");
            n.Reply("Main khud ghayal hoon", () =>
            {
                if (hp < 0.7f)
                {
                    if (EmergencyDispatch.Get().AmbulanceForPlayer(PlayerPos))
                        return Bye("108 Ambulance", "Ambulance nikal chuki hai. Wahin rukiye, zyada hiliye mat - team aapki marham-patti kar degi.");
                    return Bye("108 Ambulance", "Abhi saari gaadiyan busy hain. Thodi der mein phir call kijiye.");
                }
                return Prank("Aapki awaaz to bilkul theek lag rahi hai... theek hai, team bhej rahe hain.");
            });
            n.Reply("Koi aur ghayal hai", () =>
            {
                if (inc != null)
                {
                    if (inc.ambulance) return Bye("108 Ambulance", "Is jagah ke liye ambulance raste mein hai. Ghayal ko hilaiye mat.");
                    var pm = PoliceManager.Instance;
                    inc.ambulanceSent = true;
                    inc.ambulance = pm ? pm.SpawnAmbulance(inc) : null;
                    if (!inc.ambulance) inc.ambulanceSent = false;   // the incident's own dispatch tries again
                    return Bye("108 Ambulance", "Ambulance bhej rahe hain. Ghayal ko seedha leta rehne dijiye, paani mat pilaiye.");
                }
                return Prank("Theek hai, team bhej rahe hain.");
            });
            return n;
        }

        // ------------------------------------------------------------------ 101 fire
        static CallNode Fire()
        {
            var n = new CallNode("101 Fire Control", "Fire control room. Aag kahan lagi hai?");
            n.Reply("Yahin paas mein, gaadi / dukaan jal rahi hai", () =>
            {
                if (FireManager.NearestFire(PlayerPos, NearFire, out var at, out var src))
                {
                    if (FireTruck.Coming(at)) return Bye("101 Fire Control", "Fire tender raste mein hai. Logon ko door rakhiye.");
                    if (EmergencyDispatch.Get().FireTruck(at, src))
                        return Bye("101 Fire Control", "Fire tender nikal chuka hai. Aag se door rahiye, aur kisi ko paas mat jaane dijiye.");
                    return Bye("101 Fire Control", "Saare tender abhi busy hain. Thodi der mein phir call kijiye.");
                }
                return Prank("Theek hai, team bhej rahe hain.");
            });
            n.Reply("Kahin nahi... mazaak tha", () => Prank("Fire control se mazaak? Police ko aapki location bhej di gayi hai."));
            return n;
        }

        // ------------------------------------------------------------------ helpers
        static CallNode Bye(string who, string line) => new CallNode(who, line);

        static CallNode Prank(string line)
        {
            prankTimes.RemoveAll(t => Time.time - t > 600f);
            prankTimes.Add(Time.time);
            EmergencyDispatch.Get().Police(PlayerPos, true, "prank");
            return new CallNode("Control Room", line);
        }

        /// <summary>How many prank calls in the last 10 minutes (the second one also brings a star).</summary>
        public static int RecentPranks { get { prankTimes.RemoveAll(t => Time.time - t > 600f); return prankTimes.Count; } }

        public static IncidentManager.Incident NearestIncident(Vector3 p, float r)
        {
            var im = IncidentManager.Instance;
            if (!im) return null;
            IncidentManager.Incident best = null;
            float bd = r * r;
            foreach (var inc in im.Active)
            {
                if (inc.done) continue;
                float d = (inc.pos - p).sqrMagnitude;
                if (d < bd) { bd = d; best = inc; }
            }
            return best;
        }

        static bool NearestWreck(Vector3 p, float r, out Vector3 at)
        {
            at = p;
            float bd = r * r;
            bool found = false;
            foreach (var v in VehicleDamage.All)
            {
                if (!v || !(v.Wrecked || v.Burning || v.hits >= 4)) continue;
                float d = (v.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; at = v.transform.position; found = true; }
            }
            return found;
        }

        static bool EnemyNear(Vector3 p, float r)
        {
            foreach (var e in EnemyAI.All)
                if (e && !e.Dead && (e.transform.position - p).sqrMagnitude < r * r) return true;
            return false;
        }
    }

    /// <summary>
    /// What a call set going: police cars on their way (a prank: they come, find nothing and fine the player), the 108
    /// ambulance for the player, the fire tender. Retries a spawn that found no road out of sight.
    /// </summary>
    public class EmergencyDispatch : MonoBehaviour
    {
        static EmergencyDispatch inst;
        public static EmergencyDispatch Get()
        {
            if (!inst) inst = new GameObject("EmergencyDispatch").AddComponent<EmergencyDispatch>();
            return inst;
        }

        class Visit { public Vector3 at; public bool prank; public string why; public PoliceCar car; public float t0, arrivedT = -1f, retry; }
        readonly List<Visit> visits = new List<Visit>();

        public void Police(Vector3 at, bool prank, string why)
        {
            var v = new Visit { at = at, prank = prank, why = why, t0 = Time.time };
            var pm = PoliceManager.Instance;
            v.car = pm ? pm.SpawnSceneCar(at) : null;
            visits.Add(v);
        }

        public bool AmbulanceForPlayer(Vector3 at)
        {
            var pm = PoliceManager.Instance;
            return pm && pm.SpawnAmbulanceForPlayer(at);
        }

        public bool FireTruck(Vector3 at, Component src)
        {
            var pm = PoliceManager.Instance;
            return pm && pm.SpawnFireTruck(at, src);
        }

        void Update()
        {
            var p = PlayerController.Instance;
            Vector3 pp = p ? p.transform.position : Vector3.zero;
            for (int i = visits.Count - 1; i >= 0; i--)
            {
                var v = visits[i];
                float age = Time.time - v.t0;
                if (!v.car)
                {
                    // no road out of sight yet: try again now and then; a prank is fined from the station after a while
                    v.retry -= Time.deltaTime;
                    if (v.retry <= 0f && age < 40f)
                    {
                        v.retry = 3f;
                        var pm = PoliceManager.Instance;
                        v.car = pm ? pm.SpawnSceneCar(v.at) : null;
                    }
                    if (!v.car && age > 60f)
                    {
                        if (v.prank) Fine(true);
                        visits.RemoveAt(i);
                    }
                    continue;
                }
                bool there = v.car.Driver && (v.car.Driver.Arrived || v.car.Driver.stop) ||
                             (v.car.transform.position - v.at).sqrMagnitude < 26f * 26f;
                if (there && v.arrivedT < 0f) v.arrivedT = Time.time;
                bool gaveUp = age > 150f;
                if ((v.arrivedT > 0f && Time.time - v.arrivedT > 5f) || gaveUp)
                {
                    if (v.prank) Fine((pp - v.at).sqrMagnitude > 90f * 90f);
                    else if (v.why == "fight") Arrest(v.at);
                    else if (v.why != "accident") GameEvents.ShowToast("Police: \"Hum dekh lete hain. Aap aage badhiye.\"", 3f);
                    v.car.sceneDone = true;
                    visits.RemoveAt(i);
                }
            }
        }

        /// <summary>The police found nothing: a challan of Rs 500; a second prank in 10 minutes (or no money): a star too.</summary>
        static void Fine(bool byPost)
        {
            int amount = EmergencyCalls.RecentPranks >= 2 ? 1000 : 500;
            bool paid = GameState.TrySpend(amount);
            // whole sentences, so each has one entry in the language table (Loc; the amount is its '#')
            string msg = byPost
                ? (paid ? $"Police ne call trace karke challan bhej diya - Rs {amount} jurmana" : "Police ne call trace karke challan bhej diya - jurmana bharne ke paise nahi!")
                : (paid ? $"Police: \"Yahan to kuch hua hi nahi! Jhoothi call?\" - Rs {amount} jurmana" : "Police: \"Yahan to kuch hua hi nahi! Jhoothi call?\" - jurmana bharne ke paise nahi!");
            GameEvents.ShowToast(msg, 4f);
            if (!paid || EmergencyCalls.RecentPranks >= 2)
            {
                var w = WantedSystem.Ensure();
                w.Prank(paid ? "Baar baar jhoothi call! Police naraz hai" : "Jurmana nahi bhara! Police naraz hai");
            }
        }

        /// <summary>A real "maar-peet" call: the goons near the spot run off before the police (they leave the scene).</summary>
        static void Arrest(Vector3 at)
        {
            int n = 0;
            foreach (var e in EnemyAI.All.ToArray())
                if (e && !e.Dead && (e.transform.position - at).sqrMagnitude < 50f * 50f) { Destroy(e.gameObject); n++; }
            GameEvents.ShowToast(n > 0 ? "Police ne goondon ko pakad liya!" : "Police: \"Yahan ab sab theek hai. Dhyaan rakhiye.\"", 3.5f);
        }
    }
}
