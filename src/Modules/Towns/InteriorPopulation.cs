using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    // Applies to native interior groups throughout the world while the addon is loaded.
    // Native FSMs still own creation, registration, names and one-shot progress.
    internal static class InteriorPopulation
    {
        private sealed class Group { internal PlayMakerFSM Fsm; internal TownSite Town; internal bool Started, Suspended; internal int Attempts; internal float StartedAt; }
        private sealed class Member { internal GameObject Owner; internal FsmFloat Health; internal HashSet<string> Hostile; internal object Agent; }
        private static readonly Dictionary<PlayMakerFSM, Group> Groups = new Dictionary<PlayMakerFSM, Group>();
        private static readonly List<Group> Pending = new List<Group>();
        private static readonly Dictionary<int, Member> Members = new Dictionary<int, Member>();
        private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<int> Fresh = new HashSet<int>();
        private static readonly List<int> Dead = new List<int>();
        private static FieldInfo _owner, _ambush, _hostile, _agents;
        private static FieldInfo _ghost, _priority, _state;
        private static MethodInfo _release;
        private static int _next, _near;
        private static Vector3 _player;
        private static bool _havePlayer;
        internal static void Install(TownsModule module)
        {
            NativePoiPopulation.Install(module);
            var senses = typeof(Services).Assembly.GetType("WildWasteland.Modules.NPC.Senses", true);
            var agent = senses.GetNestedType("Agent", BindingFlags.NonPublic);
            _owner = agent.GetField("Owner"); _ambush = agent.GetField("AmbushDist"); _hostile = agent.GetField("Hostile");
            _agents = senses.GetField("_agents", BindingFlags.Static | BindingFlags.NonPublic);
            _ghost = agent.GetField("Ghost"); _priority = agent.GetField("GhostPrio"); _state = agent.GetField("State"); _release = Patches.Method(senses, "Release");
            if (!Patches.Patch(module, Patches.Method(senses, "Assign"), prefix: Patches.Method(typeof(InteriorPopulation), "Assign"))
                || !Patches.Patch(module, Patches.Method(senses, "Look"), prefix: Patches.Method(typeof(InteriorPopulation), "Look"))
                || !Patches.Patch(module, Patches.Method(senses, "FoughtFor"), prefix: Patches.Method(typeof(InteriorPopulation), "Fought")))
                throw new InvalidOperationException("Interior population hearing bridge could not be installed");
            Hooks.FsmEnable.Postfix(module, Enabled); Hooks.CreateObjectEnter.Postfix(module, Created);
            Scheduler.Sliced(module, "Interior pre-spawn", 700, Step);
            Scheduler.Interval(module, "Interior hearing", .25f, Hearing);
        }
        internal static void DiscoverExisting()
        { foreach (var f in PlayMakerFSM.FsmList) if (f != null) Enabled(f); }
        private static bool Interior(PlayMakerFSM f)
        {
            if (f == null || f.FsmName != "ItemSpawner" || !f.name.StartsWith("EnemySpawn", StringComparison.Ordinal)) return false;
            foreach (var collider in f.GetComponents<Collider>())
                if (collider.isTrigger && collider.enabled && (!(collider is SphereCollider) || collider.bounds.extents.magnitude < 70)) return true;
            return false;
        }
        private static void Enabled(PlayMakerFSM f)
        {
            if (!f.gameObject.scene.IsValid() || !Interior(f)) return;
            for (var t = f.transform; t != null; t = t.parent) if (TownMarkers.Owns(t)) return;
            Add(f, null);
        }
        private static void Add(PlayMakerFSM f, TownSite town)
        {
            Group group;
            if (Groups.TryGetValue(f, out group)) { if (town != null) group.Town = town; if (!Pending.Contains(group)) { group.Started = false; Pending.Add(group); } return; }
            group = new Group { Fsm = f, Town = town }; Groups.Add(f, group); Pending.Add(group);
        }
        internal static void OwnTown(TownSite site, int lot)
        {
            for (int n = 0; n < site.Spawners[lot].Length; n++)
            {
                var f = site.Spawners[lot][n];
                if (f != null && f.gameObject.name.StartsWith("EnemySpawn", StringComparison.Ordinal)
                    && (f.gameObject.name.IndexOf("boss", StringComparison.OrdinalIgnoreCase) < 0 || f.gameObject.name.IndexOf("bodyguard", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    Add(f, site); var group = Groups[f];
                    group.Suspended = Value(f, "OnOff") == 0 && site.PopulationCounts[lot][n] > Value(f, "number");
                    if (group.Suspended) f.enabled = false;
                }
            }
        }
        private static int Value(PlayMakerFSM f, string name)
        { var value = f.FsmVariables.FindFsmInt(name); return value == null ? 0 : value.Value; }
        internal static bool TownReady(TownSite site)
        {
            foreach (var group in Pending) if (group.Town == site && group.Fsm != null && group.Fsm.gameObject.activeInHierarchy && (group.Fsm.enabled || group.Suspended) && Value(group.Fsm, "OnOff") == 0) return false;
            return true;
        }
        internal static bool Ready
        {
            get { foreach (var group in Pending) if (group.Fsm != null && group.Fsm.gameObject.activeInHierarchy && (group.Fsm.enabled || group.Suspended) && Value(group.Fsm, "OnOff") == 0) return false; return true; }
        }
        private static bool CanRun()
        { return !SaveSystem.Busy && !SaveBus.Saving && (GameRefs.InGame || GameRefs.SaveLoadState == "Wait for Terrain Gen"); }
        private static float _spawnAfter;   // a native group spawn is one synchronous frame cost: space them like building placements during play
        private static bool Step()
        {
            if (Pending.Count == 0 || !CanRun() || Time.unscaledTime < _spawnAfter) return false;
            if (_next >= Pending.Count) _next = 0;
            for (int i = 0; i < Pending.Count; i++)
            {
                int at = _next++ % Pending.Count; var group = Pending[at]; var f = group.Fsm;
                if (f == null || !f.gameObject.activeInHierarchy || !f.enabled && !group.Suspended || Value(f, "OnOff") > 0) { Pending.RemoveAt(at); return true; }
                if (group.Town != null && group.Town.NextLot < group.Town.Plan.LotCount) continue;
                if (group.Suspended) { group.Suspended = false; f.enabled = true; }
                if (group.Started)
                {
                    if (Time.unscaledTime - group.StartedAt < 2) continue;
                    if (group.Attempts >= 2 || f.ActiveStateName != "wait" && f.ActiveStateName != "trigger")
                    { f.enabled = false; Pending.RemoveAt(at); Log.Warn("[Towns] stopped stalled interior spawn group " + f.gameObject.name + "; existing actors retained"); return true; }
                    group.Started = false;
                }
                if (!f.Fsm.Initialized || f.ActiveStateName != "wait" && f.ActiveStateName != "trigger") continue;
                // Respect authored completed-boss flags before bypassing the wait/entry trigger.
                bool completed = false;
                foreach (var state in f.FsmStates) if (state.Name == "wait") foreach (var action in state.Actions)
                {
                    var test = action as BoolTest;
                    if (test != null && test.boolVariable.Value && test.isTrue != null && test.isTrue.Name == "back") completed = true;
                }
                if (completed) { f.Fsm.SetState("spawned"); return true; }
                TriggerEvent trigger = null;
                foreach (var state in f.FsmStates) if (state.Name == "trigger") foreach (var action in state.Actions)
                    if (action is TriggerEvent) trigger = (TriggerEvent)action;
                if (trigger == null || trigger.sendEvent == null) continue;
                group.Started = true; group.Attempts++; group.StartedAt = Time.unscaledTime;
                // Run the authored random roll and its actual trigger transition, not a guessed spawn count/state.
                f.Fsm.SetState("trigger"); f.Fsm.Event(trigger.sendEvent);
                _spawnAfter = Time.unscaledTime + (GameRefs.InGame ? .04f : 0);
                Log.Always("[Towns] interior pre-spawn " + f.transform.parent.name + "/" + f.gameObject.name + "; native sequence retained.");
                return true;
            }
            return false;
        }
        private static void Created(CreateObject action)
        {
            if (action.Fsm == null || action.Fsm.Name != "ItemSpawner" || action.storeObject == null) return;
            var f = action.Fsm.Owner as PlayMakerFSM; Group group;
            if (f == null || !Groups.TryGetValue(f, out group)) return;
            var made = action.storeObject.Value; if (made == null || FsmUtil.Find(made, "Health") == null || FsmUtil.Find(made, "Detection") == null) return;
            Fresh.Add(made.GetInstanceID());
            if (group.Town != null) TownPopulation.Created(group.Town, made);
            var health = FsmUtil.Find(made, "Health"); Members[made.GetInstanceID()] = new Member { Owner = made, Health = health.FsmVariables.FindFsmFloat("Health") };
        }
        private static Member Observe(object agent)
        {
            var owner = (GameObject)_owner.GetValue(agent); if (owner == null) return null;
            int id = owner.GetInstanceID(); Member member;
            if (!Members.TryGetValue(id, out member))
            {
                var health = FsmUtil.Find(owner, "Health");
                member = new Member { Owner = owner, Health = health == null ? null : health.FsmVariables.FindFsmFloat("Health") };
                Members[id] = member;
            }
            member.Agent = agent; member.Hostile = (HashSet<string>)_hostile.GetValue(agent); return member;
        }
        private static bool Tracked(GameObject owner)
        { return owner != null && (Fresh.Contains(owner.GetInstanceID()) || Names.Contains(owner.name)); }
        private static bool Deaf(object agent)
        {
            var owner = (GameObject)_owner.GetValue(agent);
            if (!Tracked(owner)) return false;
            // Far interior groups remain quiet; only the player's immediate encounter unlocks hearing.
            return !_havePlayer || _near >= 3 || (owner.transform.position - _player).sqrMagnitude > 2500;
        }
        private static bool Assign(object __0, object __2, ref bool __result)
        { int src = Convert.ToInt32(__2); if (src < 1 || src > 4 || !Deaf(__0)) return true; __result = false; return false; }
        private static void Look(object __0)
        { if (Tracked((GameObject)_owner.GetValue(__0))) _ambush.SetValue(__0, 0f); }
        private static bool Fought(object __0, string __3)
        { return __3 != "hears his shots while fighting him" || !Deaf(__0); }
        private static void Hearing()
        {
            var player = GameRefs.Player; _havePlayer = player != null; if (!_havePlayer) return;
            int previous = _near; _player = player.transform.position; _near = 0; Dead.Clear();
            foreach (object agent in ((IDictionary)_agents.GetValue(null)).Values)
            {
                var member = Observe(agent); if (member == null) continue;
                if (Tracked(member.Owner)) { Names.Add(member.Owner.name); _ambush.SetValue(agent, 0f); }
                if (_near < 3 && member.Owner.activeInHierarchy && member.Health != null && member.Health.Value > 0
                    && member.Hostile != null && member.Hostile.Contains("Player") && (member.Owner.transform.position - _player).sqrMagnitude <= 2500) _near++;
            }
            foreach (var pair in Members) if (pair.Value.Owner == null) Dead.Add(pair.Key);
            foreach (int id in Dead) { Members.Remove(id); Fresh.Remove(id); }
            foreach (var member in Members.Values)
            {
                if (member.Agent == null || !Tracked(member.Owner) || !Deaf(member.Agent) || _ghost.GetValue(member.Agent) == null) continue;
                int priority = Convert.ToInt32(_priority.GetValue(member.Agent)), state = Convert.ToInt32(_state.GetValue(member.Agent));
                if (priority < 1 || priority > 4 || state != 2 && state != 3) continue;
                _release.Invoke(null, new[] { member.Agent }); _state.SetValue(member.Agent, Enum.ToObject(_state.FieldType, 0));
            }
            if ((previous >= 3) != (_near >= 3) && Names.Count > 0)
                Log.Always("[Towns] interior hearing: " + (_near >= 3 ? "3+ enemies within 50 m; sounds ignored" : _near + " enemies within 50 m; nearby hearing enabled"));
        }
        internal static void Clear()
        { NativePoiPopulation.Clear(); Groups.Clear(); Pending.Clear(); Members.Clear(); Fresh.Clear(); Names.Clear(); _next = _near = 0; _havePlayer = false; }
        internal static void Load(string path)
        { RestoreNames(File.Exists(path) ? File.ReadAllLines(path) : new string[0]); }
        internal static void RestoreNames(string[] names)
        { Clear(); foreach (string name in names) if (!string.IsNullOrEmpty(name)) Names.Add(name); }
        internal static string[] CaptureNames()
        {
            foreach (var member in Members.Values) if (member.Owner != null && Tracked(member.Owner)) Names.Add(member.Owner.name);
            var rows = new List<string>(Names); rows.Sort(StringComparer.Ordinal); return rows.ToArray();
        }
    }
}
