using System;
using UnityEngine;

namespace Nainital
{
    public class Health : MonoBehaviour
    {
        public float max = 100f;
        public float current = 100f;
        public float regenPerSecond = 0f;
        public float regenDelay = 6f;
        [NonSerialized] public bool invulnerable;

        public event Action<float, Vector3, GameObject> Damaged;  // amount, from position, source
        public event Action Died;

        public bool IsDead => current <= 0f;
        public float Fraction => Mathf.Clamp01(current / max);
        public float LastDamageTime { get; private set; } = -100f;

        void Update()
        {
            if (!IsDead && regenPerSecond > 0f && current < max && Time.time - LastDamageTime > regenDelay)
                current = Mathf.Min(max, current + regenPerSecond * Time.deltaTime);
        }

        public bool Damage(float amount, Vector3 fromPosition, GameObject source)
        {
            if (IsDead || invulnerable || amount <= 0f) return false;
            current = Mathf.Max(0f, current - amount);
            LastDamageTime = Time.time;
            Damaged?.Invoke(amount, fromPosition, source);
            if (current <= 0f) Died?.Invoke();
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            current = Mathf.Min(max, current + amount);
        }

        public void Revive(float fraction = 1f)
        {
            current = max * Mathf.Clamp01(fraction);
            LastDamageTime = -100f;
        }
    }
}
