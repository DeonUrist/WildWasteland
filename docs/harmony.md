# Harmony patch inventory (0.1.0)

Every patched method appears here exactly once. Rules (AGENTS.md 5.6): a target shared by two modules is patched **once** by a Core dispatcher in `src/Core/Hooks.cs` (Harmony id = the plugin GUID) and the modules subscribe handlers; a target only one module uses is a **module patch** applied through `Core.Patches` with the module's own Harmony id `com.denis.apocalypter.wildwasteland.<Id>` (unpatched when the module is switched off). `Core.Patches` logs `CONFLICT` when two modules patch the same target+kind directly. The live inventory is on the F7 page **Debug & performance -> Harmony patches** and in the startup summary (`core hooks: <target>[prefixes/postfixes] ...`; `(NOT PATCHED)` marks a dispatcher that failed to apply).

Dispatcher semantics = HarmonyX with separate plugins: every prefix handler runs (in module init order PlayerBody, NPC, Guns, HUD, Patrols), the original is skipped when any returned false, postfix and finalizer handlers always run. A dispatcher is only patched when at least one running module subscribed (`Hooks.ApplyUsed`), so a disabled feature costs nothing. All 50 string-named targets were checked against the game's IL (review 2026-10-13).

"Hot?" = how often the target runs; the early-out filter is what each handler checks **first** (static bool / reference compare / length gate) before any string work.

## 1. Core dispatchers (Hooks)

