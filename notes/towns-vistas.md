# Towns / Vistas compatibility — 2026-10-20 — Testing

Denis requested a quick Towns compatibility pass after identifying missing Vistas road hints and post-apply terrain ownership. Changes stay in Towns; existing shared-tree work is retained.

- `TownPaths.cs`: TownWorldRoads combines native splines with the existing Core.VistasWorldGen.RoadLines snapshot, including when native splines are absent. It filters segments to the current block, samples long segments for siting and orders the combined snapshot deterministically. This also supplies desert gas-station siting.
- `TownPlanner.cs`: new town randomness includes Vistas' DLL/generation-settings identity. The chunk/save key is deliberately unchanged. Saved towns retain their layouts and spawner progress; changing worldgen settings does not relocate them.
- `TownParking.cs`: with Vistas present, a new town's ground approach can extend to the nearest published road. It retains the outward parking lane, rejects a return through the town pad, checks native POI exclusion and the existing 22% endpoint grade limit. Otherwise existing approach behavior remains. Vanilla behavior is retained without Vistas.
- `TownVistasTerrain.cs` + `TownsModule.cs`: capture only bounded applied height rectangles for town pads/access corridors and gas-station pads. After the existing Vistas TerrainEdited callback, restore only owned samples to their captured values. This keeps level foundations and avoids cumulative feather blending. Road-connected access profiles adjust to the live road-end height; the final ten metres remain with Vistas. Terrain identity, resolution, size and tile-coordinate guards reject stale captures. Streaming, world changes and module shutdown release captures/subscriptions. No per-frame scan or new scheduler job.

The quoted earlier review described TownPaths.Build's older road connection. The current compact-town generator instead calls TownParking.Access; that is the path adapted here.

## Limits

Vistas 1.0.7's road network is populated asynchronously by its driver after applied terrain becomes available (`A/S.cs`, `A/q.cs` in the local inspection-only decompile). The existing bridge exposes published roads, not a readiness API. A first generation pass can therefore still have no Vistas road hints; this patch does not force tile regeneration, hold world loading or relocate saved towns when roads appear later. With Ensure selected towns off, a block without published/native roads can still reject town siting. Existing short saved parking approaches are retained. Full guaranteed first-pass road readiness would require a separate bridge/generation-lifecycle change.

This callback protects against Vistas road-grading callbacks, not every unrelated terrain writer. Pads lie inside tile borders under the existing siting rules; general seam-weld behavior is still an in-game check. Runtime height-write cost and Vistas' own revalidation behavior are unmeasured.

No config entries, save formats, assets or Core/Patrols sources changed. No game/harness launched; no commit/push/release.

## Build / deployment

Windows `dotnet build WildWasteland.csproj -c Release -p:Deploy=false`: zero errors, two existing CS0649 NPC PoiNav.Route field warnings.

Staged DLL: `V:/Repos/Apocalypter/_releases/WildWasteland_towns-vistas_20261020_215301_946.dll` (3,408,896 bytes), MD5 `1DA0F4D1FA034E504E6490687521ADAA`. Matching build asset index staged beside it. Game was running at deployment check, so no live DLL/index/assets were overwritten. Active/disabled manifest checks passed; installed DLL at check was `7D1A5DE0B857063BFC918789E2598456`.

## Denis's verification

1. Close the game and install the staged DLL. Launch with Vistas enabled. Require its existing `[Vistas] ... compatibility ready:` line and `[Towns] Vistas compatibility active: generated road hints and protected town/gas-station terrain.`
2. In Replace road mode, visit a newly generated roadside town and gas station after the network has populated. Expect sites beside Vistas roads and an outside parking approach toward a suitable road. Check Add mode as well. No agent changed live settings; use a test world if increasing town chance for coverage.
3. Stay near a town while Vistas finishes grading, then leave/return and save/reload. Expect level foundations/wall bases and no pad drift. Check the access-road mouth for a smooth join. With global VerboseLog on, actual restorations report `[Towns] Vistas terrain restored: ... pad/access samples; buildings retained.`
4. Check an existing saved town keeps its name/layout/population and a world without Vistas keeps normal Towns behavior. Check frame pacing while Vistas finishes road grading. Nothing is marked Verified until Denis reports these results.

## Combined local review — 2026-10-20

Denis requested review and commitment of all pending local work. Reviewed the road snapshot, saved-layout handling, terrain sample ownership and callback lifecycle, the Camp_10 test tool, and the town-overlay cleanup fix. The Camp_10 tool was committed separately as `7e74f24` while this review was in progress; the earlier material guard is already in `826a53e`.

The cleanup exception followed an earlier missing-key error during overlay capture: gas-station parking shells were excluded from readiness checks but included in the subsequent native-grid lookup. Capture now applies the same parking/skipped-lot exclusions in both passes and publishes grids/ownership only after successful complete capture. Returning to the menu and starting another world should no longer fail in `TownOverlap.Restore`; verify those transitions after allowing a town to finish preparation.

Combined Windows Release build: zero errors, two existing NPC CS0649 warnings. Stage `V:/Repos/Apocalypter/_releases/WildWasteland_towns-reviewed_20261020_220447_140.dll`, MD5 `F4271B7534DCEAD6B47E7CD6DD88D326`. Supersedes the earlier staged builds for testing the combined tree. Game still running, so deployment is pending; no live DLL, asset, config or save changes. Build and source review do not verify gameplay, terrain appearance or frame pacing. The default-off Camp_10 test setting is the only new config entry in the reviewed combined work.
