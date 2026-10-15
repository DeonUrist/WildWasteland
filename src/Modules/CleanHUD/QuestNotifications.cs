using System;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class QuestNotifications
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install(CleanHudModule owner)
        {
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("local.apocalypterquests", out info) || ReferenceEquals(info.Instance, null) || info.Metadata.Version != new Version(1, 8, 0)) return;
            Core.Patches.Patch(owner, AccessTools.Method(info.Instance.GetType(), "AddToast"), prefix: AccessTools.Method(typeof(QuestNotifications), "BeforeToast"));
        }
        private static bool BeforeToast(string __0)
        {
            if (!CleanHudModule.Running || !CleanHudModule.Enabled.Value || string.IsNullOrEmpty(__0)) return true;
            MessageHook.Push(__0); Core.Log.Always("[Quests] " + __0); return false;
        }
    }
}
