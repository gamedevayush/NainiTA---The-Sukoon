# Free-roam features (GTA San Andreas style, no missions) - plan of 21 Nov 2026

The owner picked these on 21 Nov 2026 (night): "itna saara pehle poora perfect krdo". No missions now. Every answer
below is his; build to it, and log what was done (and what is still open) in the table at the end and in `README.md`.

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
| 2 | Night lighting, whole town | done 22 Nov, first pass (below); the owner checks it in the game |
| 3 | Phone: calls (police / ambulance / fire truck), camera | **next** - design below, no code yet |
| 4 | Petrol pumps + fuel | - |
| 5 | Shops + restaurants (seated people, ordering) | - |
| 6 | House on the zoo road | - |
| 7 | Gym + stats + hunger | - |
| 8 | Cows, dogs, cats | - |
| 9 | Radio | - |
| 10 | Umbrellas in the rain | - |

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

## 3. Phone - design for the next session (not built yet)

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
