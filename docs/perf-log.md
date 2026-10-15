# Performance log

One entry per build that changes runtime behaviour. Measure with ModProfiler (deep capture, 30 s) **and** the F7 Performance page on the scenarios in `verification/README.md`. Keep raw captures out of git (`verification/perf/raw/`); copy the summary numbers here.

Budget (initial targets, adjust in docs/decisions.md):
- WW managed self time: average <= 0.5 ms/frame, worst frame <= 2 ms, in every scenario.
- No GC allocations per frame in steady state (any allocation in a per-frame path is a bug).
- Load: WW work after a save load spread over >= 10 frames, no single frame > 8 ms.
- Sliced background work (nav bakes, scene scans) stops for the frame once `[Core] Frame budget (ms)` (tools.cfg, default 2 ms) is used.

### 0.1.26 / #133 vanilla optimization, first pass — 2026-10-15 — NOT YET MEASURED

New module `Performance` (notes/plan-Performance.md), 38 features on by default, preset Balanced, own F7 tab "Performance" with frame-time ring (avg/p95/p99, spikes tagged save/load/tile/cull), GC alloc/frame, draw calls, SetPass, shadow casters and a census file button. PlayMaker runtime: FsmList side index + targeted events, fast removal, broadcast without copies, PlayMakerGUI loop over GUI candidates only, unhandled trigger/visibility events skipped, one shared mouse-pick ray per frame, string-convert reuse, FSM sleep via `Fsm.ManualUpdate` (FS1), compiled FsmProperty/CallMethod/action loading. Game logic: sliced culler replacing the four __GameManager__/NewGO sweeps, car simulation LOD, exact poll-sleep for hinge/wait/option polls, vehicle reflection FSMs -> direct NWH, LightFlicker 20 Hz manager, HUD/DayTime throttles, Coyote broadcast filter, duplicate Rigidbody skip, capsule writes. Rendering: point-light shadow budget + distance cull, dead post-FX skipped, TAA(no blend) -> SMAA/FXAA, sun shadow caps, terrain detail, particles, small-caster shadows, cull-only LODGroups, camera layer cull distances, Low-only far clip/texture limit, NPC animator/IK culling. World: MapMagic thread/apply budget, CCD sweep, time/physics sleep, origin-jump scan sharing, Azure FixedUpdate throttles, GC slice. Built and compiled only (agents could not run Unity; FS2-FS4 conversions unit-checked on mono against vanilla). Denis: measure Vanilla preset + all features Off vs default on the verification scenarios; run FS1, PM2, PM7, FS2, PM10 in Shadow first and expect 0 mismatches.

## What the scheduler measures (F7 -> Debug & performance -> Performance)

### 0.1.13 RC10 / #127 Engine Sim — 2026-10-15 — NOT YET MEASURED

Engine Sim 1.7.0's original Update/FixedUpdate bodies run as named scheduler jobs. Installation/scene-boundary seeds plus native vehicle/sound OnEnable events replace repeated global discovery. Vehicle subtrees and the initial scene listener walk use 500 us slices; inactive sounds are included and destroyed registry entries are pruned twice a second. No recurring whole-scene walk runs during play. Source scan filtering/sorting, part checks, session setup, native simulation threads, DSP audio and vehicle-part physics retain their costs and timing. Newly enabled vehicle discovery adds a slice delay before the next original 0.5 s scan; initial resource and individual scene/component queries cannot be preempted. Existing vehicle hints/status are reused; only switch state changes rebuild hint text. Native worker CPU is outside WW scheduler counters. Engine Sim was not installed locally; no runtime capture or speedup is claimed. Denis compares identical saves/engine count/quality in ModProfiler plus WW Performance, with Engine Sim verbose off because it also triggers a full FSM dump. Full steps: notes/compat-EngineSim.md.

### Scheduler measurements

`Core.Scheduler` times every job with one `Stopwatch` (ticks -> ms), per phase (EarlyUpdate, Update, LateUpdate, LateUpdateLast, EarlyFixedUpdate, FixedUpdate, Gui):
- per job: average ms/frame (`TotalMs / Frames`), last call ms, worst call ms, calls, exceptions, "(off)" when error isolation switched it off;
- per module: the sum of its jobs' average ms/frame and its worst single job call;
- whole mod: `Frames` measured, average and worst WW frame (`FrameMs` = sum of all job times of that frame, reset in EarlyUpdate);
- "Reset statistics" restarts the counters (do it after the scenario started, read after 30 s).

