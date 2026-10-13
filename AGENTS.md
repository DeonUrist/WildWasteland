# AGENTS.md - Wild Wasteland

Instructions for every AI agent and session working in this repo. Read all of it before changing anything. Keep it current: if you learn something that would have saved you time, add it here (or to the project doc `claude/GAME_KNOWLEDGE.md` if it is about the game, not this repo).

## 1. What this repo is

Wild Wasteland (WW) is one BepInEx 5 plugin for **Apocalypter** (Unity 2020.3.49f1, Mono, game logic in PlayMaker FSMs) that combines mechanics previously shipped as separate mods. The point of combining them is:

1. **They load together correctly**: one load order, one set of Harmony patches, one save/scene lifecycle, no feature running twice.
2. **They perform well together**: one frame budget shared by all mechanics, enforced by a scheduler and measured every build.

A port that works but breaks either goal is not done.

## 2. Read first

- Project doc **`claude/GAME_KNOWLEDGE.md`** (Claude project "Apocalypter Mods"): the game facts and gotchas. Its section 0 ("30 rules") is mandatory.
- Project doc `claude/<mod>-notes.md` for every source mod you port.
- `docs/modules.md` (which mechanics are in, from which commit), `docs/harmony.md` (patch inventory), `docs/decisions.md`, `docs/perf-log.md`, `verification/README.md`.

## 3. Ground rules (same as every Apocalypter repo)

- **Check HEAD first.** Several sessions work in parallel. Before editing: `git log --oneline | head`, take sources from the current HEAD, re-check right before committing.
- **Port, don't link.** Copy code from a source mod into `src/Modules/<Name>/` and record the source commit in `docs/modules.md`. Never edit a source mod's repo from a WW task, never reference its DLL.
- Commit only when Denis asks; never push or release unless asked. Commit messages end with the Co-Authored-By / Claude-Session lines.
- git on the device needs delete permission on `V:\Repos\Apocalypter` (lock files); request it before running git. Use `-c user.name=Denis -c user.email=...` (no global identity).
- Never `git reset`/`clean` the whole tree; Denis keeps uncommitted assets in repos.
- No secrets in the repo (tokens, PATs).

## 4. Identity

