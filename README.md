# NainiTA – The Sukoon

An open-world game set in Nainital, Uttarakhand, built in Unity. The town is made from real map data and you play it from third person. It runs on PC and on Android, with touch controls.

The repository holds the whole project except the built world. That folder (`Assets/Nainital/Generated`, about 600 MB) is rebuilt from the data in the repository by one menu command, which takes about 10 minutes.

## Setting up on a new PC

### 1. Install the tools

| Tool | Version | Notes |
|---|---|---|
| Git | any recent | <https://git-scm.com> |
| Git LFS | 3.x | Ships with Git for Windows. Check with `git lfs version`. |
| Unity Hub | latest | <https://unity.com/download> |
| Unity Editor | **6000.4.1f1** | Use exactly this version. In Unity Hub: Installs → Install Editor → Archive. Add the **Android Build Support** module (with OpenJDK and Android SDK & NDK) if you want to build for phones. |

### 2. Download the project

Run this once:

```
git lfs install
```

Then clone the project:

```
git clone https://github.com/gamedevayush/NainiTA---The-Sukoon.git
cd NainiTA---The-Sukoon
git lfs pull
```

The models, textures, animations, sounds and map data come through Git LFS, about 360 MB. Check that the LFS files really downloaded:

```
git lfs ls-files | head
```

A `*` after a file's id means it was downloaded. A `-` means only a small pointer file arrived; run `git lfs pull` again.

### 3. Open in Unity

1. In Unity Hub, choose **Add → Add project from disk** and pick the cloned folder. Open it with 6000.4.1f1.
2. The first import takes 20–40 minutes, because Unity rebuilds its `Library` cache. Wait until the progress bar at the bottom right is gone.
3. If Unity asks about the new Input System or an API update, choose **Yes**.

### 4. Build the world (once per PC)

1. In the top menu, choose **Nainital → 1. Project Setup**. This sets up the project settings the game expects (layers and so on).
2. Choose **Nainital → 2. Build World (generate scene)**.
   - It takes about 8–10 minutes. The Console prints `[Nainital] ...` lines as it goes.
   - It creates `Assets/Nainital/Generated` (terrain, town meshes, building atlas, materials, road network, lamp glow) and saves `Assets/Nainital/Scenes/Nainital.unity`.
   - Wait until the Console shows `terrain texture weights after save`.
3. Choose **Nainital → 0. Open Nainital scene**.

### 5. Play

Press **Play**.

| Action | Keyboard / mouse | Phone (touch) |
|---|---|---|
| Move | WASD | Joystick, bottom left |
| Look | Mouse | Drag the right side of the screen; pinch to zoom |
| Sprint | Shift | **Daudo** |
| Jump | Space | **Jump** |
| Use / talk / get in | E | **Use** |
| Punch / kick / block / roll | Mouse buttons, Q, … | Buttons on the right |
| Map | M | **Map** |
| Cheats | Type the code: UDAAN, SAFAR, JEEVAN, AMAR, GAADI, JEEP, TAXI, BUS | **Cheat** button |
| In a car | W/S throttle and brake, A/D steer, Space handbrake, G horn | Joystick; **Brake**; **Horn** |
| Take a moving car | E near it: it pulls up, the driver gets out, then you get in | **Use** |
| Weather and time | Pause menu | **Mausam**: Saaf / Baadal / Kohra / Baarish, time presets, ±1 hour, stop the clock |
| Report a bug | F8 (or **Nainital → Bug capture karo**, Ctrl+Shift+B, in the editor) | **Bug** |

The phone controls only show on a phone. To try them on PC, set PlayerPrefs `nt_touch = 1`.

Traffic runs on real car physics, each vehicle with its own mass. Crashes add up: after 4 or 5 knocks black smoke comes out of the bonnet, after 7 or 8 the car catches fire and then blows up.

A bug report grabs the screen, asks what is wrong (in the editor it can also attach more photos), and saves a folder in `BugReports/` with `screen.png` and `note.txt`. The note has the player and camera position, the hour and the weather. On a phone the folder goes to the app's data folder. On an iPhone, saving opens the Share sheet (WhatsApp, Mail, Save to Files), and the reports are also in the Files app under On My iPhone → Nainital Open World → BugReports. `BugReports/` is not in git.

### 6. Build the Android APK (optional)

1. Choose **Nainital → 5. Android: switch platform**. It takes about 5 minutes the first time, while textures are re-compressed.
2. Choose **Nainital → 6. Android: build APK**.
   - The first IL2CPP build takes about an hour.
   - The APK is written to `Builds/Android/NainitalOpenWorld.apk`. It is about 600 MB, ARM64, and needs Android 9 or later.
3. To install on a phone with USB debugging (or wireless debugging) on:
   ```
   adb install -r Builds/Android/NainitalOpenWorld.apk
   ```

### 7. Install on an iPhone (optional; no Mac and no paid Apple account needed)

The quick way: close Unity and run `Tools/pipeline/make_ipa.ps1` (see the helper scripts below); it does steps 1 and 2. By hand:

1. **Make the Xcode project (on Windows).** Install the iOS Build Support module for 6000.4.1f1. Then choose **Nainital → 8. iOS: switch platform**, then **Nainital → 9. iOS: build Xcode project**. The project is written to `Builds/iOS`.
2. **Compile it on GitHub's free Mac.**
   - Zip the contents of `Builds/iOS` as `NainitalGame-iOS-Xcode.zip`.
   - Attach it to a GitHub release with the tag `ios-build`.
   - Run the **iOS IPA (unsigned, for Sideloadly)** workflow (Actions tab). About 30–60 minutes later, `NainiTA-unsigned.ipa` appears on the same release.