| Target (Type.Method) | Kind | Subscribers in order (module: handler) | Hot? | Early-out filter per handler | Known other patchers |
|---|---|---|---|---|---|
| `HutongGames.PlayMaker.Actions.CreateObject.OnEnter` | prefix | Patrols: `WorldSpawning.CreateNaturalTrailer.Prefix` (War Rig; skips + `Finish()` when it builds the rig) | per spawn | `ReferenceEquals(gameObject.Value, Factory.Cached)` | ApocalypterAI, Apocaspawner |
| same | postfix | PlayerBody: `Projectiles.AfterCreateObject`; NPC: `Senses.AfterCreateObject`, `Idle.AfterCreateObject`, `Pounce.AfterCreateObject`; Guns: `GunsModule.AfterCreateObject` (-> `Bosses.Spawned`, `Tracers.OrbSpawned`); Patrols: `TrafficCamps.SpawnHook.Postfix` | per spawn | PlayerBody: `LanceIgnoresOwnCar`, `Game.InCar`, then `Fsm.Name == "Attack"`; NPC: `!Running` (Pounce: `Airborne == 0`); Guns: null result, `BossHealth` / `TracersEnabled && OrbsHitWalls`, boss name cached per prefab id; Patrols: `storeObject == null`, owner `EnemySpawn_` | |
| `SendEvent.OnEnter` | prefix | NPC: `Brain.BeforeSendEvent` (skips + Finish), `Aim.BeforeSendEvent`; Patrols: `NativeStormProtection.DamageEventEnter.Prefix` (skip + Finish) | every SendEvent in the game | Brain: `_npcs.Count == 0 \|\| !On`; Aim: `!Running \|\| !AimEnabled` then event name "Activate" / FSM "RangedAttackWait"; Patrols: `WarRigUnit.All.Count == 0` then `TornadoDamage` / `Damage` / eventTarget | ApocalypterAI, standalone NPCAI |
| `SendEvent.OnUpdate` | prefix | Patrols: `NativeStormProtection.DamageEventUpdate.Prefix` | every everyFrame SendEvent | `WarRigUnit.All.Count == 0` | |
| `SetFsmFloat.OnEnter` | prefix (observe only, returns true) | HUD: `VanillaHits.BeforeSetFsmFloat` | every SetFsmFloat (many per frame) | static `VanillaHits.Observing` and `External && Services.Guns != null` -> return; then `Bodypart` / `Damage` / `Attack` / owner has `Reload` FSM | ApocalypterAI, standalone Gunplay / NPCAI |
| same | postfix | NPC: `VanillaEvents.AfterDamage`, `DogBite.AfterSetFsmFloat`; Guns: `Tracers.AfterSetFsmFloat` (loose-wheel melee) | same | NPC: `!Senses.On` / `_list.Count == 0 \|\| !Running`; Guns: `MeleeOnWheels`, owner name length 6 + `"Attack"`, `IsPlayerMelee` cached per weapon id | |
| `SetFsmFloat.DoSetFsmFloat` | prefix (skip) | Patrols: `NativeStormProtection.DamageValue.Prefix` | every SetFsmFloat | `WarRigUnit.All.Count == 0` then `TornadoDamage` / `Damage` | |
| `SetVelocity.DoSetVelocity` | prefix (skip) | NPC: `Brain.BeforeSetVelocity` (registers NPCs via `Brain.Of`), `DogAnimations.BeforeSetVelocity` (only with the dog bundle) | per frame per ground NPC | `!On` (static) then FSM name "Movement" (needed for registration); dogs: `Dogs.Count == 0 \|\| !Running` | ApocalypterAI |
| `GetButtonDown.OnUpdate` | prefix (skip) | PlayerBody: `ThirdPerson.BeforeGetButtonDown`; NPC: `Senses.BeforeGetButtonDown`; Guns: `GetButtonDown_Patch.Prefix` (Reload suppressed while unloading) | every GetButtonDown action every frame | PlayerBody: per-FSM classification cache (reference-keyed dictionary, no string compares after the first); NPC: `!Running`, modifier None, modifier held (one keyboard read per frame); Guns: `AmmoSettings.Enabled`, `buttonName != "Reload"` | Apocasetter InputBlocker (skips the original while its window is open), PartAdjustment / Ultimate Tools, ApocalypterAI |
| `GetButton.DoGetButton` | prefix (skip, storeResult false) | NPC: `Senses.BeforeGetButton` | every GetButton per frame | as above | Apocasetter InputBlocker |
| `GetButtonUp.OnUpdate` | prefix (skip) | NPC: `Senses.BeforeGetButtonUp` | every GetButtonUp per frame | as above | Apocasetter InputBlocker |
| `GetMouseButtonDown.OnUpdate` | prefix | (dispatcher defined, **no subscriber in 0.1.0 - never patched**) | - | - | Apocasetter InputBlocker |
| `Raycast.OnEnter` | prefix | NPC: `VanillaEvents.BeforePlayerShot` (observes, returns true); Guns: `Tracers.BeforeRaycast` (returns false + `Finish()` when it fires) | every Raycast.OnEnter (everyFrame ones too) | NPC: `!Senses.On \|\| Guns projectiles on`, FSM "Attack"; Guns: `TracersEnabled && PlayerGuns`, FSM "Attack", `repeatInterval != 0`, `_notGun`/`_guns` dictionary by Fsm reference | ApocalypterAI, standalone Gunplay / NPCAI |
| `Raycast.DoRaycast` | prefix (skip) | NPC: `Brain.BeforeRaycast` | per frame per attacking NPC | `_npcs.Count == 0 \|\| !On`, FSM "Attack" | ApocalypterAI |
| `SmoothLookAt.DoSmoothLookAt` | prefix (skip) | PlayerBody: `FirstPersonSway.BeforeSmoothLookAt` (`[Sway]` under the PlayerCamera); NPC: `Brain.BeforeSmoothLookAt` (melee chasers) | every SmoothLookAt every frame | PlayerBody: per-action classification cache (sentinel for other FSMs), then `SmoothWeaponSway`; NPC: `_npcs.Count == 0 \|\| !On` | ApocalypterAI |
| `ActivateGameObject.OnEnter` | prefix (never skips) | Guns: `MagazineDrawPatch.Prefix` -> `PerGunAmmo.Draw` | every ActivateGameObject.OnEnter | `Fsm.Name.Length == 12` + `"WeaponInHand"`, `activate.Value`, `PerGunMagazines` | |
| same | postfix | NPC: `Brain.AfterActivate` | same | `_npcs.Count == 0 \|\| !Running`, dictionary lookup | |
| `ActivateGameObject.DoActivateGameObject` | prefix (skip) | PlayerBody: `ThirdPerson.BeforeActivateGameObject` | every ActivateGameObject | static `ThirdPerson.AimZoom` | |
| `SetPosition.DoSetPosition` | postfix | Patrols: `Suspension.AfterSetPosition` | every SetPosition | static int `SuspensionTune.Live <= 0`, then FSM "Suspension", owner `hinge_wheel_` | |
| `Micosmo.SensorToolkit.PlayMaker.SensorGetDetectionRayHit.OnEnter` | prefix | NPC: `VanillaEvents.BeforeNpcShot` (observes); Guns: `Tracers.BeforeRayHit` (returns false + sends `isNotIntersectedEvent` when it fires) | per NPC burst ray | NPC: `!Running`, FSM "Damage Ranged"; Guns: `TracersEnabled`, FSM "Damage Ranged", target null, `ShooterInfo.IsGun` cached per owner id | ApocalypterAI, standalone Gunplay / NPCAI |
| same | finalizer | NPC: `VanillaEvents.AfterNpcShot` (restores the lengthened RaySensor; module-side stack keyed by the action instance, runs even when Guns skipped the original) | same | `!Running` | |
| `SensorGetLineOfSightResult.OnEnter3D` | prefix (skip) | NPC: `Senses.BeforeLosResult`; Patrols: `SeatedLosGuard.Before` (zero outputs + `Finish()` when not everyFrame) | per burst check | NPC: `!On`, FSM "Attack"; Patrols: `targetObject.Value != null` -> vanilla | |
| `SensorGetLineOfSightResult.OnUpdate3D` | prefix (skip) | NPC: `Senses.BeforeLosResult`; Patrols: `SeatedLosGuard.Before` | every frame for everyFrame LOS actions | same | |
| `SensorGetDetections.DoAction` | prefix (skip) | NPC: `Senses.BeforeGetDetections` (registers agents via `Senses.Of`) | per frame per NPC | `!On` (static) then FSM "Detection" | ApocalypterAI |
| `HutongGames.PlayMaker.Fsm.ProcessEvent(FsmEvent, FsmEventData)` | prefix | NPC: `DogAnimations.BeforeDamageEvent` (only with the dog bundle; skips); Guns: `Fsm_Event_Patch.Prefix` -> `Unloader.OnFsmEvent` (never skips) | EVERY FSM event | NPC: `Dogs.Count == 0 \|\| !Running`; Guns: `Unloader.Busy` field compare | Apocasaver `Fsm_ProcessEvent_Patch` (separate Harmony owner; coexists) |
| `Fsm.EnterState(FsmState)` | prefix + postfix (never skip) | Guns: `MagazineStatePatch` -> `PerGunAmmo.BeforeState` / `AfterState` (WeaponInHand, SaveLoadGame) | every state entry of every FSM (x2) | `Fsm.Name.Length != 12` (both watched names are 12 chars) | Apocasaver `AutosaveStatePatch` (postfix) |
| `ES3Internal.ES3ReferenceMgrBase.GetPrefab(long,bool)` | prefix (first resolver returning a prefab wins) | Patrols: `MotorcycleIntegration.ResolvePrefab` (id 735710202610050001), `PrefabLookup.Resolve` (War Rig, id 735710202610090001) | per prefab lookup on load | `id != PrefabId` | |
| `NWH.Common.FloatingOrigin.FloatingOrigin.Jump` | prefix + postfix (**always patched**) | Core: `Origin.BeforeJump` / `AfterJump` -> `IModule.OnOriginShift(delta)` for every running module (PlayerBody: active climb path; NPC: ghosts, nav boxes, brain/idle/nests/webs positions; Guns: bullets in flight; HUD: floating numbers; Patrols: `TrafficSpace.OriginShift`). No module subscribes to the raw `Hooks.OriginJump`. | per 1000 m jump | delta == zero -> nothing | standalone Apocapatrol (`TrafficSpace.OriginJump`, removed) |

