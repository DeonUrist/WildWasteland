using System;
using System.Collections.Generic;
using ES3Internal;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace WildWasteland.Modules.Patrols.WarRig
{
    internal static class WorldSpawning
    {
        private static readonly HashSet<PlayMakerArrayListProxy> touched = new HashSet<PlayMakerArrayListProxy>();
        private static Bounds clearance;
        private static Vector3 modelOffset;
        private static bool clearanceReady;

        // Sample the entire tanker from above: an upward ray alone can miss a roof
        // when its origin is already inside rock. Use current world coordinates.
        internal static bool CanSpawnAt(Vector3 position, Quaternion rotation)
        {
            if (!clearanceReady)
            {
                clearance = Factory.Model().Bounds;
                foreach (var hull in Factory.Hulls()) clearance.Encapsulate(hull.bounds);
                clearance.center *= S.Scale;
                clearance.size = clearance.size * S.Scale + Vector3.one * .2f;
                modelOffset = Layout.Off;
                clearanceReady = true;
            }
            var modelRotation = rotation * Layout.Rot;
            var origin = position + rotation * modelOffset;
            // Reserve the full model/hull envelope, including overhangs, with 10 cm clearance.
            if (Physics.CheckBox(origin + modelRotation * clearance.center, clearance.extents,
                modelRotation, Physics.AllLayers, QueryTriggerInteraction.Ignore)) return false;
            int mask = (1 << 0) | (1 << 11) | (1 << 14);
            foreach (var terrain in Terrain.activeTerrains)
                if (terrain != null) mask |= 1 << terrain.gameObject.layer;
            for (int z = 0; z <= 10; z++)
                for (int x = -1; x <= 1; x++)
                {
                    var sample = origin + modelRotation * new Vector3(
                        Mathf.Lerp(clearance.min.x, clearance.max.x, (x + 1) * .5f), -.75f,
                        Mathf.Lerp(clearance.min.z, clearance.max.z, z * .1f));
                    RaycastHit hit;
                    if (!Physics.Raycast(sample + Vector3.up * 256f, Vector3.down, out hit,
                        512f, mask, QueryTriggerInteraction.Ignore) || hit.point.y > sample.y + .05f)
                        return false;
                }
            return true;
        }

        internal static bool IsTemplate(GameObject go)
        {
            var prefab = go != null ? go.GetComponent<ES3Prefab>() : null;
            return prefab != null && prefab.prefabId == Factory.PrefabId;
        }

        private static bool InCave(Transform node)
        {
            for (; node != null; node = node.parent)
                if (node.name.StartsWith("Cave_", StringComparison.OrdinalIgnoreCase) ||
                    node.name.StartsWith("underground_cave", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static GameObject NativeTrailer(GameObject owner)
        {
            foreach (var proxy in owner.GetComponents<PlayMakerArrayListProxy>())
            {
                if (proxy.referenceName != "CarSpawn" || proxy.arrayList == null) continue;
                foreach (var item in proxy.arrayList)
                {
                    var go = item as GameObject;
                    if (go != null && go.name == "Scrapwagon") return go;
                }
            }
            return Factory.Source("Scrapwagon");
        }

        internal static void UpdatePool(PlayMakerArrayListProxy proxy)
        {
            if (proxy == null || proxy.arrayList == null || proxy.referenceName != "CarSpawn" ||
                !proxy.name.StartsWith("TrailerSpawn", StringComparison.Ordinal)) return;
            bool scrapwagon = false, warRig = false;
            foreach (var value in proxy.arrayList)
            {
                var go = value as GameObject;
                if (go == null) continue;
                if (go.name == "Scrapwagon") scrapwagon = true;
                if (IsTemplate(go)) warRig = true;
            }
            if (!scrapwagon) return;
            touched.Add(proxy);
            if (S.SpawnEnabled.Value && !InCave(proxy.transform))
            {
                if (!warRig) proxy.arrayList.Add(Factory.Template());
            }
            else
                for (int i = proxy.arrayList.Count - 1; i >= 0; i--)
                    if (IsTemplate(proxy.arrayList[i] as GameObject)) proxy.arrayList.RemoveAt(i);
        }

        internal static void Refresh()
        {
            touched.RemoveWhere(x => x == null);
            foreach (var proxy in touched) UpdatePool(proxy);
        }

        // ---- WW wiring: module patches (only War Rig uses ArrayListGetRandom) + the shared CreateObject.OnEnter dispatcher
        internal static void Install(Core.IModule owner)
        {
            Core.Patches.Patch(owner, AccessTools.Method(typeof(ArrayListGetRandom), "GetRandomItem"), postfix: AccessTools.Method(typeof(SplitTrailerSelection), "Postfix"));
            Core.Patches.Patch(owner, AccessTools.Method(typeof(ArrayListGetRandom), "OnEnter"), prefix: AccessTools.Method(typeof(BeforeTrailerSelection), "Prefix"));
            Core.Hooks.CreateObjectEnter.Prefix(owner, CreateNaturalTrailer.Prefix);
        }
        // module shutdown: no War Rig template left in the game's trailer pools (the "disabled" path of UpdatePool)
        internal static void RemoveFromPools()
        {
            touched.RemoveWhere(x => x == null);
            foreach (var proxy in touched)
                if (proxy != null && proxy.arrayList != null)
                    for (int i = proxy.arrayList.Count - 1; i >= 0; i--)
                        if (IsTemplate(proxy.arrayList[i] as GameObject)) proxy.arrayList.RemoveAt(i);
        }
        internal static void ResetForScene() { touched.Clear(); Spawners.Clear(); clearanceReady = false; }

        // First-line filter for the ArrayListGetRandom patches: one string check per FSM, then a reference lookup (no string compare
        // on unrelated FSMs after their first call). Cleared on scene load.
        private static readonly Dictionary<HutongGames.PlayMaker.Fsm, bool> Spawners = new Dictionary<HutongGames.PlayMaker.Fsm, bool>();
        private static bool TrailerSpawner(HutongGames.PlayMaker.FsmStateAction a)
        {
            var fsm = a.Fsm; if (fsm == null) return false;
            bool yes;
            if (Spawners.TryGetValue(fsm, out yes)) return yes;
            var owner = a.Owner;
            yes = fsm.Name == "ItemSpawner" && owner != null && owner.name.StartsWith("TrailerSpawn", StringComparison.Ordinal);
            Spawners[fsm] = yes;
            return yes;
        }

        // Keep the pair's total weight; only divide its selections between the two trailers.
        internal static int SplitPair(PlayMakerArrayListProxy proxy, int selected, int roll)
        {
            if (proxy == null || proxy.arrayList == null || proxy.referenceName != "CarSpawn" ||
                !proxy.name.StartsWith("TrailerSpawn", StringComparison.Ordinal) ||
                selected < 0 || selected >= proxy.arrayList.Count) return selected;
            var chosen = proxy.arrayList[selected] as GameObject;
            if (chosen == null || (chosen.name != "Scrapwagon" && !IsTemplate(chosen))) return selected;
            int scrap = -1, rig = -1;
            for (int i = 0; i < proxy.arrayList.Count; i++)
            {
                var go = proxy.arrayList[i] as GameObject;
                if (go == null) continue;
                if (go.name == "Scrapwagon" && scrap < 0) scrap = i;
                if (IsTemplate(go) && rig < 0) rig = i;
            }
            if (scrap < 0 || rig < 0) return selected;
            return S.SpawnEnabled.Value && !InCave(proxy.transform) && roll < S.WarRigPercent.Value ? rig : scrap;
        }

        // GetRandomItem completes before OnEnter calls Finish, so the following CreateObject
        // action receives the weighted result even if the FSM advances immediately.
        private static class SplitTrailerSelection
        {
            internal static void Postfix(ArrayListGetRandom __instance, PlayMakerArrayListProxy ___proxy)
            {
                if (!TrailerSpawner(__instance) || __instance.Owner == null ||
                    __instance.randomIndex == null || __instance.randomItem == null) return;
                int original = __instance.randomIndex.Value;
                if (___proxy == null || ___proxy.arrayList == null || ___proxy.referenceName != "CarSpawn" ||
                    !___proxy.name.StartsWith("TrailerSpawn", StringComparison.Ordinal) ||
                    original < 0 || original >= ___proxy.arrayList.Count) return;
                var chosen = ___proxy.arrayList[original] as GameObject;
                if (chosen == null || (chosen.name != "Scrapwagon" && !IsTemplate(chosen))) return;
                int selected = SplitPair(___proxy, original, UnityEngine.Random.Range(0, 100));
                if (selected == original) return;
                __instance.randomIndex.Value = selected;
                if (!PlayMakerUtils.ApplyValueToFsmVar(__instance.Fsm, __instance.randomItem, ___proxy.arrayList[selected]))
                    __instance.Fsm.Event(__instance.failureEvent);
            }
        }

        private static class BeforeTrailerSelection
        {
            internal static void Prefix(ArrayListGetRandom __instance)
            {
                if (!TrailerSpawner(__instance) || __instance.Owner == null) return;
                foreach (var proxy in __instance.Owner.GetComponents<PlayMakerArrayListProxy>()) UpdatePool(proxy);
            }
        }

        // The vanilla spawner keeps its own naming, registry, Randomize event and OnOff save flag.
        // Only its CreateObject step needs our active six-hinge trailer instead of an inactive template clone.
        private static class CreateNaturalTrailer
        {
            internal static bool Prefix(CreateObject __instance)
            {
                // first line = reference filter: only a CreateObject whose prefab IS the (already built) War Rig template continues
                var go = __instance.gameObject; var cached = Factory.Cached;
                if (cached == null || go == null || !ReferenceEquals(go.Value, cached)) return true;
                if (__instance.Fsm == null || __instance.Fsm.Name != "ItemSpawner" || __instance.Owner == null ||
                    !__instance.Owner.name.StartsWith("TrailerSpawn", StringComparison.Ordinal) ||
                    __instance.gameObject == null || !IsTemplate(__instance.gameObject.Value)) return true;
                if (!S.SpawnEnabled.Value)
                {
                    // A toggle between selection and creation must not produce a new War Rig.
                    __instance.gameObject.Value = NativeTrailer(__instance.Owner); return true;
                }
                var point = __instance.spawnPoint != null ? __instance.spawnPoint.Value : null;
                Vector3 position = point != null ? point.transform.position : __instance.position.Value;
                Quaternion rotation = point != null ? point.transform.rotation : Quaternion.identity;
                if (__instance.rotation != null && !__instance.rotation.IsNone) rotation = Quaternion.Euler(__instance.rotation.Value);
                if (InCave(__instance.Owner.transform) || (point != null && InCave(point.transform)) || !CanSpawnAt(position, rotation))
                {
                    // Preserve vanilla naming, registration and the consumed-spawner flag.
                    Core.Log.Info("[Patrols] War Rig spawn rejected: cave, blocked clearance or unsuitable ground; using Scrapwagon.");
                    __instance.gameObject.Value = NativeTrailer(__instance.Owner); return true;
                }
                var rig = Factory.Create(position, rotation, S.EquipWheels, null);
                if (__instance.parent != null && __instance.parent.Value != null) rig.transform.SetParent(__instance.parent.Value.transform, true);
                if (__instance.storeObject != null) __instance.storeObject.Value = rig;
                __instance.Finish(); return false;
            }
        }
    }
}
