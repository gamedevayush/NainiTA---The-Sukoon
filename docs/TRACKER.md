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