## 2. Core-only patches (`Core.Patches.Patch(null, ...)`, Harmony id = GUID)

| Target | Kind | Handler | Purpose | Hot? | Filter |
|---|---|---|---|---|---|
| `ES3Internal.ES3Reflection.GetType(string)` | prefix | `Core.SaveTypes.Prefix` | Maps saved component type strings `Apocapatrol.X, Apocapatrol`, `WarRig.X, WarRig` (and `Apocaplayer`, `NPCAI`, `Gunplay`, `GunplayHUD`, unused today) to `WildWasteland.Modules.<Id>.X` by prefix rename; leaves the lookup to ES3 when the old assembly is still loaded | per saved type string on load (cached per string) | `typeString` has no comma -> vanilla; dictionary cache |

Not Harmony but worth knowing: `Core.SaveBus` polls `SaveLoadGame.ActiveStateName` once per frame (no patch on the SaveLoadGame FSM); Apocasetter's `InputBlocker` prefixes every PlayMaker input action and WW's input handlers still run under it (HarmonyX runs all prefixes) - `Core.Hotkeys` early-outs on `ApocasetterBridge.InputBlocked`.

## 3. Module patches (targets only that module uses; Harmony id `<GUID>.<Id>`)

### PlayerBody

| Target | Kind | Handler | Filter (first line) | Hot? |
|---|---|---|---|---|
| `HutongGames.PlayMaker.Actions.MouseLook.OnUpdate` | prefix (skip) | `ThirdPerson.BeforeMouseLook` | `return !Orbiting` (static bool) | every MouseLook, every frame |
| `HutongGames.PlayMaker.ActionHelpers.DoMousePick(float,int)` | postfix | `PickAssist.AfterDoMousePick` | `Running` + static `ThirdPerson.On / CarShift` | once per frame something picks |
| `HutongGames.PlayMaker.Actions.iTweenMoveTo.OnEnter` | prefix | `AimTransition.BeforeMoveTo` | `Fsm.Name == "AimDownSights"` | per ADS press / release |
| `HutongGames.PlayMaker.Actions.PlayRandomSound.OnEnter` | prefix + finalizer | `FemalePain.Before` / `Finally` | `FemalePainSounds` + Female (static), FSM name, owner == Player | per damage sound |
| `HutongGames.PlayMaker.Actions.SetAudioClip.OnEnter` | prefix + finalizer | `FemalePain.BeforeDeath` / `FinallyDeath` | `FemalePainSounds` + Female (static), Health/playerDeath, owner == Player | per death sound |

