using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Nainital
{
    // its own file (24 Nov 2026): a scene component in a shared file has no script asset, the build inlined a
    // MonoScript into level1 and the iPhone player stopped at "level1 is corrupted"
    /// <summary>A parlour machine: E opens its game (Rs 20 a go).</summary>
    public class ArcadeCabinet : Interactable
    {
        public ArcadeScreen.Game game;
        public string title = "Arcade";
        public int price = 20;

        void Awake() { radius = 1.8f; }

        public override string Prompt => Loc.F("{0} khelo (Rs {1})", title, price);
        public override bool CanInteract(PlayerController p) => !ArcadeScreen.Current && !BowlingLane.Playing && !Cutscene.Playing && p.CurrentMode == PlayerController.Mode.Normal;

        public override void Interact(PlayerController p)
        {
            if (!GameState.TrySpend(price)) { GameEvents.ShowToast(Loc.F("Rs {0} chahiye", price), 2f); return; }
            ArcadeScreen.Open(game, title);
        }
    }
}
