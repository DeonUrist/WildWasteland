# Wild Wasteland

A BepInEx 5 mod for **Apocalypter** that combines five gameplay mods into one plugin: one load order, one set of Harmony patches, one save/scene lifecycle and one shared frame budget. Every former mod is a **module** with its own on/off switch; everything else about them (features, settings, saves, data files) stays as it was.

Version **0.1.0** (plugin GUID `com.denis.apocalypter.wildwasteland`). Status: ported and reviewed, in-game verification pending - treat it as a test build.

| Module | What it is | Replaces (remove these from `BepInEx\plugins`) |
|---|---|---|
| Player body & animation (`PlayerBody`) | Your character's body, third-person camera, animations, climbing, equipment on the body, vehicle HUD and hotkeys | Apocaplayer 2.3.9, Apocaclimber |
| NPC AI & navigation (`NPC`) | NPCs that see, hear, search, take cover and pace their bursts; camp maps; idle life; burrowing / ambushing / pouncing creatures, dogs, spider nests, storms | NPC Overhaul (NPCAI) 1.9.15 |
| Guns, ammo & tracers (`Guns`) | Real bullets with tracers and range falloff, vehicle part damage, NPC aim and lead, melee on wheels and corpses, boss health, per-gun magazines, hold-to-unload, backpack / armor overflow | Gunplay 1.3.0, Apocaunloader |
| Combat HUD (`HUD`) | Crosshair styles, selection dot, hit marker, damage numbers, headshot sound | Gunplay HUD 1.2.2 |
| Patrols, traffic & War Rig (`Patrols`) | Raider cars, trucks and bikes with live crews, patrols and convoys, persistent world traffic with faction battles and Coyote merchants, vehicle templates and editors, the War Rig tanker trailer | Apocapatrol 2.14.4, War Rig 1.1.3, Merchant Object Spotter |

While any of those standalone plugins is still installed, the module that replaces it **stays off** (the log says which DLL to remove, and the F7 Modules page shows it) - nothing runs twice, but nothing of that module runs either. Apocaraider (the old monolith) is not detected: remove it by hand.

## Install

