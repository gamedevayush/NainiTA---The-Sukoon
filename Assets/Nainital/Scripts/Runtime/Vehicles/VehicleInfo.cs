using UnityEngine;

namespace Nainital
{
    /// <summary>Real size and wheel radius of a library vehicle (VehicleFactory.CreateModel), read by CarController and
    /// TrafficCar in place of the generic numbers for the vehicle's kind.</summary>
    public class VehicleInfo : MonoBehaviour
    {
        public float wheelRadius = 0.35f, length = 4.4f, width = 1.8f, height = 1.8f;
    }
}
