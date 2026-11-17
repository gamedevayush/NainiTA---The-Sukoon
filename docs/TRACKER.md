# NainiTA - work tracker (15 Nov 2026, afternoon)

The owner is away for ~2 hours (from ~14:45). Brief: keep going, decide yourself, don't get stuck, make it better and
better, find new reference videos, make the main spots and places very real.

Unity: never left open. Every build / shot runs as one batch (`Logs/run_photobatch.ps1`, steps in `Bridge/batch.txt`).
Shots land in `Shots/`; before/after pairs are listed here.

## Plan

| # | Task | Status |
|---|---|---|
| 1 | West-shore face across from Mallital (khaki band) | lake wall fixed; small band left OPEN, see 15:20 |
| 2 | Reference frames for the main spots from recent videos | done (contact sheets) |
| 3 | Main spots against the references: Boat House Club, yachts, Mallital ghat boats | done |
| 3b | Next spots: Mall Road shop fronts at street level, Tallital chowk, Naina Devi temple (no usable frames yet) | next |
| 4 | New iPhone IPA with everything (photoreal, HUD, world fixes) | running since 15:16 |

## Log

- 14:45 Tracker started. Last build: cliff trees accepted only ~150 spots; band pixel unchanged (131,132,113).
- 14:50 Cause: the 7-13 m road margin round Thandi Sadak covered the whole face. Cliff trees now skip the road margin but
  stay 8 m over the lake (above the path). Build running.
- 14:55 References: YouTube search page -> video ids -> auto-frames (i.ytimg hq1-3). Contact sheets in the scratchpad
  (`refs/sheet_*.jpg`). Useful: Boat House Club (white walls, red roof, turret, green-white balconies), the club's
  dinghies with broad two-colour striped sails, yellow wooden boats at the Mallital steps, green boats at the club
  jetty, teal lake in sun, bright shop signs on Mall Road at night, red ropeway tower and cabin (ours matches).
- 15:00 Done in code (next build): seven yachts with banded sails (yellow-green, red-yellow, white-red, yellow-blue,
  green-white, orange-yellow, white-blue) instead of four plain ones; boats at the ghats mostly yellow, 16 a side.
- 15:05 Built and checked: Boat House Club (`Shots/chk_boatclub.png`), ghat boats mostly yellow with canopies
  (`Shots/chk_ghat_boats.png`). Yachts are out on the lake (no frame caught one close).
- 15:05-15:20 The khaki band across from Mallital, measured pixel by pixel in shot 38 (600-800, 112-120):
  rock tint 0.82 -> 0.28: only 141,140,117 -> 131,132,113; lake wall capped at 3 m (it had climbed 10-45 m) - kept;
  basemap distance and tints baked into the textures: no effect (one "dark" reading, 64,76,94, was a run where the day
  shot came after the night shots - a lighting carry-over, not a fix). Left: basemapDistance 1600 (reverting it is
  fine if phones need it; it did not change the band), tints as remap. Likely cause now: specular sheen from the
  scan's alpha read as smoothness on a sun-facing 70-80 degree face. OPEN - small, 0.5-1 km away.
- 15:16 IPA pipeline started, unattended (`Logs/make_ipa.ps1`, progress in `Logs/make_ipa.log`): Unity batch iOS
  build -> zip -> GitHub release upload -> macOS workflow -> `Builds/NainiTA-unsigned.ipa`.
- 15:20 More references (Mall Road day walks, Naina Devi temple, Tallital): `refs/sheet_mall_day.jpg`,
  `refs/sheet_temple_tallital.jpg`. Temple: red gateway with white arch bands and Hindi boards, red and saffron flags -
  ours already matches (built from his own stills on 14 Nov). Tallital: Gandhi ji in dark bronze with a big spinning
  wheel beside him and a marigold garland - ours had the walking figure only.
- 15:25 In code (next build): charkha and marigold garland on the Tallital statue; preview shot 24 now takes the
  temple gate from its own camera marker (it looked for the old "Arch ..." name and was skipped every run).
  Waiting for the IPA's Unity step to finish (the project is locked while it runs).
- 15:30 IPA done on GitHub (run 43701483097) and downloaded: `Builds/NainiTA-unsigned.ipa` (711 MB). iTunes + iCloud
  installed; Sideloadly from winget failed (hash mismatch), so its installer was fetched from sideloadly.io
  (`Builds/SideloadlySetup64.exe`, unsigned) and opened for the owner.
