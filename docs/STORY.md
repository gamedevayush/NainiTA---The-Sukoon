# Story missions - the owner's decisions and the build status

The story and every mission: `docs/Nainital_Story_Mission_Bible.docx` (made by `docs/make_story_bible.py`, v2, 23 Nov 2026).
This file holds what was decided after it and how far the missions are built.

## Decisions (owner, 26 Nov 2026)

- **Build the missions exactly as the bible's section B lists them** - every mission on its own, no merges or cuts from
  section E (owner picked "Bible jaisi hai waisi").
- **The 8 old demo missions go** (Nainital Darshan, Gunda Raj Khatam, Naav Race, Taxi Driver, Snow View Parcel, Bandar
  Bhagao, Photo Tour, Raat ki Gasht). Their parts that the story needs (markers, timer, HUD mission card) are reused.
- **Dialogue: Hindi text + AI voice**, every line voiced, each character with its own voice. Voice engine: Microsoft
  neural voices through `edge-tts` (hi-IN Madhur / Swara, pitch and rate per character), generated once to files that
  ship in the game. (The Windows Hindi voices Kalpana / Hemant are not installed since the 26 Nov reinstall.)
- **Names:**
  | Who | Name |
  |---|---|
  | The player (main guy) | **Nikhil** |
  | His friend, the boat operator | **Jassi** |
  | Jassi's girlfriend | **Himanshi** |
  | The girl (Boss's daughter, later Nikhil's girlfriend) | **Shreya** |
  | The mysterious man | no name - "Uncle" |
  | Mummy, Papa | no names (owner: "mummy papa ke naam nhi chahiye") |
  | Boss | not given yet |

## Still pending from the bible (section D)

Commission amounts and the exact earning gate (2.G), the 6 / 12-point package stops, the exact list of 6.3-6.7, Shreya's
personality / age / background, when and how the Boss reveal comes, Tower 360 reference photos.

## How the story missions are built (plan, 26 Nov 2026)

- **One runner** (`Runtime/Story/StoryMissions.cs`, replaces the old `MissionManager` catalogue): the chain of missions
  in the bible's order, the current one saved in `StoryFlags` (key `mission`), each mission = beats run one after
  another: a cutscene (`Resources/Story/<id>.json`, the existing `Cutscene` player: shots, lines, choices, the QTE
  `mash`), a gameplay step (the existing `MissionStep`s: go to, enter a vehicle, visit, + new ones: follow at a
  distance, swim to, drive clean (touch meter), detection meter), or a scripted set piece in code (the crash).
  The next mission's yellow marker stands where the bible says it starts; a mission that follows straight on starts
  by itself. Pass / fail / retry from the beat's checkpoint, as the bible's "Fail" column says.
- **Lines**: Hindi in Devanagari on screen (the Hinglish key + the Hindi in `Tools/loc/strings_story.tsv`), each line
  voiced: `Tools/story/make_voices.py` (edge-tts, one voice + pitch / rate per character in `Tools/story/cast.json`)
  -> `Assets/Resources/Voices/Story/<mission>/<n>.ogg`, the `voice` field of each `say`.
- **Chapter 0 set pieces**: the family car on the road into Nainital (a bend on a valley edge), the wrong-side car,
  the crash, the fire (`FireSpot`), the QTE out through the broken glass, the 108 ambulance + police lights, the POV
  ride with blur, a hospital room (built as a set), Mall Road tutorial, Uncle at the lakeside, follow him to the Zoo
  road house (FREEROAM house), the landlord, Uncle gone, sleep = save.

## Build status

| Chapter | Missions | Status |
|---|---|---|
| 0 Prologue | 0.0, 0.0b, 0.0c, 0.1, 0.2 | **built and played through (26 Nov 2026)**, English and Hindi |
| 1-10 | 1.1 ... 10.4 | not started |

## Chapter 0 as built (26 Nov 2026)

Code `Assets/Nainital/Scripts/Runtime/Story/Chapter0.cs`; scenes `Tools/story/scenes/c0_*.json` (-> `Resources/Story`);
27 voice lines `Resources/Voices/Story/c0_*`.

- **0.0 The Last Trip** - New Game starts it (an old save: a yellow marker at the Lower Mall start). The family's car on
  the **Haldwani - Nainital highway** (owner: "crash haldwani nainital road pe krna") from the map's edge up to the bend
  at (267, -3034) - a ~46 m drop on the left going up. Aerial, side and in-car shots through the talk (the bible's lines
  + two of mine: Mummy "Pehle Naina Devi ke darshan, phir jheel mein naav", Papa "Chalo, pahunch ke bahas karna... bas
  aadha ghanta aur"); the black SUV on their side round the bend, horn, skid, the hit; the car shoved over the left edge
  and stuck nose-down on the slope (fitted to the ground), fire at the front growing over 30 s; QTE "Sheesha todke bahar
  niklo" (E / Space / tap; each press moves him towards the window, it slides back when the presses stop; 15 s, else
  black and again); he crawls out, the car flares; from the road: the 108 and a police Bolero come down, sirens; black.
- **0.0b Sirens** - inside the ambulance (a white box with a back window built onto the model), the site with its
  police lights falling behind, blur; the player can turn his head (mouse / stick / drag) for a few seconds; "Maa...
  maa..." "Papa..."; he sobs (a narrator line).
- **0.0c Waking Up** - a hospital room (a set over BD Pandey, out of sight): "Aankhein kholo" (one press), the blur
  clears, the nurse, the one-option question "Meri mummy-papa kahan hain?", the doctor's two lines, the ceiling, black.
- **0.1 A New Start** - "Kuch din baad...", Lower Mall at (833, -896), "Naya shehar... aur main akela."; tutorial: walk
  8 m, run 1.2 s, turn the camera, the map (M) open + shut, the phone (T) open + shut; go to the lake marker.
- **0.2 A Roof Over My Head** - Uncle at the lake railing at (804, -743); Nikhil starts to tell, Uncle cuts in (bible
  lines). **Follow him** (he waits if Nikhil falls 11 m behind; 30 m for 1.5 s fails, retry from the meeting) ~300 m up
  the Upper Mall and onto the Snow View Road; then "Thodi der baad, Zoo Road..." at the house: the landlord ("Aapki
  baat to pehle hi ho chuki thi", "Ye lijiye chaabi"), Nikhil turns - Uncle is gone; sleep in the bed = save.
  MISSION PASSED + "Ghar mil gaya - bistar pe sone se game save hota hai".

**Changed from the bible (tell the owner):** the house's only way in is the Zoo Road from the Snow View Road - ~800 m on
foot from any point by the lake - so the follow is the first ~300 m and the scene cuts to the house.

**Open:** the doctor and the nurse wear the library's looks (the doctor is "Sardar ji" in a bright festive vest) - a
white coat outfit would be right; the family car is white (the model's paint); Mummy's head sits low in her seat.

## How to test (bridge, Play mode)

`call PlayTest.Begin`, then `call StoryTest.Mission` (arg.txt `s0_0` ... `s0_2`, optional ` step`), `StoryTest.MissionState`,
`StoryTest.Mash` (QTE presses), `StoryTest.Pick` (arg 1..4), `StoryTest.Lang` (`hi` / `en`), `StoryTest.FastWalk` (arg
factor) + `StoryTest.BehindUncle`, `FreeroamTest.Sleep`; `capture name` for shots. `StoryTest.ResetStory` forgets the
s0_* passes. End with `call PlayTest.End` (puts the save back).
