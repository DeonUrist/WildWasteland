using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Micosmo.SensorToolkit.PlayMaker;
using UnityEngine;

namespace WildWasteland.Modules.NPC
{
    // Observe vanilla actions without replacing them. Gunplay sends corresponding events
    // at projectile creation/impact, including its immediate over-cap fallback.
    internal static class VanillaEvents
    {
        // (WW) Core.Hooks.SensorRayHitEnter has no __state: the lengthened burst sensor is kept here (prefix) and restored by the
        // finalizer of the same OnEnter (a small stack: an OnEnter inside an OnEnter stays paired).
        internal sealed class RayState { public SensorGetDetectionRayHit Action; public Micosmo.SensorToolkit.RaySensor Sensor; public float Length; }
        private static readonly List<RayState> _rays = new List<RayState>(4);
        private static readonly List<RayState> _rayPool = new List<RayState>(4);
        public static bool BeforeNpcShot(SensorGetDetectionRayHit __instance)
        {
            if (!NpcModule.Running) return true;
            try
            {
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Damage Ranged" || fsm.GameObject == null) return true;
                Brain.Shot(fsm.GameObject, fsm);        // (1.1.2) one ray = one shot of the magazine (counted whatever else is on)
                if (WeaponRanges.ProjectilesEnabled || (!Senses.On && !NpcModule.AimEnabled.Value && !NpcModule.BrainEnabled.Value)) return true;
                WeaponRanges.Kind kind;
                var owner = fsm.GameObject;
                if (!WeaponRanges.GunKindOf(owner, out kind)) return true;
                // Vanilla's gun sensor is only 80 m. Aim chooses positions from the
                // effective range; repulse at that reach so rifle/sniper NPCs can hit
                // from those positions without requiring Gunplay's projectiles.
                var refs = RefsOf(owner);
                if (NpcModule.AimEnabled.Value || NpcModule.BrainEnabled.Value)
                {
                    var sensor = refs.Sensor;
                    if (sensor != null)
                    {
                        RayState st;
                        if (_rayPool.Count > 0) { st = _rayPool[_rayPool.Count - 1]; _rayPool.RemoveAt(_rayPool.Count - 1); } else st = new RayState();
                        st.Action = __instance; st.Sensor = sensor; st.Length = sensor.Length;
                        _rays.Add(st);
                        sensor.Length = WeaponRanges.RangeOf(kind);
                        sensor.Pulse();
                    }
                }
                Vector3 origin = refs.Muzzle != null ? refs.Muzzle.position : owner.transform.position;
                if (Senses.On) Senses.Shot(owner.transform.root.gameObject, origin, kind, false);
            }
            catch (Exception e) { NpcModule.Warn("Vanilla NPC shot observation: " + e.Message); }
            return true;
        }

        // per NPC gunman: its burst sensor and the muzzle (fire_effect) of the gun in its hand. Looked up once instead of a full
        // GetComponentsInChildren + a name string per bone on every burst ray; the muzzle is looked up again when that gun is put away.
        private sealed class ShooterRefs { public GameObject Owner; public Micosmo.SensorToolkit.RaySensor Sensor; public Transform Muzzle; public float NextScan; }
        private static readonly System.Collections.Generic.Dictionary<int, ShooterRefs> _refs = new System.Collections.Generic.Dictionary<int, ShooterRefs>();
        private static ShooterRefs RefsOf(GameObject owner)
        {
            int id = owner.GetInstanceID();
            ShooterRefs r;
            if (!_refs.TryGetValue(id, out r) || r.Owner != owner)
            {
                if (_refs.Count > 512) _refs.Clear();
                var rayObject = owner.transform.Find("AttackRaycast_Ranged");
                r = new ShooterRefs { Owner = owner, Sensor = rayObject != null ? rayObject.GetComponent<Micosmo.SensorToolkit.RaySensor>() : null };
                _refs[id] = r;
            }
            if ((r.Muzzle == null || r.Muzzle.parent == null || !r.Muzzle.parent.gameObject.activeInHierarchy) && Time.time >= r.NextScan)
            {
                r.Muzzle = null; r.NextScan = Time.time + 1f;
                foreach (var t in owner.GetComponentsInChildren<Transform>(true))
                    if (t.name == "fire_effect" && t.parent != null && t.parent.name != "fire_effect" && t.parent.gameObject.activeInHierarchy)
                    { r.Muzzle = t; break; }
            }
            return r;
        }

