# Vehicles: the unbranded set, the Vehicle Setup tool, hands on the wheel

Written 20 Nov 2026 so another PC can pick this up. The work log is in `docs/TRACKER.md` (19-20 Nov); the README's
"Work in progress" has the short version.

## 0. Hand-off of 21 Nov 2026 (03:00, main PC) - read this first

The owner set up and approved **all 20** vehicles in the tool (21 Nov 01:04-01:45), then asked for a check ("check kro")
and a fix "with perfect precision". If anything below looks wrong or needs changing, it goes into this list.

**What was checked (measured, not by eye):** every seat of every model against the model's own surfaces - every surface
straight down along each seat's line every 5 cm (`call VehicleSetupShots.RunProfileZ`, arg.txt `all`, arg2.txt `seats`),
cut views made two-sided so a Tripo shell's floor and seats show from inside (`SeatCuts`), the handlebar meshes' ends
(`RunBar`), and the characters' own bodies in each pose (`RunOffsets`: hips over the seat of the trousers 0.06-0.21 m, head
top over the hips 0.64-1.01 m, ankle over the sole 0.07-0.10 m seated / 0.11-0.15 m astride).

**What was wrong in the owner's setups (all fixed by `Tools/vehicles/fit_vehicle_seats.py --write`):**

| Vehicle | Was | Now |
|---|---|---|
| all cars / buses with a modelled cushion | the old Snap rule (hips = floor + 0.27) ignored cushions: people sat 8-17 cm INTO them (mpv_large rows 2-3, hatch rear, suv_boxy / tuktuk drivers) | hips = the cushion's top + 0.12 (`HumanoidRig.NominalButt`), 0.17 m in front of the backrest's foot |
| bus_tourist | feet 12-28 cm under the floor (1.43 vs floor 1.55); seats 19 / 20 sat 7 cm from 15 / 16 (two people in one seat) | feet on the floor (ankle 1.63), under the next seat's overhang in the last row; 19 / 20 on the empty left row z -1.24 |
| bus_roadways | seats 13-16 on open floor at the back (hips 1-15 cm over the floor), 17 at the back centre; 5 / 6 floating 20 cm, feet 52 cm short | 13-16 on the back-to-back row (z -2.30, facing the back - its backrest is on the forward side), 17 on the empty front-left seat; all on the 1.44 cushions, feet on the 1.29 floor; the driver's feet on the cab floor (1.13) |
| bike_street160 | rider 14 cm over the seat, pillion in his lap (17 cm apart), rider's feet 27 cm ahead of the pegs | rider on the seat dip (0.83), feet on the pegs found by a side scan (z -0.15); pillion on the pad (z -0.60, 0.99), pegs 0.54 high |
| car_tallboy | driver 22 cm back, inside his backrest (feet 33 cm short) | on the cushion beside the passenger, feet in the footwell |
| car_sedan_compact | driver 14 cm lower than the passenger | same height (an empty shell: floor + 0.27) |
| car_hatch_small | front passenger's feet hanging under the dash 28 cm over the floor | on the floor (0.39) |
| car_suv_boxy | the three rear passengers sat behind the rear seat's backrest, in the boot | on the rear seat (z -0.90) |
| car_mpv_large | front row 16-22 cm high, middle / third rows 15-17 cm sunk, feet under the floor | on the cushions; third row's footwell is filled in the model, so the feet go where a floor 0.30 m down would be |
| tirri_erickshaw | rear-facing pair's feet on the opposite bench (0.85-1.05), one of them 11 cm sunk; driver's left hand 20 cm ahead of the bar | feet on the floor (0.45), both on the cushion; left hand on the bar's end (-0.34, 1.07, 0.69) |
| tuktuk_auto | driver sunk 8 cm, passengers' feet under their raised floor (0.60) | on the cushions, feet on that floor |
| truck_light, car_pickup, car_van | hips / feet a few cm off | fitted |
| scooty_neoretro | Bend 0 + bar ends at +-0.40: arms spread like wings, hands 11 cm short | grips +-0.34 symmetric, **Bend 8** (the owner had set 0 - put it back if he prefers) |

Untouched (already right): car_mpv_compact, car_suv_muscular, the other bikes' feet / grips, all lamps.
The owner's own setups as they were: `Tools/vehicles/setups_before_fit_21nov/` (diff against `Assets/Resources/VehicleSetup`).

**New in code:**
- `VehicleSetupData.Seat.surface` / `roof`: the sitting surface and the roof's inside over the seat (vehicle space, -1 =
  not measured). The fitter fills them.
- `HumanoidRig` seat fit: seats count on a nominal seat of the trousers (0.12 m under the hips); each character measures
  its own once per pose from its baked skin (cached per mesh) and sits that much higher / lower (clamped +-0.07 m: a kurta /
  saree hem hanging under the seat line would read as a fuller seat), eased in; with `seatRoof` set it keeps its head 3 cm
  under the roof. `SoleLift`: HandIK lifts / lowers the foot targets by the character's own sole (nominal 0.08 m seated,
  0.14 m astride). This applies to every seated / riding character in the game, not only the setups.
- Tool: each person's `seatRoof` from the seat's `roof`; `VehicleSetupWindow.SeatData(i)`.
- Checks (bridge, Play first): `VehicleSetupShots.RunProfileZ` / `RunColumns` / `RunSideScan` / `RunBar` / `RunOffsets` /
  `SeatCuts` (arg.txt `model:seat,seat;model2`) -> `Shots/vseat_<model>.png` with a 10 cm ruler and the hips in red; the
  sheet (`Run`) now logs `butt-gap` (seat of the trousers over the surface) and `head-room` per seat. Readers in
  `Tools/vehicles/`: `sheet_report.py` (worst over all runs), `seat_geom.py`, `profile_line.py`, `sidescan_read.py`,
  `offsets_read.py`; `bridge.ps1` / `waitlog.ps1` / `recompile_play.ps1` drive the bridge.

