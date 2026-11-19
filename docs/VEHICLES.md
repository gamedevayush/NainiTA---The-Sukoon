# Vehicles: the unbranded set, the Vehicle Setup tool, hands on the wheel

Written 20 Nov 2026 so another PC can pick this up. The work log is in `docs/TRACKER.md` (19-20 Nov); the README's
"Work in progress" has the short version.

## 1. Why there is a new set

The old library (`Assets/Nainital/Vehicles/Models`: Bolero, Scorpio, Innova Crysta, WagonR, Ertiga, Amaze, Tata 407...)
copies real brands. Fine for testing; a public / store release needs vehicles that look like no maker's model and carry no
logo or lettering. The owner (19 Nov): "2 bus, 10 gaadiyan, 5 bikes, 3 scooty, 1 tirri, 1 tuktuk ... bilkul bhi kisi company
se match na khaaye, no logo, but kinda Indian ... tripo ko best images dena taaki tyre kaatne me dikkat na ho".

**20 vehicles are in the set** (the owner removed the retro 350 and the electric scooty from the blend on 20 Nov; their
uncut FBX are still in `Vehicles/New` - ask him before using or deleting them):

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
Handlebar with Wheel_F under it; both buses have a door (`Door_FR`). Not made yet: car doors, `Steering` objects, the five
damage shape keys. **Open question:** both buses have their door on the vehicle's RIGHT (-X); Indian buses have it on the
left - Tripo probably mirrored them. Mirror the two buses in X if the owner agrees.

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
