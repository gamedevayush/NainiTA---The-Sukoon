using UnityEngine;

namespace Nainital
{
    /// <summary>Tests only (VehicleTest.BikeBus in the editor; an editor-folder script cannot be added in Play): rides a
    /// BikeRide at `aim` at `speed` every physics step, the way TrafficBike / PlayerBike call Drive.</summary>
    public class BikeTestDriver : MonoBehaviour
    {
        public Vector3 aim;
        public float speed;
        BikeRide bike;

        void FixedUpdate()
        {
            if (!bike) bike = GetComponent<BikeRide>();
            if (bike && !bike.Crashed) bike.Drive(aim, speed, Time.fixedDeltaTime);
        }
    }
}
