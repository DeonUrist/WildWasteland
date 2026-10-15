using System.Collections.Generic;
using DitzelGames.FastIK;
using HarmonyLib;
using UnityEngine;
using WildWasteland.Core;
using WildWasteland.Core.UI;

namespace WildWasteland.Modules.Performance
{
    /// <summary>
    /// RC6 (research C §1 "Animators", §2 Tier 2.9). Two parts:
    /// 1. NPC Animators set to AlwaysAnimate in their prefab (roots with a "Detection" FSM, e.g. Sources
    ///    data/hierarchy/prefabs/L/Lugnut.txt:L1-44) become CullUpdateTransforms: the state machine keeps running (parameters,
    ///    transitions, events), only bone writes stop while no renderer of the NPC is visible. Prefab assets only: Animator has no
    ///    ES3Type (not saved), and live instances are never touched, because WW modules switch the animators they drive to
    ///    AlwaysAnimate at runtime (NPC DogAnimations, Patrols RiderAnchorGuard, PlayerBody) and their write must win. Disable
    ///    restores the assets and every live animator of the same controller that still has our CullUpdateTransforms.
    /// 2. FastIKFabric.LateUpdate (70 prefab instances on NPC rigs, 46 on the first-person arms: Sources data/script_usage.tsv) is
    ///    skipped for rigs whose skinned mesh is not visible or that are farther than the preset distance from the camera.
    ///    Never skipped: anything under Player / PlayerCameraHolder. WW NPC DogAnimations disables the dog IK components itself
    ///    while it drives them (no interaction).
    /// Risk (research C §5): bone-mounted sensors / Bodypart colliders of an off-screen NPC keep the last pose.
    /// </summary>
    public sealed class AnimatorCulling : Feature, IScanClient
    {
        public override string PlanId { get { return "RC6"; } }
        public override string Key { get { return "Animator culling"; } }
        public override string Title { get { return "Off-screen NPC animation and IK"; } }
        public override string Category { get { return Categories.World; } }
        public override int Order { get { return 20; } }
        public override bool UsesPreset { get { return true; } }
        public override string Help { get { return "NPC skeletons stop updating while off-screen; leg/arm IK of NPCs runs only when visible and near."; } }

        Knob _cullAnim, _ikDist;
        public override void Bind(ModuleConfig cfg)
        {
            _cullAnim = NewKnob(cfg, "Cull NPC animators", "1 = AlwaysAnimate NPC animators become CullUpdateTransforms (prefabs).", 1, 1, 1, 0);
            _ikDist = NewKnob(cfg, "IK distance", "FastIK runs only within this distance (m) and while visible; 0 = always.", 25, 40, 60, 0);
        }

        internal static bool On;
        static float _ikSqr;
        sealed class ARec { public Animator A; public AnimatorCullingMode Orig; }
        readonly List<ARec> _assets = new List<ARec>();
        readonly Dictionary<RuntimeAnimatorController, AnimatorCullingMode> _byController = new Dictionary<RuntimeAnimatorController, AnimatorCullingMode>();
        readonly List<PlayMakerFSM> _fbuf = new List<PlayMakerFSM>();
        readonly HashSet<int> _npcRoots = new HashSet<int>(), _otherRoots = new HashSet<int>();
        public ScanTypes Types { get { return ScanTypes.Animators; } }

        protected override void OnEnable(FeatureMode mode)
        {
            EnsurePatched(typeof(IkPatch));
            Configure();
            On = true;
            if (_cullAnim.Value > 0.5f) RenderScan.Register(this);
        }

        protected override void OnDisable()
        {
            On = false;
            RenderScan.Unregister(this);
            RestoreAnimators();
            _ik.Clear();
        }

        void Configure() { float d = _ikDist.Value; _ikSqr = d > 0f ? d * d : 0f; }

        public override void OnSettingsChanged()
        {
            Configure();
            if (_cullAnim.Value > 0.5f) RenderScan.Register(this); else { RenderScan.Unregister(this); RestoreAnimators(); }
        }

