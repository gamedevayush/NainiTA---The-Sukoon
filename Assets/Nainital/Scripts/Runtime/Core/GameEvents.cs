using System;
using UnityEngine;

namespace Nainital
{
    /// <summary>Tiny global event hub so gameplay code does not depend on UI/audio directly.</summary>
    public static class GameEvents
    {
        public static event Action<Vector3, bool> Hit;          // position, blocked
        public static event Action<string, float> Toast;        // message, seconds
        public static event Action<string, string> Dialog;      // title, body
        public static event Action<string> LocationChanged;
        public static event Action<string, string, Color> Banner;   // big centre text (mission passed/failed)
        public static event Action Ate;
        public static event Action MonkeyScared;
        public static event Action<Vector3> Splash;
        public static event Action<Vector3> Horn;

        public static void RaiseHit(Vector3 pos, bool blocked) => Hit?.Invoke(pos, blocked);
        public static void ShowToast(string msg, float seconds = 3f) => Toast?.Invoke(msg, seconds);
        public static void ShowDialog(string title, string body) => Dialog?.Invoke(title, body);
        public static void RaiseLocation(string name) => LocationChanged?.Invoke(name);
        public static void ShowBanner(string title, string sub, Color c) => Banner?.Invoke(title, sub, c);
        public static void RaiseAte() => Ate?.Invoke();
        public static void RaiseMonkeyScared() => MonkeyScared?.Invoke();
        public static void RaiseSplash(Vector3 p) => Splash?.Invoke(p);
        public static void RaiseHorn(Vector3 p) => Horn?.Invoke(p);
    }

    /// <summary>Shared runtime assets, filled by <see cref="GameManager"/>.</summary>
    public static class GameAssets
    {
        public static Material AtlasMaterial;
        public static Material MarkerMaterial;
        public static Material RainMaterial;
        public static Font Font;
    }
}
