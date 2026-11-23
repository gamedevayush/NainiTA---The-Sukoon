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

## 17 Nov 2026 (later) - front end: splash, loading, main menu, pause, settings, save, intro, haptics
Owner: "map ka kaam stay, UI khatarnaak - splash, uttarakhandi loading with photos, main menu (resume, save, settings,
credits khaali, exit), phone based, vibrations, black fade, GTA-style map-to-player in 3 s, Hindi + English".
- `Scenes/Boot.unity` (build scene 0, made by `FrontEndSetup.MakeBootScene`; build scripts use `FrontEndSetup.BuildScenes()`):
  `Boot.cs` = Aipan splash with temple bell, language on first start, loading screen (10 Uttarakhand photos from Wikimedia
  Commons in `Assets/Resources/Loading`, CC BY-SA, photographer credited on screen; facts, tips, progress). Nainital
  loads async behind it and the loading screen stays until `FrontEnd` says the world is warm.
- `FrontEnd.cs` (made by GameManager): main menu over the live world (camera drifting up Naini Lake), Continue / New Game /
  Settings / Credits (empty) / Quit; pause menu (Resume, Save, Missions, Settings, Abandon Mission, Main Menu, Quit + a
  "where you are" card); confirm dialogs; intro = black, top-down 380 m -> 140 m -> 42 m, swing into the third-person
  camera, 3 s, HUD fades in. Runs at execution order 200 to override the camera.
- `GameSettings.cs` (master / ambience / effects volume, look speed, invert Y, vibration, quality, 30/60 fps, language),
  `SaveGame.cs` (position, hour, weather, place; manual, every 90 s, on app pause), `Haptics.cs` +
  `Plugins/iOS/NainitalHaptics.mm` (Taptic Engine; Android VibrationEffect), `Aipan.cs` (procedural Kumaoni art), `UiKit.cs`.
- HUD: old pause panel gone; top right = Menu / Map / more (tray: Din-Raat, Help, Mausam, Cheat, Bug). Taps on buttons,
  hits, crashes, mission pass/fail, money, last 10 s of a timer.
- Editor: `PlayTest` / `HudCheck` set PlayerPrefs `nt_skip_menu` (straight into play). `FrontEndCheck.Run(folder)` plays
  from Boot at 2316x1080 and screenshots every screen.
- OPEN: test on the iPhone (haptics, the intro over Mallital boat stand roof, load time); no IPA built yet.

## 17 Nov 2026 (night) - fake interiors (photos) on ground floors, Mall Road shop fronts by kind
Owner: "unity fake interior shader pdho... saari building or shops pe lgao, only ground floor, phle check kro heavy to
nhi pdega"; then the Bgolus interior package from a YouTube video (InteriorShaderAll.unitypackage, read, not imported);
"phone pe chlega?" -> photos instead of painted rooms, "crowd bhi photos me" (Indian; eating in restaurants, standing in
shops); "mall road pe same type ki shops mat rakho - restaurant, shops, road shops, rooftop cafe, front look random";
"low poly smaan htaao, fake interior dikhega".
- Tried first: Bgolus 2D-atlas box interior mapping with 16 procedurally painted rooms (InteriorArt.cs, removed). It
  worked (Shots/int_* of the first build) but carried 7 floats per atlas vertex and needed screen-derivative frames
  (the building meshes have no tangents: MeshBuilder never calls RecalculateTangents). Replaced by plane parallax on
  photos: one texture read + ~25 ALU per glass pixel, nothing per vertex.
- Flow: Brave Profile 1 on CDP 9333, project 1:1 x1 Nano Banana Pro, 32 photos in ~15 min, no usage limit hit. A prompt
  "from the open doorway" gave the door frame and the street; "from just inside the entrance, only the interior" fixed it.
  Sparkle at (900..944, 900..944) on 1024 x 1024, painted out with LaMa in pack_rooms.py.
- Reference: v01 / v02 re-downloaded at 480p to D:\Unity\ref\yt10\01, 02 (the 14 Nov frames were not on this PC);
  v02 every 8 s in D:\Unity\ref\yt10\sheets\v02_*.jpg. Mall Road timeline (Tallital gate 4:20 -> Mallital 28:20) is
  the table in WorldBuilder.MallFronts.cs; anchors St. Francis church 8:48 and the Grand Hotel arch 23:48.
- Measured (batch, no GUI): rooms on vs off at 5 street views, 4K: +0.00..+0.06 ms (0.0-0.2 %); 1080p within +-0.3 ms.
  With the editor GUI open the same test swung +-11 ms: never time with the GUI open.
- Build: 110 Mall Road fronts -> 177 units (88 glass, 57 open, 32 shut), 8 rooftop cafes. First build: 29 units were
  the heritage hotel (the long-wall stretch alternated shut / hotel) and the shop photos were mirrored (ShopGlass took
  x along cross(up, N), which is to the LEFT of someone facing the front); both fixed. Kiosks: 0 at 2.6 m out (all
  in the road), now on the step in front of a shut shop.
- Trap: FXC flattens an if() that holds a hardware-derivative texture read; inside such a branch use SAMPLE_GRAD with
  derivatives taken outside it (or finite differences), or the branch runs on every pixel.
