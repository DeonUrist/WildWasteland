using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WildWasteland.Modules.Guns
{
    /// <summary>
    /// Ammo settings (ex Gunplay.Ammo.AmmoSettings). The entries are bound by GunsModule.BindConfig in F6 order; the migration from
    /// the standalone Apocaunloader cfg is in LegacyConfig. The Apocaunloader takeover (runtime patch + UnpatchSelf) is gone:
    /// GunsModule.ConflictsWith keeps the whole module off while the standalone Apocaunloader is installed (AGENTS.md 5.8).
    /// </summary>
    internal static class AmmoSettings
    {
        internal static ConfigEntry<bool> Enabled, DropBackpackOverflow, DropArmorOverflow;
        internal static ConfigEntry<float> HoldSeconds, PhaseTimeout;
        internal static ConfigEntry<Key> FallbackKey;
        internal static int TapFrame = -100;
        internal static void V(string message) { GunsModule.Verbose(message); }

        internal static bool ReloadHeld()
        {
            try { return Input.GetButton("Reload"); } catch { }
            try { var kb = Keyboard.current; if (kb != null) return kb[FallbackKey.Value].isPressed; } catch { }
            try { KeyCode kc; if (Enum.TryParse(FallbackKey.Value.ToString(), out kc)) return Input.GetKey(kc); } catch { }
            return false;
        }
        internal static bool TapPending() { int frame = Time.frameCount; return frame == TapFrame || frame == TapFrame + 1; }
    }
}
