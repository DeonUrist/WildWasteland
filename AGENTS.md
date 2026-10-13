# AGENTS.md - Wild Wasteland

Instructions for every AI agent and session working in this repo. Read all of it before changing anything. Keep it current: if you learn something that would have saved you time, add it here (or to the project doc `claude/GAME_KNOWLEDGE.md` if it is about the game, not this repo).

## 0. Fixing issues: the default task (read this before anything else)

From 0.1.0 on, almost every task is "fix issue #N" from the board (https://github.com/users/DeonUrist/projects/4). These rules override anything below that suggests otherwise.

### 0.1 Scope: change only what the issue needs
- One issue per task. Find the module the issue belongs to (`[Module]` in the title) and keep the change inside the files that fix it, normally one module folder.
- Do **not** touch `src/Core/`, another module, `build.sh`, `WildWasteland.csproj`, `docs/` (except the rows your fix changes) or the wiki unless the fix cannot work without it. If it must, stop and ask Denis first, saying which file and why.
- No refactors, renames, reformatting, comment rewrites, "cleanups", perf tweaks, dependency or language-version changes, and no "while I'm here" fixes. Keep the existing code style even when it looks odd (the ported code is dense on purpose and matches its source mod). A fix that grows beyond about 50 changed lines or into a second module: stop and ask.
- Never rename classes, namespaces, GameObject/anchor names, save keys or prefab ids (save compatibility, 5.1 and 5.10).

### 0.2 Find the cause before changing code
- Read the issue and its comments, the code path, and Denis's log `E:\SteamLibrary\steamapps\common\Apocalypter\BepInEx\LogOutput.log` if you can reach it.
- If the cause is not proven by code + log, do not guess: ask Denis for a log with the right switches on (F6 → Wild Wasteland → Debug → `VerboseLog`; module log switches in F7, e.g. NPC AI & navigation → Debug → `BrainLog`), and say exactly what to do in game to reproduce.
- One fix per round. If Denis reports that a fix did not help, revert it before trying the next idea; never stack guesses (lesson from #63: three rounds, the first two aimed at the wrong cause).
- Use the mechanism the codebase already uses for the same situation. No workarounds that hide a symptom. If the only fix is a workaround or changes another module's behaviour, describe the options and let Denis choose.

### 0.3 Testing: build only, Denis tests in game
- You cannot run the game. Do not try to launch, automate or screenshot it, and do not write test harnesses, mocks, test projects or simulations for a fix.
- Your whole verification is: `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` in a **Windows** shell (PowerShell / cmd) compiles with **zero errors** (section 7.1). Then stop; do not loop on further checks. If you changed config entries, say so in the handover (the mono config harness is run by Claude cloud sessions, not by you).
- **On Denis's PC never use WSL, mono, `mcs`, `build.sh`, `tools/build-module.sh` or `tools/config-harness`, and never install compilers or SDKs.** Those are for Claude cloud sessions only (section 7.2). If `dotnet` is missing or the build cannot find the game DLLs, stop and tell Denis.
- After a successful build, **deploy the DLL to the game** (0.4) so Denis can test right away.
- Hand over to Denis in your final message: (1) the cause, (2) what you changed (files, about how many lines), (3) whether the DLL is deployed (md5) or why not, (4) exact in-game test steps with the expected result, (5) log lines that confirm it if any. Denis tests and reports back.
- Do not close issues and do not move them to Verified; Denis does that. Leave the status as Testing.

### 0.4 Git and deploy
- Commit only when Denis asks; the message starts with the issue (`Fix #N: ...`) and ends with your attribution lines. Never push, tag or release unless asked.
- **Deploy every successful fix build** without being asked: copy the built `WildWasteland.dll` to `E:\SteamLibrary\steamapps\common\Apocalypter\BepInEx\plugins\WildWasteland\WildWasteland.dll` (or `<GameDir>\BepInEx\plugins\WildWasteland\`), overwrite only that one file (never assets, configs or other plugins), and report the md5 of the copied file. With the csproj: `dotnet build WildWasteland.csproj -c Release -p:Deploy=true` does the copy. If the copy fails because the DLL is locked, the game is running: tell Denis to close it, do not retry or kill the game.

## 1. What this repo is

Wild Wasteland (WW) is one BepInEx 5 plugin for **Apocalypter** (Unity 2020.3.49f1, Mono, game logic in PlayMaker FSMs) that combines mechanics previously shipped as separate mods. The point of combining them is:

1. **They load together correctly**: one load order, one set of Harmony patches, one save/scene lifecycle, no feature running twice.
2. **They perform well together**: one frame budget shared by all mechanics, enforced by a scheduler and measured every build.

A port that works but breaks either goal is not done.

State at 0.1.0: five modules are in (PlayerBody, NPC, Guns, HUD, Patrols - see `docs/modules.md`), compiled and reviewed, **not yet verified in the game**. `docs/PORTING.md` is the recipe the ports followed and the recipe for the next one.

## 2. Read first

- Project doc **`claude/GAME_KNOWLEDGE.md`** (Claude project "Apocalypter Mods"): the game facts and gotchas. Its section 0 ("30 rules") is mandatory.
- Project doc `claude/<mod>-notes.md` for every source mod you port.
- `docs/modules.md` (which mechanics are in, from which commit, services, migrations), `docs/harmony.md` (patch inventory), `docs/decisions.md`, `docs/perf-log.md`, `docs/PORTING.md`, `docs/WIKI-FORMAT.md`, `verification/README.md`.
- The port reports and the integration review of 0.1.0 (`notes/port-<Id>.md`, `notes/review-integration.md` in the working folder of the porting sessions) explain why the code looks the way it does.

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
| Root namespace | `WildWasteland` (Core: `WildWasteland.Core`, modules: `WildWasteland.Modules.<Id>`) |
| Harmony ids | created only by `Core.Patches`: the GUID for Core and the shared dispatchers, `<GUID>.<ModuleId>` per module (runtime unpatch). Never `new Harmony` elsewhere. |
| Install path | `BepInEx\plugins\WildWasteland\` (DLL + `theme\` + `<ModuleId>\` assets) |
| Config | `BepInEx\config\com.denis.apocalypter.wildwasteland.cfg` (F6, player-facing), `BepInEx\config\WildWasteland\tools.cfg` (F7, technical), `BepInEx\config\WildWasteland\<ModuleId>\` for data/sidecars |
| Version | plain `System.Version` (`0.1.0`), never `0.1.0-beta` (BepInEx silently skips it). Release tag = this version. `Plugin.VERSION` is the single source. |

## 5. Architecture (how combined mechanics must be built)

### 5.1 Layout
```
src/Core/            Plugin, Runner, Scheduler, ModuleHost, ModuleBase/IModule, ModuleConfig (+Legacy), Hooks, Patches, Services,
                     GameRefs (+FsmUtil), Origin, SaveBus, SaveTypes, Hotkeys, Assets, Log, ApocasetterBridge, UI/ (ToolWindow, ToolPage,
                     CorePages, EditorSession, Skin, Ledger)
src/Modules/<Id>/    one folder per module (namespace WildWasteland.Modules.<Id>); <Id>Module.cs = entry; <Id>Pages.cs = F7 pages
src/Modules/<Id>/<Framework>/   subfolders = the logical frameworks of the module (NPC/Detection, Navigation, Brain, Animals, Hazards;
                     Patrols/Spawning, Vehicles, Crews, Traffic, Merchants, Editor, Persistence, WarRig ...). Folders do NOT change the namespace.
src/Modules/<Id>/Embedded/      files embedded as resources (logical name <Id>.<path with / -> .>)
docs/  verification/  tools/
```
- **Modules keep the original class names and nesting of the source mod** (decisions 2026-10-13): `Core.SaveTypes` resolves saved component types by a pure namespace-prefix rename (`Apocapatrol.X, Apocapatrol` -> `WildWasteland.Modules.Patrols.X`). One namespace per module; sub-namespaces only where documented in `docs/modules.md` (`Patrols.Motorcycles`, `Patrols.WarRig`).
- Modules may depend on Core. Modules must not reference each other's types; cross-module calls go through `Core.Services` (5.5). Record any module-to-module dependency in `docs/modules.md` 1.3.

### 5.2 One entry point, one runner
- `Plugin.Awake` only: `Assets.Init`, `Skin.Init`, first-run flags, binds Core entries, `RegisterModules()` (order = init order = F6 order: PlayerBody, NPC, Guns, HUD, Patrols), `ModuleHost.BindAll`, `Runner.Ensure`, Core jobs, `SaveTypes.Install`, `ToolWindow.Init`, `ModuleHost.StartAll`, logs the startup summary. The game destroys plugin GameObjects on scene load, so `BaseUnityPlugin.Update/OnGUI` never run.
- Runner = `WildWasteland.Runner` GameObject (`HideAndDontSave` + `DontDestroyOnLoad`) with three MonoBehaviours (`EarlyRunner` order -1000, `Runner`, `LateRunner` order 20000) that call `Scheduler.Run(phase)`; `Runner.OnGUI` also draws the F7 window; `OnApplicationQuit` -> `ModuleHost.Quit`. Recreated on `SceneManager.sceneLoaded` if gone. Modules never create their own MonoBehaviour update loops (exception: components that must sit on a specific game object, e.g. per-NPC brain, per-car crew; they gate on the module's `Running` flag and die on Shutdown).

### 5.3 Module contract (src/Core/ModuleBase.cs, as implemented)
`public sealed class <X>Module : ModuleBase` with:
- `Id` (stable, = config section prefix, Harmony id suffix, asset folder), `DisplayName`, `ConflictsWith` (standalone GUIDs, 5.8), `DefaultEnabled` (true).
- `BindConfig(ModuleConfig cfg)`: bind EVERY entry (5.7). Called for every module, enabled or not, before any Init, in module order; Core binds `[<Id>] Enabled` first and sets `Config`.
- `Init()`: once, only when enabled and no conflicting plugin is loaded: patches (`Core.Patches`), hooks (`Core.Hooks`), jobs (`Scheduler`), hotkeys (`Core.Hotkeys`), services, assets. **Must be re-runnable after Shutdown** (runtime off -> on from F6/F7, retry after a failed Init): reset statics at the start of Init or in Shutdown; AssetBundles stay loaded across off/on.
- `Shutdown()`: undo game-state side effects (restore materials/renderers/camera/FSM variables, destroy module-added components and objects, stop coroutines), set the provided `Services.X` back to null, `Running = false` first so re-entrant handlers no-op. Core then removes jobs, hooks, hotkeys and patches (`ModuleHost.Cleanup`). Must be safe after a partial Init.
- `OnSceneLoaded(scene, mode)`, `OnWorldReady()` (first `isPlay` after LoadGame / setSeed / new scene), `OnSaveBegin(slot)` (`SaveGame`), `OnSaveEnd(slot)` (back to `isPlay`; sidecar writes go through `SaveBus.AfterSave`), `OnLoadBegin(slot)` (`LoadGame`), `OnOriginShift(delta)`, `ToolPages()` (F7 pages, 5.11). All virtual, all called by `ModuleHost` inside try/catch.
- `Active` (set by Core while running), `Config`, `Dir` (= `BepInEx\plugins\WildWasteland\<Id>\`).
- ModuleHost states: `NotStarted, Running, OffByConfig, OffConflict, InitFailed, DisabledAfterErrors`. `ModuleHost.Restart(m)` for tools that change Init-time settings. The startup summary (`ModuleHost.Summary()`) lists every module with state and patch count plus `core hooks: ...`.

### 5.4 Scheduler and frame budget (src/Core/Scheduler.cs)
- API: `Scheduler.EveryFrame(this, "Name", Tick, Phase)`, `Interval(this, "Name", seconds, Tick, Phase, scaledTime)` (random initial phase so jobs spread over frames; `RunNow()` forces the next tick), `Sliced(this, "Name", micros, Step)` (step while it returns true, within the job's microseconds and the whole-frame budget), `Gui(this, "Name", Draw)`, `StartCoroutine / StopCoroutine` (on the Core runner). Phases: `EarlyUpdate` (= old `[DefaultExecutionOrder(-1000)]` runners), `Update`, `LateUpdate`, `LateUpdateLast`, `EarlyFixedUpdate`, `FixedUpdate`, `Gui`. Jobs run in registration order within a phase (= module init order, preserving each source mod's internal order). Name jobs as `<Id>.<Thing>`; tick methods must be named methods, not lambdas (ModProfiler Focus captures show method names).
- `EveryFrame` only for things that are visibly wrong otherwise (camera, held-item pose, projectiles); interval-gated work becomes `Interval`; heavy one-off work (grid bakes, prefab scans, texture decodes) is `Sliced` (~1 ms/frame) or done at startup, never in one frame during play. Scale think intervals with load (NPCAI precedent: 0.1 s for <= 5 agents, 0.2 s <= 10, 0.3 s <= 20, 0.5 s above).
- Budget: WW managed time avg <= 0.5 ms/frame, worst <= 2 ms (docs/perf-log.md). Every job is timed with a Stopwatch; `FrameMs` is the frame's total; sliced jobs stop once `FrameMs > FrameBudgetMs` (`[Core] Frame budget (ms)` in tools.cfg, default 2). F7 -> Performance shows per job and per module avg/last/worst ms, calls, errors.
- **Job isolation**: a job that throws 20 times in 10 s is switched off (`Enabled = false`, logged once, "(off)" on the Performance page); the module keeps running. Job errors are counted per module (first 5 logged with stack) but never disable the module. Hook-handler and lifecycle-hook errors count towards the module rule (40 in 10 s -> `DisabledAfterErrors`). Off -> on re-registers all jobs.

### 5.5 Shared services (src/Core, write once, use from every module)
- **GameRefs**: `Player`, `PlayerCameraHolder`, `GameManager`, `Menu` FSM, `SaveLoadGame` FSM, `NewGOArrayList`, `ItemNameId`, `MainCamera` (per frame), `InGame` (Menu `play` && SaveLoadGame `isPlay` && timeScale > 0), `InPlay` (same, ignoring timeScale - for tools while paused), `SaveLoadState`. Looked up at most once per second while missing, cleared on scene load. `FsmUtil.Find(go, fsmName)` iterates `GetComponents<PlayMakerFSM>` (no `FindFsmOnGameObject`: it logs per miss). Never `GameObject.Find` / `FindObjectsOfType` per frame or per spawn in modules.
- **Origin**: patches `FloatingOrigin.Jump` (always) and raises `IModule.OnOriginShift(delta)` (delta = old - new TotalOffset; add it to cached scene positions). `Origin.Anchor` / `World()` / `Scene()` convert to MapMagic-relative positions for long-lived data. NWH moves scene root objects itself, not DontDestroyOnLoad objects.
- **SaveBus**: the only watcher of SaveLoadGame (reads `ActiveStateName` once per frame; no patch). Raises `StateChanged(prev, now)` (for ported watchers) and the module hooks `OnSaveBegin` / `OnSaveEnd` / `OnLoadBegin` / `OnWorldReady`; `SaveBus.AfterSave(action, delay)` runs ~1 s after the save returned to `isPlay` (ES3 cache rule); `Slot` (`SaveGameN.es3` from `ES3Settings.defaultSettings.path`), `State`, `Saving`, `Loading`, `Busy`. Module save formats are kept verbatim (decisions): the planned per-slot JSON sidecar is not used by any 0.1.0 module. Apocasaver coexistence rules: notes/HostMods.md 2.6 (observe, never patch its ES3 targets with conflicting semantics).
- **SaveTypes**: prefix on `ES3Reflection.GetType(string)` mapping `<OldAsm>, <OldNs>.X` -> `WildWasteland.Modules.<Id>.X` (aliases WarRig, Apocapatrol, Apocaplayer, NPCAI, Gunplay, GunplayHUD; longer prefixes first; leaves the lookup to ES3 when the old assembly is still loaded). `Resolved` count; verbose log per mapped type.
- **Services** (src/Core/Services.cs): `Services.Animation` (`IAnimationApi`/`IAnimatedCharacter`, PlayerBody), `Services.Npc` (`INpcApi` + `Structures`, NPC), `Services.Guns` (`IGunsApi`, Guns), `Services.Hud` (`IHitFeedback`, HUD); events `CrewBailedOut`, `VehicleExploded` (raised by Patrols via `RaiseCrewBailedOut` / `RaiseVehicleExploded`, handlers wrapped in try/catch). Provider sets the field in Init and nulls it in Shutdown (`is` check so a module never clears another's instance); consumers null-check at every call site and fall back exactly like the old "provider mod not installed" path. New cross-module needs = a new interface member here, never a type reference.
- **Hotkeys**: `Hotkeys.Register(this, entry, action, when, allowWhilePaused)` for `ConfigEntry<Key>` entries (InputSystem `Key`, the only type Apocasetter rebinds); polled once per frame in EarlyUpdate (`Core.Frame` job); nothing fires while Apocasetter's InputBlocker is active, the window is unfocused, or timeScale == 0 (unless `allowWhilePaused`). `CheckConflicts()` logs duplicate keys and game keys (F2 heal, F9 hide UI, F11 god mode) after Init. `Pressed(key)` / `Held(key)` helpers. Context-polled keys (editors' numpad) stay inside the editor's tick. The PlayMaker button hooks (`GetButtonDown.OnUpdate` etc.) are Hooks dispatchers, not Hotkeys.
- **Assets**: `Assets.Root` (plugin folder), `ModuleDir(id)` = `plugins\WildWasteland\<Id>\`, `ConfigDir` = `config\WildWasteland\`, `ModuleConfigDir(id)` (created), `LegacyPluginDir("<OldFolder>")` for one-time data migration. Embedded resources: `src/Modules/<Id>/Embedded/...` -> `<Id>.<path>` (build.sh / csproj).
- **Log**: `Log.Info` only when `[Debug] VerboseLog` (`Log.Verbose`) - check it before building strings on hot paths; `Log.Always` for startup summary / migrations / state changes; `Warn` / `Error` always, rate-limited to 5 repeats per message (reset on scene load). Prefix messages with `[<Id>] `.
- **ApocasetterBridge**: the only file that talks to Apocasetter (reflection): `InputBlocked`, `SetInputBlock(bool)`, `GameFont`, `Present`. Other host-mod bridges (Apocasaver, Dog Companion, ApocaDustStorm, MotorcycleMod, Apocaspawner, Apocapocket) live in one file each inside the module that needs them, `object`-typed, `NoInlining`.
- **UI**: `Core.UI.EditorSession` (one modal owner for the whole mod: pauses, frees the cursor, blocks input via Apocasetter, releases one frame after close), `Skin` (= Apocapatrol's PatrolSkin; theme in `plugins\WildWasteland\theme\`), `Ledger` / `LedgerDropdown` (editor widgets), `ToolWindow` / `ToolPage` (5.11).
- **Json**: use a hand-written serializer/MiniJson; Unity `JsonUtility` drops nested custom arrays.

### 5.6 Harmony rules (src/Core/Hooks.cs, src/Core/Patches.cs)
- **Shared targets go through `Core.Hooks`**: one `[HarmonyPatch]` forwarder per target+kind (`P_<Target>` classes at the bottom of Hooks.cs), subscribed with `Hooks.<Target>.Prefix(this, handler)` / `Postfix` / `Finalizer` / `Resolver` in Init; `Hooks.RemoveAll(module)` on Shutdown; `Hooks.ApplyUsed()` patches a dispatcher only when it has a subscriber. Semantics = HarmonyX with separate plugins: all prefixes run in module init order, the original is skipped when any returned false, postfixes/finalizers always run; owners are tracked in arrays parallel to the handlers so a module removed mid-dispatch still reports to the right owner. Current dispatchers: `docs/harmony.md` section 1. Adding one: a `Hook<T>` field + `P_` class + lines in `RemoveAll`, `ApplyUsed`, `Summary`.
- **Module-only targets**: `Core.Patches.Patch(this, original, prefix:, postfix:, finalizer:)` or `Patches.ApplyClass(this, typeof(PatchClass))` (each `[HarmonyPatch]` class separately, try/catch, recorded; a failure disables only that target and shows as `patches N (M failed)` in the summary). Harmony id = `<GUID>.<Id>`; `Patches.Unpatch(module)` on Shutdown. Two modules patching the same target+kind directly is detected (`CONFLICT` in the log and summary) and is a review blocker: move it to a Hooks dispatcher. Never `new Harmony`, never `PatchAll`.
- Every target is listed in `docs/harmony.md` with its subscribers in order, kind, call frequency, early-out filter and known other patchers. The F7 "Harmony patches" page shows the live list.
- PlayMaker action patches run for every FSM in the game: first line must be a cheap filter (static bool, reference compare of `Fsm`/owner against a cached set, length gate before a string compare, dictionary lookup), no allocations, no string building on unrelated FSMs. Read private action fields with `AccessTools.FieldRefAccess` / `Traverse` set up once in Init.
- Prefer postfixes/observation over skipping originals. When skipping (`return false`), respect `eventTarget` on `SendEvent` and call `Finish()` where the action would have finished (and only when `!everyFrame` where that applies).
- Apocasetter's InputBlocker prefixes every PlayMaker input action: WW's input prefixes still run under it (all prefixes run), so they early-out on `ApocasetterBridge.InputBlocked` / `timeScale <= 0` where it matters.

### 5.7 Config (src/Core/ModuleConfig.cs): F6 vs F7
- **Main cfg (F6, Apocasetter)**: `[General] Apocasetter = true` (Core), `[Keys]` (every hotkey of every module, `cfg.Hotkey(key, Key.X, desc)`), then per module `[<Id>] Enabled` (bound by Core first) + `cfg.Player(sub, key, def, desc[, AcceptableValueRange/List])` into `[<Id>]` or `[<Id> / <Sub>]`, `[Debug] VerboseLog` last (Core). Bind order = display order; feature toggles first, in feature order; **one on/off toggle per player-visible feature**; target <= ~25 F6 entries per module; `AcceptableValueRange` for sliders, enum / `AcceptableValueList` for dropdowns; descriptions plain text ("restart" in a description = Apocasetter's NEEDS A RESTART chip); sections with "debug" in the name fold; > 40 entries per section collapse.
- **tools.cfg (F7)**: `cfg.Tool(sub, key, def, desc[, range])` -> `BepInEx\config\WildWasteland\tools.cfg`, sections `<Id> / <Sub>`: tuning constants, offsets, poses, debug switches, editor toggles, developer options, the old mods' hidden never-saved settings (same keys and defaults). Invisible to Apocasetter; edited live on F7 `ConfigPage`s. No never-saved `ConfigFile`s any more.
- **Migration**: `cfg.Migrate(entry, "<old cfg file>", "<old section>", "<old key>")` right after binding; applies only on the first WW run (`Legacy.FirstRunMain` = main cfg missing at startup, `FirstRunTools` = tools.cfg missing), reads the old file raw (`Core.Legacy.Get`; BepInEx `ContainsKey` only sees bound entries), logs every migrated value, never writes the old files. Chains of moved keys: oldest first, newest wins. KeyCode -> Key names: PlayerBody `KeyMap` (Core request: move it to ModuleConfig).
- Use `SettingChanged` for anything that must react live (F6 and F7 write entries live, Apocasetter forces `Save()`); handlers check the module's `Running` flag. InvariantCulture for every float parse/format. Never purge orphans / `Config.Clear()` / re-order the shared files (`SettingsLayout`, `PurgeStaleEntries`, `OrphanedEntries` hacks of the old mods are gone).
- `tools/config-harness` binds every module's config on plain mono and catches type mismatches, (section,key) collisions, foreign sections, BindConfig exceptions and migration errors before the first in-game run (section 7).

### 5.8 Coexistence with the standalone mods it replaces
BepInEx will happily load WW and the old standalone mod side by side (different GUIDs) and every feature then runs twice. At startup `ModuleHost.StartAll`:
1. For each module, checks `Chainloader.PluginInfos` for the GUIDs in `ConflictsWith` (`!ReferenceEquals(info.Instance, null)`; plugin instances read as destroyed). `Plugin` has a soft `BepInDependency` on every such GUID so they load first.
2. If found: the module stays `OffConflict`, the log names the DLL to remove, the F7 Modules page and the startup summary show it (Apocasetter shows it once the index entry lists the GUID in `replaces`).
3. Never does the reverse (patch or disable the other mod).
Exception: if a module keeps a source mod's GUID for save compatibility (precedent: Ultimate Tools kept Part Adjustment's GUID), that must be a separate plugin class and documented in `docs/decisions.md`. Never the Apocapatrol / Apocapocket GUIDs (Apocasaver binds private members by name on them).

### 5.9 Inter-mod APIs and soft dependencies
- Soft dependencies (Apocasetter, Apocasaver, Dog Companion, ApocaDustStorm, MotorcycleMod, Apocaspawner, PartAdjustment, Apocapocket): `[BepInDependency(guid, SoftDependency)]` on `Plugin`, all calls to that mod in ONE file per dependency with `[MethodImpl(MethodImplOptions.NoInlining)]`, fields typed `object`, so WW loads without it. Verify with load test A.3.
- APIs that other mods used (NPCAI.Api v1, Apocaplayer ModAPI, Gunplay.Api, GunplayHUD.Api, Apocapatrol hooks) are replaced by `Core.Services` for the absorbed consumers; third-party consumers need a shim that WW does not ship yet (documented gap, `docs/modules.md` 1.4).
- Third-party: ApocalypterAI drives the same NPC actions as the NPC module (only one brain per body); ApocalypterGarage objects are spawned only through its API.

### 5.10 Saves
- Never add components to saved game objects; never `SetActive(false)` objects that must receive `SaveGame` (hide renderers/colliders instead).
- Module save data keeps the source mod's keys and formats verbatim (decisions 2026-10-13): ES3 keys `Apocapatrol.Patrol`, `Apocaunloader.Magazines`, binary / text sidecars under `config\WildWasteland\<Id>\Saves\`, ES3 prefab ids resolved through `Hooks.GetPrefab`, component types through `SaveTypes`. Old sidecar locations stay readable for at least one release.
- A save made with WW loads without WW as well as a save made with the standalone mods loaded without them (mod-created objects - motorcycles, War Rigs, crews - cannot be resolved by the game alone; documented in README).
- New modules that own save data: prefer sidecars; ES3 keys only with a prefix and following notes/HostMods.md 2.6 (Apocasaver's transaction).

### 5.11 F7 tool window (src/Core/UI/ToolWindow.cs, ToolPage.cs, CorePages.cs)
- One window for the whole mod, key `[Keys] Tool window` (F7, Core's), drawn by the Core runner's OnGUI with the ledger skin at `GUI.depth -900`, pauses the game through `EditorSession`, Esc / CLOSE closes. Left column = groups (`ToolGroups.Order`: Wild Wasteland, Player body & animation, NPC AI & navigation, Weapons & HUD, Patrols & vehicles, Seat & turret poses, Merchants, Debug & performance), pages in registration order inside a group. Core pages: Modules (state + Enabled toggles), Performance, Harmony patches, Core settings.
- **Adding a page**: return it from the module's `ToolPages()` (collected once when F7 first opens; `Owner` is set to the module). Use `new ConfigPage(this, group, title, Config.Tools, "Id / Section", "Id / Prefix*") { Intro = ... }` for lists of tool settings, `new ActionPage(this, group, title, DrawMethod)` for live values and buttons, or a `ToolPage` subclass with `OnShow` / `Update` (per frame while shown) / `Draw(ToolLayout l)`. Layout helpers: `l.Heading`, `Text`, `Value(label, value)`, `Button`, `Buttons(...)`, `Toggle(entry)`, `Slider`, `Entry(ConfigEntryBase)`, `Space`. A page of a module that is not running shows a note and is read-only unless `ShowWhenModuleOff` (ConfigPages stay editable). Buttons that need the world use `GameRefs.InPlay` (the game is paused while F7 is open).
- A page that opens another modal editor calls `ToolWindow.CloseThen(() => editor.Open())`. Live in-world editors (numpad editors) keep running after F7 closes; their page shows the on/off toggle, the key list, the current selection and SAVE / RESET buttons.

## 6. Performance rules (checked in review)

Banned in per-frame or per-spawn paths: `GameObject.Find*`, `FindObjectsOfType`, `Resources.FindObjectsOfTypeAll`, `GetComponentsInChildren` (cache instead), LINQ, string concatenation/format, boxing, lambdas capturing locals (they allocate a closure on every call, even on the early-out path), `new` collections, `Camera.main` (use `GameRefs.MainCamera`), `Physics.RaycastAll` (use NonAlloc with reused buffers), per-instance material copies, reflection `GetValue`/`Invoke` (compile delegates once).

Required:
- Reused buffers/heaps; caches keyed by instance id, swept for destroyed objects, cleared on scene load.
- Every job registered with the scheduler has a name and a named tick method so ModProfiler and the F7 Performance page can attribute time.
- Before porting a module, profile the standalone mod (ModProfiler) on the scenarios in `verification/README.md` and record the baseline in `docs/perf-log.md`; the ported module must not be slower. Preserving behaviour beats a perf gain: apply only local, safe fixes (notes section 10 of each mod).
- When two modules do the same scan/raycast/lookup, move it to a Core service and share the result.

## 7. Build

### 7.1 Denis's PC (Codex, Visual Studio, any local agent): dotnet in Windows only

```
dotnet build WildWasteland.csproj -c Release -p:Deploy=true                 # build + copy the DLL into the game (game closed)
dotnet build WildWasteland.csproj -c Release -p:GameDir="D:\other\Apocalypter"   # if the game is elsewhere
```
- Run it from PowerShell or cmd in the repo folder (`V:\Repos\Apocalypter\WildWasteland`). **Not from WSL**: WSL has neither the Windows .NET SDK nor the game path, and must not be used for this repo.
- Needs the .NET SDK 6 or newer on Windows; nothing else (it compiles against the game's own `mscorlib`/`System` from `Apocalypter_Data\Managed` via `NoStdLib` + `FrameworkPathOverride`, so no .NET Framework targeting pack and no NuGet). Same sources, references and embedded-resource names as build.sh (verified identical, 33 resources). Output `bin\Release\WildWasteland.dll`; `-p:Deploy=true` copies it to `<GameDir>\BepInEx\plugins\WildWasteland\`.
- If the build fails on a reference, the game folder is wrong: fix `GameDir`, never the code or the csproj.
- `LangVersion 7.3`: the C# limits listed in 7.2 apply.

### 7.2 Claude cloud sessions only: mono mcs 6.8 (never on Denis's PC)

Used by Claude in its Linux cloud container (game DLLs staged there):
```
apt-get update && apt-get install -y mono-mcs mono-runtime
# stage <game>\Apocalypter_Data\Managed\*.dll and <game>\BepInEx\core\*.dll, then:
MANAGED=/path/Managed BEPCORE=/path/core sh build.sh /path/outdir            # -> outdir/WildWasteland.dll
MANAGED=... BEPCORE=... sh tools/build-module.sh <Id> [outdir]               # Core + one module, the other four stubbed
MANAGED=... BEPCORE=... sh tools/config-harness/run.sh /path/outdir [BepInEx/config with the old cfg files]
```
- `build.sh` compiles every `.cs` under `src/` (same set as the csproj glob) against the game's DLLs (the reference list is in build.sh; if a DLL is missing in Managed\, remove it from both build.sh and the csproj) and embeds every file under `src/Modules/<Id>/Embedded/` as resource `<Id>.<path with / -> .>` (e.g. `Patrols.DefaultTemplates.Bike.json`, `HUD.Textures.crosshair_dot.png`). `-langversion:latest` = C# 7.x: no local functions, no `out var` / pattern variables inside iterator methods, no locals shadowing a lambda parameter, no tuples / `default` literal / `in` params; `?.`, `=>`, `nameof`, interpolation are fine; `lock` is a keyword; avoid type names `Path`, `Cursor`, methods named `System`. Must build with zero errors (2 known warnings: NPC `Nav.cs` CS0675).
- `tools/build-module.sh <Id>` copies Core + one module into a temp tree with empty `ModuleBase` stubs for the others - the way to check a port in isolation.
- `tools/config-harness/run.sh` compiles `Harness.cs` against the built DLL and runs `BindConfig` of every module (alone and through `ModuleHost.BindAll`) on plain mono: catches AcceptableValue type mismatches, duplicate / colliding (section,key) pairs, statics of one module that share one entry (BepInEx returns the existing entry on a duplicate Bind of the same type), foreign sections, BindConfig exceptions and migration errors against Denis's real old cfg files. Color entries log "could not be parsed" there (native ColorUtility): ignore.

## 8. Deploy and verify (every build)

1. Staging name must be unique per build: `WildWasteland_<ver><letter>.dll` (same-name transfers have delivered stale bytes).
2. Game must be closed (Task Manager; Steam re-focuses a lingering process). Copy to `BepInEx\plugins\WildWasteland\WildWasteland.dll`.
3. md5 the file on disk == build output.
4. Assets: `BepInEx\plugins\WildWasteland\theme\` (shared skin) and `<Id>\` folders (PlayerBody `Models\`, `Sounds\Female\`; NPC `Models\*.bundle`; Guns `Sounds\knifestab.wav`; HUD `Sounds\headshot.mp3`, optional `Textures\`; Patrols templates / textures / sounds / layouts / `WarRig\*.glb`). Not in git: take them from the old plugin folders or the release zip. Patrols copies its own on the first run; the others fall back to the old folders with a warning.
5. The absorbed standalone plugins must not be in `BepInEx\plugins` (WW keeps the matching module OFF while one is loaded). On Denis's machine they were disabled through Apocasetter (`BepInEx\cache\Apocasetter\disabled\`); their cfg / data files stay in `BepInEx\config\` for the first-run migration.
6. After launch: `Loading [Wild Wasteland x.y.z]` in `BepInEx\LogOutput.log`, then `[<Id>] started (N patches)` per module and the summary `Wild Wasteland x.y.z modules:` (every module `on`, `core hooks: ...` without `(NOT PATCHED)`, no `PATCH CONFLICTS`).
7. In-game verification (`verification/README.md`, at least A + the scenario touched by the change) is done by **Denis**, never by an agent (section 0.3): give him the steps. Remove ApocaFsmInspector from plugins when done debugging.

## 9. Releases (only when Denis asks)

- Zip unpacks into `BepInEx\plugins` (`WildWasteland\WildWasteland.dll` + `theme\` + `<Id>\` assets + README). Tag `v<version>` == `[BepInPlugin]` version. Zips go to `V:\Repos\Apocalypter\_releases\`.
- uploads.github.com is blocked from both shells: create the release, Denis attaches the zip (or uses Mod Publisher). Only when Denis declares a build verified (not for 0.1.0, see docs/decisions.md), register/update the entry in Apocasetter-Index (`mods/com.denis.apocalypter.wildwasteland.json`, `pluginFolder: "WildWasteland"`, `replaces` listing the absorbed standalone GUIDs, `optional` the host mods); later mark the absorbed mods `deprecated` with `replacedBy`.
- README states which standalone mods it replaces and must be removed.
- Wiki: update the feature pages' Status, run `tools/wiki-index.py`, push the wiki repo (section 11).

## 10. Definition of done for a ported mechanic (porting only; for issue fixes see section 0)

- [ ] Row in `docs/modules.md` filled (source commit, status `in`, services, F6/F7, migrations).
- [ ] Code under `src/Modules/<Name>/`, original class names kept, no own update loop, no own Harmony instance, no duplicate patch targets (`docs/harmony.md` updated).
- [ ] Config section with `Enabled`, one toggle per feature, migration from the old cfg, `ConflictsWith` set, harness passes.
- [ ] Save data keys/formats verbatim; old sidecars/keys migrated or still read.
- [ ] Denis ran verification A-C in game; perf scenarios recorded in `docs/perf-log.md`, not slower than the standalone baseline.
- [ ] Module disables cleanly at runtime (Enabled=false) and starts again (Init re-runnable).
- [ ] Port report written (`notes/port-<Id>.md`), wiki pages under `Modules/<Id>/` with metadata tables, `tools/wiki-index.py` run.

## 11. Wiki

- Source: the separate repo `V:\Repos\Apocalypter\WildWastelandWiki` (exact spelling), pushed as the GitHub wiki of this repo. Format: `docs/WIKI-FORMAT.md`. One page per feature with the metadata table (Module / Framework / Status / F6 toggle / F7 tools / Source / Since) as the first table after the title; module overview pages `Modules/<Id>/<Id>-Module.md`; hand-written `Guides/` and `Architecture/`; `Backlog-Candidates.md` for the remaining candidate mods.
- GitHub wikis flatten folders: every file name must be unique across the wiki and links are `[[Page-Name]]` or `[text](Page-Name)` (no folders, no `.md`).
- `python3 tools/wiki-index.py <wiki folder>` (no dependencies) parses the metadata tables and regenerates `Home.md`, `_Sidebar.md`, `Feature-Status.md` and `features.json` (the board export). Never edit those four by hand. Statuses: Backlog / Working on / Testing / Verified / Done (= the GitHub project columns). Run it after every wiki change; it fails on an unknown status or a duplicate file name.
