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

## Work in progress (last updated 18 Nov 2026)

Start here when you pick the project up on another PC. Build the world first (**Nainital → 2. Build World**). The 17 Nov work was done on a PC with only Unity 6000.3.28f1; it opens the project fine (the repo stays on 6000.4.1f1: the `.meta`, `ProjectSettings` and `packages-lock.json` changes that 6000.3 makes on import are not committed).

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