        public override void OnSceneLoaded() { _ik.Clear(); _npcRoots.Clear(); _otherRoots.Clear(); RenderScan.RequestFull(); }

        void RestoreAnimators()
        {
            for (int i = 0; i < _assets.Count; i++) if (_assets[i].A != null) _assets[i].A.cullingMode = _assets[i].Orig;
            if (_byController.Count > 0)
            {
                var live = Resources.FindObjectsOfTypeAll<Animator>();
                AnimatorCullingMode o;
                for (int i = 0; i < live.Length; i++)
                {
                    var a = live[i];
                    if (a == null || !a.gameObject.scene.IsValid() || a.cullingMode != AnimatorCullingMode.CullUpdateTransforms) continue;
                    var rc = a.runtimeAnimatorController;
                    if (rc != null && _byController.TryGetValue(rc, out o)) a.cullingMode = o;
                }
            }
            _assets.Clear(); _byController.Clear(); _npcRoots.Clear(); _otherRoots.Clear();
        }

        public void OnAnimator(Animator a, ref ScanCtx c)
        {
            if (!c.Asset || c.Player || c.Vehicle || a.cullingMode != AnimatorCullingMode.AlwaysAnimate) return;
            if (!IsNpcRoot(c.Root)) return;
            _assets.Add(new ARec { A = a, Orig = a.cullingMode });
            if (a.runtimeAnimatorController != null) _byController[a.runtimeAnimatorController] = a.cullingMode;
            a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        bool IsNpcRoot(Transform root)
        {
            if (root == null) return false;
            int id = root.GetInstanceID();
            if (_npcRoots.Contains(id)) return true;
            if (_otherRoots.Contains(id)) return false;
            root.GetComponents(_fbuf);
            bool npc = false;
            for (int i = 0; i < _fbuf.Count; i++) if (_fbuf[i] != null && _fbuf[i].FsmName == "Detection") { npc = true; break; }
            _fbuf.Clear();
            (npc ? _npcRoots : _otherRoots).Add(id);
            return npc;
        }

        public void OnLight(Light l, ref ScanCtx c) { }
        public void OnRenderer(MeshRenderer r, ref ScanCtx c) { }
        public void OnParticles(ParticleSystem p, ref ScanCtx c) { }
        public void OnRoot(Transform root, ref ScanCtx c) { }
        public void OnPassDone() { }

        // ---- FastIK gate ----
        sealed class Ik { public Renderer R; public Transform T; public bool Player; }
        static readonly Dictionary<int, Ik> _ik = new Dictionary<int, Ik>();
        public static long IkSkipped;

        static bool Allow(FastIKFabric f)
        {
            Ik k;
            int id = f.GetInstanceID();
            if (!_ik.TryGetValue(id, out k))
            {
                // once per IK component: its root and the first skinned mesh of the rig
                var root = f.transform.root;
                string rn = root.name;
                k = new Ik { T = f.transform, Player = rn == "Player" || rn == "PlayerCameraHolder", R = root.GetComponentInChildren<SkinnedMeshRenderer>() };
                if (_ik.Count > 4096) _ik.Clear();
                _ik[id] = k;
            }
            if (k.Player) return true;
            if (k.R != null && !k.R.isVisible) return false;
            if (_ikSqr > 0f)
            {
                var cam = GameRefs.MainCamera;
                if (cam != null && (cam.transform.position - k.T.position).sqrMagnitude > _ikSqr) return false;
            }
            return true;
        }

        [HarmonyPatch]
        static class IkPatch
        {
            [HarmonyPatch(typeof(FastIKFabric), "LateUpdate"), HarmonyPrefix]
            static bool Pre(FastIKFabric __instance)
            {
                if (!On) return true;
                if (Allow(__instance)) return true;
                IkSkipped++;
                return false;
            }
        }

        public override void DrawStats(ToolLayout l)
        {
            l.Value("NPC animators culled (prefabs)", _assets.Count.ToString());
            l.Value("FastIK", _ik.Count + " rigs, " + IkSkipped + " updates skipped");
        }
    }
}
