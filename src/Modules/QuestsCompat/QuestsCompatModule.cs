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
        private static Func<object, MethodInfo> _immersiveAdd;
        private static Action<object, bool> _setReady;
        private static Action<object, PlayMakerFSM> _saveFsm;
        private static Action<object, bool> _hadSaveFsm;
        private static Action<object, string> _saveState;
        private static bool _dispatch, _tickDispatch, _newWorld;
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
            _owner = this; _generation++; _newWorld = false; _dispatch = _tickDispatch = false;
            ClearScan();
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
                QuestFollower.Install(this, asm, _quests);
                var hooks = asm.GetType("ApocalypterQuests.Hooks", true);
                foreach (var name in new[] { "OpenPostfix", "ClosePostfix", "CreateObjectPostfix" }) PluginLifetime.Install(this, hooks, name);
                SaveBus.StateChanged += StateChanged;
                Scheduler.EveryFrame(this, "Quest frame", QuestFrame);
                Scheduler.EveryFrame(this, "Quest poses", QuestLate, Phase.LateUpdateLast);
                Scheduler.Gui(this, "Quest windows", QuestGui);
                Scheduler.Interval(this, "Quest logic", .5f, QuestTick);
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
        private static bool TickOnly() { return _tickDispatch; }
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
        private static void QuestGui() { if (!EditorSession.Busy) Run(_questGui, _quests); }
        private static void QuestTick()
        {
            if (!_ready(_quests) || !_scanReady || !GameRefs.InGame) return;
            DeliveryRecovery.Tick(People, _scanRevision);
            _tickDispatch = true; try { _questTick(_quests); } finally { _tickDispatch = false; }
        }
        private static void StateChanged(string previous, string state)
        {
            if (_quests == null) return;
            if (state == "LoadGame" || state == "LoadVar" || state == "generateTerrain 2" || state == "loaded" || state == "setSeed" || state == "generateTerrain")
            {
                if (_ready(_quests)) { _setReady(_quests, false); _reset(_quests); _generation++; ClearScan(); }
                _newWorld = state == "setSeed" || state == "generateTerrain";
                if (_newWorld) _bookClear(_book(_quests));
            }
            else if (state == "isPlay" && !_ready(_quests))
            {
                _setReady(_quests, true); if (!_newWorld) _load(_quests);
                Log.Always("[QuestsCompat] Quest world ready; original save/profile rules active");
            }
            else if (state == "SaveGame" && _ready(_quests))
            {
                string slot = SaveBus.Slot; int generation = _generation;
                SaveBus.AfterSave(() => { if (_quests != null && generation == _generation && slot == SaveBus.Slot) _save(_quests); });
            }
        }
        private static void ClearScan() { Items.Clear(); People.Clear(); _nextItems.Clear(); _nextPeople.Clear(); _scan = _scanRevision = 0; _scanAt = 0; _scanReady = false; DeliveryRecovery.Clear(); }
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
            var f = fsms[_scan++];
            if (f != null)
            {
                if (f.FsmName == "ItemName") _nextItems.Add(f);
                else if (f.FsmName == "Health" || f.FsmName == "ID") _nextPeople.Add(f);
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
            if (_quests != null && _reset != null) _reset(_quests);
            QuestFollower.Uninstall();
            if (_quests != null && _saveFsm != null) { _saveFsm(_quests, GameRefs.SaveLoadGame); _hadSaveFsm(_quests, GameRefs.SaveLoadGame != null); _saveState(_quests, SaveBus.State); }
            if (_talk != null && _talking != null && _talking(_talk)) _talkClose(_talk);
            DialogueSession.Close(true);
            DeliveryRecovery.Uninstall();
            _talk = _quests = null; _owner = null; _dispatch = _tickDispatch = false;
        }
    }
}
