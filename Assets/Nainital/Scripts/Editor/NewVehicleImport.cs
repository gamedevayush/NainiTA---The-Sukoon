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

        static string Plain(string path)
        {
            var parts = path.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                int dot = parts[i].LastIndexOf('.');
                if (dot > 0 && int.TryParse(parts[i].Substring(dot + 1), out _)) parts[i] = parts[i].Substring(0, dot);
            }
            return string.Join("/", parts);
        }

        /// <summary>The same for the clip the FBX carries (the buses' door animation, 21 Nov): its curves still name
        /// Door_FR.001 etc., which no longer exist once the parts are renamed above.</summary>
        void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!assetPath.Replace('\\', '/').StartsWith(VehicleSetupWindowPaths.NewDir + "/")) return;
            var bindings = AnimationUtility.GetCurveBindings(clip);
            bool any = false;
            foreach (var b in bindings) if (Plain(b.path) != b.path) { any = true; break; }
            if (!any) return;
            var curves = new AnimationCurve[bindings.Length];
            for (int i = 0; i < bindings.Length; i++) curves[i] = AnimationUtility.GetEditorCurve(clip, bindings[i]);
            clip.ClearCurves();
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                b.path = Plain(b.path);
                AnimationUtility.SetEditorCurve(clip, b, curves[i]);
            }
        }
    }

    static class VehicleSetupWindowPaths
    {
        public const string NewDir = "Assets/Nainital/Vehicles/New";
    }
}
