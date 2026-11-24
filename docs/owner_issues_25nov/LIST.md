# Owner's issues, 25 Nov 2026 (from the Unity editor). Only noted - fix when he says go ("krna baad me..pehle sab note krlo").

1. Tallital bus stand (canopy + parked buses/cars) takes up half the road: the canopy and its vehicles stand on the
   carriageway. Fit it between the two roads (the fork of the Bhowali and Haldwani roads) so both roads are clear.
   (01_bus_stand_on_road_a.png: canopy pillars and a bus on the road; 01_bus_stand_on_road_b.png: canopy, bus and cars
   on the road's edge)
2. Traffic lights editable in the editor: today TrafficSignals builds them only at game start (Play: under
   "Traffic (AI cars)" -> "Signal x,z" -> "Post <road>", "Stop line"), so he cannot see or move them in edit mode.
   Plan: menu "Nainital > Traffic Lights > Place in Scene" bakes posts + stop lines into the scene ("Traffic Lights
   (placed)"); runtime uses the placed ones when present (only the signal logic), else places them as now; Build World
   keeps them like the hand-placed things (RestorePlacedByHand).

## Done 25 Nov (Build World 01:41, shots Shots/o25*_*.png)
1. Bus stand between the roads: BusStrip() finds the strip between the Haldwani highway and the Bhowali road (23.4 m
   clear here); canopy 21 m across it, yard half 8.9 m, buses centred, taxis only where there is room.
2. Traffic lights in the editor: Build World puts them under "Traffic Lights (placed)"; move a post, Ctrl+S ->
   Assets/Resources/TrafficLightPlaces.json; the game uses those places (Editor/TrafficLightPlaces.cs). Nainital >
   Traffic Lights > Back to automatic places resets.
Also: his own moves (Snow View ropeway bench, Tallital traffic post) kept through rebuilds: Nainital > Keep my move
(selected) -> Data/hand_moves.json (Editor/HandMoves.cs).
