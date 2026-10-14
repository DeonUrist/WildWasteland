using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class ApocasaverLoading
    {
        private static LoadingScreen _screen;
        private static bool _ready;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install(CleanHudModule owner, LoadingScreen screen)
        {
            _screen = screen; _ready = false;
            var type = AccessTools.TypeByName("Apocasaver.AutosavePresentation");
            if (type == null) return;
            bool begin = Core.Patches.Patch(owner, AccessTools.Method(type, "Begin"), postfix: AccessTools.Method(typeof(ApocasaverLoading), "AfterBegin"));
            bool end = Core.Patches.Patch(owner, AccessTools.Method(type, "End"), postfix: AccessTools.Method(typeof(ApocasaverLoading), "AfterEnd"));
            _ready = begin && end;
            if (_ready) CleanHudModule.Log("Autosave loading screen hooks ready");
        }
        internal static void Clear() { _screen = null; _ready = false; }
        private static void AfterBegin()
        {
            if (_screen == null || !_ready) return;
            try { _screen.Begin(true); } catch (Exception e) { CleanHudModule.Throttled("autosave screen", e); }
        }
        private static void AfterEnd()
        {
            if (_screen == null || !_ready) return;
            try { _screen.EndSaving(); } catch (Exception e) { CleanHudModule.Throttled("autosave screen end", e); }
        }
    }
}
