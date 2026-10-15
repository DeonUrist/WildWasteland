using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.QuestsCompat
{
    public sealed class QuestsCompatModule : ModuleBase
    {
        private static QuestsCompatModule _owner;
        private static object _talk, _quests;
        private static Action<object> _talkUpdate, _talkLate, _talkClose, _questUpdate, _questLate, _questGui, _questTick, _save, _load, _reset, _bookClear;
        private static Func<object, bool> _talking, _ready;
        private static Func<object, BepInEx.Configuration.ConfigEntry<bool>> _pauseTalk;
        private static readonly EditorSession DialogueSession = new EditorSession();
        private static Func<object, object> _book;
        private static Func<object, object> _bridge;
        private static Func<object, Transform> _dialogueNpc;
        private static Func<object, IList> _buttons;
        private static Func<object, string> _buttonKey;
        private static Func<object, MethodInfo> _immersiveAdd;
        private static Action<object, bool> _setReady;
        private static Action<object, PlayMakerFSM> _saveFsm;
        private static Action<object, bool> _hadSaveFsm;
        private static Action<object, string> _saveState;
        private static bool _dispatch, _tickDispatch, _splitDispatch, _newWorld;
        private static Action<Vector3> _neutralize;
        private static Action<object, Vector3> _giverTick;
        private static Action<object> _syncMarkers, _reputation;
        private static Func<Transform> _player;
        private static Func<object, IList> _toasts;
        private static Func<object, List<string>> _trackerLines;
        private static Func<object, bool> _showLog;
        private static Func<object, BepInEx.Configuration.ConfigEntry<bool>> _showTracker;
        private static int _generation;
        private static readonly List<PlayMakerFSM> Items = new List<PlayMakerFSM>(), People = new List<PlayMakerFSM>();
        private static List<PlayMakerFSM> _nextItems = new List<PlayMakerFSM>(), _nextPeople = new List<PlayMakerFSM>();
        private static int _scan, _scanRevision;
        private static bool _scanReady;
        private static float _scanAt;
        private static readonly Dictionary<string, Marker> Markers = new Dictionary<string, Marker>();
        private static readonly List<string> Removed = new List<string>();
        private const string MarkerOwner = "QuestsCompat";
        private sealed class Marker
        {
            internal Vector3 Position; internal Transform Target; internal string Label, Icon;
            internal Color Color; internal int Compass, Map; internal ICompassMarkers CompassApi; internal IMapMarkers MapApi;
            internal Vector3 Scene() { return Target != null ? Target.position : Origin.Scene(Position); }
        }
        public override string Id { get { return "QuestsCompat"; } }
        public override string DisplayName { get { return "Talk & Quests compatibility"; } }
        public override void BindConfig(ModuleConfig cfg) { }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public override void Init()
        {
            _owner = this; _generation++; _newWorld = false; _dispatch = _tickDispatch = _splitDispatch = false;
            ClearScan(); CrewHistory.Clear();
            PluginInfo info;
            if (Chainloader.PluginInfos.TryGetValue("com.example.apocalypter.talk", out info) && !ReferenceEquals(info.Instance, null))
            {
                if (info.Metadata.Version != new Version(2, 0, 0)) throw new NotSupportedException("Talk compatibility requires Apocatalker 2.0.0");
                _talk = info.Instance; var type = _talk.GetType();
                _talkUpdate = Binding.Call(type, "Update"); _talkLate = Binding.Call(type, "LateUpdate"); _talkClose = Binding.Call(type, "Close");
                _talking = Binding.Get<bool>(type, "Talking");
                _pauseTalk = Binding.Get<BepInEx.Configuration.ConfigEntry<bool>>(type, "pauseWhileTalking");
                Patch(type, "Update", "ScheduledOnly"); Patch(type, "LateUpdate", "ScheduledOnly");
                Patch(type, "Open", "BeforeOpen");
                Patch(type, "Open", postfix: "AfterOpen"); Patch(type, "Close", postfix: "AfterClose");
                Patch(type, "GetCam", "TalkCamera");
                Patch(type, "OnDestroy", "KeepDialogueModel");
                var api = type.Assembly.GetType("ApocalypterTalk.TalkApi", true);
                PluginLifetime.Install(this, api, "LoadFile"); PluginLifetime.Install(this, api, "Reload");
                Scheduler.EveryFrame(this, "Talk frame", TalkFrame);
                Scheduler.EveryFrame(this, "Talk cursor", TalkLate, Phase.LateUpdateLast);
                Log.Always("[QuestsCompat] Apocatalker 2.0.0 scheduled; original story profiles retained");
            }
            if (Chainloader.PluginInfos.TryGetValue("local.apocalypterquests", out info) && !ReferenceEquals(info.Instance, null))
            {
                if (info.Metadata.Version != new Version(1, 8, 0)) throw new NotSupportedException("Quest compatibility requires Apocalypter Quests 1.8.0");
                _quests = info.Instance; var type = _quests.GetType(); var asm = type.Assembly;
                _questUpdate = Binding.Call(type, "Update"); _questLate = Binding.Call(type, "LateUpdate"); _questGui = Binding.Call(type, "OnGUI"); _questTick = Binding.Call(type, "Tick");
                _save = Binding.Call(type, "SaveBook"); _load = Binding.Call(type, "LoadBook"); _reset = Binding.Call(type, "ResetSession");
                _ready = Binding.Get<bool>(type, "ready"); _setReady = Binding.Set<bool>(type, "ready"); _book = Binding.Get<object>(type, "book");
                _bookClear = Binding.Call(asm.GetType("ApocalypterQuests.QuestBook", true), "Clear");
                _saveFsm = Binding.Set<PlayMakerFSM>(type, "saveFsm"); _hadSaveFsm = Binding.Set<bool>(type, "hadFsm"); _saveState = Binding.Set<string>(type, "lastState");
                Patch(type, "Update", "ScheduledOnly"); Patch(type, "LateUpdate", "ScheduledOnly"); Patch(type, "OnGUI", "ScheduledOnly");
                Patch(type, "Tick", "TickOnly"); Patch(type, "TrackSaveLoad", "SkipWatcher");
                var map = asm.GetType("ApocalypterQuests.MapLink", true);
                _immersiveAdd = Binding.Get<MethodInfo>(map, "addQ");
                Patch(map, "get_Available", null, "MapAvailable"); Patch(map, "Sync", "ForwardMap"); Patch(map, "Reset", null, "ClearMarkers");
                var game = asm.GetType("ApocalypterQuests.Game", true);
                DeliveryRecovery.Install(this, asm, _quests, _talk);
                Patch(game, "FindItems", transpiler: "ItemScan");
                foreach (var name in new[] { "FindPeople", "FindEnemies", "FindBodies", "LiveNames", "FindNpcNames" }) Patch(game, name, transpiler: "PeopleScan");
                var hire = asm.GetType("ApocalypterQuests.Hire", true);
                if (!Core.Patches.Patch(this, AccessTools.Method(hire, "NearestByName"), transpiler: AccessTools.Method(typeof(QuestsCompatModule), "PeopleScan")))
                    Log.Warn("[QuestsCompat] Hire search keeps the full FSM list walk");
                // Quests' 0.5 s Tick runs these four in one frame; each now has its own randomly phased job (internal timers unchanged).
                var takeover = asm.GetType("ApocalypterQuests.Takeover", true);
                _neutralize = (Action<Vector3>)Delegate.CreateDelegate(typeof(Action<Vector3>), AccessTools.Method(takeover, "NeutralizeTick"));
                var giver = AccessTools.Method(takeover, "GiverTick"); var bookArg = System.Linq.Expressions.Expression.Parameter(typeof(object), "b");
                var playerArg = System.Linq.Expressions.Expression.Parameter(typeof(Vector3), "p");
                _giverTick = System.Linq.Expressions.Expression.Lambda<Action<object, Vector3>>(System.Linq.Expressions.Expression.Call(giver,
                    System.Linq.Expressions.Expression.Convert(bookArg, giver.GetParameters()[0].ParameterType), playerArg), bookArg, playerArg).Compile();
                _syncMarkers = Binding.Call(type, "SyncMapMarkers"); _reputation = Binding.Call(type, "ReputationTick");
                _player = (Func<Transform>)Delegate.CreateDelegate(typeof(Func<Transform>), AccessTools.Method(game, "Player"));
                Patch(takeover, "NeutralizeTick", "SplitOnly"); Patch(takeover, "GiverTick", "SplitOnly");
                Patch(type, "SyncMapMarkers", "SplitOnly"); Patch(type, "ReputationTick", "SplitOnly");
                _toasts = Binding.Get<IList>(type, "toasts"); _trackerLines = Binding.Get<List<string>>(type, "trackerLines");
                _showLog = Binding.Get<bool>(type, "showLog"); _showTracker = Binding.Get<BepInEx.Configuration.ConfigEntry<bool>>(type, "showTracker");
                QuestsPerf.Install(this, asm, _talk, _talking);
                QuestFollower.Install(this, asm, _quests);
                _bridge = Binding.Get<object>(type, "bridge"); _dialogueNpc = Binding.Get<Transform>(asm.GetType("ApocalypterQuests.TalkBridge", true), "Npc");
                _buttons = Binding.Get<IList>(type, "wantButtons"); _buttonKey = Binding.Get<string>(asm.GetType("ApocalypterQuests.TalkBtn", true), "Key");
                Patch(type, "BuildButtons", postfix: "CrewButtons"); Patch(type, "HireNpc", "HireAllowed");
                Patch(type, "AskForWork", "WorkAllowed"); Patch(type, "AcceptQuest", "WorkAllowed"); Patch(type, "RunTalkAction", "CrewTalkAction");
                var hooks = asm.GetType("ApocalypterQuests.Hooks", true);
                foreach (var name in new[] { "OpenPostfix", "ClosePostfix", "CreateObjectPostfix" }) PluginLifetime.Install(this, hooks, name);
                SaveBus.StateChanged += StateChanged;
                Services.CrewBailedOut += CrewHistory.Bailed;
                Scheduler.EveryFrame(this, "Quest frame", QuestFrame);
                Scheduler.EveryFrame(this, "Quest poses", QuestLate, Phase.LateUpdateLast);
                Scheduler.Gui(this, "Quest windows", QuestGui, QuestGuiWanted);   // (WW perf) IMGUI off while Quests has nothing to draw
                Scheduler.Interval(this, "Quest logic", .5f, QuestTick);
                Scheduler.Interval(this, "Quest camps", .5f, CampTick);
                Scheduler.Interval(this, "Quest givers", .5f, GiverTick);
                Scheduler.Interval(this, "Quest map sync", .5f, MapTick);
                Scheduler.Interval(this, "Quest reputation", .5f, ReputationTick);
                Scheduler.Sliced(this, "World cache", 500, ScanStep);
                Scheduler.Interval(this, "Marker providers", .5f, ReconcileMarkers);
                if (SaveBus.State != null) StateChanged(null, SaveBus.State);
                Log.Always("[QuestsCompat] Apocalypter Quests 1.8.0 scheduled; ES3 key ApocalypterQuests retained");
            }
            if (_talk == null && _quests == null) Log.Always("[QuestsCompat] Talk/Quests not loaded; compatibility idle");
        }
        internal static void Patch(Type type, string name, string prefix = null, string postfix = null, string transpiler = null)
        {
            if (!Core.Patches.Patch(_owner, AccessTools.Method(type, name),
                prefix: prefix == null ? null : AccessTools.Method(typeof(QuestsCompatModule), prefix),
                postfix: postfix == null ? null : AccessTools.Method(typeof(QuestsCompatModule), postfix),
                transpiler: transpiler == null ? null : AccessTools.Method(typeof(QuestsCompatModule), transpiler)))
                throw new InvalidOperationException("Compatibility hook failed: " + type.Name + "." + name);
        }
        private static bool ScheduledOnly() { return _dispatch; }
        private static bool VehicleCrew(Transform npc)
        {
            if (npc == null || npc.parent == null || Services.FollowerVehicles == null) return false;
            var pose = Services.FollowerVehicles.ResolveType("Apocapatrol.Pose");
            return pose != null && npc.GetComponent(pose) != null;
        }
        private static bool WorkAllowed(object __instance) { return !CrewHistory.Blocked(_dialogueNpc(_bridge(__instance))); }
        private static bool HireAllowed(Transform __2) { return !VehicleCrew(__2); }
        private static bool CrewTalkAction(object __instance, string __0) { return __0 != "quest.give" || WorkAllowed(__instance); }
        private static void CrewButtons(object __instance)
        {
            var npc = _dialogueNpc(_bridge(__instance));
            bool seated = VehicleCrew(npc), blocked = CrewHistory.Blocked(npc);
            if (!seated && !blocked) return;
            var buttons = _buttons(__instance);
            for (int i = buttons.Count - 1; i >= 0; i--)
            {
                string key = _buttonKey(buttons[i]);
                if ((blocked && (key == "work" || key == "accept")) || (seated && key == "hire")) buttons.RemoveAt(i);
            }
        }
        private static bool TickOnly() { return _tickDispatch; }
        private static bool SplitOnly() { return _splitDispatch || !_tickDispatch; }
        private static bool SkipWatcher() { return false; }
        private static bool KeepDialogueModel() { return false; }
        private static bool TalkCamera(ref Camera __result) { __result = GameRefs.MainCamera; return false; }
        private static bool BeforeOpen() { return GameRefs.InGame && DialogueSession.Acquire(_pauseTalk(_talk).Value); }
        private static void AfterOpen() { if (!_talking(_talk)) DialogueSession.Close(); }
        private static void AfterClose() { DialogueSession.Close(); }
        private static void Run(Action<object> action, object instance)
        { _dispatch = true; try { action(instance); } finally { _dispatch = false; } }
        private static void TalkFrame()
        {
            if (_talking(_talk) || (!EditorSession.Busy && !ApocasetterBridge.InputBlocked && GameRefs.InPlay)) Run(_talkUpdate, _talk);
        }
        private static void TalkLate() { if (_talking(_talk)) { Run(_talkLate, _talk); DialogueSession.Maintain(); } }
        private static void QuestFrame() { if (!_ready(_quests) || _scanReady) Run(_questUpdate, _quests); QuestFollower.Tick(); }
        private static void QuestLate() { Run(_questLate, _quests); QuestFollower.Late(); }
        private static void QuestGui()
        {
            // Quests' OnGUI only draws labels/textures (no controls or input), so the Repaint event is the only one that matters.
            var e = Event.current; if (EditorSession.Busy || e == null || e.type != EventType.Repaint || !_ready(_quests)) return;
            if (_toasts(_quests).Count == 0 && !_showLog(_quests) && (!_showTracker(_quests).Value || _trackerLines(_quests).Count == 0)) return;
            Run(_questGui, _quests);
        }
        // QuestGui's skip test without the event check: true while a toast is up (Quests expires them inside its OnGUI), the J window
        // is open (native when CleanHUD is off) or the tracker has lines. showLog and toasts change in Quests' Update, before this runs.
        private static bool QuestGuiWanted()
        {
            return !EditorSession.Busy && _ready(_quests)
                && (_toasts(_quests).Count > 0 || _showLog(_quests) || (_showTracker(_quests).Value && _trackerLines(_quests).Count > 0));
        }
        private static void QuestTick()
        {
            if (!_ready(_quests) || !_scanReady || !GameRefs.InGame) return;
            DeliveryRecovery.Tick(People, _scanRevision);
            _tickDispatch = true; try { _questTick(_quests); } finally { _tickDispatch = false; }
        }
        private static bool SplitReady() { return _ready(_quests) && _scanReady && GameRefs.InGame; }
        private static void Split(int which)
        {
            if (!SplitReady()) return;
            var player = _player(); if (player == null) return;
            _splitDispatch = true;
            try
            {
                if (which == 0) _neutralize(player.position);
                else if (which == 1) { QuestsPerf.GiverDispatch = true; _giverTick(_book(_quests), player.position); }
                else if (which == 2) _syncMarkers(_quests);
                else _reputation(_quests);
            }
            catch (Exception e) { Log.Warn("[QuestsCompat] Quests " + (which == 0 ? "NeutralizeTick" : which == 1 ? "GiverTick" : which == 2 ? "MapMarkers" : "ReputationTick") + ": " + e.Message); }
            finally { _splitDispatch = false; QuestsPerf.GiverDispatch = false; if (which == 1) QuestsPerf.ReleaseObjects(); }
        }
        private static void CampTick() { Split(0); }
        private static void GiverTick() { Split(1); }
        private static void MapTick() { Split(2); }
        private static void ReputationTick() { Split(3); }
        private static void StateChanged(string previous, string state)
        {
            if (_quests == null) return;
            if (state == "LoadGame" || state == "LoadVar" || state == "generateTerrain 2" || state == "loaded" || state == "setSeed" || state == "generateTerrain")
            {
                if (state == "LoadGame" || state == "setSeed") CrewHistory.Begin(state == "setSeed");
                if (_ready(_quests)) { _setReady(_quests, false); _reset(_quests); _generation++; ClearScan(); }
                _newWorld = state == "setSeed" || state == "generateTerrain";
                if (_newWorld) _bookClear(_book(_quests));
            }
            else if (state == "isPlay" && !_ready(_quests))
            {
                CrewHistory.Load(_newWorld);
                _setReady(_quests, true); if (!_newWorld) _load(_quests);
                Log.Always("[QuestsCompat] Quest world ready; original save/profile rules active");
            }
            else if (state == "SaveGame" && _ready(_quests))
            {
                string slot = SaveBus.Slot; int generation = _generation;
                var crew = CrewHistory.Snapshot();
                SaveBus.AfterSave(() => { if (_quests != null && generation == _generation && slot == SaveBus.Slot) { CrewHistory.Save(slot, crew); _save(_quests); } });
            }
        }
        private static void ClearScan() { Items.Clear(); People.Clear(); _nextItems.Clear(); _nextPeople.Clear(); _scan = _scanRevision = 0; _scanAt = 0; _scanReady = false; DeliveryRecovery.Clear(); QuestsPerf.Clear(); }
        private static bool ScanStep()
        {
            if (_quests == null || !_ready(_quests) || !GameRefs.InPlay || Time.unscaledTime < _scanAt) return false;
            var fsms = PlayMakerFSM.FsmList;
            if (fsms == null) return false;
            if (_scan >= fsms.Count)
            {
                Items.Clear(); Items.AddRange(_nextItems); People.Clear(); People.AddRange(_nextPeople);
                _scanReady = true; _scanRevision++;
                _nextItems.Clear(); _nextPeople.Clear(); _scan = 0; _scanAt = Time.unscaledTime + .5f; return false;
            }
            for (int end = Math.Min(fsms.Count, _scan + 32); _scan < end; )
            {
                var f = fsms[_scan++];
                if (f == null) continue;
                string name = f.FsmName;
                if (name == "ItemName") _nextItems.Add(f);
                else if (name == "Health" || name == "ID") { _nextPeople.Add(f); if (name == "Health") CrewHistory.Observe(f.gameObject); }
            }
            return true;
        }
        private static List<PlayMakerFSM> CachedItems() { return Items; }
        private static List<PlayMakerFSM> CachedPeople() { return People; }
        private static IEnumerable<CodeInstruction> ReplaceScan(IEnumerable<CodeInstruction> code, string getter)
        {
            foreach (var c in code)
            {
                var m = c.operand as MethodInfo;
                if (m != null && m.DeclaringType == typeof(PlayMakerFSM) && m.Name == "get_FsmList")
                { c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(QuestsCompatModule), getter); }
                yield return c;
            }
        }
        private static IEnumerable<CodeInstruction> ItemScan(IEnumerable<CodeInstruction> code) { return ReplaceScan(code, "CachedItems"); }
        private static IEnumerable<CodeInstruction> PeopleScan(IEnumerable<CodeInstruction> code) { return ReplaceScan(code, "CachedPeople"); }
        private static void MapAvailable(ref bool __result) { __result |= Services.Map != null || Services.Compass != null; }
        private static bool ForwardMap(Dictionary<string, Vector3> __0)
        { MapSync(__0); return _immersiveAdd(null) != null; }
        private static void MapSync(Dictionary<string, Vector3> __0)
        {
            Removed.Clear(); foreach (var key in Markers.Keys) if (!__0.ContainsKey(key)) Removed.Add(key);
            foreach (var key in Removed) { RemoveMarker(Markers[key]); Markers.Remove(key); }
            foreach (var kv in __0)
            {
                Marker m;
                if (!Markers.TryGetValue(kv.Key, out m))
                {
                    bool turnIn = kv.Key.StartsWith("T:", StringComparison.Ordinal);
                    m = new Marker { Label = (turnIn ? "Report in: " : "Quest: ") + kv.Key.Substring(2), Icon = "flag", Color = QuestColor(kv.Key.Substring(2)) };
                    Markers.Add(kv.Key, m);
                }
                m.Position = Origin.World(kv.Value);
                QuestFollower.DescribeMarker(kv.Key, ref m.Label, out m.Target);
            }
            ReconcileMarkers();
        }
        private static Color QuestColor(string id)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < id.Length; i++) hash = unchecked((hash ^ id[i]) * 16777619u);
            return Color.HSVToRGB((hash % 360u) / 360f, .6f, 1f);
        }
        private static void ReconcileMarkers()
        {
            foreach (var m in Markers.Values)
            {
                if (m.CompassApi != Services.Compass) { if (m.CompassApi != null) m.CompassApi.Remove(m.Compass); m.CompassApi = Services.Compass; m.Compass = 0; }
                if (m.MapApi != Services.Map) { if (m.MapApi != null) m.MapApi.Remove(m.Map); m.MapApi = Services.Map; m.Map = 0; }
                if (m.CompassApi != null && !m.CompassApi.Exists(m.Compass)) m.Compass = m.CompassApi.Add(MarkerOwner, m.Scene, m.Icon, m.Color);
                if (m.MapApi != null && !m.MapApi.Exists(m.Map)) m.Map = m.MapApi.Add(MarkerOwner, m.Scene, m.Icon, m.Color, m.Label);
            }
        }
        private static void RemoveMarker(Marker m) { if (m.CompassApi != null) m.CompassApi.Remove(m.Compass); if (m.MapApi != null) m.MapApi.Remove(m.Map); }
        private static void ClearMarkers() { foreach (var m in Markers.Values) RemoveMarker(m); Markers.Clear(); }
        public override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        { _generation++; ClearMarkers(); ClearScan(); QuestFollower.Release(); if (_quests != null) { _setReady(_quests, false); _reset(_quests); } if (_talk != null && _talking(_talk)) _talkClose(_talk); _newWorld = false; }
        public override void Shutdown()
        {
            _generation++; SaveBus.StateChanged -= StateChanged; ClearMarkers(); ClearScan();
            Services.CrewBailedOut -= CrewHistory.Bailed;
            if (_quests != null && _reset != null) _reset(_quests);
            QuestFollower.Uninstall();
            if (_quests != null && _saveFsm != null) { _saveFsm(_quests, GameRefs.SaveLoadGame); _hadSaveFsm(_quests, GameRefs.SaveLoadGame != null); _saveState(_quests, SaveBus.State); }
            if (_talk != null && _talking != null && _talking(_talk)) _talkClose(_talk);
            DialogueSession.Close(true);
            DeliveryRecovery.Uninstall(); QuestsPerf.Uninstall();
            _talk = _quests = null; _owner = null; _dispatch = _tickDispatch = _splitDispatch = false;
        }
    }
}
