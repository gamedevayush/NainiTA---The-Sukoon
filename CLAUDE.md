# Naini Sukoon - notes for Claude (read first)

Unity 6000.4.1f1 open-world Nainital. Public repo, Git LFS. The owner talks Hinglish; give him full absolute paths.

## After every pull / on a new PC - check these and TELL THE OWNER what is missing
1. **Two free Asset Store packs are not in git** (Asset Store licence, public repo). If `Assets/MotoInteractionAnimsFREE/`
   or `Assets/Kevin Iglesias/` is missing, tell him to import them: Package Manager > My Assets > "Motorcycle Interaction
   Animset FREE" (SpectraMotions) and "Human Basic Motions FREE" (Kevin Iglesias) > Download + Import, then
   **Nainital > 6b. Build character animator**. Unity also warns about it on load (`StorePackCheck`). Never commit them.
2. Unity Hub must be running and signed in, or batch mode fails with "No valid Unity Editor license".
3. The built world (`Assets/Nainital/Generated/`) is not in git: **Nainital > 2. Build World** after `world.json` changes.

## 26 Nov 2026 - Windows crashed, reinstalled (owner)
The main PC's Windows crashed and was reinstalled; C: is fresh, this repo on D: survived (clean, in sync with origin/main).
- Windows user folder is now `C:\Users\Ayush Singh` (the old `Ayush Singth` path below is gone).
- Git 2.56.1 reinstalled (`C:\Program Files\Git`), GitHub login via Git Credential Manager (`gamedevayush`). D: files
  belong to the old Windows user, so git said "dubious ownership"; fixed with
  `git config --global --add safe.directory D:/NainiTA---The-Sukoon`.
- **Reinstalled by 27 Nov:** Unity 6000.4.1f1 (`C:\Program Files\Unity\Hub\Editor\6000.4.1f1`, licensed), Python 3.10
  at `C:\Users\Ayush Singh\AppData\Local\Programs\Python\Python310\python.exe` (numpy, Pillow, fonttools, uharfbuzz,
  edge-tts), ffmpeg 9.1 (winget Gyan.FFmpeg, not on PATH: `CinemaReel.FFmpeg()` finds it). Only the Microsoft Store
  `python` stub is on PATH - call that python.exe by its full path.
- **This PC is Windows 11 Pro N**: no Media Feature Pack, so Photos / Edge cannot play an mp4 ("lack of Windows Media
  Foundation"). Show videos with `ffplay.exe` from the ffmpeg folder (or VLC / the Media Feature Pack if the owner
  installs them).

## Where to start
- **Story missions: `docs/STORY.md`** (the owner's decisions, what is built, how to test). The scenes are edited in
  `Tools/story/scenes/*.json` and built with `python Tools/story/build_story.py` (needs `pip install --user edge-tts`).
- **Cinematics + the trailer: section "Cinematics" below** (renders of the owner's camera timelines, the morning shot,
  the "First Glimpse of NainiVerse" trailer).
- **New chat on the free-roam batch: read `docs/FREEROAM.md` section "HANDOFF - 22 Nov 2026" first** (state, what
  was running, what is left, how to resume).
- `README.md` "Work in progress": the next-PC steps and what changed, by date. `docs/TRACKER.md`: the day-by-day log.
- Vehicles / the Vehicle Setup tool: `docs/VEHICLES.md` - **section 0.3 is the open list** (0.1 / 0.2: what the 21 Nov
  afternoon and evening passes did); tick items off there and put anything new that is wrong or changed into it (owner:
  "agar koi dikkat ho ya kuch change ho to push instruction pe daaldena").
- On the 20 fitted vehicles change seats with `Tools/vehicles/fit_vehicle_seats.py` (its MOVES / OVR hold hand fixes),
  not the tool's Snap (Snap follows the fitter's automatic rule only and would undo them).
- A vehicle setup changed (tool, fitter) -> run `call VehicleSetupShots.RunFits` for it (Play in the VehicleSetup scene):
  the game seats only the characters `Assets/Resources/VehicleFits.json` says fit. With the Asset Store riding pack in,
  re-run it for the six two-wheelers (they were measured on the procedural pose).

## Rules
- Commit / push only when the owner asks; push instructions for the next PC go into the docs above.
- Close the Unity editor when idle (RAM); run heavy jobs one at a time.
- Build World resets `bundleVersion` to 0.1.0 - never commit that.
- **Vehicle rules (owner, 21 Nov 2026, "yaad rkhna")**: whenever the player gets into ANY vehicle (car, bus, auto,
  e-rickshaw, bike, scooty) everyone else in it gets out; traffic shows its indicator before a turn; stuck traffic may
  flash its dipper now and then; a townsperson in a vehicle's way steps aside. Keep these true in every vehicle change
  (checks: `VehicleTest.RideBike` / `RideReport`, `SignalReport`, `JamReport`, `ColliderReport`, `PushTest`).
