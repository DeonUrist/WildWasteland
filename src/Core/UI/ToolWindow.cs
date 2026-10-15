using System;
using System.Collections.Generic;
using UnityEngine;
using CleanHud = WildWasteland.Modules.CleanHUD;

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
        const float Width = 1420, Height = 900;
        static readonly Action<Rect, string, float, Color, TextAnchor> SettingsText = CleanHud.GlyphFont.Draw;
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
            if (_open && !_session.Open) Close();
            if (!_openNewGame || !GameRefs.InGame || EditorSession.Busy || ApocasetterBridge.InputBlocked) return;
            Open(CorePages.GamePage);
            if (_open) { _openNewGame = false; Log.Always("[Core] New game ready: opened Game settings"); }
        }

        public static void Add(ToolPage p) { if (p != null && !_pages.Contains(p)) _pages.Add(p); }
        public static void Remove(ToolPage p) { _pages.Remove(p); if (_current == p) _current = null; }

        public static void Toggle() { if (_open) Close(); else Open(null); }

        public static void Open(ToolPage page)
        { Open(page, GameRefs.Menu != null && GameRefs.Menu.ActiveStateName == "pause"); }

        internal static void OpenFromMenu() { Open(CorePages.GamePage, true); }

        static void Open(ToolPage page, bool fromMenu)
        {
            if (!_open)
            {
                if (EditorSession.Busy || !_session.Acquire(true, fromMenu)) { Log.Info("Settings: another editor is open or the game is paused"); return; }
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
            float s = Mathf.Min((Screen.width - 96) / Width, (Screen.height - 96) / Height);
            var accent = CleanHud.CleanHudModule.Running ? CleanHud.AccentTheme.Resolve(CleanHud.DialogueWidgets.Gold) : CleanHud.DialogueWidgets.Gold;
            var old = Ledger.BeginSettings(accent, CleanHud.Assets.Tex("panel_menu"), SettingsText); var m = GUI.matrix;
            Ledger.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, .6f));
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Width * s) / 2, (Screen.height - Height * s) / 2, 0), Quaternion.identity, new Vector3(s, s, 1));
            _rect = new Rect(0, 0, Width, Height);
            try { GUI.Window(0x57570007, _rect, DrawWindow, "", GUIStyle.none); }
            finally { Ledger.EndSettings(); GUI.skin = old; GUI.matrix = m; GUI.depth = depth; }
        }

        static readonly ToolLayout _layout = new ToolLayout();

        static void DrawWindow(int id)
        {
            Ledger.Window(_rect);
            Ledger.Label(new Rect(40, 24, 900, 60), "WILD WASTELAND", Ledger.Brand, Ledger.Accent);
            Ledger.Label(new Rect(44, 84, 700, 24), "CONFIGURATION  /  " + Plugin.VERSION, Ledger.Small, Ledger.Muted);
            if (Ledger.Button(new Rect(_rect.width - 170, 40, 126, 48), "CLOSE", true, false, true, null, true)) { Close(); return; }
            Ledger.Rule(32, 124, _rect.width - 64);
            Ledger.Label(new Rect(40, _rect.height - 42, 600, 24), "CHANGES APPLY IMMEDIATELY", Ledger.Small, Ledger.Muted);
            Ledger.Label(new Rect(_rect.width - 440, _rect.height - 42, 400, 24), Plugin.ToolWindowKey.Value + " / ESC TO CLOSE", Ledger.Small, Ledger.Muted, TextAnchor.MiddleRight);
            if (CleanHud.CleanHudModule.Running && CleanHud.AccentTheme.Setting != null)
                for (int i = 0; i < CleanHud.AccentTheme.DisplayOrder.Length; i++)
                {
                    int choice = CleanHud.AccentTheme.DisplayOrder[i];
                    var swatch = new Rect(_rect.width / 2 - 112 + i * 28, _rect.height - 42, 22, 22);
                    bool selected = choice == CleanHud.AccentTheme.Index || swatch.Contains(Event.current.mousePosition);
                    Ledger.Panel(swatch, Ledger.Field, selected ? Ledger.Accent : Ledger.Line);
                    Ledger.Fill(new Rect(swatch.x + 4, swatch.y + 4, 14, 14), CleanHud.AccentTheme.Colors[choice]);
                    if (GUI.Button(swatch, GUIContent.none, GUIStyle.none)) CleanHud.AccentTheme.Setting.Value = CleanHud.AccentTheme.Names[choice];
                }

            var pages = Sorted();
            // ---- left: groups and pages
            var nav = new Rect(32, 144, 260, _rect.height - 204);
            _navScroll = Ledger.Scroll(nav, _navScroll, _navHeight, w =>
            {
                float y = 0; string group = null;
                foreach (var p in pages)
                {
                    if (p.Group != group)
                    {
                        group = p.Group; y += group == pages[0].Group ? 2 : 18;
                        Ledger.Label(new Rect(14, y, w - 28, 24), group == ToolGroups.General ? "SETTINGS" : group.ToUpperInvariant(), Ledger.Small, Ledger.Muted); y += 34;
                    }
                    bool off = p.Owner != null && !ModuleHost.IsRunning(p.Owner);
                    var r = new Rect(2, y, w - 4, 52);
                    bool highlighted = p == _current || r.Contains(Event.current.mousePosition);
                    if (highlighted) { Ledger.Fill(r, Ledger.Selected); Ledger.Fill(new Rect(r.x, r.y + 14, 4, r.height - 28), Ledger.Accent); }
                    Ledger.Label(new Rect(r.x + 20, r.y, r.width - 30, r.height), p.Title.ToUpperInvariant() + (off ? "  (OFF)" : ""), Ledger.GameButton, off ? Ledger.Muted : highlighted ? Ledger.Accent : Ledger.Text);
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none) && p != _current) { _current = p; _scroll = Vector2.zero; Ledger.ClearNumbers(); ToolLayout.CancelRebind(); p.OnShow(); }
                    y += 58;
                }
                _navHeight = y + 6;
            });

            // ---- right: page content
            var body = new Rect(316, 144, _rect.width - 348, _rect.height - 204);
            Ledger.Panel(body, Ledger.Surface, Ledger.Line);
            if (_current == null) return;
            Ledger.Label(new Rect(body.x + 24, body.y + 14, body.width - 48, 44), _current.Title.ToUpperInvariant(), Ledger.DialogTitle, Ledger.Accent);
            Ledger.Rule(body.x + 24, body.y + 70, body.width - 48);
            var inner = new Rect(body.x + 16, body.y + 88, body.width - 32, body.height - 104);
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
