# Free-roam features (GTA San Andreas style, no missions) - plan of 21 Nov 2026

The owner picked these on 21 Nov 2026 (night): "itna saara pehle poora perfect krdo". No missions now. Every answer
below is his; build to it, and log what was done (and what is still open) in the table at the end and in `README.md`.


## HANDOFF - 22 Nov 2026, 05:40 (read this first in a new chat)

**Update 22 Nov 13:20 (test run 3, main PC, not committed):** the gym camera is fixed and seen (`GymStation.PickView`:
12 places round the player, the camera's own 0.25 m sphere cast + a ray, 3/4 front preferred; an override view during
the workout, the old yaw / pitch back after), the new waiter look is seen (white shirt, no pandit), **umbrellas were
broken and are fixed** (`Umbrellas` picked "odd instance ids" - Unity 6 gives even ids only, so nobody got one; now a
mixed hash: 8 of 16 walkers in the rain, all folded ~8 s after it stops), the phone's Radio / Stats apps are right in
English and Hindi (+ a `Waiter` line in `strings_shops.tsv`). Captures: `Shots/sheet_g4_gym_noon.jpg`,
`sheet_r6a.jpg`, `sheet_r6b.jpg`, `sheet_r8_umbrellas.jpg`. Test helpers: `FreeroamTest.UmbrellaView` / `UmbrellaWhy`,
`Gym` fills the energy bar first, `Editor/GymCamProbe.cs` (`Run`: what the camera hits; `Ground`: terrain round each
frame).
**Update 22 Nov 14:10 - City Joggers Park rebuilt (main PC, not committed; world built twice, Unity closed 18:10).**
Owner: no gate, not flat ("up down terrain"), ground first, then the equipment. Done (details: TRACKER 22 Nov 13:30):
the shore moved out to the satellite's edge (`FixJoggersParkShore`, `Editor/JoggersPark.cs`), rolling ground from a
level earth strip under the parapet down to the stone wall at the water, two openings in the parapet (no gate), 9 level
spots, the equipment built after the last ground step on concrete footings, and the road's bank rings no longer raise
the ground behind the Lower Mall parapet (whole Lower Mall). Checked: every frame +0.00 m on its ground; walk in through
both openings (3.6-3.9 m down into the park) and back up to the road; all three workouts seen, gym camera now gets a
clear 3/4 front view at 4 m (`Shots/sheet_jp2_a.jpg`, `sheet_jp2_b.jpg`, `sheet_jp_play.jpg`). **Other PCs: Build World.**
Left: outside the park the Lower Mall bank still has a small wedge at the parapet's foot (its bank starts 1.8 m under
the road; the 1.46 m heightmap) - same strip trick would fix it, not done (owner's call); at the twister a pine's
branches can sit in front of the gym camera (leaves have no collider).

**Start here (the owner stopped at 05:40, back in 4-5 hours): the gym camera** - DONE 13:10 (above). Tripo models: NOT
now (he said so again).

**State.** Every item of the batch (#1-#10) is built and pushed; only the Tripo MODELS are missing (cows, dogs, cats,
the unbranded fire truck): the owner parked them for later ("tripo ke saare model baad me krenge pr yaad rkhna") -
remind him. Their code is in (Characters/Animal.cs, AnimalManager.cs, Animals/Editor/AnimalImport.cs: nothing spawns
until Resources/Animals/<name> exists; Tools/animals/rig_animal.py is still to write). Making them needs his Brave
restarted with remote debugging (CDP 9333, Profile 1) - ask him first.

**After pulling: Nainital > 2. Build World (~13 min)** - the scene is not committed; the pumps, fire station, house,
shops' sit-outs and the restaurant chairs / bed / almari are made by the build. Then Unity resolves the NativeGallery
package from its git URL (git on PATH).

**Verified in Play on 22 Nov** (`Editor/FreeroamTest.cs` through the bridge): fuel at the Tallital pump (16 -> 46 L,
Rs 3000 -> 0, "not enough money" fill), shop buy + wear (Kumaon Kapda Ghar), gym (muscle 10 -> 13), hunger blur
(volume 0.55 at 14 %), radio with the owner's own songs (94 found, playing), phone calls / camera / Hindi (earlier).
Last build: 2 pumps (Tallital 8 m from his pin), fire station, house, 13 / 13 shops, 5 / 5 restaurants.

**Test run 2 (22 Nov, 05:15-05:40, in Play through the bridge, captures `Shots/play_r3_*`, `play_r4_*`, `play_r5_*`):**
- *Restaurant (Tashi Momo Corner)* - OK: seated, the menu (5 dishes), steamed momos ordered (Rs 80), the waiter walks
  out of the kitchen at ~8 s, "Here you are - your Steamed momos" at ~10 s, the plate on the table, hunger 0.20 -> 0.54;
  4 diners at the other chairs with their plates (they appear ~1 s after the player comes within 70 m, so `Sit`'s own
  "diners 0" is only its timing). The waiter came out in the **pandit's look** (dhoti, beads, barefoot): `MakeWaiter`
  now picks goan_ladka / m_man_flannel / m_young_puffer - compiled, **not seen yet**.
- *House* - OK: "Sleep in the bed (save)", the HOME sign, the wardrobe (3 outfits, Wear), sleep 17.65 -> 07.15 and
  "Morning - a good night's sleep! Game saved". Fixed: the bed could be used with the almari panel open (the panel stayed
  up through the sleep and the sleep's end unblocked the input under it) -> `HouseBed.CanInteract` needs no
  HouseUI / ShopUI open.
- *Gym* - the numbers are OK (pull: muscle 16 -> 19; pedal: -> 20.1, stamina 18 -> 20.4; twist: -> 21.3, fat 19.7 ->
  16.7; both toasts). **OPEN - the camera:** `GymStation.Workout` never touches the camera, so it stays where it was
  and, at the City Joggers Park frames (Lower Mall bank: trees, the wall, the frame posts), its collision pulls it in -
  pull-ups: the camera inside the player's jacket, then the player out of view; pedal / twist: low between the trees.
  So **the three poses (HandIK) are still not seen.** Planned fix (not written - he stopped the session): at the workout
  start sphere-cast from the player's pivot in ~8 directions, take the side with the most room (prefer a 3/4 side
  view), set `ThirdPersonCamera` yaw / pitch / distance, put distance + pitch back after; then capture the poses again.
- *Test helper fix:* a player left on a restaurant chair stayed there - every later `Put` (Bed, Gym) did nothing. `Put`
  now closes an open shop / almari panel and calls the new `PlayerController.StandUp()` (the seat's own get-up code).
  `Sit` / `ShopOpen` top the money up to 3000, so money in a report after them is the test's, not the game's.
- *FrontEnd.LateUpdate NullReference* (settings / credits null for a frame around a reload; 43x in the log) -> null check.
- `GalleryCheck` OK in the editor (NativeGallery.Runtime, the byte[] SaveImageToGallery overload).
- **Still to run:** umbrellas in the rain (`Near` arg a Mall Road shop, `Rain`, wait ~10 s, `Report`, capture), the
  phone's Radio / Stats apps in English and Hindi (`PhoneTest.App` arg radio / stats, `PhoneTest.Hindi`, capture,
  `PhoneTest.English`), NativeGallery on a real phone (needs an APK / IPA - only when he asks).

Steps: `call PlayTest.Begin`, then `call FreeroamTest.Sit` (arg "Tashi"), `ShopBuy`, captures every 2 s to ~16 s,
`Report`; `Bed`, `Almari`, `Sleep`; `Gym` (arg pull / pedal / twist); `Near` + `Rain` + `Report`; `PhoneTest.App`;
`capture <name>` after each. `Tools/bridge.sh` still points at the old `D:/NainitalGame` folder - on the main PC write
`Bridge/cmd.txt` in `D:\NainiTA---The-Sukoon` (fresh id each command, `Bridge/arg.txt` for the argument). The owner may
touch the open editor during a run ("maine hila diya") - then just run that step again.

**Fixed on 22 Nov, keep in mind:** a component the builder adds must have its own file (RestaurantSeat, HouseBed,
HouseStorage were saved as missing scripts); pumps and sit-outs reserve their ground in "Pump and sit-out plans"
before the houses; a smaller place circle wins the name (pump circles 16 m).

**Open points for the owner:** the Sukhatal pin is 50 m from any road in the map data, so that pump stands 39 m from
it on the Kaladhungi road (OK?); Bazaar Bhature Wala's planned patch was taken by the Bada Bazar structure, so it stands
30 m off (-153, 192) - look; Barf Pizza Point on Snow View's hilltop is an open patch with no road (looked bare);
he judges the radio's synthesized stations by ear.

**How he wants the work done:** never sit waiting on a build / render - run it in the background and keep working;
one task at a time, no parallel agents (they burned ~50 % of his usage); commit / push only when he asks; his own
music stays in his folder (Documents\NainiTA\music_folders.txt lists it), never copied into the repo.

## His answers

| Feature | What he wants |
|---|---|
| Radio | Spotify cannot be used (its developer policy bans games). A local music folder, like SA's User Tracks: Android - the game's `Music` folder, iPhone - a folder in the Files app, PC - any folder. Plus as many free-licence stations as the licences allow ("jitna free allowance hain vo to kr hi do") |
| Petrol pumps | Only the real ones in the map: IndianOil Durga Stores, Tallital (Thandi Sadak, near the High Court, 29.380094 N 79.4621 E), Parvat Filling Station, Khurpatal, and Sukhatal. The real station's shape from photos, a **fake brand** name |
| Fuel | A gauge on the HUD; a car / bike stops when empty; refuelling costs money |
| Stats | All of them: stamina, muscle, fat, driving, bike, swimming / breath. **Numbers and their effects only** (no body change). Stamina also drains while walking |
| Hunger | Yes: health drops slowly, and the screen gets more and more blurry |
| Shops | 10-15 interactable shops, each its own goods: clothes / jackets / caps, Bal Mithai, candles, woollens, umbrellas, medical, phone recharge, souvenirs / toys, books, bakery, fruit, gym gear. Fake shop names at the real places (Mall Road, Bara Bazaar, Tibetan Market). 4-6 outfits. **Everything bought goes to the house.** No barber |
| House | One, on the zoo road, a really good interior (pahadi wooden, fireplace, lake-view balcony, drawing room, bedroom, kitchen); sleep and save there. Later it is bought in a mission; for now it is simply open |
| Phone | Calls 112 and 100 police / 108 ambulance / 101 fire, answered through a UI dialogue with if / else replies. A new unbranded fire truck from a reference photo and the real fire station; fires: burning vehicles and now and then a random fire. A prank call: the police are angry, a fine or one star. A camera with a selfie mode that saves to the phone's gallery. (The map is already there) |
| Gym | One, at a real gym's place; stats only (no fight moves) |
| Animals | Cows, dogs, cats: Tripo models, rigged by us. They cannot be hurt - touched, they run off very fast. Cows on the road stop the traffic |
| Restaurants | 5-6, people at the tables eating (tea, pizza ...). The player sits -> a menu opens -> 10-15 s later someone brings the order (hunger / health) |
| Umbrellas | Townsfolk under umbrellas in the rain |
| Night lighting | A pass over the whole town: many areas are still black or dim |
| Traffic | A stuck car backs up and finds a way out; after 4-5 failed tries it rams its way through ("thok ke lejaaye") |

Not wanted: a repair / respray shop, a garage or showroom, stunt jumps, a barber, fight moves, races, fishing,
collectibles, photo spots, festivals, snow, a bigger police chase.

Tripo: he left the settings to us ("apne hisaab se set krlena") - keep the polycount **low** so phones do not lag.

## Order and status

| # | Work | Status |
|---|---|---|
| 1 | Traffic unstick | done 22 Nov (docs/VEHICLES.md 0.2, "Traffic that stays stuck") |
| 1b | Vehicle sounds (asked 22 Nov): real engine pull per vehicle, braking, heavy hits, one fixed horn per vehicle (not a random one each time), the horn together with the dipper | done 22 Nov (docs/VEHICLES.md 0.2, "Vehicle sounds"); waiting for the owner's ears on the mix |
| 2 | Night lighting, whole town | done 22 Nov: first pass + gap fill (below); the owner checks it in the game |
| 3 | Phone: calls (police / ambulance / fire truck), camera | done 22 Nov (section 3); fire station built at his pin; NativeGallery added (his OK, 22 Nov); open: the unbranded fire truck model (Tripo - parked for later by the owner) |
| 3b | Language: English default, Hindi (Devanagari) everywhere (asked 22 Nov) | done 22 Nov: `Core/Loc.cs`, `Tools/loc/` (see the repo CLAUDE.md), 628 texts, every scene text covered (LocDump) |
| 3c | Phone UI: curved body, glassmorphism, keypad, Nainital wallpapers (asked 22 Nov) | done 22 Nov (section 3, second pass) |
| 4 | Petrol pumps + fuel | built 22 Nov (section 4); in the combined build-and-test |
| 5 | Shops + restaurants (seated people, ordering) | built 22 Nov (section 5); shop buy + wear, restaurant order + waiter + diners verified in Play 22 Nov (test run 2); the waiter's new look seen 22 Nov 13:12 (white shirt) |
| 6 | House on the zoo road | built 22 Nov (section 6); bed / sleep / save + almari verified in Play 22 Nov; bed blocked while the almari is open |
| 7 | Gym + stats + hunger | built 22 Nov (section 7); numbers + hunger blur verified; workout camera fixed + the three poses seen (22 Nov 13:10); City Joggers Park ground + equipment rebuilt 22 Nov 14:10 (HANDOFF) |
| 8 | Cows, dogs, cats | code built 22 Nov (Characters/Animal.cs, AnimalManager.cs; spawns nothing until the models exist); the Tripo models wait for the owner's OK to restart Brave with CDP |
| 9 | Radio | built 22 Nov (section 9): own music + three original stations |
| 10 | Umbrellas in the rain | built 22 Nov (Characters/Umbrellas.cs); was giving none (even instance ids) - fixed + seen 22 Nov 13:17: about half the walkers, folded ~8 s after the rain |

## 2. Night lighting (22 Nov 2026)

Measured with `Editor/NightCheck.cs` (play mode: `call PlayTest.Begin`, then `call NightCheck.Run` with arg `21`):
174 street-level views wherever houses stand, 21:00, the real pooled lamp lights; small PNGs + `report.csv` in
`Shots/night/`, the log lists the darkest. Before -> after: dark share of the street 0.42 -> 0.24, views over 70 % black
43 -> 11, views with no lamp within 30 m 15 -> 6.

- `Runtime/World/DayNightCycle.cs`: night ambient a moonlit blue (sky 0.07 / eq 0.046 / ground 0.018, was 0.016 / 0.01 /
  0.004), moon 0.2 (was 0.07), night fog / haze lifted so ridges show.
- `Runtime/World/LampManager.cs`: 72 pooled lights on a PC (phone 32), range 34 m; a third list `porchLights` (warm,
  8 m, the nearest few lit for real).
- `Editor/WorldBuilder.NightLights.cs` (replaces the old `BuildRoadLamps`): every road among houses lit (main / lanes
  ~32 m, footpaths and steps ~28 m, a short lane one in its middle; out of town main roads ~42 m, lanes dark), no pole
  inside a house (`SolidAt`), else a lamp on the house wall (`PropKit.WallLamp`); `BuildPorchLights`: a bulb by the door
  of about half the houses facing a road (`PropKit.PorchBulb`), halos in `WorldBuilder.LampGlow.cs`. Build log: 1520
  poles + 13 wall lamps, 593 porch bulbs. **Needs Nainital > 2. Build World on each PC** (the built world is not in git).
- Still dark: two forest tracks (no lamps on purpose), a few hillside lanes where neither a pole nor a wall fits (97
  spots in the log), the pedestrian lanes at (-364, 218) and (0, 246) between tall houses.

**Gap fill (22 Nov, second pass).** The dark views' nearest lamp was 19-40 m away: the first pass skipped a spot when a
lamp stood within 15 m on the MAP (even on a road 8-20 m higher or lower) and gave up where one spot had no room. A second
walk over every road among the houses every 6 m now puts a lamp wherever none hangs within 15 m on the same level (head
-2..+11 m over the road): a pole up to 2.5 m further out, else a wall lamp up to 9 m away. Build log: gap fill 475 poles
+ 2 wall lamps, 7 six-metre spots still dark; lamps 2072. Halos shrink close up (`LampGlow.shader`: 0.4x at 3 m, full from
25 m - a lamp 6-8 m off looked like a full moon). NightCheck 21:00: dark share 0.244 -> 0.155 (median 0.19 -> 0.10), views
over 70 % black 11 -> 2 (the two forest tracks), the lanes at (-364, 218) 0.99 -> 0.04 and (0, 246) 0.91 -> 0.00.
Darkest left: the trunk road at (829, -1893) 0.66, (-519, -174) 0.55.

## 3. Phone (built 22 Nov 2026)

T on a PC, or the phone button (teal, left of "more") under the money. `UI/Phone.cs` (Call, Camera, Selfie, Gallery,
Map), `Police/EmergencyCalls.cs` (the operator's lines and replies, `EmergencyDispatch`), `Police/FireTruck.cs`,
`World/Fires.cs` (`FireManager`, `FireSpot`), `Player/PhoneCamera.cs`; `Ambulance.BeginForPlayer`,
`PoliceManager.SpawnAmbulanceForPlayer / SpawnFireTruck`, `VehicleDamage.All / OnFire / Extinguish` (a wreck now burns
150 s, was 14 s), `WantedSystem.Prank`. Tests: `Editor/PhoneTest.cs` (bridge: `call PhoneTest.Prank / Fire / Hurt108 /
Shot / Selfie / Open / Report`).
- Real or prank: 100 accident = an incident or a wreck within 60 m; 100 maar-peet = the player hurt in the last 90 s or a
  goon within 45 m; 108 "main khud" = health under 70 %; 108 "koi aur" = an incident within 60 m; 101 = flames within
  220 m (random fires start 90-180 m off and are called in from where the smoke is seen).
- Prank: a police car comes to the spot; 5 s after it arrives (or 60 s with no road to send it on) a Rs 500 challan; a
  second prank in 10 minutes Rs 1000 + 1 star; no money: 1 star.
- Random fires: every 8-14 min (the first after 4-7), not in rain, a heap by a road 90-180 m from the player; 6 min.
- Tested 22 Nov (play, bridge): prank -> police came, Rs 500 taken; prank with no money -> 1 star, warned, cleared; fire
  102 m off -> 101 -> tender drove ~85 s, two firemen, 7 s of water, out, left; 108 at 35 % health -> ambulance came, the
  medic walked over, left (health regenerates on its own: 0.35 -> 0.97 in 21 s, see #7); camera + selfie shots saved
  (Gallery + Pictures/NainiTA on a PC); a soft light on the face for night selfies.
- Done since: the real fire station at his pin (the tender drives out of it). NativeGallery (yasirkula, MIT) v1.9.5 is in
  `Packages/manifest.json` (owner's OK 22 Nov): `PhoneCamera.SaveToDevice` calls it directly (album "NainiTA"; it asks
  for the photo permission itself; iOS photo-library texts come from its automated setup, Project Settings > yasirkula).
- Open: the unbranded fire truck model (Flow -> Tripo; now the light truck painted red) - the owner parked every Tripo
  model of the batch for later (22 Nov).

**Phone UI, second pass (owner, 22 Nov: "phone UI tgda bnaao .. curved sides .. iphone ki tarah but not iphone .. crazy
glassmorphism apps with smooth animations .. keypad .. phone me nainital ka wallpaper").** `UI/Phone.cs` rewritten: a
rounded titanium body (9-sliced `frame.png`), a punch-hole camera, side buttons; the screen is a rounded Mask holding a
wallpaper - the game's own renders (`Editor/PhoneWallpaper.cs`: bridge, in Play, arg `name;x,y,z;lx,ly,lz;hour;fov`, a y
ending in `a` is absolute; Shots/wall_*.png), three of them: lake (16:36 over the lake), dawn (06:24 from Tallital),
mall_night (20:30). Frosted glass = `Resources/Phone/UIGlass.shader`: samples a blurred copy of the wallpaper at the
pixel's place on the screen (`_NtPhoneRect` in root-canvas space, `_NtPhoneBlur`; set by Phone.LateUpdate), tint by the
material (`glassLight / glassDark / glassClear`). Art: `Tools/phone/make_phone_art.py` (Pillow: frame, glass, rings, the
gradient app glyphs, wallpapers -> `Resources/Phone/`). Pages: home (clock, date, weather glass, apps, dock), Phone (keypad
with letters, 112/100/108/101 chips, PC digits / Enter / Backspace), call (pulsing rings while it rings, a timer, the
operator's line typed out in a glass bubble, replies fade in, red end button), SOS, Gallery, Photo (delete), Settings
(wallpaper, language), Weather (a believable temperature by hour and weather), Wallet. Animations on unscaled time: the
phone springs up (OutBack) and drops away, apps grow out of their icons and shrink back, icons stagger in, buttons spring
(`PhonePress`), keys ripple. Test hooks: `PhoneTest.App / Type / DialNow / Reply` (arg.txt).

### The design it was built to (21 Nov)

- Open from a round HUD button / the tray (`HUD.AddTrayButton`) and a key on PC. Apps: Call, Camera (Map exists).
- Calls with an operator dialogue (buttons, if / else) that checks the real game state:
  - 112: asks which service, then as below.
  - 100 police: "Accident" / "Maar-peet, chori" / "Mazaak tha". An accident claimed with no body / wreck within 40 m,
    or "mazaak": a prank - police come, find nothing, fine (GameState money) or 1 star (`WantedSystem`).
    Real: `PoliceManager.SpawnSceneCar(playerPos)` (already used by `IncidentManager`).
  - 108 ambulance: the player hurt (Health below ~70 %) -> an ambulance comes and heals the player on arrival (new mode in
    `Police/Ambulance.cs`; `PoliceManager.SpawnAmbulance` makes one for an `IncidentManager.Incident`); someone
    hurt / dead near -> the incident flow; else a prank.
  - 101 fire: a burning vehicle within ~60 m -> fire truck (new `FireTruck` like `Ambulance`, `RouteDriver`), the crew
    sprays water and puts it out (new `VehicleDamage.Extinguish`); random house fires later. The truck model: an
    unbranded fire truck from a reference photo (Flow -> Tripo, low poly) - until then the light truck in red with
    `EmergencyLights`.
- Camera: HUD hidden, shutter / flip / close; selfie = a camera ~0.9 m in front of the player's face. Save to the phone
  gallery with NativeGallery (MIT): add `"com.yasirkula.nativegallery": "https://github.com/yasirkula/UnityNativeGallery.git#v1.9.5"`
  to `Packages/manifest.json`; on a PC save to Pictures/NainiTA.

## 4-10. The rest of the batch (22 Nov 2026; started by 7 agents, stopped on the usage limit, finished by the lead one by one)

Code checks without the editor: `python Tools/pipeline/compile_check.py` (Unity's Roslyn on the current files, 7 s).
Every text: `Tools/loc/strings_<area>.tsv`, then `python Tools/loc/build_loc.py`.

- **4 Fuel / pumps.** `Runtime/Vehicles/VehicleFuel.cs` (tank by model, 30-75 % at first drive, burns only while the player
  drives, empty = no pull, electric never runs dry; fills at Rs 100 / litre when stopped on a forecourt), `World/FuelStation.cs`,
  `UI/FuelGauge.cs` (cars and bikes), hooks in CarController / PlayerBike. Pumps: `Editor/LandmarkBuilder.PetrolPumps.cs`
  ("Himgiri Fuels", a made-up brand in green / white / saffron) at the owner's pins Tallital (861.1, -1104.9) and Sukhatal
  (-862.1, 304.5): canopy, dispensers, office, air pump, price totem, cement forecourt painted into the terrain, slid along
  the road until free (bus-stand plaza / service lanes do not block; the first build put Sukhatal's 45 m from the pin).
- **5 Shops + restaurants.** `Core/Inventory.cs` (owned things, outfits, `ShopCatalog` with every item and price,
  `OutfitLook`: recoloured copies of the player's own texture from `Tools/outfits/make_outfits.py` + a topi / beanie),
  `Interaction/Shop.cs`, `UI/ShopUI.cs`, `Interaction/Restaurant.cs` (+ `RestaurantSeat`), `Editor/LandmarkBuilder.Shops.cs`
  (step "Shops and restaurants", before the trees): 13 shops with made-up names on built shop fronts (8 on Upper Mall Road,
  3 in Bada Bazar, 2 in the Tibetan Market) and 5 sit-outs (3 tables x 2 chairs, umbrellas, a counter with its board): the
  player sits, orders, a waiter (a townsperson's look) walks over in ~10-15 s; townsfolk sit at the other tables with
  their plates while the player is near. The sit-outs open on the road's edge like dhabas (Upper Mall Road allowed):
  Tashi Momo Corner by the real Sonam Fast Food, Pahadi Rasoi by the real Chandni Chowk (Mall Road), Bazaar Bhature Wala
  by Bada Bazar, Lake Breeze Cafe on Mall Road by the lake, Barf Pizza Point on Snow View's hilltop (no road there: an
  open patch). Their ground and the pumps' is reserved in the step "Pump and sit-out plans" before the houses.
- **6 House.** `Editor/LandmarkBuilder.House.cs` (+ step "House plan (Zoo Road)" before the Buildings: replaces the one
  data house on its spot), `World/House.cs`: a Kumaoni house on the valley side of Zoo Road a third of the way to the zoo:
  stone plinth, timber walls, red tin roof, chimney, drawing room with a burning fireplace, kitchen, bedroom (bed: sleep to
  07:00 + save), almari (things bought, change clothes), a balcony towards the lake.
- **Fire station** at the owner's pin (-237.9, 104.9): `Editor/LandmarkBuilder.FireStation.cs` from the Street View refs in
  `Tools/firestation/refs` (three-storey cream block, open bay, pale blue "POLICE FIRE STATION" board); step "Fire station
  plan" keeps its footprint clear; `Police/FireStationBay.cs`: the 101 tender drives out of it (when the fire is over 100 m
  off and the bay is out of sight) and back to it.
- **7 Stats + hunger + gym.** `Player/PlayerStats.cs` (six stats, energy, breath, hunger 30 min full to empty, under 30 %
  no regen + health falls + a runtime URP blur volume; regen now 0.4-1.2 /s instead of 3), hooks in PlayerController (sprint,
  jump, swim), PlayerBike (speed), FoodStall (Eat); `Interaction/GymStation.cs` on the City Joggers Park open gym (5 s
  workouts); the phone's Stats app (+ the tray's "Stats").
- **9 Radio.** `Audio/Radio.cs` (in vehicles; R / the phone's Radio app), own music from Documents\NainiTA\Music (PC)
  plus every folder listed in Documents\NainiTA\music_folders.txt, sub-folders too (the owner's PC: a folder of
  his own songs, read in place - never copied into the game: they are not ours to ship),
  files/Music (Android), Documents/Music shown in the Files app (iPhone: `Editor/IosFileSharing.cs`); three stations of
  ORIGINAL music synthesised by `Tools/radio/make_stations.py` (no third-party tracks: Resources/Radio/CREDITS.txt).
- **10 Umbrellas.** `Characters/Umbrellas.cs`: rain over ~0.2 -> half the walkers near the camera raise the right hand
  (HandIK) holding a shaft and an eight-panel canopy; folded a few seconds after the rain.
