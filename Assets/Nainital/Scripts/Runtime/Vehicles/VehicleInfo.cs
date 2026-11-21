using UnityEngine;

namespace Nainital
{
    /// <summary>Real size and wheel radius of a library vehicle (VehicleFactory.CreateModel), read by CarController and
    /// TrafficCar in place of the generic numbers for the vehicle's kind.</summary>
    public class VehicleInfo : MonoBehaviour
    {
        public float wheelRadius = 0.35f, length = 4.4f, width = 1.8f, height = 1.8f;
        /// <summary>The library model's name, where its axles are (vehicle space z) and the body's width without the
        /// mirrors (from the tyres' track): VehicleSeats places the driver from these.</summary>
        public string model;
        public float frontAxleZ, rearAxleZ, bodyWidth;
        /// <summary>The model's own top speed (m/s) and mass (kg), 0 = the kind's (VehicleLibrary.Entry).</summary>
        public float maxSpeed, mass;
    }
}
