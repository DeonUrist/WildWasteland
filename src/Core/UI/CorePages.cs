using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using Player = WildWasteland.Modules.PlayerBody.PlayerBodyModule;
using HudSettings = WildWasteland.Modules.HUD.HudModule;
using Patrols = WildWasteland.Modules.Patrols.PatrolsModule;

namespace WildWasteland.Core.UI
{
    /// <summary>Pages owned by Core: module overview (on/off), performance, Harmony inventory, core settings.</summary>
    internal static class CorePages
    {
        internal static ToolPage GamePage;
        static readonly List<ToolPage> _debugPages = new List<ToolPage>();
        static ConfigPage _settings;
        static ConfigEntry<bool> _poi;
        internal static void AddDebugPage(ToolPage page) { _debugPages.Add(page); }

        internal static void Register()
        {
            GamePage = new GameSettingsPage();
            ToolWindow.Add(GamePage);
            ToolWindow.Add(new ActionPage(null, ToolGroups.General, "Modules", Modules));
            ToolWindow.Add(new ActionPage(null, ToolGroups.General, "Debug", Debug) { DebugOnly = true });
            _settings = new ConfigPage(null, ToolGroups.General, "Other settings", Plugin.Settings, d => true) { Intro = "Remaining gameplay values and switches." };
            foreach (var e in ModuleHost.Entries) if (e.Module.Id == "POI") _poi = e.Config.Enabled;
        }

        static string F(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }

        sealed class GameSettingsPage : ToolPage
        {
            internal GameSettingsPage() { Group = ToolGroups.General; Title = "Game"; }
            public override void OnShow()
            {
                WildWasteland.Modules.PlayerBody.InventoryModel.PrepareSettingsPictures();
                WildWasteland.Modules.HUD.CrosshairController.PreparePreview();
            }
            public override void Draw(ToolLayout l) { Game(l); }
        }

        static void Switch(ToolLayout l, string label, ConfigEntry<bool> entry)
        {
            bool value = l.Choices(label, entry.Value ? 1 : 0, "Off", "On") == 1;
            if (value != entry.Value) entry.Value = value;
        }

        static void Game(ToolLayout l)
        {
            l.Heading("Player");
            BodySelection(l);
            int body = !Player.BodyFirstPerson.Value ? 0 : Player.FullBodyOnlyOnFoot.Value ? 1 : 2;
            int next = l.Choices("Full Body", body, "Off", "Only on foot", "On");
            if (next != body) { Player.FullBodyOnlyOnFoot.Value = next == 1; Player.BodyFirstPerson.Value = next != 0; }
            int arms = !Player.EmptyHandArms.Value ? 0 : Player.FirstPersonArmsOnlyOnFoot.Value ? 1 : 2;
            next = l.Choices("First Person Arms", arms, "Off", "On foot", "On");
            if (next != arms) { Player.FirstPersonArmsOnlyOnFoot.Value = next == 1; Player.EmptyHandArms.Value = next != 0; }
            Switch(l, "Climbing", Player.ClimbingOn);
            Switch(l, "Third Person Camera", Player.ThirdPersonOnFoot);
            Switch(l, "Occlusion", Player.OcclusionPrototype);   // third person on foot only; in cars it stays off
            l.Text("Full Body shows your body in first person. Use the game's Change Camera key for third person.");
            if (!ModuleHost.IsRunning(Player.Instance)) l.Text("Enable Player body & animation in Modules to use these player settings.");

            l.Heading("World");
            int vehicles = !Patrols.ConvoyEnabled.Value && !Patrols.WorldTrafficEnabled.Value ? 0 : Patrols.FuryRoad.Value ? 2 : 1;
            next = l.Choices("Enemy Vehicles", vehicles, "Loner", "New arrival", "Fury road");
            if (next != vehicles)
            {
                Patrols.ConvoyEnabled.Value = next != 0;
                Patrols.FuryRoad.Value = next == 2;
                Patrols.WorldTrafficEnabled.Value = next != 0;
            }
            l.Text("Loner: no automatic patrols. New arrival: groups unlock with distance and boss kills. Fury road: all patrol types are available from the start.");
            if (_poi != null) Switch(l, "New POI", _poi);

            l.Heading("HUD");
            CrosshairSelection(l);
            Switch(l, "Grabbing dot", HudSettings.SelectionDot);
            bool damage = HudSettings.HudEnabled.Value && HudSettings.DamageNumbers.Value == 2;
            bool show = l.Choices("Floating damage", damage ? 1 : 0, "Off", "On") == 1;
            if (show != damage) { HudSettings.DamageNumbers.Value = show ? 2 : 0; if (show) HudSettings.HudEnabled.Value = true; }
            FeedbackSwitch(l, "Hit markers", HudSettings.HitMarker);
            FeedbackSwitch(l, "Headshot sound", HudSettings.HeadshotSound);
            Switch(l, "Toggle Ironsights", WildWasteland.Modules.Guns.GunsModule.ToggleIronsights);
            l.KeyPair("Shout", WildWasteland.Modules.NPC.NpcModule.ShoutKey, "Shout modifier", WildWasteland.Modules.NPC.NpcModule.ShoutModifier);
            if (!ModuleHost.IsRunning(HudSettings.Instance)) l.Text("Enable Combat HUD in Modules to use these HUD settings.");
        }

