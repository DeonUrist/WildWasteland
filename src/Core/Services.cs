using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Core
{
    /// <summary>
    /// Cross-module contracts (AGENTS.md 5.1: modules never reference each other's internals). A provider module assigns
    /// its implementation in Init and clears it in Shutdown; consumers null-check every call, so any module can be
    /// switched off without breaking the others (they fall back exactly as the standalone mods did when the provider mod was absent).
    /// </summary>
    public static class Services
    {
        /// <summary>Animation framework (PlayerBody module, ex Apocaplayer.ModAPI). Null when PlayerBody is off.</summary>
        public static IAnimationApi Animation;
        /// <summary>NPC AI (NPC module, ex NPCAI.Api + Nav structures). Null when NPC is off.</summary>
        public static INpcApi Npc;
        /// <summary>Guns (Guns module, ex Gunplay.Api). Null when Guns is off.</summary>
        public static IGunsApi Guns;
        /// <summary>Hit feedback (HUD module, ex GunplayHUD.Api). Null when HUD is off.</summary>
        public static IHitFeedback Hud;
        public static IVehicleStatusDisplay VehicleStatus;
        public static ICompassMarkers Compass;
        public static IMapMarkers Map;
        public static IFollowerVehicles FollowerVehicles;
        private static IPoiApi _poi;
        public static event Action<IPoiApi> PoiChanged;
        public static IPoiApi Poi
        {
            get { return _poi; }
            set
            {
                _poi = value;
                var changed = PoiChanged;
                if (changed != null) try { changed(value); } catch (Exception e) { Log.Error("POI provider notification", e); }
            }
        }

        /// <summary>Patrols raised: an occupant left a raider car (ex Apocapatrol.Patrol.BailOut). (car, previous passenger object, new on-foot NPC)</summary>
        public static event Action<GameObject, GameObject, GameObject> CrewBailedOut;
        /// <summary>Patrols raised: a raider car exploded (ex Apocapatrol.Explode.Blast).</summary>
        public static event Action<GameObject> VehicleExploded;

        public static void RaiseCrewBailedOut(GameObject car, GameObject pax, GameObject npc)
        {
            var h = CrewBailedOut; if (h == null) return;
            try { h(car, pax, npc); } catch (Exception e) { Log.Error("CrewBailedOut handler", e); }
        }
        public static void RaiseVehicleExploded(GameObject car)
        {
            var h = VehicleExploded; if (h == null) return;
            try { h(car); } catch (Exception e) { Log.Error("VehicleExploded handler", e); }
        }

        internal static void ClearEvents()
        {
            CrewBailedOut = null; VehicleExploded = null;
        }
    }

    public sealed class PoiDefinition
    {
        public string Id;
        public float Radius;
        public Func<bool> Enabled;
        public Action<Vector3, Quaternion, Action<GameObject>> Spawn;
        public Action<GameObject> Mark;
    }

    public interface IPoiApi
    {
        void Register(PoiDefinition definition);
        void Remove(string id);
    }

    public interface ICompassMarkers
    {
        bool VehiclesEnabled { get; }
        int Add(string owner, Func<Vector3> position, string icon, Color color);
        bool Exists(int id);
        bool Remove(int id);
        void RemoveAll(string owner);
    }

    // ------------------------------------------------------------------ Animation (PlayerBody)

    /// <summary>Mirror of Apocaplayer.ModAPI v1 (static part).</summary>
    public interface IAnimationApi
    {
        int ApiVersion { get; }
        string Version { get; }
        /// <summary>Animation bundle loaded (clips available).</summary>
        bool Ready { get; }
        bool PlayerThirdPerson { get; }
        AudioClip PlayerShout { get; }
        bool TryPlayerJumpHead(GameObject player, out Vector3 position);
        bool TryPlayerWeaponMuzzle(out Vector3 position);
        string[] ClipNames();
        bool IsPlayerClip(string name);
        AnimationClip GetClip(string name);
        string WeaponKey(string objectName);
        string WeaponKind(string weaponKey);
        bool HasWeaponPoses(string weaponKey);
        bool WeaponPose(string weaponKey, string clip, out Vector3 localPosition, out Quaternion localRotation);
        string ReloadClip(string weaponKey);
        float ReloadClipSeconds(string weaponKey);
        float RoundSeconds(string weaponKey);
        bool ReloadsOneRoundAtATime(string weaponKey);
        bool CocksAfterShot(string weaponKey);
        float NativeSpeed(string tier);
        int Count { get; }
        /// <summary>Drives an NPC/turret Animator with the player animation set. Null when another owner animates it.</summary>
        IAnimatedCharacter Attach(Animator animator);
        IAnimatedCharacter Find(Animator animator);
    }

    /// <summary>Mirror of Apocaplayer.ModAPI.Character.</summary>
    public interface IAnimatedCharacter
    {
        Animator Animator { get; }
        bool Valid { get; }
        string WeaponKey { get; }
        string WeaponKind { get; }
        bool Reloading { get; }
        bool Pumping { get; }
        string HandsClip { get; }
        string ActionClip { get; }
        string UpperClip { get; }
        float SpeedCap { get; }
        bool ManualVelocity { get; set; }
        Vector3 Velocity { get; set; }
        bool Crouched { get; set; }
        bool Aiming { get; set; }
        bool Firing { get; set; }
        bool Airborne { get; set; }
        float AimPitch { get; set; }
        bool TurnInPlace { get; set; }
        bool WalkToStop { get; set; }
        bool Suspended { get; set; }
        void SetWeapon(Transform model);
        void SetWeapon(Transform model, string weaponKey);
        void Shoot();
        void Shoot(float seconds);
        void Reload(float seconds);
        void Reload(float seconds, int rounds);
        void CancelReload();
        void Pump();
        void Jump();
        void Kick();
        void Throw();
        void Throw(bool rightHand);
        void Strike();
        void Strike(float seconds);
        void Dispose();
    }

    // ------------------------------------------------------------------ NPC

    /// <summary>A baked NPC navigation structure (camp/cave/building), ex NPCAI.Nav.Structure.</summary>
    public sealed class NavStructure
    {
        public Transform Root;
        /// <summary>Baked bounds (scene space at bake time; re-validate with Box.Contains(Root.position) after origin jumps).</summary>
        public Bounds? Box;
        public bool Baked;
        /// <summary>Floor height per cell (NaN = no floor), row-major with width <see cref="W"/>, origin Box.min.</summary>
        public float[] FloorY;
        public int W;
        public float Cell;
    }

    /// <summary>Mirror of NPCAI.Api v1 plus the nav structure list Apocapatrol read by reflection.</summary>
    public interface INpcApi
    {
        int ContractVersion { get; }
        void SetSimpleConvoyBandit(GameObject npc, bool enabled);
        void SetSimpleConvoyBanditState(GameObject npc, bool enabled, bool combat);
        void SetSimpleConvoyBanditTarget(GameObject npc, GameObject target);
        /// <summary>weaponKind 0..5 = Pistol, Smg, Rifle, Sniper, Shotgun, Crossbow (shared with Guns).</summary>
        void Shot(GameObject shooter, Vector3 position, int weaponKind, bool player);
        void Hurt(GameObject victim, GameObject attacker);
        float SpreadFactor(float distance);
        void Inform(GameObject npc, GameObject target);
        void SetFollowerDriven(GameObject npc, bool enabled, float reloadUntil = 0f);
        /// <summary>Fills <paramref name="into"/> (cleared first) with the current nav structures.</summary>
        void Structures(List<NavStructure> into);
    }

    // ------------------------------------------------------------------ Guns

    /// <summary>Mirror of Gunplay.Api v1.</summary>
    public interface IGunsApi
    {
        int ContractVersion { get; }
        /// <summary>Gunplay projectiles/tracers replace the vanilla hitscan (NPC vanilla observers stand down).</summary>
        bool ProjectilesEnabled { get; }
        bool PlayerProjectilesEnabled { get; }
        bool BlocksProjectile(Collider collider);
        float EffectiveRange(int kind);
        bool TryGetWeaponKind(GameObject owner, out int kind);
        /// <summary>Guns handles melee hits on fitted wheels (Patrols' own melee-wheel code stands down; ex Apocapatrol.MeleeWheels.RaiderHandles).</summary>
        bool HandlesFittedWheelMelee { get; }
    }

    // ------------------------------------------------------------------ HUD

    public enum MapDetail { Off, Ambiguous, On }

    public interface IMapMarkers
    {
        MapDetail Markers { get; }
        MapDetail Routes { get; }
        int Add(string owner, Func<Vector3> position, string icon, Color color, string label);
        bool Exists(int id);
        void Remove(int id);
        void RemoveAll(string owner);
    }

    public interface IFollowerVehicles
    {
        // Legacy Quests binds these types by reflection; the provider owns the mapping.
        Type ResolveType(string name);
        void Animated(GameObject npc, bool enabled);
        void Combat(GameObject npc, GameObject target, float blockedUntil);
    }

    public interface IVehicleStatusDisplay
    {
        bool TryShow(bool ignitionOff, bool handbrakeOn, bool playing, string volume);
    }

    /// <summary>Mirror of GunplayHUD.Api.</summary>
    public interface IHitFeedback
    {
        bool Enabled { get; }
        void PlayerHit(GameObject target, Vector3 point, float damage, bool head);
        void PartHit(GameObject target, Vector3 point, float percent);
        /// <summary>True while the Guns module reports hits through this API, so the HUD's vanilla SetFsmFloat observer must stay quiet.</summary>
        bool ExternalReporter { get; set; }
    }
}
