using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace WildWasteland.Core.UI
{
    /// <summary>Pages owned by Core: module overview (on/off), performance, Harmony inventory, core settings.</summary>
    internal static class CorePages
    {
        internal static void Register()
        {
            ToolWindow.Add(new ActionPage(null, ToolGroups.General, "Modules", Modules));
            ToolWindow.Add(new ActionPage(null, ToolGroups.Debug, "Performance", Performance));
            ToolWindow.Add(new ActionPage(null, ToolGroups.Debug, "Harmony patches", Harmony));
            ToolWindow.Add(new ConfigPage(null, ToolGroups.Debug, "Core settings", Plugin.Tools, "Core") { Intro = "Shared scheduler settings. Sliced work (nav bakes, scene scans) yields once the frame budget is used." });
        }

        static string F(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }

        static void Modules(ToolLayout l)
        {
            l.Text("Every feature group of Wild Wasteland. The same switches are in the F6 Mods menu (Wild Wasteland page). Turning a module off stops it at once and removes its patches; other modules keep working and fall back to their standalone behaviour.");
            foreach (var e in ModuleHost.Entries)
            {
                l.Heading(e.Module.DisplayName);
                string state;
                switch (e.State)
                {
                    case ModuleState.Running: state = "RUNNING"; break;
                    case ModuleState.OffByConfig: state = "off"; break;
                    case ModuleState.OffConflict: state = "OFF - standalone mod installed: " + e.Conflict + " (remove it, Wild Wasteland replaces it)"; break;
                    case ModuleState.InitFailed: state = "FAILED to start: " + e.LastError; break;
                    case ModuleState.DisabledAfterErrors: state = "DISABLED after repeated errors: " + e.LastError; break;
                    default: state = e.State.ToString(); break;
                }
                l.Value("State", state);
                l.Value("Patches / errors", Patches.Count(e.Module.Id) + " / " + e.Errors);
                if (e.Config != null && e.State != ModuleState.OffConflict) l.Toggle(e.Config.Enabled, "Enabled");
            }
            l.Space();
            if (l.Button("Write startup summary to the log", true, false, 360)) Log.Always(ModuleHost.Summary());
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
