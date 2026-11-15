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

## Work in progress (last updated 16 Nov 2026)

Start here when you pick the project up on another PC. Build the world first (**Nainital → 2. Build World**). Everything below was built and checked in renders on 15 Nov, in Unity 6000.4.1f1.

In-game positions are metres: x = east, z = north. A Scene view camera looking at the point is enough to check each one. With the editor open, `Bridge/cmd.txt` takes commands (`refresh`, `world`, `shot name;x,y,z;lookX,lookY,lookZ;hour`), see `Editor/EditorBridge.cs`; shots go to `Shots/`.

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
| `Editor/PhotoBatch.cs`: one batch-mode Unity run for a list of steps in `Bridge/batch.txt` (`world`, `call Type.Method`, `shots 03,38`), then it quits; `Logs/run_photobatch.ps1` runs it at BelowNormal and prints the results. Use it instead of leaving the editor open. | — | — |
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
