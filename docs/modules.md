# Modules

One row per mechanic that lives (or will live) in Wild Wasteland. Fill in **Status** and **Ported from** when work starts. "Ported from" is the source repo commit the code was taken from, so later fixes in the standalone mod can be diffed and carried over.

Status values: `candidate` (not decided), `planned`, `porting`, `in` (shipped in WW), `dropped`.

## Candidate source mods (snapshot of V:\Repos\Apocalypter, 2026-10-13)

| Mod (repo folder) | Plugin GUID | Version / HEAD | Status | Ported from | WW module | Notes |
|---|---|---|---|---|---|---|
| npcai | com.denis.apocalypter.npcai | 1.9.12 / 5d3871e | candidate | | | NPC Overhaul; exposes NPCAI.Api v1. Soft-depends on Apocaplayer. Conflicts with third-party ApocalypterAI. |
| gunplay | com.denis.apocalypter.gunplay | 1.3.0 / 006d690 | candidate | | | Uses NPCAI.Api. |
| gunplayhud | com.denis.apocalypter.gunplayhud | 1.2.2 / a8f94cf | candidate | | | Crosshair textures + headshot sound assets. |
| womenofwasteland | com.denis.apocalypter.womenofwasteland | 1.0.0 / 21d05c6 | candidate | | | Split out of Apocaraider 1.7.0. |
| Apocaraider | com.denis.apocalypter.apocaraider | 1.7.0 / 7c1e3e9 | candidate | | | Monolith that gunplay/NPCAI/WoW were split from; running it beside them doubles features. |
| Apocaplayer | com.denis.apocalypter.apocaplayer | 2.3.9 / ba17d32 | candidate | | | Body/animation; ModAPI used by NPCAI and Apocapatrol. |
| Apocapatrol | com.denis.apocalypter.apocapatrol | 2.7.5 / ed0c422 | candidate | | | 65 files; convoys, merchants, crews. Hooks Apocasaver. |
| Apocapocket | com.denis.apocalypter.apocapocket | 2.0.10 / 41a5032 | candidate | | | Extra slots; owns save data. |
| Apocaunloader | com.denis.apocalypter.apocaunloader | 1.3.0 / 7a215cb | candidate | | | |
| HealthStacks | com.denis.apocalypter.healthstacks | 1.0.0 / a326d8d | candidate | | | |
| Apocatremors | com.denis.apocalypter.apocatremors | 1.2.0 / 60f2bb2 | candidate | | | |
| CaveDarkness | com.denis.apocalypter.cavedarkness | 1.0.0 / 2eac97c | candidate | | | |
| WarRig | com.denis.apocalypter.warrig | 1.1.3 / 89419ea | candidate | | | Custom saveable prefab (ES3 prefabId). |
| ultimatetools | com.denis.apocalypter.partadjustment | 2.3.2 / e0abb4c | candidate | | | Already a merge (Part Adjustment + toolbox); kept the old GUID for save compatibility. |
| Apocaclimber | com.denis.apocalypter.apocaclimber | 1.0.2 / (no git) | candidate | | | |
| iTweenGuard | com.denis.apocalypter.itweenguard | 1.0.0 / d7f5411 | candidate | | | Tiny fix; could stay standalone. |

## Stay standalone (integrate, don't absorb)

| Mod | Why |
|---|---|
| Apocasetter | Settings/menu host and installer for every mod. WW soft-depends on it. |
| Apocasaver | Save coordinator; WW talks to it through its hooks. Decide later. |
| ApocaLanguage | Translates IMGUI text of all mods. |
| ApocaFsmInspector, ModProfiler | Debug tools; never shipped inside WW. |
| Apocaspawner | Dev/cheat spawner. |

## Third-party mods to stay compatible with

| Mod | GUID | What to do |
|---|---|---|
| ApocalypterAI | ApocalypterAI | Prefixes the same PlayMaker actions as NPCAI. Only one brain per body. |
| ApocalypterGarage | ApocalypterGarage | Spawn only through its functions; never add its objects to ArrayList_* yourself. |
| ApocaDustStorm | local.apocalypter.duststorm | Disables vanilla storms; its NPCAI bridge refuses NPCAI >= 1.8. |
| Hazel Cars | com.hazel.apocalypter.cars | Check overlap with any vehicle module. |
