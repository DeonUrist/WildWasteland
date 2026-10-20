using System;
using System.Collections.Generic;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    internal static class TownFactory
    {
        // A lot's prefab clone is deserialized one build slot ahead (Stage) and activated in the next (Building): the two
        // indivisible native costs land in different frames.
        internal static bool NeedsStage(TownSite site, int index)
        { var lot = site.Plan.Lots[index]; return !lot.Skipped && !lot.IsParking && site.Buildings[index] == null; }
        internal static GameObject Stage(TownSite site, int index, TownAssets assets, Transform staging)
        {
            var lot = site.Plan.Lots[index]; BuildingAsset asset;
            if (!NeedsStage(site, index)) return null;
            if (!assets.Buildings.TryGetValue(lot.Prefab, out asset)) throw new InvalidOperationException("Saved town prefab unavailable: " + lot.Prefab);
            return UnityEngine.Object.Instantiate(asset.Prefab, staging, false);
        }
        internal static void Building(TownSite site, int index, TownAssets assets, TownSettings settings, TownRecord record, Transform staging, Transform parent, GameObject staged)
        {
            var lot = site.Plan.Lots[index]; BuildingAsset asset;
            if (lot.Skipped) { site.Spawners[index] = new PlayMakerFSM[0]; if (staged != null) UnityEngine.Object.Destroy(staged); return; }
            if (site.Buildings[index] != null)
            { if (staged != null) UnityEngine.Object.Destroy(staged); site.Buildings[index].transform.SetParent(parent, true); site.Buildings[index].SetActive(true); TownPopulation.Configure(site, index, assets, settings); InteriorPopulation.OwnTown(site, index); TownBakeBridge.Discover(site.Buildings[index]); return; }
            if (lot.IsParking) { site.Spawners[index] = new PlayMakerFSM[0]; if (staged != null) UnityEngine.Object.Destroy(staged); Log.Always("[Towns] lot " + site.Plan.Key + "/" + index + ": reserved 24 x 40 m ground parking slot"); return; }
            if (!assets.Buildings.TryGetValue(lot.Prefab, out asset)) throw new InvalidOperationException("Saved town prefab unavailable: " + lot.Prefab);
            var go = staged != null ? staged : UnityEngine.Object.Instantiate(asset.Prefab, staging, false);
            site.Buildings[index] = go;
            TownMarkers.Building(go.transform);
            // Boss trap FSMs must never close doors or alter global boss progression in towns.
            foreach (var fsm in go.GetComponentsInChildren<PlayMakerFSM>(true)) if (fsm.FsmName == "BossDoor") fsm.enabled = false;
            foreach (string path in asset.Markers)
            { var icon = go.transform.Find(path); var renderer = icon == null ? null : icon.GetComponent<Renderer>(); if (renderer != null) renderer.forceRenderingOff = true; }
            go.name = lot.Prefab + " (Towns " + TownPlanner.Hash(site.Plan.Key) + "/" + index + ")";
            go.transform.position = site.Scene(lot.LocalPosition);
            go.transform.rotation = site.Anchor.rotation * Quaternion.Euler(0, site.Plan.Yaw + lot.Yaw, 0);
            go.SetActive(true); // Still under the inactive staging root: no spawn callback yet.
            var spawners = new PlayMakerFSM[asset.Spawners.Length];
            site.Spawners[index] = spawners;
            for (int i = 0; i < spawners.Length; i++)
            {
                var node = asset.Spawners[i].Length == 0 ? go.transform : go.transform.Find(asset.Spawners[i]);
                if (node == null) throw new InvalidOperationException("Missing cached town spawner path " + asset.Spawners[i]);
                spawners[i] = FsmUtil.Find(node.gameObject, "ItemSpawner");
                if (spawners[i] != null && asset.Budgets[i].Kind == 6)
                { Set(spawners[i], "OnOff", 1); spawners[i].enabled = false; }
            }
            go.transform.SetParent(parent, true);
            // Native test spawners start in wait (one scaled second before their trigger). Restore before
            // their first Update. Keep their components/names and native spawned-item save lists intact.
            SpawnerState[] states;
            if (record.Spawners.TryGetValue(index, out states))
                foreach (var state in states)
                    for (int i = 0; i < spawners.Length; i++) if (asset.Spawners[i] == state.Path && spawners[i] != null)
                    {
                        Set(spawners[i], "OnOff", state.OnOff); Set(spawners[i], "number", state.Number); Set(spawners[i], "numberEnd", state.NumberEnd);
                        if (state.OnOff > 0 || (state.NumberEnd > 0 && state.Number >= state.NumberEnd))
                        { Set(spawners[i], "OnOff", 1); spawners[i].Fsm.SetState("spawned"); }
                        // A partially completed native synchronous spawn loop resumes only after isPlay.
                    }
            TownPopulation.Configure(site, index, assets, settings);
            InteriorPopulation.OwnTown(site, index);
            TownBakeBridge.Discover(go);
            Log.Always("[Towns] lot " + site.Plan.Key + "/" + index + ": " + lot.Prefab + ", role=" + lot.Role + ", town=" + site.Plan.Condition);
        }
        internal static void Resume(TownSite site)
        {
            foreach (var fsms in site.Spawners) if (fsms != null) foreach (var f in fsms)
            {
                if (f == null || !f.enabled || Get(f, "OnOff") > 0 || Get(f, "number") <= 0 || Get(f, "numberEnd") <= Get(f, "number")) continue;
                if (f.ActiveStateName == "wait" || f.ActiveStateName == "trigger") f.Fsm.SetState("getRandom int");
            }
        }
        internal static void Capture(TownSite site, TownRecord record, TownAssets assets)
        {
            for (int i = 0; i < site.Spawners.Length; i++)
            {
                var fsms = site.Spawners[i]; if (fsms == null) continue;
                if (site.Plan.Lots[i].IsParking || site.Plan.Lots[i].Skipped) continue;
                var states = new SpawnerState[fsms.Length];
                var paths = assets.Buildings[site.Plan.Lots[i].Prefab].Spawners;
                for (int j = 0; j < fsms.Length; j++) states[j] = new SpawnerState { Path = paths[j],
                    OnOff = Get(fsms[j], "OnOff"), Number = Get(fsms[j], "number"), NumberEnd = Get(fsms[j], "numberEnd") };
                record.Spawners[i] = states;
            }
        }
        private static int Get(PlayMakerFSM f, string name)
        { var v = f == null ? null : f.FsmVariables.FindFsmInt(name); return v == null ? 0 : v.Value; }
        private static void Set(PlayMakerFSM f, string name, int value)
        { var v = f.FsmVariables.FindFsmInt(name); if (v != null) v.Value = value; }

        internal static GameObject Streets(TownSite site, TownAssets assets, Transform parent)
        {
            var root = new GameObject("Towns parking and wreck pockets " + site.Plan.Key); root.transform.SetParent(parent, false);
            TownDebris.Build(site, assets, root.transform);
            return root;
        }
        internal static void KeepGeometry(TownSite site, int index, Transform parent)
        {
            var go = site.Buildings[index]; if (go == null) { site.Plan.Lots[index].Skipped = true; return; }
            foreach (var f in go.GetComponentsInChildren<PlayMakerFSM>(true)) if (f.FsmName == "ItemSpawner") { Set(f, "OnOff", 1); f.enabled = false; }
            go.transform.SetParent(parent, true); go.SetActive(true); TownBakeBridge.Discover(go);
        }
        internal static void Destroy(TownSite site)
        {
            if (site.Staged != null) { UnityEngine.Object.Destroy(site.Staged); site.Staged = null; }
            if (site.Walls != null) site.Walls.Dispose();
            if (site.Overlap != null) site.Overlap.Dispose();
            TownMarkers.Remove(site);
            foreach (var building in site.Buildings) if (building != null) { TownOverlap.Forget(building.transform); UnityEngine.Object.Destroy(building); }
            if (site.Streets != null)
            { foreach (var filter in site.Streets.GetComponentsInChildren<MeshFilter>()) if (filter.sharedMesh != null && filter.sharedMesh.name == "Towns parking slab") UnityEngine.Object.Destroy(filter.sharedMesh); UnityEngine.Object.Destroy(site.Streets); }
        }
    }
}
