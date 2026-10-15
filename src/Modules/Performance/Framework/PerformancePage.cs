using System;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// The separate "Performance" tab of the settings window (between Modules and Debug; registered by Core's CorePages).
    /// All Performance settings live here only: the module switch, the preset, every optimization's mode, tuning overrides
    /// and live measurements. They are stored in tools.cfg so they never appear on the Game, Modules list or Debug pages.
    /// </summary>
    public sealed class PerformancePage : ToolPage
    {
        static PerformancePage _instance;
        public static PerformancePage Instance { get { return _instance ?? (_instance = new PerformancePage()); } }
        bool _tuning;

        PerformancePage() { Group = ToolGroups.General; Title = "Performance"; ShowWhenModuleOff = true; }

        static readonly string[] _modes2 = { "Off", "On" };
        static readonly string[] _modes3 = { "Off", "On", "Shadow" };
        static readonly string[] _presets = { "Vanilla", "Low", "Balanced", "Max" };

        public override void Draw(ToolLayout l)
        {
            var m = PerformanceModule.Instance;
            if (m == null) { l.Text("Performance module not loaded."); return; }
            var entry = ModuleHost.Of(m);
            if (entry != null && entry.Config != null) l.Toggle(entry.Config.Enabled, "Performance optimizations");
            bool running = ModuleHost.IsRunning(m);
            l.Text(running
                ? "Optimizations of the base game (issue #133). Each one can be switched off on its own; changes apply immediately. Shadow = the original game code keeps running and the optimized result is only compared (mismatches are counted below and logged)."
                : "The Performance module is off: the game runs unmodified.", running ? (Color?)null : Ledger.Danger);

            var p = PerformanceModule.PresetEntry;
            if (p != null)
            {
                int sel = l.Choices("Preset", (int)p.Value, _presets);
                if (sel != (int)p.Value) p.Value = (PerfPreset)sel;
                l.Text("Preset applies to optimizations that change what is drawn or simulated (marked *). Vanilla keeps them off; Low is fastest, Max closest to the original look.");
            }

            l.Heading("Measurements");
            Telemetry.Draw(l);

            string cat = null;
            foreach (var f in m.Features)
            {
                if (f.Category != cat) { cat = f.Category; l.Heading(cat); }
                var labels = f.SupportsShadow ? _modes3 : _modes2;
                int cur = (int)f.ModeEntry.Value; if (cur >= labels.Length) cur = 1;
                string state = f.Running == FeatureMode.Off ? (f.ModeEntry.Value != FeatureMode.Off && running && f.UsesPreset && PerformanceModule.Preset == PerfPreset.Vanilla ? "  (Vanilla preset)" : "") : "";
                int next = l.Choices(f.PlanId + "  " + f.Title + (f.UsesPreset ? " *" : ""), cur, labels);
                if (next != cur) f.ModeEntry.Value = (FeatureMode)next;
                if (!string.IsNullOrEmpty(f.Help) || state.Length > 0) l.Text(f.Help + state);
                if (f.LastError != null) l.Text("Error: " + f.LastError, Ledger.Danger);
                if (f.Mismatches > 0) l.Text("Shadow mismatches: " + f.Mismatches + " (see log)", Ledger.Danger);
                if (f.Running != FeatureMode.Off)
                {
                    try { f.DrawStats(l); } catch (Exception e) { l.Text("Stats error: " + e.Message, Ledger.Danger); }
                }
            }

            l.Space(10);
            _tuning = l.Toggle("Show tuning overrides (-1 = follow preset)", _tuning);
            if (_tuning)
                foreach (var f in m.Features)
                    foreach (var k in f.Knobs) l.Entry(k.Override);
        }
    }
}
