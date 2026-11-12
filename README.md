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
| Unity Editor | **6000.3.28f1** | Use exactly this version. In Unity Hub: Installs → Install Editor → Archive. Add the **Android Build Support** module (with OpenJDK and Android SDK & NDK) if you want to build for phones. |

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

1. In Unity Hub, choose **Add → Add project from disk** and pick the cloned folder. Open it with 6000.3.28f1.
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

The phone controls only show on a phone. To try them on PC, set PlayerPrefs `nt_touch = 1`.

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

1. **Make the Xcode project (on Windows).** Install the iOS Build Support module for 6000.3.28f1. Then choose **Nainital → 8. iOS: switch platform**, then **Nainital → 9. iOS: build Xcode project**. The project is written to `Builds/iOS`.
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

## If something looks wrong

| Problem | Fix |
|---|---|
| Models or textures are pink, or the Console says "not a valid FBX/PNG" | The LFS files did not download. Run `git lfs pull`, then right-click Assets → Reimport. |
| The scene is empty or Generated is missing | Run **Nainital → 2. Build World** (step 4). |
| Buildings are plain white | Run **Nainital → 2. Build World** again. It recreates the building atlas material. |
| No vehicles in the traffic | Run **Nainital → 7. Import vehicles**, then Build World. |
| No people, or wrong animations | Run **Nainital → 6. Build locomotion animator**, then **4. Import rigged characters**, then Build World. |
| Wrong Unity version | Install exactly 6000.3.28f1. A newer version may upgrade the project. |

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