        public static Exception AfterNpcShot(SensorGetDetectionRayHit __instance, Exception __exception)
        {
            for (int i = _rays.Count - 1; i >= 0; i--)
            {
                var st = _rays[i];
                if (!ReferenceEquals(st.Action, __instance)) continue;
                if (st.Sensor != null) st.Sensor.Length = st.Length;
                _rays.RemoveAt(i); st.Action = null; st.Sensor = null; _rayPool.Add(st);
                break;
            }
            return __exception;
        }

        // Shutdown: a sensor still lengthened (an exception between prefix and finalizer cannot leave one, but be safe) gets its length back
        internal static void Shutdown()
        {
            foreach (var st in _rays) if (st.Sensor != null) st.Sensor.Length = st.Length;
            _rays.Clear(); _refs.Clear();
        }

        internal static bool PlayerWeapon(GameObject weapon)
        {
            for (var t = weapon != null ? weapon.transform : null; t != null; t = t.parent)
                if (t.name == "PlayerCamera" || t.name == "PlayerCameraHolder") return true;
            return false;
        }

        public static bool BeforePlayerShot(Raycast __instance)
        {
            try
            {
                if (!Senses.On || WeaponRanges.ProjectilesEnabled) return true;
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.Name != "Attack" || fsm.GameObject == null || __instance.repeatInterval == null || __instance.repeatInterval.Value != 0f) return true;
                bool reload = false;
                foreach (var f in fsm.GameObject.GetComponents<PlayMakerFSM>()) if (f != null && f.FsmName == "Reload") { reload = true; break; }
                if (!reload || !PlayerWeapon(fsm.GameObject)) return true;
                var kind = WeaponRanges.Classify(fsm.GameObject.name);
                bool shotgun = false;
                var hit = fsm.GetState("hit");
                if (hit != null && hit.Actions != null)
                    foreach (var a in hit.Actions)
                    { var ic = a as IntCompare; if (ic != null && ic.integer1 != null && ic.integer1.Name == "pellets" && ic.integer2 != null) { shotgun = true; break; } }
                if (shotgun) kind = WeaponRanges.Kind.Shotgun;
                else if (kind == WeaponRanges.Kind.Shotgun) kind = WeaponRanges.Kind.Rifle;
                var from = fsm.GetOwnerDefaultTarget(__instance.fromGameObject);
                Vector3 position = from != null ? from.transform.position : fsm.GameObject.transform.position;
                foreach (var t in fsm.GameObject.GetComponentsInChildren<Transform>(true))
                    if ((t.name.StartsWith("muzzle_flesh_effect", StringComparison.Ordinal) || t.name.StartsWith("muzzle_flash", StringComparison.Ordinal)) && t.gameObject.activeInHierarchy)
                    { position = t.position; break; }
                Senses.Shot(Core.GameRefs.Player, position, kind, true);
            }
            catch (Exception e) { NpcModule.Warn("Vanilla player shot observation: " + e.Message); }
            return true;
        }

        public static void AfterDamage(SetFsmFloat __instance)
        {
            try
            {
                if (!Senses.On || __instance.fsmName == null || __instance.fsmName.Value != "Bodypart" || __instance.variableName == null || __instance.variableName.Value != "Damage" || __instance.setValue == null || __instance.setValue.Value >= 0f) return;
                var fsm = __instance.Fsm;
                if (fsm == null || fsm.GameObject == null) return;
                bool player = fsm.Name == "Attack" && PlayerWeapon(fsm.GameObject);
                bool ranged = fsm.Name == "Damage Ranged";
                if (!player && !ranged) return;
                // Melee stays vanilla with Gunplay installed, and still needs this observer.
                bool gun = ranged;
                if (player)
                    foreach (var f in fsm.GameObject.GetComponents<PlayMakerFSM>()) if (f != null && f.FsmName == "Reload") { gun = true; break; }
                if (gun && WeaponRanges.ProjectilesEnabled) return;
                var target = fsm.GetOwnerDefaultTarget(__instance.gameObject);
                if (target == null) return;
                Senses.Hurt(target, player ? Core.GameRefs.Player : fsm.GameObject.transform.root.gameObject);
            }
            catch (Exception e) { NpcModule.Warn("Vanilla hurt observation: " + e.Message); }
        }
    }
}