**Check after the fit (3 runs of all 20 sheets, random characters each run; `sheet_report.py`):** every bus seat
(43 people), car_mpv_compact, car_van, the suv_boxy / suv_muscular passengers, tirri and the pillions of commuter125,
dualsport, street160 and both scooties: feet and hands 0-4 cm. The seat of the trousers sits on the surface by
construction of the fit (0 cm) except where the roof rule pushes a tall character down (suv_boxy rear up to 6 cm, tirri 4,
and item 1). Heads clear the roof everywhere (>= 3 cm). The list that was open then (items 1-8 below, kept for the
record) **was done on 21 Nov afternoon - section 0.1; what is still open: section 0.2.**
1. **Small cabins vs tall characters (owner to decide)**: the roofs of car_hatch_small, car_tallboy, car_mpv_large,
   car_pickup, truck_light and tuktuk_auto are too low for the taller characters (head top 0.75-1.0 m over the hips,
   headroom over a correct hips height 0.72-0.80 m). The roof rule keeps every head 3 cm inside, so those characters sink
   10-31 cm into the cushion (hidden by the doors from outside, visible in the cut views). Options: (a) keep it; (b) let
   heads touch / pass the roof instead (`HumanoidRig.HeadClear`, or no `roof` in those setups); (c) seat only the shorter
   characters there (`CharacterLibrary.Pick` by height).
2. Hands short: **bike_street160 rider 20 cm** (the bar's real ends are at +-0.44, z 0.40 by `RunBar`; his targets are at
   +-0.27 z 0.30 on the switch housings - move them out to about +-0.38 1.03 0.37, or raise Bend), **tuktuk driver 10 cm**
   and **scooty_family rider 10 cm** (both rose 8-20 cm onto their cushions: lower the balls or move the seat forward),
   truck driver 5 cm.
3. Feet short after the rise: **car_sedan_compact driver 9 cm** and **car_suv_muscular driver 10 cm** (pedals 0.62 m
   ahead: bring the feet to ~0.50 m ahead), **bike_commuter100 rider 7 cm / third rider 12 cm**, bike_dualsport rider 8 cm,
   scooty_neoretro rider 5 cm (short-legged characters on high seats).
4. **The tool's Snap still uses the old floor rule** (floor + 0.27, cushions ignored): do not press Snap / Snap everyone
   on these 20 fitted vehicles until `SnapSeats` follows `fit_vehicle_seats.py` (cushion + NominalButt, feet on the floor
   ahead of the cushion, `surface` / `roof` stored). A new vehicle snapped by the tool should go through the fitter too.
5. bus_tourist driver: the model has no cushion under him (a block from 1.33 to 1.92 m where his seat is, cab floor
   0.91); only his feet were put on the floor. Look at `SeatCuts` `bus_tourist:0` and set him by eye.
6. tuktuk / tirri handlebars are modelled on the LEFT half only (x -0.40..0): both hands sit left of the centre. Fix in
   Blender (centre the bar) or accept.
7. scooty_family's headlight quad is turned 254 deg (side-on: a thin line from the front; the beam itself uses the
   vehicle's forward). Snapped heads at ~180 deg are fine (the quad is two-sided). Set it to 180 in its Rotation field.
8. Then the game must read the approved setups (section 5, "After the owner's OK"): seats with `surface` / `roof`
   (set `HumanoidRig.seatRoof`), lamps by `on`, HandIK feet / hands / bend.

### 0.1 Done on 21 Nov 2026, afternoon (PC "AYUSH", 2nd pass; owner: "jo baaki kaam hai vo poora krlo..ek dam precisely perfect krdo")

Measured the same way as above (ProfileZ again on this PC first: the fitter re-ran from the owner's backup and gave the
committed setups back with 0 differences, so every change below is only the new overrides).

