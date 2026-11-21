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
        /// <summary>In the game (21 Nov 2026) the set is the town's traffic: 25-31k triangles a vehicle, so the importer
        /// makes mesh LODs (Unity 6 Mesh LOD: the renderer drops to a coarser index buffer as the vehicle gets small on
        /// screen; no LOD Group).</summary>
        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(VehicleSetupWindowPaths.NewDir + "/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.generateMeshLods = true;
            // a knock dents the body in the game (VehicleParts.DentMesh copies and moves its vertices): the mesh must stay
            // readable in the build (owner, 21 Nov 2026: "takkar par hi dent")
            mi.isReadable = true;
        }

        /// <summary>Phones and tablets: the 2K textures at 1K (twenty vehicles' colour / normal / ORM maps).</summary>
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(VehicleSetupWindowPaths.NewDir + "/Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var s = ti.GetPlatformTextureSettings(platform);
                if (s.overridden && s.maxTextureSize == 1024) continue;
                s.overridden = true;
                s.maxTextureSize = 1024;
                s.format = TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(s);
            }
        }

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