3. **Install it with [Sideloadly](https://sideloadly.io)** on the Windows PC.
   - Install iTunes and iCloud from Apple's website and connect the iPhone by cable.
   - On the iPhone, turn on **Settings → Privacy & Security → Developer Mode** (iOS 16+).
   - In Sideloadly, drop the .ipa, sign in with your Apple ID and press Start.
   - On the iPhone, allow the app in **Settings → General → VPN & Device Management**.
   - With a free Apple ID the app works for 7 days. Re-install it from Sideloadly to refresh.

## Work in progress (last updated 21 Nov 2026, 03:00)

**Next PC, in this order (21 Nov 2026):**

1. `git pull` (LFS on). Open the project (Unity 6000.4.1f1, or 6000.3.28f1 - do not commit the `.meta` /
   `ProjectSettings` / `packages-lock.json` changes 6000.3 makes).
2. **Nainital > 2. Build World** (~17 min) - `world.json` changed on 19-21 Nov (Street View fit + Mall Road joins).
   After a build, reopen `Scenes/Nainital.unity` before taking screenshots from a script (the forest is not drawn in
   them otherwise).
3. Vehicles: **all 20 are set up and approved by the owner (21 Nov) and fitted to each model's shape** - open items
   (small cabins vs tall characters, a few hands / feet short, the tool's Snap rule) are in **`docs/VEHICLES.md` section 0**;
   do those first. **Nainital > Vehicle Setup**, Play, to look at them. Blender source: `Tools/newvehicles/NewVehicles_v06.blend`;
   after editing it run `blender -b NewVehicles_v06.blend -P export_unity.py -- Assets\Nainital\Vehicles\New`, then
   `call VehicleSetupWindow.FixMaterials`. Turn Auto Keying OFF in Blender (it broke v05). Details: `docs/VEHICLES.md` 9.0.
4. More Street View routes (Brave extension "Street View 360 Saver", `C:\Users\<you>\tools\streetview-360` on the main PC;
   it saves `Downloads\StreetView360\route_*`): add the route and its stretches to `SEGMENTS` / `STREET_EDITS` in
   `Tools/fit_mall_streetview.py`, run it, **then `Tools/fix_mall_road.py`** (the fit starts again from
   `Generated/world.before_svfit.json` and would undo the clean-up), then Build World. The ~500 m of Mall Road between
   the 19 Nov routes is not fitted yet.
4b. **Asset Store packs (not in git)**: in Package Manager > My Assets, download and import "Motorcycle Interaction
   Animset FREE" (SpectraMotions) and "Human Basic Motions FREE" (Kevin Iglesias), then **Nainital > 6b. Build character
   animator**. Without them riders keep the old procedural pose. Never commit `Assets/MotoInteractionAnimsFREE/` or
   `Assets/Kevin Iglesias/` (the repo is public).
5. Open: car doors, `Steering` objects and `Dmg_*` keys on the new vehicles; the game using the approved setups; Mall
   Road heights (Lower Mall 4-6 m over the water, real ~1.5 m - needs `process_data.py` and the raw data); real shop
   names along the routes.

Start here when you pick the project up on another PC. Build the world first (**Nainital → 2. Build World**). The 17 Nov work was done on a PC with only Unity 6000.3.28f1; it opens the project fine (the repo stays on 6000.4.1f1: the `.meta`, `ProjectSettings` and `packages-lock.json` changes that 6000.3 makes on import are not committed).

### Changed on 21 Nov (early morning): the owner's 20 vehicle setups checked and fitted to the models

Owner: "sab krdi hain maine..check kro", then "krskte ho theek? perfect precision ke saath?", then "iske baad agar koi
dikkat ho ya kuch change ho to push instruction pe daaldena".

| What | Where |
|---|---|
| Every seat fitted to the model: hips on the cushion's top (+ 0.12 m), feet on the floor ahead of it, people moved off open floor / out of the boot / out of each other (bus_roadways 13-17, bus_tourist 19-20, street160 pillion, suv_boxy rear, tallboy driver) | `Assets/Resources/VehicleSetup/*.json` (approved kept), `Tools/vehicles/fit_vehicle_seats.py`; before: `Tools/vehicles/setups_before_fit_21nov/` |
| Each character sits on its own body (seat of the trousers, head under the roof, sole) - measured once per pose from its skin | `Runtime/Characters/HumanoidRig.cs` (`SeatLock`, `NominalButt`, `seatRoof`, `SoleLift`), `HandIK.cs` |
| Seats store the sitting surface and the roof's inside | `VehicleSetupData.Seat.surface / roof` |
| Measuring tools: surfaces along each seat line, side scan, handlebar ends, two-sided cut views with a ruler, body offsets; sheet logs butt-gap / head-room | `Editor/VehicleSetupShots.cs`, `Tools/vehicles/*.py`, `*.ps1` |

What is still open (small cabins vs tall characters - owner to decide; a few hands / feet short; Snap still on the old
rule): `docs/VEHICLES.md` **section 0**.

### Changed on 20 Nov (night): Vehicle Setup - lamp position fields, feet on the floor and the pegs

Owner: "vehicle light transform position ka option nahi diya, or bike pe and car me bethne ka animation bhi theek nahi hai".

| What | Where |
|---|---|
| Lamps: Position / Rotation fields, Snap to body, x <-> 0 | `Editor/VehicleSetupWindow.cs` `LampRows`, `SnapLamp` |
| Feet IK (every seat), FL / FR markers, Snap / Snap everyone / Feet to default, new vehicles snapped by themselves | `Runtime/Characters/HandIK.cs`, `VehicleSetupWindow.cs` `DefaultFeet`, `SnapSeats`; `VehicleSetupData.Seat.feet / footL / footR` |
| Riders lean to the grips when the arms are short; grips found on the bar mesh; rider seated an arm's reach behind them | `HandIK.Reach`, `VehicleSetupWindow.Grips`, `Default()` |
| Check sheets of the tool's vehicles (cut views, hand / foot misses), interior probe, clip muscle report | `Editor/VehicleSetupShots.cs`, `Editor/ClipPoseReport.cs` |
| Riding from real clips: Ride (rider, Lean blend) / PillionRide states, Mount / Dismount / BikeStart, fingers gripping the wheel / grips (Asset Store packs, NOT in git) | `Editor/AnimKit.cs` (`Store`), `HumanoidRig` (`lean`, `HasRideClips`, Grip layer), `.gitignore` |
| Fixed: the car get-in / pulled-out / honk triggers never existed in the animator (since 19 Nov) | `Editor/AnimKit.cs` |

Why the car looked wrong: every Mixamo sitting clip (Driving too) is a chair pose, shins straight down; a car seat is ~0.27 m
over the floor, so the feet went through the floor and out under the car. Not done: the two free Asset Store packs for real
riding / low-seat clips were then imported by the owner (docs/VEHICLES.md section 5): the motorbike pack gives real
riding; the Kevin Iglesias pack has no sitting clips in its free version (only the finger grip is used).

### Added on 19-20 Nov: an unbranded vehicle set, the Vehicle Setup tool, hands on the wheel, Mixamo clips for getting in and out

**Full guide: [`docs/VEHICLES.md`](docs/VEHICLES.md)** (where everything is, the part names in Blender, Blender -> Unity,
how to use the tool, what is left).

| # | What | State |
|---|---|---|
| 1 | 22 original Indian-looking vehicles, no brand, no logo (Flow design -> Tripo 30k -> Blender) | 20 in the main blend **`Tools/newvehicles/NewVehicles_v06.blend`** (21 Nov: v05 + `fix_v06.py` - doors, bike 13, keys, mirrors, the van's rear wheels; every car / bus checked right-hand drive) |
| 2 | One FBX per vehicle in `Assets/Nainital/Vehicles/New` (+ PNG textures, URP materials) | Re-exported from v06 on 21 Nov (the bus doors with their fold / slide clip), imported on the main PC, `FixMaterials` + `ImportCheck` clean |
| 3 | **Nainital > Vehicle Setup** (scene VehicleSetup, Play): every vehicle one at a time with all its people in their poses (Drive / Sit / Ride / Pillion / SideSaddle), entry points, head/tail lamps + night preview, door sliders, damage sliders, wheels spinning forward/back + steering, the "player gets in" preview, Checks for the Blender parts, Save / OK | Compiled and tried in Play on 21 Nov (a car, an SUV, bike 125, the auto, both buses): Checks green, the bus door slider plays the door's own clip, wheel radius from the mesh |
| 4 | Hands on the steering wheel / handlebar grips (`HandIK`, wrist points per seat, ride on the handlebar / `Steering`) | Compiled; the rider's hands follow the handlebar in Play |
| 5 | Bikes, scooties, auto, tirri: the front wheel is a child of the `Handlebar`, which turns about its own raked axis | In the blend and the tool |
| 6 | Mixamo: Entering / Exiting Car, Pulled From Seat, Removing Driver, Honk, Jostled Passenger, Sitting (hands on thighs), Stand To Sit, Sit To Stand | In `Characters/Anims`, triggers in the controller; Mixamo has no motorcycle riding (procedural riding + a new pillion pose) |

**The 20 Nov hand-off ([`docs/VEHICLES.md` section 9](docs/VEHICLES.md)) was done on 21 Nov** - see its section 9.0
for what changed (both buses' doors are on the right, next to the driver: `Door_FR` for both). The retro 350 and the
electric scooty were removed on purpose.

Still to do after that: the game using the approved setups (seats, lamps, passengers, hands, doors, damage, handlebar
steering) and the new set replacing the branded library; car doors, `Steering` objects and the `Dmg_*` shape keys in Blender.

### Changed on 19 Nov (night): Mall Road joins, houses out of the carriageway (`Tools/fix_mall_road.py`)

The Street View fit (below) moved Upper and Lower Mall but not the short links at their ends: at Tallital (the toll plaza)
Upper Mall stopped 5.9 m short of the link from the chowk, at Mallital 6.2 m short - the ground between the road ends was
shaped by none of them (the owner: "terrain ubad khabad near toll plaza"). Now every end is pulled onto the link's end, the
pull easing out over 30 m. 21 generated houses stood in a Mall carriageway or within 0.8 m of its edge: pushed out sideways
(none removed, the house count stays); 24 trees too. A copy of the old data is `Generated/world.before_mallfix.json`.
World built with it (no errors); road-level shots at both junctions are clean.

**Still open on Mall Road** (measured by `Editor/MallProbe.cs`: `call MallProbe.Run` -> `Logs/mall_probe.txt`,
`call MallProbe.Sections` -> `Logs/mall_sections.txt`):
- Lower Mall: across the road the ground drops 1.4 m by 2 m from the centre toward the lake at x 406, 496, 731 - either the
  bank shaping (`WorldBuilder.LowerMallWater.cs`, "under the parapet and the edge of the deck") reaches into the paving, or
  the paving is narrower than world.json's 6 m (LowerHalf 2.4 m). Check in a road-level shot first.
- Upper Mall (a deck mesh): on the hill side the terrain comes up through the deck (0.3 m at 2 m from the centre near
  (708, -468)); near Tallital (x 854-875) the deck floats ~1.2 m over the lake-side ground; the Mall divider overlaps the
  Upper Mall edge by 0.5-1 m.

### Changed on 19 Nov (evening): Mall Road, Tallital and Mallital fitted to the owner's Street View routes

The owner saved three Street View routes as 360 panoramas with their GPS (Brave extension "Street View 360 Saver":
Tallital chowk -> Mall Road 935 m, Mallital 147 m, Bada Bazar -> Jama Masjid 199 m; `Downloads\StreetView360\route_*`).
Each panorama was cut into left / ahead / right views and set beside the game rendered from the same spot.

| # | What | What should be true now |
|---|---|---|
| 1 | Mall Road position | Upper and Lower Mall lie on the Street View car's track (the OSM lines ran up to 8.7 m east, so the camera stood on Lower Mall where the real Upper Mall is); the shop row moved with them |
| 2 | Tallital to Mall Road | The Army Rest & Recoup Centre: whitewashed wall along the road, the gate with the red "ARMY REST & RECOUP CENTRE NAINITAL" board, low green-roofed barracks; no shop row |
| 3 | Upper Mall 154-230 m, 364-513 m (from the chowk) | Mossy rubble retaining walls at the road, trees on the bank behind, no shops |
| 4 | Mall trees | Old broadleaves close together on the strip between the roads (123, was 51) and on the bank |
| 5 | Lower Mall lake side | City Joggers Park open gym, swan pedal boats, the Rotary Selfie Point pavilion (red tin roof, blue arch) |
| 6 | Mallital, the road west past the Flats | A garden of trees on the south side instead of shops; the clock-tower building white with a red roof |
| 7 | Bada Bazar | Left side open over the car park; right side four and five storeys, peach / pink / cream; the Flats ground grey grit |

Not done: road and ground heights (Lower Mall stands 4-6 m over the water, the real one ~1.5 m; the Bada Bazar car park
is level with the road) need `process_data.py` and the raw data; real shop names; ~500 m of Mall Road between the routes.

Where: `Tools/fit_mall_streetview.py` (SEGMENTS: which road the car was on; STREET_EDITS: drop / low / tall / fill houses
and trees per stretch; it always starts from `Generated/world.before_svfit.json`, run it, then Build World),
`Editor/LandmarkBuilder.ArmyRest.cs`, `Editor/LandmarkBuilder.LowerMallSights.cs`, `WorldBuilder.Mall.cs` UpperWalls,
`WorldBuilder.LowerMallWater.cs` (tree density). After a world build, reopen the scene before taking screenshots from a
script, or the forest is not drawn in them.

### Added on 19 Nov: Eco Cave Garden, Lover's Point, Suicide Point, Khurpatal Lake View Point, Mansa Devi, Sariyatal, Woodland Waterfall, black main roads, Raj Bhavan, the zoo gate

The owner: "nainital eco cave park bnna hai theek se, abhi to khraab sa hai, iske andr ka map dhundho, bahut badi cave
hain, 4-5 hain" (photos + the park's guide map); then Street View photos of Lover's Point with a sketch on the satellite
("orange means stairs to down, green is plain part, baaki buildings satellite view mein").

| # | What | What should be true now |
|---|---|---|
| 1 | Eco Cave Garden gate (Kaladhungi road) | Stone pillars, a green iron arch with the "ECO CAVE PARK / K.M.V.N. NAINITAL" board, a stone wall along the road, the ticket counter and the price board (adult Rs 100, child Rs 60) |
| 2 | The six caves | Apes, Flying Fox, Bats, Porcupine, Tiger and Panther caves: winding walk-through tunnels (~1.7 m wide, ~2.3 m high, 15-20 m) inside piles of grey boulders on the upper ridge, a green ENTRY / EXIT board at each mouth, two bulbs (with light) inside each |
| 3 | Walks | Stone steps with green chain railings up from the gate; flagstone walks and steps with rusty red railings to the fountain, three summer houses, cafeteria, photography kiosk (Kumaoni dresses), manager's hut, bull ride, toilets, generator room; a ridge walk past the cave exits |
| 4 | Dressing | A Kumaoni villager statue with a blue drum by the junction, a bamboo hut with a lantern, bamboo stands, oaks / deodars / pines among the walks (60 added), the direction board |
| 5 | Lover's Point (the hairpin on Kaladhungi road) | A wide verge with the Ghora Chaalak Seva Samiti hut ("WELCOMES YOU LOVERS POINT"), the green shade net, blue pipe railings, benches on a low stone wall, the horse-ride boards (6 point tour Rs 760); across the road a stone-walled horse terrace with tarp shelters, steps up and signs |
| 6 | Suicide Point | A walk from the verge's south corner along the tin-roofed tea sheds (Subhan Khan tea stall): level, steps down, level, steps, a landing with a lamp, a last flight to a railed round ledge over the valley, 11 m below the road |
| 7 | Khurpatal Lake View Point (the hairpin's tip between Lover's Point and Sariyatal) | A gravel pull-off round the tip with snack stalls under blue tarps, a big tarp over benches, concrete bench blocks, a green board; a dirt path straight out along a spur, bending towards Khurpatal, then rough rock steps down to a bare earth clearing among Himalayan cypresses, with the many-trunked cypress of the photos; trees all along (no railing, as in the photos) |
| 8 | Mansa Devi Mandir | On the knoll inside that hairpin, just above the view point: white shrine, white pyramid shikhara with a painted sun, "JAI MATA DI" in red, red collapsible gate and window grille, marigold garlands, red flag, a lamp |
| 9 | Sariyatal | The north end is the park (the water was cut back): lawn, tables and plastic chairs, umbrellas, a green-roofed cafe, a Pepsi shed, slide, swings, seesaw, flags, two fenced settling basins, a stepped weir, a crash barrier on the road above; a paved west shore walk with a blue railing, snack stalls (Laxmi Fast Food Centre) and chairs between it and the road; zorb balls and paddle boats; a small dam at the south tip. The DEM had sunk the west road up to 12 m under the water: the shore and road are raised to the lake before the roads are levelled |
| 10 | Woodland Waterfall | Gate on the lower road (stone walls, green ticket counter "RS 50", a log arch "WOODLAND WATERFALL", a red bench, a solar lamp, NO PARKING), stone steps with green railings and red flower pots up the ravine to the lower and the upper bathing pools (concrete, wire fence), a loop through the cypresses back down; the stream from the lake's south end, a cascade, two ~3.5 m falls from rock ledges, runs over stones to the road; ~110 trees; restaurant boards (Uttaranchal, Shubham) across the road |
| 11 | Main roads | The roads Google draws yellow (Kaladhungi road, Mall roads, Haldwani / Bhawali highways, the Nainital Bypass) are black tarmac with white edge lines and a dashed centre line; every other road stays the normal grey (the DSA road and the Tallital patch moved to the grey channel) |
| 12 | Raj Bhavan | Rebuilt on its real footprint from photos: dark grey rubble stone, cream string courses and Gothic windows, battlements, four towers with corner turrets (the tricolour on one), green roofs, a three-arched porch with a red railing towards the town, lawns, a gravel drive, hydrangeas, palms |
| 13 | Nainital Zoo | The gate from the photo: a concrete portal with "BHARAT RATNA PT. GOVIND BALLABH PANT / HIGH ALTITUDE ZOO, NAINITAL", green iron gates, grilles, a ticket counter; behind it a bear pit with a stone den and a deer paddock in chain-link (no animals yet: no animal models) |
| 14 | Naina Peak, Tiffin Top, Land's End | Photo pass: flagstone ground walled down to the slope, grey rock outcrops, timber railings, benches, Dorothy's Seat in dressed stone (the flat-coloured boulders and fences are gone) |

How it was laid out: the guide map fitted to the satellite (park frame along the road, origin at the gate; the six caves on
the ridge where the satellite shows the rock); Lover's Point from the owner's photos and sketch, the satellite's buildings
shifted onto the game road (the mapped road runs ~12 m east of the one in the imagery). The terrain there is a 33 degree
slope, so the ground under the verge, the walk and the sheds is shaped into the shelf the real place stands on - after
the roads are levelled. No horses or zoo animals yet (no models). Teleport: "Eco Cave Garden", "Lover's Point", "Suicide Point", "Khurpatal Lake View Point", "Mansa Devi Mandir", "Sariyatal", "Woodland Waterfall", "Raj Bhavan".

Where: `Editor/LandmarkBuilder.EcoCave.cs` (plan, caves, walks, huts, trees), `Editor/LandmarkBuilder.LoversPoint.cs`
(verge, terrace, walk, sheds, ledge, ground shaping), `Editor/LandmarkBuilder.KhurpatalLake.cs` (pull-off, spur walk, clearing,
the many-trunked cypress, Mansa Devi), `Editor/LandmarkBuilder.Sariyatal.cs` (park, shore, Woodland Waterfall),
`Editor/LandmarkBuilder.Top10.cs` (Raj Bhavan, zoo gate, peaks), `WorldBuilder.Concrete.cs` IsMainRoad (black roads), `Editor/LandmarkBuilder.SiteKit.cs` (photo-material meshes in metres,
walkways and steps, pipe and chain railings, boards in 3D letters, tin huts, terrain shaping / painting, the grey rock and
light tin scans made from the brown ones: `Data/PBR/rock_grey_*`, `tin_light_*`). Shots: `Shots/eco_*.png`, `Shots/lp_*.png`.

### Changed on 18 Nov (night): Mixamo animations, vehicle physics, smarter traffic, seats, tyres

The owner: legs not right on the player and the people ("avatar problem?"), take every animation from Mixamo; AI traffic
dumb (does not see walls); a moving vehicle jolts when touched, cars hit each other like balloons, an empty car does not
react to a hit; drivers sit in the wrong place; tyres badly cut on all vehicles.

| # | What | What should be true now |
|---|---|---|
| 1 | Walk, run, jump, fall, swim, sit, drive, die, get knocked down and up | Real Mixamo clips on every rigged character. The run cycles properly (it froze before) |
| 2 | Punch / kick / hit / dodge roll | Mixamo jab, cross, hook, roundhouse kick, hit reaction, roll (Combatant's timing kept) |
| 3 | Police | Aimed-pistol idle / walk / run, a shot recoil; the stretcher, hands up and the guard stay as arm poses |
| 4 | Women | Their own walk and run (Female Locomotion pack) |
| 5 | Crash a car into another or into a parked one | They push each other by their masses (WagonR 950 kg into a parked Bolero 1700 kg at ~14 m/s: the Bolero moves 4.5 m/s, no flying, no wild spin); a parked / empty car is a real body that wakes when hit |
| 6 | Walk into a moving car | The car is not jolted; you are moved aside (slow) or knocked over (fast) |
| 7 | Traffic | Slows for the sharpest bend ahead in time, keeps ~2 s from the car in front, brakes for a wall in its path, eases off walls at the sides, moves left for an oncoming car on a narrow road, backs out when it noses into something |
| 8 | Drivers (townspeople, police, the player) | Seated at the wheel (right-hand drive) by each model's front axle, head under the roof, with the Driving clip; the player is seen in the car now |
| 9 | Tyres | Cut again on the Ambulance, Tourist Bus, Tourist Coach, Honda Amaze (had none) and Tata 407; the others were right and are unchanged |

Why the legs were wrong: the avatars are fine (Shots/pose_*.png: every character walks right with only the Animator).
Two things in our code: the procedural pose (falling, sitting, kick legs) overrode the clips, and the run was a blend of
a 0.7 s run cycle with the 2.6 s multi-stride Jogging clip, which froze the legs (Shots/film_player_run.png).

Where: clips in `Assets/Nainital/Characters/Anims` (57 Mixamo FBX: Locomotion, Female Locomotion, Pistol/Handgun
Locomotion, Action Adventure, Gestures Basic packs one motion at a time, plus single motions; fetched with the
google-flow-automation skill's Mixamo scripts in Brave), `Editor/AnimKit.cs` (import settings, builds
`Resources/NainitalCharacter.controller`: Base / Action / Upper layers), `HumanoidRig` (Full mode, SeatLock),
`CarController` (physics), `TrafficCar` (BendSpeed, Feel, Oncoming, MakeWay, backing out), `Vehicles/VehicleSeats.cs`,
`Tools/recut_wheels.py`. Checks: `Editor/CharacterPoseSheet.cs`, `Editor/AnimFilm.cs` (frames of a character moving in
play), `Editor/VehicleTest.cs` (Crash, Seat, TrafficStats, AttackShot).

### Added on 18 Nov (evening): police, the 108 ambulance, bikes and scooties in the traffic

The owner asked for a proper police and ambulance system: when someone dies the police come, then the ambulance; the traffic stops; the ambulance takes the body away. Three police levels only. Then: bikes and scooties in the normal traffic too, and a siren and roof lights on the police and the ambulance. And (after the first test): stopped next to the police in a car, they get out and shoot from outside; driving off, they get back in and chase.

| # | What you do | What should happen |
|---|---|---|
| 1 | Hit someone (punch, or knock them with a car) | 1 star. One constable comes on a bike (6 in 10) or running, stops ~9 m off, points his pistol: "Ruk ja! Haath upar!". He does not shoot. Stay calm ~12 s: "Chal nikal! Agli baar seedha andar." and he leaves |
| 2 | Kill someone | 2 stars. A police Bolero with two men (white, "UTTARAKHAND POLICE" in 3D letters, red-blue light bar, siren) |
| 3 | 2 stars, on foot, stand still when they reach you | Arrested: hands up, then the Mallital police (fine Rs 300; 1 star Rs 100, 3 stars Rs 600) |
| 4 | 2 stars, run, fight back, or hit a policeman | They shoot (pistol, ~7 health a hit) |
| 5 | 2 stars, sitting in a stopped car | They get out, guns on you: "Gaadi se bahar niklo!". Step out and stand: arrested. Stay in ~4 s: they shoot from outside (you can die) |
| 6 | Wanted and driving off | They run back, get in and chase; the passenger fires from the window while both are moving |
| 7 | Kill 3 people, or 2 policemen | 3 stars: two Bolero and a bike, shoot on sight |
| 8 | Die while wanted | "BEHOSH!": BD Pandey District Hospital, bill Rs 200 |
| 9 | Out of their sight 30 s (2 stars) / 45 s (3 stars), once they have seen you | "Police ne peecha chhod diya". Stars blink at the top while they have lost you |
| 10 | Anyone dies (any cause) | Traffic stops ~10 m short of the body; people stop and stare; a police Bolero comes with its siren and one constable guards the body; then the 108 ambulance comes, two of its crew carry the body to it on an orange stretcher, it drives off with its siren; the traffic goes on |
| 11 | Normal traffic | Splendors and scooties with riders keep to the left edge; cars and bikes pull over for a siren. Hit one with a car: the bike tumbles, the rider is thrown (may die) |
| 12 | HUD | Three stars at the top centre; police on the radar flashing red/blue (pinned to the edge while wanted), the ambulance white |
| 13 | Cheats (phone: Cheats panel) | POLICE = one more star, SHANTI = no stars |

Checked in play mode on 18 Nov (bridge `call PoliceTest.*`, reports in `Bridge/police_report.txt`): accident → police scene car from 220 m, ambulance from 210 m, body carried and taken, traffic held then released; 1 star → bike constable aimed and left; 2 stars → car deployed 65 m off (stuck behind traffic), both men walked up, arrest, respawn at the Mallital police; 3 stars → standing still you last ~30 s against one constable; driving into a scooty knocked it over and killed the rider (2 stars, an incident). No errors logged. Pictures: `Shots/play_acc*.png`, `play_shoot*.png`, `play_bike*.png`, `play_box*.png`.

How it works and where (`Assets/Nainital/Scripts/Runtime/Police/`):
- `WantedSystem` (stars, crimes, arrest, hospital), `PoliceManager` (sends units, makes people and vehicles), `PoliceOfficer` (foot AI + pistol shots), `PoliceCar` / `PoliceBike` (`PoliceRide`), `RouteDriver` (drives a CarController to a goal along the roads, goes round cars, asks traffic to make way), `RoadRoute` (A* over the road points), `IncidentManager` + `Casualty` (bodies, traffic hold, crowd), `Ambulance` + `Medic` (stretcher), `EmergencyLights` (light bar + siren), `Pistol`.
- Traffic two-wheelers: `Runtime/Vehicles/BikeRide.cs` (moves a bike on the ground, leans, crashes) and `TrafficBike.cs`; `TrafficManager.maxBikes` (8 PC, 4 phone). Townspeople now have 60 health (`NpcWalker`).
- Models: police_1 / police_2 (the kit's PoliceGuys, cut to 18k / 6k triangles by `Tools/decimate_rigged_fbx.py`, `Characters/Models`), Ambulance108 (`Vehicles/Models`, weight 0 = never in traffic), Splendor and Scooty (`Vehicles/Bikes`, `Tools/make_vehicle_lods.py`). After changing any of them: Nainital → 4. Import rigged characters, 7. Import vehicles, then **7b. Police and ambulance kit** (`Editor/PoliceKitBuilder.cs`: lamp materials, lettering measured on the models → `Assets/Resources/PoliceKit.asset`).
- Sounds: Mixkit siren (police: "Police siren", ambulance: "Ambulance siren US") and "Game gun shot", fetched by `Tools/fetch_sounds.py` (`ONLY=siren_police,siren_ambulance,gun_pistol`).
- Known limits: police cars can be slow to get through Mall Road crowds (they then stop short and come on foot); bikes have no separate wheels (they do not spin); the player cannot ride a bike yet.

### Added on 17 Nov (night): rooms behind the glass, Mall Road shop fronts by kind

The owner asked for a fake interior shader on every building and shop, ground floor only, "pehle check kro heavy to nahi"; then photos instead of painted rooms, the crowd inside the photos (Indian, eating in restaurants, standing in shops), Mall Road shops of different kinds as in the reference videos (restaurants, shops, road shops, rooftop cafes) with a random front look, and the low-poly goods taken off the Mall Road fronts.

| # | Where (x, z) | What should be true now |
|---|---|---|
| 1 | Any ground-floor window or open shop front, e.g. Tallital (626, -1047) | A room photo behind the glass that slides behind the frame as you walk past; curtains, grills and frames stay in front. Upper floors unchanged |
| 2 | Upper Mall, Gurdeep / Moonlight stretch (556, -245) | Fronts cut into ~4 m shops of different kinds: glass fronts with people eating (restaurants, cafes), open shops with people standing (gifts, woollens, bags), hotels with a canopy and two pots, some shutters down; no goods tables or hanging shawls outside |
| 3 | Upper Mall, banks stretch (380, -58) and Nanak / Bata stretch (207, 52) | Bank, optician, books, mobiles; restaurant, footwear, sweets - the kinds video 02 shows there |
| 4 | Roofs above (560, -240) | Rooftop cafes (8): deck, rail, tables and chairs under umbrellas, string lights that glow at night |
| 5 | Mall Road at night | Shop interiors lit warm; dark windows stay dark |
| 6 | Settings → Quality → Low | Rooms behind ordinary windows switch off (Mall Road shop glass stays) |
| 7 | Frame rate | Unchanged. Measured on this PC (batch, GPU-bound 4K): rooms on vs off +0.00 to +0.06 ms (0.0-0.2 %) at five street views; rooms cover 0.1-3 % of the screen. Cost: one texture read + ~25 ALU per glass pixel, nothing per vertex, +1 draw call per Mall Road map cell (shop glass), +2.8 MB texture on the phone (ASTC 8x8) |

Built (18 Nov, 01:00): Mall Road's 110 building fronts → 169 shop units (80 glass fronts, 56 open shops, 33 shut, 8 skipped where two houses of the data overlap), 19 kiosks on the steps of shut shops, 8 rooftop cafes. Checked in shots `Shots/mf*_shop*.png`, `mf1080_roof.png`, `19_mall_upper_south_day.png`.

How it works and where:
- Photos: 32 rooms made in Google Flow from `Tools/interiors/prompts.json` (the `naini-flow-rooms.js` script of the google-flow-automation skill), watermark painted out and packed by `Tools/interiors/pack_rooms.py` into `Assets/Nainital/Data/Interiors/RoomPhotos.png` (8 x 4 tiles; the order is the room index, `Atlas.Room*`). To change one: edit its prompt, delete `Tools/interiors/raw/NN_name.png`, run the script, then `pack_rooms.py`, then Build World.
- Shaders: `Shaders/NainitalRooms.hlsl` (the photo on a plane ~2 m behind the glass: plane parallax), used by `AtlasForwardPass.hlsl` (ordinary ground-floor bays: `BuildingKit.FacadeWall` shifts their atlas UV by whole atlas widths, `Atlas.InteriorBay`, and the glass mask is the atlas alpha painted by `FacadeArt`) and by `ShopGlass.shader` (Mall Road shop glass; room index in the vertex colour, `MeshBuilder.QuadTagged`). The atlas textures now repeat (needed for the shifted UVs).
- The look (photo size / zoom, height, side shift, depths, light) is the owner's to set: **Nainital → Fake Interior Look** (sliders, live in the Scene and Game views, play mode too; saved in `Assets/Resources/InteriorLook.asset`, which the game applies at start - `Runtime/World/InteriorLook.cs`).
- Mall Road: `Editor/WorldBuilder.MallFronts.cs` (video 02 timeline → shop kind per unit, the fronts, kiosks), called from `MallShopDressing` in `WorldBuilder.Mall.cs`; rooftop cafes in `BuildingKit.RooftopCafe`, chosen in `WorldBuilder.cs`. `Editor/InteriorCost.cs` (bridge `call InteriorCost.Run`) measures the cost and saves on/off pictures `Shots/int_*.png`.

### To test on the iPhone (IPA of 18 Nov, 03:29)

`Builds/NainiTA-unsigned.ipa` (also on the `ios-build` release), install with Sideloadly. Everything of 17-18 Nov is in it.

| # | Where (x, z) | What should be true |
|---|---|---|
| 1 | Upper Mall (556, -240) and (207, 55) | Each shop has its own front and its own 3D name board; the room photo behind the glass slides as you walk; people eating in restaurants, standing in shops |
| 2 | Any ground-floor window, e.g. Tallital (626, -1047) | A room behind the glass; upper floors unchanged |
| 3 | Mall Road at night | Shop interiors and some name boards lit |
| 4 | Roofs above (560, -240) | Rooftop cafes: umbrellas, tables, string lights |
| 5 | Capitol (0, -30), Naina Devi gate, police booths, road signs | Solid 3D letters in their own fonts |
| 6 | Settings → Quality → Low | Rooms behind ordinary windows off (Mall Road shop glass stays) |
| 7 | Frame rate on Mall Road | Not lower than the 17 Nov IPA |

### Added on 18 Nov: solid 3D shop names in a font by kind

Every sign that had flat text now has solid 3D letters (owner: "tagda 3D name, different stylish font based on their category"): restaurants and cafes in a script (Pacifico), sweets and bakeries rounded (Lilita One), hotels in Roman capitals (Cinzel Decorative), crafts and gifts Indian-style (Yatra One), woollens a heavy serif (Rozha One), clothes tall (Bebas Neue), banks / chemists / opticians / police / road signs bold (Anton), the Capitol art deco (Limelight). Some letters glow at night. Each Mall Road shop has its own board and a name of its kind; kiosks too. Check at (207, 55) and (556, -240) on Upper Mall, the Capitol (0, -30), the welcome gate on the highway. Data: `Tools/fonts3d/make_fonts3d.py` → `Assets/Nainital/Data/Fonts3D/*.json`; letters: `Editor/Glyph3D.cs` (styles and names per kind).

### To test on the iPhone (IPA of 17 Nov, 15:19)

Install `Builds/NainiTA-unsigned.ipa` (or the `ios-build` release) with Sideloadly. If anything is still wrong, take a bug report on the spot (**Bug** button) and pull it with `Tools/pull_bugs.py` (below).

| # | Where (x, z) | What should be true now |
|---|---|---|
| 1 | Upper Mall walk by the Boat House Club (118, 104) | No ground or grass coming up through the walk |
| 2 | Anywhere on slopes and roads | The player's feet stay on the ground drawn; no stepped, terraced ground |
| 3 | Help centre at the Upper/Lower Mall split (100, 107) | Stands on the divider's nose in the middle of the road; the Upper Mall walk meets the junction flush |
| 4 | Road edges everywhere, e.g. Mall Road (77, 118) | Sharp, straight edges along the road; where two surfaces overlap the main one is on top (no pink blobs) |
| 5 | Colvin Road (95, 121) | No trees on the road; smooth climb |
| 6 | DSA road, Mall Road down to the Flats (81, 107) | Grey cement, one even slope |
| 7 | Bandstand, ahead of the T where the path from the Flats meets the lakeside path (45, 16) | At the water's edge, steps toward the T, no white railing or bench across it |
| 8 | Lakeside path, Capitol to the bandstand (22, -36) | Low and flat, no hump; the children's park fence complete on its wall |
| 9 | Walk above the Capitol, by the stairs (-2, -14) and the park corner (1, -13) | Flat to the wall, no trench or hole, the player never gets stuck |
| 10 | Lower Mall by the Boat House Club (153, 74) | Smooth, no dip and rise; the lake-side line lies on the road |
| 11 | Frame rate | Not lower than the 16 Nov build (the phone terrain is now a CPU mesh, see below) |

### Fixed on 17 Nov (from the iPhone reports of 16 Nov and the owner's screenshots)

| What | Code |
|---|---|
| **Phone terrain = collider.** URP decodes terrain heights with `UnpackHeightmap(real4)`, and on phones `real` is half: at our 1437 m height range every height snapped to ~0.7 m terraces (ground through the Upper Mall walk, roads up and down, feet off the ground, player stuck). The terrain shader now decodes in float in every pass, and on phones the terrain is drawn non-instanced (a CPU mesh from the same heights as the `TerrainCollider`), pixel error 1. | `Shaders/NainitalTerrainHeight.hlsl`, `NainitalTerrain.shader`, `Runtime/Core/GameManager.cs` |
| Upper Mall deck: the ground under it is lowered 40 cm below its top (the deck was left out of the carve: "0 lowered under slabs"), and at both ends the junction rises to meet it over 7 m | `Editor/WorldBuilder.Carve.cs` (`DeckEndRamps`), `WorldBuilder.cs` |
| **Road mask = signed distances** to each surface's edge (±4 m over 0..255), from 4×4-sampled coverage with Gustavson's anti-aliased edge distance. The shader cuts the edge sharp at 0, so edges stay straight at 4096 on phones (ASTC 4x4 now, not 6x6). Overlaps: cement over asphalt over grey pavers over red pavers, no blending. The plain coverage goes to `Generated/RoadCoverage.png` for the Unreal export. | `Editor/WorldBuilder.RoadMask.cs`, `Shaders/NainitalTerrainRoads.hlsl`, `Runtime/World/TerrainRoads.cs`, `Editor/UnrealExport.cs` |
| DSA road: grey cement from Mall Road to the Flats, one grade (smoothed ±24 m about the line between its ends) | `Editor/WorldBuilder.cs` (`IsDsaRoad`) |
| Mapped trees are cleared off every road (Colvin Road had trees on it) | `Editor/WorldBuilder.cs` (`BuildTrees`) |
| Tourist help centre on the divider's nose; bandstand moved to the water's edge ahead of the T (`BandstandSpot`), the lake railing, the ghat balustrade and benches keep clear of it | `WorldBuilder.MallitalEntry.cs`, `WorldBuilder.DataFixes.cs`, `WorldBuilder.Mallital.cs`, `WorldBuilder.Lakefront.cs` |
| Lake side of the children's park: the lakeside path one grade from the Capitol's level to the bandstand T (it had a 1.2 m hump); the park fence stands on the wall's coping | `WorldBuilder.CapitolGround.cs`, `WorldBuilder.Lakefront.cs` |
| Retaining walls: a flat 1.5 m stone ledge with a collider on the high side of the walk wall, the corner wall and the park walls (the low ground under a wall left a trench beside it); the park edge facing the walk strip has no wall | `WorldBuilder.CapitolGround.cs` (`InnerLedge`) |
| Lower Mall's level smoothed over ±15 m (it dipped 1.4 m by the Boat House Club); the ground follows the road 0.8 m past its lake edge | `WorldBuilder.Mall.cs` |

Everything below was built and checked in renders on 15–16 Nov, in Unity 6000.4.1f1.

In-game positions are metres: x = east, z = north. A Scene view camera looking at the point is enough to check each one. With the editor open, `Bridge/cmd.txt` takes commands (`refresh`, `world`, `shot name;x,y,z;lookX,lookY,lookZ;hour`), see `Editor/EditorBridge.cs`; shots go to `Shots/`.

Helper scripts (Windows, run from the project folder; paths are worked out from where the script is):

| Script | What it does |
|---|---|
| `Tools/pipeline/run_photobatch.ps1` | One batch-mode Unity run (BelowNormal) for the steps in `Bridge/batch.txt`: `world`, `call Type.Method`, `shots 03,38`, `shot name;x,y,z;lx,ly,lz;hour[;g]` (`g`: heights over the ground). Prints the results; log in `Logs/photobatch.log`. The editor must be closed. |
| `Tools/pipeline/make_ipa.ps1` | iPhone build with no Mac: Unity iOS batch build → zip → GitHub release `ios-build` → the `ios-ipa.yml` workflow on GitHub's Mac → `Builds/NainiTA-unsigned.ipa`. The editor must be closed. Uses the Unity version in `ProjectVersion.txt` (Hub folder on C: or D:). Upload and workflow: `ios_ipa_chain.sh` with Git Bash, or else `ios_ipa_chain.ps1` with the GitHub CLI (`gh auth login` as the repo owner). Needs the iOS module and Python. About 25–35 min; progress in `Logs/make_ipa.log`. |
| `Tools/pipeline/install_apple_for_sideloadly.ps1` | iTunes, iCloud, Sideloadly, and Apple Mobile Device Support (without it Sideloadly says "no Apple devices detected"). If winget refuses Sideloadly with "installer hash does not match" (its winget entry is old), download `SideloadlySetup64.exe` from sideloadly.io and run it with `/S`. |
| `Tools/pull_bugs.py` | Pulls new bug reports from the iPhone over USB: `Tools\.venv\Scripts\python.exe Tools\pull_bugs.py BugReports`. Then **BugReview** (`Editor/BugReview.cs`, set env `NT_BUGS=2026-11-17` for one day's reports) renders each report's camera, an eye-level and a top view into `Shots/bug_*` |
| `Tools/pipeline/fix_ipa_frameworks.py` | Makes every framework in the IPA signable by Sideloadly and checks it (`IPA frameworks ok`). Unity 6000.4's pre-signed FAT `UnityRuntime.framework` otherwise fails on the phone: "ApplicationVerificationFailed ... 0xe8008001". `make_ipa.ps1` runs it after the download. |
| `Tools/satcheck/*.py` | Satellite comparisons (Esri World Imagery tiles cached in `Shots/satcompare/_tiles`): `satcompare.py` (the game's top view from `Editor/TopView.cs` beside the photo), `footprints_on_sat.py`, `capcrop.py x z half out.jpg` (data outlines on the photo), `gamecrop.py`, `sheet.py` (contact sheet of shots) |
| `docs/TRACKER.md` | The work log, day by day: what was tried, measured and left open |

### Fixed and checked on 16 Nov

| What | Where (x, z) | Code |
|---|---|---|
| **Roads are part of the terrain**, not meshes: every carriageway, footpath, junction and paved area is painted into one road mask (8192², 0.73 m a texel; RGBA = asphalt, cement, grey pavers, red pavers) that the terrain shader reads. Junctions blend with no seam; edges fray into the earth; grime over everything; damp patches; wet and puddles in the rain (`TerrainRoads` follows `WeatherSystem.Rain`). Dirt tracks go into the terrain's dirt layer. Phones get the mask at 4096 (ASTC 6x6). | everywhere | `Shaders/NainitalTerrain.shader`, `NainitalTerrainRoads.hlsl`, `Runtime/World/TerrainRoads.cs`, `Editor/WorldBuilder.RoadMask.cs` |
| The ground under a road is set to the road's surface (its height smoothed over ±8 m; averaged where roads overlap; two rings of samples beside it follow part way), right after the roads and again at the end. The Jama Masjid road is smooth. Still meshes: the Upper Mall deck (a two-level road with a wall between; a heightfield cannot make the step), its footway and parking; the lake road's white lines are a thin decal. | Jama Masjid road (-140, 40) | `Editor/WorldBuilder.Carve.cs`, `WorldBuilder.cs` (`BuildRoads`) |
| Capitol: its OSM outline stood 1.3 m inside the walk to Naina Devi; it is pushed clear. The children's park is a terrace at the walk's level on 1.5 m stone retaining walls; the lane, the Capitol, the Video Games hall and the lakeside path are one low level, paved red; the walk and the strip by the park are grey pavers. The stairs are out of the park: in the lane's corner, along the walk wall, against the park's wall. The walls are 1.5 m thick with low ground under them, because the terrain has a sample every 1.46 m and a thin wall left a ramp at its foot. | Capitol (0, -40) | `Editor/WorldBuilder.CapitolGround.cs`, `WorldBuilder.Capitol.cs`, `WorldBuilder.Mallital.cs` |
| Video Games / Zoom Land hall: from the Capitol to a narrow lane above the lakeside path (about 14 x 33 m, as in the satellite photo). "Capitol Boat Stand" jetty with boats below it. | hall (15, -45), boats (34, -60) | `Editor/WorldBuilder.Capitol.cs`, `WorldBuilder.Mallital.cs` |
| Lakeside lamps, benches and food stalls stay off roads: a lamp moves into the strip between the road and the railing, or is left out | Thandi Sadak, Tallital | `Editor/WorldBuilder.cs` (`BuildPromenade`) |
| `Editor/ShaderCheck.cs`: bridge `call ShaderCheck.Run` writes the project shaders' compile messages to `Logs/shadercheck.txt` | — | — |

### Fixed and checked on 15 Nov

| What | Where (x, z) | Code |
|---|---|---|
| Lake light streaks at night: long reflections from the shore towards the viewer, worked out from the camera's and the lamp's height over the water (they were 16–48 m and looked like dots from across the lake) | Mallital and Tallital shores | `Shaders/LampGlow.shader`, `Editor/WorldBuilder.LampGlow.cs` |
| Night preview shots show the lamp halos and streaks (`_NtLampGlow` was only set in play mode) | — | `Editor/PreviewCapture.cs` |
| Flats steps: the ground is cut along the whole run. `InStands` measured the distance wrongly next to a short segment, so strips beside the steps counted as road: they were raised to the road and the steps sat in blocky pits. | (-143, 45) | `Editor/WorldBuilder.FlatsEdge.cs` |
| Flats grit at the south end of the steps: no more square holes. The steps' line no longer claims ground past its first point, the cut box covers all it claims, and the ground eases down to the cut at most 10 cm a sample. | (-140, 9) | `Editor/WorldBuilder.FlatsEdge.cs`, `WorldBuilder.Flats.cs` |
| High-street colours: three strong wall colours for the high streets only (purple, blue, rose pink), in free atlas cells; the town's random colours are unchanged | (-527, 522) and (825, -1145) | `Runtime/Core/Atlas.cs` (`StrongWallPalette`), `Editor/TextureKit.cs` |
| High-street grass gaps: the street wall is built wall to wall in 1.5 m steps, a smaller building where a full one does not fit, rectangle overlap tests (it was one every 8 m with circle tests about 9 m apart, so every other one failed). 292 street-wall shops, was 108. | same | `Editor/WorldBuilder.HighStreet.cs` |
| Ropeway: every pylon went up on top of the Snow View station (two in the same spot). The stations' ends are no longer counted; one tower now stands 47 m below the upper station, the rest of the span clears the ground by 12 m. The build log lists the towers. | tower (543, 487) | `Editor/LandmarkBuilder.cs` (`BuildRopeway`) |
| New HUD, the game's own look (`UI/HudStyle.cs`): radar ringed by health with a compass, place / time / weather card, money in rupees with each change, mission card and timer, sliding notifications, key prompt, arc speedometer with gear and damage, red edges when hurt, restyled dialog, help, pause (language moved in here) and full map. Rajdhani font (SIL OFL) in `Assets/Resources/Fonts`. Checked on a 1920x1080 PC view and a 2316x1080 phone view (`Editor/HudCheck.cs` puts the HUD in each state for the bridge's `capture`). | — | `Runtime/UI/HUD.cs`, `Minimap.cs`, `TouchControls.cs` |
| Photoreal pass: no depth of field (it made the town a tilt-shift miniature), more contrast and colour, warm sun and cool shade, lower daylight ambient, lighter haze, brighter worn asphalt, softer shade; the snow range lower, smoothed and deep in the haze; greener forest floor, dark mossy rock | everywhere | `Editor/WorldBuilder.cs` (post profile), `Runtime/World/DayNightCycle.cs`, `Editor/TextureKit.cs`, `Editor/WorldBuilder.FarRidges.cs` |
| Lake: greener deep colour; broad slow ripple patches at every distance so the sun's glitter is broken up, not straight stripes; preview shots render the lake probe | lake | `Shaders/Water.shader`, `Editor/PreviewCapture.cs` |
| Forest: `HillForestFill` plants the open hillsides round the lake (about 21,500 trees; the build log says why spots were refused), and smaller trees cling to the 63-80 degree face over the west shore; far impostors 1.4x wider so the canopy closes from across the lake | hills | `Editor/WorldBuilder.HighStreet.cs`, `Editor/TreeKit.cs` |
| Lake wall capped at 3 m over the water: on the steep west shore it climbed 10-45 m with the slope and read as a khaki band from Mallital | west shore | `Editor/WorldBuilder.cs` (`BuildLakeEmbankment`) |
| `Editor/PhotoBatch.cs`: one batch-mode Unity run for a list of steps in `Bridge/batch.txt` (`world`, `call Type.Method`, `shots 03,38`), then it quits; `Tools/pipeline/run_photobatch.ps1` runs it at BelowNormal and prints the results. Use it instead of leaving the editor open. | — | — |
| Positions: `Editor/RoadAudit.cs` lists anything with a road through it. Landmarks and civic buildings are pushed off roads (`LandmarkBuilder.ClearOfRoads`); St. Francis Church fits between its two roads; lookouts are ledges at their road's level; the "above Naini lake level" floor only applies near the lake (it put the Khurpatal View Point on a 260 m tower) | Khurpatal (-2490, -1755), church (842, -765) | `Editor/LandmarkBuilder.cs`, `LandmarkBuilder.Worship.cs`, `WorldBuilder.Goan.cs` |
| Roads by the lake (Mall Road, Tallital) pale cement grey (the texture averaged 88/255 and read black); Gandhi ji's statue has his charkha and a marigold garland; ghat boats mostly yellow; yachts with striped sails | Mall Road, Tallital | `Editor/WorldBuilder.Concrete.cs`, `WorldBuilder.Tallital.cs`, `WorldBuilder.Details.cs`, `PropKit.cs` |
| Buildings against the satellite (Esri World Imagery, top view at 0.5 m/px: `Editor/TopView.cs`): the shop rows of process_data had replaced real footprints. `Tools/restore_footprints.py` put back 863 real Overture footprints (Google / Microsoft / OSM) and dropped 159 row houses on real buildings or open ground; `Tools/prune_rows_by_satellite.py` dropped 61 more row houses standing on trees (the Mall Road row is kept); `Tools/roof_colours_from_satellite.py` gives 986 real buildings their roof colour from the photo (roof type 2 = photo tin: sloped, never a G.O.A.N. box). `world.json` before all this: `Data/world.before_restore.json`. They need `fetch_overture.py` output and the tile cache in `Shots/satcompare/_tiles` | town | `Tools/*.py`, `Editor/WorldBuilder.cs` |

Also seen and fine: Mall Road colonial fronts, Lower Mall green lamps, moored boats square to the ghat, town trees.

### Checked in renders on 14 Nov

| What | Where (x, z) | Code |
|---|---|---|
| Boat House Club wing moved off the road; deck between the wings; jetty starts from the deck | (125, 60) | `Editor/LandmarkBuilder.BoatClub.cs` |
| Flats steps: one continuous run, pink lattice railing, orange rail at the foot. The ground is cut before the Flats grit is laid. | (-143, 45) | `Editor/WorldBuilder.FlatsEdge.cs`, `WorldBuilder.Flats.cs` |
| Capitol: walk level and front level are both flat, with a stone wall between them and stairs running down along it. The ramp footway is gone. | (0, -15) | `Editor/WorldBuilder.CapitolGround.cs`, `WorldBuilder.DataFixes.cs` |
| Lake bank below the Capitol: level with the path, a straight stone embankment, railing on top | (25, -40) | `Editor/WorldBuilder.CapitolGround.cs` |
| Tallital traffic post on the junction node; barricades face each road | (832, -1132) | `Editor/WorldBuilder.Tallital.cs` |
| High streets with 3–4 storey painted shop buildings and new shops along the frontage: the Sukhatal / High Court chauraha and Tallital | (-527, 522) and (825, -1145) | `Editor/WorldBuilder.HighStreet.cs` |

### Still open

- **High-street colours against Street View:** the new purple, blue and rose are picked at random along the high streets. Which real building is which colour (Hotel Beena Lodge, Hotel Kohinoor) still needs the owner's photos.
- **Rotary Club "Selfie Point" red frame** on Mall Road: location not known yet.
- **Models to make in Tripo:** see `TRIPO_LIST.md`. Notes from the YouTube comparison are in `REFERENCE_REPORT.md`.
- **Shaktipeeth gate:** placed by hand. Its position is kept in `Assets/Nainital/Data/placed_by_hand.json`, which saves when the scene is saved and is restored at build. Never place it from code.

### Rules the owner has set

- Do not make Android or iOS builds unless asked; build scenes only.
- Real places must match the owner's photos and Street View: positions, rotation, scale and roads.
- Low-poly geometry with good textures. Keep the house count.

## If something looks wrong

| Problem | Fix |
|---|---|
| Models or textures are pink, or the Console says "not a valid FBX/PNG" | The LFS files did not download. Run `git lfs pull`, then right-click Assets → Reimport. |
| The scene is empty or Generated is missing | Run **Nainital → 2. Build World** (step 4). |
| Buildings are plain white | Run **Nainital → 2. Build World** again. It recreates the building atlas material. |
| No vehicles in the traffic | Run **Nainital → 7. Import vehicles**, then Build World. |
| No people, or wrong animations | Run **Nainital → 6. Build locomotion animator**, then **4. Import rigged characters**, then Build World. |
| Wrong Unity version | Install exactly 6000.4.1f1. A newer version may upgrade the project. |

## What is where

| Path | Contents |
|---|---|
| `Assets/Nainital/Scripts` | Game code. `Runtime` holds the player, vehicles, crowd, audio, UI and the day/night schedule. `Editor` holds the world builder, import kits and Android build. |
| `Assets/Nainital/Data` | Processed real-world data: `world.json` (roads, buildings, landmarks, terrain heights), minimap, far ridges, PBR textures |
| `Assets/Nainital/Generated` | *Not in git.* The built world, made by Build World. |
| `Assets/Nainital/Vehicles` | 10 vehicle models: Ertiga, Innova Crysta, Bolero, WagonR, Honda Amaze, Scorpio, Bolero Pickup, Tata 407, tourist coach, tourist bus. Each is under 20k triangles, with LODs and spinning wheels. |
| `Assets/Nainital/Characters`, `Assets/Resources` | People, animations, the vehicle library, sounds |
| `Assets/GOAN` | Buildings from the G.O.A.N. project |
| `Tools/` | Python and Blender scripts that fetch and process the map data and prepare models and sounds. You do not need them to play; see below. |

## Rebuilding the data from scratch (optional)

`Assets/Nainital/Data` is already processed, so normally none of this is needed. To regenerate it you need Python 3.11+ (in a venv: numpy, Pillow, requests, duckdb, earthengine-api) and Blender 4.5. The steps, with scripts in `Tools/`, are:

1. Fetch the source data:
   - `fetch_data.py` and `fetch_osm_tiles.py`: OpenStreetMap and terrain
   - `fetch_overture.py`: building footprints
   - `fetch_open_buildings.py`: heights; needs an Earth Engine project
   - `fetch_textures.py`: Poly Haven
   - `fetch_sounds.py`: Mixkit
2. Process it: `process_data.py RawData <out> <preview.png>`. It needs about 20 GB RAM and takes about 7 minutes. Then run `make_minimap.py` and `make_far_ridges.py`, and copy the output to `Assets/Nainital/Data`.
3. Prepare the models: `make_vehicle_lods.py` and `make_building_lods.py` run in Blender.

## Credits and data

- Map data © OpenStreetMap contributors (ODbL), and Overture Maps.
- Building heights: Google Open Buildings 2.5D (CC BY 4.0).
- Terrain: AWS Terrain Tiles (SRTM and other public DEMs).
- Textures: Poly Haven (CC0).
- Sound effects: Mixkit (Mixkit Sound Effects Free License). They are for use inside this game; do not redistribute them as separate sound files. `Tools/fetch_sounds.py` downloads them again.