| Item | What was done | Measured after |
|---|---|---|
| 1 small cabins | **Option (c), measured**: `call VehicleSetupShots.RunFits` seats every character of the CharacterLibrary in every seat of all 20 vehicles as the game would (its own seat fit, the roof rule, HandIK) and records per seat its worst miss (feet, hands, the seat of the trousers off the cushion) -> `Assets/Resources/VehicleFits.json`. The game (`VehicleSetup.PickLook`, used by `PedestrianManager.MakeSeated`) seats the wanted sex first (drivers mostly men, a scooty a third women), then those within 4 cm, else within 2 cm of the best of them; no child at a wheel / handlebar. | Most seats: 15-22 of 22 characters fit. **car_hatch_small, car_tallboy, tuktuk_auto: no grown man fits** (their cushions are modelled 0.60 m up, a real small car's ~0.45; roofs low): only children and the small women fit; men there sink 10-14 cm (hidden by the body from outside) - the best of the men is seated, not a woman every time. Real fix if wanted: lower those cushions / raise those roofs in Blender, export, fitter, RunFits. |
| 2 hands | bike_street160: grips onto the bar's real ends (+-0.38, 1.03, 0.39; RunBar: ends +-0.44 at y 1.00-1.10, z 0.40), rider to the front of the seat dip (z -0.20), Bend 18. scooty_family: its right hand was on the cowl (0.19, 1.10, 0.17) - grips onto the bar ends (+-0.30, 0.98, 0.36), rider forward to z -0.15. Truck: 0 cm on this PC. | Hands on the grips (<= 4 cm) for the characters long-armed enough: street160 8 of 22, scooty_family 12, dualsport 8, commuter125 12, commuter100 16 (the town's 12 own characters have 0.18-0.39 m arms); RunFits seats only those |
| 3 feet | car_sedan_compact / car_suv_muscular drivers: feet 0.50 m ahead of the root (were 0.62) on the floor there (fitter key `feetAhead`). The short-legged riders: RunFits leaves them off the high bikes. | drivers 0 cm |
| 4 Snap | `VehicleSetupWindow.SnapSeats` now follows the fitter (cushion run along the seat line every 5 cm, hips NominalButt over the cushion 0.17 m ahead of its rear edge - further while the floor is out of 0.50 m reach - feet past its front edge, drivers 10 cm further; an empty shell: 0.27 / 0.42 over the underbody; a two-wheeler on its seat's top) and stores `surface` / `roof`. Per-model hand fixes stay in the fitter's MOVES / OVR: **on the 20 fitted vehicles run the fitter, not Snap** (Snap would undo those fixes); Snap is for a new vehicle, then add its fixes to the fitter. | Snap vs the committed setups: sedan, van, commuter125, neoretro 0 cm; truck, bus_roadways 2-4 cm along the seat (the 5 cm sampling step); mpv_large, suv_boxy, tirri differ where the fitter has hand fixes |
| 5 bus_tourist driver | He sat INSIDE the closed block that is the driver's seat (backrest top 1.92, underside 1.33, z 3.29-3.59; `Tools/newvehicles/diag/cut_render.py` side cut). Now: back against its front face (hips z 3.71), hips 1.45 (0.12 over its underside), feet on the cab floor (0.91) at z 3.95. | feet / hands 0 cm, 21 of 22 fit |
| 6 auto / e-rickshaw steering | Renders from the driver's seat (`diag/bar_render.py`) showed it is a small **round steering wheel** on a column, not half a T-bar - mirroring it made two wheels, so it is **moved onto the centre line** instead (`Tools/newvehicles/fix_v06_bars.py`, saved into `NewVehicles_v06.blend`, both FBX exported). Wrists on its rim at ten-to-two (printed by the script, in the fitter's OVR). | 0 cm |
| 7 scooty_family headlight | euler (0, 180, 0) (it lay flat, facing down) - fitter `LAMPS` | - |
| 8 the game | see below | In the Nainital scene (world rebuilt on this PC): all 20 in a row on The Flats (`VehicleTest.Lineup -85,20`), people in every seat, day and night shots (`Shots/lineup_*.png`): wheels on the ground, drivers' hands on the wheels / grips, lamps lit at night (four bikes' headlights on their handlebars). Live traffic 50 s: 15 cars + 8 bikes, all of the new set, 1-5 people each with HandIK, no errors. The player took a traffic sedan (driver and passengers got out; he sat in the setup seat, hands and feet 0 cm) and got out at its entry point. Police car (car_suv_boxy, lettering, light bar, two constables) and police bike (bike_commuter125) spawned with their crews seated. |

**The game now uses the unbranded set (item 8, section 5 "After the owner's OK"):**
- `Nainital > 7. Import vehicles` (`VehicleKit.Build` -> `BuildNew`): the 14 vehicles + 6 two-wheelers of `Vehicles/New`
  become `Resources/VehicleLibrary` (prefabs in `Vehicles/New/Prefabs`, the FBX as they are; roles: the MPVs and the boxy
  SUV are the white hill taxis, the auto / e-rickshaw rare taxis with their own top speed and mass). Of the branded models
  only `Ambulance108` stays (no stand-in yet, never in traffic). The importer makes **mesh LODs** (Unity 6 Mesh LOD,
  11 levels) and phones get the textures at 1K (`NewVehicleImport`).
- `VehicleParts` (new, `Runtime/Vehicles`): wheels spin about their own axles, a car's front wheels steer, the handlebar
  turns about its own raked axis with Wheel_F under it, a `Steering` object 3x the angle (from `CarController.SteerAngle`
  / `BikeRide.Steer`), the wheels bounce with the suspension, doors open on their hinge or by the buses' own clip while
  someone gets in / out, a hit dents the `Dmg_*` key of that side (when the models get them), the setup's lamps (a bike's
  headlight on its handlebar turns with it). `Sit(person, rig, seat)`: the setup seat's pose, place, head under its roof,
  hands on the wheel / grips and feet on the floor / pegs (HandIK, Bend).
- Traffic: driver + passengers (a car mostly 0-2, a taxi one more, a bus 3-10; all traffic together at most 28, a phone
  10), each a character that fits its seat; they get out at their own doors when the player takes the car or it burns.
  Two-wheelers: rider + a pillion one time in three. The player sits in the setup's driver seat (hands, feet, door) and
  gets out at its entry point. Police: `car_suv_boxy` and `bike_commuter125` (lettering and light bar re-measured,
  `Nainital > 7b`), officers in the setup's seats. Parked vehicles of an older scene are mapped to the new set by kind
  (no rebuild needed); the bus stand asks for `bus_roadways` / `bus_tourist` by name.
- Check in the game: `call VehicleTest.Lineup` (the Nainital scene in Play: every vehicle in a row by the nearest road,
  people seated as the traffic would) then `call VehicleTest.LineupShots` -> `Shots/lineup_N.png`.

### 0.2 Still open
1. **Riders were measured on the procedural pose here** (this PC has no Asset Store riding pack): on the main PC (packs
   in) run `call VehicleSetupShots.RunFits` with arg.txt `bike_commuter100,bike_commuter125,bike_dualsport,bike_street160,scooty_family,scooty_neoretro`
   (Play in the VehicleSetup scene first) and commit `Assets/Resources/VehicleFits.json`. Re-run RunFits for a vehicle
   whenever its setup changes or a character is added.
2. The three small cabins (item 1 above): remodel or accept.
3. Car doors, `Steering` objects and the five `Dmg_*` shape keys are still not in the models (the game drives them as soon
   as the models have them).