- Second and third builds: 169 units (80 glass, 56 open, 33 shut; 8 skipped where two houses overlap), 19 kiosks on the
  steps of shut shops (Upper Mall's road width reaches the shop line, so a road clearance test refused every kiosk).
- A speckle on some glass fronts (photo pixels mixed with another tile's) was the room index: a half varying turned 8
  into 7.9999, and fmod/floor then flipped between two tiles per pixel. Now a float varying, rounded in the fragment,
  and the tile lookup rounds too. Found with InteriorCost.GlassDebug (flat colour per tag + a log of the glass quads:
  no overlaps, tags whole). Two other fixes on the way: ShopGlass reflects _GlossyEnvironmentCubeMap (unity_SpecCube0
  is not set up in Forward+), and its depth passes use GetVertexPositionInputs like the forward pass (depth priming).
- OPEN: owner to look; per-unit signboards (the old one board per building still spans its units); IPA not built.
- 18 Nov: owner "fake interior thoda zoomed in, thoda chote or andar ko": photo 4.2 -> 4.8 m, back planes deeper (Mall
  glass 1.9 -> 3.0 m, open 2.4 -> 3.4, windows 1.8 -> 2.6, bazaar shops 2.4 -> 3.2), photo middle at 1.5 m; all in the
  shader constants (ShopGlass no longer reads the material's _Depth).
- 18 Nov: "thoda neeche krdo saare": NT_PHOTO_Y0 -0.9 -> -1.3 (the photos' middle at 1.1 m above the floor).
- 18 Nov: owner "tool jisse main set kr sku, pos and zoom wagera sab, for all": Nainital -> Fake Interior Look
  (Editor/InteriorLookWindow.cs) edits Assets/Resources/InteriorLook.asset (Runtime/World/InteriorLook.cs, applied as
  shader globals at start and live). The constants in NainitalRooms.hlsl / AtlasForwardPass / ShopGlass became globals
  (_NtMall*, _NtBay*, _NtRoom*, _NtShopDayLight, _NtMaxSlide) with the old values as defaults while _NtLookSet = 0.
- 18 Nov: "mesh hi badi hai, x,y scaling bhi de": the tool now also sets the Mall Road opening (width share of the front,
  sill and top in metres; outside it a panel in a chosen colour - done in ShopGlass's fragment, the opening's half width
  comes from |uv.x| of the corners) and a photo X / Y stretch for both groups. Mullions and kick plates are geometry and
  stay where they are until the next build; bake the chosen opening into WorldBuilder.MallFronts.cs when it is settled.

## 18 Nov 2026 - 3D lettering on every sign, font by shop kind
Owner: "saare jahan jahan text hai, vo bahut basic hai - usko tagda 3D name kardo, different stylish font based on their
category". All world text was TextMesh in the built-in font (~700 shop signs = ~700 renderers).
- `Tools/fonts3d/make_fonts3d.py`: eight OFL fonts (Tools/fonts3d/ttf, from google/fonts) -> outlines flattened (2-6
  pieces a curve), holes bridged into their outer contour, ear clipping, written to Assets/Nainital/Data/Fonts3D/*.json
  (cap height 1). Pure Python + fontTools (a pip install of mapbox_earcut was refused, not needed). Variable fonts
  (Baloo 2, Cinzel) came out broken - their contours overlap - so static ones are used: Lilita One, Cinzel Decorative.
  `preview_fonts3d.py` draws the faces to check holes.
- `Editor/Glyph3D.cs`: extrudes a line of text into a MeshBuilder (face proud of the board, sides in a second colour),
  styles per shop kind (font, board, letters, glow chance: some letters in the lamp-bulb colour glow at night), names
  per kind. Restaurants/cafes Pacifico, sweets/bakery Lilita One, hotels Cinzel Decorative, crafts Yatra One,
  woollens Rozha One, clothes Bebas Neue, services Anton, the Capitol Limelight.
- Used by: Mall Road units (a board + name each, `MallSign`; shut shops keep a board; kiosks have their name),
  the other shop boards (`Details.cs`, style from the name's words), `Label()` (Capitol, Video Games, Zoom Land, Naina
  Devi gate, police / help centres, road signs, the zoo arch), the welcome gate. Letters sit in 40 m chunks without
  shadows, culled far off. Credits list the fonts.
- Build (18 Nov 02:43): 700 bazaar boards with 3D names; Mall Road 169 units each with its own board (the open item
  "per-unit signboards" is done), 22 kiosks with names. First look: the Mall boards hid behind the colonial verandah -
  the step lifts the shop floor to the road, often ~0.5 m over the house floor, so a 2.75 m front ran into the
  first-floor slab. Now each front stops under it (H = house floor + 2.9 - shop floor, 2.0..2.75; the house floor is
  the shop-front point's y - 2.75). Checked: Shots/sign1540*.png, sign1080.png, mf1540_shop.png, mf1080_shop.png.
- 18 Nov 03:29: IPA built from ee07842 (Unity iOS build 3.8 min, GitHub Mac workflow ~15 min), `IPA frameworks ok: 2`,
  Builds/NainiTA-unsigned.ipa + the ios-build release. README "To test on the iPhone (IPA of 18 Nov)" lists what to try.

## 18 Nov 2026 (evening) - police (3 levels), the 108 ambulance, bikes and scooties in the traffic
Owner: "police or ambulance system tayyar kro proper... koi marta hai to police aayegi phir ambulance, traffic ruk
jaayega, ambulance gaadi me daal ke le jaayegi... police ke 3 levels: basic maarne pe 1 star (bike se ya run karte
huye, guns focus karke fade out), poora maar diya to 2 (car, saamne to arrest), 3 = 3 log ya 2 police... basic
pistol, car / bike / running se bhi shoot... health usi base pe jaayegi"; then "normal traffic me bhi bikes or scooty,
police aur ambulance me siren aur top lights"; then (after seeing it) "ruke hain to andar se goli kyun? bahar aake
maare... bhaag raha hoon to baith ke chase kare".
- Assets: police_1 / police_2 from the kit's PoliceGuys (206k tris each) -> `Tools/decimate_rigged_fbx.py` (Mixamo rig
  kept, 18k + 6k LOD); Ambulance108 from the kit's Sultanpur AmbulanceFinal (Force Traveller, 13.7k); Splendor
  (EtahMinorCar splendor_dec, front +y) and Scooty (KochiMiniCooper scooter_dec, front -y) via make_vehicle_lods.py.
  Sounds from Mixkit (ids paired with titles from the listing page's HTML: the id comes before its title):
  1641 police siren (fast hi-lo), 1642 ambulance wail, 1662 gun shot; one exact siren cycle each, short cross-fade.
- Code: Runtime/Police (see README). Townspeople got Health 60, so punches and car hits now hurt and kill them.
- Bugs found in play tests and fixed: a killing blow was not blamed on the player (Health is already 0 when Damaged
  fires; the attacker is now recorded before the dead check) -> kills gave 0 stars; the 2-star clock ran while the
  police were still on the way (now it starts once they have seen you; never seen: 2 min); a car stuck behind traffic
  never let its men out (now: blocked 3 s or no progress 6 s within 75 m -> deploy); the ambulance and the held traffic
  faced each other and both waited (RouteDriver now goes round on the right if there is room and asks a TrafficCar to
  back off, TrafficCar.MakeWay); spawned police / ambulance faced away from their route (now face along it); a bike
  standing still could not turn (it walks round now); 4 constables at 3 stars took 90 health in 6 s (now a shot at
  most every 0.35 s from all police together, 7 a hit, lower accuracy: one constable ~30 s); a deployed car pulled its
  men out again each time they sat back in; a scene guard stayed on guard after the chase.
- Not done / open: police are slow through dense Mall Road crowds (they stop short and walk); the player cannot ride a
  bike; bikes' wheels do not spin; no door animations on the ambulance; night look of the light bars not checked on a
  phone; frame rate with 2 police cars + ambulance + 8 bikes not measured on the iPhone yet.

## 18 Nov 2026 (night) - Mixamo animations, vehicle physics, traffic AI, seats, tyres
Owner: "player ke aur characters ke legs theek nahi (avatar problem maybe); gun shoot, run, walk, shout, punch, kick
aur jitne animation use ho rahe hain vo Mixamo se uthaao; AI traffic dumb, wall check nahi; running vehicle chhoone pe
heavily move; doosri gaadi takraye to light balloon; khaali gaadi physics pe react nahi; vehicles pe player aur NPC
position theek nahi; tyres theek se cut nahi"; then "iska ek basic pack aata hai" (Mixamo packs); then "bhai kuch theek
nahi hua, saare animations kharab, bahut kharab bhaag raha hai, tumne kya check kiya?".
- Mixamo in Brave Profile 1 (signed in through Google, CDP 9333). The pack zip export fails through the API ("The job
  failed"), so each motion of a pack is exported as a single Motion from its gms_hash. 57 clips, X Bot, no skin, 30 fps.
- What I checked first was not enough: a pose sheet (one frame per clip, editor) and one photo from behind. The run in
  play was broken. Films (`AnimFilm`) and the legs' muscle ranges (`AnimKit.SwingReport`) found it: the Jogging clip is
  2.6 s of several strides; in the blend tree with the 0.7 s run cycle the legs stopped. Jogging is out; walk (1.5 m/s)
  to run (4.4 m/s); faster plays the run faster. Avatars were never the problem (Animator-only film of an NPC walks right).
- Physics: Unity's default max depenetration velocity is unlimited - interpenetrating cars were thrown apart ("balloon").
  Now 2.5 m/s, max angular 7 rad/s, 12/4 solver iterations, a body material (friction 0.35-0.45, bounce 0.02). Parked
  cars were kinematic (a wall): now dynamic and asleep. People: a CharacterController is immovable to physics, so a car
  touching one stopped dead; the car now keeps its pre-step velocity, ignores that person 1.5 s and knocks them aside.
  Crash test: WagonR 950 kg at 14 m/s into a parked Bolero 1700 kg -> Bolero peak 4.5 m/s (momentum says 4.9), moved
  1.5 m, no rise, spin 1.4 rad/s.
- Traffic: first feelers stopped cars at every walled bend (9 of 10 standing after 95 s); now only a wall straight ahead
  brakes, side walls nudge the wheel (max a third of lock), and a car pressing on without moving backs out. 2 min sample:
  mean 2.4-3.1 m/s, 3-7 of 10 standing (queues), 5 knocks.
- Tyres: `Tools/recut_wheels.py` (lower-half tread fit with an air gap outside, one size per car / per axle for buses,
  measured width). Applied to Ambulance108, TouristBus, TouristCoach, HondaAmaze, Tata407 (renders in the scratchpad).
- Seats: `VehicleSeats` from the front axle and the body; drivers' heads were through the roof of small cars -> hip height
  capped at H - 1.1 m. HumanoidRig.SeatLock puts any sitter's hips at the seat point.
- Open: police pistol pose only seen from a distance; bus / truck driver heights and the player's view inside not checked
  close up; NPC flee speed blends walk/run (fine in films so far); the traffic still queues on narrow bends.

## 19 Nov 2026 - Eco Cave Garden and Lover's Point / Suicide Point from the owner's photos
Owner: "nainital eco cave park bnna hai theek se, abhi to khraab sa hai, iske andr ka map dhundho, bahut badi cave hain,
4-5 hain, texture and sample maine dediya" (9 photos + the park's guide map), "krdo"; then Street View of Lover's Point
and a sketch on the satellite ("orange means stairs to down, green in plain part, baaki buildings sattelite view mein").
- Eco Cave: the guide map (not to scale) fitted to the Esri satellite and OSM (scratchpad ecocave/eco_layout.jpg). Park
  frame u along Kaladhungi road (NE), v up the slope (SE), origin at the gate (-937, 182). The frame is a mirror image
  (U x V = -1): bends written in u/v flip in world space - the first build put Apes Cave's rock 3.3 m from the NE walk;
  bends negated, rechecked in world space (check_world.py: no walk within 3.6 m of a cave body).
- Caves: a Catmull-Rom axis every 0.35 m, the floor = ground smoothed over 5 m and pinned at both mouths; inner passage
  (superellipse arch, Perlin roughness, smooth normals, darker tint), flat floor, an outer rock mass with a skirt into
  the ground, thinning to a lip at each mouth; boulders round the mouths, on the back and round the foot. First look was
  a smooth brown dome (every rock scan on disk is brown): rock_grey made from rock_face (desaturated, lifted) and the
  rock made lumpier. Boulders on the back hung into the passage (centre at 0.55-0.85 of the rock height): now kept above
  the roof. Inside it was near black (the sun's shadow, ambient only): lighter tint and a 5.5 m point light per bulb.
- Walks: nodes every 2.5 m at the smoothed ground (+0.12), steps where steeper than 12 %, slabs walled down to below the
  ground; the ground is cut under walks and caves, never raised. Spurs join each cave mouth to the nearest walk at the
  cave floor's level. 60 trees added on the open slope (the hillside fill skips slopes under 14 degrees).
- The old 13 m pad at the Eco Cave POI is gone (it flattened the bottom of the park).
- Lover's Point: the walk from the sketch (green = level, orange = steps down; 11.2 m down in all), the buildings from
  the satellite shifted +12 m east onto the game road. The DEM falls ~33 degrees west of the hairpin, so the verge, the
  walk and the sheds stand on a shaped shelf (fill up to ~25 m at the ledge, blend band growing with the height, rock
  painted on the steep faces). First shaped before LevelRoads from the raw road height (1.5 m low) - now shaped in
  BuildLoversPoint from the levelled road. A mistake of mine on the way: a height grid script overwrote the map centre
  variables and showed the west rising; the real grid falls.
- Verge first read black (tint 0.72 on worn concrete): lightened; the sheds' tin roofs were dark: tin_light made from
  corrugated_iron_02. The walk's railings ran onto the ledge: they stop at the last flight.
- Open: no horses (no model); the caves' passages checked from two inside shots only (Tiger, Apes), not walked through
  in play; Lover's terrace seen from the road only at a distance.
- Khurpatal Lake View Point (owner: Street View of the hairpin, 2 photos of the clearing, a sketch - "green in plain, red
  in rocky stairs or just kinda rock trek, whole place is full of trees"). The sketch's map was turned 90 degrees (its
  down is west: the hairpin's tip points right, the bare slopes are top-right, SE in the imagery); scale from the gap
  between the two arms (~17 m = 160 px). The data hut at (-1862, -1124) is the stall by the path's head in the sketch,
  which fixed the orientation. As at Lover's Point the imagery lies ~12 m west of the game road, so the walk hangs off
  the game road's west arm: 17 m level-ish dirt (-2.5 m), 12 m of rock steps (-4.9 m), a 5.5 m clearing. The DEM falls
  ~20 m there: the spur is shaped from the levelled road (crest at the walk's level, flanks at ~35 degrees down to the
  hillside), cypress / oak / deodar added along it (the crest is too gentle for the hillside fill).
- The editor was opened at the owner's word ("pehle unity kholo tbhi krna"); later he said it loaded too long ("band krke krlo"),
  so the rest went back to batch builds (~9.5 min each against ~14 in the editor).
- Owner on a screenshot: "khurpatal view point is not red, its at green" - it is at the hairpin's TIP, not on its west arm
  (I had read the turned sketch as the west arm). Moved: pull-off round the tip, the walk straight out and bending to
  Khurpatal (bearing ~222). For a while I also moved it to the road above Khurpatal lake after his map of the lake - wrong;
  the map was for the temple and the lake. Mansa Devi Mandir ("just above it on road"): on the knoll inside the hairpin.
- Khurpatal lake: its own water material (turquoise, M_WaterKhurpatal) from his photos.
- The spur's crest ran on past the walk's end as a causeway into the valley (Project clamps at the end): past the end the
  distance to the end node is used now.
- Sariyatal: the water polygon reached the road at the north end, where the park is: clipped at z -1168 (ClipSouth in
  BuildPonds), the park's ground set just above the water. West road raised to the lake (the DEM had it 12 m under the
  water) in the levelling step, stopping at the south tip so the falls' ravine keeps its own ground.
- Woodland Waterfall from his sketch (north-up, 5.3 px/m, anchored on the OSM "Sariyatal Falls" point = the gate). First
  build set the stream's levels from the top down: it floated up to 19 m over the ravine. Now from the ravine floor, with
  each pool's lip lifted 3.5 m on a rock ledge.
- Main roads black (owner: "all yellow roads are hard black concrete with white striped roads, all grey are normal grey"):
  the mask's second channel (was cement concrete for the lake roads) is the main roads' black tarmac (asphalt_02 unlifted,
  its normal map), white lines on every main road; the shader already put that channel over the grey at junctions.
- Top 10 (owner: "saare points photorealistic, top 10 places ke photos uthaao ... saare banao ... check, push, PC
  shutdown"): photos from Wikimedia Commons (Google Street View needs a browser session; Commons rate-limited a few). Raj
  Bhavan rebuilt on its OSM footprint (the generic extrusion of that footprint is skipped now), the zoo gate and two pens,
  a photo pass on Naina Peak, Tiffin Top and Land's End. Naini lake, Naina Devi, Mall Road, Snow View, Eco Cave were
  already built from photos.
- Land's End: its view shot looked at an upside-down forest with no ground - the ledge and its camera were under the
  hillside (the OSM point is up the bank and the lookout took the point's side of the road). Lookouts now always take the
  road's lower side (the valley), and when the given bearing points back over the road they face away from it. Himalaya
  Darshan and the Khurpatal View Point moved to their valley sides with it (both checked: the snow range, the lake).
- Checked in the last build (shots): Eco Cave inside / mouths, Lover's verge / walk / ledge, Khurpatal tip / temple /
  clearing, Sariyatal park / shore, Woodland gate / stairs / falls, the black main roads (Kaladhungi, Mall Road, Tallital
  grey), Raj Bhavan, the zoo gate, Naina Peak, Tiffin Top, Land's End, Himalaya Darshan, Khurpatal View Point.
  Not walked in play: the caves' passages, the Woodland stairs, Raj Bhavan's porch.


## 19 Nov 2026 (night) - 20 Nov - Mall Road joins, an unbranded vehicle set, the Vehicle Setup tool
Owner: pulled ebd2ff0 (Street View fit, made on the other PC), Build World; "ye real brand ki cars daalna theek rahega?" ->
"2 bus, 10 gaadiyan, 5 bikes, 3 scooty, 1 tirri, 1 tuktuk generate karvaalo, kisi company se match na khaaye, no logo,
kinda Indian, tripo ko best images"; "ek unity ka scene ... saari vehicles ki setting ... headlights, backlights, driver pose
with another person ... capacity jitne character ... door alag aur 5 side damage preview ... same for bikes and scooty ...
player kaise baithega, purane ko kaise hatayega, tyres aage peeche"; "bike pe baithne ka, car drive, sab ka humanoid
animation nikaal lena"; "mall road ki haalat kharab, terrain ubad khabad near toll plaza, kisi ghar me na ghuse";
"bike me handle ke child me front tyre"; "right and left hand pose ... steering for car, handle for bike scooty";
"auto ke liye three wheeled and handle too"; then "sab push karo with instructions".
- Vehicles: full write-up in docs/VEHICLES.md. Flow (Nano Banana Pro) copies real models from plain prompts (Matiz, Cube,
  Innova, Delica, Bajaj RE) and badges noses (Tata T, Renault diamond): per-vehicle briefs with "must not look like ..." and
  a bare-paint badge spot fixed it; every nose zoomed before Tripo. Flow's grid is virtualised: the first run saved the
  wrong image under 11 names (shifted by one) - the script now takes only the never-seen top-left tile. Tripo: 22 models,
  first 5 with PBR (30 credits), the rest PBR off (25). Front per model checked on 4-view sheets (cars +X, most
  two-wheelers +Y, buses / tirri / two scooties / one bike -Y); scale by length alone made the buses 3.6 m wide -> median
  of the three ratios.
- Owner cut the parts himself (20 Nov, his own names); fixed in his open Blender over the blender MCP and saved as
  NewVehicles_v05.blend: names, Wheel_F under the Handlebar, Handlebar origin + raked axis through the front wheel centre,
  door hinges on the front edge, loose verts (truck 2, van 8, coach door 4 - they only spoilt the bounding boxes), 3 van
  wheel faces and 2 coach-door faces back to the Body. The "origin off" numbers were along the axle (harmless).
- Export: an excluded collection exported empty FBX (19 of 20 at 0.0 MB) - export_unity.py now shows each collection.
  Unity left the FBX-embedded textures off the URP materials: FixMaterials builds them from the exported PNGs.
- Mixamo: 9 clips through the site's Download button (the token route was refused by the safety check); no motorcycle
  riding exists on Mixamo.
- Mall Road: Upper Mall ended 5.9 m (Tallital) and 6.2 m (Mallital) short of the links; 21 houses within 0.8 m of a
  carriageway. Fixed in world.json (fix_mall_road.py), world built, junction shots clean. MallProbe found more (README).
- 19 Nov ~22:35 the PC crashed (0x154) - BitLocker decrypting C/D/E/F at once while Unity and Blender ran; the 1 TB HDD
  was gone until a full power cycle. 20 Nov 03:20 the HDD was 100 % busy again with nothing of ours running: the v05 FBX
  were exported but not imported into Unity; HandIK / the handlebar and three-wheeler changes are not compiled yet.
- 20 Nov 04:00 hand-off (owner asleep): the owner had removed the retro 350 and the electric scooty on purpose (their FBX
  removed now) and mirrored the coach, the hatchback and the truck in X for right-hand drive ("sabka right hi ho").
  Found: Auto Keying was on - one-frame actions pin many transforms; the two bus doors have real fold/slide animations, so
  my 20 Nov hinge change left their meshes displaced; bike 13's handlebar key undid its raked frame. Nothing more changed
  in the blend; the full fix list for the next PC is docs/VEHICLES.md section 9 (diagnostics in Tools/newvehicles/diag).

## 20 Nov 2026 (night) - Vehicle Setup: lamp position, feet on the floor / pegs, riders reach the grips
Owner (main PC, after pulling cd09c69): "vehicle light transform position ka option nahi diya, or bike pe and car me bethne
ka animation bhi theek nahi hai.. vo dhoondh ke add karo".
- Unity batch at first refused: "No valid Unity Editor license" (Hub closed, sign-in expired); he signed in and opened the GUI.
- Measured, not guessed: `VehicleSetupShots` (check sheets with cut views + per-seat hand / foot misses), `Probe` (every
  surface under the cabin), `ClipPoseReport`. Sedan: people's feet under the car, the driver's ankle 0.15 m under the road;
  the floor (the underbody's inside) at 0.22 m; Tripo cabins have partial seats only. All Mixamo sitting clips are chair poses.
- Trap: Physics.RaycastAll gives ONE hit per collider - the car body is one mesh, so it only gave the roof; `AllHits` loops
  Collider.Raycast with back faces on. Trap: the hips over the seat root are 0.51 m for Sit / Drive (SeatLock) but 0.40 m for
  the riding pose; a first guess of 0.40 for all put the car foot targets 11 cm low.
- Done: feet IK + markers + Snap (floor-based, small moves only), lamp Position / Rotation / Snap to body, reach lean,
  grips from the bar mesh (a scooty's bar carries its apron: band 8-40 cm under the top), the rider an arm's reach behind
  the grips, pillion pegs 0.28 m ahead (straight under the hips looked stood up). All 20 defaults: hands / feet within ~15 cm.
- Found (free, Asset Store EULA): "Motorcycle Interaction Animset FREE" (SpectraMotions, May 2026) and "Basic Motions FREE"
  (sitting low = a car seat). Waiting for the owner to add them to his account; then wire them in AnimKit.
- Old: `[Nainital] car rescued (under the ground) at (0.0, 750.5, 0.0)` spams in Play (106x on 19 Nov, 53x today) - a car
  at the world origin falling through again and again; not looked into yet.
- 21:56 the owner imported both packs. SpectraMotions' is Humanoid with riding idle, lean loops (rider + passenger),
  mount / dismount; Kevin Iglesias' free version has no sitting clips (the search result described an older paid-era
  listing) - only its finger grip pose is used. Wired in AnimKit (Ride / PillionRide / Mount / Dismount / BikeStart / Grip
  layer), HumanoidRig (`lean`, SeatLock at 0.40 m for clip riding). Packs kept out of git (.gitignore; public repo).
- Found: CarEnter / CarExit / PulledOut / RemoveDriver / Honk / StandToSit / SitToStand never had animator parameters (19 Nov
  on) - the car enter / pulled-out moves never played. Fixed in AnimKit.
- Checked on the sheets: riders on AS_Idle_Riding with hands on the grips (0 cm), pillions on the passenger loop, the
  sedan's driver on Driving with hands on the wheel.
- 22:20 owner: "pehla itna jhuka kyun hai" (his bike_commuter100 rider folded over the bars). Causes measured: the pack's
  clips lean like a sport bike; the town's 12 own characters have arms of 0.18-0.39 m shoulder-to-wrist (half the Mixamo
  ones; ClipPoseReport.Arms); his saved L / R balls sit 0.62 m ahead of the hips at hip height. HandIK: Upright to a per-seat
  bend (rider 12, pillion 5; three passes - one fell short), reach lean capped at 20 deg, forearm stretch up to 1.35x.
  Riders 37-43 -> 24-29 deg, pillions 12-24 -> 5 deg. Tool: "Bend deg" field on Ride / Pillion seats.
- 22:50 owner: "kuch me front light handle ka child hongi". Lamp.on (the part's name) + "handle" toggle; Snap to body hangs
  the lamp on the part it hit; new handlebar vehicles snap their headlights by themselves. Checked with the Steer slider at
  30 deg (VehicleSetupWindow.SteerArg): dualsport / street160 / both scooties on the Handlebar and turning, commuter-125's
  headlight is in its Body mesh (stays), autos on the body.
- 23:10 owner's screenshot: everyone on bike_commuter100 lying back, arms up to the bars. Cause: the character animator was
  EMPTY in the editor's memory (0 parameters, 0 states; the file on disk was fine) - the AnimatorController calls in
  AnimKit.Build record Undo steps, and a Ctrl+Z while moving things in the tool walked back through them. With no clip the
  people fell to their default pose, SeatLock never ran (hips 0.03-0.20 m), the IK pulled hands / feet to the bike, and the
  log filled with 1.7 M "Parameter 'Speed' does not exist" lines (from 22:52). Fix: AnimKit.Build rebuilt, and it now clears
  the controller's Undo history (Undo.ClearUndo on every sub-asset). Check: rider in Ride, hips 0.95, bend 10, hands 0 cm;
  pillions in PillionRide; no missing-parameter lines. New checks: VehicleSetupShots.Diag, ClipPoseReport.Controller.

## 21 Nov 2026 (00:30-03:00) - the owner's 20 vehicle setups checked and fitted to the models
Owner (main PC): "unity kholo" (opened via WMI, Hub first), then he set up and approved all 20 vehicles (01:04-01:45):
"sab krdi hain maine..check kro". Then "krskte ho theek? perfect precision ke saath?", and at the end "iske baad agar koi
dikkat ho ya kuch change ho to push instruction pe daaldena..sab push krdo with updates and instructions".
- First check (sheets + JSON summary): 11 vehicles fine, 9 with clear faults (street160 rider over the seat + pillion in
  his lap, roadways 5/6 feet 52 cm short, tallboy driver inside his backrest, sedan driver 14 cm low, mpv_large front
  20 cm high, tirri rear pair's feet on the opposite bench, neoretro arms like wings, tirri left hand, scooty_family
  headlight quad at 254 deg).
- Measured instead of guessed: every surface down each seat's line every 5 cm (`VehicleSetupShots.ProfileZ`), two-sided
  cut views with a ruler (`SeatCuts`; a Tripo shell is one-sided, so from inside its floor / seats were invisible), side
  scans and the handlebar meshes' ends (`SideScan`, `Bar`), the characters' own bodies (`Offsets`).
- Found a systematic fault of the 20 Nov Snap rule: hips = floor + 0.27 ignores modelled cushions, so in every vehicle with
  one people sat 8-17 cm INTO it; bus_tourist's feet 12-28 cm under its floor (and seats 19/20 on top of 15/16);
  bus_roadways' back four on open floor; suv_boxy's rear three in the boot.
- Body offsets (22 characters, 4 poses): hips over the seat of the trousers 0.06-0.21 m, head top 0.64-1.01 m over the
  hips, ankle over the sole 0.07-0.10 seated / 0.11-0.15 astride. A fixed hips height cannot be right for all, so
  HumanoidRig now measures each skin once per pose and sits it on the surface (clamped +-0.07: hems), heads under `roof`.
- `Tools/vehicles/fit_vehicle_seats.py` refitted all 20 (with hand-set rows where the automatic read was wrong: filled
  footwells in mpv_large row 3 / suv_boxy rear, the tourist driver's cab, tirri side ledges, tuktuk raised floor).
- After: 3 runs x 20 sheets. Buses (43 people), mpv_compact, van, suv passengers, tirri, 5 pillions: 0-4 cm. Open
  (docs/VEHICLES.md section 0): low roofs sink tall characters 10-31 cm in hatch / tallboy / mpv_large / pickup / truck /
  tuktuk (owner to decide); street160 rider's hands 20 cm, tuktuk / scooty_family 10, sedan / suv_muscular drivers' feet
  9-10, commuter100 riders 7 / 12; Snap still on the old rule; the tourist driver by eye.
- Not touched: the `car rescued (under the ground)` spam; scooty_family's headlight quad turned 254 deg (double-sided,
  the beam uses the vehicle's forward - harmless but looks thin from the front).

## 21 Nov 2026 (20:00-22:00, main PC) - parked cars out of the lake, unbranded ambulance, dents where hit
Owner pulled da6c08d (the other PC's afternoon pass), then: "iphone build ko chodke baaki solve krdo". His own edits to
bike_commuter100.json / bus_roadways.json (20:38) kept.
- RunFits for the six two-wheelers with the riding pack in (riders 7-12 of 22, pillions 9-20).
- Owner: "double lights ki vehicle me mesh to 2 hain lights ki, but actual me 1 hi light hai" - the tool's night preview
  lit only the first lamp of a mirrored pair; the twin gets its own light (`VehicleSetupWindow.Glow`).
- The 19 Nov "car rescued (under the ground) at (0, 750.5, 0)" spam, found: EVERY parked vehicle (53) ended in the lake.
  VehicleFactory makes the body at the origin; ParkedVehicles / CheatCodes moved the transform without
  Physics.SyncTransforms, so the first FixedUpdate read the origin (under the terrain, the lake), Rescue lifted the car
  THERE and it fell through again and again. Sync added; Rescue skips an unsynced body. 2.5 min: 53 -> 0 rescues, 52/53
  parked within 3 m of their place (`PlayTest.ParkedReport`). Police spawns already synced.
- 108 ambulance -> the white car_van (PoliceKitBuilder; letters / light bar measured on it); the accident sequence ran to
  the end with it.
- Owner's choices (asked): dents at the hit point at play time, not Dmg_* keys (phone cost); doors only if a perfect cut
  is possible; small cabins as they are; Mall Road raw data later (process_data needs ~20 GB RAM, this PC 15.9).
- Dents: VehicleParts.DentMesh (mesh copied on the first knock, pushed in round the skin's nearest point; the contact is
  on the box collider 10-20 cm out - first try moved 16 vertices 0.8 cm, after snapping to the skin 219 / 253 vertices,
  9-13 cm at 15 m/s). FBX Read/Write (NewVehicleImport), reimported (`VehicleTest.ReimportNew`).
- Doors: untextured renders (diag/door_seams.py) - no door seams in the geometry, so not cut. Steering: the wheels are
  Tripo fragments mixed with the dashboard (diag/steering_pieces.py), so not separated.
- Trap (again): PowerShell variables are case-insensitive - a loop `$t` overwrote the tools path `$T`.
- 22:30 owner: "auto me seesha alag krna pdega..andar ka dikh nahi raha". The windscreen is painted opaque on the Body.
  First renders seemed to show no change after cutting - the blend was saved with the tuktuk's mesh in EDIT MODE, so
  scripts read / rendered stale data (fix: mode_set OBJECT first). Rays (diag/glass_rays.py) found the slab; three cuts:
  the first took the roof lip and the cab's curved corners, the second bit teeth into the roof lip (white gaps on the
  yellow roof), the third (|x| <= 0.50, y 1.08-1.52, |n.z| >= 0.6, |n.x| < 0.45) is clean. Glass -> M_VehicleGlass
  (alpha 0.22 / smooth 0.92 looked like a pale blue panel; now 0.15 / 0.7). NewVehicles_v07.blend; tuktuk re-exported,
  FixMaterials, VehicleKit.Build. Tool: VehicleSetupWindow.NightArg (the night preview was left on and darkened sheets).
- 23:15-00:30 owner: "bike scooty pe bethne ka abhi nhi bna kya? or yaad rkhna...kisi bhi vehicle me agar bethre hain
  to baaki saari utarjayenge..gaadi agar aage kahin turn hogi to indicator degi pehle...or stucked hain to random dipper"
  then "indicator chlra hain mst", then "saari gaadi ke proper collider check krna, abhi ek gaadi mere aarpaar chla gaya
  tha... NPC ke pass gaadi aaye to vo side hojaaye vrna jam lgjayega".
  - Indicators / dipper: RoadChoice + TurnPlan (plan 30 m ahead, signal >25 deg, U-turn right, 2.5 s hold), amber quads
    at the lamps' outer edges (VehicleParts.BuildIndicators), VehicleLamps.signal / Flash. SignalReport: 3-4 of 24 at a
    time, 9 dipper flicks / 1.5 min. Owner liked it as it is (a bigger in-cluster indicator was started, he stopped it).
  - The player on a bike / scooty: PlayerBike (E: it stops, rider + pillion off, Mount clip, VehicleParts.Sit),
    PlayerController.Mode.Bike. RideBike / RideReport / RideGo / RideOff: scooty_family, 0 people left on it, feet 1 cm,
    hands 7 cm, 13 m in 3 s, off.
  - Colliders: every car / bus / auto box 97 % long, 87-88 % wide, 79-87 % tall, dynamic (ColliderReport; first run read
    0-16 % because exhaust particles keep a world-space box at the origin - meshes only now). The two-wheelers are
    kinematic and went through people: BikeRide.PushPeople. PushTest x3: out 0.71-0.96 m (clear 0.65).
  - Jams: NpcWalker.ScanTraffic ignored a waiting traffic vehicle (< 1.5 m/s) and every bike (kinematic, speed 0) - fixed.
    JamReport x9: 0 standing for a person. Found two wedged spots (police chowki into the road; road 30 kerb) - open.

## 22 Nov 2026 (01:30-05:00, main PC) - the rest of the free-roam batch (#3-#10), not committed yet

- Owner, in order: "kro jo bcha hia sab"; English by default with Hindi (Devanagari, Hindi font) everywhere; a 'tagda'
  glassmorphism phone with a Nainital wallpaper; his pins for the fire station and the two pumps; "multi agent bnaake kaam
  divide krlo" -> 7 agents ran out of usage -> "saare agents roko", then "one by one" (agents burn ~50 % of the usage at
  once); "pehle sab build waale ready krlo or ikkhate build chlaao"; "ruko mt" (do other work while a build runs).
- Built: phone + calls + camera (FREEROAM s.3), language (Core/Loc.cs, Tools/loc), pumps + fuel, 13 shops, 5 sit-outs
  with diners and a waiter, the house on Zoo Road, the fire station (101 tender drives out of it), stats + hunger blur +
  the open gym, radio (own folders, listed in Documents\NainiTA\music_folders.txt), umbrellas.
  Animals: code only (no models yet). Details: docs/FREEROAM.md "The rest of the batch".
- World build traps: the Tallital pump found no free spot (houses on every forecourt) and two sit-outs landed in back yards
  / a field -> a "Pump and sit-out plans" step before the Buildings reserves their ground (keepClear, circle to the
  corners); sit-outs open on the road's edge (Upper Mall Road allowed: it is not a "motor road" for NearestMotorRoad).
  Sukhatal pin is 50 m from the nearest road in the map data -> the pump stands 39 m from it on the Kaladhungi road.
- Tools: Tools/pipeline/compile_check.py (Unity's Roslyn on the files, 7 s with the editor idle; minutes while it
  builds - run it in the background), Editor/FreeroamTest.cs (Fuel, ShopOpen/ShopBuy, Sit, Bed/Sleep/Almari, Gym,
  RadioPick, Rain, Report).

## 22 Nov 2026 (05:15-05:40, main PC) - test run 2 of the free-roam batch

- Owner: "baaki waala saara kaam shuru krdo", then "abhi tripo waala mt kro...baaki kro", "ek baar phirse chlaake
  dekhlo na" (run it again fresh, not from the old log), "sab kuch roko... 4-5 ghante baad" + push.
- Verified in Play: restaurant order -> waiter from the kitchen (~8 s) -> plate -> hunger 0.20 -> 0.54, 4 diners with
  plates; house bed / sleep to 07:00 + save toast / wardrobe; gym numbers (muscle 16 -> 21.3 over pull / pedal / twist).
- Fixed: waiter look (was the pandit) -> young men in shirts (not seen yet); bed usable with the almari open -> blocked;
  FrontEnd.LateUpdate NullReference around reloads; test `Put` stands a seated player up (`PlayerController.StandUp()`).
- Open: the gym camera (collides at the Lower Mall frames: inside the jacket / low in the trees; plan in
  docs/FREEROAM.md HANDOFF) - the three HandIK poses not seen yet; umbrellas in rain; phone Radio / Stats apps EN + HI.
- Log trap: Editor.log was 5.6 M lines (old NullReference spam: LakeArea.RandomPointInside 290x, VehicleFX.Update:57
  92x - none in the current sessions); read only the lines after the current Play start.

## 22 Nov 2026 (13:00-13:20, main PC) - test run 3: gym camera, waiter, umbrellas, phone apps

- Owner: "chlo aage ka kro...rukna mt" (the HANDOFF list: gym camera, then umbrellas and the phone apps).
- Gym camera: `GymStation.Workout` now sets a ThirdPersonCamera override view (`PickView`: 12 places round the player,
  3/4 front preferred, sized by the camera's own 0.25 m sphere cast + a ray) and puts the old yaw / pitch back after.
  First try used a 0.3 m sphere: at the pull-up frame it started inside the bank and missed it, the camera's 0.25 m cast
  hit it at 0.25 m -> still in the jacket (`GymCamProbe.Run` showed it). Now: pull 300 deg / 2.6 m, pedal 90 / 4 m,
  twist 120 / 4 m; all three poses seen (`Shots/sheet_g4_gym_noon.jpg`).
- Waiter: the new look (white shirt) brings the momos; no pandit (`sheet_r6a.jpg`).
- Umbrellas: 0 in full rain with 30 walkers. `UmbrellaWhy`: every walker had an even instance id (Unity 6), the "odd id"
  half was empty. Mixed hash instead: 8 / 16 eligible under umbrellas, 0 again ~8 s after the rain stops
  (`sheet_r8_umbrellas.jpg`).
- Phone Radio / Stats apps in English and Hindi: right (`sheet_r6b.jpg`). On a phone the radio note has no "R" key.
- Found, left for the owner: the open gym's frames stand below the final ground (`GymCamProbe.Ground`, terrain minus
  the frame's floor, centre then 8 points 1.2 m round, from south to north):
  pedal 772.7,-638.4: 0.0 flat | pull 776.7,-644.8: 0.0 flat | twist 768.3,-631.7: +0.4 flat | pull 765.0,-626.8: +0.9 flat |
  pedal 761.8,-621.8: +1.55 (+1.2..+1.6) | twist 756.9,-616.7: +1.45 (+0.3..+2.3) | pull 754.0,-611.0: +1.45 (+0.3..+2.4) |
  pedal 750.7,-605.9: +1.28 (+0.1..+2.3) | twist 746.7,-598.4: +0.74 (-0.5..+1.9).
  Cause: built in the Mall Road step at `ctx.H` of that moment; a later ground step raises the ground there (which one: not traced yet - Lake embankment or "Ground under the roads" are the candidates).

## 22 Nov 2026 (13:30-15:00, main PC) - City Joggers Park: the ground first, then the open gym

- Owner: "actually na to park ka koi gate hai, na vo flat hai...up down terrain hai..pehle terrain theek kro wahan
  ki...then gym equipements do" (no gate, not flat: rolling ground; fix the ground, then the equipment).
- Found (`GymCamProbe.Profile` / `Across` / `Wall`, Esri zoom 18 with 5 m ticks across the Street View track):
  - the lake outline had ONE vertex along the park (747.1, -610.7), ~95 m from the next ones, so the shore cut 4-9 m
    inside the real edge: the land past Lower Mall was 2-8 m and fell 5 m to the water like a cliff. Satellite: trees
    and brown ground 20-28 m out from the track.
  - the open gym's frames were built in the Mall Road step on the ground of that moment; the last "ground under the
    roads" pass then raised the 3 m behind the parapet 25-60 % of the way back to the road (its two "bank rings"), so 6
    of 9 frames stood 0.7-1.5 m in the ground and the 3 southern ones on Lower Mall itself. The same rings make the dark
    mounds against the parapet and half-bury the trees by the railing along the whole Lower Mall bank.
- Done:
  - `WorldBuilder.DataFixes.cs` `FixJoggersParkShore`: that vertex -> 8 points read off the satellite (`JoggersPark.Shore`).
  - `Editor/JoggersPark.cs`: the park in the track's frame (s along, d out), its blend, two openings in the parapet
    (s 9 and 47, no gate), the equipment spots.
  - `LowerMallBank`: the park is always shaped (weight 1 there) with its own profile - a 1.8 m level strip of earth a hand
    under the road along the parapet (else the 1.46 m heightmap makes a saw-tooth of road-high lumps out from the wall),
    then rolling ground (two Perlin octaves, +-0.4-0.9 m) falling to the ledge at the stone wall; the ground comes up to
    the road at the openings; 9 level spots (flattest place across the bank, rows near the road / near the water by
    turns, levelled over 1.5 m, blended to 3.3 m); earth and leaf litter more than grass, bare round the equipment;
    trees kept 3.2 m off the spots and off the ways in.
  - `RoadGround`: samples LowerMallBank shaped behind the parapet (`bankKeep`) are left out of the bank rings - for the
    whole Lower Mall bank, not just the park.
  - `MallLakeEdge`: no parapet / railing over the two openings, a plastered end face at each side.
  - `LandmarkBuilder.BuildJoggersPark`: the equipment, in a new step after "Roads into the terrain" (on the ground as it
    finally is): posts from 0.3 m below each one's own ground on a concrete footing, the bar 2.05 m over the spot, the
    board just inside the south way in. Removed from `BuildLowerMallSights`.
- Checked after the second build (the level strip): `GymCamProbe.Ground` every frame +0.00 m (level within 0.1 m over
  1.2 m round); `Wall`: the ground 0.3 m under the road at the strip's end, then falling evenly (no lumps against the
  parapet); Play (`walktest`): from Lower Mall through the south / north openings down 3.6 / 3.9 m into the park and
  back up onto the road; the three workouts, gym camera 30-60 deg from the facing at 4 m (score 1.0). Shots:
  `Shots/sheet_jp2_a.jpg`, `sheet_jp2_b.jpg`, `sheet_jp_play.jpg`. Build World resets bundleVersion (ProjectSettings) -
  not to be committed.
- Left: the small wedge at the parapet's foot outside the park (the bank there starts 1.8 m under the road); a pine's
  branches in front of the gym camera at the twister.

## 22 Nov 2026 (18:20-, main PC) - AI traffic and the Upper Mall deck (not committed)

- Owner: "AI vehcile ko perfect kro...also upper mall road pe ab bhi unnchi niche mesh hai zoo road side..tum ek gaadi ko
  bus stand se DSA tk bhejo...mall road pe rukjayegi...AI glt mudne pe jab back krti hai to same direction me phirse chli
  jaati hai...gaadiyan aar paar jaari hain kabhi kabhi..sab mst check kro".
- Tools: `Editor/TrafficLab.cs` - edit or Play: `LinkAudit`, `LaneAudit`, `DeckAll`, `DeckProfile`; Play: `RouteDrive` (bus
  stand -> DSA), `Overlaps` (aar-paar: vehicles, walls, people), `TurnTest` (every turn of the town + wrong-way recovery).
  Batch lists: `Bridge/batch_traffic_world.txt`, `Bridge/batch_traffic_check.txt`.
- Causes measured:
  - Upper Mall from 700 m to Tallital: the slab took the raw ground every 2 m (470 bumps > 6 cm, worst 0.67 m; 11 lane
    points where the slab stood into a car's box) and the hill's triangles came up through it (258 of 1488 m).
  - both Mall roads were one-way to Tallital (the sign assumed the map's point order): no way north on the Mall.
  - junctions by "any road within 12 m": a link through the Mall divider wall, corner cuts; two connectors under 12 m
    were not baked (Kunjakharak Road's).
  - the Tallital police island stands on the junction's node (the owner's position, 14 Nov): every lane led into it.
  - 123 of 34301 lane points shut by static things; hairpins steered straight; a failed turn drove on the way it faced.
  - aar-paar: kinematic traffic bikes went into cars; the 5th-try ram passed through static things in full view; spawn
    only checked the traffic's own list.
- After the first build: bumps 1 (6 cm, the north end), ground through the slab 2 m, lane points shut 13, the island's
  ring clear 24/24. Two regressions seen in the shots and fixed in code, rebuild pending: the painted lines / zebra lay
  inside the levelled slab; the hill still stood through the footway's cobbles.
- 20:22: the PC froze mid crash test (the HDD with this repo dropped out; repo and uncommitted work intact, the save
  snapshot restored identical). Resumed 20:40.
- Play, 21:00-21:40:
  - crash test (10 cars, 6 roads): 10 of 10 hit -> accelerator off within 0.03 s -> stop -> back 2.2-8.8 m -> out.
  - Tallital island, every turn twice: 20 of 20 out, 0 hops (2 back-offs: the Boat Stand's board in the ring).
  - bus stand -> DSA, the owner was right: 1.1 m/s up the whole Upper Mall behind someone walking in the lane (480 s, not
    there). People now clear 14 m in front of traffic and stay aside, cars honk and go round a walker, a person at the
    bumper is seen (a sphere cast misses what it starts inside). Then: 244 s and 237 s, 0 stops, 0 hits.
  - every turn (171) + 40 left pointing the wrong way: 165 turns made (26 with back-offs), 6 failed; 40 of 40 back on
    their own lane.
- Owner, 21:37: "heavy traffic lgaake.. bus stand se mallroad hote huye, eco cave park, then lovers points hote huye poora
  khurpatal tk pahuchaao... bus ko.. full traffic" -> `Editor/TrafficBusTest.cs` (45 cars + 20 two-wheelers round a bus):
  - no route existed: traffic could only turn at a road's end, Khurpatal's road leaves the Kaladhungi road mid-way
    (`RoadNetwork.Branches`, routes take it with the indicator).
  - run 1: jam at Mallital after 1636 m. Upper Mall's slab ended in a 0.3-0.4 m step across Lower Mall's only way in, and
    held-up cars turned round in the junction. Deck ends ramped (`DeckEndSides`), no U-turn in a junction or a queue,
    another road out instead, one of two facing cars gives way, coach-wide lanes where there is room.

### PENDING - next session starts here (22 Nov 2026, 22:15; owner: "pending me daaldo ... next session me krenge")

1. **Physics: a bike shoves a bus backwards** (owner: "ek bike waala meri bus ko hi dhakka lgaate huye peeche leaaya ...
   wheres physics?"). `BikeRide` is a kinematic body moved by MovePosition, so it pushes a 7.5 t bus as if it had endless
   mass. That predates 22 Nov; the bike back-out added that night (`BikeRide.BackOut`) may add to it - not checked.
2. **The owner's bus test has NOT passed** (full traffic, bus stand -> Mall Road -> Eco Cave Park -> Lovers Point ->
   Khurpatal; `call TrafficBusTest.Run` in Play). Run 1: jam at Mallital after 1636 m. Run 2, after the junction fixes:
   stuck at the Tallital island after 98 m among bikes, the traffic post and a house; stopped by the owner. The turn onto
   Khurpatal's road has never been driven.
3. **Last edits not verified in Play:** the Mallital junction fixes (deck ends, no U-turn in a junction, another road out,
   give way), coach-wide lanes, people pushed out of a car's width, the board out of the island. The crash test (10/10),
   island test (20/20) and bus stand -> DSA runs (244 s, 237 s) were on earlier code of the same night: run them again.
4. Aar-paar still open: bike <-> bus up to 1.28 m, people in full traffic, vehicles into houses / G.O.A.N. plinths.
5. 6 of 171 turns failed (Kunjakharak, #17 -> #27, #83 -> #23, #52 -> Mall Road x2); 13 lane points have no room.
6. After pulling: **Build World** (the Upper Mall deck, junctions, lanes and boards are built, not in git).

## 22 Nov 2026 (22:30-23:20, main PC) - real mass, real bike drive, getting in / out, jumping out (not committed)

- Owner: "bike waala meri bus ko peeche dhakka ... wheres physics?" then "vehicle objects and characters real mass ki tarah
  behave ... based on real calculation, badi gaadi choti ko dhakka maarske", "bike scooty itti bekaar kyun chlri..auto braking
  kyun hori...khade khade mudri hai...actual drive do na usko", "entry exit ka animation bhi nhi chlra", "4 baar tk girke
  vaapas same vehicle pe baithke chle jaaye...bahut tej thoka to gira rehjaaye", "speed me kisi bhi vehicle se exit krte
  hain to ragdoll bnke girega..health jayegi based on speed".
- `BikeRide` is a real dynamic body now (was kinematic = endless mass in PhysX): bike 135 kg / scooty 105 + 70 kg a person
  aboard (Weigh), box 0.3 m off the ground, one `Machine` for everyone: engine pull falling to 0 at top speed, rolling
  resistance + air (1/2 rho CdA v^2) + engine braking off the throttle, brakes, the slope's g sin, tyres inside mu = 0.9
  (forwards + sideways together), turns only as it rolls (omega = v tan(steer)/wheelbase, <= 0.85 mu g / v), lean
  tan = v omega / g. Player: `Ride(throttle, brake, steer, top, paddleBack)`, no assists (PlayerBike's stop-short cast
  removed). Traffic / police: `Step` turns aim + target into the same controls; its eyes sweep the bike's box for the
  stopping distance (0.75 s reaction + v^2/2a), steer for a clear heading and brake in time.
- Knock-over from the contact impulse PhysX solved: dv > 2 m/s sideways or > 3.5 m/s front/back, or shoved sideways
  > 1.2 m/s for 0.25 s; then a free body (never kinematic again: it sleeps).
- Measured (`VehicleTest.BikeBus`): bus 7500 kg at 2.27 m/s into a bike -> 2.18 (momentum 2.23); at 6.55 -> 6.31 (6.43);
  hatch 950 kg 7.16 -> 5.99 (6.27), bike thrown 11.8 m. Player bike: A/D standing still 0 m / 0 deg, full throttle
  12.4 m in 3 s (~2.4 m/s2).
- Traffic riders who fall get up, stand the bike up and ride on, up to 4 falls (`TrafficBike.GetBackOn`); thrown above
  8.5 m/s, badly hurt (< 25% health) or the 5th fall: they stay down. Bikes > 120 m from the camera are still despawned.
- Getting in / out is seen (`PlayerController.BoardVehicle / BoardBike / AlightVehicle / AlightBike`): walk to the
  setup's entry, "Entering Car" (cars, vans, buses) / sitting down (auto, e-rickshaw) / Mount, slide onto the seat; out the
  same way. Owner: "theek chlra entry exit".
- Jumping out above 2.5 m/s (`PlayerController.BailOut`, new `Characters/Ragdoll.cs`): a ragdoll on the humanoid bones
  (70 kg in real shares, RagdollBuilder joint limits, built from the standing pose, on the person's own layer), damage
  0.45 v^2 - 4 (30 km/h -25, 40 -50, 50 -85, ~55+ dead), gets up with the getting-up clip; the empty car rolls on
  (no auto handbrake: `coastUntil`), a riderless bike falls. Car bail at 32 km/h checked in a film (-32 health).
- Not done: the bike bail not filmed yet; the 22 Nov PENDING list (bus test to Khurpatal, crash / island / every-turn
  re-runs) was not run on this code; traffic bikes still hit each other at junction crossings now and then.
- New tests: `VehicleTest.BikeBus` (into / bus2 / bus7 / car7), `LogBikeHits`, `CarE` (arg model; again with a speed =
  jump out), `Throttle`, `Film`, `KnockBike` arg = speed.

## 22 Nov 2026 (23:30-, main PC) - riding a bus as a passenger, the bus simulation with the player aboard

- Owner: "bus me as passenger bethne ka bhi rkho....or ye hojaaye to ek bus stand se khurpatal tk ka simulation chlaao".
- New `Vehicles/BusPassenger.cs` (on every traffic bus from TrafficManager, and on TrafficBusTest's bus): E at the passenger
  door (the setup's seat-1 way in, else the left side) - a moving traffic bus pulls up first (`TrafficCar.StopForBoarding`)
  - walk up, climb in (StandToSit), sit on a free passenger seat (`PlayerController.BoardPassenger / SitPassenger`,
  Mode.Mount like the ropeway, camera on the bus). E again: off at the door when it stands (`AlightPassenger`), a ragdoll
  jump when it moves (`BailOut`). Died / arrested: off at once. A bus with the player in it is never despawned
  (TrafficManager) or destroyed (TrafficCar.Leave, TrafficBusTest.Stop evict him first).
- Checked in a film (`VehicleTest.BusRide`): walk to the door, sit, "Bus se utro", off beside the bus.
- `TrafficBusTest.Run` now seats the player in its bus as a passenger (no jetpack); results below.
- Run 3 (23:35, full traffic, player aboard as a passenger the whole way): bus stand -> Mall Road in 108 s (768 m, 0
  stops), then stuck at the north end of Upper Mall Road / the Mallital junction from 236 s: 17 stops, 7 contacts (4 of
  them "gave way"), 1 pass; FAIL, no progress for 120 s at (87.8, 114.0) after 649 s, 1698 m. In front of it the whole
  time: another traffic bus 3.6 m ahead that never got through the junction (shots play_bus_06..39). Same place as run 1
  (1636 m). Not reached: Eco Cave Park, Lovers Point, Khurpatal. Next: why a bus cannot clear the Mallital junction
  (the turn from Upper Mall's end onto the Mall towards the Flats / Kaladhungi road) - its turn path, the booth / gate,
  oncoming give-way.
- 23 Nov (00:10) owner: "agar NPC driver ko aisa lgra hai ki aage waala uske comparison me hlke hain or saamne se koi
  vehicle nhi aara to overtake krle...and also bus me ek camera option jisse fpv me rotate krskte".
  - Overtaking (TrafficCar `CanOvertake / OtherLaneClear`): 2 s held under 80% (and 2 m/s under) the speed it wants,
    room beside it on the road, no junction island within 45 m, the lane within 10 deg straight for its own way past,
    the other lane clear (vehicles, the player) for that + what a car at 12 m/s covers meanwhile (capped 170 m), room to
    pull in ahead of it; out on the right (StartPass), on the gas, back in once past; something in the other lane close
    ahead before it is alongside: drops back (`Aborts`). Bikes: the same idea (TrafficBike `RightClear`, 3 s of closing).
    Counts in `VehicleTest.TrafficStats` (overtakes, dropped back).
  - First-person view while seated (bus passenger, at the wheel, on a bike): V / the new "Camera" touch button
    (`GameInput.CameraPressed`, `ThirdPersonCamera.FirstPerson`): the eyes just in front of the head bone, look round
    +-165 deg / -70..60 deg with the mouse or a drag, turning with the vehicle; near clip 0.04 m. Checked: forward,
    left, right, back through the bus windows (`VehicleTest.Cam`, Shots/play_fpv_*).
  - HUD: a passenger (Mount) shows only Use / Camera now (no fight buttons); on a bike the jump button reads Brake.
- 23 Nov (00:15-00:35) owner: "traffic light lgaani pdengi jahan atleast 3 bhi major roads hain...vrna jam bahut tgda
  lgra"; "naina devi ka jo mall road gate hai..., bus stand wala chouraha, or bhi agar kuch major ho to...ek perfect se
  lights lgaana".
  - New `Vehicles/TrafficSignals.cs`, built by TrafficManager when the game starts (no Build World): junctions from
    `RoadNetwork.meetings` grouped within 32 m, arms = roads leaving the meeting point (one per 30 deg), kept with 3+
    main-road arms (>= 6 m wide, a road counted twice at most - a hairpin of road #7 read as four arms). 10 junctions:
    Mallital gate (88,114: Mall Road, Upper Mall, Colvin Road), the Tallital / bus stand chauraha (822,-1140, its three
    meetings one junction, stop lines 29 m out round the island), (-149,21), (-530,534), (-569,560), (-1241,-155),
    (0,865), (737,352), (-1147,878), (-638,1393). Each incoming arm: a stop line (white strip on the ground) and a post
    on the drivers' left (yellow-black band, yellow board, black housing, red / amber / green lamps with visors, the lit
    one emissive). One arm green at a time, vehicle-actuated (an empty arm is skipped; green 6-16 s, ends early once its
    arm is empty and somebody waits elsewhere), amber 3 s, all red 2.5 s. TrafficCar / TrafficBike stop their nose at
    the line (`HoldAt`, eased at 3 m/s2; amber: go on if it cannot stop comfortably); no overtaking within 60 / 50 m of a
    signalled junction. The player is not held / fined (not asked yet).
  - Checked from the drivers' side (`VehicleTest.CamPost`, Shots/play_p_*): Mallital (the Naina Devi gate behind),
    Tallital round the police island. Run 5 of the bus test (with the lights) started 00:32.
- 23 Nov (00:40-01:10): the owner's ride, the menu's bus ride, the High grade.
  - Run 6 (lights on, ride seating): through the Mallital junction in normal flow (276 s, 1793 m on road #28 past
    (88,114)) where runs 1, 3 and 4 had stood for minutes - stopped there by the owner's Play stop, not a fail.
  - The ride bus (`TrafficCar.FillForRide`): his seat first out of an empty bus (BusPassenger.SeatNow, row 1 = the
    second-last row, the left window seat), then grown women on every other seat but the last row (`PickLook grownOnly`:
    no Girl / OldWoman), a man at the wheel, a man standing just inside the door as the conductor
    (`PedestrianManager.MakeStanding`, `NpcWalker.EnterStanding`). `VehicleParts.RowFromBack` (0.35 m z gaps), `IsFree`.
  - Menu: HUD tray "Bus Sawari" -> `Vehicles/BusTour.cs`: the bus test's route (bus stand -> Mall Road -> Eco Cave Park
    -> Lovers Point -> Khurpatal) planned in the game (`World/RoadPlanner.cs`, TrafficLab.Plan now calls it), stops
    called out, "Khurpatal aa gaya! E se utro" at the end, the bus goes away once he is off and 120 m from it. Checked:
    7 roads planned, 12 women aboard, the last row empty (looking back in first person), the bus held at the Tallital red.
  - High graphics (GameSettings.HighGrade): a global volume at priority 0.5 only on High - colour adjustments (exposure
    +0.42, contrast 18, saturation 13), lift / gamma / gain, split toning, white balance +5, shadows / midtones /
    highlights (warm light, neutral shadows), high-quality bloom, vignette 0.22, motion blur medium. AO: PC_Renderer's
    SSAO (on at High, PC) raised to intensity 0.85, radius 0.45, high samples. First try read as dusk at 11 am (three
    blue shadow tints on top of the SSAO) - retuned; compare Shots/play_q_med*/high*. Phones: the grade yes, SSAO no.
- 23 Nov (01:10-01:25) owner: "jab bus chlri ho to shake wgera aaye FPV camera ko hlke ya tej based on road and speed";
  "mujhe desktop view do editor me ...mobile view nhi".
  - First-person ride shake (`ThirdPersonCamera.Ride`): a hum growing with speed (x2.4 off the network's roads, bike
    x1.6, a heavy vehicle x0.75) + jolts from the body's measured vertical acceleration and pitch / roll rate; the view
    leans 70% with the vehicle. Measured on the bus tour (`VehicleTest.ShakeLog`, on the road, 6.8 m/s mean, 9.3 most):
    first try 0.38 cm / 0.19 deg mean and no jolt at all in 25 s (thresholds too high for a bus on a road) -> retuned:
    0.71 cm / 0.35 deg mean, 1.1 cm / 0.64 deg most, small jolts registering.
  - `PlayTest.Begin` is the desktop game now (no touch HUD, a 1920 x 1080 Game view); `PlayTest.BeginPhone` the phone's.
- 23 Nov (01:25-01:40) owner: "andr ka wide angle...sab hlke hlke hilte rhe log..brake pe gardan aage...mod pe react";
  "paas me blur hora hai ...like lens"; "speed ka cheat..+ or - dono"; "bus ke radio pe...bus.mp3...doppler effect na dena".
  - First person: 92 deg field of view (wide angle).
  - `Characters/RideSway.cs` (added by VehicleParts.Sit, off at Leave; order 60: after the rig, before the camera and
    the hand IK): the vehicle's smoothed acceleration felt as inertia through a spring (braking -> forward, pulling away
    -> back, a bend -> out of it, a bump -> the head drops), the same for everyone aboard, plus each one's slow own sway
    (waist, neck, looking about); never piles up on frames the animator skips. Bus tour, 45 s (`VehicleTest.SwayLog`):
    14 people, most 15 deg forward (braking 11 m/s2 at the lights), 10 back, 7 to the side. Owner: checked, fine.
  - The near blur was URP camera motion blur (it takes the world as standing still: the moving bus's inside smeared):
    `ThirdPersonCamera.ClearRide` - a priority-10 volume, motion blur 0, while he rides / rows.
  - Cheats TEZ / DHEEMA (CheatCodes, the cheat panel): the game's pace 0.25 / 0.5 / 1 / 1.5 / 2 / 3 / 4 x
    (Time.timeScale; the pause menu restores it, a new game resets it). Checked 1 -> 3 -> 0.5.
  - `Audio/BusRadio.cs`: the bus he rides plays the player's OWN file named bus.mp3 (PC Documents\NainiTA\bus.mp3,
    phones the game's persistentDataPath) - never in the game or the repo (the owner has no rights to his song); looping,
    from the front, 3D fade outside, Doppler 0; a hint on screen where to put it when missing. Tested with the game's own
    synth track converted to mp3 (101 s, loaded and played), the test file deleted afterwards.
  - 01:45 owner: "3d sound chahiye jo driver ke pass ho": BusRadio is fully 3D now (spatial blend 1, spread 30,
    logarithmic from 2.5 m, Doppler 0, volume 0.9) on the dashboard in front of the driver's seat (setup seat 0 + 1.15 m
    up, 0.65 m ahead): on the tour bus at (0.75, 2.17, 3.44) - the right-hand (driver's) side, near the front.
- 23 Nov (01:40-02:10) weather + the ride. Owner: "baarish ptli kro..bahut moti hai"; "chlte game pe baarish or heavy fog";
  "baarish me bhi dhoop hai hlki..balki mausam grey hojaata hai..fog sirf pahaad tk hai..skybox react nhi krrha";
  "isko bus journey me connect krdiya?"; "bus ab start hogi BD pandey se"; "random jagah se chlega".
  - Rain drops 4.5 -> 1.5 cm, fainter, longer streaks, more of them (WeatherSystem).
  - New weather RainFog ("Baarish + Kohra": the Mausam panel, cheats TOOFAN / SAAF): rain with the fog ~120 m (Fog: 280).
    The weather-name / temperature lists (HUD, FrontEnd, Phone) had 4 entries - IndexOutOfRange spam - now 5.
  - `WeatherSystem.Overcast` (cloudy 0.55, fog 0.8, rain 1): the sun 8% in rain, shadow strength -> 0.08, grey sun colour,
    a grey trilight ambient, grey fog colour (DayNightCycle); Sky.shader `_Overcast` greys the whole sky and hides the
    sun disc, `_SkyFog` (fog x 0.9) puts the whole sky in the fog colour, AtmosphereFog.shader fogs the sky overhead too.
    Checked at 11 am: clear (sun, shadows) / RainFog (flat grey, no shadows, the far shore gone) / Rain.
  - BusRadio: starts at a random point of the song (its length read from the file); retries the file every 2 s.
  - Pause menu: "BUS SAWARI / BUS RIDE" starts the ride. The ride (BusTour): from BD Pandey District Hospital (world.json
    POI, -150, 230) -> Eco Cave Park -> Lovers Point -> Khurpatal (Mall Road dropped: a 3 km detour from Mallital), in
    RainFog (weather back to its own cycle when the ride ends). Checked: 3 roads planned, the bus climbing out of Mallital
    in the rain and fog.
- 23 Nov (10:45) radio. Owner: "koi bhi vehicle me random song, and bus me bus.mp3 hi hmesha from random place". Code
  done + offline compile OK; NOT tested in Unity yet (owner: "unity me abhi mt test kro").
  - `Audio/Radio.cs`: every time he gets into a car / auto / taxi or onto a bike: his own songs ("Apne Gaane"), a random
    one, from a random place in it - not the last ride's station (his editor had "Radio band" saved, nt_radio = 4, so
    nothing played). R still changes the station for that ride. No own songs: the first bundled station. Own songs now
    load whole and compressed (a streamed clip cannot start in the middle). Log line `[Nainital] radio: <song> ... from N s`.
  - `Audio/BusRadio.cs`: back to bus.mp3 only, looping, from a random place each time a bus's radio comes on; also when
    he DRIVES a bus (Radio hands a bus to BusRadio). The 10-songs-from-his-folder bus playlist was dropped. Log line
    `[Nainital] bus radio: bus.mp3 (N s) from N s`.
  - TO TEST (next session): (1) drive a car (`VehicleTest.CarE`), get out, get in again x3 -> a different song and start
    each time; (2) a bike the same; (3) `VehicleTest.Tour` / pause menu BUS SAWARI -> bus.mp3 from the middle, 3D at the
    driver, no car radio on top; (4) drive a bus -> bus.mp3, not his songs; (5) R in a car still cycles to Radio band.
- 23 Nov (evening) story system, game parlour, lights, temple court, ropeway, Snow View. Owner: "cutscene, dialogue and choice
  UI system bnaalo proper..or mall road, choki se niana devi mandir tk lighting add kro path pe...nainta devi mandir ke saamne
  mandir poore me hain...bandstand ke andr light..capitol ke neeche video games ki dukaan me pool wgera, bowling and basic
  playable games...upar 360 tower or uske paas ka market...ropeways interactable bnaao... proper both sides entry.. or side
  concrete barricade...lighting mujhe bhdiya chahiye...specially mall raod to khatarnaak".
  - References (reference only, never in the game or the repo): `D:\Unity\ref\yt_nov23\` - naina (Naina Devi at night),
    snowmkt + snow360 (Snow View, Tower 360, adventure park, market), rope (both stations), mallnight (Mall Road, the walk,
    Capitol at night), zoom (Zoom Land / Video Games on the lakeside). 4x4 contact sheets with times in each folder.
  - Story system (`Runtime/Story/`): `Cutscene` runs a JSON script from `Resources/Story/<id>.json` (actors from the
    character library, shots / glides / orbits / follows, lines, choices with flags and jumps, if / goto / labels, walk /
    face / place, the QTE `mash`, fade / bars / blur / objective); `StoryUI` (cinema bars, typed subtitles with names, choices
    by 1-4 / arrows / tap / pad with an optional timer, hold-Esc skip that stops at choices, the QTE meter, a DOF + vignette
    blur volume); `StoryFlags` (one PlayerPrefs key `nt_story`, in TestSave). `GameInput.StoryLock` mutes gameplay input.
    Test scene `demo_uncle` (relative to the player): cheat KAHANI, menu Nainital > Story, bridge `call StoryTest.Play`.
    Hindi lines: `Tools/loc/strings_story.tsv` (build_loc.py). Offline compile OK; not yet played in the editor.
  - Game parlour (`Runtime/Games/`): `ArcadeScreen` (Bandar Bhagao whack-a-monkey, Pahadi Dash lane dodger, practice Pool
    with real ball physics) opened by `ArcadeCabinet` (E, Rs 20/30); `BowlingLane` builds its own lane / pins / ball with
    rigid bodies (6 kg ball, 1.5 kg pins), 5 frames, strike +10, spare +5 (Rs 50). The Video Games / Zoom Land hall
    (`WorldBuilder.Capitol.cs`) is hollow now: door in the middle bay, floor, ceiling, neon edges, tube lights (accent
    lights), 6 machines, 2 pool tables, the bowling lane on the lake side; the Capitol front washed violet / pink.
  - Lights (`WorldBuilder.Festive.cs`, a build step on the final ground): LED dots drawn by the LampGlow shader (faded in at
    dusk): warm strip + post bulbs along the Lower Mall's lake railing (`mallRailRuns`, from MallLakeEdge), fairy lights on
    the Mall's own trees (every fourth blue / violet), jhalar over the Mall's shop fronts, the walk Mall -> Naina Devi with
    white lantern posts every 12 m (real lamps), a violet strip and warm foot-lights on its lake side; accent lights
    (LampManager: new accent category, colour / range / intensity each) for the bandstand's lamp, the temples, the parlour,
    the ropeway stations. The bandstand has a lamp under its roof and gold LED on its eave, ridges and posts.
  - Naina Devi (`LandmarkBuilder.Worship.cs` NainaDeviCourt): the court runs 12 m on toward the market: the bell pavilion
    (lavender pillars, three rows of brass bells), the hall of the ten avatars (tile wall), two white marble shrines with gold
    doors, chunri on the peepal, a diya stand, potted plants, strings of bulbs across, gold / white LED on every roof edge.
  - Ropeway (`LandmarkBuilder.RopewayStation.cs`, `Runtime/World/Ropeway.cs`, `RopewayRide.cs`): the same station at both
    ends - ENTRY door, ticket hall with counter and rate board, queue rails, 26 stairs to the platform, two bays, red steel
    frame, the curved guides outside, jersey barriers along the door's lane where the ground is free; two cabins on two
    tracks passing each other (jig-back); E at your cabin's door on the platform, off onto the other platform.
  - Snow View: Tower 360 yellow with red / white top bands, a maroon ring with "360"; the adventure park and market lane
    (`LandmarkBuilder.SnowViewPark.cs`) - in progress.
  - Snow View also: a food court on the open grass east of the Gol Market (chai / Maggi carts, umbrellas, stools, benches,
    coin binoculars, strings of bulbs, steps down the slope) and a zip line (yellow tower, cable, seat).
  - Barriers: the check box ignores the terrain now (slopes cut it before) and spots along the side walls were added:
    Mallital 16, Snow View 24. A "rain roof" (a box on the vehicle layer, which the rain collides with) over the parlour and
    both stations. Parlour lights down the middle, range <= half the hall (no shadows: they lit the walls outside).
  - PLAY TEST (20:00-20:30, desktop view, bridge): demo cutscene ran end to end (line, choice 2 -> its branch, walk, orbit,
    QTE passed by presses, flags demo_reply=hide demo_qte=1, controls back); Bandar / Dash / Pool open and play; bowling 5
    frames with real pins (strikes), the score bar shows; ropeway: cabin 0 docked at Snow View, boarded at its platform door
    (Rs 300), rode down, got off on the Mallital platform beside its cabin; night in Play with real lights: Naina Devi court,
    bandstand lamp, the walk's lanterns, Mall railing LED + fairy trees in the rain, Capitol violet, Snow View lane lanterns;
    TOOFAN: no rain inside the parlour. Fixed on the way: pool aimed at the wall, pool label, bowling bar empty + player in
    the camera's way, a game opening over bowling. Test hooks: StoryTest.Play/Next/Pick/Mash/Skip/State, Arcade, Bowl,
    Rope, RopeBoard, PutAt (Editor/StoryTest.cs).
  - OPEN (owner's review 21:05, "baaki abhi fix mt krna..jab main kahu to krna" - wait for his go): game parlour - (1) the
    walls inside glow by day (sky ambient reaches in): the inside must look the same day and night, outside light only at
    the door; (2) the ceiling lights look right but little light reaches the floor (6.4 m ceiling, range 6.2): hang them
    lower / more range; (3) add 5-10 more arcade machines and real game-zone vibes (carpet, neon boards, decor) - it is bare;
    (4) the bowling lane flickers: its top (y 0.12) z-fights the hall floor top (0.12) - lift the lane.
  - OPEN (owner 21:15, with 3 Street View 360s; "abhi kaam mt krna"): the Naina Devi / Tibetan Market chowk is a hill
    mound in the game (white curved paint lines on a bump, the walk climbing over it); in reality the whole chowk is FLAT,
    grey and red pavers with round cobble medallions; of the two roads ahead the first is on this same level, the upper one
    higher, with stairs between them; the chowk is lined with shops all round (China Town, the Tibetan market's clothes
    stalls under blue tarps, Fartiyal's, the temple's red gate) - the game has open grass there. Level it, pave it as the
    photos, add the stairs, fill the edges with the shop rows.
  - OPEN (owner 21:20, the walk Mall -> Naina Devi at night): (1) walkers press into the red wall on one side and through
    the green railing on the other and get nowhere ("isme brain nhi hai?") - check their route line against the walk's
    real width and whether the new lantern posts / anything new blocks them; (2) the violet LED strip and its uplights lie
    on the grass outside the green railing: Festive.cs put them 3.2 m from the walk's centre - follow the real railing line.
  - OPEN (owner 21:30, a marked Google Maps image; wait for his go): DSA road is on two levels. Green = the main road
    (upper) Mall Road -> Naina Devi; yellow = the lower path along the lake, parallel to it, from the Boat House past the
    bandstand to Children's Park; the two joined by stairs (two connectors near the bandstand / Boat Point park); orange =
    the two parks (the small one by the bandstand between the levels, Children's Park); pink = the Boat House club's ground,
    also low; black = trees lining the upper road thickly on both sides, so from the upper road the lake shows only at the
    bandstand opening. In the data (Tools/satcheck/capcrop.py 45 55 85): upper road (87,114) -> (-15,-30); lower path
    (107,97) -> (58,51) -> (51,37) -> (28,-28); connectors around (40-55, 20-50); small park (32-57, 34-55); Children's
    Park (0-35, -27..25); Boat House lawn (62-104, 55-95).
  - OPEN (owner 21:35, Mallital chowki junction at night): (1) traffic signal posts stand inside the carriageway and buses
    hit them instead of passing (TrafficSignals.cs: push each post out past the kerb / onto the footway, never in a lane);
    (2) the walk's violet LED strip runs across the road at the chowki (Festive.cs: skip any piece where Clearance says
    road).
  - OPEN (owner 21:40, Upper Mall -> Lower Mall stairs): every flight between the two decks has no side walls, so the
    terrain inside shows beside the steps. Close both sides of each flight with walls (the divider's mossy stone) from the
    upper road down to the lower one (WorldBuilder.Mall.cs MallDivider stairs).
  - OPEN (owner 21:45, night, rain): the Zoo Road mouth off Mall Road is shut by a long mossy stone wall (the Mall's
    divider / retaining wall runs straight past the junction); Zoo Road is visible behind it but cannot be entered. Open
    the wall wherever a road meets the Mall (check every junction along both decks, not only Zoo Road).
  - OPEN (owner 21:50): the Himgiri petrol pump stands in the wrong place (on Mall Road at the lake's edge). Find its real
    spot (Google Maps / satellite), move it there (LandmarkBuilder.PlanPetrolPumps / PetrolPumps.cs: position, facing,
    the way in and out), and dress the place it leaves as the photos show.
  - OPEN (owner 21:55, a hillside lane at night, minimap: a road running north-south through houses): a house stands on
    the road - the road runs into it. Run Editor/RoadAudit.cs (every built object with a road through it) and clear all of
    them, not only this one; the lane must stay drivable.
  - OPEN (owner 22:00, a road wall at night): the road walls show from one side only (seen from behind / above they vanish).
    Make all of them double-sided (the wall material's cull off, or back faces built) - WorldBuilder.RoadWalls.cs and the
    Mall's walls alike.
- 23 Nov (night, 22:00-23:30) the OPEN list above, all fixed (owner: "itna kro pehle bilkul perfect", then "ok"):
  - Signals: TrafficSignals.Dress never puts a post on ANY road of the network (OnAnyRoad: half width + 0.6), trying back
    and out (Play: Mallital chowki junction post on the footway corner).
  - Walk LEDs: the generic violet strip / foot lights (3.2 m from the walk's centre) removed; they come with the DSA wall.
  - Walls double-sided: M_MallMossyStone (every retaining wall) _Cull 0.
  - Mall stairs: both sides of each Upper -> Lower Mall flight closed with stone walls and a coping.
  - "Zoo road": the road the owner meant is Snowview Road (Zoo is up it), meeting Upper Mall at (785,-635): two generated
    houses stood across its mouth (now left off) and the tall-wall stretches never asked LaneAt - they do now (Play: open).
    (Zoo Road itself starts 78 m off the Mall, from the road above it - right in the data.)
  - Houses on roads: BuildBuildings skips a house whose middle or corners are on a carriageway (38 left off), unnamed OSM
    footprints too; Kaladhungi roadside items skip a road after stepping back; zoo fence opens where a road passes; the
    G.O.A.N. plinth shrinks off a road; Land's End's ring steps forward off its road. New audit RoadAudit.Colliders (every
    collider on a drivable road, per 10 m) - left: the Tallital traffic island (by design), the Army centre on a 3.5 m lane.
  - Petrol pump: the Tallital pin moved to IndianOil's locator (29.380094, 79.462100) -> (688.5,-1153.6), the lake road west
    of the chowk.
  - Game parlour: InteriorZone (Runtime/World): while the camera is inside, DayNightCycle dims the ambient (same by day and
    night); 12 real lamps 1.2 m under the ceiling + pool lamps + violet / pink glows, on day and night, switched by distance;
    dark blue walls on a black dado, black ceiling, neon carpet (procedural texture), neon boards; 31 machines / tables
    (wall rows, a back-to-back island, 2 racers, 2 claw machines, basketball, air hockey); the bowling lane on a 16 cm
    base (no flicker).
  - Walkers: ClearWalks (Living.cs) cuts every walkway at points a walker-sized capsule finds blocked (3555 cut, 320 -> 441
    pieces), so nobody walks into a wall, railing or fence.
  - DSA road two levels (WorldBuilder.DsaRoad.cs): the lake side of the upper road from (24,29) to the Mall at the lower
    level 748.2 (upper 750.3-754.4), the Boat House path a grade to the Mall, a 1.5 m stone retaining wall with coping and
    green railing (violet LED on top, warm foot lights) sloping with the road, a flat ledge on the high side, 13 stairs
    (39,51)->(53,42) between side walls, the small park's fence, 49 trees in rows both sides (lake side open by the
    bandstand); the wall's foot kept low after the roads (bankKeep).
  - Naina Devi chowk (WorldBuilder.Chowk.cs): flat at 749.32 over the satellite outline, grey pavers with red bands and
    medallions (PaintRoadSurfaceOver: red wins over grey), stone walls where the ground beyond differs, stairs where paths
    climb out (2), 6 shops round the free edges; the old painted "Tibetan market ring" removed.

## 23 Nov 2026 (23:30-, main PC) - the town feels alive (owner: "saare krdo inme...and bandar bhi tripo pe pending krdo")
- Wind (Shaders/FoliageWind.shader = URP Lit + Shaders/NtWind.hlsl; `_NtWind` from WeatherSystem, gusts): tree leaves and
  bark, grass, cloth (flags, prayer flags, tarps, awnings, shawls, coats: one "Cloth in the wind" mesh, FestiveStep).
- Moored boats bob on a swell (`_WindBob`, "Moored boats on the swell" mesh: Details + Mallital ghat rowing boats, swans).
- Sounds (AudioManager.Alive): vendor calls + boatmen (Resources/Audio/vendor_*, boatman_* - CLIPS STILL NEEDED: Windows
  has no Hindi TTS voice here, edge-tts install was not allowed), far horns, bus-stand air horn, lake lapping, rain on a
  tin roof overhead, aarti bells at Naina Devi (6:00-6:36, 18:24-19:12), azaan from the Jama Masjid at prayer times
  (needs Resources/Audio/azaan), footsteps on roads / pavers (TerrainRoads.SurfaceAt: the mask read back from the GPU).
  Reverb: the parlour (InteriorZone) and both ropeway halls (AudioReverbZone).
- Particles (Runtime/World/AmbientLife.cs): falling leaves, dust in the sun, morning / fog mist on the lake, breath fog
  (player + people near), steam at tea stalls and Mall eateries, chimney smoke (cottage) mornings / evenings.
- Town: shop shutters down 21:30-23:30 till 7:30-9:30 (each shop its own time), windows switch on / off at night
  (NainitalRooms.hlsl, `_NtHourP1`, `_NtGameMinutes`); ~3% of lamp halos flicker in bursts (LampGlow).
- NPCs: when they stop (12% per waypoint, 3-7 s): photo of the lake (phone up), phone call, eating a bhutta, wave, a bow
  towards Naina Devi, a look round.
- PENDING (Tripo): monkeys (bandar) - with the animals and the fire truck.
