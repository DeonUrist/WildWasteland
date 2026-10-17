# Console noise with debug off — 2026-10-18

Read Denis's current `E:\SteamLibrary\steamapps\common\Apocalypter\BepInEx\LogOutput.log` after his RC31 request. At inspection it contained 225 Info lines, 17 Warning lines, six Message lines and no Error/Fatal lines. By source: World Amp 89 lines, WW 65, SigmaMod 29; the remainder is other plugins, loader output and continuations. This is a single session snapshot, not a rate measurement.

## Why the switches do not silence it

- Vistas/Apocalypter World Amp's `Debug.Trace`, `Overlay` and `Probe` are already false. Trace controls a separate `WorldAmp-trace.txt`, Overlay controls the on-screen display, and Probe controls a separate one-shot report. They do not gate the normal BepInEx Info logger. The read-only decompile shows its road callback in `A/R.cs` calling LogInfo directly; `ApocalpyterWorldAmp/SpawnerTierPatch.cs` logs every changed group size directly; terrain paint, wheel and hill-assist reports also call LogInfo independently. WW's global VerboseLog cannot control another plugin's logger.
- WW's global and NPC VerboseLog entries are false. `Core/Log.cs` gates Info, but intentionally leaves Always enabled for startup/module/load milestones. `Spawning/VehicleBuildCache.cs` uses Always for its ready message; enqueueing a refill clears `_reported`, so the ready message repeats whenever reserves finish replenishing. Six ready messages occurred in this session. This is status logging, not evidence that a complete rebuild occurs six times.
- Warnings remain visible regardless of WW debug switches. Eleven are SigmaMod's `sigma_test.json` failing to find EngineSim component types, five are Harmony optional API/type lookups (two Dev Toolkit and three Apocapocket), and one is Vistas reporting that Azure fog fields were not found. These are separate compatibility/setup warnings, not verbose traces. Hiding Info will leave them visible.

## Console-only control

In `BepInEx/config/BepInEx.cfg`, `[Logging.Console]` currently has Enabled=true and LogLevels=`Fatal, Error, Warning, Message, Info`. Set its LogLevels to `Fatal, Error, Warning, Message` to hide ordinary Info messages from every plugin in the console. Retain `[Logging.Disk]` at `Fatal, Error, Warning, Message, Info` for diagnosis. Restart the game for BepInEx settings to apply. To hide the console entirely, set only `[Logging.Console] Enabled=false` instead; disk logging is independent.

There is no Vistas setting in this installed version that selectively turns off these normal Info calls. A Vistas logging change or an explicit per-source log filter would be needed for that. No installed logging configuration, external DLL, Sigma test asset or WW logging behavior was changed for this read-only request.