4. The 108 ambulance has no unbranded stand-in (`VehicleKit.KeepOld`).
5. Owner's look in the game; phone FPS with the new traffic (20-23k-triangle bodies; mesh LOD drops them with distance).

## 1. Why there is a new set

The old library (`Assets/Nainital/Vehicles/Models`: Bolero, Scorpio, Innova Crysta, WagonR, Ertiga, Amaze, Tata 407...)
copies real brands. Fine for testing; a public / store release needs vehicles that look like no maker's model and carry no
logo or lettering. The owner (19 Nov): "2 bus, 10 gaadiyan, 5 bikes, 3 scooty, 1 tirri, 1 tuktuk ... bilkul bhi kisi company
se match na khaaye, no logo, but kinda Indian ... tripo ko best images dena taaki tyre kaatne me dikkat na ho".

**20 vehicles are in the set** (the owner removed the retro 350 and the electric scooty on purpose on 20 Nov; their FBX
were removed from `Vehicles/New` too; their design image and Tripo model stay in `Tools/newvehicles/clean` and `models`):

> **Read section 9 first: the 20 Nov hand-off has a list of things that are broken or not done.**

| Kind | Vehicles |
|---|---|
| Buses | `bus_roadways` (hill route, 7.8 m), `bus_tourist` (coach, 9.2 m) |
| Cars, vans, trucks | `car_hatch_small`, `car_tallboy`, `car_sedan_compact`, `car_mpv_compact`, `car_mpv_large`, `car_suv_boxy`, `car_suv_muscular`, `car_pickup`, `car_van`, `truck_light` |
| Bikes | `bike_commuter100`, `bike_commuter125`, `bike_street160`, `bike_dualsport` (`bike_retro350` removed) |
| Scooties | `scooty_family`, `scooty_neoretro` (`scooty_electric` removed) |
| Three-wheelers | `tirri_erickshaw` (battery e-rickshaw), `tuktuk_auto` (auto-rickshaw) |

## 2. Where everything is

| What | Where |
|---|---|
| **The main blend** (every vehicle, cut into parts, textures packed) | `Tools/newvehicles/NewVehicles_v05.blend` |
| Unity models (one FBX per vehicle) + textures + URP materials | `Assets/Nainital/Vehicles/New/` (`Textures/`, `Materials/`) |
| Tripo originals (decoded, 30k tris, 2K texture) | `Tools/newvehicles/models/NN_name_dec.glb` |
| Tripo input images (Flow designs, watermark/badges cleaned) | `Tools/newvehicles/clean/NN_name.png` |
| Design prompts (style, per-vehicle briefs, "must not look like") | `Tools/newvehicles/prompts.json` (`briefs.py` wrote them) |
| Scripts | `Tools/newvehicles/`: `clean.py`, `check_model.py`, `build_blend.py`, `export_unity.py`, `sheet.py` |
| The tool | `Assets/Nainital/Scripts/Editor/VehicleSetupWindow.cs` (menu **Nainital > Vehicle Setup**) |
| Setup data, previews | `Assets/Nainital/Scripts/Runtime/Vehicles/VehicleSetup.cs` |
| Hands on the wheel / grips | `Assets/Nainital/Scripts/Runtime/Characters/HandIK.cs` |
| Name clean-up on import | `Assets/Nainital/Scripts/Editor/NewVehicleImport.cs` |
| Saved setups (made by the tool) | `Assets/Resources/VehicleSetup/<name>.json` |

## 3. The parts in Blender (names the tool and the game look for)

Each vehicle is one collection named `NN_name` (e.g. `02_car_hatch_small`). Front of every vehicle = **-Y**, ground = z 0,
real size, one uniform scale. Blender adds `.001`, `.002` to repeated names; Unity drops them on import
(`NewVehicleImport.cs`), so `Wheel_FL.004` arrives as `Wheel_FL`.