- **Free-roam batch (GTA-SA style, no missions; owner's answers 21 Nov 2026)**: `docs/FREEROAM.md` - his answer for
  every feature, the order, the status, what is NOT wanted, and the next item's design. Work it in order; tick it there.
- Play tests through the bridge: start Play with `call PlayTest.Begin` (skips the main menu; `VehicleSetupShots.Play`
  opens the menu, which blocks all input). Night: `call NightCheck.Run` (arg hour). Sounds: `call VehicleTest.SoundReport`.
- **Language (owner, 22 Nov 2026: "default will be english .. can be changable to hindi .. with hindi font .. and everywhere")**:
  English by default, Hindi in Devanagari (Settings > Language, the phone's Settings). Code writes Hinglish keys; every
  shown text goes through `Loc.T` / `Loc.L(hi, en)` / `Loc.F`. The table: `Tools/loc/strings*.tsv` (key, English, Hindi;
  '#' = a number; {0} = an argument). **After adding or changing any text: `python Tools/loc/build_loc.py`** (needs
  fonttools + uharfbuzz: `pip install --user fonttools uharfbuzz`; run it WITHOUT -I so the user site-packages load). It
  shapes the Hindi with HarfBuzz (uGUI cannot), extends the Rajdhani fonts in Resources/Fonts with Private Use Area glyphs
  (originals in Tools/loc/fonts_src) and writes Resources/Loc/strings.json. Never type Devanagari straight into code: it
  shows unshaped. Gaps: in the editor every shown text without an entry lands in Bridge/loc_missing.txt; scene texts:
  `call LocDump.Run` -> Bridge/loc_scene.tsv. World signs (3D letters) stay as built.
- Tools' Python with numpy on the main PC: `C:\Users\Ayush Singh\AppData\Local\Programs\Python\Python310\python.exe -I`
  (since the 26 Nov 2026 reinstall; it was under `Ayush Singth` before).
- **A component the world builder adds (AddComponent in Editor code) must live in its own file with the class's name**
  (`RestaurantSeat.cs`, `HouseBed.cs`): Unity saves a MonoBehaviour from a shared file as a MISSING script, so it works
  right after the build and is gone after the scene reloads (22 Nov: every restaurant chair and the house bed).
- Code check without the editor: `python -I Tools/pipeline/compile_check.py` (Unity's own compiler on the current files,
  7 s; minutes while Unity is building - run it in the background). Free-roam test steps: `Editor/FreeroamTest.cs`.

## Cinematics (27 Nov 2026)
- The owner makes camera shots as Timelines in Nainital.unity: **CinematicAnimation** (his own, 121 s, 10 shots cut by
  jumps in the recorded camera curve) and **MorningShot** (`call CinemaReel.MakeMorningShot` built it, edit mode; arg
  `force` remakes it over his edits: 14 s from 1 m over mid-lake water up to Naina Peak; 6:15 has the best sky).
- Render (Play first: `call PlayTest.Begin`): `call CinemaReel.Timeline`, Bridge/arg.txt:
  `frames 12 55 h15.5 h21` = 4K PNG stills at those seconds and hours; `shots [day|night] [n ...] [test] [hH]` = one 4K
  60 fps mp4 per camera cut (the town settles 12 s round each cut first) + the shots joined; `tl:<director>` another
  timeline (`tl:morningshot`); `clear` a clear sky (default cloudy). Day = 15:30, night = 21:00. Out:
  `D:\NainiTA---The-Sukoon\Cinematics\` (git-ignored, GBs). ~10 fps on the GTX 1060 (GPU-bound).
- Cinema mode (Runtime/Dev/CinemaRunner) puts the PC render pipeline on for the take (the editor's Android target runs
  Mobile_RPAsset: hard shadows, one cascade, no SSAO), 4096 x 4-cascade shadows over 300 m, cloud with the sun through it
  (`WeatherSystem.OvercastCap` 0.15, `DayNightCycle.ShadowMin` 0.85), the 8 nearest street lamps + 8 headlights casting
  soft shadows (`LampManager.ShadowLamps`; off in the game, a point light's shadow is six maps), libx264 crf 15 (NVENC
  needs a newer NVIDIA driver than 560.94). Everything is put back after the take.
- **While Unity renders / captures, run no heavy job on the PC** (ffmpeg, tests, compiles): the owner notices the load.
- The trailer **"First Glimpse of NainiVerse : The Story of Lake City"**: `Tools/trailer/` (`config.json` = every text,
  timing, place name, strobe, grade and sound; `build.py` = the edit). `python Tools/trailer/build.py all` (or `preview`
  / `graphics` / `audio` / `trailer` / `films` / `verify`): sunrise 6-10 s of the morning shot, then each timeline shot
  plays day -> strobe at its midpoint -> night at the same camera position, place titles "Mall Road (Day)" flipping to
  "(Night)" with the strobe, hook / title / clue / "Directed by @gamedevayush" / CTA / end card, a progress bar, game
  ambience + synthesised effects (no music, owner). Grade per clip: the renders' blacks sit at Y 40-55, so the black
  point comes down first. **Output 1080p60** (owner) to `Tools/trailer/out/` (git-ignored); fonts are fetched into
  `Tools/trailer/fonts/` (OFL, Google Fonts). The first trailer was made 27 Nov in
  `C:\Users\Ayush Singh\video-edits\nainiverse-first-glimpse\out\NainiVerse_First_Glimpse_1080p60.mp4`; the day / night
  films (`build.py films`) were not wanted that night.
