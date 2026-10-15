using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core.UI
{
    /// <summary>
    /// The F7 tool window (key rebindable in F6 [Keys] "Tool window"): every debug, editor and technical setting of all
    /// modules in one place, grouped by type, drawn with the Apocapatrol ledger skin. Pauses the game while open
    /// (shared <see cref="EditorSession"/>, so only one editor/tool is open at a time). Modules add pages with <see cref="Add"/>
    /// (or return them from IModule.ToolPages); tools that open their own window (e.g. the patrol ledger) close this one first.
    /// </summary>
    public static class ToolWindow
    {
        static readonly List<ToolPage> _pages = new List<ToolPage>();
        static readonly EditorSession _session = new EditorSession();
        static ToolPage _current;
        static Vector2 _navScroll, _scroll;
        static float _contentHeight = 400, _navHeight = 400;
        static Rect _rect;
        static bool _open;
        static Action _deferred;
        public static bool IsOpen { get { return _open; } }
        /// <summary>The window is open or has a deferred action to run: the GUI runner must stay on.</summary>
        internal static bool NeedsGui { get { return _open || _deferred != null; } }

        internal static void Init()
        {
            Hotkeys.Register(null, Plugin.ToolWindowKey, Toggle, null, true);
            CorePages.Register();
            SaveBus.StateChanged += OnSaveState;
        }

        static bool _newGame, _openNewGame;
        static void OnSaveState(string previous, string current)
        {
            if (current == "LoadGame" || current == "generateTerrain 2") { _newGame = _openNewGame = false; }
            else if (current == "setSeed" || current == "generateTerrain") _newGame = true;
            else if (current == "isPlay" && _newGame) { _newGame = false; _openNewGame = true; }
        }
        internal static void Tick()
        {
            if (!_openNewGame || !GameRefs.InGame || EditorSession.Busy || ApocasetterBridge.InputBlocked) return;
            Open(CorePages.GamePage);
            if (_open) { _openNewGame = false; Log.Always("[Core] New game ready: opened Game settings"); }
        }

        public static void Add(ToolPage p) { if (p != null && !_pages.Contains(p)) _pages.Add(p); }
        public static void Remove(ToolPage p) { _pages.Remove(p); if (_current == p) _current = null; }

        public static void Toggle() { if (_open) Close(); else Open(null); }

        public static void Open(ToolPage page)
        {
            if (!_open)
            {
                if (EditorSession.Busy || !_session.Acquire()) { Log.Info("F7: another editor is open or the game is paused"); return; }
                _open = true;
                CollectModulePages();
            }
            if (page != null) { _current = page; _scroll = Vector2.zero; Ledger.ClearNumbers(); ToolLayout.CancelRebind(); }
            if (_current == null && _pages.Count > 0) _current = Sorted()[0];
            if (_current != null) _current.OnShow();
        }

        public static void Close()
        {
            if (!_open) return;
            _open = false; _session.Close(); Ledger.ClearNumbers(); ToolLayout.CancelRebind();
        }

        /// <summary>Close the window and run <paramref name="a"/> on the next frame (after the session was released), e.g. to open another editor.</summary>
        public static void CloseThen(Action a) { Close(); _deferred = a; }

        static bool _collected;
        static void CollectModulePages()
        {
            if (_collected) return;
            _collected = true;
            foreach (var e in ModuleHost.Entries)
            {
                IEnumerable<ToolPage> pages = null;
                try { pages = e.Module.ToolPages(); } catch (Exception ex) { Log.Error("[" + e.Module.Id + "] ToolPages", ex); }
                if (pages == null) continue;
                foreach (var p in pages) { if (p.Owner == null) p.Owner = e.Module; CorePages.AddDebugPage(p); }
            }
        }

        static List<ToolPage> Sorted()
        {
            var l = new List<ToolPage>();
            foreach (var p in _pages) if (Visible(p)) l.Add(p);
            l.Sort((a, b) =>
            {
                int ga = GroupIndex(a.Group), gb = GroupIndex(b.Group);
                if (ga != gb) return ga.CompareTo(gb);
                return _pages.IndexOf(a).CompareTo(_pages.IndexOf(b));
            });
            return l;
        }
        static bool Visible(ToolPage p)
        {
            return (!p.DebugOnly || Plugin.EnableDebug.Value) && (p != CorePages.DeveloperPage || Plugin.DeveloperMode.Value);
        }
        static int GroupIndex(string g) { int i = Array.IndexOf(ToolGroups.Order, g); return i < 0 ? ToolGroups.Order.Length : i; }

        /// <summary>Called from the Runner's OnGUI.</summary>
        internal static void Draw()
        {
            if (_deferred != null && !EditorSession.Busy && Event.current.type == EventType.Repaint)
            {
                var d = _deferred; _deferred = null;
                try { d(); } catch (Exception e) { Log.Error("F7 deferred action", e); }
            }
            if (!_open) return;
            if (_current != null && !Visible(_current))
            { _current = Plugin.EnableDebug.Value ? CorePages.DebugPage : CorePages.GamePage; _scroll = Vector2.zero; Ledger.ClearNumbers(); ToolLayout.CancelRebind(); _current.OnShow(); }
            if (Event.current.type == EventType.Layout)
            {
                _session.Maintain();
                ToolLayout.PollRebind();
                if (!ToolLayout.Rebinding && Event.current != null && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) { Close(); return; }
                if (_current != null) { try { _current.Update(); } catch (Exception e) { Log.Error("F7 page " + _current.Title, e); } }
            }
            if (Skin.GameFont == null) { var f = ApocasetterBridge.GameFont; if (f != null) Skin.SetGameFont(f); }
            int depth = GUI.depth; GUI.depth = -900;
            float s = Mathf.Min((Screen.width - 32) / Ledger.Width, (Screen.height - 32) / Ledger.Height);
            var old = Ledger.Begin(); var m = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Ledger.Width * s) / 2, (Screen.height - Ledger.Height * s) / 2, 0), Quaternion.identity, new Vector3(s, s, 1));
            _rect = new Rect(0, 0, Ledger.Width, Ledger.Height);
            try { GUI.Window(0x57570007, _rect, DrawWindow, "", GUIStyle.none); }
            finally { GUI.skin = old; GUI.matrix = m; GUI.depth = depth; }
        }

        static readonly ToolLayout _layout = new ToolLayout();

        static void DrawWindow(int id)
        {
            Ledger.Window(_rect);
            Ledger.Header(_rect.width, "WILD WASTELAND  ·  SETTINGS");
            Ledger.Label(new Rect(_rect.width - 380, 30, 230, 26), Plugin.VERSION + "  ·  " + Plugin.ToolWindowKey.Value + " / Esc to close", Ledger.Small, Ledger.Muted, TextAnchor.MiddleRight);
            if (Ledger.Button(new Rect(_rect.width - 130, 26, 100, 34), "CLOSE", true, false, true)) { Close(); return; }

            var pages = Sorted();
            // ---- left: groups and pages
            var nav = new Rect(16, 92, 250, _rect.height - 108);
            Ledger.Panel(nav, Ledger.Surface, Ledger.Line);
            _navScroll = Ledger.Scroll(new Rect(nav.x + 4, nav.y + 4, nav.width - 8, nav.height - 8), _navScroll, _navHeight, w =>
            {
                float y = 0; string group = null;
                foreach (var p in pages)
                {
                    if (p.Group != group)
                    {
                        group = p.Group; y += group == pages[0].Group ? 2 : 10;
                        Ledger.Label(new Rect(6, y, w - 12, 20), group.ToUpperInvariant(), Ledger.Small, Ledger.Accent); y += 22;
                    }
                    bool off = p.Owner != null && !ModuleHost.IsRunning(p.Owner);
                    var r = new Rect(2, y, w - 4, 28);
                    if (p == _current) Ledger.Fill(r, Ledger.Selected);
                    Ledger.Label(new Rect(r.x + 12, r.y, r.width - 16, r.height), p.Title + (off ? "  (off)" : ""), Ledger.Body, off ? Ledger.Muted : Ledger.Text);
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none) && p != _current) { _current = p; _scroll = Vector2.zero; Ledger.ClearNumbers(); ToolLayout.CancelRebind(); p.OnShow(); }
                    y += 30;
                }
                _navHeight = y + 6;
            });

            // ---- right: page content
            var body = new Rect(278, 92, _rect.width - 294, _rect.height - 108);
            Ledger.Panel(body, Ledger.Surface, Ledger.Line);
            if (_current == null) return;
            Ledger.Label(new Rect(body.x + 14, body.y + 8, body.width - 28, 28), _current.Title, Ledger.DialogTitle, Ledger.Text);
            Ledger.Rule(body.x + 8, body.y + 40, body.width - 16);
            var inner = new Rect(body.x + 6, body.y + 46, body.width - 12, body.height - 52);
            var page = _current;
            _scroll = Ledger.Scroll(inner, _scroll, _contentHeight, w =>
            {
                _layout.Begin(w, page.Title + "/");
                bool off = page.Owner != null && !ModuleHost.IsRunning(page.Owner);
                if (off)
                {
                    var e = ModuleHost.Of(page.Owner);
                    _layout.Text(page.Owner.DisplayName + " is not running (" + (e != null ? e.State.ToString() : "?") + "). Settings can still be edited; live tools need the module on (F6 or the Modules page).", Ledger.Danger);
                }
                bool wasEnabled = GUI.enabled;
                if (off && !page.ShowWhenModuleOff && !(page is ConfigPage)) GUI.enabled = false;
                try { page.Draw(_layout); }
                catch (Exception ex) { _layout.Text("Page error: " + ex.Message, Ledger.Danger); Log.Error("F7 page " + page.Title, ex); }
                GUI.enabled = wasEnabled;
                _contentHeight = _layout.Y + 12;
            });
        }
    }
}
