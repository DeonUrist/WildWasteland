# Modules

One row per mechanic that lives (or will live) in Wild Wasteland. "Ported from" is the source repo commit the code was taken from, so later fixes in the standalone mod can be diffed and carried over. Snapshot date of every source tree: **2026-10-13**.

Status values: `candidate` (not decided), `planned`, `porting`, `in` (ported; 0.1.0 = compiled and reviewed, awaiting in-game verification), `dropped`.

## 1. Modules in Wild Wasteland 0.1.0

Init order = config display order = the order below (providers before consumers). Every module: `DefaultEnabled = true`, one Harmony id `com.denis.apocalypter.wildwasteland.<Id>` created by Core, no own update loop, no own Harmony instance.

| Module (Id) | DisplayName | Status | Source mod, version, commit | Folder / namespace | ConflictsWith (standalone GUIDs) |
|---|---|---|---|---|---|
| `PlayerBody` | Player body & animation | `in` | Apocaplayer 2.3.9 `ba17d32` (+ Apocaclimber 1.0.2, already absorbed by Apocaplayer, no git) | `src/Modules/PlayerBody/` = `WildWasteland.Modules.PlayerBody` (subfolders Animation, Body, Camera, Climbing, Vehicles) | `com.denis.apocalypter.apocaplayer`, `com.denis.apocalypter.apocaclimber` |
| `NPC` | NPC AI & navigation | `in` | NPC Overhaul (npcai) 1.9.15 = `5d3871e` (1.9.12) + uncommitted working tree | `src/Modules/NPC/` = `WildWasteland.Modules.NPC` (Detection, Navigation, Brain, Animals, Hazards, Debug) | `com.denis.apocalypter.npcai` |
| `Guns` | Guns, ammo & tracers | `in` | Gunplay 1.3.0 `006d690` (contains Apocaraider 1.7.0 projectiles + Apocaunloader 1.3.0 ammo) | `src/Modules/Guns/` = `WildWasteland.Modules.Guns` (Tracers, Ammo, Bosses) | `com.denis.apocalypter.gunplay`, `com.denis.apocalypter.apocaunloader` |
| `HUD` | Combat HUD | `in` | Gunplay HUD 1.2.2 `a8f94cf` | `src/Modules/HUD/` = `WildWasteland.Modules.HUD` (Crosshair, HitFeedback, Embedded) | `com.denis.apocalypter.gunplayhud` |
| `CleanHUD` | Clean HUD & menus | `in` (Testing) | CleanHUD 1.5.1 `119a2f8` | `src/Modules/CleanHUD/` = `WildWasteland.Modules.CleanHUD` (+ `.Api` for the public compass types); 46 embedded assets | `com.denis.apocalypter.cleanhud` |
| `Patrols` | Patrols, traffic & War Rig | `in` | Apocapatrol 2.14.4 = `ed0c422` (2.7.5) + uncommitted working tree; War Rig 1.1.3 `89419ea`; debug MerchantSpotter 1.0.2 (inside the Apocapatrol tree) | `src/Modules/Patrols/` = `WildWasteland.Modules.Patrols` (+ `.Motorcycles`, `.WarRig`; subfolders Spawning, Vehicles, Crews, Traffic, Merchants, Editor, Persistence, WarRig, Embedded) | `com.denis.apocalypter.apocapatrol`, `com.denis.apocalypter.warrig`, `com.denis.apocalypter.merchantspotter` |

