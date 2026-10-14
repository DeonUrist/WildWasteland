using System;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace WildWasteland.Core
{
    // iTween Guard 1.0.0 (d7f5411): always-on fix, no config or module switch.
    // iTween bug: a tween started on an object with a Rigidbody sets physics = true and keeps calling
    // GetComponent<Rigidbody>().MoveRotation/MovePosition in FixedUpdate. When the body is later removed (item picked up,
    // part bolted onto a car) every physics tick throws "NullReferenceException at iTween.ApplyRotateToTargets".
    // Fix: before each tween update, if the body is gone, switch the tween to non-physics mode (iTween's Update then drives it).
    internal static class ITweenGuard
    {
        internal const string GUID = "com.denis.apocalypter.itweenguard";
        static int _fixed;

        internal static void Init()
        {
            BepInEx.PluginInfo info;
            if (Chainloader.PluginInfos.TryGetValue(GUID, out info) && !ReferenceEquals(info.Instance, null))
            {
                Log.Warn("[Core] iTween guard: standalone iTweenGuard installed; built-in fix stays off. Remove iTweenGuard.dll to use the built-in fix.");
                return;
            }
            var target = AccessTools.Method(typeof(iTween), "TweenUpdate");
            if (target == null) { Log.Warn("[Core] iTween guard: iTween.TweenUpdate not found; nothing patched"); return; }
            if (Patches.Patch(null, target, prefix: AccessTools.Method(typeof(ITweenGuard), nameof(Prefix))))
                Log.Always("[Core] iTween guard ready (always on)");
        }

        // ___physics / ___kinematic: iTween's private fields, accessed directly by Harmony (no reflection per call).
        static void Prefix(iTween __instance, ref bool ___physics, ref bool ___kinematic)
        {
            if (!___physics) return;
            try
            {
                if (__instance.GetComponent<Rigidbody>() != null) return;
                ___physics = false;
                ___kinematic = false;   // iTween would also touch the missing body when the tween ends
                _fixed++;
                if (Log.Verbose && _fixed <= 20)
                {
                    string path = __instance.name; for (var p = __instance.transform.parent; p != null; p = p.parent) path = p.name + "/" + path;
                    Log.Info("[Core] iTween guard: repaired a tween whose Rigidbody was removed: " + path);
                }
            }
            catch (Exception) { }
        }
    }
}
