# Wild Wasteland

A BepInEx 5 mod for **Apocalypter** that combines gameplay mods into one plugin: one load order, one set of Harmony patches, one save/scene lifecycle and one shared frame budget. Every former mod is a **module** with its own on/off switch; everything else about them (features, settings, saves, data files) stays as it was.

Version **0.1.19 RC16** (plugin GUID `com.denis.apocalypter.wildwasteland`). Status: in-game verification remains pending for features marked Testing on the project board.

| Module | What it is | Replaces (remove these from `BepInEx\plugins`) |
|---|---|---|
| Player body & animation (`PlayerBody`) | Your character's body, third-person camera, animations, climbing, equipment on the body, vehicle HUD and hotkeys | Apocaplayer 2.3.9, Apocaclimber |
| NPC AI & navigation (`NPC`) | NPCs that see, hear, search, take cover and pace their bursts; camp maps; idle life; burrowing / ambushing / pouncing creatures, dogs, spider nests, storms; female raiders and crew | NPC Overhaul (NPCAI) 1.9.15, WomenOfWasteland 1.0.0 |
| Guns, ammo & tracers (`Guns`) | Real bullets with tracers and range falloff, vehicle part damage, NPC aim and lead, melee on wheels and corpses, boss health, per-gun magazines, hold-to-unload, backpack / armor overflow | Gunplay 1.3.0, Apocaunloader |
| Combat HUD (`HUD`) | Crosshair styles, selection dot, hit marker, damage numbers, headshot sound | Gunplay HUD 1.2.2 |
| Clean HUD & menus (`CleanHUD`) | Vitals, weapon silhouettes and ammo, quick slots, strip compass, message log, title/pause menus and controls catalog | CleanHUD 1.5.1 |
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
4. Start the game. `BepInEx\LogOutput.log` shows `Loading [Wild Wasteland 0.1.19]` and a summary `Wild Wasteland 0.1.19 modules:` with every module `on`.

