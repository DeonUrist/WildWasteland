using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.QuestsCompat
{
    internal static class DeliveryRecovery
    {
        private const string ReturnPlace = "WW:return-empty-delivery";
        private static object _plugin;
        private static Func<object, object> _book;
        private static Func<object, IList> _quests;
        private static Func<object, int> _type, _count;
        private static Func<object, string> _place, _giverId, _giverName, _label;
        private static Func<object, Vector3> _position, _giverPosition;
        private static Action<object, Vector3> _setGiverPosition;
        private static Func<object, float> _radius;
        private static Func<object, bool> _hasPosition, _hasGiverPosition, _returnToGiver;
        private static Action<object, string> _setTitle, _setPlace, _fail;
        private static Action<object, bool> _setHasPosition, _setReturn, _setDone;
        private static Action<object, int> _setReward;
        private static Action<object, float> _refresh, _mapRefresh;
        private static Action<object, bool> _dirty;
        private static Func<object, IDictionary> _aimCache;
        private static Func<object, string> _id;
        private static Action<string> _toast;
        private static Func<Collider, Transform> _resolveNpc;
        private static Func<Transform, bool> _friendly;
        private static Func<GameObject, bool> _enemy;
        private sealed class Empty { internal float Since; internal int Scan; }
        private static readonly Dictionary<object, Empty> Waiting = new Dictionary<object, Empty>();
        private static readonly List<object> Expired = new List<object>();

        internal static void Install(IModule owner, Assembly assembly, object plugin, object talk)
        {
            _plugin = plugin; Clear(); var p = plugin.GetType(); var q = assembly.GetType("ApocalypterQuests.Quest", true);
            _book = Binding.Get<object>(p, "book"); _quests = Binding.Get<IList>(assembly.GetType("ApocalypterQuests.QuestBook", true), "Active");
            _type = Binding.Get<int>(q, "Type"); _count = Binding.Get<int>(q, "Count"); _id = Binding.Get<string>(q, "Id");
            _place = Binding.Get<string>(q, "Place"); _giverId = Binding.Get<string>(q, "GiverId"); _giverName = Binding.Get<string>(q, "GiverName"); _label = Binding.Get<string>(q, "Label");
            _position = Binding.Get<Vector3>(q, "Pos"); _giverPosition = Binding.Get<Vector3>(q, "GiverPos"); _radius = Binding.Get<float>(q, "Radius");
            _setGiverPosition = Binding.Set<Vector3>(q, "GiverPos");
            _hasPosition = Binding.Get<bool>(q, "HasPos"); _hasGiverPosition = Binding.Get<bool>(q, "HasGiverPos"); _returnToGiver = Binding.Get<bool>(q, "ReturnToGiver");
            _setTitle = Binding.Set<string>(q, "Title"); _setPlace = Binding.Set<string>(q, "Place"); _setReward = Binding.Set<int>(q, "Reward");
            _setHasPosition = Binding.Set<bool>(q, "HasPos"); _setReturn = Binding.Set<bool>(q, "ReturnToGiver"); _setDone = Binding.Set<bool>(q, "ObjectiveDone");
            _refresh = Binding.Set<float>(p, "nextRefresh"); _mapRefresh = Binding.Set<float>(p, "nextMapSync"); _dirty = Binding.Set<bool>(p, "uiDirty"); _aimCache = Binding.Get<IDictionary>(p, "aimCache");
            var quest = Expression.Parameter(typeof(object), "q"); var reason = Expression.Parameter(typeof(string), "reason");
            _fail = Expression.Lambda<Action<object, string>>(Expression.Call(Expression.Constant(plugin, p), AccessTools.Method(p, "FailQuest"), Expression.Convert(quest, q), reason), quest, reason).Compile();
            _toast = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), plugin, AccessTools.Method(p, "AddToast"));
            _enemy = (Func<GameObject, bool>)Delegate.CreateDelegate(typeof(Func<GameObject, bool>), AccessTools.Method(assembly.GetType("ApocalypterQuests.Game", true), "IsPlayerEnemy"));
            if (talk != null)
            {
                _resolveNpc = (Func<Collider, Transform>)Delegate.CreateDelegate(typeof(Func<Collider, Transform>), talk, AccessTools.Method(talk.GetType(), "ResolveNpc"));
                _friendly = (Func<Transform, bool>)Delegate.CreateDelegate(typeof(Func<Transform, bool>), talk, AccessTools.Method(talk.GetType(), "IsFriendly"));
            }
            Patch(owner, p, "Objective", postfix: "Objective"); Patch(owner, p, "DeliveryStolen", prefix: "Timeout");
            Patch(owner, p, "CancelQuest", prefix: "Cancel"); Patch(owner, p, "ReturnGoods", transpiler: "ReturnDialogue");
            Patch(owner, assembly.GetType("ApocalypterQuests.Takeover", true), "GiverTick", transpiler: "GiverRespawn");
        }
        private static void Patch(IModule owner, Type type, string name, string prefix = null, string postfix = null, string transpiler = null)
        {
            if (!Core.Patches.Patch(owner, AccessTools.Method(type, name),
                prefix: prefix == null ? null : AccessTools.Method(typeof(DeliveryRecovery), prefix),
                postfix: postfix == null ? null : AccessTools.Method(typeof(DeliveryRecovery), postfix),
                transpiler: transpiler == null ? null : AccessTools.Method(typeof(DeliveryRecovery), transpiler)))
                throw new InvalidOperationException("Delivery recovery hook failed: " + name);
        }
        private static bool Returning(object q) { return _type(q) == 6 && _place(q) == ReturnPlace; }
        internal static void Tick(List<PlayMakerFSM> people, int scan)
        {
            var camps = Services.Camps; if (_plugin == null || camps == null || _resolveNpc == null) { Clear(); return; }
            var player = GameRefs.Player; if (player == null) return;
            var quests = _quests(_book(_plugin));
            Expired.Clear(); foreach (var q in Waiting.Keys) if (!quests.Contains(q)) Expired.Add(q);
            foreach (var q in Expired) Waiting.Remove(q);
            for (int i = quests.Count - 1; i >= 0; i--)
            {
                var q = quests[i]; if (_type(q) != 6) continue;
                bool returning = Returning(q);
                if (returning && HasRecipient(q, people, Vector3.zero, float.PositiveInfinity, true)) { Waiting.Remove(q); continue; }
                if (returning ? !_hasGiverPosition(q) : !_hasPosition(q)) continue;
                Vector3 world = returning ? _giverPosition(q) : _position(q);
                var status = camps.At(world, 120f);
                if (status == CampState.Cleared) { Fail(q, returning ? "return camp is dead" : "destination camp is dead"); continue; }
                Vector3 scene = Origin.Scene(world), delta = player.transform.position - scene;
                float radius = returning ? 120f : _radius(q), arrival = Mathf.Max(5f, Mathf.Min(30f, radius * .6f));
                if (delta.x * delta.x + delta.z * delta.z > arrival * arrival || status != CampState.Ready || HasRecipient(q, people, scene, radius, returning))
                { Waiting.Remove(q); continue; }
                Empty empty;
                if (!Waiting.TryGetValue(q, out empty)) { Waiting.Add(q, new Empty { Since = Time.time, Scan = scan }); continue; }
                if (Time.time - empty.Since < 15f || empty.Scan == scan) continue;
                Waiting.Remove(q);
                if (returning) { Fail(q, "nobody is there to take the goods back"); continue; }
                if (!_hasGiverPosition(q)) { Fail(q, "nobody to deliver to and return location is unknown"); continue; }
                _setPlace(q, ReturnPlace); _setHasPosition(q, false); _setReturn(q, true); _setDone(q, true); _setReward(q, 0);
                _setTitle(q, "Return " + _count(q) + " " + _label(q)); _aimCache(_plugin).Remove(_id(q)); Refresh();
                _toast("There was nobody to deliver to. Return the " + _label(q) + " to the " + _giverName(q) + ".");
            }
        }
        private static bool HasRecipient(object q, List<PlayMakerFSM> people, Vector3 position, float radius, bool returning)
        {
            foreach (var f in people)
            {
                if (f == null || f.FsmName != "Health" || f.ActiveStateName != "health" || !f.gameObject.activeInHierarchy) continue;
                if (returning && f.gameObject.name != _giverId(q)) continue;
                var health = f.FsmVariables.GetFsmFloat("Health"); if (health == null || health.Value <= 0f) continue;
                Vector3 delta = f.transform.position - position; if (delta.x * delta.x + delta.z * delta.z > radius * radius) continue;
                var collider = f.GetComponent<Collider>(); if (collider == null || !collider.enabled) continue;
                var npc = _resolveNpc(collider);
                if (npc != null && _friendly(npc) && !_enemy(npc.gameObject))
                { if (returning) _setGiverPosition(q, Origin.World(npc.position)); return true; }
            }
            return false;
        }
        private static void Refresh() { _refresh(_plugin, 0); _mapRefresh(_plugin, 0); _dirty(_plugin, true); }
        private static void Fail(object q, string why) { Waiting.Remove(q); _fail(q, why); Refresh(); }
        private static bool Timeout(object __0) { if (!Returning(__0)) return true; Fail(__0, "ran out of time returning the goods"); return false; }
        private static bool Cancel(object __0, string __1) { if (!Returning(__0)) return true; Fail(__0, __1); return false; }
        private static void Objective(object __0, ref string __result)
        { if (Returning(__0)) __result = "There was nobody to deliver to. Return all " + _count(__0) + " " + _label(__0) + " to the " + _giverName(__0) + "."; }
        private static string ReturnLine(string original, object q)
        { return Returning(q) ? "There was nobody to deliver to. Here are your " : original; }
        private static IEnumerable<CodeInstruction> ReturnDialogue(IEnumerable<CodeInstruction> code)
        {
            int matched = 0;
            foreach (var c in code)
            {
                yield return c;
                if (c.opcode != OpCodes.Ldstr || (string)c.operand != "I can't make that delivery. Here are your ") continue;
                matched++; yield return new CodeInstruction(OpCodes.Ldarg_1); yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DeliveryRecovery), "ReturnLine"));
            }
            if (matched != 1) throw new InvalidOperationException("Return-goods dialogue not found exactly once");
        }
        private static bool AllowRespawn(object q) { return _returnToGiver(q) && !Returning(q); }
        private static IEnumerable<CodeInstruction> GiverRespawn(IEnumerable<CodeInstruction> code)
        {
            int matched = 0;
            foreach (var c in code)
            {
                var field = c.operand as FieldInfo;
                if (c.opcode == OpCodes.Ldfld && field != null && field.Name == "ReturnToGiver")
                { matched++; c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(DeliveryRecovery), "AllowRespawn"); }
                yield return c;
            }
            if (matched != 1) throw new InvalidOperationException("Giver respawn gate not found exactly once");
        }
        internal static void Clear() { Waiting.Clear(); Expired.Clear(); }
        internal static void Uninstall() { Clear(); _plugin = null; _resolveNpc = null; _friendly = null; }
    }
}