| Part | Name | Origin |
|---|---|---|
| Body | `Body` | anywhere |
| Car front wheels | `Wheel_FL`, `Wheel_FR` (left = +X in Blender) | the wheel's centre |
| Rear wheels | `Wheel_R` (both in one object, same axle) or `Wheel_RL` + `Wheel_RR` | on the axle |
| Bike / scooty / auto / tirri front wheel | `Wheel_F`, **a child of the handlebar** | the wheel's centre |
| Bike / scooty rear wheel | `Wheel_R` | the wheel's centre |
| Handlebar (with fork, front mudguard, headlamp) | `Handlebar` (or `Handle*`, `Fork*`) | on the steering axis; its local **Z = the steering axis** (raked: 24 deg bikes/scooties, 12 deg auto/tirri, through the front wheel's centre) |
| Car steering wheel (optional) | `Steering` | its centre, local Z along the column |
| Doors | `Door_FL`, `Door_FR`, `Door_RL`, `Door_RR`, `Door_Back` | **on the hinge line** |
| Damage | shape keys on the Body: `Dmg_Front`, `Dmg_Rear`, `Dmg_Left`, `Dmg_Right`, `Dmg_Top` | - |

State of `NewVehicles_v05.blend` (20 Nov): every vehicle has Body + wheels; bikes, scooties, auto and tirri have the
Handlebar with Wheel_F under it; both buses have a door with the owner's own fold/slide **animation** (keys on location and
scale, not a hinge). Not made yet: car doors, `Steering` objects, the five damage shape keys. **v05 has known problems
(displaced bus doors, bike 13's handlebar, keyed mirrors, right-hand drive to check) - fix them first: section 9.**

## 4. Blender -> Unity

Save the blend, then (Blender closed or open, it does not matter):

```
"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" -b Tools\newvehicles\NewVehicles_v05.blend --python-exit-code 1 -P Tools\newvehicles\export_unity.py -- Assets\Nainital\Vehicles\New
```

It writes `Assets/Nainital/Vehicles/New/<name>.fbx` per collection (moved to the origin for the export only, same axes as
`make_vehicle_lods.py`, modifiers NOT applied so the shape keys survive) and the textures as PNG in `Textures/`. It
un-hides / un-excludes each collection first (an excluded collection exported an EMPTY FBX on 20 Nov). Only some
vehicles: add their names at the end (`... -- Assets\Nainital\Vehicles\New car_van bike_street160`).

Then in Unity (batch or the open editor's bridge): `call VehicleSetupWindow.FixMaterials` (URP Lit materials from those
PNGs - Unity leaves the FBX-embedded textures off its materials) and `call VehicleSetupWindow.ImportCheck` (size, length
along z, materials with textures, the parts it found). `call VehicleSetupWindow.SheetShot` photographs the whole set to
`Shots/vehicles_new.png`.

**Not done on the main PC yet:** the v05 FBX were exported but not imported into Unity (the HDD was 100 % busy with BitLocker
decryption after a crash). The first Unity start on the next PC imports them; run `FixMaterials` once after that.

## 5. The Vehicle Setup tool

1. **Nainital > Vehicle Setup** -> "Open / make the VehicleSetup scene" -> **Play** (people animate only in Play; saving
   works in Play).
2. **Prev / Next** or the drop-down: one vehicle at a time (tick "Also the old library vehicles" for the old set). ✓ = OK'd,
   • = saved.
3. **People**: as many as the vehicle carries (`VehicleSetup.Capacity`: car 5, MPV/SUV 7, large MPV 8, pickup/truck 3,
   van 2, roadways bus 33, coach 36, bike/scooty 2, tuktuk 4, tirri 5). Pose per seat: Drive, Sit, Ride, Pillion,
   SideSaddle. Move / turn each person in the Scene view (click selects the person); the green disc = where he stands to get
   in and out ("Entry"). "Face" = another look, "+ Seat", "✕", "Mirror selected seat".
4. **Hands**: driver / rider seats get a blue (left) and orange (right) ball = the wrists. Put them on the steering rim /
   the handlebar grips ("L" / "R" select them). HandIK bends the arms to them; they ride on the handlebar / `Steering`, so
   the hands turn with the steering. On a handlebar they start on the grips (the bar mesh's widest points in a band 8-40 cm
   under its top). When the arms cannot reach, HandIK leans the back forward (up to 35 deg) like a real rider.
4b. **Feet (20 Nov night)**: everyone gets a cyan (left) and pink (right) foot marker ("FL" / "FR") = the ankles; HandIK
   bends the legs to them. Every Mixamo sitting clip (Driving too) is a chair pose with the shins straight down, so without
   this the feet went through a car's floor and hung off a bike's pegs. **Snap** (per seat) / **Snap everyone**: sets the
   person at seat height over this model's floor (car 0.27 m, bus / truck / van / auto 0.42 m, head room 0.98 m under the
   roof; a two-wheeler on its seat, the pillion never more than 0.10 m over the rider) and the feet 8 cm over the floor
   under their markers. The floor = the lowest surface a ray meets inside the shell (a Tripo body is a closed shell: its
   underbody's inside, 0.22 m in the compact sedan). Only small moves are taken (hips +-0.25 m, feet up to 0.6 m under the
   hips): a bus shell has no cabin floor, so bus seats keep their height. A vehicle with no saved setup is snapped by itself.
   "Feet to default" puts the feet back (car driver: pedals 0.62 m ahead; motorbike: pegs 0.32 m high; scooty: floorboard;
   pillion: rear pegs 0.28 m ahead, 0.40 m high).
5. **Lamps**: "+ Head lamp" / "+ Tail lamp" (mirrored pair by default). Per lamp: size, **Position (m)** and **Rotation (deg)**
   in the vehicle's space (x right, y up, z forward; drag the X / Y / Z labels to slide), **Snap to body** (moves it along
   the length onto the body's surface ahead of / behind it and turns it to face out), "x <-> 0" (centre it). The fields
   follow what is moved in the Scene view. "Night preview".
   **On the handlebar (20 Nov night: "kuch me front light handle ka child hongi")**: the "handle" toggle hangs a lamp on the
   handlebar object (saved as `on` = its name), so it turns with the steering; Snap to body sets it by itself to the part
   the lamp landed on. A new handlebar vehicle gets its headlights snapped at once: dualsport, street160 and both
   scooties came out on the Handlebar, commuter-125 on the body (its headlight is cut into the Body mesh, so it cannot turn
   anyway), the auto and the e-rickshaw on the body. Position / Rotation stay in the vehicle's space with the steering
   straight. The game (when it reads the setups) must hang a lamp under the part named in `on`.
6. **Wheels**: Speed slider (- = reverse) spins every wheel, Steer turns the handlebar (and Wheel_F under it) or the car's
   front wheels and `Steering`. The **Checks** list says what is wrong with the parts (origin off the centre, Wheel_F not
   under the handlebar, door origin in the middle, wheel count...).
7. **Doors**: a slider each; hinge Side / Top / Bottom, flip, angle.
8. **Damage**: a slider per `Dmg_*` shape key.
9. **Player gets in**: "Play it" - the one in the seat is pulled out (Mixamo "Pulled From Seat") and walks off; the player
   walks up and gets in (Mixamo "Entering Car"; not for the open three-wheelers).
10. **Save** -> `Assets/Resources/VehicleSetup/<name>.json`. **OK - approve** = the owner is happy; the game will only use
   approved setups.

