using UnityEngine;

namespace Nainital
{
    /// <summary>Boots runtime systems (input, HUD) and applies per-platform quality.</summary>
    [DefaultExecutionOrder(-200)]
    public class GameManager : MonoBehaviour
    {
        public Material atlasMaterial;
        public Material markerMaterial;
        public Material rainMaterial;
        public Texture2D minimap;
        public float worldSize = 4000f;
        public float worldCx, worldCz;   // the map square's centre
        public string[] blipNames;
        public Vector3[] blipPositions;
        public float pcFogEnd = 7000f, mobileFogEnd = 2000f;   // PC sees the far ridges (WorldBuilder.FarRidges)

        void Awake()
        {
            GameAssets.AtlasMaterial = atlasMaterial;
            GameAssets.MarkerMaterial = markerMaterial;
            GameAssets.RainMaterial = rainMaterial;
            GameAssets.Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameState.Load();
            Time.timeScale = 1f;
            Minimap.MapTexture = minimap;
            Minimap.WorldSize = worldSize;
            Minimap.WorldCx = worldCx;
            Minimap.WorldCz = worldCz;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;
            GameSettings.ApplyAll();   // volume, 30/60 fps (the graphics again once the HUD and the world are up)

            bool mobile = Application.isMobilePlatform;
            RenderSettings.fogEndDistance = mobile ? mobileFogEnd : pcFogEnd;
            RenderSettings.fogStartDistance = mobile ? 300f : 1200f;  // the atmosphere pass does the aerial perspective
            var cam = Camera.main;
            if (cam) cam.farClipPlane = (mobile ? mobileFogEnd : pcFogEnd) + 200f;

            // Phones: the terrain drawn from a mesh Unity builds on the CPU, in full float, from the same heights the
            // TerrainCollider uses (user, iPhone 2026-11-17: "upper mall road pe grass layer aari hai", "legs distance upar
            // neeche hori hai, terrain shape change hora"). GPU-instanced terrain reads its heights from a texture in the
            // vertex shader, at the phone GPU's precision: the ground drawn and the ground walked on came apart, and the
            // drawn ground rose through the Upper Mall slab. Pixel error 1: the drawn mesh stays within a pixel of the
            // collider's shape.
            mobileTerrain = mobile;

            if (!FindAnyObjectByType<GameInput>()) new GameObject("GameInput").AddComponent<GameInput>();
            if (!FindAnyObjectByType<HUD>()) new GameObject("HUD").AddComponent<HUD>();
            // the main menu, the pause menu, settings, saving and the intro (it greets the player when play starts)
            if (!FindAnyObjectByType<FrontEnd>()) new GameObject("FrontEnd").AddComponent<FrontEnd>();
            WantedSystem.Ensure();   // the police, accidents and the 108 ambulance (Runtime/Police)
        }

        bool mobileTerrain;

        void Start()
        {
            if (mobileTerrain)   // (see Awake) every terrain is enabled by now
                foreach (var t in FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                {
                    t.drawInstanced = false;
                    t.heightmapPixelError = 1f;
                }
        }
    }
}