1. BepInEx 5 is installed and Apocasetter is recommended (the F6 Mods menu shows and rebinds every setting; WW works without it).
2. Move the replaced mods out of `BepInEx\plugins\` (e.g. into `BepInEx\disabled-by-WildWasteland\`; keep them around until you are happy). Keep their config and data files - WW reads them once.
3. Unzip the release into `BepInEx\plugins\`:
   ```
   BepInEx\plugins\WildWasteland\WildWasteland.dll
   BepInEx\plugins\WildWasteland\theme\...            shared UI theme of the tool window and editors
   BepInEx\plugins\WildWasteland\PlayerBody\Models\, Sounds\   body models, animation bundles, HUD icons, pain sounds
   BepInEx\plugins\WildWasteland\NPC\Models\          dog animation bundles
   BepInEx\plugins\WildWasteland\Guns\Sounds\         knifestab.wav
   BepInEx\plugins\WildWasteland\HUD\Sounds\, Textures\   headshot.mp3, crosshair textures
   BepInEx\plugins\WildWasteland\Patrols\             templates, textures, sounds, layouts, WarRig\ models
   ```
   If an asset is missing, WW falls back to the old plugin folder (`plugins\Apocaplayer\`, `plugins\NPCAI\Models\`, `plugins\Gunplay\Sounds\`, `plugins\GunplayHUD\Sounds\`, `plugins\Apocapatrol\`, `plugins\WarRig\`) or to a copy built into the DLL, and warns in the log. Patrols copies its old data into its new folder on the first run.
4. Start the game. `BepInEx\LogOutput.log` shows `Loading [Wild Wasteland 0.1.0]` and a summary `Wild Wasteland 0.1.0 modules:` with every module `on`.

## Settings: F6 and F7

- **F6 (Apocasetter Mods menu, page "Wild Wasteland")**: the player-facing settings - one `Enabled` switch per module, one on/off toggle per feature, the gameplay values, and all hotkeys in `[Keys]`. File: `BepInEx\config\com.denis.apocalypter.wildwasteland.cfg`. Switching a module off at runtime stops it at once (patches removed, the game's own behaviour returns); on starts it again.
- **F7 (Wild Wasteland tool window, key rebindable in `[Keys] Tool window`)**: everything technical in one place - the module overview (on/off, errors), performance per job, the Harmony patch list, and per module the live status pages, editors (patrol editor, template recorder, turret spot / bus passenger pose / merchant item editors, weapon pose editor) and every tuning value the old mods kept hidden. File: `BepInEx\config\WildWasteland\tools.cfg`. The window pauses the game; Esc or CLOSE closes it. Editor hotkeys default to **None** - open the editors from F7 (or bind a key in F6).
- `[Debug] VerboseLog` (F6, off by default) turns on the detailed info logging of every module.

Module data and sidecars: `BepInEx\config\WildWasteland\<Module>\`.

## First run: migration

When WW's cfg files do not exist yet, it reads the old ones once (`com.denis.apocalypter.apocaplayer.cfg`, `apocaclimber`, `npcai`, `gunplay`, `apocaunloader`, `gunplayhud`, `apocapatrol`, `warrig`, `merchantspotter`, Apocaraider as a fallback, `config\Apocaplayer\camera.cfg`, the `hidden-settings.not-saved` files) and copies every value it knows, including key bindings (converted to the new key names). Data files are copied where missing: `config\Apocaplayer\*` -> `config\WildWasteland\PlayerBody\`, `config\NPCAI\*` -> `config\WildWasteland\NPC\`, `plugins\Apocapatrol\*` + `plugins\WarRig\*` -> `plugins\WildWasteland\Patrols\`. The old files are never changed, and WW keeps reading the old locations as a fallback. Keys whose default became None (bus pose editor Insert, merchant editor Numpad*, NPC test web F9) are only carried over if you had changed them. The log lists every migrated value.

## Saves

Nothing about saves changed: per-gun magazines stay under `Apocaunloader.Magazines` in the game's save file, patrols under `Apocapatrol.Patrol` plus the sidecar `config\WildWasteland\Patrols\Saves\<SaveGameN.es3>.bin` (old `config\Apocapatrol\Saves\` still read), NPC knowledge in `config\WildWasteland\NPC\Saves\<slot>.senses.txt`, motorcycles and War Rigs keep their prefab ids. Saves made with the standalone mods load in WW, and saves made with WW load in a game **without** WW exactly as they did without the standalone mods: everything vanilla is fine; raider crews, motorcycles and War Rigs that came from the mods cannot be restored by the game alone (same as before).

## Known gaps (0.1.0)

- Not yet verified in the game (no game in the build environment): run the checks in `verification/README.md`; the first bugs will show up as `job '...' threw 20 times in 10 s - job DISABLED` lines in the log.
- Assets are not in the repository; the release zip must contain them (see Install) or the old plugin folders must still be present.
- Other mods that used the old public APIs by reflection (`NPCAI.Api`, `Apocaplayer.ModAPI`, `Gunplay.Api`, `GunplayHUD.Api`) will not find them; no known mod does. Ask for a shim if you have one.
- Apocasaver no longer recognises the patrol system (it keyed on Apocapatrol's GUID): patrol saves work exactly as without Apocasaver; Apocasaver's clean-save step no longer removes abandoned raider cars.
- Performance has not been measured yet (`docs/perf-log.md` has the procedure); the budget is 0.5 ms average / 2 ms worst per frame for the whole mod.
- Simple-bandit crews are not handed back to the NPC module when Patrols is switched off at runtime (they stay seated and inert until reload).

## Build

No compiler is needed on the game machine. In the cloud (Linux, mono mcs 6.8):
```
apt-get update && apt-get install -y mono-mcs
MANAGED=<game>/Apocalypter_Data/Managed BEPCORE=<game>/BepInEx/core sh build.sh <outdir>   # -> <outdir>/WildWasteland.dll
MANAGED=... BEPCORE=... sh tools/build-module.sh Patrols                                   # Core + one module, others stubbed
MANAGED=... BEPCORE=... sh tools/config-harness/run.sh <outdir> <BepInEx/config with old cfgs>   # binds every config without the game
```
On Windows: `dotnet build WildWasteland.csproj` (`-p:Deploy=true` copies the DLL into the game, game closed). `AGENTS.md` has the rules for contributors and AI sessions; `docs/` has the module, patch, decision and performance records.

## Links

- Wiki (features, settings, troubleshooting, architecture): https://github.com/DeonUrist/WildWasteland/wiki (source: `V:\Repos\Apocalypter\WildWastelandWiki`)
- Project board (feature status: Backlog / Working on / Testing / Verified / Done): the GitHub project of this repository; `Feature-Status` in the wiki mirrors it.
- Apocasetter / mod index: https://github.com/DeonUrist/Apocasetter-Index