**Checking without opening the cars (20 Nov night)**: `call VehicleSetupShots.Play` (bridge), then `call
VehicleSetupShots.Run` with `Bridge/arg.txt` = a model name, a comma list or `all` -> `Shots/vsetup_<model>.png`: the left
side, a cut through the driver's seat and one through the left passengers (the body in front of the cut clipped away), front
three-quarter, the top with the roof cut off, a close one over the driver's shoulder; the log line gives each seat's clip,
hips height and how far each hand / foot is from its target. `call VehicleSetupShots.Probe` lists every surface under two
lines down the vehicle (floor, seats, roof). `call ClipPoseReport.Run` prints the leg / arm muscles of the sitting clips.
`call VehicleSetupShots.Stop` leaves Play. With the default setups every hand / foot is within ~15 cm of its target on all
20 models (most 0 cm).

**Riding clips from the Asset Store (20 Nov night, in)**: Mixamo has no motorcycle riding and no car-seat sitting. The
owner added two free packs (Standard Asset Store EULA) to his Unity account and imported them:
- "Motorcycle Interaction Animset FREE" (SpectraMotions) -> `Assets/MotoInteractionAnimsFREE/` (Humanoid). AnimKit uses
  `AS_Idle_Riding` + `AS_Turn_V1_Left/Right_Loop` as the **Ride** state (1D blend on `Lean`, -1 left .. +1 right),
  `AS_Turn_V1_Left/Right_Loop_Passenger` as **PillionRide**, and `AS_Mount_Left` / `AS_Dismount_Left` / `AS_Bike_Start` as
  the Mount / Dismount / BikeStart actions. HumanoidRig: `riding` (+ `pillion`) now plays these; `lean` drives the turn
  loops; the hips are held 0.40 m over the root as the old procedural pose did, so saved seats and BikeRide stay right.
  The setup tool's Steer slider leans a two-wheeler's people; "Player gets in" plays Dismount / Mount on two-wheelers.
- "Human Basic Motions FREE" (Kevin Iglesias) -> `Assets/Kevin Iglesias/`. This version has NO sitting clips (idles,
  locomotion, talk only); its masked pose `Human@ObjectGripHands01` is the **Grip** layer (fingers only) - drivers and
  riders close their fingers round the wheel / the grips. Car seats rely on the feet IK above.
- **Not in git** (the repo is public; the EULA forbids redistributing them): both folders are in .gitignore. On another PC:
  Package Manager > My Assets > download + import both, then **Nainital > 6b. Build character animator**. Without them
  AnimKit builds no Ride / Pillion / Grip states and HumanoidRig keeps the procedural riding pose (the committed controller
  references their clips; on a PC without them those states are empty and HumanoidRig does not use them).
- Found while wiring: the 19 Nov car triggers (CarEnter, CarExit, PulledOut, RemoveDriver, Honk, StandToSit, SitToStand)
  had states but no parameters, so they never played ("parameter does not exist"); AnimKit adds them now.
- **Back lean (owner: "pehla itna jhuka kyun hai")**: the pack's clips lean like a sport bike (chest muscle -0.71), and the
  town's own 12 characters have short arms (shoulder to wrist 0.18-0.39 m vs 0.41-0.50 for the Mixamo-rigged goan_* /
  police: `call ClipPoseReport.Arms`), so the reach hunched riders 37-43 deg. HandIK now straightens the back to `maxBend`
  (rider 12 deg, pillion 5 deg; per seat: the "Bend deg" field next to Ride / Pillion seats, saved as `bend`), leans at most
  `maxReachLean` 20 deg more for the grips, and draws the forearm out up to `maxArmStretch` 1.35x for the rest. Riders came
  to 24-29 deg, pillions 5. A rider still folded = his L / R balls are far / low from his hips (the sheet's log gives
  "grips 0.62m ahead -0.01m up"): move the seat forward or the balls back / up.
- **Trap (20 Nov 23:10)**: a Ctrl+Z in the editor could undo AnimKit.Build's steps and leave the controller empty in memory
  (people drop through their seats, "Parameter 'Speed' does not exist" floods the log). Build now clears that Undo history;
  if it ever happens again: Nainital > 6b. Build character animator. `call ClipPoseReport.Controller` shows what the asset
  holds; `call VehicleSetupShots.Diag` shows each person's animator state.
- Check (vsetup sheets, 20 Nov 22:04): every rider plays AS_Idle_Riding with both hands on the grips (0 cm), pillions the
  passenger loop, car drivers Driving with hands on the wheel.

**After the owner's OK (done 21 Nov afternoon - section 0.1; the doors / Dmg keys wait for the models):** the game must read these setups - `VehicleSeats` (seats), `VehicleLamps`
(lamps), traffic passengers by capacity, HandIK on drivers/riders, `BikeRide` steering the handlebar with the wheel under
it, doors on getting in/out, the `Dmg_*` key on the side that was hit - and the new FBX must go into `VehicleKit` /
`Resources/VehicleLibrary` (LODs, colliders) in place of the branded ones.

## 6. Animations added (Mixamo, X Bot, no skin, 30 fps)

`Assets/Nainital/Characters/Anims`: `car_enter`, `car_exit`, `car_pulled_out`, `car_remove_driver`, `car_honk`,
`car_passenger_jostled`, `sit_hands_thighs`, `stand_to_sit`, `sit_to_stand`. Action-layer triggers in
`Resources/NainitalCharacter.controller` (`AnimKit.Build`): CarEnter, CarExit, PulledOut, RemoveDriver, Honk, StandToSit,
SitToStand (`HumanoidRig.PlayGesture`). Mixamo has no motorcycle riding: the rider and the pillion keep the procedural
riding pose (`HumanoidRig.riding`; `pillion` = hands on the thighs). Downloaded through mixamo.com's own Download button
in Brave Profile 1 (signed in with Google); reading the site token from a script was refused by the safety check.

## 7. How the set was made (to add more)

