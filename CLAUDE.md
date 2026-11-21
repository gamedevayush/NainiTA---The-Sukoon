# Naini Sukoon - notes for Claude (read first)

Unity 6000.4.1f1 open-world Nainital. Public repo, Git LFS. The owner talks Hinglish; give him full absolute paths.

## After every pull / on a new PC - check these and TELL THE OWNER what is missing
1. **Two free Asset Store packs are not in git** (Asset Store licence, public repo). If `Assets/MotoInteractionAnimsFREE/`
   or `Assets/Kevin Iglesias/` is missing, tell him to import them: Package Manager > My Assets > "Motorcycle Interaction
   Animset FREE" (SpectraMotions) and "Human Basic Motions FREE" (Kevin Iglesias) > Download + Import, then
   **Nainital > 6b. Build character animator**. Unity also warns about it on load (`StorePackCheck`). Never commit them.
2. Unity Hub must be running and signed in, or batch mode fails with "No valid Unity Editor license".
3. The built world (`Assets/Nainital/Generated/`) is not in git: **Nainital > 2. Build World** after `world.json` changes.

## Where to start
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
