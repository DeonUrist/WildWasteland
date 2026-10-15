using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.QuestsCompat
{
    // All calls into the optional quest DLL are compiled here, once at installation.
    internal static class QuestFollower
    {
        private static Func<object, GameObject> _npc, _target;
        private static Func<object, bool> _mounted, _active;
        private static Func<object, float> _reloadUntil;
        private static Func<object, int> _magazine;
        private static Action<object, GameObject> _setTarget;
        private static Action<object, float> _lastInform, _engagedUntil;
        private static Func<object, IList> _activeQuests;
        private static Func<object, object> _questBook;
        private static Func<object, string> _id, _title, _giverName, _giverId;
        private static Func<string, Transform> _findLive;
        private static object _plugin;
        private static GameObject _current;
        private static Animator _animator;
        private static PlayMakerFSM _attack;
        private static Transform _weapon;
        private static Transform[] _models;
        private static IAnimatedCharacter _body;
        private static IAnimationApi _animation;
        private static bool _ownsBody;
        private static float _reloadShown, _retry;
        private static float _weaponAt;
        private static string _weaponKey;
        private static string[] _modelKeys;
        private static int _combatFrame = -1; private static GameObject _combatNpc, _combatTarget; private static float _combatBlocked;
        private static readonly Dictionary<FieldInfo, object> LegacyFields = new Dictionary<FieldInfo, object>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install(IModule owner, Assembly assembly, object instance)
        {
            var t = assembly.GetType("ApocalypterQuests.Hire", true);
            LegacyFields.Clear();
            foreach (var name in new[] { "guardAttach", "poseApply", "shootDistField", "informApi", "apiLooked", "taughtAll" })
            { var field = AccessTools.Field(t, name); LegacyFields.Add(field, field.GetValue(null)); }
            _npc = Binding.Get<GameObject>(t, "npc"); _target = Binding.Get<GameObject>(t, "target");
            _mounted = Binding.Get<bool>(t, "mounted"); _active = Binding.Get<bool>(t, "Active");
            _reloadUntil = Binding.Get<float>(t, "reloadUntil"); _magazine = Binding.Get<int>(t, "magSize");
            _setTarget = Binding.Set<GameObject>(t, "target"); _lastInform = Binding.Set<float>(t, "lastInform"); _engagedUntil = Binding.Set<float>(t, "engagedUntil");
            Patch(owner, t, "MissingMods", "MissingMods"); Patch(owner, t, "GetPlugin", "GetPlugin");
            PluginLifetime.Install(owner, t, "GetPlugin");
            Patch(owner, t, "FindApi", "FindApi"); Patch(owner, t, "Inform", "Inform"); Patch(owner, t, "SetExcluded", "SetExcluded");
            Patch(owner, t, "CountShot", postfix: "Shot");
            Patch(owner, t, "FeedTarget", "FeedTarget"); Patch(owner, t, "HoldFire", "HoldFire");
            Patch(owner, t, "FootShoot", transpiler: "AnimationCalls");
            Patch(owner, t, "PlayShotAnim", transpiler: "AnimationCalls");
            Patch(owner, t, "WatchShots", transpiler: "AnimationCalls");
            // Keep Quests' seating, snapshots, gun setup and restoration, with a provider-owned legacy type map.
            foreach (var name in new[] { "TryMount", "AttachGuard", "LendArmPose", "TeachAll", "PatrolShootDistance" }) Patch(owner, t, name, transpiler: "LegacyTypes");
            var plugin = assembly.GetType("ApocalypterQuests.Plugin", true);
            _plugin = instance;
            _questBook = Binding.Get<object>(plugin, "book");
            _activeQuests = Binding.Get<IList>(assembly.GetType("ApocalypterQuests.QuestBook", true), "Active");
            var q = assembly.GetType("ApocalypterQuests.Quest", true);
            _id = Binding.Get<string>(q, "Id"); _title = Binding.Get<string>(q, "Title"); _giverName = Binding.Get<string>(q, "GiverName"); _giverId = Binding.Get<string>(q, "GiverId");
            _findLive = (Func<string, Transform>)Delegate.CreateDelegate(typeof(Func<string, Transform>), AccessTools.Method(assembly.GetType("ApocalypterQuests.Game", true), "FindLive"));
            Log.Always("[QuestsCompat] Follower bridge ready: Quests movement, WW animations/reloads and passenger combat");
        }
        private static void Patch(IModule owner, Type type, string method, string prefix = null, string postfix = null, string transpiler = null)
        {
            if (!Core.Patches.Patch(owner, AccessTools.Method(type, method),
                prefix: prefix == null ? null : AccessTools.Method(typeof(QuestFollower), prefix),
                postfix: postfix == null ? null : AccessTools.Method(typeof(QuestFollower), postfix),
                transpiler: transpiler == null ? null : AccessTools.Method(typeof(QuestFollower), transpiler)))
                throw new InvalidOperationException("Follower hook failed: " + method);
        }
        private static bool MissingMods(ref string __result)
        {
            __result = Services.Npc == null ? "NPC AI" : Services.FollowerVehicles == null ? "Patrols" : "";
            return false;
        }
        private static void GetPlugin(ref string __0)
        { if (__0 == "com.denis.apocalypter.apocapatrol" && Services.FollowerVehicles != null) __0 = WildWasteland.Plugin.GUID; }
        private static bool FindApi(ref bool __result) { __result = Services.Npc != null; return false; }
        private static bool Inform(GameObject __0)
        {
            var npc = _npc(null);
            if (npc != null && __0 != null && Services.Npc != null)
            {
                Services.Npc.Inform(npc, __0); _setTarget(null, __0);
                _lastInform(null, Time.time); _engagedUntil(null, Time.time + 25f);
            }
            return false;
        }
        private static bool SetExcluded(bool __0)
        { if (Services.Npc != null) Services.Npc.SetFollowerDriven(_npc(null), __0); return false; }
        private static bool FeedTarget()
        {
            if (Services.FollowerVehicles == null) return true;
            // Quests feeds the target from Update, HoldFire and LateUpdate; Combat only stores these three values on the guard.
            GameObject npc = _npc(null), target = _target(null); float blocked = _reloadUntil(null); int frame = Time.frameCount;
            if (frame == _combatFrame && ReferenceEquals(npc, _combatNpc) && ReferenceEquals(target, _combatTarget) && blocked == _combatBlocked) return false;
            _combatFrame = frame; _combatNpc = npc; _combatTarget = target; _combatBlocked = blocked;
            Services.FollowerVehicles.Combat(npc, target, blocked); return false;
        }
        private static bool HoldFire()
        { return Services.FollowerVehicles == null || !_mounted(null) ? true : FeedTarget(); }
        private static Type ResolveType(Assembly assembly, string name, bool throwOnError)
        {
            if (name.StartsWith("Apocapatrol.", StringComparison.Ordinal))
                return Services.FollowerVehicles != null ? Services.FollowerVehicles.ResolveType(name) : null;
            return assembly.GetType(name, throwOnError);
        }
        private static IEnumerable<CodeInstruction> LegacyTypes(IEnumerable<CodeInstruction> code)
        {
            var get = typeof(Assembly).GetMethod("GetType", new[] { typeof(string), typeof(bool) });
            foreach (var c in code)
            {
                if (Equals(c.operand, get)) { c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(QuestFollower), "ResolveType"); }
                yield return c;
            }
        }
        private static void Play(Animator animator, string name, int layer, float time)
        { if (_body == null || !_body.Valid || animator != _animator) animator.Play(name, layer, time); }
        private static IEnumerable<CodeInstruction> AnimationCalls(IEnumerable<CodeInstruction> code)
        {
            var play = typeof(Animator).GetMethod("Play", new[] { typeof(string), typeof(int), typeof(float) });
            foreach (var c in code)
            {
                if (Equals(c.operand, play)) { c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(QuestFollower), "Play"); }
                yield return c;
            }
        }
        private static void Shot()
        {
            if (_body == null || !_body.Valid) return;
            _body.Shoot();
            float until = _reloadUntil(null);
            if (Services.Npc != null) Services.Npc.SetFollowerDriven(_npc(null), true, until);
            if (until > Time.time && until != _reloadShown)
            { _reloadShown = until; _body.Reload(until - Time.time, Services.Animation != null && Services.Animation.ReloadsOneRoundAtATime(_weaponKey) ? Math.Max(1, _magazine(null)) : 0); }
        }
        internal static void Tick()
        {
            if (_npc == null) return;
            var npc = _active(null) ? _npc(null) : null;
            if (_current != npc || _animation != Services.Animation || (_body != null && !_body.Valid))
            {
                Release(); _current = npc; _animation = Services.Animation;
                if (npc != null)
                {
                    _animator = npc.GetComponent<Animator>() ?? npc.GetComponentInChildren<Animator>(true);
                    _attack = FsmUtil.Find(npc, "Attack");
                    _models = npc.GetComponentsInChildren<Transform>(true); _modelKeys = null; _weaponAt = 0;
                    _retry = 0;
                }
            }
            if (_current == null) return;
            if (Services.Npc != null) Services.Npc.SetFollowerDriven(_current, true, _reloadUntil(null));
            if (_body == null && _animator != null && _animator.isHuman && _animation != null && _animation.Ready && Time.unscaledTime >= _retry)
            {
                _retry = Time.unscaledTime + 1f;
                _body = _animation.Find(_animator); _ownsBody = _body == null;
                if (_body == null) _body = _animation.Attach(_animator);
                if (_body != null)
                {
                    UpdateWeapon();
                    Log.Always("[QuestsCompat] Follower animation attached: " + npc.name + " / " + (_weaponKey ?? "unarmed"));
                }
            }
            if (_body == null || !_body.Valid) return;
            UpdateWeapon();
            bool mounted = _mounted(null); var target = _target(null);
            bool animated = _weapon != null && _animation.WeaponKind(_weaponKey) != "Melee" && (mounted || _attack == null || _attack.ActiveStateName != "attack_melee");
            if (!animated)
            {
                _body.Suspended = true;
                if (Services.FollowerVehicles != null) Services.FollowerVehicles.Animated(_current, false);
                return;
            }
            if (_animator.speed != 1f) _animator.speed = 1f;
            _body.Suspended = false; _body.ManualVelocity = mounted; _body.Velocity = Vector3.zero;
            _body.Crouched = false; _body.Airborne = false; _body.Aiming = target != null; _body.Firing = false;
            if (target != null)
            {
                var v = target.transform.position + Vector3.up - (_current.transform.position + Vector3.up * 1.4f);
                _body.AimPitch = -Mathf.Atan2(v.y, Mathf.Sqrt(v.x * v.x + v.z * v.z)) * Mathf.Rad2Deg;
            }
            if (_reloadShown > 0 && _reloadUntil(null) <= Time.time) { _body.CancelReload(); _reloadShown = 0; }
            if (Services.FollowerVehicles != null) Services.FollowerVehicles.Animated(_current, mounted);
        }
        private static void UpdateWeapon()
        {
            if (_body == null || _animation == null || _models == null || Time.unscaledTime < _weaponAt) return;
            _weaponAt = Time.unscaledTime + .5f;
            if (_weapon != null && _weapon.gameObject.activeInHierarchy) return;
            if (_modelKeys == null || _modelKeys.Length != _models.Length)
            {
                // Child names do not change; look each one up once instead of every half second while unarmed.
                _modelKeys = new string[_models.Length];
                for (int i = 0; i < _models.Length; i++) _modelKeys[i] = _models[i] != null ? _animation.WeaponKey(_models[i].name) : null;
            }
            for (int i = 0; i < _models.Length; i++)
            {
                var child = _models[i];
                if (child == null || !child.gameObject.activeInHierarchy) continue;
                var key = _modelKeys[i];
                if (!string.IsNullOrEmpty(key) && _animation.HasWeaponPoses(key) && child.GetComponentInChildren<Renderer>(true) != null)
                { _weapon = child; _weaponKey = key; _body.SetWeapon(child, key); return; }
            }
            if (_weapon != null) _body.SetWeapon(null);
            _weapon = null; _weaponKey = null;
        }
        internal static void Late()
        {
            // Quests' LateUpdate keeps the passenger on its seat; the body receives locomotion from its rigidbody on foot.
            if (_current != null && _body != null && _body.Valid && _mounted(null)) _body.Velocity = Vector3.zero;
        }
        internal static void Release()
        {
            if (_current != null)
            {
                if (Services.Npc != null) Services.Npc.SetFollowerDriven(_current, false);
                if (Services.FollowerVehicles != null) Services.FollowerVehicles.Animated(_current, false);
            }
            if (_body != null && _body.Valid) { _body.CancelReload(); _body.ManualVelocity = false; if (_ownsBody) _body.Dispose(); else _body.Suspended = true; }
            _body = null; _current = null; _animator = null; _weapon = null; _weaponKey = null; _animation = null; _reloadShown = 0;
            _models = null; _modelKeys = null;
            _attack = null; _combatFrame = -1; _combatNpc = _combatTarget = null;
        }
        internal static void Uninstall()
        {
            Release(); foreach (var kv in LegacyFields) kv.Key.SetValue(null, kv.Value); LegacyFields.Clear();
            _npc = null; _plugin = null;
        }
        private const string ReportIn = "Report in: ";
        internal static void DescribeMarker(string key, ref string label, out Transform target)
        {
            target = null; if (_plugin == null || _activeQuests == null) return;
            var quests = _activeQuests(_questBook(_plugin));
            foreach (var q in quests)
            {
                string id = _id(q);
                if (id == null || id.Length != key.Length - 2 || string.CompareOrdinal(key, 2, id, 0, id.Length) != 0) continue;
                if (key[0] == 'T')
                {
                    string giver = _giverName(q) ?? "";
                    if (label == null || label.Length != ReportIn.Length + giver.Length || !label.StartsWith(ReportIn, StringComparison.Ordinal)
                        || string.CompareOrdinal(label, ReportIn.Length, giver, 0, giver.Length) != 0) label = ReportIn + giver;
                    target = _findLive(_giverId(q));
                }
                else label = _title(q);
                break;
            }
        }
    }
}
