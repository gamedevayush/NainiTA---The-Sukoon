using UnityEngine;

namespace Nainital
{
    /// <summary>
    /// How busy the town is at each hour (user, 2026-11-11):
    ///   00-06  nobody on foot (after midnight everyone walks to the nearest house and goes in); a few cars
    ///   06-08  a few early walkers; a few cars
    ///   08-17  the normal crowd and traffic
    ///   17-22  the evening rush: the Mall full, heavy traffic
    ///   22-24  thinning out
    /// Values are fractions of the most the crowd / traffic managers keep.
    /// </summary>
    public static class TownClock
    {
        public static float Hour => DayNightCycle.Instance ? DayNightCycle.Instance.timeOfDay : 12f;

        public static float Crowd(float h)
        {
            if (h < 6f) return 0f;
            if (h < 8f) return Mathf.Lerp(0.05f, 0.2f, (h - 6f) / 2f);
            if (h < 17f) return 0.6f;
            if (h < 22f) return 1f;
            return Mathf.Lerp(0.35f, 0.05f, (h - 22f) / 2f);
        }

        public static float Traffic(float h)
        {
            if (h < 8f) return 0.15f;
            if (h < 17f) return 0.6f;
            if (h < 22f) return 1f;
            return 0.35f;
        }

        /// <summary>Shops and stalls: open from half past eight to ten at night.</summary>
        public static bool ShopsOpen(float h) => h >= 8.5f && h < 22f;

        /// <summary>Locals at their posts (shopkeepers, boatmen, vendors, temple-goers) go home after midnight.</summary>
        public static bool LocalsOut(float h) => h >= 6f;
    }
}