        static void FeedbackSwitch(ToolLayout l, string label, ConfigEntry<bool> entry)
        {
            bool value = HudSettings.HudEnabled.Value && entry.Value;
            bool next = l.Choices(label, value ? 1 : 0, "Off", "On") == 1;
            if (next != value) { entry.Value = next; if (next) HudSettings.HudEnabled.Value = true; }
        }

        static void BodySelection(ToolLayout l)
        {
            var r = l.Next(162); float width = (r.width - 10) / 2;
            for (int i = 0; i < 2; i++)
            {
                bool female = i == 1;
                var card = new Rect(r.x + i * (width + 10), r.y, width, r.height);
                bool selected = Player.Female == female;
                Ledger.Panel(card, selected ? Ledger.Selected : Ledger.Field, selected ? Ledger.Accent : Ledger.Line);
                var texture = WildWasteland.Modules.PlayerBody.InventoryModel.SettingsPicture(female);
                if (texture != null) GUI.DrawTexture(new Rect(card.x + 8, card.y + 6, card.width - 16, card.height - 36), texture, ScaleMode.ScaleToFit);
                else Ledger.Label(new Rect(card.x + 8, card.y + 8, card.width - 16, card.height - 40), "TAB preview unavailable", Ledger.Small, Ledger.Muted, TextAnchor.MiddleCenter);
                Ledger.Label(new Rect(card.x, card.yMax - 29, card.width, 26), female ? "Female" : "Male", Ledger.Body, selected ? Ledger.Accent : Ledger.Text, TextAnchor.MiddleCenter);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none) && !selected) Player.Character.Value = female ? Player.Gender.Female : Player.Gender.Male;
            }
        }

        static readonly string[] _crosshairs = { "None", "Vanilla", "Lines", "Dot" };
        static void CrosshairSelection(ToolLayout l)
        {
            l.Text("Crosshair");
            var r = l.Next(74); float width = (r.width - 24) / 4;
            for (int i = 0; i < _crosshairs.Length; i++)
            {
                var card = new Rect(r.x + i * (width + 8), r.y, width, r.height);
                bool selected = HudSettings.Crosshair.Value == i;
                Ledger.Panel(card, selected ? Ledger.Selected : Ledger.Field, selected ? Ledger.Accent : Ledger.Line);
                WildWasteland.Modules.HUD.CrosshairController.DrawPreview(new Rect(card.center.x - 18, card.y + 5, 36, 36), i);
                Ledger.Label(new Rect(card.x, card.yMax - 29, card.width, 26), _crosshairs[i], Ledger.Body, selected ? Ledger.Accent : Ledger.Text, TextAnchor.MiddleCenter);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none) && !selected) HudSettings.Crosshair.Value = i;
            }
        }

        static void Modules(ToolLayout l)
        {
            l.Text("Turn feature groups on or off. Changes apply immediately.");
            foreach (var e in ModuleHost.Entries)
            {
                bool enabled = GUI.enabled; GUI.enabled = enabled && e.State != ModuleState.OffConflict;
                if (e.Config != null) l.Toggle(e.Config.Enabled, e.Module.DisplayName);
                GUI.enabled = enabled;
                if (e.State == ModuleState.OffConflict) l.Text("Remove the replaced standalone mod to enable this module: " + e.Conflict, Ledger.Danger);
                else if (e.State == ModuleState.InitFailed || e.State == ModuleState.DisabledAfterErrors) l.Text("Could not start. Switch off and on to retry: " + e.LastError, Ledger.Danger);
            }
            l.Heading("Performance");
            Performance(l);
        }

        static bool DebugSetting(ConfigEntryBase e)
        {
            if (ReferenceEquals(e, Player.Character) || ReferenceEquals(e, Player.BodyFirstPerson) || ReferenceEquals(e, Player.FullBodyOnlyOnFoot)
                || ReferenceEquals(e, Player.EmptyHandArms) || ReferenceEquals(e, Player.FirstPersonArmsOnlyOnFoot) || ReferenceEquals(e, Player.OcclusionPrototype)
                || ReferenceEquals(e, Player.ThirdPersonOnFoot) || ReferenceEquals(e, Player.ClimbingOn) || ReferenceEquals(e, Patrols.FuryRoad)
                || ReferenceEquals(e, Patrols.ConvoyEnabled) || ReferenceEquals(e, Patrols.WorldTrafficEnabled) || ReferenceEquals(e, HudSettings.SelectionDot)
                || ReferenceEquals(e, HudSettings.Crosshair) || ReferenceEquals(e, HudSettings.HitMarker) || ReferenceEquals(e, HudSettings.HeadshotSound)
                || ReferenceEquals(e, HudSettings.DamageNumbers) || ReferenceEquals(e, WildWasteland.Modules.Guns.GunsModule.ToggleIronsights)
                || ReferenceEquals(e, WildWasteland.Modules.NPC.NpcModule.ShoutKey) || ReferenceEquals(e, WildWasteland.Modules.NPC.NpcModule.ShoutModifier)) return false;
            foreach (var m in ModuleHost.Entries) if (ReferenceEquals(e, m.Config.Enabled)) return false;
            // tools.cfg holds the fine tuning (offsets, poses, AI and animation constants): it stays in the file and keeps working, but the
            // Debug page only shows its debug switches (logs, overlays, editors)
            if (ReferenceEquals(e.ConfigFile, Plugin.Tools)) return ToolsDebugSwitch(e);
            return true;
        }

        static bool ToolsDebugSwitch(ConfigEntryBase e)
        {
            if (e.SettingType != typeof(bool)) return false;
            string sec = e.Definition.Section, key = e.Definition.Key;
            if (sec.EndsWith("/ Debug", System.StringComparison.Ordinal) || sec.EndsWith("/ Editors", System.StringComparison.Ordinal)) return true;
            return key == "Log" || key.IndexOf("Verbose", System.StringComparison.Ordinal) >= 0 || key == "DebugMap" || key == "Real patrol names" || key == "hide route type";
        }

        static void Debug(ToolLayout l)
        {
            l.Text("Editors, debug switches, gameplay values and diagnostics. Changes are saved immediately unless an editor has its own Save button. Fine tuning (offsets, poses, AI constants) is kept in config/WildWasteland/tools.cfg.");
            l.EntryFilter = DebugSetting; l.DeduplicateEntries = true;
            l.Heading("Tools");
            foreach (var p in _debugPages) if (p.IsTool && IsPatrolsTool(p)) DrawDebugPage(l, p);   // patrol / convoy / merchant editors first
            foreach (var p in _debugPages) if (p.IsTool && !IsPatrolsTool(p)) DrawDebugPage(l, p);
            l.Space(12); l.Heading("Settings and diagnostics");
            foreach (var p in _debugPages) if (!p.IsTool) DrawDebugPage(l, p);
            l.Prefix = "Debug/";
            _settings.Draw(l);
            l.Heading("Harmony patches"); Harmony(l);
            if (l.Button("Write startup summary to the log", true, false, 360)) Log.Always(ModuleHost.Summary());
        }

        static bool IsPatrolsTool(ToolPage p) { return p.Owner != null && p.Owner.Id == "Patrols"; }

        static void DrawDebugPage(ToolLayout l, ToolPage p)
        {
            l.Heading(p.Group + " / " + p.Title);
            l.Prefix = p.Group + "/" + p.Title + "/";
            bool enabled = GUI.enabled;
            bool off = p.Owner != null && !ModuleHost.IsRunning(p.Owner);
            if (off) l.Text(p.Owner.DisplayName + " is off. Settings remain editable; live tools need the module enabled.");
            if (off && !p.ShowWhenModuleOff && !(p is ConfigPage)) GUI.enabled = false;
            try { p.Draw(l); } catch (System.Exception ex) { l.Text("Page error: " + ex.Message, Ledger.Danger); Log.Error("Debug page " + p.Title, ex); }
            finally { GUI.enabled = enabled; }
        }

        static readonly Dictionary<string, double[]> _sum = new Dictionary<string, double[]>();

        static void Performance(ToolLayout l)
        {
            l.Text("Managed time of every Wild Wasteland job, measured by the scheduler. Budget: average <= 0.5 ms per frame, worst <= 2 ms. Times while this window is open are from the paused game; close it and reopen to read live numbers.");
            l.Value("Frames measured", Scheduler.Frames.ToString(CultureInfo.InvariantCulture));
            l.Value("WW average / worst frame (ms)", F(Scheduler.AvgFrameMs) + " / " + F(Scheduler.MaxFrameMs));
            if (l.Button("Reset statistics", true, false, 220)) Scheduler.ResetStats();
            _sum.Clear();
            foreach (var j in Scheduler.AllJobs())
            {
                string owner = j.Owner != null ? j.Owner.Id : "Core";
                double[] s; if (!_sum.TryGetValue(owner, out s)) { s = new double[2]; _sum[owner] = s; }
                s[0] += j.TotalMs; if (j.MaxMs > s[1]) s[1] = j.MaxMs;
            }
            l.Heading("Per module (avg ms/frame, worst job ms)");
            double frames = Mathf.Max(1, Scheduler.Frames);
            foreach (var kv in _sum) l.Value(kv.Key, F(kv.Value[0] / frames) + "   worst " + F(kv.Value[1]));
            l.Heading("Jobs (avg ms/frame, last, worst, calls, errors)");
            foreach (var j in Scheduler.AllJobs())
                l.Value(j.ToString(), F(j.TotalMs / frames) + "  " + F(j.LastMs) + "  " + F(j.MaxMs) + "  " + j.Calls + (j.Exceptions > 0 ? "  ERR " + j.Exceptions : "") + (j.Enabled ? "" : "  (off)"));
        }

        static void Harmony(ToolLayout l)
        {
            l.Text("Every method Wild Wasteland patches. Shared PlayMaker targets go through one Core dispatcher each (Core rows); module rows are targets only that module uses.");
            l.Value("Conflicts", Patches.Conflicts.ToString(CultureInfo.InvariantCulture));
            l.Text(Hooks.Summary());
            string owner = null;
            foreach (var r in Patches.Records)
            {
                if (r.Owner != owner) { owner = r.Owner; l.Heading(owner); }
                l.Text((r.Ok ? "" : "FAILED ") + r.Kind + "  " + r.Target + (r.Ok ? "" : "  - " + r.Error), r.Ok ? (Color?)null : Ledger.Danger);
            }
        }
    }
}