Dropped from Apocaplayer: `BepInEx.Configuration.ConfigFile.Save` postfix (SettingsLayout). Non-Harmony hook: `Camera.onPreCull += ThirdPerson.PreCull` (only while third person is on).

### NPC

| Target | Kind | Handler | Filter (first line) | Hot? |
|---|---|---|---|---|
| `Rotate.DoRotate` | prefix (skip) | `Brain.BeforeRotate` | `_npcs.Count == 0 \|\| !On`, FSM name | per frame per rotating NPC |
| `LookAt.DoLookAt` | prefix (skip) | `Brain.BeforeLookAt` | same | per burst state |
| `AddForce.DoAddForce` | prefix (skip) | `Brain.BeforeAddForce` | same | per Unstuck hop |
| `AudioPlay.OnEnter` | prefix (skip + finishedEvent + Finish) | `Senses.BeforeAudioPlay` | `!On`, FSM name | every AudioPlay |
| `RandomWait.OnEnter` | prefix (swaps min/max, runs original) | `Aim.BeforeRandomWait` | `!Running`, FSM "RangedAttackWait" | per burst cycle |
| `ActivateGameObjects.OnEnter` (optional, `TypeByName`) | postfix | `Brain.AfterActivate` | as ActivateGameObject | rare |
| `Wait.OnEnter` (only with the dog bundle) | prefix (modifies time) | `DogAnimations.BeforeDamageWait` | `Dogs.Count == 0 \|\| !Running` | every Wait |
| `DogCompanion.Dog.Update` (prefix + postfix), `.ChaseTick`, `.LateUpdate`, `.OnDisable`, `.Steer`, `.RideTick`, `.Step`, `DogCompanion.Distraction.Nearest` (third party, only when Dog Companion is loaded) | as NPCAI | `DogCompanionCompat.*` | `On` = bridge resolved && Running && `[NPC] DogCompanionCompat` | per dog tick |

Dropped from NPCAI: postfixes on `Apocapatrol.Patrol.BailOut` / `Apocapatrol.Explode.Blast` (now `Services.CrewBailedOut` / `VehicleExploded`), the 7 Harmony instances. Known other patchers of Rotate/AddForce: ApocalypterAI.

### Guns

| Target | Kind | Handler | Filter (first line) | Hot? |
|---|---|---|---|---|
| `GetLayer.OnEnter` | postfix | `Tracers.AfterGetLayer` | `TracersEnabled`, FSM "Attack", state `getLayer`, result 10 | rare |
| `SetAudioClip.OnEnter` | postfix | `Tracers.AfterSetAudioClip` | `_corpseHitAt.Count == 0` first | rare |
| `TranslatePosition.OnFixedUpdate` | prefix (false for registered orbs, `Finish()` when not everyFrame) | `Tracers.BeforeTranslatePosition` | `_orbs.Count == 0` | every TranslatePosition per FixedUpdate |
| `IntClamp.DoClamp` | prefix (false only while an overflow drop is pending) | `AmmoCapacity_Patch` -> `BackpackOverflow.BeforeClamp` | `Fsm.Name.Length == 4` + `"Ammo"`, per-action cached classification, `Idle()` fast path | every IntClamp, everyFrame x calibers |
| `FloatClamp.DoClamp` | prefix (false only while a plate drop is pending) | `ArmorCapacityPatch` -> `ArmorOverflow.BeforeClamp` | `Fsm.Name.Length == 13` + `"ArmorBackpack"`, per-action cache, `Idle()` | every FloatClamp, everyFrame |
| `HutongGames.PlayMaker.FsmState.OnEnter` | postfix | `BackpackSlot_Patch` -> `BackpackOverflow.OnSlotEnter` | `Fsm.Name.Length != 13`, `DropBackpackOverflow`, `"SlotEmptyFull"`, owner `Backpack_Item` | every state entry of every FSM |
| `ES3PlayMaker.SaveAll.Enter` | postfix | `MagazineSavePatch` -> `PerGunAmmo.SaveGlobal` | FSM "SaveLoadGame", key `global` | per save |
| `ES3PlayMaker.LoadAll.Enter` | prefix | `MagazineLoadPatch` -> `PerGunAmmo.LoadGlobal` | same | per load |
| `ES3.StoreCachedFile(ES3Settings)` | prefix | `MagazineStorePatch` -> `PerGunAmmo.BeforeStore` | `settings.path == _saveFile` | per cache flush |

