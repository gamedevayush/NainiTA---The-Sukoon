using System.Collections.Generic;
using UnityEngine;

namespace Nainital
{
    /// <summary>Landmark information board.</summary>
    public class InfoSign : Interactable
    {
        public string title;
        [TextArea] public string body;
        public override string Prompt => "Padho: " + title;
        public override void Interact(PlayerController p) => GameEvents.ShowDialog(title, body);
    }
}
