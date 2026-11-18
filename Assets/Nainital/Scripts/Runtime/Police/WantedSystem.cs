using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// The police after the player (user, 2026-11-18): three levels only.
    /// 1 star: hitting someone. One constable comes, on a bike or running, points his pistol at you and, if you calm
    ///         down, tells you off and goes ("focus guns on us and fade out"). He does not shoot.
    /// 2 stars: killing someone. A police Bolero with two men. Face to face and standing still, they arrest you; run,
    ///         drive off or fight and they shoot (on foot, from the car, from the bike).
    /// 3 stars: three people killed, or two policemen. Two Bolero and a bike; they shoot on sight.
    /// Hitting a policeman is at least 2 stars. Out of their sight long enough (30 s at 2 stars, 45 s at 3) they give up.
    /// Arrested: Mallital police, a fine. Killed while wanted: BD Pandey District Hospital, the bill.
    /// The pistol hits take the player's own health (Health), as any hit does.
    /// Lives on the "Police" object (GameManager makes it) with PoliceManager (the units) and IncidentManager (bodies,
    /// the 108 ambulance, traffic held at the spot).
    /// </summary>
    public class WantedSystem : MonoBehaviour
    {
        public static WantedSystem Instance { get; private set; }

        public int Stars { get; private set; }
        public int CivKills { get; private set; }
        public int CopKills { get; private set; }
        public float SinceSeen => Time.time - lastSeen;
        /// <summary>A policeman has had the player in sight since the stars went up (only then can they lose him).</summary>
        public bool Engaged { get; private set; }
        /// <summary>The HUD flashes the stars while the police have lost sight of the player.</summary>
        public bool Searching => Stars >= 2 && Engaged && SinceSeen > 4f;
        public float LastCopAssault { get; private set; } = -100f;
        public float StarsSince { get; private set; }
        public event System.Action<int> StarsChanged;

        // BD Pandey District Hospital and the Mallital police (OSM pois in world.json, local metres)
        public static readonly Vector3 Hospital = new Vector3(-150.18f, 0f, 230.19f);
        public static readonly Vector3 Kotwali = new Vector3(81.96f, 0f, 97.0f);

        float lastSeen = -100f, lastCrime = -100f, aimedAt = -1f;

        public static WantedSystem Ensure()
        {
            if (!Instance) new GameObject("Police").AddComponent<WantedSystem>();
            return Instance;
        }

        void Awake()
        {
            Instance = this;
            if (!GetComponent<PoliceManager>()) gameObject.AddComponent<PoliceManager>();
            if (!GetComponent<IncidentManager>()) gameObject.AddComponent<IncidentManager>();
        }

        // ------------------------------------------------------------------ crimes
        /// <summary>Was this the player (or the car the player drives)?</summary>
        public static bool ByPlayer(GameObject src)
        {
            if (!src) return false;
            var p = PlayerController.Instance;
            if (!p) return false;
            if (src == p.gameObject || src.transform.IsChildOf(p.transform)) return true;
            var car = CarController.Driving;
            return car && (src == car.gameObject || src.transform.IsChildOf(car.transform));
        }

        /// <summary>The player hurt someone (a townsperson, or a policeman: at least 2 stars).</summary>
        public void Assault(Vector3 at, bool cop)
        {
            lastCrime = Time.time;
            if (cop) LastCopAssault = Time.time;
            Raise(cop ? 2 : 1, cop ? "Police pe haath uthaya!" : "Maar-peet! Police aa rahi hai");
        }

        /// <summary>The player killed someone: 2 stars; 3 people (or 2 policemen) this time round: 3 stars.</summary>
        public void Kill(Vector3 at, bool cop)
        {
            lastCrime = Time.time;
            if (cop) { CopKills++; LastCopAssault = Time.time; } else CivKills++;
            int s = CopKills >= 2 || CivKills >= 3 ? 3 : 2;
            Raise(s, s == 3 ? (CopKills >= 2 ? "Do police wale maar diye!" : "Teen logon ka khoon!") : (cop ? "Police wale ka khoon!" : "Khoon ho gaya! Police gaadi aa rahi hai"));
        }

        void Raise(int s, string why)
        {
            if (s <= Stars) { Seen(); return; }
            int before = Stars;
            Stars = Mathf.Clamp(s, 0, 3);
            StarsSince = Time.time;
            Seen();
            StarsChanged?.Invoke(Stars);
            GameEvents.ShowToast(why + "  (" + new string('*', Stars) + ")", 3f);
            Haptics.Warning();
            if (before == 0) { aimedAt = -1f; Engaged = false; }
        }

        /// <summary>The crime was seen (the stars just went up): the clock starts, but nobody is chasing yet.</summary>
        void Seen() => lastSeen = Time.time;

        /// <summary>A policeman can see the player.</summary>
        public void Spotted()
        {
            lastSeen = Time.time;
            if (Stars > 0) Engaged = true;
        }

        /// <summary>A 1-star constable has the player at gunpoint (the warning; the star fades a while after).</summary>
        public void Aimed() { if (aimedAt < 0f) aimedAt = Time.time; }

        public void Clear(string message)
        {
            if (Stars == 0) return;
            Stars = 0;
            Engaged = false;
            CivKills = CopKills = 0;
            aimedAt = -1f;
            StarsChanged?.Invoke(0);
            if (!string.IsNullOrEmpty(message)) GameEvents.ShowToast(message, 3f);
        }

        void Update()
        {
            if (Stars == 0) return;
            var p = PlayerController.Instance;
            if (!p) return;
            if (Stars == 1)
            {
                // the constable came and pointed his gun: stay calm ~12 s and it is over; nobody came: over after 35 s
                bool warned = aimedAt > 0f && Time.time - aimedAt > 12f && Time.time - lastCrime > 12f;
                if (warned || Time.time - lastCrime > 35f) Clear(warned ? "Constable: \"Chal nikal! Agli baar seedha andar.\"" : "Police ne chhod diya");
                return;
            }
            // out of their sight long enough, once they have been on to the player; never found at all: 2 minutes
            float need = Stars == 2 ? 30f : 45f;
            bool lost = Engaged ? SinceSeen > need : Time.time - StarsSince > 120f;
            if (lost && Time.time - lastCrime > need) Clear("Police ne peecha chhod diya");
        }

        // ------------------------------------------------------------------ the end of it
        /// <summary>A policeman got hold of the player: hands up, then the Mallital police and a fine.</summary>
        public void Busted()
        {
            var p = PlayerController.Instance;
            if (!p || Stars == 0 || p.CurrentMode == PlayerController.Mode.Dead) return;
            int fine = Mathf.Min(GameState.Money, Stars == 1 ? 100 : Stars == 2 ? 300 : 600);
            GameState.AddMoney(-fine);
            p.Arrest(Spot(Kotwali));
            GameEvents.ShowBanner("GIRAFTAAR!", "Mallital Kotwali  -  jurmana " + HudStyle.Rupees(fine), HudStyle.Coral);
            Haptics.Error();
            Clear(null);
        }

        /// <summary>PlayerController: the player died. While wanted: the hospital and its bill (true = handled).</summary>
        public bool PlayerDied(PlayerController p)
        {
            if (Stars == 0) return false;
            int bill = Mathf.Min(GameState.Money, 200);
            GameState.AddMoney(-bill);
            p.NextRespawn = Spot(Hospital);
            GameEvents.ShowBanner("BEHOSH!", "BD Pandey Hospital  -  ilaaj ka bill " + HudStyle.Rupees(bill), HudStyle.Coral);
            Clear(null);
            return true;
        }

        /// <summary>A place to stand near a point of interest: the nearest road point to it, on the ground.</summary>
        static Vector3 Spot(Vector3 poi)
        {
            Vector3 at = poi;
            if (RoadRoute.ReadyAny())
            {
                int k = RoadRoute.Nearest(poi, 200f);
                if (k >= 0) at = RoadRoute.Pos(k);
            }
            var t = Terrain.activeTerrain;
            float top = t ? t.SampleHeight(at) + t.transform.position.y : at.y;
            if (Physics.Raycast(new Vector3(at.x, Mathf.Max(at.y, top) + 3f, at.z), Vector3.down, out var hit, 12f, ~((1 << 8) | (1 << 9) | (1 << 10) | (1 << 2) | (1 << 4)), QueryTriggerInteraction.Ignore))
                at.y = hit.point.y;
            else at.y = Mathf.Max(at.y, top);
            return at + Vector3.up * 0.2f;
        }
    }
}