Not measured by the scheduler (shows up only in ModProfiler as part of the `WildWasteland.dll` row): Harmony handler bodies (the Hooks dispatchers and module patches), per-object MonoBehaviours that keep their own Unity loops (Patrols `Crew`, `Pilot`, `Rider*`, `Pose`, `SuspensionTune`, `MotorcycleBalance`, `TrafficActor`, `WarRigUnit`, `MerchantRuntime`; PlayerBody `OcclusionCutaway`), coroutines, `Camera.onPreCull` (ThirdPerson), and `Core.Frame` itself (EditorSession / SaveBus / Hotkeys ticks: listed as job `Core.Frame`). Numbers read while F7 is open are from the paused game: close it, play, reopen.

ModProfiler attributes by assembly: in the live HUD all of WW is one row (`WildWasteland.dll`, Runner Update/LateUpdate/FixedUpdate/OnGUI + every coroutine `MoveNext` + every patch body). A **Focus** capture on any WW method instruments every method of the assembly and gives per-method rows (jobs are named static/instance methods, not lambdas, for this reason). ModProfiler's own prefix+finalizer on every WW patch body adds overhead on the hot PlayMaker dispatchers: measure with and without it.

## 0.1.6 RC3 compatibility build - 2026-10-15 - NOT YET MEASURED

Optional QuestsCompat routes Talk/Quests Update, LateUpdate and GUI through named scheduler jobs, separates Quests Tick into a 0.5 s job, and replaces its save watcher with SaveBus. Item/people candidate discovery is a 500 us sliced job; the original queries still read live quantities/health/positions. Typed delegates replace compatibility hot-path reflection. CleanHUD dialogue rebuilds on source content/choice changes; map lookup runs once per second while markers exist, and projection runs only while the native map is open. Native global input/death hooks and source voice/spawn work remain unchanged. The scheduler measures source method duration too; it cannot preempt an individual source callback. No baseline or measured speedup is claimed. Denis should compare five active quests, takeover walkers and an armed hired passenger, following notes/compat-Talk-Quests.md.

## 0.1.0 (build a) - 2026-10-13 - NOT YET MEASURED

No game in the cloud: nothing below is a measurement. The five ports compiled (`build.sh` 0 errors) and passed the integration review and the config harness; the first in-game run (Denis) fills the table.

### Per-frame work reduced during the port (from the port reports, sections "Performance changes")

