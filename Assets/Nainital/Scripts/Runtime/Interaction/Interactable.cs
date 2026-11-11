using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>Base for anything the player can use with E / the hand button.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        public float radius = 2.4f;
        public abstract string Prompt { get; }
        public virtual bool CanInteract(PlayerController p) => true;
        public abstract void Interact(PlayerController p);
        public virtual Vector3 Point => transform.position;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);
    }
}