1. **Design image** in Google Flow (Nano Banana Pro, 16:9, 0 credits) with `prompts.json`: the style text + a per-vehicle
   brief listing distinct features and "it must not look like <real models of that kind>" + "the badge spot is smooth bare
   paint". Plain prompts came back as real cars (Matiz, Nissan Cube, Innova, Delica, Bajaj RE) with badges (Tata, Renault).
   Script: google-flow-automation skill `scripts/naini-flow-vehicles.js` (takes only the top-left, never-seen tile: the
   grid is virtualised and old tiles came back as "new").
2. **Clean** (`clean.py`): Flow's sparkle watermark (16:9 1K: x 1256 y 648, 50 px) and any badge box, LaMa inpaint, pasted
   back through the mask only. Zoom every nose before Tripo.
3. **Tripo** (`samjho-tripo-pipeline.js`, 30k, Ultra off, 2K; PBR on = 30 credits, off = 25). Tripo's .glb is
   meshopt-compressed: decode with `E:\SamjhoUnity\_tools\glbdecode\decode.mjs` before Blender.
4. **Check** each model (`check_model.py`: 4 views) and **build the blend** (`build_blend.py`: front to -Y per vehicle
   (`FRONT_OVERRIDE`), uniform scale = the median of the length / width / height ratios against `REAL`).

## 8. Traps hit

- 19 Nov ~22:35 the PC crashed (0x154) while BitLocker was decrypting C, D, E, F at once and Unity + Blender ran; after the
  reboot the 1 TB HDD (D/E/F) was missing until a full power cycle. Decrypt one partition at a time; one heavy job at a time.
- An excluded collection exports an empty FBX (fixed in `export_unity.py`).
- `bpy.ops.object.select_all` fails in background mode when the blend was saved in edit mode (the script selects directly).
- Wheel "origin off" was mostly along the axle (harmless); the tool measures across the axle only.

## 9.0 DONE on 21 Nov 2026 (main PC) - read this first

Sections 9.1-9.3 below were done; the current blend is **`Tools/newvehicles/NewVehicles_v06.blend`**, made from v05 by
`Tools/newvehicles/fix_v06.py` (v04 is not in git, so the doors were fixed without it):

