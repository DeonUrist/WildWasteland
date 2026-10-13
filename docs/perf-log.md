# Performance log

One entry per build that changes runtime behaviour. Measure with ModProfiler (deep capture, 30 s) **and** the F7 Performance page on the scenarios in `verification/README.md`. Keep raw captures out of git (`verification/perf/raw/`); copy the summary numbers here.

Budget (initial targets, adjust in docs/decisions.md):
- WW managed self time: average <= 0.5 ms/frame, worst frame <= 2 ms, in every scenario.
- No GC allocations per frame in steady state (any allocation in a per-frame path is a bug).
- Load: WW work after a save load spread over >= 10 frames, no single frame > 8 ms.
- Sliced background work (nav bakes, scene scans) stops for the frame once `[Core] Frame budget (ms)` (tools.cfg, default 2 ms) is used.

## What the scheduler measures (F7 -> Debug & performance -> Performance)

`Core.Scheduler` times every job with one `Stopwatch` (ticks -> ms), per phase (EarlyUpdate, Update, LateUpdate, LateUpdateLast, EarlyFixedUpdate, FixedUpdate, Gui):
- per job: average ms/frame (`TotalMs / Frames`), last call ms, worst call ms, calls, exceptions, "(off)" when error isolation switched it off;
- per module: the sum of its jobs' average ms/frame and its worst single job call;
- whole mod: `Frames` measured, average and worst WW frame (`FrameMs` = sum of all job times of that frame, reset in EarlyUpdate);
- "Reset statistics" restarts the counters (do it after the scenario started, read after 30 s).

Not measured by the scheduler (shows up only in ModProfiler as part of the `WildWasteland.dll` row): Harmony handler bodies (the Hooks dispatchers and module patches), per-object MonoBehaviours that keep their own Unity loops (Patrols `Crew`, `Pilot`, `Rider*`, `Pose`, `SuspensionTune`, `MotorcycleBalance`, `TrafficActor`, `WarRigUnit`, `MerchantRuntime`; PlayerBody `OcclusionCutaway`, `AutoStepUp.Contacts`), coroutines, `Camera.onPreCull` (ThirdPerson), and `Core.Frame` itself (EditorSession / SaveBus / Hotkeys ticks: listed as job `Core.Frame`). Numbers read while F7 is open are from the paused game: close it, play, reopen.

ModProfiler attributes by assembly: in the live HUD all of WW is one row (`WildWasteland.dll`, Runner Update/LateUpdate/FixedUpdate/OnGUI + every coroutine `MoveNext` + every patch body). A **Focus** capture on any WW method instruments every method of the assembly and gives per-method rows (jobs are named static/instance methods, not lambdas, for this reason). ModProfiler's own prefix+finalizer on every WW patch body adds overhead on the hot PlayMaker dispatchers: measure with and without it.

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

## Template

### x.y.z (build letter) - date
| Scenario | WW avg ms (F7) | WW worst ms (F7) | Worst module (avg) | Top 3 jobs (avg ms) | ModProfiler avg / max ms | GC alloc/frame | vs. standalone mods | Jobs off / errors |
|---|---|---|---|---|---|---|---|---|
| S1 idle open world | | | | | | | | |
| S2 camp fight, 4+ NPCs | | | | | | | | |
| S3 driving through a tile boundary + origin shift | | | | | | | | |
| S4 load save | | | | | | | | |
| S5 convoy / many vehicles | | | | | | | | |
