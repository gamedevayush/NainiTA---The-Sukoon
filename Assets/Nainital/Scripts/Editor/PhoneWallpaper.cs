using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// The phone's wallpapers are the game's own Nainital (owner, 22 Nov 2026: "phone me nainital ka wallpaper"): a portrait
    /// 1080 x 2340 render from the play camera. Bridge (in Play): arg.txt "name;x,h,z;lx,ly,lz;hour;fov" (h = metres over the
    /// ground; look point absolute, its y over the ground too), then "call PhoneWallpaper.Render" -> Shots/wall_name.png.
    /// Tools/phone/make_phone_art.py turns them into Resources/Phone/wall_*.jpg (+ a blurred copy for the glass).
    /// </summary>
    public static class PhoneWallpaper
    {
        public static void Render()
        {
            if (!EditorApplication.isPlaying) throw new System.Exception("Play first (PlayTest.Begin)");
            string arg = File.ReadAllText(Path.Combine(Application.dataPath, "../Bridge/arg.txt")).Trim();
            var host = new GameObject("WallpaperShot").AddComponent<Runner>();
            host.StartCoroutine(host.Go(arg));
        }

        class Runner : MonoBehaviour
        {
            public IEnumerator Go(string arg)
            {
                var p = arg.Split(';');
                string name = p[0];
                // a y ending in 'a' is absolute (over the lake the ground is its bed), else metres over the ground
                float G(Vector3 v) { var t = Terrain.activeTerrain; return t ? t.SampleHeight(v) + t.transform.position.y : 0f; }
                Vector3 V(string s)
                {
                    var a = s.Split(',');
                    bool abs = a[1].EndsWith("a");
                    var v = new Vector3(float.Parse(a[0]), float.Parse(a[1].TrimEnd('a')), float.Parse(a[2]));
                    if (!abs) v.y += G(v);
                    return v;
                }
                Vector3 pos = V(p[1]);
                Vector3 look = V(p[2]);
                float hour = p.Length > 3 ? float.Parse(p[3]) : 17.5f;
                float fov = p.Length > 4 ? float.Parse(p[4]) : 62f;
                var dn = DayNightCycle.Instance;
                if (dn) dn.timeOfDay = hour;
                var cam = Camera.main;
                var tpc = ThirdPersonCamera.Instance;
                if (tpc) tpc.enabled = false;
                if (HUD.Instance) HUD.Instance.SetAlpha(0f);
                // the player stands where the camera is, so the town streams / lamps pool around the view
                var pl = PlayerController.Instance;
                if (pl)
                {
                    var cc = pl.GetComponent<CharacterController>();
                    if (cc) cc.enabled = false;
                    pl.transform.position = new Vector3(pos.x, G(pos) + 0.2f, pos.z) - (look - pos).normalized * 3f;
                    if (cc) cc.enabled = true;
                }
                for (int i = 0; i < 45; i++)
                {
                    cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos));
                    yield return null;
                }
                const int W = 1080, H = 2340;
                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                float oldFov = cam.fieldOfView;
                cam.fieldOfView = fov;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                cam.fieldOfView = oldFov;
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                rt.Release();
                string outp = Path.GetFullPath(Path.Combine(Application.dataPath, "../Shots/wall_" + name + ".png"));
                File.WriteAllBytes(outp, tex.EncodeToPNG());
                Debug.Log("[Nainital] wallpaper -> " + outp);
                if (tpc) tpc.enabled = true;
                if (HUD.Instance) HUD.Instance.SetAlpha(1f);
                Destroy(gameObject);
            }
        }
    }
}
