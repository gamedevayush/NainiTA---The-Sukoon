using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>A police vehicle the constables ride in and get out of (PoliceCar: the Bolero, PoliceBike: the Splendor).</summary>
    public abstract class PoliceRide : MonoBehaviour
    {
        public readonly List<PoliceOfficer> crew = new List<PoliceOfficer>();
        public EmergencyLights lights;

        /// <summary>Where seat n is (vehicle space: the rider's root).</summary>
        public abstract Vector3 SeatLocal(int seat);
        /// <summary>Where the man in seat n gets in and out (world).</summary>
        public abstract Vector3 Door(int seat);
        /// <summary>Can still be driven (not burnt out, not lying in the road).</summary>
        public abstract bool Usable { get; }
        public virtual bool Bike => false;
        /// <summary>What a seated man is parented to (a bike's leaning body, so the rider leans with it).</summary>
        public virtual Transform SeatParent => transform;

        public bool AllAboard
        {
            get
            {
                foreach (var c in crew) if (c && !c.Dead && !c.Seated) return false;
                return true;
            }
        }

        public int Living
        {
            get
            {
                int n = 0;
                foreach (var c in crew) if (c && !c.Dead) n++;
                return n;
            }
        }

        public bool AnyAboard
        {
            get
            {
                foreach (var c in crew) if (c && !c.Dead && c.Seated) return true;
                return false;
            }
        }

        /// <summary>Everyone seated gets out.</summary>
        public void Unload()
        {
            foreach (var c in crew) if (c && !c.Dead && c.Seated) c.GetOut();
        }

        /// <summary>Somebody seated who can shoot from here (the passenger in a car, the rider on a bike).</summary>
        public PoliceOfficer Shooter
        {
            get
            {
                PoliceOfficer best = null;
                foreach (var c in crew)
                    if (c && !c.Dead && c.Seated && (Bike || c.seat == 1 || best == null)) best = c;
                return best;
            }
        }

        protected virtual void OnDestroy()
        {
            // anyone still inside goes with the vehicle
            foreach (var c in crew) if (c && c.Seated) Destroy(c.gameObject);
        }
    }
}