Known other patchers of SaveAll / LoadAll: Apocasaver `SaveHooks` (prefix/postfix/finalizer, separate owner; WW's are observe-only + one key write - coexist). Dropped from Gunplay: `Apocaunloader.Runner.Update` prefix + `UnpatchSelf` on Apocaunloader's id (now ConflictsWith), `Apocapatrol.MeleeWheels.RaiderHandles` prefix (now `Services.Guns.HandlesFittedWheelMelee`), `PatchAll`.

### HUD

No module-only patches (its single target is the shared `SetFsmFloat.OnEnter` prefix above).

### Patrols

| Target | Kind | Handler | Filter (first line) | Hot? |
|---|---|---|---|---|
| `HutongGames.PlayMaker.ActionHelpers.GetGameObjectFsm(GameObject,string)` | prefix (skip) | `QuietFsm.Prefix` (silences "missing FSM" warnings during builds) | static bool `QuietFsm.On` | every FSM lookup |
| `ArrayListGetRandom.GetRandomItem` | postfix | `WorldSpawning.SplitTrailerSelection.Postfix` (War Rig share of trailer picks) | per-Fsm cached bool (`Dictionary<Fsm,bool>`, cleared on scene load) | every ArrayListGetRandom |
| `ArrayListGetRandom.OnEnter` | prefix | `WorldSpawning.BeforeTrailerSelection.Prefix` | same cache | same |
| `PlayMakerFSM.OnEnable` | postfix | `NativeStormProtection.TrackDust.Postfix` | `FsmName != "SandPlayer"` (string, per enable, not per frame) | per FSM enable |
| `Explosion.ShouldApplyForce(GameObject)` | postfix | `NativeStormProtection.RadialForce.Postfix` | `__result && rigs loaded` | per explosion force query |
| `Tornado.OnTriggerStay(Collider)` | prefix (skip) | `NativeStormProtection.Suction.Prefix` | rigs loaded | per tornado contact per physics step |
| `MotorcycleMod.Factory.Template()` (third party, only when loaded) | postfix | `MotorcycleIntegration.PrepareRearAccess` | - | rare |
| `Apocaspawner.Catalog.Entries()` / `Apocaspawner.Spawner.Spawn(entry,int)` (third party, only when loaded) | postfix / prefix | `SpawnerIntegration.CatalogPostfix` / `SpawnPrefix` | entry key | on catalog build / spawn |
| `ApocaDustStorm.StormShelter.ContainsActor` / `ApocaDustStorm.PlayerStormHazards.Tick` (third party, only when loaded) | prefix | `DustStormBridge.Shelter` / `RefreshOnCrossing` | `broken` flag (one failure stands both down) | per DustStorms tick |

Dropped from Apocapatrol / War Rig: the 6 + 1 Harmony ids, `TrafficSpace.OriginJump` (Core.Origin), WarRig `PatchAll(assembly)`, `Apocasetter.Catalog.OptedIn` postfix (SettingsIntegration), WarRig's separate `CreateObject` / `GetPrefab` patches (now dispatcher subscribers). Non-Harmony hooks: `TerrainTile.OnBeforeTilePrepare / OnTileFinalized`, `Application.quitting += PatrolPersistence.FlushPending`, `SaveBus.StateChanged += PatrolPersistence.OnStateChanged` (subscribed in Init, removed in Shutdown).

## 4. Known shared targets without a WW subscriber

Other mods patch these too; if a future module needs them, add a dispatcher: `Rotate.DoRotate`, `AddForce.DoAddForce` (NPC-only module patches today; ApocalypterAI patches them), `Wait.OnEnter`, `RandomWait.OnEnter`, `AudioPlay.OnEnter`, `LookAt.DoLookAt`, `FsmState.OnEnter`, `IntClamp.DoClamp`, `FloatClamp.DoClamp`, `PlayMakerFSM.OnEnable`, `ES3PlayMaker.Save/SaveAll/Load/LoadAll.Enter` (Apocasaver), `ES3Settings.get_FullPath`, `ES3IO.CommitBackup` (Apocasaver only - never patch these for save purposes, see notes/HostMods.md 2.6).