- 15:35 Gandhi statue: charkha and garland built and seen (`Shots/chk_gandhi_close.png`).
- 15:50 Mall Road and Tallital roads were near black from street level (30,32,40): they use M_RoadConcrete, whose
  texture came out 88/255. Now pale cement grey (63,70,81 in shadeless sky light; 18 / 70 / 30 shots look right).
  M_Asphalt also duller (smoothness 0.22) and warmer.
- 21:30-22:45 Positions and rotations (owner: "church road pe aa raha hai ... baaki bhi bahut dikkatein"):
  `Editor/RoadAudit.cs` lists every built object with a road through it (2342 checked). Fixed:
  St. Francis Church (in a 24 m gap between Upper Mall and Snowview roads; its 26 m plinth ran over both, now 23 m);
  landmarks and civic buildings pushed off roads (`LandmarkBuilder.ClearOfRoads`: Govt Primary School Tallital 2.7 m,
  Bada Bazar 6.7 m, Eco Cave 6.3 m); lookouts (Land's End, Himalaya Darshan, Lover's Point, both Khurpatal points) now
  railed ledges at their road's level with a wall down the drop, no flatten pad. Khurpatal View Point stood on a 260 m
  tower: the "above Naini lake level" floor applied everywhere, and Khurpatal is 260 m below the lake - now only within
  200 m of the lake (landmarks and G.O.A.N. civic). Left in the audit, checked and fine: Boat House Club (jetty bounds),
  Bada Bazar and Eco Cave (beside their roads), ropeway and prayer flags (long objects).
- Khaki band explained: painting each terrain layer red showed it is the rock layer, but ~95 of its brightness is
  added light (haze over a bare sunlit face), so no tint can darken it. Only trees on that face would. Left as is.
- Probe trap: a terrain layer changed in memory in batch mode is saved to disk (TL_Grass came out red); a world
  build regenerates the layers. Never edit generated assets in a probe without rebuilding after.
- 22:30-23:30 Satellite check (owner: "google map se har building ke shape check karo"). Esri World Imagery (not
  Google tiles: scripted Google downloads break its terms) against an orthographic top view, same frame. Found: the
  shop rows replaced real footprints and some stood on trees. Restored 863 real footprints, dropped 220 made-up row
  houses (on real buildings, open ground or trees; Mall Road row kept), roof colours of 986 buildings from the photo.
  Comparisons: Shots/satcompare/cmp_*.jpg (satellite | game | blend), fp_*.jpg (data outlines on the photo).

## 16 Nov 2026 - roads into the terrain, Capitol area (owner's 9 screenshots)
- Roads are no longer meshes: every carriageway, footpath, junction and paving is painted into an 8192 road mask
  (asphalt / cement / grey pavers / red pavers, 0.73 m a texel) read by `Shaders/NainitalTerrain.shader` (URP
  TerrainLit + `NainitalTerrainRoads.hlsl`): one surface, frayed dusty edges, grime everywhere, damp patches, wet in
  the rain (`Runtime/World/TerrainRoads.cs` follows WeatherSystem.Rain). Phones: ASTC 6x6 at 4096.
  The ground under a painted road is set to its surface (smoothed over +-8 m), averaged where roads overlap, banked
  2 samples beside it (`WorldBuilder.Carve.cs`), right after the roads and again at the end. Dirt tracks go into the
  TL_Dirt splat layer. Painted: junctions, Lower Mall, Tallital chowk + town ground, Bada Bazar ground, bus yard, zoo
  approach, Tibetan plaza, children's park earth. Still meshes: Upper Mall deck (two levels with a wall between - a
  heightfield cannot step), its footway and parking; the lake road's white lines are a 6 cm decal.
- Capitol (satellite + photos): the OSM box stood 1.3 m inside the walk - pushed clear (Mallital.cs CapitolBox).
  Children's park is a terrace at the walk's level with 1.5 m stone retaining walls; the lane, Capitol and Video
  Games hall low and red-paved; walk and the strip by the park grey. Stairs moved out of the park: in the lane's corner
  against the walk wall and the park wall, parallel to the walk; railing stops at their landing. Video Games hall now
  from the Capitol to a narrow lane above the lakeside path (14 x 33 m). New "Capitol Boat Stand" jetty below it.
  Walls are 1.5 m thick with low ground under them: the terrain's 1.46 m sample step otherwise left a ramp at the
  wall's foot (first try, shot stairs_lane). The Capitol shaping now runs after LevelRoads (its 2.5 m ease filled the
  lane up to the walk).
