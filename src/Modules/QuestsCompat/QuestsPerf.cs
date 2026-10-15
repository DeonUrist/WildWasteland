using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TMPro;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.QuestsCompat
{
    // Allocation and frequency fixes for the loaded Quests/Talk DLLs. Every hook here is optional: a failed one is logged and
    // the source behaviour stays, unlike the compatibility hooks in QuestsCompatModule, which roll the module back.
    internal static class QuestsPerf
    {
        private static IModule _owner;
        // Hire
        private static Func<object, GameObject> _madeRaycast;
        private static Func<object, BepInEx.Configuration.ConfigEntry<string>> _rangedNames;
        private static Func<Transform, string, Transform> _findDeep;
        private static Func<GameObject, bool> _namedRanged;
        private sealed class FsmSet { internal GameObject Go; internal PlayMakerFSM[] Fsms; internal float Until; }
        private static readonly Dictionary<int, FsmSet> FsmSets = new Dictionary<int, FsmSet>();
        private sealed class Ranged { internal bool Native, Named; internal string Names; internal float Until; }
        private static readonly Dictionary<int, Ranged> RangedNpcs = new Dictionary<int, Ranged>();
        private static int _detachFrame = -1;
        private static string _nearestName; private static float _nearestAt;
        private static RaycastHit[] _rays = new RaycastHit[32];
        private static RaycastHit[][] _exact = new RaycastHit[33][];
        // Takeover
        private sealed class MoveMemo { internal PlayMakerFSM Fsm; internal float Until; }
        private static readonly Dictionary<int, MoveMemo> Moves = new Dictionary<int, MoveMemo>();
        private static readonly Dictionary<string, float> NoInactive = new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> NoPrefab = new Dictionary<string, float>(StringComparer.Ordinal);
        internal static bool GiverDispatch;
        private static GameObject[] _all; private static int _allFrame = -1;
        private const float GiverMemoSeconds = 30f;   // = the source's own giver retry cadence (giverMissingSince + 27 s + 3 s, prefabMissUntil 30 s)
        // Game.Matches
        private sealed class MatchMemo { internal string Name, Display; internal bool Result; internal float Until; }
        private static readonly Dictionary<string, Dictionary<int, MatchMemo>> Matches = new Dictionary<string, Dictionary<int, MatchMemo>>(StringComparer.Ordinal);
        private static int _matchCount;
        private static Func<PlayMakerFSM, string, string> _findString;
        // Plugin.Aim
        private static Func<object, IDictionary> _aimCache;
        private static Func<object, string> _questId, _aimKey;
        private static Func<object, float> _aimAt;
        private static Action<object, float> _setAimAt;
        private static Func<object, int, bool> _handIn;
        private static int _aimFrame = -1, _aimComputed;
        // Talk bridge
        private static Func<object, FieldInfo> _bridgeTalking;
        private static FieldInfo _talkingField; private static bool _talkingFieldOk; private static Type _talkType;
        private static Func<object, bool> _talking;
        private static Func<object, string> _signature, _btnKey, _btnText;
        private static readonly List<object> SigButtons = new List<object>();
        private static readonly List<string> SigStrings = new List<string>();
        private static string _sig;
        // Talk prompt
        private static Func<object, TMP_Text> _hint;
        private static Func<object, RectTransform> _panel;
        private static Func<object, string> _promptShown;
        private static object _promptUi; private static string _promptName, _promptText; private static KeyCode _promptKey;

        internal static void Install(IModule owner, Assembly asm, object talk, Func<object, bool> talking)
        {
            _owner = owner; Clear();
            var hire = asm.GetType("ApocalypterQuests.Hire", true);
            _madeRaycast = Binding.Get<GameObject>(hire, "madeRaycast"); _rangedNames = Binding.Get<BepInEx.Configuration.ConfigEntry<string>>(hire, "rangedNames");
            _findDeep = (Func<Transform, string, Transform>)Delegate.CreateDelegate(typeof(Func<Transform, string, Transform>), AccessTools.Method(hire, "FindDeep"));
            _namedRanged = (Func<GameObject, bool>)Delegate.CreateDelegate(typeof(Func<GameObject, bool>), AccessTools.Method(hire, "NamedRanged"));
            Optional(hire, "NearestByName", "NearestPrefix", "NearestPostfix");
            Optional(hire, "IsRangedNpc", "IsRangedPrefix");
            foreach (var name in new[] { "Clear", "Reset" }) Optional(hire, name, null, "HireCleared");
            Optional(hire, "DetachGuard", null, "Detached");
            // Read-only per-frame FSM lookups; one-shot snapshots (Attach, SnapCombat, TryMount ...) keep fresh arrays.
            foreach (var name in new[] { "FindFsm", "FootMagazine", "FootWatch", "Hostile", "HoldFire", "FireShots", "WatchShots", "FeedTarget", "Calm", "IsRangedNpc" })
                Optional(hire, name, transpiler: "CachedFsms");
            Optional(hire, AccessTools.Method(hire, "ClearShot", new[] { typeof(GameObject) }), null, null, "NonAllocRays");

            var takeover = asm.GetType("ApocalypterQuests.Takeover", true);
            Optional(takeover, "MoveFsm", "MovePrefix", "MovePostfix");
            Optional(takeover, "InactiveExists", "InactivePrefix", "InactivePostfix", "SharedObjects");
            Optional(takeover, "ScanForPrefab", "PrefabPrefix", "PrefabPostfix", "SharedObjects");

            var game = asm.GetType("ApocalypterQuests.Game", true);
            _findString = (Func<PlayMakerFSM, string, string>)Delegate.CreateDelegate(typeof(Func<PlayMakerFSM, string, string>), AccessTools.Method(game, "FindString"));
            Optional(game, "Matches", "MatchPrefix", "MatchPostfix");

            var plugin = asm.GetType("ApocalypterQuests.Plugin", true); var quest = asm.GetType("ApocalypterQuests.Quest", true);
            var aim = AccessTools.Inner(plugin, "AimInfo");
            _aimCache = Binding.Get<IDictionary>(plugin, "aimCache"); _questId = Binding.Get<string>(quest, "Id");
            _aimKey = Binding.Get<string>(aim, "Key"); _aimAt = Binding.Get<float>(aim, "At"); _setAimAt = Binding.Set<float>(aim, "At");
            var q = Expression.Parameter(typeof(object), "q"); var have = Expression.Parameter(typeof(int), "have");
            _handIn = Expression.Lambda<Func<object, int, bool>>(Expression.Call(AccessTools.Method(plugin, "HandIn"), Expression.Convert(q, quest), have), q, have).Compile();
            Optional(plugin, "Aim", "AimPrefix");

            var bridge = asm.GetType("ApocalypterQuests.TalkBridge", true); var btn = asm.GetType("ApocalypterQuests.TalkBtn", true);
            _btnKey = Binding.Get<string>(btn, "Key"); _btnText = Binding.Get<string>(btn, "Text");
            var list = Expression.Parameter(typeof(object), "list"); var sig = AccessTools.Method(bridge, "Signature");
            _signature = Expression.Lambda<Func<object, string>>(Expression.Call(sig, Expression.Convert(list, sig.GetParameters()[0].ParameterType)), list).Compile();
            Optional(bridge, "NeedsSync", transpiler: "CachedSignature");
            if (talk != null)
            {
                _talking = talking; _talkType = talk.GetType(); _bridgeTalking = Binding.Get<FieldInfo>(bridge, "fTalking");
                Optional(bridge, AccessTools.PropertyGetter(bridge, "TalkingNow"), "TalkingNow", null, null);
                var ui = talk.GetType().Assembly.GetType("ApocalypterTalk.DialogueUI", true);
                _hint = Binding.Get<TMP_Text>(ui, "hint"); _panel = Binding.Get<RectTransform>(ui, "panel"); _promptShown = Binding.Get<string>(ui, "currentNpcName");
                Optional(ui, "ShowPrompt", "PromptPrefix", "PromptPostfix");
            }
        }
        private static void Optional(Type type, string name, string prefix = null, string postfix = null, string transpiler = null)
        { Optional(type, AccessTools.Method(type, name), prefix, postfix, transpiler); }
        private static void Optional(Type type, MethodBase method, string prefix, string postfix, string transpiler)
        {
            if (!Core.Patches.Patch(_owner, method,
                prefix: prefix == null ? null : AccessTools.Method(typeof(QuestsPerf), prefix),
                postfix: postfix == null ? null : AccessTools.Method(typeof(QuestsPerf), postfix),
                transpiler: transpiler == null ? null : AccessTools.Method(typeof(QuestsPerf), transpiler)))
                Log.Warn("[QuestsCompat] Optional speed-up unavailable: " + type.Name + "." + (method != null ? method.Name : "?") + "; original kept");
        }
        internal static void Clear()
        {
            FsmSets.Clear(); RangedNpcs.Clear(); Moves.Clear(); NoInactive.Clear(); NoPrefab.Clear(); Matches.Clear(); _matchCount = 0;
            _nearestName = null; _nearestAt = 0; _all = null; _allFrame = -1; GiverDispatch = false;
            SigButtons.Clear(); SigStrings.Clear(); _sig = null; _talkingField = null; _talkingFieldOk = false;
            _promptUi = null; _promptName = _promptText = null;
        }
        internal static void Uninstall() { Clear(); _owner = null; _talking = null; _bridgeTalking = null; }

        // ---- Hire ----
        // T7.1: NearestByName walks the cached People list (PeopleScan); while the hire is not found, retry at 4 Hz.
        private static bool NearestPrefix(string __0, ref Transform __result, out bool __state)
        {
            __state = Time.unscaledTime < _nearestAt && __0 == _nearestName;
            if (!__state) return true;
            __result = null; return false;
        }
        private static void NearestPostfix(string __0, Transform __result, bool __state)
        {
            if (__state) return;
            if (__result == null) { _nearestName = __0; _nearestAt = Time.unscaledTime + .25f; } else _nearestName = null;
        }
        private static void HireCleared() { FsmSets.Clear(); RangedNpcs.Clear(); _nearestName = null; }
        private static void Detached() { _detachFrame = Time.frameCount; RangedNpcs.Clear(); }
        // F2: the FSM components of an NPC do not change; destroyed entries are skipped by every caller's null check.
        internal static PlayMakerFSM[] FsmsOf(GameObject go)
        {
            if (go == null) return go.GetComponents<PlayMakerFSM>();
            int id = go.GetInstanceID(); FsmSet set; float now = Time.unscaledTime;
            if (FsmSets.TryGetValue(id, out set) && ReferenceEquals(set.Go, go) && now < set.Until) return set.Fsms;
            if (set == null) { if (FsmSets.Count >= 64) FsmSets.Clear(); set = new FsmSet(); FsmSets[id] = set; }
            set.Go = go; set.Fsms = go.GetComponents<PlayMakerFSM>(); set.Until = now + 1f;
            return set.Fsms;
        }
        internal static PlayMakerFSM[] FsmsOfComponent(Component c) { return c == null ? c.GetComponents<PlayMakerFSM>() : FsmsOf(c.gameObject); }
        private static IEnumerable<CodeInstruction> CachedFsms(IEnumerable<CodeInstruction> code)
        {
            foreach (var c in code)
            {
                var m = c.operand as MethodInfo;
                if (m != null && m.Name == "GetComponents" && m.IsGenericMethod && m.GetParameters().Length == 0 && m.GetGenericArguments()[0] == typeof(PlayMakerFSM))
                {
                    if (m.DeclaringType == typeof(GameObject)) { c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(QuestsPerf), "FsmsOf"); }
                    else if (m.DeclaringType == typeof(Component)) { c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(QuestsPerf), "FsmsOfComponent"); }
                }
                yield return c;
            }
        }
        // F2: the FSM/AttackRaycast_Ranged/name checks are cached per NPC for 1 s; Quests' own added raycast child counts at once.
        private static bool IsRangedPrefix(GameObject __0, ref bool __result)
        {
            var go = __0; if (go == null) return true;
            var made = _madeRaycast(null);
            if (made != null && made.transform.IsChildOf(go.transform)) { __result = true; return false; }
            int id = go.GetInstanceID(); Ranged r; float now = Time.unscaledTime;
            var names = _rangedNames(null); string list = names != null ? names.Value : null;
            if (!RangedNpcs.TryGetValue(id, out r) || now >= r.Until)
            {
                if (_detachFrame == Time.frameCount) return true;
                bool native = false; var fsms = FsmsOf(go);
                for (int i = 0; i < fsms.Length && !native; i++)
                    if (fsms[i] != null && (fsms[i].FsmName == "RangedAttackWait" || fsms[i].FsmName == "Damage Ranged")) native = true;
                if (!native) native = _findDeep(go.transform, "AttackRaycast_Ranged") != null;
                if (r == null) { if (RangedNpcs.Count >= 64) RangedNpcs.Clear(); r = new Ranged(); RangedNpcs[id] = r; }
                r.Native = native; r.Names = null; r.Until = now + 1f;
            }
            if (!r.Native && !ReferenceEquals(r.Names, list)) { r.Named = _namedRanged(go); r.Names = list; }
            __result = r.Native || r.Named; return false;
        }
        // F2: ClearShot's RaycastAll -> NonAlloc into a reused buffer, returned as an exact-length array (same unordered hit set).
        internal static RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float distance)
        { return RaycastAll(origin, direction, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.UseGlobal); }
        internal static RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float distance, int mask, QueryTriggerInteraction triggers)
        {
            int n;
            while ((n = Physics.RaycastNonAlloc(origin, direction, _rays, distance, mask, triggers)) >= _rays.Length) _rays = new RaycastHit[_rays.Length * 2];
            if (n >= _exact.Length) Array.Resize(ref _exact, _rays.Length + 1);
            var hits = _exact[n] ?? (_exact[n] = new RaycastHit[n]);
            Array.Copy(_rays, hits, n); return hits;
        }
        private static IEnumerable<CodeInstruction> NonAllocRays(IEnumerable<CodeInstruction> code)
        {
            int matched = 0; var list = new List<CodeInstruction>(code);
            foreach (var c in list)
            {
                var m = c.operand as MethodInfo;
                if (m == null || m.DeclaringType != typeof(Physics) || m.Name != "RaycastAll") continue;
                var p = m.GetParameters(); Type[] want = null;
                if (p.Length == 3 && p[0].ParameterType == typeof(Vector3) && p[1].ParameterType == typeof(Vector3)) want = new[] { typeof(Vector3), typeof(Vector3), typeof(float) };
                else if (p.Length == 5 && p[0].ParameterType == typeof(Vector3) && p[1].ParameterType == typeof(Vector3)) want = new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(int), typeof(QueryTriggerInteraction) };
                if (want == null) throw new InvalidOperationException("Unexpected RaycastAll overload");
                c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(QuestsPerf), "RaycastAll", want); matched++;
            }
            if (matched != 1) throw new InvalidOperationException("ClearShot RaycastAll not found exactly once");
            return list;
        }

        // ---- Takeover ----
        // F8: walkers ask for their Movement FSM every frame until they have one; remember misses for 5 s.
        private static bool MovePrefix(GameObject __0, ref PlayMakerFSM __result, out bool __state)
        {
            __state = false; MoveMemo m;
            if (ReferenceEquals(__0, null) || !Moves.TryGetValue(__0.GetInstanceID(), out m)) return true;
            if (!ReferenceEquals(m.Fsm, null)) { if (m.Fsm == null) return true; __result = m.Fsm; __state = true; return false; }
            if (Time.unscaledTime >= m.Until) return true;
            __result = null; __state = true; return false;
        }
        private static void MovePostfix(GameObject __0, PlayMakerFSM __result, bool __state)
        {
            if (__state || __0 == null) return;
            if (Moves.Count >= 128) Moves.Clear();
            Moves[__0.GetInstanceID()] = new MoveMemo { Fsm = __result, Until = Time.unscaledTime + 5f };
        }
        // T7.4: GiverTick retries a missing giver about every 30 s with two FindObjectsOfTypeAll scans. Inside GiverTick, misses are
        // remembered for 30 s (no longer than the source retry, so a returning giver is not delayed), and both scans of one frame share one array.
        internal static GameObject[] AllObjects()
        {
            if (_all == null || _allFrame != Time.frameCount) { _all = Resources.FindObjectsOfTypeAll<GameObject>(); _allFrame = Time.frameCount; }
            return _all;
        }
        internal static void ReleaseObjects() { _all = null; _allFrame = -1; }
        private static IEnumerable<CodeInstruction> SharedObjects(IEnumerable<CodeInstruction> code)
        {
            int matched = 0; var list = new List<CodeInstruction>(code);
            foreach (var c in list)
            {
                var m = c.operand as MethodInfo;
                if (m != null && m.DeclaringType == typeof(Resources) && m.Name == "FindObjectsOfTypeAll" && m.IsGenericMethod && m.GetGenericArguments()[0] == typeof(GameObject))
                { c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(QuestsPerf), "AllObjects"); matched++; }
            }
            if (matched != 1) throw new InvalidOperationException("FindObjectsOfTypeAll not found exactly once");
            return list;
        }
        private static bool Missed(Dictionary<string, float> memo, string name)
        { float until; return GiverDispatch && name != null && memo.TryGetValue(name, out until) && Time.unscaledTime < until; }
        private static void Miss(Dictionary<string, float> memo, string name, bool found)
        {
            if (!GiverDispatch || name == null) return;
            if (found) memo.Remove(name); else { if (memo.Count >= 256) memo.Clear(); memo[name] = Time.unscaledTime + GiverMemoSeconds; }
        }
        private static bool InactivePrefix(string __0, ref bool __result) { if (!Missed(NoInactive, __0)) return true; __result = false; return false; }
        private static void InactivePostfix(string __0, bool __result) { if (!Missed(NoInactive, __0)) Miss(NoInactive, __0, __result); }
        private static bool PrefabPrefix(string __0, ref GameObject __result) { if (!Missed(NoPrefab, __0)) return true; __result = null; return false; }
        private static void PrefabPostfix(string __0, GameObject __result) { if (!Missed(NoPrefab, __0)) Miss(NoPrefab, __0, __result != null); }

        // ---- Game.Matches (T7.3) ----
        // Keyed by ItemName FSM + token; the inputs that can change (object name, ItemName variable) are re-read and compared,
        // the rest (ID FSM variables) expires after 30 s.
        private static bool MatchPrefix(GameObject __0, PlayMakerFSM __1, string __2, ref string __3, ref bool __result, out bool __state)
        {
            __state = false; Dictionary<int, MatchMemo> byFsm; MatchMemo m;
            if (__2 == null || ReferenceEquals(__1, null) || ReferenceEquals(__0, null) || !Matches.TryGetValue(__2, out byFsm)
                || !byFsm.TryGetValue(__1.GetInstanceID(), out m) || Time.unscaledTime >= m.Until) return true;
            string display = _findString(__1, "ItemName");
            if (!string.Equals(display, m.Display, StringComparison.Ordinal) || !string.Equals(__0.name, m.Name, StringComparison.Ordinal)) return true;
            __3 = display; __result = m.Result; __state = true; return false;
        }
        private static void MatchPostfix(GameObject __0, PlayMakerFSM __1, string __2, ref string __3, bool __result, bool __state)
        {
            if (__state || __2 == null || __0 == null || __1 == null) return;
            Dictionary<int, MatchMemo> byFsm;
            if (_matchCount >= 20000) { Matches.Clear(); _matchCount = 0; }
            if (!Matches.TryGetValue(__2, out byFsm)) { byFsm = new Dictionary<int, MatchMemo>(); Matches.Add(__2, byFsm); }
            MatchMemo m; int id = __1.GetInstanceID();
            if (!byFsm.TryGetValue(id, out m)) { m = new MatchMemo(); byFsm.Add(id, m); _matchCount++; }
            m.Name = __0.name; m.Display = __3; m.Result = __result; m.Until = Time.unscaledTime + 30f;
        }

        // ---- Plugin.Aim (T7.3) ----
        // At most one directions recompute per frame: other expired entries stay as they are until the next refresh.
        private static bool AimPrefix(object __instance, object __0, int __1)
        {
            int frame = Time.frameCount; if (frame != _aimFrame) { _aimFrame = frame; _aimComputed = 0; }
            var cache = _aimCache(__instance); var entry = cache[_questId(__0)];
            if (entry == null || _aimKey(entry) != (_handIn(__0, __1) ? "hand" : "work")) { _aimComputed++; return true; }
            float now = Time.unscaledTime, at = _aimAt(entry);
            if (now - at < 4f && now >= at) return true;
            if (_aimComputed++ == 0) return true;
            _setAimAt(entry, now - 3.95f);
            return true;
        }

        // ---- Talk bridge (F9) ----
        private static bool TalkingNow(object __instance, ref bool __result)
        {
            if (_talking == null) return true;
            var field = _bridgeTalking(__instance);
            if (!ReferenceEquals(field, _talkingField))
            { _talkingField = field; _talkingFieldOk = field != null && field.IsStatic && field.FieldType == typeof(bool) && field.Name == "Talking" && field.DeclaringType == _talkType; }
            if (field == null) { __result = false; return false; }
            if (!_talkingFieldOk) return true;
            __result = _talking(null); return false;
        }
        // NeedsSync builds the button signature every frame while talking; reuse it while the list holds the same buttons.
        internal static string Signature(object want)
        {
            var list = want as IList; if (list == null) return _signature(want);
            bool same = _sig != null && SigButtons.Count == list.Count;
            for (int i = 0; same && i < list.Count; i++)
            { var b = list[i]; same = ReferenceEquals(b, SigButtons[i]) && b != null && ReferenceEquals(_btnKey(b), SigStrings[2 * i]) && ReferenceEquals(_btnText(b), SigStrings[2 * i + 1]); }
            if (same) return _sig;
            _sig = _signature(want); SigButtons.Clear(); SigStrings.Clear();
            for (int i = 0; i < list.Count; i++) { var b = list[i]; SigButtons.Add(b); SigStrings.Add(b != null ? _btnKey(b) : null); SigStrings.Add(b != null ? _btnText(b) : null); }
            return _sig;
        }
        private static IEnumerable<CodeInstruction> CachedSignature(IEnumerable<CodeInstruction> code)
        {
            int matched = 0; var list = new List<CodeInstruction>(code);
            foreach (var c in list)
            {
                var m = c.operand as MethodInfo;
                if (m != null && m.Name == "Signature" && m.DeclaringType != null && m.DeclaringType.FullName == "ApocalypterQuests.TalkBridge")
                { c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(QuestsPerf), "Signature"); matched++; }
            }
            if (matched != 1) throw new InvalidOperationException("NeedsSync signature not found exactly once");
            return list;
        }

        // ---- Talk prompt (F10) ----
        // ShowPrompt rebuilds the hint string 4x per second; skip it while the same prompt is already shown untouched.
        private static bool PromptPrefix(object __instance, string __0, KeyCode __1)
        {
            if (!ReferenceEquals(__instance, _promptUi) || __1 != _promptKey || !string.Equals(__0 ?? "", _promptName, StringComparison.Ordinal)) return true;
            var hint = _hint(__instance); var panel = _panel(__instance);
            if (hint == null || panel == null || !hint.gameObject.activeSelf || panel.gameObject.activeSelf || !ReferenceEquals(hint.text, _promptText)
                || !string.Equals(_promptShown(__instance), _promptName, StringComparison.Ordinal)) return true;
            return false;
        }
        private static void PromptPostfix(object __instance, string __0, KeyCode __1)
        {
            var hint = _hint(__instance);
            _promptUi = __instance; _promptKey = __1; _promptName = __0 ?? ""; _promptText = hint != null ? hint.text : null;
        }
    }
}
