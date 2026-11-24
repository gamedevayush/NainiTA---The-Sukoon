# Owner's issues, 24 Nov 2026 night (from the Unity editor). Only noted - fix when he says go.

1. Remove all the old low-poly (box-style) bikes / scooties parked across the map. (01_old_lowpoly_bikes.png)
2. Lake edge by this road (02_*.png):
   a. A green grill (railing) along the lake border, as in his Street View shot (02_streetview_green_grill_park.png).
   b. The boats are wrong where they are (red circle: sunk into the terrain, there are no boats there in reality).
      They belong further on (green circle), and right next to them, touching, is Bhimrao Ambedkar Park
      (blue) - see 02_where_boats_and_ambedkar_park_go.png.
3. Army Rest & Recoup Centre (03_*.png):
   a. The road is not aligned with its gate - the gate must open straight onto the road (Street View: gate right on
      the Mall Road edge, wall + small house beside it).
   b. "till bus stand road tk yellow building hai ek": one yellow building runs from there up to the bus stand road
      (confirm the exact stretch with him when fixing).
4. Lower Mall Road entry turn (Mall Road Nainital board, Tallital end) is too sharp (04_lower_mall_turn_sharp.png):
   smooth it - widen the road a little toward the lake (his green line: the lake-side edge pushed out, arrows
   pointing lakeward), with a boundary (railing/wall) along that new lake edge. Red lines = the current sharp edges.
5. Floating balconies on Mall Road near the Zoo road turn (05_floating_balconies_zoo_road.png).
   Cause found: WorldBuilder.Mall.cs ~1349 "long balconies" puts one balcony per floor from world.json's floor
   count (levels) at street + 3 m * floor, but the house actually built there is shorter / a different building, so
   the upper balconies hang in the air. Fix: stack them only up to the height of the building that really stands
   there (and only where one stands), then check every Mall Road balcony.

## Done 25 Nov (Build World 25 Nov, shots Shots/o24b_*, o24c_*, o24d_*)
1. Box bikes removed. 2. Boat stand + moored row at the real boats, floating; green grill on the water; Ambedkar Park.
3a. Gate on the compound road. 3b. Long yellow building (teal tin) on the Bhowali road, junction -> bus stand.
4. Lower Mall bend widened on the lake side, park grill as the boundary. 5. Balconies only on built houses.
