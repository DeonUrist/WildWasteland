using System;
using System.Collections.Generic;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core.UI;

namespace WildWasteland.Core
{
    // Loaded plugins keep runners/coroutines/patches: disable through the installer's pre-load operation.
    internal static class ReplacementGuard
    {
        static readonly Dictionary<string, string> _status = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly List<string> _names = new List<string>();
        static bool _title;
        internal static bool MenuModalOpen;
        static int _failed;
        static GUIContent _message;
        static readonly GUIContent Heading = new GUIContent("RESTART REQUIRED");

        internal static void Prepare()
        {
            foreach (var entry in ModuleHost.Entries)
            {
                var guids = entry.Module.ConflictsWith;
                if (guids != null) foreach (string guid in guids) Disable(guid);
            }
            Disable("local.immersivemap");
            Disable("com.denis.apocalypter.womenofwasteland");
            Disable(HealthStacks.GUID);
            Disable(ITweenGuard.GUID);
            if (_names.Count == 0) return;
            var text = new StringBuilder();
            text.Append(_names.Count).Append(_names.Count == 1 ? " standalone mod should be removed" : " standalone mods should be removed");
            text.Append(" from your active mods because Wild Wasteland replaces them:\n\n");
            for (int i = 0; i < _names.Count; i++) text.Append("- ").Append(_names[i]).Append('\n');
            if (_failed < _names.Count) text.Append("\nRestart the game. Apocasetter will disable the queued mods. Their files and configs are kept.");
            if (_failed != 0) text.Append("\n\nDisable the mods marked 'disable manually' before restarting. Automatic disabling was unavailable for them; see LogOutput.log for details.");
            _message = new GUIContent(text.ToString());
            Scheduler.Interval(null, "Core.Replacement notice", .5f, Tick).RunNow();
            Scheduler.Gui(null, "Core.Replacement notice", Draw, WantsNotice);
        }

        static void Disable(string guid)
        {
            if (_status.ContainsKey(guid)) return;
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(guid, out info) || ReferenceEquals(info.Instance, null)) return;
            string error;
            bool queued = ApocasetterBridge.QueueDisable(info, out error);
            _status[guid] = queued ? "disable queued in Apocasetter; restart the game for Wild Wasteland to take over"
                : "automatic disable unavailable (" + error + "); disable the standalone mod and restart";
            _names.Add(info.Metadata.Name + (queued ? "" : " (disable manually)"));
            if (!queued) _failed++;
            Log.Warn("[Core] Replacing " + info.Metadata.Name + ": " + _status[guid] + ". Configs are preserved.");
        }

        internal static string Status(string guid)
        {
            string status;
            return _status.TryGetValue(guid, out status) ? status : "disable the standalone mod and restart";
        }

        internal static void SceneLoaded(Scene scene) { _title = scene.buildIndex == 0; }
        static void Tick() { SceneLoaded(SceneManager.GetActiveScene()); }
        static bool WantsNotice() { return _title && _message != null && !MenuModalOpen; }
        static void Draw()
        {
            if (MenuModalOpen || ToolWindow.IsOpen || EditorSession.Busy || ApocasetterBridge.InputBlocked) return;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Skin.Ensure(scale);
            float width = 540f * scale, inset = 20f * scale;
            float height = Skin.Body.CalcHeight(_message, width - inset * 2) + 74f * scale;
            var panel = new Rect(Screen.width - width - inset, Mathf.Max(inset, (Screen.height - height) * .5f), width, height);
            int depth = GUI.depth; Color color = GUI.color;
            GUI.depth = -800; GUI.color = Color.white;
            Skin.Fill(panel, new Color(.035f, .027f, .024f, .97f));
            Skin.Fill(new Rect(panel.x, panel.y, 4f * scale, panel.height), Skin.Yellow);
            GUI.Label(new Rect(panel.x + inset, panel.y + 14f * scale, width - inset * 2, 30f * scale), Heading, Skin.H1);
            GUI.Label(new Rect(panel.x + inset, panel.y + 54f * scale, width - inset * 2, height - 64f * scale), _message, Skin.Body);
            GUI.color = color; GUI.depth = depth;
        }
    }
}