| Item | Value |
|---|---|
| Plugin GUID | `com.denis.apocalypter.wildwasteland` |
| Plugin name | `Wild Wasteland` |
| Assembly / DLL | `WildWasteland.dll` |
| Root namespace | `WildWasteland` |
| Harmony id | same as GUID, ONE Harmony instance |
| Install path | `BepInEx\plugins\WildWasteland\` (DLL + assets) |
| Config | `BepInEx\config\com.denis.apocalypter.wildwasteland.cfg` (+ `BepInEx\config\WildWasteland\` for data/sidecars) |
| Version | plain `System.Version` (`0.1.0`), never `0.1.0-beta` (BepInEx silently skips it). Release tag = this version. |

## 5. Architecture (how combined mechanics must be built)

### 5.1 Layout
```
src/Core/            Plugin entry, Runner, Scheduler, ModuleHost, GameRefs, Origin, SaveBus, SpawnBus, InputBus, Log, Config helpers
src/Modules/<Name>/  one folder per mechanic (namespace WildWasteland.Modules.<Name>)
src/Patches/         Harmony dispatchers, one class per patched target (optional; may live in Core)
docs/  verification/  tools/
```
Modules may depend on Core. Modules must not reference each other's internals; cross-module calls go through a small interface in Core or an event on a bus. Record any module-to-module dependency in `docs/modules.md`.

### 5.2 One entry point, one runner
- The `BaseUnityPlugin` only binds config, applies patches and creates the **Runner**. The game destroys plugin GameObjects on scene load, so `BaseUnityPlugin.Update/OnGUI` never run.
- Runner = one hidden GameObject (`HideAndDontSave` + `DontDestroyOnLoad`), recreated on `SceneManager.sceneLoaded` if gone. It owns the only `Update`, `LateUpdate`, `FixedUpdate`, `OnGUI` of the whole mod and forwards to modules through the scheduler. Modules never create their own MonoBehaviour update loops (exception: components that must sit on a specific game object, e.g. per-NPC brain; even those should be ticked centrally when possible).

### 5.3 Module contract
Each module implements a Core interface with at least:
- `Id`, `DisplayName`, `[Section] Enabled` config entry (default decided per module).
- `ConflictsWith` = GUIDs of the standalone mods it replaces (see 5.8).
- `Init()` (once, after config), `OnSceneLoaded()`, `OnWorldReady()` (SaveLoadGame reached `isPlay`), `OnSaveBegin()/OnSaveEnd()`, `OnOriginShift(Vector3 delta)`, `Shutdown()`.
- Work registered with the scheduler (5.4), not done in raw Update.
ModuleHost calls every hook inside its own try/catch, counts exceptions per module, logs the first few with stack, and **disables a module after repeated failures** (and says so in the log) instead of throwing every frame.

### 5.4 Scheduler and frame budget
- All periodic work is a scheduled job: `EveryFrame` (only for things that are visibly wrong otherwise, e.g. camera, held-item pose), `Interval(seconds)` with a random phase so jobs don't all fire on the same frame, or `Sliced` (a work queue processed for at most N microseconds per frame).
- Global budget (initial, see docs/perf-log.md): WW managed time avg <= 0.5 ms/frame, worst <= 2 ms. The scheduler measures each job with `Stopwatch` (cheap) and exposes per-module totals in the debug overlay; sliced jobs stop when the frame's budget is spent and continue next frame.
- Scale think intervals with load (precedent from NPCAI: 0.1 s for <= 5 agents, 0.2 s <= 10, 0.3 s <= 20, 0.5 s above).
- Heavy one-off work (grid bakes, prefab scans, texture decodes) is sliced (~1 ms/frame) or done at startup, never in one frame during play.

### 5.5 Shared services (write once in Core, use from every module)
- **GameRefs**: Player, PlayerCamera(Holder), `__GameManager__` FSMs (Menu), SaveLoadGame FSM, `NewGO_ArrayList` proxies (`ArrayList_Items/Cars/Sandstorms`), `itemNameID`. Re-found after every scene load; never `GameObject.Find`/`FindObjectsOfType` per frame or per spawn. `InGame` = Menu `play` && SaveLoadGame `isPlay` && timeScale > 0.
- **FSM lookup helpers**: iterate `GetComponents<PlayMakerFSM>()` and match `FsmName` (no `FindFsmOnGameObject`, it logs per miss); `Find*` variables (never `GetFsm*` as existence test); check `Fsm.Initialized`. Cache per instance id, sweep destroyed entries, clear on scene load.
- **Origin**: tracks `FloatingOrigin` shifts (1000 m) and raises `OnOriginShift(delta)`. Modules store long-lived positions relative to the MapMagic anchor, never raw world coordinates across frames.
- **SaveBus**: the only code that watches SaveLoadGame (`SaveGame`/`wait`/`isPlay`/`LoadGame`/`setSeed`). Raises save/load phases to modules, resolves the current slot from `ES3Settings.defaultSettings.path`, writes all WW sidecar data after the vanilla save finishes (~1 s after `isPlay`; ES3 cache rule), restores ~0.5 s after `isPlay` following LoadGame, cancels restores on new LoadGame/scene reset. One sidecar per slot: `BepInEx\config\WildWasteland\<SaveGameN>.json`, one section per module. Integrates with Apocasaver if present (soft).
- **SpawnBus**: one postfix on `CreateObject.OnEnter`, one post-load scan; modules subscribe by prefab name/tag instead of each scanning the scene. Also owns the save-compatible spawn recipe (Instantiate -> itemNameID++ -> `<prefab>(Clone)N` -> ArrayListAdd once -> optional `Randomize`) and clean removal (remove from every proxy, then Destroy).
- **InputBus**: one prefix on `GetButtonDown.OnUpdate` (and `GetMouseButtonDown.OnUpdate`) with a lookup table (fsm name + button) -> handler; one place that knows all WW hotkeys and checks conflicts. Hotkeys default to None or verified-free keys (game: F2 Heal, F9 hide UI, F11 god mode; our mods use F1, F3-F8, Insert).
- **Log**: `[Debug] VerboseLog` (default off) gates all info logging; no string building on hot paths when it is off. Warnings/errors always on, rate-limited.
- **Json**: use a hand-written serializer/MiniJson; Unity `JsonUtility` drops nested custom arrays.

### 5.6 Harmony rules
- ONE patch method per target per kind for all of WW, implemented as a dispatcher that calls subscribed modules. Two modules patching the same method is a review blocker.
- Every target is listed in `docs/harmony.md` with its subscribers, call frequency and early-out filter.
- PlayMaker action patches run for every FSM in the game: first line must be a cheap filter (reference compare of `Fsm`/owner against a cached set, or a static bool), no allocations, no string compares on unrelated FSMs.
- Patch each `[HarmonyPatch]` class separately (`CreateClassProcessor(t).Patch()` in try/catch), so one bad target disables only its dispatcher; report it in the startup summary.
- Prefer postfixes/observation over skipping originals. When skipping (`return false`), respect `eventTarget` on `SendEvent` and call `Finish()` where the action would have finished.

### 5.7 Config
- Sections: `[General]` (`Apocasetter = true` for the Mods menu, master switches), one section per module (`[<Module>] Enabled` first), `[Debug]` last.
- Bind order = display order in Apocasetter; bind `Enabled` first in each section.
- Only player-facing settings in the cfg; tuning constants and hidden state in a separate never-saved `ConfigFile` (`SaveOnConfigSet = false`).
- Migrations from source mods: read their old cfg files directly (BepInEx `ContainsKey` only sees bound entries), copy values, write once, log what was migrated. Purge orphaned entries via `ConfigFile.OrphanedEntries` (reflection).
- InvariantCulture for every float parse/format.

### 5.8 Coexistence with the standalone mods it replaces
BepInEx will happily load WW and the old standalone mod side by side (different GUIDs) and every feature then runs twice. At startup WW must:
1. For each module, check `Chainloader.PluginInfos` for the GUIDs in `ConflictsWith` (use `ReferenceEquals(info.Instance, null)`, plugin instances read as destroyed).
2. If found: keep that module OFF, log a clear warning naming the DLL to remove, and show it in the Apocasetter page.
3. Never do the reverse (patch or disable the other mod).
Exception: if a module keeps a source mod's GUID for save compatibility (precedent: Ultimate Tools kept Part Adjustment's GUID), that must be a separate plugin class and documented in `docs/decisions.md`.

### 5.9 Inter-mod APIs and soft dependencies
- Soft dependencies (Apocasetter, Apocaplayer ModAPI, ApocalypterGarage, Apocasaver): `[BepInDependency(guid, SoftDependency)]`, all calls to that mod in ONE file per dependency with `[MethodImpl(MethodImplOptions.NoInlining)]`, fields typed `object`, so WW loads without it. Verify with load test A.3.
- APIs that other mods use today (NPCAI.Api v1, Apocaplayer ModAPI, Apocapatrol hooks) must keep their public names/namespaces if WW absorbs the provider, or WW must ship a compatibility shim. Note each in `docs/modules.md`.
- Third-party: ApocalypterAI drives the same NPC actions as NPCAI (only one brain per body); ApocalypterGarage objects are spawned only through its API.

### 5.10 Saves
- Never add components to saved game objects; never `SetActive(false)` objects that must receive `SaveGame` (hide renderers/colliders instead).
- Prefer sidecar data to custom ES3 prefabs. A save made with WW should still load without WW; if a module cannot achieve that, record it in docs/decisions.md and the README.
- When absorbing a mod that owns save data, read its old sidecar/keys on first load (migration) and keep reading them for at least one release.

## 6. Performance rules (checked in review)

Banned in per-frame or per-spawn paths: `GameObject.Find*`, `FindObjectsOfType`, `Resources.FindObjectsOfTypeAll`, `GetComponentsInChildren` (cache instead), LINQ, string concatenation/format, boxing, lambdas capturing locals, `new` collections, `Camera.main` (cache), `Physics.RaycastAll` (use NonAlloc with reused buffers), per-instance material copies.

Required:
- Reused buffers/heaps; caches keyed by instance id, swept for destroyed objects, cleared on scene load.
- Every job registered with the scheduler has a name so ModProfiler and the WW overlay can attribute time.
- Before porting a module, profile the standalone mod (ModProfiler) on the scenarios in `verification/README.md` and record the baseline in `docs/perf-log.md`; the ported module must not be slower.
- When two modules do the same scan/raycast/lookup, move it to a Core service and share the result.

## 7. Build

No compiler on the device VM. Build in the cloud:
```
apt-get update && apt-get install -y mono-mcs
# stage <game>\Apocalypter_Data\Managed\*.dll and <game>\BepInEx\core\*.dll, then:
MANAGED=/path/Managed BEPCORE=/path/core sh build.sh /path/outdir
```
- `build.sh` compiles every `.cs` under `src/` (same set as the csproj glob). Old mcs: C# 7, no local functions, `lock` is a keyword, avoid type names `Path`, `Cursor`, methods named `System`. Compile only against the game's DLLs.
- Windows alternative: `dotnet build WildWasteland.csproj` (deploy is opt-in: `-p:Deploy=true`, game closed).
- If a reference DLL is missing in Managed\, remove it from both build.sh and the csproj.

## 8. Deploy and verify (every build)

1. Staging name must be unique per build: `WildWasteland_<ver><letter>.dll` (same-name transfers have delivered stale bytes).
2. Game must be closed (Task Manager; Steam re-focuses a lingering process). Copy to `BepInEx\plugins\WildWasteland\WildWasteland.dll`.
3. md5 the file on disk == build output.
4. After launch: `Loading [Wild Wasteland x.y.z]` in `BepInEx\LogOutput.log` and the WW startup summary (all modules, state, patches applied).
5. Run `verification/README.md` (at least A + the scenario touched by the change). Remove ApocaFsmInspector from plugins when done debugging.
6. Remove the standalone DLLs of absorbed mechanics from Denis's plugins only when he agrees.

## 9. Releases (only when Denis asks)

- Zip unpacks into `BepInEx\plugins` (`WildWasteland\WildWasteland.dll` + assets + README). Tag `v<version>` == `[BepInPlugin]` version. Zips go to `V:\Repos\Apocalypter\_releases\`.
- uploads.github.com is blocked from both shells: create the release, Denis attaches the zip (or uses Mod Publisher). Then register/update the entry in Apocasetter-Index (`mods/com.denis.apocalypter.wildwasteland.json`, with `replaces` listing the absorbed standalone GUIDs).
- README states which standalone mods it replaces and must be removed.

## 10. Definition of done for a ported mechanic

- [ ] Row in `docs/modules.md` filled (source commit, status `in`).
- [ ] Code under `src/Modules/<Name>/`, no own update loop, no own Harmony instance, no duplicate patch targets (`docs/harmony.md` updated).
- [ ] Config section with `Enabled`, migration from the old cfg, `ConflictsWith` set.
- [ ] Save data through SaveBus; old save data migrated.
- [ ] Verification A-C pass; perf scenarios recorded in `docs/perf-log.md`, not slower than the standalone baseline.
- [ ] Module disables cleanly at runtime (Enabled=false) without leaving patches active in an inconsistent state.
