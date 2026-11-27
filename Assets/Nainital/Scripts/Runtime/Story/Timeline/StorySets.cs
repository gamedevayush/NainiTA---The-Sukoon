using UnityEngine;

namespace Nainital
{
    /// <summary>The meshes and lights of the story's built sets (StorySet): atlas colours, faces turned inwards.</summary>
    public static class StorySets
    {
        /// <summary>The atlas material: the game's (set when it runs), in the editor the generated asset.</summary>
        public static Material AtlasMat
        {
            get
            {
                if (GameAssets.AtlasMaterial) return GameAssets.AtlasMaterial;
#if UNITY_EDITOR
                return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Nainital/Generated/Materials/M_Atlas.mat");
#else
                return null;
#endif
            }
        }

        static GameObject Mesh(Transform parent, string name, MeshBuilder mb, UnityEngine.Rendering.ShadowCastingMode shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = AtlasMat;
            mr.shadowCastingMode = shadows;
            return go;
        }

        static Light Lamp(Transform parent, string name, Vector3 local, float range, float intensity, Color c)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point; l.range = range; l.intensity = intensity; l.color = c; l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>A ward room 5.2 x 6.4 x 3 m: green walls with a band, a frosted bright window, the bed with its sheet and
        /// pillow, a drip stand, a monitor with its green screen, a blue curtain on a rail, the door; a tube light and fills.</summary>
        public static void HospitalRoom(Transform root)
        {
            var mb = new MeshBuilder();
            Vector2 wall = Atlas.SolidUV(Pal.ShirtWhite), floor = Atlas.SolidUV(Pal.StoneGrey), green = Atlas.SolidUV(Pal.JacketGreen),
                    blue = Atlas.SolidUV(Pal.JacketBlue), steel = Atlas.ExtraUV(Px.Chrome), glow = Atlas.SolidUV(Pal.WindowGlow), dark = Atlas.SolidUV(Pal.IronBlack);
            float hx = 2.6f, hz = 3.2f, h = 3.0f;
            mb.Quad(new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz), floor, Vector3.up);
            mb.Quad(new Vector3(-hx, h, hz), new Vector3(hx, h, hz), new Vector3(hx, h, -hz), new Vector3(-hx, h, -hz), wall, Vector3.down);
            mb.Quad(new Vector3(-hx, 0f, hz), new Vector3(-hx, 0f, -hz), new Vector3(-hx, h, -hz), new Vector3(-hx, h, hz), green, Vector3.right);
            mb.Quad(new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), new Vector3(hx, h, hz), new Vector3(hx, h, -hz), green, Vector3.left);
            mb.Quad(new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz), new Vector3(-hx, h, hz), new Vector3(hx, h, hz), wall, Vector3.back);
            mb.Quad(new Vector3(-hx, 0f, -hz), new Vector3(-hx, h, -hz), new Vector3(hx, h, -hz), new Vector3(hx, 0f, -hz), wall, Vector3.forward);
            mb.Box(new Vector3(0f, 1.0f, -hz + 0.01f), new Vector3(2 * hx, 0.12f, 0.02f), green);
            mb.Box(new Vector3(hx - 0.02f, 1.7f, -0.2f), new Vector3(0.03f, 1.3f, 1.8f), glow);
            mb.Box(new Vector3(hx - 0.04f, 1.7f, -0.2f), new Vector3(0.05f, 1.42f, 0.08f), dark);
            mb.Box(new Vector3(0.9f, 1.05f, hz - 0.03f), new Vector3(1.0f, 2.1f, 0.05f), Atlas.SolidUV(Pal.WoodLight));
            mb.Box(new Vector3(0f, 0.32f, -1.9f), new Vector3(1.0f, 0.5f, 2.1f), steel);
            mb.Box(new Vector3(0f, 0.62f, -1.9f), new Vector3(0.95f, 0.14f, 2.05f), wall);
            mb.Box(new Vector3(0f, 0.75f, -2.65f), new Vector3(0.6f, 0.14f, 0.38f), wall);
            mb.Box(new Vector3(0f, 0.72f, -1.45f), new Vector3(0.97f, 0.1f, 1.3f), blue);
            mb.Box(new Vector3(0f, 0.9f, -2.95f), new Vector3(1.0f, 0.9f, 0.06f), steel);
            mb.Cylinder(new Vector3(-0.85f, 0f, -2.4f), 0.02f, 0.02f, 1.95f, 6, steel, true);
            mb.Box(new Vector3(-0.85f, 1.75f, -2.4f), new Vector3(0.16f, 0.26f, 0.05f), Atlas.ExtraUV(Px.CarGlass));
            mb.Box(new Vector3(0.95f, 1.35f, -2.85f), new Vector3(0.5f, 0.38f, 0.22f), dark);
            mb.Box(new Vector3(0.95f, 1.36f, -2.73f), new Vector3(0.42f, 0.28f, 0.01f), Atlas.SolidUV(Pal.LampBulb));
            mb.Box(new Vector3(-1.75f, 1.45f, -1.2f), new Vector3(0.05f, 2.4f, 2.6f), blue);
            mb.Box(new Vector3(-1.75f, 2.7f, -1.2f), new Vector3(0.03f, 0.03f, 3.2f), steel);
            Mesh(root, "Room", mb, UnityEngine.Rendering.ShadowCastingMode.TwoSided);   // the sun stays out
            Lamp(root, "Tube light", new Vector3(0f, h - 0.2f, -0.6f), 10f, 6f, new Color(0.92f, 0.97f, 1f));
            Lamp(root, "Window light", new Vector3(hx - 0.6f, 1.8f, -0.2f), 6f, 3f, new Color(1f, 0.96f, 0.88f));
            Lamp(root, "Fill light", new Vector3(0f, 1.7f, -1.4f), 4f, 1.6f, new Color(1f, 0.97f, 0.93f));
        }

        /// <summary>A car's inside, fitted to the model (its size from VehicleLibrary, the seats from its Vehicle Setup, in
        /// the StoryVehicle's frame: x right, z ahead, y up from the ground): grey fabric seats with headrests (the front
        /// two apart, the back a bench), a dark dashboard with its top and the wheel on the driver's side (right-hand
        /// drive), the console between the front seats, the door panels below the windows, the floor, the roof lining
        /// and the parcel shelf. The model's own panels are one-sided: from inside they were not there.</summary>
        static Vector2 steelUV() => Atlas.ExtraUV(Px.Chrome);

        public static void CarInside(Transform root, StoryVehicle v)
        {
            if (!v) return;
            v.Ensure();
            var vp = v.Parts;
            Vector3 size = new Vector3(1.75f, 1.48f, 4.4f);
            var lib = VehicleLibrary.Instance;
            int idx = string.IsNullOrEmpty(v.model) ? -1 : PoliceManager.ModelIndex(v.model);
            if (lib && idx >= 0 && lib.entries[idx].size.sqrMagnitude > 0.1f) size = lib.entries[idx].size;
            // the seats in the vehicle's frame
            var seats = new System.Collections.Generic.List<(Vector3 p, bool drive)>();
            if (vp && vp.HasSetup)
                for (int i = 0; i < vp.SeatCount; i++)
                    seats.Add((v.transform.InverseTransformPoint(vp.Frame.TransformPoint(vp.SeatData(i).pos)), vp.PoseOf(i) == SeatPose.Drive));
            if (seats.Count == 0)
            {
                seats.Add((new Vector3(0.4f, 0f, 0.1f), true)); seats.Add((new Vector3(-0.4f, 0f, 0.1f), false));
                seats.Add((new Vector3(0.45f, 0f, -0.75f), false)); seats.Add((new Vector3(-0.45f, 0f, -0.75f), false));
            }
            float frontZ = float.MinValue, backZ = float.MaxValue, driverX = 0.4f;
            foreach (var st in seats) { frontZ = Mathf.Max(frontZ, st.p.z); backZ = Mathf.Min(backZ, st.p.z); if (st.drive) driverX = st.p.x; }
            // inside the shell: the library width has the mirrors in it, the roof curves down to the sides
            float hw = size.x * 0.5f - 0.2f, roof = size.y - 0.22f, floorY = 0.07f;   // the floor low: the sitters' feet are at the ground's height
            Debug.Log($"[Nainital] car inside: {v.model} size {size}, {seats.Count} seats, front z {frontZ:F2}, back z {backZ:F2}, driver x {driverX:F2}");
            Vector2 fabric = Atlas.SolidUV(Pal.StoneGrey), dark = Atlas.SolidUV(Pal.IronBlack), panel = Atlas.SolidUV(Pal.Concrete),
                    lining = Atlas.SolidUV(Pal.ShirtWhite), floor = Atlas.SolidUV(Pal.PantsBlack), trim = Atlas.SolidUV(Pal.WoodDark);
            var mb = new MeshBuilder();
            Vector2 seat = Atlas.SolidUV(Pal.PantsKhaki), seam = Atlas.SolidUV(Pal.WoodDark), post = steelUV();
            // a seat: the cushion with its front lip and side bolsters, the backrest with its bolsters and stitched rows,
            // headrests on their two posts, the frame under it (owner: "seat kyun nhi bnaayi" - shaped seats, beige)
            var lean = Quaternion.Euler(-12f, 0f, 0f);   // backrests lean back
            void Seat(Vector3 c, float w, int heads)
            {
                mb.Box(new Vector3(c.x, 0.42f, c.z + 0.04f), new Vector3(w, 0.11f, 0.5f), seat);
                mb.Box(new Vector3(c.x, 0.44f, c.z + 0.3f), new Vector3(w, 0.09f, 0.08f), Quaternion.Euler(-12f, 0f, 0f), seat);
                mb.Box(new Vector3(c.x, 0.3f, c.z + 0.04f), new Vector3(w * 0.86f, 0.13f, 0.46f), dark);
                var backC = new Vector3(c.x, 0.8f, c.z - 0.24f);
                mb.Box(backC, new Vector3(w, 0.62f, 0.12f), lean, seat);
                foreach (float sx in new[] { -1f, 1f })
                {
                    mb.Box(new Vector3(c.x + sx * (w * 0.5f - 0.05f), 0.49f, c.z + 0.04f), new Vector3(0.1f, 0.07f, 0.48f), seat);
                    mb.Box(backC + lean * new Vector3(sx * (w * 0.5f - 0.05f), 0f, 0.04f), new Vector3(0.1f, 0.56f, 0.12f), lean, seat);
                }
                for (int k = 0; k < 3; k++)
                    mb.Box(backC + lean * new Vector3(0f, -0.18f + k * 0.17f, 0.062f), new Vector3(w - 0.2f, 0.012f, 0.004f), lean, seam);
                mb.Box(new Vector3(c.x, 0.477f, c.z + 0.04f), new Vector3(w - 0.2f, 0.004f, 0.012f), seam);
                for (int h = 0; h < heads; h++)
                {
                    float hx = heads == 1 ? c.x : c.x + (h - (heads - 1) * 0.5f) * (w / heads);
                    var hc = new Vector3(hx, 1.22f, c.z - 0.33f);
                    mb.Box(hc, new Vector3(0.26f, 0.19f, 0.1f), lean, seat);
                    foreach (float px in new[] { -0.07f, 0.07f }) mb.Box(hc + lean * new Vector3(px, -0.14f, 0f), new Vector3(0.015f, 0.1f, 0.015f), lean, post);
                }
            }
            foreach (var st in seats)
                if (st.p.z >= frontZ - 0.35f) Seat(st.p, 0.5f, 1);
            if (backZ < frontZ - 0.35f)
            {
                Seat(new Vector3(0f, 0f, backZ), 2f * hw - 0.08f, 3);
                mb.Box(new Vector3(0f, roof - 0.42f, backZ - 0.62f), new Vector3(2f * hw - 0.08f, 0.03f, 0.42f), dark);   // the parcel shelf
            }
            // the instruments behind the wheel and the air vents
            mb.Box(new Vector3(driverX, 1.07f, frontZ + 0.6f), new Vector3(0.38f, 0.1f, 0.2f), dark);
            mb.Box(new Vector3(driverX, 1.03f, frontZ + 0.505f), new Vector3(0.3f, 0.08f, 0.005f), Atlas.SolidUV(Pal.LampBulb));
            foreach (float vx in new[] { -0.12f, 0.12f }) mb.Box(new Vector3(vx, 0.93f, frontZ + 0.52f), new Vector3(0.16f, 0.05f, 0.02f), steelUV());
            // the dashboard: its body, its top under the windscreen, a trim strip; the console between the front seats
            float dz = frontZ + 0.68f;
            mb.Box(new Vector3(0f, 0.86f, dz), new Vector3(2f * hw, 0.3f, 0.32f), dark);
            mb.Box(new Vector3(0f, 1.02f, dz + 0.02f), new Vector3(2f * hw - 0.2f, 0.05f, 0.3f), Quaternion.Euler(-8f, 0f, 0f), dark);
            mb.Box(new Vector3(0f, 0.9f, dz - 0.215f), new Vector3(2f * hw - 0.1f, 0.04f, 0.01f), trim);
            mb.Box(new Vector3(0f, 0.55f, dz - 0.02f), new Vector3(2f * hw, 0.42f, 0.24f), dark);
            mb.Box(new Vector3(0f, 0.52f, frontZ - 0.05f), new Vector3(0.22f, 0.32f, 0.75f), dark);
            mb.Box(new Vector3(0f, 0.7f, frontZ + 0.42f), new Vector3(0.24f, 0.36f, 0.3f), dark);   // the gear / music box
            // the door panels (below the windows), the floor, the roof lining
            float z0 = backZ - 0.5f, z1 = frontZ + 0.55f;
            foreach (float sx in new[] { -1f, 1f })
            {
                mb.Box(new Vector3(sx * hw, 0.63f, (z0 + z1) * 0.5f), new Vector3(0.04f, 0.6f, z1 - z0), panel);
                mb.Box(new Vector3(sx * (hw - 0.05f), 0.76f, (z0 + z1) * 0.5f), new Vector3(0.08f, 0.04f, z1 - z0 - 0.3f), dark);   // the armrest line
            }
            mb.Box(new Vector3(0f, floorY, (z0 + dz) * 0.5f), new Vector3(2f * hw, 0.03f, dz - z0), floor);
            mb.Box(new Vector3(0f, roof, (backZ - 0.4f + frontZ + 0.3f) * 0.5f), new Vector3(2f * hw - 0.3f, 0.025f, frontZ - backZ + 0.7f), lining);
            // the underbody, full length (an upturned car shows its floor: no feet through it)
            mb.Box(new Vector3(0f, 0.02f, 0f), new Vector3(2f * hw + 0.12f, 0.03f, size.z - 0.7f), dark);
            Mesh(root, "Car inside", mb, UnityEngine.Rendering.ShadowCastingMode.On);
            // the steering wheel on its column, in front of the driver, tilted towards him
            var wheel = new MeshBuilder();
            wheel.Cylinder(new Vector3(0f, -0.015f, 0f), 0.19f, 0.19f, 0.03f, 20, dark, true, true);
            wheel.Cylinder(new Vector3(0f, -0.2f, 0f), 0.03f, 0.03f, 0.2f, 8, dark, true, false);
            var w0 = Mesh(root, "Steering wheel", wheel, UnityEngine.Rendering.ShadowCastingMode.On);
            w0.transform.localPosition = new Vector3(driverX, 0.98f, frontZ + 0.45f);
            w0.transform.localRotation = Quaternion.Euler(-62f, 0f, 0f);
        }

        /// <summary>The 108's back seen from inside (the model has no inside): white walls, a red stripe, the stretcher,
        /// a cabinet, the back door's window open to the road behind; a cold light.</summary>
        public static void AmbulanceInside(Transform root)
        {
            var mb = new MeshBuilder();
            Vector2 wall = Atlas.SolidUV(Pal.ShirtWhite), floor = Atlas.SolidUV(Pal.StoneGrey), steel = Atlas.ExtraUV(Px.Chrome), red = Atlas.SolidUV(Pal.TempleRed);
            float w = 0.85f, y0 = 0.62f, y1 = 2.25f, z0 = -2.45f, z1 = 0.55f;
            mb.Quad(new Vector3(-w, y0, z0), new Vector3(w, y0, z0), new Vector3(w, y0, z1), new Vector3(-w, y0, z1), floor, Vector3.up);
            mb.Quad(new Vector3(-w, y1, z1), new Vector3(w, y1, z1), new Vector3(w, y1, z0), new Vector3(-w, y1, z0), wall, Vector3.down);
            mb.Quad(new Vector3(-w, y0, z1), new Vector3(-w, y0, z0), new Vector3(-w, y1, z0), new Vector3(-w, y1, z1), wall, Vector3.right);
            mb.Quad(new Vector3(w, y0, z0), new Vector3(w, y0, z1), new Vector3(w, y1, z1), new Vector3(w, y1, z0), wall, Vector3.left);
            mb.Quad(new Vector3(w, y0, z1), new Vector3(-w, y0, z1), new Vector3(-w, y1, z1), new Vector3(w, y1, z1), wall, Vector3.back);
            float wx = 0.45f, wy0 = 1.15f, wy1 = 1.75f;
            mb.Quad(new Vector3(-w, y0, z0), new Vector3(-w, y1, z0), new Vector3(-wx, y1, z0), new Vector3(-wx, y0, z0), wall, Vector3.forward);
            mb.Quad(new Vector3(wx, y0, z0), new Vector3(wx, y1, z0), new Vector3(w, y1, z0), new Vector3(w, y0, z0), wall, Vector3.forward);
            mb.Quad(new Vector3(-wx, y0, z0), new Vector3(-wx, wy0, z0), new Vector3(wx, wy0, z0), new Vector3(wx, y0, z0), wall, Vector3.forward);
            mb.Quad(new Vector3(-wx, wy1, z0), new Vector3(-wx, y1, z0), new Vector3(wx, y1, z0), new Vector3(wx, wy1, z0), wall, Vector3.forward);
            mb.Box(new Vector3(-w + 0.01f, 1.35f, (z0 + z1) * 0.5f), new Vector3(0.01f, 0.08f, z1 - z0), red);
            mb.Box(new Vector3(0.15f, 0.95f, -0.9f), new Vector3(0.62f, 0.08f, 1.9f), wall);
            mb.Box(new Vector3(0.15f, 0.78f, -0.9f), new Vector3(0.55f, 0.3f, 1.7f), steel);
            mb.Box(new Vector3(-w + 0.22f, 1.5f, -1.2f), new Vector3(0.4f, 0.9f, 1.2f), wall);
            Mesh(root, "Inside", mb, UnityEngine.Rendering.ShadowCastingMode.Off);
            Lamp(root, "Inside light", new Vector3(0f, y1 - 0.15f, -0.8f), 3.5f, 1.6f, new Color(0.85f, 0.95f, 1f));
        }
    }
}
