# Vehicles: the unbranded set, the Vehicle Setup tool, hands on the wheel

Written 20 Nov 2026 so another PC can pick this up. The work log is in `docs/TRACKER.md` (19-20 Nov); the README's
"Work in progress" has the short version.

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
   the hands turn with the steering.
5. **Lamps**: "+ Head lamp" / "+ Tail lamp" (mirrored pair by default), size per lamp; "Night preview".
6. **Wheels**: Speed slider (- = reverse) spins every wheel, Steer turns the handlebar (and Wheel_F under it) or the car's
   front wheels and `Steering`. The **Checks** list says what is wrong with the parts (origin off the centre, Wheel_F not
   under the handlebar, door origin in the middle, wheel count...).
7. **Doors**: a slider each; hinge Side / Top / Bottom, flip, angle.
8. **Damage**: a slider per `Dmg_*` shape key.
9. **Player gets in**: "Play it" - the one in the seat is pulled out (Mixamo "Pulled From Seat") and walks off; the player
   walks up and gets in (Mixamo "Entering Car"; not for the open three-wheelers).
10. **Save** -> `Assets/Resources/VehicleSetup/<name>.json`. **OK - approve** = the owner is happy; the game will only use
   approved setups.

**After the owner's OK (not done yet):** the game must read these setups - `VehicleSeats` (seats), `VehicleLamps`
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