| Module | Removed / reduced per frame or per spawn | Unchanged by design |
|---|---|---|
| PlayerBody | `EquipmentInventory.Read` (every frame): Apocapocket/Apocainventory bridge compiled once into typed delegates (no `FieldInfo.GetValue` / `Invoke` / boxing per frame); `Equipment.Position` long-gun bounds cached per visual (was `GetComponentsInChildren<MeshFilter>` + 16 transforms per gun per frame); `Car.UpdateHints` string rebuilt only when one of 10 ints changes (was StringBuilder + string every frame in a car); `GunPose.OnGUI` one cached GUIStyle; `Game.Velocity` Rigidbody cached; `Game.Find` from `GameRefs` (no `GameObject.Find`); `PickAssist` `Camera.main` -> `GameRefs.MainCamera`; `GetButtonDown` prefix per-FSM classification cache (was up to 3 string compares on every GetButtonDown of the game every frame); `FirstPersonSway` per-action cache + compiled setters (was `FieldInfo.SetValue` x3 per sway object per frame); `RustlinerDoors` nearby overlap every 1 s + whole-scene scan every 10 s (was whole scene every 1 s); `IgnitionControl` 0.5 s Interval job (was every frame) | Body LateUpdate pose work, ModAPI characters, ThirdPerson PreCull / crosshair, cutaway renders, climbing (visibly wrong otherwise) |
| NPC | `DogAnimations.Tick` reused dead-list buffer (was `new List<int>()` per frame); `DogBite.Tick` target collider cached; `Camera.main` -> `GameRefs.MainCamera` in Brain, Idle, Nests, Burrow, Webs, Senses, DogAnimations, DebugOverlay; `GameObject.Find("Player" / "__GameManager__" / "NewGO_ArrayList" / "MapMagic" / "SaveLoadGame")` -> GameRefs / Origin; the per-frame SaveLoadGame `ActiveStateName` poll gone (SaveBus); button prefixes: one keyboard read per frame instead of one per GetButton action; static filters ahead of string compares on SendEvent, Rotate / LookAt / SmoothLookAt / Raycast / AddForce (no brain registered), Fsm.ProcessEvent / Wait / SetVelocity (no dog); debug overlay job disabled unless ShowNavigation | Idle.Drive per-frame capsule fan, Dijkstra / PickOutExit spikes (unsliced), `AvoidFriends` O(n), eager log-string building at some `Senses.Log` sites, `VanillaEvents.BeforePlayerShot` allocations per player shot (behaviour-sensitive, flagged in notes/NPCAI.md 10) |
| Guns | `Integration.Ensure` AppDomain / GetType loop every 5 s gone; `Tracers.Tick` fitted-wheel scan: per-child cached `Attack` FSM array (was `GetComponents<PlayMakerFSM>()` per weapon child per frame), `HitDistance.Instance` comparer (was `Comparer.Create(lambda)` per swing), `RemoveWhere` static predicate, `GameRefs` for camera / rig; IntClamp / FloatClamp prefixes: classification cached per action (no `count.Name + "_capacity"`, no `GameObject.name` per call), closures moved out of the hot path (the old lambdas allocated a closure on EVERY IntClamp / FloatClamp call game-wide), `Idle()` fast path; EnterState x2 / FsmState.OnEnter / ActivateGameObject name-length gates; CreateObject postfix `prefab.name` cached per prefab id; `PerGunAmmo.Tick` SaveLoadGame `FindObjectsOfType` per second -> GameRefs / SaveBus, `AdoptDrawn` searches the `WeaponsArm` subtree instead of the whole scene; `Unloader`, `BackpackOverflow.Tick`, `AmmoBox.Drop`: `GameObject.Find` / `FindFsmOnGameObject` / LINQ -> GameRefs / FsmUtil / loops; Bosses `GameObject.Find("Player")` -> GameRefs, tick from a 0.5 s job | `FindEffects` / `AmmoBox.FindPrefab` `Resources.FindObjectsOfTypeAll` on first use, `Info()` `GetComponentsInChildren` retries for gunless monsters, `HealthOf` first lookup, per-hit `GetComponents`, BossUI `FindObjectsOfType` scans (4 bursts after scene load, then 60 s) |
| HUD | SetFsmFloat filter: one static bool for the common case (was 3 ConfigEntry reads + 2 string compares per call); past the filter one reused `List` (was `GetComponents<PlayMakerFSM>()` arrays); crosshair: 0.25 s Interval job writing only on change (was every frame) and `Scene.GetRootGameObjects(List)` search at most once per second while missing; headshot audio: no per-frame work (edge-triggered Refresh; was 2 config reads + `Stop()` per frame while off); floaters use `GameRefs.MainCamera`. Expected idle cost well under 0.01 ms/frame | - |
| Patrols | No SaveLoadGame poll (SaveBus), no `GameObject.Find` retry in `InGame` (GameRefs); `Camera.main` -> `GameRefs.MainCamera` in Pose, Convoy, Patrol, TurretSpotter, BusPose, MerchantEditor, PatrolVisibility; `GameObject.Find("Player" / "PlayerCameraHolder" / "__GameManager__" / "itemNameID" / "NewGO_ArrayList")` -> GameRefs; hot hooks start with a static / reference filter: SetPosition (`SuspensionTune.Live`), CreateObject War Rig (template reference), SendEvent / SetFsmFloat / Tornado / Explosion storm handlers (`WarRigUnit.All.Count`; before: string compares on every SendEvent / SetFsmFloat in the game), ArrayListGetRandom (per-FSM cache), QuietFsm (static bool); War Rig + Apocapatrol share one GetPrefab / CreateObject dispatcher instead of two patches each; `PatrolVisibility` static cache instead of a component per car | No algorithmic change; `SeatedLosGuard` still runs for every targetless LOS action; `TrackDust` keeps its string filter (per FSM enable); first-run `Textures\` copy is synchronous at startup (once) |
| Core | Shared `GameRefs` (one 1 s lookup while missing, cleared on scene load), `MainCamera` per frame, `SaveBus` one `ActiveStateName` read per frame for all modules, `Hotkeys` one poll per bound key per frame (EarlyUpdate), dispatchers patched only when subscribed | `Origin.Anchor` does `GameObject.Find("MapMagic")` at most once per second while missing |

### Procedure for the first measurement (Denis)

Prerequisites: WW 0.1.0 build a deployed (md5 checked), the nine standalone DLLs in `BepInEx\disabled-by-WildWasteland\`, ModProfiler installed (`[Keys] ToggleWindow` set to a free key, F7 is WW's now), `[Debug] VerboseLog = false`, `[NPC / Debug] ShowNavigation = false`, `[Patrols / Debug] AiOverlay = false`, DebugMap off.

For each scenario S1-S5:
1. Load the save / reach the situation, wait 10 s for the scene to settle.
2. F7 -> Debug & performance -> Performance -> **Reset statistics**, close F7. Play the scenario 30 s. Open F7 again, read: WW average / worst frame, the per-module rows, the three worst jobs (avg ms/frame), any "ERR" or "(off)" job. Write them into the table.
3. Same scenario with ModProfiler: open its window, 30 s deep capture, then a **Focus** capture on `WildWasteland.Core.Runner.Update` (gives per-method rows inside WW). Note the `WildWasteland.dll` row: main-thread ms/frame, max frame ms, GC alloc/frame (the realtime dump F9 -> `BepInEx\config\ModProfiler\Realtime\*-live.json`; copy the file to `verification/perf/raw/0.1.0a-S<n>.json`, not into git).
4. Baseline for "vs. standalone mods": the same scenario and save with WW's module(s) off and the standalone DLLs moved back to `plugins\` (Apocaplayer + NPCAI + Gunplay + GunplayHUD + Apocapatrol + WarRig); sum their ModProfiler rows. The pre-port captures in `BepInEx\config\ModProfiler\Realtime\2026093*-live.json` / `Captures\` are the only existing standalone numbers (NPCAI + Apocapatrol era; no Gunplay baseline exists).
5. Watch `LogOutput.log` during every scenario for `job '...' threw ... - job DISABLED` and `errors in 10 s - module DISABLED`: those are bugs, not perf.

Scenarios (verification/README.md D): S1 idle open world on foot, S2 camp fight with 4+ NPCs (NPC + Guns + HUD + PlayerBody busy), S3 driving across a tile boundary with a floating-origin jump (Patrols traffic, PlayerBody in car), S4 load a save with patrols + motorcycle + War Rig (spread of the post-load work; no frame > 8 ms), S5 convoy / faction battle with many vehicles (Patrols crews + NPC bandits).

| Scenario | WW avg ms (F7) | WW worst ms (F7) | Worst module (avg) | Top 3 jobs (avg ms) | ModProfiler avg / max ms | GC alloc/frame | vs. standalone mods | Jobs off / errors |
|---|---|---|---|---|---|---|---|---|
| S1 idle open world | | | | | | | | |
| S2 camp fight, 4+ NPCs | | | | | | | | |
| S3 driving through a tile boundary + origin shift | | | | | | | | |
| S4 load save (patrols + bike + War Rig) | | | | | | | | |
| S5 convoy / many vehicles | | | | | | | | |

Pass: every row within the budget and not slower than the standalone sum. Fail: note the job / method here, fix, new build letter, measure again.

## 0.1.3 - CleanHUD integration - 2026-10-14 - measurement pending

Source CleanHUD 1.5.1 `119a2f8`; no standalone capture was available in the local ModProfiler config directory. This is a code audit, not a performance result. Agents cannot launch or profile the game under AGENTS.md.

Named jobs: CleanHUD.HUD and CleanHUD.Menus (LateUpdateLast); HUD attachment (1 s), Layout reload (2 s), Scene key discovery and Weapon icons (500 us cooperative slices). Heavy DLL key scanning runs once in a worker without a main-thread join. HUD number parsing/formatting is cached; weapon descriptions resolve on item changes; root paths, menu CanvasGroups, MODS button, HUD graphics, slot FSMs and Quantity variables are cached. Native icons/font assets preload at module startup. Icon mesh/crop work is sliced and completed textures are cached. Native Unity rendering/readback/upload operations cannot be preempted by the scheduler: measure the first modded icon separately. Canvas batching and the willRenderCanvases callback require ModProfiler as well as F7 counters.

Denis: compare standalone CleanHUD and the port in the same save, 30 s per scenario. Reset F7 Modules/Performance counters, close F7 during capture, reopen to read jobs. Whole-WW targets remain <=0.5 ms average / <=2 ms worst, zero steady-state allocations and no disabled jobs/errors. Leave status Testing until measured.

| Scenario | Standalone CleanHUD avg/max | Integrated CleanHUD avg/max | Whole WW avg/worst | GC allocation | Result |
|---|---|---|---|---|---|
| Idle, rotate compass, Needs/Numbers enabled | pending | pending | pending | pending | Testing |
| Fire/reload/switch weapons/heal/message log | pending | pending | pending | pending | Testing |
| Driving/origin shift | pending | pending | pending | pending | Testing |
| Load save, return to title, runtime off/on | pending | pending | pending | pending | Testing |
| Convoy combat | pending | pending | pending | pending | Testing |
| First modded weapon icon and cached revisit | pending | pending | pending | pending | Testing |

## 0.1.3 - CleanHUD vehicle status - 2026-10-15 - measurement pending

PlayerBody delegates its existing vehicle status to CleanHUD through Services.VehicleStatus. Three vector graphics are built with the HUD and reused; no new textures, scene scans, jobs or per-frame strings. Volume text/width is updated only when its cached value changes. The group follows Insanity's anchor/scale, packing left of the visible icon or against the right edge when Insanity is hidden. Status expires when PlayerBody stops submitting it (exit, disabled hints or module off), and hides while paused. The legacy display is used when CleanHUD is off/unavailable. Runtime/performance verification remains Denis's task.

## 0.1.3 - persistent CleanHUD loading - 2026-10-15 - measurement pending

Loading UI previously belonged to the scene-bound title menu and its `_starting` flag was reset on scene changes; the game-scene native loading text then became visible. A separate persistent canvas now observes SaveBus and survives that transition until isPlay/native loading completion. One named job, CleanHUD.Loading screen, checks cached native references and updates the animated label only twice per second. Numbered PNGs decode at startup or when changed before the next load; no image decoding/file reads during ordinary play. Tip text is wrapped once per selected image. Unchanged textures and UI are reused, released on module shutdown. YES/NO confirmation text now uses centered label geometry. No new BepInEx entries; the user-requested LoadingScreens/screens.json holds editable tips. Denis verifies new game, Continue, load slot, slow terrain generation, return to title, JSON edits and quit confirmation alignment.

## 0.1.3 - CleanHUD autosave presentation - 2026-10-15 - measurement pending

Apocasaver displays a dynamic clone of native LoadingScreen on a sorting-31000 canvas. The normal-loading observer never saw that clone, and the replacement's sorting 30000 was below it. Two optional reflected presentation postfixes now start/end the existing replacement, with sorting 32000 and cached AUTOSAVING labels. SaveBus completion cannot close the autosave view before Apocasaver finishes disk finalization/cleanup. No extra job or scene scan; exceptions in the display handlers are contained and save logic is untouched. Denis verifies a scheduled autosave/countdown and resume, then a normal load; logs should show Autosave loading screen hooks ready and Autosaving ScreenN.png between native autosave start/finish.

## Template

### x.y.z (build letter) - date
| Scenario | WW avg ms (F7) | WW worst ms (F7) | Worst module (avg) | Top 3 jobs (avg ms) | ModProfiler avg / max ms | GC alloc/frame | vs. standalone mods | Jobs off / errors |
|---|---|---|---|---|---|---|---|---|
| S1 idle open world | | | | | | | | |
| S2 camp fight, 4+ NPCs | | | | | | | | |
| S3 driving through a tile boundary + origin shift | | | | | | | | |
| S4 load save | | | | | | | | |
| S5 convoy / many vehicles | | | | | | | | |

## 0.1.4 RC1 — 2026-10-15

Reviewed 691 bound installed config entries; ten defaults changed, ToggleIronsights explicitly remains false. All current local source changes built in Windows Release with Deploy=true: 0 errors, 7 existing warnings. Installed/build/package DLL MD5 8C700DED83EE607C870BEF46172DC2D3. Defaults introduce no jobs or allocations; CleanHUD runtime profiling and in-game verification remain pending with Denis. Release candidate does not promote feature statuses.

## Compass vehicle/location markers — 2026-10-15

Static audit: two named 0.5 s reconciliation jobs; existing registered traffic/merchant/car lists only. Native mapIcon discovery shares the existing 500 microsecond SceneCache slice; tile-apply callback queues work rather than scanning synchronously. Only locations within 1 km are registered for per-frame compass drawing, whose existing icon/distance pools are reused. No new MonoBehaviour Update loop, resource scan, texture decode, save entry or Harmony patch. Two player config entries (ShowConvoys / ShowLocations), both true. Windows Release build with Deploy=true passed with 0 errors (7 existing warnings); installed/build DLL MD5 4564EBED5E35C24BDED034F98D34E23A. Denis must verify colors, duplicate suppression, retirement/off-on/load behavior and profiler timings in game.

## 0.1.5 RC2 — 2026-10-15

Release contains the compass vehicle-group and native location feeds documented above. Version-only rebuild in Windows Release with Deploy=true passed with 0 errors, 7 existing warnings. Build/installed DLL MD5 E5977630DF9C1D676157FABF3DDC7245. Two new player entries ShowConvoys and ShowLocations default true. Verification and runtime timings remain with Denis; project statuses stay Testing.

## 2026-10-15 - NPC / Women of Wasteland (Testing)
No standalone or integrated in-game measurements were taken: local agents must not run the game. Denis must capture the same camp/crew/save-load scenes before declaring the port performance verified. Original repeated global renderer scans are replaced by a 500 us scheduler scene walk plus NPC registration and spawn queues; component/mesh/material/voice caches are reused. Default models, textures and native skeleton layouts warm at startup; default voice loudness prepares during startup loading. Pending voice initialization uses cached component arrays and processes one NPC per 0.25 s interval. First-use mesh layouts, Unity texture/mesh uploads, audio creation and per-NPC voice FSM edits are indivisible work and still need ModProfiler measurements. Performance acceptance remains WW avg <= 0.5 ms/frame, worst <= 2 ms; no measured claim is made.

## Perf pass 2026-10-15 (on 0.1.21 RC18, unversioned build) - NOT YET MEASURED

Static audit `notes/perf-audit-2026-10-15.md`, implemented across all modules (77 files). Mono build 0 errors (2 known CS0675). Nav exit picks/move masks checked with `tools/nav-equivalence` (equivalent, ties/shared-field classes only). No config entries added; no defaults changed (ScaleWithActors stays false).
- CleanHUD: native `MapCheat_Camera` enabled only while the map is shown (was rendering its 1024² RT every frame); compass markers in their own container (no per-frame SetAsLastSibling), nested canvases on the HUD root/compass, fades via CanvasRenderer alpha, distance text 5 Hz with cached strings, compare-before-write layout, duplicate vanilla hide removed. POI 500 m trigger -> 1 Hz proximity job.
- NPC: Brain/Idle/Senses think and look phases keep their phase (`next += interval`) with a random phase on (re)engage and per-frame caps; Senses 1 s maintenance round-robin over 8 frames; SeesNow cached 0.1 s; WeaponOf from cached hand bones; per-shot allocations removed; `_pending` spawn queue no longer fills up. Nav: PickOutExit sliced (1 ms/frame job), per-cell move bitmasks (Build ~2.4x faster), LRU field cache, origin shift keeps grid fields, discovery sweep 15 s while the player is still; Idle feeler fan 10 Hz, distance-scaled idle thinks; Women discovery event-driven + 60 s safety pass; dog reach memo; Passthrough round-robin.
- Patrols: camp/POI scan time-budgeted (300 us), regex gated, deep rescan on tile apply / 60 s; PassengerGuard verdict cached 0.25 s; GroundRecovery 10 Hz; typed NWH input; War Rig gate 4 Hz; RemoveTree single-pass compaction; Prefabs.Exact memo; TrafficMap ink only on change; LINQ/alloc removal in WorldTraffic/Riders/Crew.
- QuestsCompat: see docs/harmony.md "Talk/Quests performance patches"; Quests OnGUI Repaint-only; Tick split into 4 jobs.
- PlayerBody/Guns/Core: per-frame string/array garbage removed in the weapon pose path; Equipment inventory 10 Hz + on change; aim-convergence ray every other frame; bullets simulated at a fixed 60 Hz with interpolated drawing; IMGUI moved to a GuiRunner enabled only while a Gui job's predicate (or F7) wants it. Perf line (VerboseLog) adds GC alloc/frame and MapCheat_Camera state.

Denis: compare S1-S5 against 0.1.21 RC18 (F7 Performance + Perf line), especially S2 camp fight worst ms (Brain/Idle/Nav/Senses) and the GC count per window.

Released for testing as 0.1.22 RC19 with the HEADLIGHTS and raised-sights wheel-zoom fixes. Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` passed with zero errors and eight warnings. Built/deployed/packaged DLL MD5: `FB582EC2684915B084C4F299E6CC6559`. No in-game or performance measurements were taken for this release.

