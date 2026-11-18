using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// What the police and the 108 ambulance are made of (Resources/PoliceKit, built by Nainital > Police kit,
    /// Editor/PoliceKitBuilder): the lamp materials of the roof light bars, the "UTTARAKHAND POLICE" / "108 AMBULANCE"
    /// lettering on the vehicle sides (solid 3D letters, Glyph3D, atlas colours) and where the roof is on each model.
    /// The people are in the CharacterLibrary (police_1, police_2: uniformed, never in the crowd), the vehicles in the
    /// VehicleLibrary (Bolero for the police, Ambulance108, the Splendor bike).
    /// </summary>
    public class PoliceKit : ScriptableObject
    {
        public Material lampRed, lampBlue, lampWhite;   // unlit HDR, lit by EmergencyLights
        public Material muzzle;                         // additive puff for the pistol's flash
        public Mesh policeLetters;                      // both sides of the police Bolero
        public Mesh ambulanceLetters;                   // both sides of the ambulance
        public Vector3 policeRoof = new Vector3(0f, 1.95f, -0.1f);      // top of the roof, model space (light bar sits here)
        public Vector3 ambulanceRoof = new Vector3(0f, 2.75f, 1.6f);
        public float policeRoofWidth = 1.3f, ambulanceRoofWidth = 1.6f;
        public string policeCarModel = "Bolero", ambulanceModel = "Ambulance108", bikeModel = "Splendor";
        public string[] officerIds = { "police_1", "police_2" };
        public string[] medicIds = { "m_man_flannel", "goan_kuli", "goan_mistri", "goan_kisaan" };

        static PoliceKit cached;
        static bool looked;

        public static PoliceKit Instance
        {
            get
            {
                if (!looked) { cached = Resources.Load<PoliceKit>("PoliceKit"); looked = true; }
                return cached;
            }
        }
    }
}
