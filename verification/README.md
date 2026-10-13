# Verification

Run before every build that goes to Denis's game, and fully before any release. Record results (pass/fail, notes) in the commit message or docs/perf-log.md.

## A. Load
1. Game closed; DLL copied; md5 on disk equals the build.
2. `BepInEx\LogOutput.log`: `Loading [Wild Wasteland x.y.z]`, no Harmony exceptions, the WW startup summary lists every module with state (on / off by config / off: standalone mod present / off: init failed).
3. Same check with each soft dependency missing (Apocasetter, Apocaplayer, ApocalypterGarage, ...) and with each absorbed standalone mod also installed (its module must step aside, not run twice).

## B. Lifecycle
4. New game -> play 2 min -> save -> load that save -> load a different slot -> back to main menu -> load again. No exceptions, no duplicated objects, no module running twice (check counts in the debug overlay/log).
5. Save/load round trip keeps every module's state; save made with WW loads in a game WITHOUT WW (or the break is documented).

## C. World
6. Drive > 1 km in one direction (floating-origin shift): nothing WW tracks jumps, warps or loses its target.
7. Leave an area > 5 km and come back: WW handles objects destroyed by the game (no NREs, caches swept).
8. Enter a car / 3rd-person camera / exit: no item thrown out, no FSM restarted by WW.

## D. Performance (ModProfiler, see docs/perf-log.md)
S1 idle open world, S2 camp fight with 4+ NPCs, S3 driving across a tile boundary, S4 save load, S5 convoy / many vehicles. Compare against the same scenario with the equivalent standalone mods.