| Item | What was done |
|---|---|
| Auto Keying | Off. The 19 one-frame actions are gone (transforms kept); only the two bus doors keep their animation |
| Bus doors | Each door's mesh slid back into its opening by matching the door's open edges to the body's (`diag/door_fit.py`, renders `diag/door_try.py`): roadways (-0.28, -0.41, +0.07) m, coach (+0.12, -0.45, +0.16) m. Their fold / slide animation is kept. No stray faces were found on the coach's door |
| Door names | **Both buses have their passenger door on the vehicle's RIGHT** (-X; front is -Y, so right is -X), next to the driver: `Door_FR` is the right name for both (the "rename it Door_FL" in 9.2 step 5 was a slip) |
| Bike 13 | The handlebar's key removed and its raked rotation put back (the mesh was counter-rotated for it, so it is where the owner made it); Wheel_F back on the wheel centre below the steering axis |
| Mirrors | The negative scales of the 01 / 02 / 11 bodies, the coach's door and bike 12's front wheel are baked into the meshes (world shape unchanged, scale positive) |
| Right-hand drive | Checked by renders (`diag/windscreen_check.py`, `diag/cabin_check.py`): 00, 01, 02, 03, 06, 07, 09, 11 have the steering wheel on the right; 04, 05, 08, 10 have opaque glass (no cabin to see). **Nothing more had to be mirrored**; 00_bus_roadways was already right-hand drive |
| Van | 10_car_van's `Wheel_R` (both rear wheels in one mesh) sat 0.75 m to the left - a one-frame key held it there; slid back under the body |
| Export | `export_unity.py` now bakes the doors' animation into the FBX (and moves their location keys with the collection) |
| Unity | 20 FBX imported, `FixMaterials` + `ImportCheck` clean (every model along z, every material textured); the 20 Nov code compiled with no errors; `NewVehicleImport` also renames the clip's curves (Door_FR.001 -> Door_FR); the tool's door slider plays the door's own clip when it has one; the wheel radius comes from the mesh (the rotated wheels' world box read up to 40 % big) |
| Tried in Play | car_hatch_small, car_suv_boxy, bike_commuter125, tuktuk_auto, bus_roadways, bus_tourist: Checks all green (only "no Steering object" info on cars / buses), handlebar steering with the rider's hands on the grips, the coach's door opening from its clip with the driver on the right, the "player gets in" preview, Save (the test file was deleted: setups are the owner's to make and approve) |

**Still open:** car doors, `Steering` objects and the `Dmg_*` shape keys (not made yet); the game using the approved
setups (section 5); the Mall Road items in 9.4.

## 9. (was PENDING) - hand-off of 20 Nov 2026, 04:00 (the owner went to sleep; do these on the next PC, in this order)

The owner's last words: "jitna theek hai vo bhej do, jitna reh gaya hai vo agle PC ko likh do, taaki wahan chalaun to
theek kar de". Also: **every car and bus must be right-hand drive** (steering on the vehicle's right, as in India) -
"buses aur kuch car maine scale -1 X axis ki kyunki unka steering left side tha, to tum aise karna ki sabka right hi ho".
BitLocker: the owner handles it himself.

### 9.1 What is wrong in `NewVehicles_v05.blend` (and so in the pushed FBX, exported from it at 03:17)

**Auto Keying was on in the owner's Blender.** Many objects have an action with ONE keyframe that pins their
location / rotation / scale (bodies, wheels, a handlebar). When the file loads or the frame changes, those keys put the
transform back to the keyed value. Found with `anim_check.py` (any object with `animation_data.action`):

| Object (collection) | Keys | Effect |
|---|---|---|
| `Door_FR` (00_bus_roadways) | location y -3.007 -> -2.704 and scale y 0.986 -> 0.085, frames 1-12 | **the door's real opening animation** (it folds/slides back) - keep it |
| `Door_FR.001` (01_bus_tourist) | scale x -0.998; scale y 1 -> 0.207 and location y -3.773 -> -3.443, frames 0-100 | **the door's opening animation** + the owner's mirror - keep the animation |
| `Body.001` (01_bus_tourist), `Body.002` (02_car_hatch_small), `Body.011` (11_truck_light) | scale x -0.998 / -1 / -0.962 (one key) | the owner's mirror for right-hand drive, held by a key |
| `Handlebar.001` (13_bike_commuter125) | rotation = identity (one key) | **undoes the raked axis**: on 20 Nov I set its rotation along the steering axis and counter-rotated its mesh; the key put the rotation back to identity, so the handlebar mesh now leans 24 deg the wrong way and Wheel_F under it is off |
| `Wheel_F` (12), wheels in 01, 03, 08, 10, 11, 13, 14, 16, 17, 18, 20, 21; `Body.013` | one key on location / rotation / scale | harmless while the values equal the object's own transform; check after the fixes |

**The bus doors are displaced.** On 20 Nov I moved each door's origin onto a "hinge" (front edge) and moved the mesh the
other way - but the doors' animation keys hold their location, so their origin went back and the mesh stayed moved:
the roadways door now sits 0.29 m inside the body and 0.41 m back; the tourist door sits outside the body. The doors
do not swing on a hinge at all - they fold / slide (the scale-y animation), so the hinge idea was wrong.

### 9.2 Fix in Blender (open `Tools/newvehicles/NewVehicles_v05.blend`, save the result as `NewVehicles_v06.blend`)

1. **Turn Auto Keying off** (Timeline, the record button; `scene.tool_settings.use_keyframe_insert_auto = False`).
2. **Doors back:** take the door meshes from `NewVehicles_v04.blend` (objects `Door` -> v05 `Door_FR`, `Door.001` -> v05
   `Door_FR.001`; append them and assign their mesh data to the v05 objects, then delete the appended objects). That puts
   each mesh back where the owner made it relative to its animated origin. Then, on the tourist door only, move the 2
   faces whose centre is at x > 0 (stray faces from the other side of the coach) into its Body - without touching the
   origin. Do NOT move the doors' origins again.
3. **Bike 13 handlebar:** delete `Handlebar.001`'s action (it only pins rotation to identity), restore its mesh from v04
   (object `Handle` in 13_bike_commuter125) and redo the raked frame: origin = front wheel centre + d * 0.44 with
   d = (0, sin 24 deg, cos 24 deg) (front is -Y), object Z along d, X = world X, keep the mesh where it is; then
   Wheel_F as its child again (keep transform). Check the other seven handlebars (12, 14, 16, 17, 18, 20, 21): they have no
   keys and should be fine.
4. **One-key actions:** for every object whose action has keys at only ONE frame (everything in the table except the two
   doors), evaluate at that frame, keep the resulting transform, and remove the action (`obj.animation_data_clear()`).
5. **Mirrors into the mesh (no negative scale into Unity):** for each object with a negative determinant (bodies of 01,
   02, 11; the tourist door; Wheel_F of 12): mesh.transform(diag(-1,1,1) on the negative axis), flip normals, make the
   object's scale positive - the world shape must not change. For the tourist door keep its animation: set its scale-x
   keys to +0.998 after mirroring the mesh. The coach's door is then on the vehicle's LEFT (+X) - rename it `Door_FL`.
6. **Right-hand drive for every car and bus:** for each of 00-11 render a view through the windscreen (camera in front at
   -Y, looking +Y at windscreen height; from there the vehicle's RIGHT (-X) appears on the LEFT of the picture) and look
   where the steering wheel is. Mirror any that is still left-hand drive (as in step 5, the whole vehicle about its centre
   x = the wheels' mean x; then swap the names Wheel_FL <-> Wheel_FR and Door_FL <-> Door_FR). Known: the owner mirrored 01,
   02 and 11; **00_bus_roadways was not mirrored** - its door is on the vehicle's right (-X), so its driver is probably on
   the left: check it first.
7. Re-run the checks: `check_model.py` sheets and a front render of the whole row (`Tools/newvehicles/diag/front_check.py`;
   the other scripts there found the problems above).

### 9.3 Then Unity

1. Export: `blender -b NewVehicles_v06.blend -P export_unity.py -- Assets\Nainital\Vehicles\New`. **Doors:** export their
   animation too (`bake_anim=True` for the door objects, or a separate pass), and change the tool's door preview: if the
   model has a clip for a door, the slider scrubs the clip (the hinge rotation is only for doors without one).
2. Open Unity: it imports the 20 FBX. Then `call VehicleSetupWindow.FixMaterials` and `call VehicleSetupWindow.ImportCheck`.
3. **Compile:** the 20 Nov changes (HandIK, handlebar / three-wheeler steering, Checks, NewVehicleImport) were never
   compiled - fix any errors first.
4. Try the tool in Play on a car, a bike, the auto and a bus: Checks list, steer, spin, hands on the wheel / grips, the
   "player gets in" preview, Save.
5. Commit and push (the owner wants every PC to be able to carry on).

### 9.4 Still open elsewhere

- Mall Road (README "Changed on 19 Nov (night)"): the Lower Mall lake-side edge, the Upper Mall deck vs the terrain, the
  divider overlapping the Upper Mall edge.
- The game using the approved setups (section 5, "After the owner's OK").
- Car doors, `Steering` objects and the `Dmg_*` shape keys are not made yet.