- Lakeside lamps/benches/stalls kept off roads (Clearance): 13 lamps moved, 5 lamps and 5 benches left out.
- Jama Masjid road: smooth (painted, ground set to the smoothed line).
- `Editor/ShaderCheck.cs`: bridge "call ShaderCheck.Run" writes shader compile messages to Logs/shadercheck.txt.
- Final: the lane, Capitol, hall and lakeside path are ONE low level (the path's, 748.2; walk 750.3): at 1.2 m under
  the walk the lane rose in a hump to the path (shot hall_lane). Stairs 13 steps. Checked: Shots/stairs_lane,
  stairs_high, cap_stairs, cap_hall, cap_park, hall_lane, lane_front, 34_capitol_day, masjid_road2, thandi.
- 03:41 IPA with all of the above: Builds/NainiTA-unsigned.ipa (652 MB), GitHub run 43766292018. Pushed as 97ff978 (main).
- 03:55 Sideloadly: "no Apple devices detected" = Apple Mobile Device Support missing (winget iTunes skipped it);
  installed from Apple's signed iTunes installer (Tools/pipeline/install_apple_for_sideloadly.ps1).
- 04:00 Install failed: "ApplicationVerificationFailed ... UnityRuntime.framework : 0xe8008001", phone: "integrity
  could not be verified". Unity 6000.4 ships UnityRuntime.framework pre-signed by Unity, FAT with one arm64 slice;
  UnityFramework (built unsigned) was fine. Tools/pipeline/fix_ipa_frameworks.py drops its _CodeSignature, thins it,
  marks it iPhoneOS; the IPA chain now runs it after the download. Original kept as Builds/NainiTA-unsigned.before_fix.ipa.

## 17 Nov 2026 - iPhone bug reports (8) and the owner's 6 screenshots
Done on PC "AYUSH" in Unity 6000.3.28f1 (repo stays 6000.4.1f1; 6000.3's import changes to .meta / ProjectSettings not committed).
- Bug reports pulled from the iPhone over USB (`Tools/pull_bugs.py`); `BugReview` takes `OnlyPrefix` / env `NT_BUGS`.
  Before/after shots: `Shots/bugs_1116_before`, `Shots/bugs_1116_after`, `Shots/fix*.png`.
- ROOT CAUSE of the phone-only ground bugs: URP `UnpackHeightmap(real4)`; `real` = half on phones -> heights snap to
  ~0.7 m at our 1437 m range. Fix 1: `NainitalTerrainHeight.hlsl` redefines it in float before TerrainLitInput in every
  pass. The owner still saw grass through the Upper Mall walk and feet off the ground on that IPA, so fix 2: phones draw
  the terrain non-instanced (CPU mesh = TerrainCollider heights), pixel error 1 (`GameManager.Start`). Check FPS.
- The Upper Mall deck was never in `roadStrips` ("0 lowered under slabs"): now carved 40 cm under its top; ends ramped.
- Road mask is now a signed-distance field (Gustavson AA EDT from 4x4-sampled coverage, +-4 m); shader cuts at 0, overlap
  priority cement > asphalt > grey > red. Phones ASTC 4x4. The mask step of a world build is ~12 min (BC7 8192 import);
  `compressionQuality = 0` did not make it faster.
- A Metal compile check via `ShaderUtil.OpenCompiledShader` tried all 309,120 terrain variants and hung the editor:
  don't.
- In-editor world build over MCP: call `WorldBuilder.BuildMenu` directly (a delayCall is wiped by the domain reload).
- Layout fixes: help centre on the divider nose; bandstand at the water ahead of the T (40,19)+(16,-10)*6.3; DSA road
  cement + one grade; mapped trees off all roads; lakeside path Capitol->T one grade; wall ledges (`InnerLedge`); park
  fence on the coping; Lower Mall level +-15 m; ground 0.8 m past its lake edge.
- Audit: no bumps > 25 cm on carriageways within 400 m of Mallital (flags were props on footways and Mall stairs).
- IPAs: 13:47 (shader fix) and 15:19 (+ non-instanced phone terrain, deck carve). `make_ipa.ps1` now finds Unity from
  ProjectVersion; without Git Bash it runs `ios_ipa_chain.ps1` (gh). Trap fixed there: PowerShell `$r` is `$R`.
- Installed here: iTunes, iCloud, Apple Mobile Device Support, Sideloadly (winget hash stale -> sideloadly.io /S).
- OPEN: owner to test the 15:19 IPA (README "To test on the iPhone").