Engine Sim compatibility (#127, Testing): **Engine Sim is optional; WW works normally without it and the bridge stays idle.** If you want its features, install [Engine Sim 1.3.0](https://github.com/Wad67/Apocalypter-enginesim/releases/tag/v1.3.0) separately, including its native library and impulse responses. WW schedules its manager and supplies a 500 microsecond scene-discovery cache; the original native simulation/audio threads, engine selection, settings and vehicle-part physics remain with Engine Sim. With its experimental simulated drivetrain enabled, the existing Ignition key supports click to toggle and hold to crank; vehicle hints and CleanHUD's existing engine-status icon follow the coupled engine. Patrols start/stop requests use the simulated switch. Turn off the Engine Sim compatibility module to restore the standalone callbacks. Engine Sim is not included in this archive. Its settings remain in its own Apocasetter page and cfg files; only `[EngineSimCompat] Enabled` is added to WW settings.cfg. Performance and in-game verification remain pending.

## Settings: F6 and F7

RC16: approaching a location within 100 horizontal metres reveals its real icon on both map and compass. Discovery survives marker/ambiguity toggles and save/load; cleared red scrapyards become white. The map gains saved fog of war, importing Immersive Map exploration when available, and CleanHUD framing. Map/compass detail defaults to Ambiguous for fresh settings. Needs flash yellow at 80–90 and red above 90. Community links share one background. See [RC16 changes and test steps](notes/release-0.1.19-RC16.md).

RC15: Controls uses a draggable scrollbar and mouse-wheel scrolling, with background dragging disabled. UPDATE AVAILABLE has a dark HUD panel. Smaller framed DISCORD and GITHUB links stack vertically at the bottom right of title/pause main screens, above MODS. Duplicate-key warnings appear in Controls, without the red warning on the main screen. See [RC15 changes and test steps](notes/release-0.1.18-RC15.md).

RC14: the optional Quests J journal now uses CleanHUD's panel and atlas font, with gold quest headings, the original completed count and full journal text, and mouse-wheel scrolling. The source mod retains its J/rebound-key controls and unpaused behavior; disabling CleanHUD restores the original window. No new config entries or save data. See [RC14 changes and test steps](notes/release-0.1.17-RC14.md).

RC13: the main menu shows UPDATE AVAILABLE in the top right, labeled WILD WASTELAND, when the mod's own GitHub releases contain a newer version. The asynchronous check runs once per game launch and includes experimental prereleases, independently of the Apocasetter index. Clicking opens the newer release page. See [RC13 changes and test steps](notes/release-0.1.16-RC13.md).

RC12: F3 → Debug starts with general VerboseLog and groups all other WW verbose/log switches directly below it. Existing keys, values and logging behavior are preserved. CleanHUD's ChaseCam speed/RPM/gear readout follows live driving in either camera view; a latched scope/binocular camera flag no longer suppresses it until ESC. See [RC12 changes and test steps](notes/release-0.1.15-RC12.md).

RC11: Settings → Game → HUD → Patrols on compass → Ambiguous keeps vehicle icons and makes every group white. Locations on compass → Ambiguous uses white question marks. Both keep distances; map modes and quest flags retain their existing behavior. Existing ambiguity settings are preserved.

- **F6 (Apocasetter → Wild Wasteland)** shows `In-Game Settings Button` (default **F3**) and `Enable Debug` (default false). The Apocasetter opt-in remains enabled and hidden. File: `BepInEx\config\com.denis.apocalypter.wildwasteland.cfg`.
- **Internal SETTINGS menu** (default **F3**, historically called F7) has **Game** (Player, World, HUD), **Modules** (module switches and performance), and **Debug** when enabled in F6. It pauses the game; Esc or CLOSE closes it. Gameplay values and keys live in `BepInEx\config\WildWasteland\settings.cfg`; technical settings live in `tools.cfg`. Debug includes editors and switches; fine tuning stays in the config file.
- RC1 defaults match Denis's reviewed setup, except **Toggle Ironsights stays off** as requested. CleanHUD needs and HP/armor numbers are on, the crosshair is Vanilla, observing is toggled, camera occlusion is off, and third-person distances are 3.26 m on foot / 5.38 m in cars. Patrols debug spawn controls and traffic debug-map routes are on. Global verbose logging and Enable Debug remain off. The [complete defaults audit](docs/defaults-rc1.md) records the 691 RC1 base entries; RC2 adds ShowConvoys and ShowLocations, both enabled by default. Existing config values and first-run standalone migration are preserved.

Module data and sidecars: `BepInEx\config\WildWasteland\<Module>\`.

CleanHUD loading screens use `BepInEx\plugins\WildWasteland\LoadingScreens\Screen1.png`, `Screen2.png`, etc. The `screens.json` in the same folder maps each image filename to its tip text, with defaults for Screens 1–5. Available numbered images are chosen randomly for each new game/save load or Apocasaver autosave; repeats are allowed. JSON edits and added/replaced images apply on the next load; unchanged textures remain cached. The persistent screen keeps the CleanHUD font through scene changes, with LOADING (AUTOSAVING during Apocasaver saves) bottom left and a smaller wrapped tip near top left. Clean menus must be enabled. Missing numbered images are skipped.

CleanHUD controls are in **F7 → Game → HUD** (Clean HUD, Clean menus, Needs, HP and armor numbers, Insanity, Scale). Its module switch and job timings are in **F7 → Modules**; EnableCheats is in Debug. Settings live in `config\WildWasteland\settings.cfg`, under `CleanHUD / HUD` and `CleanHUD / Menu`. Missing settings import the standalone choices even on an existing WW install. Layout and the game key cache copy once into `config\WildWasteland\CleanHUD\`; edits to the new layout reload within two seconds. All CleanHUD textures/font assets are embedded, so no asset-folder copy is needed. Disable standalone CleanHUD through Apocasetter and restart to use the built-in module.

RC3 adds optional CleanHUD compatibility with **Apocapocket 2.0.10**, which stays a separate plugin and is not included in this archive. CleanHUD shows its three to six logical slots, highlights extra-slot weapons correctly, displays ordinary item icons, and updates key badges after rebinding. Backpack capacity controls the number of shown slots. The row retains its existing placement and scale; switching CleanHUD off restores the native pocket UI. No new config entries or save data. Verification remains Testing; see [test steps](notes/compat-Apocapocket-CleanHUD.md).

RC4 adds optional compatibility with **Apocatalker 2.0.0** and **Apocalypter Quests 1.8.0**. Keep those two mods installed, including their dialogue, quest, faction, theme and voice files; they are not bundled or replaced. WW schedules their callbacks and slices world discovery, renders CleanHUD dialogue with the original window as its fallback, adds native map/compass quest markers and connects hired followers to WW animations, reload holds and passenger combat. Immersive Map remains supported when installed. `[QuestsCompat] Enabled` defaults true in settings.cfg. Original ES3 key `ApocalypterQuests` and Talk's shared/manual story profiles are preserved. Features remain Testing; see [compatibility and test steps](notes/compat-Talk-Quests.md). If native plugin components were destroyed during a scene change, restart the game to compare fully standalone behaviour after disabling the bridge.

Women of Wasteland is now part of **NPC**. Its population controls live in **WW Settings > Debug > NPC AI & navigation > NPC population**, not Apocasetter/F6. The default female population is 33%. Eligible Flexa/Sprokka drivers, passengers and gunners use the configured female percentage; editors also offer Gungirl/Shoota explicitly. Default models/textures/voices are included in the DLL and installed only where missing under `WildWasteland/NPC`. Existing WoW settings and custom assets migrate without overwriting WW choices. If standalone WoW is loaded, the integrated female feature stays off while NPC AI continues; disable standalone WoW and restart. In-game verification remains Testing; see [port and test steps](notes/port-NPC-Women.md).

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

RC5 fixes the Quests follower hook that prevented hiring, adds distinct cave/wreck/camp icons and separate compass/map/marker/route controls, and uses compact gold dialogue choices plus CleanHUD quest notifications. Map icons have no name labels. See [RC5 changes](notes/release-0.1.8-RC5.md) and [in-game test steps](notes/patch-map-hiring-dialogue.md).

RC6 filters new delivery destinations to camps with a living, loaded NPC accepted by Talk and non-hostile to the player. Empty camps with a faction-named spawner no longer qualify. Existing accepted quests keep their saved destinations. See [RC6 changes and test steps](notes/release-0.1.9-RC6.md).

RC7 replaces RC6's restriction with delivery recovery: distant camps qualify again; reaching an empty camp changes the objective to return the goods and explain that nobody was there. A destination already known to be cleared, or an empty return point, fails the quest. Native spawners must finish and a fresh NPC scan plus 15 seconds of active play near the empty point must pass. Return progress uses the existing quest save fields. See [RC7 behavior and test steps](notes/release-0.1.10-RC7.md).

RC8 uses flags for every quest phase, with each flag matching its quest tracker bar color. The tracker clears the compass even when Insanity is hidden. Clean menus also styles Apocasaver's save-name prompt. The full package includes Women of Wasteland assets with a 33% default population. Existing configured population values remain preserved. See [RC8 changes and test steps](notes/release-0.1.11-RC8.md).

RC9 adds optional **ApocaChaseCamera 0.2.5** compatibility. Keep it installed separately: it retains its driving camera and original instruments with vanilla UI. CleanHUD shows smaller unboxed speed/RPM/gear text above the weapon panel, with RPM redline color and the rendered camera's compass heading. Size, height and enable remain ChaseCamera settings; hide diagnostics require WW verbose logging. RC9 also includes the pending crew quest restrictions: seated crew cannot be hired; current/former crew cannot offer new quests, with former-crew names saved per slot. See [RC9 changes and tests](notes/release-0.1.12-RC9.md).

## Build

On Windows (PowerShell or cmd, .NET SDK 6+, nothing else needed):
```
dotnet build WildWasteland.csproj -c Release -p:Deploy=true    # builds and copies the DLL into the game (game closed)
```
Add `-p:GameDir="<path to Apocalypter>"` if the game is not in `E:\SteamLibrary\steamapps\common\Apocalypter`. Do not use WSL for this repo.

Claude cloud sessions build with mono instead (Linux container only, not for the game PC):
```
apt-get update && apt-get install -y mono-mcs
MANAGED=<game>/Apocalypter_Data/Managed BEPCORE=<game>/BepInEx/core sh build.sh <outdir>   # -> <outdir>/WildWasteland.dll
MANAGED=... BEPCORE=... sh tools/build-module.sh Patrols                                   # Core + one module, others stubbed
MANAGED=... BEPCORE=... sh tools/config-harness/run.sh <outdir> <BepInEx/config with old cfgs>   # binds every config without the game
```
`AGENTS.md` has the rules for contributors and AI sessions; `docs/` has the module, patch, decision and performance records.

## Links

- Wiki (features, settings, troubleshooting, architecture): https://github.com/DeonUrist/WildWasteland/wiki (source: `V:\Repos\Apocalypter\WildWastelandWiki`)
- Project board (feature status: Backlog / Working on / Testing / Verified / Done): the GitHub project of this repository; `Feature-Status` in the wiki mirrors it.
- Apocasetter / mod index: https://github.com/DeonUrist/Apocasetter-Index

Compass markers (Testing): SETTINGS → Game → HUD has **Convoys on compass** and **Locations on compass (1 km)**, both on by default. Convoys covers all vehicle groups: merchant yellow, Coyotes green, patrols red, convoys orange, with one car icon and distance per group. Nearby native locations use a camp icon and distance within 1 km. These are independent of debug-map routes; toggles are saved as `CleanHUD / HUD` ShowConvoys / ShowLocations in settings.cfg. Existing unrelated compass markers remain available.
