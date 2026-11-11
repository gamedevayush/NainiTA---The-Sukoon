using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// Tourist / local: walks a path (Mall Road, sidewalks) or idles/sits at a spot (temple, bench, stall).
    /// Can be talked to; gets knocked over by careless drivers and stands up again.
    ///
    /// Street sense (user, 2026-11-11: "crowd aapas mein phans ke ruk jaate hain; gaadi aa rahi ho toh bhaag ke cross kar
    /// lenge"): walkers keep to the left of the line they follow, so people going opposite ways pass each other; they
    /// step aside for someone right in front (and walkers do not physically block each other: PedestrianManager turns off
    /// walker-walker collisions); anyone standing in the path of a moving car runs out of its way to the nearer side.
    /// At night: sent home by PedestrianManager they walk to the door of the nearest house and go in; a local at a post
    /// (homeAtNight) is gone from midnight till six.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public class NpcWalker : Interactable
    {
        public Vector3[] path;
        public int index;
        public int direction = 1;
        public float speed = 1.2f;
        public string npcName = "Tourist";
        public bool sit;              // idle seated (bench / temple)
        public bool idle;             // idle standing (shopkeeper, boatman)
        public Vector3 faceDirection; // for idle NPCs
        [TextArea] public string[] lines;
        public bool homeAtNight;      // a local at a post: not there from midnight till six
        public float laneOffset = -1f;   // how far left of the path line this walker keeps (set at random if < 0)

        public bool GoingHome { get; private set; }
        public bool Home { get; private set; }   // at the door: PedestrianManager takes this walker away

        CharacterMotor motor;
        HumanoidRig rig;
        CharacterController cc;
        float pause, talkTimer, knockTimer, scanTimer, fleeTimer, sideTimer, homeTimer;
        Vector3 fleeDir, sideStep, homeDoor, lastDir;
        bool hidden;
        Health health;
        static readonly Collider[] hits = new Collider[16];

        static readonly string[] DefaultLines =
        {
            "Bhaiya, Naina Devi mandir yahan se seedha Mallital ki taraf hai.",
            "Shaam ko Mall Road pe lights dekhna, jheel mein reflection kamaal ka lagta hai!",
            "Boating Tallital aur Mallital dono jagah se hoti hai.",
            "Snow View tak ropeway jaata hai, upar se Himalaya dikhta hai.",
            "Thandi Sadak pe shaam ko walk karna, bahut shanti hai.",
            "Bada Bazar mein candles aur woollen kapde zaroor dekhna.",
            "Tiffin Top (Dorothy's Seat) tak trek karo, view mast hai.",
            "Raat mein gali-mohallon mein sambhal ke, kuch gunde ghoomte hain!",
            "Momos khaye? Mall Road pe best milte hain.",
            "Yeh jheel aankh ki shape ki hai, isliye Naini naam pada.",
            "Parking ki bahut dikkat hai yahan, gaadi Tallital pe hi laga do.",
            "Tibetan market mein sasti jackets milti hain, dekh lena.",
            "Bandaron se bach ke, chips cheen ke bhaag jaate hain!",
            "Season mein yahan itni bheed hoti hai ki Mall Road pe chalna mushkil.",
        };

        public override string Prompt => npcName + " se baat karo";

        void Awake()
        {
            motor = GetComponent<CharacterMotor>();
            rig = GetComponentInChildren<HumanoidRig>();
            health = GetComponent<Health>();
            cc = GetComponent<CharacterController>();
            pause = Random.Range(0f, 3f);
            if (laneOffset < 0f) laneOffset = Random.Range(0.35f, 0.9f);
            scanTimer = Random.Range(0f, 0.25f);
        }

        public override bool CanInteract(PlayerController p) => (!health || !health.IsDead) && knockTimer <= 0f && !hidden;

        public override void Interact(PlayerController p)
        {
            var src = lines != null && lines.Length > 0 ? lines : DefaultLines;
            GameEvents.ShowDialog(npcName, src[Random.Range(0, src.Length)]);
            talkTimer = 5f;
        }

        public void Knock(Vector3 impulse)
        {
            if (knockTimer > 0f) return;
            knockTimer = 3.5f;
            if (rig) { rig.dead = true; }
            if (motor) motor.impulse += impulse;
            string[] shouts = { "Arey bhai dekh ke chala!", "Oye! Andha hai kya?", "Police bulaunga!", "Hai Ram!" };
            GameEvents.ShowToast(npcName + ": \"" + shouts[Random.Range(0, shouts.Length)] + "\"", 2f);
        }

        /// <summary>Walk to this door and go in (PedestrianManager, at night).</summary>
        public void GoHome(Vector3 door)
        {
            GoingHome = true;
            Home = false;
            homeDoor = door;
            homeTimer = 0f;
            idle = false;
            sit = false;
        }

        /// <summary>Fresh state for a walker taken from the pool.</summary>
        public void ResetState()
        {
            GoingHome = false;
            Home = false;
            fleeTimer = sideTimer = 0f;
            talkTimer = 0f;
        }

        void SetHidden(bool h)
        {
            hidden = h;
            if (rig && rig.gameObject.activeSelf == h) rig.gameObject.SetActive(!h);
            if (cc) cc.enabled = !h;
        }

        /// <summary>A moving vehicle about to reach this spot: run to the nearer side of its path.</summary>
        void ScanTraffic()
        {
            Vector3 pos = transform.position;
            int n = Physics.OverlapSphereNonAlloc(pos, 18f, hits, 1 << VehicleFactory.VehicleLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = hits[i];
                var rb = col.attachedRigidbody;
                Transform car = rb ? rb.transform : col.transform;
                Vector3 v = Vector3.zero;
                var tc = car.GetComponent<TrafficCar>();
                if (tc) v = car.forward * tc.Speed;
                else if (rb && !rb.isKinematic) v = rb.linearVelocity;
                v.y = 0f;
                float sp = v.magnitude;
                if (sp < 1.5f) continue;
                Vector3 fwd = v / sp;
                Vector3 rel = pos - car.position;
                rel.y = 0f;
                float ahead = Vector3.Dot(rel, fwd);
                if (ahead < -1.5f || ahead > 4f + sp * 2.2f) continue;   // behind it, or more than ~2 s away
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                float lat = Vector3.Dot(rel, right);
                var info = car.GetComponent<VehicleInfo>();
                float half = (info ? info.width : 1.8f) * 0.5f + 1.2f;
                if (Mathf.Abs(lat) > half) continue;
                fleeDir = right * (lat >= 0f ? 1f : -1f);
                fleeTimer = Mathf.Max(fleeTimer, 1.2f);
                talkTimer = 0f;
                return;
            }
        }

        /// <summary>Someone right in front, coming the other way or standing: step to the left for a moment.</summary>
        void ScanCrowd()
        {
            if (lastDir.sqrMagnitude < 0.01f) return;
            Vector3 pos = transform.position;
            int n = Physics.OverlapSphereNonAlloc(pos + lastDir * 0.9f + Vector3.up * 0.9f, 0.55f, hits, (1 << 9) | (1 << 8), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (hits[i].transform == transform || hits[i].transform.IsChildOf(transform)) continue;
                sideStep = Vector3.Cross(lastDir, Vector3.up);   // left of the way we walk
                sideTimer = 0.7f;
                return;
            }
        }

        void Update()
        {
            if (!rig) rig = GetComponentInChildren<HumanoidRig>(true);
            if (!rig) return;
            float dt = Time.deltaTime;
            if (homeAtNight)
            {
                bool away = !TownClock.LocalsOut(TownClock.Hour);
                if (away != hidden) SetHidden(away);
                if (away) return;
            }
            if (health && health.IsDead) { rig.dead = true; rig.speed = 0; return; }
            if (knockTimer > 0f)
            {
                knockTimer -= dt;
                motor.Move(Vector3.zero, 6f, dt);
                if (knockTimer <= 0f) rig.Revive();
                return;
            }
            if (!sit)
            {
                scanTimer -= dt;
                if (scanTimer <= 0f) { scanTimer = 0.25f; ScanTraffic(); ScanCrowd(); }
            }

            Vector3 desired = Vector3.zero;
            Vector3 face = transform.forward;
            float run = 1f;
            if (fleeTimer > 0f)   // out of a car's way first, whatever else
            {
                fleeTimer -= dt;
                desired = fleeDir * 3.8f;
                face = fleeDir;
            }
            else if (GoingHome)
            {
                Vector3 to = homeDoor - transform.position;
                to.y = 0f;
                homeTimer += dt;
                if (to.magnitude < 1.3f || homeTimer > 45f) { Home = true; return; }
                desired = to.normalized * speed * 1.15f;
                face = to;
            }
            else if (talkTimer > 0f)
            {
                talkTimer -= dt;
                var p = PlayerController.Instance;
                if (p) face = p.transform.position - transform.position;
            }
            else if (sit || idle)
            {
                if (faceDirection.sqrMagnitude > 0.01f) face = faceDirection;
            }
            else if (path != null && path.Length > 1)
            {
                if (pause > 0f) pause -= dt;
                else
                {
                    index = Mathf.Clamp(index, 0, path.Length - 1);
                    Vector3 to = path[index] - transform.position;
                    to.y = 0;
                    if (to.magnitude < 1.2f + laneOffset)
                    {
                        index += direction;
                        if (index >= path.Length || index < 0)
                        {
                            direction = -direction;
                            index = Mathf.Clamp(index + 2 * direction, 0, path.Length - 1);
                        }
                        if (Random.value < 0.06f) pause = Random.Range(2f, 6f);
                        to = path[index] - transform.position;
                        to.y = 0f;
                    }
                    // keep left of the line (India): people walking the other way pass on the other side
                    Vector3 d = to.sqrMagnitude > 0.001f ? to.normalized : transform.forward;
                    Vector3 left = Vector3.Cross(d, Vector3.up);
                    Vector3 aim = to + left * laneOffset;
                    desired = aim.normalized * speed;
                    face = aim;
                }
            }
            if (sideTimer > 0f && fleeTimer <= 0f && desired.sqrMagnitude > 0.01f)
            {
                sideTimer -= dt;
                desired = (desired.normalized + sideStep * 0.9f).normalized * desired.magnitude;
            }
            if (desired.sqrMagnitude > 0.01f) lastDir = new Vector3(desired.x, 0f, desired.z).normalized;
            if (!sit) motor.Move(desired * run, 6f, dt);
            motor.FaceTowards(face, fleeTimer > 0f ? 720f : 300f, dt);
            rig.speed = sit ? 0f : motor.HorizontalVelocity.magnitude;
            rig.seated = sit;
            rig.grounded = true;
        }
    }
}