| `POI` | Points of interest | `in` (Testing, #81) | New native MapMagic integration | `src/Modules/POI/` = `WildWasteland.Modules.POI` | - |
| `QuestsCompat` | Talk & Quests compatibility | `in` (Testing) | Optional Apocatalker 2.0.0 / Quests 1.8.0 bridge; DLL md5 `518f44ad8318d62bdcb1aff739291e93` / `589c5185d0825ae0a7dd1601b55d6054` | `src/Modules/QuestsCompat/` = `WildWasteland.Modules.QuestsCompat` | None: the two standalone mods must remain installed |

Not separable inside a module (no toggle): motorcycles (bike templates + ES3 prefab resolution) inside Patrols; simple convoy bandits inside NPC (driven by Patrols through the service). War Rig remains a **feature** of Patrols (`WarRig/` folder, two F6 toggles); its new world sites are registered through the POI module.

### 1.1 F6 sections (main cfg, Apocasetter page "Wild Wasteland")

Since the 2026-10-14 settings rework, F6 displays only `[Keys] In-Game Settings Button` (F7 by default) and `[General] Enable Debug` (false by default). The required Apocasetter opt-in is hidden by Apocasetter. Former gameplay settings, module switches and hotkeys retain their sections/keys in `config/WildWasteland/settings.cfg`, copied once from the old main cfg. The table below inventories those stored settings, rather than F6 controls; legacy orphans in the public cfg are retained.

| Module | Sections | Entries | Keys in `[Keys]` |
|---|---|---|---|
| PlayerBody | `[PlayerBody]`, `[PlayerBody / Climbing]`, `[PlayerBody / Camera culling]`, `[PlayerBody / Vehicles]` | 24 incl. Enabled | Observe (third person), Climb (alternative key), Cassette start/stop, Cassette volume down, Cassette volume up |
| NPC | `[NPC]`, `[NPC / Detection]`, `[NPC / Pathfinding]` | 21 incl. Enabled | Shout, Shout modifier (renamed from NPC shout / NPC shout modifier; old values carried over) |
| Guns | `[Guns]`, `[Guns / Projectiles]`, `[Guns / Ranges]`, `[Guns / Ammo]`, `[Guns / Bosses]` | 28 incl. Enabled | Guns: unload fallback key |
| HUD | `[HUD]` | 8 incl. Enabled | - |
| CleanHUD | `[CleanHUD]`, `[CleanHUD / HUD]`, `[CleanHUD / Menu]` | 8 incl. Enabled; edited in F7 Game / HUD, cheat toggle in Debug | - |
| Patrols | `[Patrols]`, `[Patrols / Spawning]`, `[Patrols / World traffic]`, `[Patrols / Merchants]`, `[Patrols / Combat]`, `[Patrols / Balance]` | 40 incl. Enabled | Patrol editor, Template recorder, Bus passenger pose editor, Merchant item editor |
| POI | `[POI]`, `[POI / World]` | 2 incl. Enabled | - |
| Core | `[General] Apocasetter`, `[Keys] Tool window` (F7), `[Debug] VerboseLog` | 3 | Tool window |

The full entry lists with meanings are in the wiki (`Guides/F6-Settings.md`) and in each module's wiki page.

### 1.2 F7 pages (tools.cfg: `BepInEx\config\WildWasteland\tools.cfg`, 545 entries)

The SETTINGS window has Game (Player: body pictures, Full Body, First Person Arms, Climbing, Third Person Camera, Occlusion; World: Enemy Vehicles, New POI; HUD: crosshair previews, Grabbing dot, floating damage, hit markers, headshot sound, Toggle Ironsights, Shout + Shout modifier keys on one row), Modules (every module toggle followed by Performance), and Debug (hidden unless enabled in F6). Off is left and On is right. First Person Arms is Off / On foot / On (default On); Occlusion defaults On and switches only the on-foot third-person culling (CameraCullingInVehicles defaults Off and the Game page never changes it). Enemy Vehicles is Loner / New arrival / Fury road: Loner disables automatic spawns and world traffic, New arrival enables both with distance/boss gates, Fury road enables both with all patrol types available from the start. Lines uses the native crosshair's two solid horizontal bars; Grabbing dot controls SelectionDot (default On). The former pages below are sections inside the single Debug page: Tools first (Patrols editors, then the other editors / overlays: pages with `ToolPage.IsTool`), then the settings and diagnostics pages, then the remaining settings.cfg entries. tools.cfg entries (tuning, offsets, poses) are not drawn except debug switches (sections `/ Debug`, `/ Editors`, keys `Log` / `*Verbose*`, the traffic debug-map toggles); they still work from the file. Game opens automatically for a new game, never a loaded save. Fury road defaults On for fresh installs; saved settings are preserved.

| Module | Group | Pages | tools.cfg sections |
|---|---|---|---|
| Core | Wild Wasteland; Debug & performance | Modules; Performance, Harmony patches, Core settings | `[Core]` (Frame budget (ms)) |
| PlayerBody | Player body & animation | Animation framework, Body & model, Camera, Climbing, Weapon pose editor, Car seat poses, First person, Animation tuning, Vehicles & ModAPI | `PlayerBody / Editors`, `/ Camera`, `/ First person`, `/ Car seat`, `/ Camera culling`, `/ Climbing`, `/ Animation`, `/ Animation (fallback clips)`, `/ Model`, `/ ModAPI` (68) |
| NPC | NPC AI & navigation | AI overview, Detection, Navigation & baking, Brain & combat, Idle & movement, Animals, Hazards (nests, webs, storms), Debug | `NPC / Detection`, `/ Senses`, `/ Nav`, `/ Brain`, `/ NpcAim`, `/ Idle`, `/ Movement`, `/ Behaviour`, `/ Burrow`, `/ Hunt`, `/ Ambush`, `/ Pounce`, `/ Dogs`, `/ Nests`, `/ Webs`, `/ Debug` (179) |
| Guns | Weapons & HUD | Tracers & projectiles, NPC aim, Ammo & magazines, Armor & overflow, Bosses & orbs | `Guns / Tracers`, `/ Ballistics`, `/ NPC aim`, `/ Melee`, `/ Ammo` (30) |
| HUD | Weapons & HUD | HUD | `HUD / Hit feedback`, `/ Crosshair`, `/ Headshot sound` (11) |
| Patrols | Patrols & vehicles; Seat & turret poses; Merchants | Patrol editor, Template recorder, World traffic, AI overlay & debug spawns, Driving AI tuning, Combat tuning, Spawning & cleanup, War Rig; Turret spot editor, Bus passenger pose editor, Driver & passenger poses, Raider arm & weapon poses, Turret rider pose; Merchant item placement, Merchants | `Patrols / Debug`, `/ Traffic tuning`, `/ Audio`, `/ Combat tuning`, `/ Turret pose`, `/ Driving`, `/ AI`, `/ Loot`, `/ Spawner`, `/ Cleanup`, `/ Pose`, `/ Pose.<Human>` x7 (~256) |

### 1.3 Services (src/Core/Services.cs) and module-to-module dependencies

Modules never reference each other's types. Every dependency below goes through `Core.Services` (interface fields set by the provider in Init, null in Shutdown; consumers null-check every call and fall back as the standalone mods did without the provider) or the two Services events.

| Provider | Service | Was (standalone API) | Consumers |
|---|---|---|---|
| CleanHUD | `Services.VehicleStatus` (`IVehicleStatusDisplay`) | PlayerBody's separate vehicle status drawing | PlayerBody submits ignition, handbrake and cassette/volume state. CleanHUD displays it left of Insanity while enabled; the original display remains the fallback. |
| CleanHUD | `Services.Map` (`IMapMarkers`), existing `Services.Compass` | Quest MapLink's Immersive Map markers | QuestsCompat mirrors quest/turn-in markers to the native map and compass; original Immersive Map integration remains supported. |
| Patrols | `Services.Camps` (`ICampStatus`, `CampState`) | QuestsCompat delivery recovery | Read-only access to persisted cleared camps and existing loaded/spawner readiness. No new scans or jobs; unavailable/unknown/unloaded camps never count as empty. |
| CleanHUD | `IMapMarkers.Markers` / `Routes` (`MapDetail`) | Map display choices | Patrols consumes route display mode without a module type reference. CleanHUD handles native debug-map access, map markers and location icons. Compass Off hides the whole strip; map Off disables native MapCheat. New settings.cfg entries: `[CleanHUD / HUD] Compass=true`; `[CleanHUD / Map] Enabled=true, Markers=On, Routes=On` (Routes imports existing DebugMap/hide-route choices once). |
| Patrols | `Services.FollowerVehicles` (`IFollowerVehicles`) | Hire's Apocapatrol reflection | QuestsCompat retains original seat/snapshot logic through a provider-owned legacy type map; supplies the explicit target, reload hold and animated-arm switch. |
| NPC | `INpcApi.SetFollowerDriven` | Hire's NPCAI idle exclusion | QuestsCompat owns follower movement/animation and magazine timing while hired; NPC yields steering and honours the external reload deadline. |
| PlayerBody | `Services.Animation` (`IAnimationApi`, `IAnimatedCharacter`) | `Apocaplayer.ModAPI` / `ModAPI.Character` (+ Apocapatrol's `Rider.Clip` reflection) | NPC (ApBody: gunmen animations, probes every 5 s until Ready; Senses: `PlayerShout` supplies the selected character's yell), Patrols (PlayerAnims: human turrets; `GetClip` for riders), Guns (`PlayerThirdPerson` gates first-person zoom; `TryPlayerWeaponMuzzle` supplies the visible third-person prop's cached muzzle/bolt tip, false when unavailable) |
| NPC | `Services.Npc` (`INpcApi` + `Structures(list)`) | `NPCAI.Api` v1 + `NPCAI.Nav._structures` reflection | Guns (Bridge: `Shot`, `Hurt`, `SpreadFactor`; built-in spread fallback), Patrols (SimpleBanditBridge; TrafficLocations: camp maps) |
| Guns | `Services.Guns` (`IGunsApi` + `HandlesFittedWheelMelee`) | `Gunplay.Api` + the `Apocapatrol.MeleeWheels.RaiderHandles` Harmony redirect | NPC (WeaponRanges, VanillaEvents: `ProjectilesEnabled` decides vanilla-observer vs projectile mode), Patrols (MeleeWheels stands down when `HandlesFittedWheelMelee`), PlayerBody (dynamic crosshair uses `PlayerProjectilesEnabled` and cached `BlocksProjectile` rules for its eye/muzzle rays) |
| HUD | `Services.Hud` (`IHitFeedback`) | `GunplayHUD.Api` (reflection from Gunplay) | Guns (PlayerHit / PartHit; sets `ExternalReporter = ProjectilesEnabled` every frame and on toggle; HUD seeds it in Init) |
| Patrols | events `Services.CrewBailedOut(car, pax, npc)`, `Services.VehicleExploded(car)` | NPCAI's postfixes on `Apocapatrol.Patrol.BailOut` / `Explode.Blast` | NPC (Senses: bailed crews keep knowledge, explosions are heard) |
| POI | `Services.Poi` (`IPoiApi`, `PoiDefinition`), `Services.PoiChanged` | Native world-generation additions, no replacement mod | Patrols registers the abandoned War Rig encounter and its temporary map Xs. Registration notification supports POI runtime off/on without a module type reference. |

Dependency graph (all optional at runtime): PlayerBody -> (NPC, Patrols, Guns); NPC -> (Guns, Patrols); Guns -> (NPC via consume, HUD, PlayerBody crosshair); HUD -> Guns (consume); Patrols -> NPC (events). PlayerBody/Guns muzzle and crosshair queries are null-checked at use time, with the existing native behavior when the provider is unavailable. Order dependence that matters: NPC's shot-counting prefixes must run before Guns' on the shared `Raycast.OnEnter` / `SensorGetDetectionRayHit.OnEnter` dispatchers (they do, by init order).

### 1.4 Replaced public APIs - known gap

The absorbed mods exposed public types that other mods could use by reflection. In WW they do not exist under their old names; the only known consumers are the absorbed mods themselves, which now use `Core.Services`:

| Old public API | Old consumers | WW replacement | Third-party status |
|---|---|---|---|
| `NPCAI.Api` (v1: SetSimpleConvoyBandit*, Shot, Hurt, SpreadFactor, Inform) | Gunplay, Apocapatrol | `Core.Services.Npc` (`INpcApi`) | **gap**: a third-party mod looking up `NPCAI.Api` finds nothing. No shim shipped in 0.1.0; none known to exist. |
| `Apocaplayer.ModAPI` / `ModAPI.Character` | NPCAI, Apocapatrol | `Core.Services.Animation`; `ModAPI`, `ModAPI.Character`, `PlayerTraversal` stay public inside `WildWasteland.Modules.PlayerBody` (other modules must use the service) | **gap**: assembly/namespace changed (`WildWasteland.Modules.PlayerBody.ModAPI`); a mod compiled against `Apocaplayer.dll` will not bind. No shim. |
| `Gunplay.Api` | NPCAI | `Core.Services.Guns` | **gap**, none known. |
| `GunplayHUD.Api` | Gunplay (reflection) | `Core.Services.Hud` | no shim: its only consumer was standalone Gunplay, which conflicts with the Guns module anyway. |
| Apocapatrol hooks (`Patrol.BailOut`, `Explode.Blast` as patch targets; `Register.AddNow`, `PatrolPersistence.*`, `Cleanup.Update`, `Patrol.SaveBusy` reached by Apocasaver) | NPCAI, Apocasaver | `Services.CrewBailedOut` / `VehicleExploded`; Apocasaver: see section 2 | Apocasaver's participant adapters are inactive (keyed on the Apocapatrol GUID). |

If a third-party consumer appears: ship a compatibility shim (a thin public type with the old namespace in a separate small assembly, forwarding to `Core.Services`) - never register the old GUIDs (AGENTS.md 5.8, decisions 2026-10-13).

### 1.5 Data, assets, migrations

Rules shared by all modules (details per module in the wiki pages and port reports `/home/claude/ww/notes/port-<Id>.md`):

- **Config, first run only** (`Core.Legacy.FirstRunMain` = main cfg file did not exist; `FirstRunTools` = tools.cfg did not exist): every entry that existed in the old cfg is copied with `cfg.Migrate(entry, "<old cfg>", section, key)`; old files are only read, never changed or purged. Hotkeys whose default became `None` are copied only when the old value differed from the old default. KeyCode names are converted to InputSystem `Key` names (PlayerBody `KeyMap`). Old `[General] Apocasetter` and per-mod `[Debug] VerboseLog` are not migrated (WW has its own).
- **Assets** (shipped, not in git): `BepInEx\plugins\WildWasteland\<Id>\...`, theme in `BepInEx\plugins\WildWasteland\theme\`. Fallbacks when a file is missing: the old plugin folder (`plugins\Gunplay\Sounds\knifestab.wav`, `plugins\GunplayHUD\Sounds\headshot.mp3`, `plugins\NPCAI\Models\*.bundle`) or an embedded copy (HUD crosshair PNGs, Patrols default templates / editor defaults / merchant layouts).
- **Data folders**: `BepInEx\config\WildWasteland\<Id>\` (`Core.Assets.ModuleConfigDir`). Missing files are copied once from the old location on Init; reads keep falling back to the old location for at least one release.
- **Saves**: every save key and format is verbatim (decisions 2026-10-13). `Core.SaveTypes` maps saved component type names `Apocapatrol.X, Apocapatrol` / `WarRig.X, WarRig` (and the other four assemblies, unused today) to the new namespaces by prefix rename.

| Module | Old cfg(s) read on first run | Old data copied / still read | Save data |
|---|---|---|---|
| PlayerBody | `com.denis.apocalypter.apocaplayer.cfg` (incl. the 2.1.11-2.3.5 moved-key chains), `com.denis.apocalypter.apocaclimber.cfg`, `config\Apocaplayer\camera.cfg` | `config\Apocaplayer\{weapon-poses.txt, aim-lift.txt, ignition-key.txt, legacy-base-lines.txt, weapon-grips.cfg, BuiltinPoses.generated.cs}` -> `config\WildWasteland\PlayerBody\`; assets from `plugins\Apocaplayer\` must be copied by hand / release zip (Models\, Sounds\Female\) | none |
| NPC | `com.denis.apocalypter.npcai.cfg`, `config\NPCAI\hidden-settings.not-saved` | `config\NPCAI\Saves\*.senses.txt`, `config\NPCAI\nest-layouts.txt` -> `config\WildWasteland\NPC\` (reads fall back to `NPCAI\Saves`, then `Apocaraider\Saves`); dog bundles: `plugins\WildWasteland\NPC\Models\` else `plugins\NPCAI\Models\` | sidecar `<slot>.senses.txt` (format `v1`), unchanged |
| Guns | `com.denis.apocalypter.apocaunloader.cfg`, `com.denis.apocalypter.apocaraider.cfg` (only when no Gunplay cfg), `com.denis.apocalypter.gunplay.cfg` | `plugins\WildWasteland\Guns\Sounds\knifestab.wav` (fallback `plugins\Gunplay\Sounds\`) | ES3 key `Apocaunloader.Magazines` (`Apocaunloader.Magazines\|1`) in the slot file, timing verbatim |
| HUD | `com.denis.apocalypter.gunplayhud.cfg` (else `apocaraider.cfg`), `config\GunplayHUD\hidden-settings.not-saved`, old `TurnOffCrosshair` rule | `plugins\WildWasteland\HUD\Sounds\headshot.mp3` (fallback `plugins\GunplayHUD\Sounds\`); textures embedded | none |
| CleanHUD | `com.denis.apocalypter.cleanhud.cfg`; each absent WW setting migrates independently, including existing WW installs | `config\CleanHUD\{layout.json,game_keys.txt}` copied if missing into `config\WildWasteland\CleanHUD\`; textures and font atlas embedded | No new save data; menu settings keep native `SaveSettings.es3`, `Settings.xml` and PlayerPrefs behavior |
| Patrols | `com.denis.apocalypter.apocapatrol.cfg` (all sections incl. old moved keys, hidden/pose sections when present), `com.denis.apocalypter.warrig.cfg`, `com.denis.apocalypter.merchantspotter.cfg` | `plugins\Apocapatrol\{BaseTemplates,PlayerTemplates,OldTemplatesFolder,CarTemplates,Textures,Sounds}`, `PatrolsAndLoot.json`, `MerchantLayouts.json`, `BusPassengerPose.json(.bak)`, markers -> `plugins\WildWasteland\Patrols\`; `plugins\WarRig\{WarRig.glb, WarRig_colliders.glb, Defaults\}` -> `Patrols\WarRig\`; `plugins\Apocapatrol\theme\**` -> `WildWasteland\theme\`; sidecars `config\Apocapatrol\Saves\<slot>.bin(.bak)` and road cache `config\Apocapatrol.roads\` still read | ES3 key `Apocapatrol.Patrol` (schema 14, magic `APOC`) + sidecar `config\WildWasteland\Patrols\Saves\<slot>.bin`; ES3 prefab ids 735710202610050001 (motorcycle) / 735710202610090001 (War Rig) resolved through `Hooks.GetPrefab`; saved component types via `SaveTypes` |

## 2. Stay standalone (integrate, don't absorb)

| Mod | Why / how WW talks to it |
|---|---|
| Apocasetter | Settings/menu host (F6) and installer for every mod. WW soft-depends on it: `Core.ApocasetterBridge` (InputBlocker, GameFont) by reflection; `[General] Apocasetter = true` opts WW into the Mods menu. |
| Apocasaver | Save coordinator. **Apocasaver no longer sees Apocapatrol's GUID**, so its patrol participant adapters (sidecar tracking inside the save transaction, `CleanWorld` patrol graph, `CleanupAllowed` / `PatrolBusy` / `PatrolItem` patches) are inactive with WW; Patrols saves exactly as standalone Apocapatrol did without Apocasaver (ES3 key + sidecar ~1 s after `isPlay`), and the stale key of legacy saves is carried forward by Apocasaver's generic retention. Convoy spawn timing is still postponed around its autosaves by reflection on `Apocasaver.Plugin` / `Apocasaver.Runner` (`Convoy.AutosaveSoon`). Guns injects `Apocaunloader.Magazines` into the ES3 cache before Apocasaver's final flush (as before). Decide later whether WW registers an explicit participant. |
| ApocaLanguage | Translates IMGUI text of all mods. |
| ApocaFsmInspector, ModProfiler | Debug tools; never shipped inside WW. ModProfiler sees WW as one row (`WildWasteland.dll`); per-module split comes from the F7 Performance page. |
| Apocaspawner | Dev/cheat spawner. Patrols patches its catalog / spawn (War Rig entry) only when it is loaded. |

## 3. Third-party mods to stay compatible with

| Mod | GUID | What to do |
|---|---|---|
| ApocalypterAI | ApocalypterAI | Prefixes the same PlayMaker actions as the NPC module. Only one brain per body. |
| ApocalypterGarage | ApocalypterGarage | Spawn only through its functions; never add its objects to ArrayList_* yourself. |
| ApocaDustStorm | local.apocalypter.duststorm | Disables vanilla storms; its NPCAI bridge refuses NPCAI >= 1.8 (with WW it finds no NPCAI at all). Patrols' War Rig bridges its `StormShelter` / `PlayerStormHazards` by reflection; NPC's Storm.cs reads it. |
| Hazel Cars | com.hazel.apocalypter.cars | Check overlap with Patrols' vehicle templates. |
| FlyingKlick's Dog Companion | apocalypter.dogcompanion | NPC `DogCompanionCompat` patches its `Dog.*` methods only when loaded (`[NPC] DogCompanionCompat`). |
| MotorcycleMod | com.denis.apocalypter.motorcyclemod | Patrols patches `MotorcycleMod.Factory.Template()` only when loaded (rear-access prep). |
| Part Adjustment / Ultimate Tools | com.denis.apocalypter.partadjustment | Shares `GetButtonDown.OnUpdate`; keep WW's prefix cheap. |
| Apocapocket / Apocainventory | com.denis.apocalypter.apocapocket | PlayerBody's `EquipmentInventory` reads their slots by type name (compiled delegates). CleanHUD's optional Apocapocket 2.0.10 bridge reads six logical slots and held-item identity, preserves stable display during transactions/saves, and fits 3-6 entries inside its existing slot row. Ordinary item textures are borrowed from Apocapocket; late native widgets are hidden/restored and only CleanHUD-owned transforms are removed from native card shifting. Soft dependency; no new config or save data. Testing; see `notes/compat-Apocapocket-CleanHUD.md`. If ever absorbed, a Core inventory interface is needed. |

## 4. Remaining candidate source mods (snapshot of V:\Repos\Apocalypter, 2026-10-13)

| Mod (repo folder) | Plugin GUID | Version / HEAD | Status | Notes |
|---|---|---|---|---|
| womenofwasteland | com.denis.apocalypter.womenofwasteland | 1.0.0 / 21d05c6 | candidate | Split out of Apocaraider 1.7.0. |
| Apocaraider | com.denis.apocalypter.apocaraider | 1.7.0 / 7c1e3e9 | dropped | Superseded: monolith that gunplay/NPCAI/WoW were split from; soft-dep only so its cfg can be migrated. Running it beside WW doubles features (not in ConflictsWith: remove it by hand). |
| Apocapocket | com.denis.apocalypter.apocapocket | 2.0.10 / 41a5032 | candidate | Extra slots; owns save data (`apocasaver.pose.*` via Apocasaver HandPose). PlayerBody reads it by reflection today. |
| Apocaunloader | com.denis.apocalypter.apocaunloader | 1.3.0 / 7a215cb | candidate | Leftover: its mechanics are in Guns (via Gunplay 1.3.0); the standalone plugin is in Guns' ConflictsWith. Nothing left to port unless a newer standalone commit differs. |
| HealthStacks | com.denis.apocalypter.healthstacks | 1.0.0 / a326d8d | in (Testing, #87) | Always-on Core fix in `src/Core/HealthStacks.cs`; no settings, assets or new save data. Tops up Bandage / First Aid slots to 3 charges, leaving excess on the ground. Uses the existing GetButtonDown dispatcher; stands down if the standalone plugin is loaded. |
| Apocatremors | com.denis.apocalypter.apocatremors | 1.2.0 / 60f2bb2 | candidate | |
| CaveDarkness | com.denis.apocalypter.cavedarkness | 1.0.0 / 2eac97c | candidate | |
| ultimatetools | com.denis.apocalypter.partadjustment | 2.3.2 / e0abb4c | candidate | Already a merge (Part Adjustment + toolbox); kept the old GUID for save compatibility - absorbing it means a second plugin class (AGENTS.md 5.8 exception). |
| Apocaclimber | com.denis.apocalypter.apocaclimber | 1.0.2 / (no git) | candidate | Already absorbed: inside PlayerBody (Climbing/), via Apocaplayer 2.3.9. Listed in PlayerBody's ConflictsWith. |
| iTweenGuard | com.denis.apocalypter.itweenguard | 1.0.0 / d7f5411 | in (Core) | Always-on Core fix in `src/Core/ITweenGuard.cs`; no settings (the standalone `LogFixes` line is now Log.Info under VerboseLog, first 20 repairs). Prefix on `iTween.TweenUpdate`; stands down if the standalone plugin is loaded. |

### POI world discovery (#81)

Init order now ends with Patrols, POI. Patrols prepares its War Rig provider first and registers it when Services.Poi becomes available. POI has no scheduler jobs. Its F7 page is Patrols & vehicles → Points of interest; F6 [POI / World] Spawn chance (%) defaults to 2, with 100 for one site per newly generated 5 km main slice. MapMagic chooses seeded positions away from native POIs, flattens a 65 m pad, and clears collidable decoration there. A 500 m event-only player trigger starts the parked, engine-less Rustchief/tanker encounter with ten wheels at 5% condition and three Zombie_Runner NPCs scattered at varied angles/radii, two Lugnut_Dead corpses beside the cab, and 3–6 bandage/nuts/ammo items inside the tanker PhysicsLock area. Cargo uses the existing deferred registration, placement and lock-FSM settling path. Temporary yellow Xs mark generated sites on M.

Old WarRigPercent is no longer bound or used; existing cfg orphans are not purged. Ordinary Scrapwagon/trailer selections are untouched. Existing War Rig prefab id/class/save compatibility is retained by Patrols. POI spawned-site keys are saved as an atomic text sidecar under config/WildWasteland/POI/Saves/<slot>.poi.txt through SaveBus.AfterSave. Save/load must preserve the sidecar to avoid a duplicate site encounter.

War Rig retention (#81): native DeleteFarAwayItems skips tankers and their attached parts permanently. The abandoned POI truck and its parts are protected until the player drives it with an engine running and horizontal speed above 1 m/s. The existing saved SetupOnly flag records whether that release has happened; no save schema change. Patrols cleanup also honors this protection and detaches/preserves a tanker when an eligible towing truck is removed.

## Compass vehicle groups and nearby locations (2026-10-15, Testing)

CleanHUD exposes its marker API to Patrols through `Services.Compass` / `ICompassMarkers`, with no cross-module type references. A 0.5 s Patrols interval reconciles one marker per traffic journey or manual spawn event, including parked/manual merchants. Physical markers follow the group's truck/leader; virtual journeys use stable MapMagic coordinates. Existing registered lists are reused, and markers are removed on retirement, player capture, scene changes or module/toggle shutdown. Merchants take yellow precedence, all Coyotes (including convoys) are green, other patrols are red and convoys orange. All use the built-in car silhouette and existing cached distance rendering.

CleanHUD collects native location roots from `mapIcon` children in its existing 500 microsecond scene-discovery slice. Native MapMagic `TerrainTile.OnTileApplied` only queues the newly applied tile into that same slice; unsubscribe on Shutdown. No second whole-scene scan or new per-frame poll is introduced. A 0.5 s interval registers only location targets within 1 km; Marker.MaxDistance enforces the exact cutoff during normal compass drawing. Native map text height is avoided by targeting the location root, not the mapIcon child. Targets follow floating origin and are removed when destroyed.

Two player settings in settings.cfg, `CleanHUD / HUD` ShowConvoys and ShowLocations, default true. Both are exposed separately in SETTINGS → Game → HUD. ShowConvoys covers all four vehicle categories; neither depends on the debug-map setting. Public F6 settings and save formats are unchanged. Runtime/performance verification remains with Denis.
