using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The new vehicles (Vehicles/New, exported from NewVehicles_vNN.blend by Tools/newvehicles/export_unity.py): Blender
    /// keeps object names unique across the whole file, so the parts arrive as Body.003, Wheel_FL.007, Handlebar.002...
    /// The numeric suffix is dropped on import, so every vehicle has plain Body / Wheel_FL / Wheel_FR / Wheel_R /
    /// Wheel_F / Handlebar / Door_FL / Door_FR / Steering parts for the Vehicle Setup tool and the game (2026-11-20).
    /// </summary>
    class NewVehicleImport : AssetPostprocessor
    {
        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.Replace('\\', '/').StartsWith(VehicleSetupWindowPaths.NewDir + "/")) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root.transform) continue;
                int dot = t.name.LastIndexOf('.');
                if (dot > 0 && int.TryParse(t.name.Substring(dot + 1), out _)) t.name = t.name.Substring(0, dot);
            }
        }
    }

    static class VehicleSetupWindowPaths
    {
        public const string NewDir = "Assets/Nainital/Vehicles/New";
    }
}
