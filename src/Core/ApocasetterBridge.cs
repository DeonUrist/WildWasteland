using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>
    /// The only file that talks to Apocasetter (soft dependency, AGENTS.md 5.9). Everything is resolved by reflection on
    /// assembly name "Apocasetter" and cached; WW works without it.
    /// </summary>
    public static class ApocasetterBridge
    {
        public const string GUID = "com.denis.apocalypter.apocasetter";
        static bool _resolved;
        static MethodInfo _set;
        static FieldInfo _active, _font;

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                Assembly asm = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) if (a.GetName().Name == "Apocasetter") { asm = a; break; }
                if (asm == null) return;
                var ib = asm.GetType("Apocasetter.InputBlocker");
                if (ib != null) { _set = ib.GetMethod("Set", BindingFlags.Static | BindingFlags.Public); _active = ib.GetField("Active", BindingFlags.Static | BindingFlags.Public); }
                var gs = asm.GetType("Apocasetter.GameSkin");
                if (gs != null) _font = gs.GetField("GameFont", BindingFlags.Static | BindingFlags.Public);
            }
            catch (Exception e) { Log.Warn("Apocasetter bridge: " + e.Message); }
        }

        public static bool Present { get { Resolve(); return _set != null; } }

        /// <summary>True while the Apocasetter Mods window blocks game input.</summary>
        public static bool InputBlocked
        {
            get { Resolve(); try { return _active != null && (bool)_active.GetValue(null); } catch { return false; } }
        }

        /// <summary>Returns true when the blocker was changed (Apocasetter present).</summary>
        public static bool SetInputBlock(bool on)
        {
            Resolve();
            if (_set == null) return false;
            try { _set.Invoke(null, new object[] { on }); return true; }
            catch (Exception e) { Log.Warn("Apocasetter InputBlocker: " + e.Message); return false; }
        }

        /// <summary>The game's menu font as captured by Apocasetter (null until its MODS button was cloned). Polled lazily.</summary>
        public static Font GameFont
        {
            get { Resolve(); try { return _font != null ? _font.GetValue(null) as Font : null; } catch { return null; } }
        }
    }
}
