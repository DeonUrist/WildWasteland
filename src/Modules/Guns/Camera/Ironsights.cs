using HutongGames.PlayMaker.Actions;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Guns
{
    internal static class Ironsights
    {
        private static Camera _camera, _renderCamera;
        private static PlayMakerFSM _ads;
        private static float _nextFind, _target = 1f, _zoom = 1f;
        private static int _buttonFrame = -1;
        private static bool _buttonValue, _transparentProjection;

        internal static void Init(GunsModule owner)
        {
            Hooks.GetButtonDo.Prefix(owner, BeforeGetButton);
            Scheduler.EveryFrame(owner, "Ironsights", Tick, Phase.EarlyUpdate);
            Camera.onPreCull += PreCull;
            Camera.onPostRender += PostRender;
        }
        internal static void Shutdown()
        {
            Camera.onPreCull -= PreCull; Camera.onPostRender -= PostRender;
            Reset();
        }
        internal static void Reset()
        {
            Restore(); _camera = null; _ads = null; _nextFind = 0f;
            _target = _zoom = 1f; _buttonFrame = -1;
        }
        private static bool ThirdPerson { get { return Services.Animation != null && Services.Animation.PlayerThirdPerson; } }
        private static bool Aiming { get { return _ads != null && _ads.enabled && _ads.ActiveStateName == "ads"; } }

        private static void Tick()
        {
            if (_camera == null && Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 1f;
                var holder = GameRefs.PlayerCameraHolder;
                var eye = holder != null ? holder.transform.Find("PlayerCamera") : null;
                if (eye != null)
                {
                    _camera = eye.GetComponent<Camera>();
                    _ads = FsmUtil.Find(eye.gameObject, "AimDownSIghts_Hold") ?? FsmUtil.Find(eye.gameObject, "AimDownSights_Hold");
                }
            }
            if (!GunsModule.IronsightWheelZoom.Value) { _target = _zoom = 1f; return; }
            if (!GameRefs.InGame || ApocasetterBridge.InputBlocked || !Application.isFocused) return;
            if (!Aiming || ThirdPerson) _target = 1f;
            else
            {
                float wheel = Input.mouseScrollDelta.y;
                if (wheel != 0f)
                {
                    _target = Mathf.Clamp(_target * Mathf.Pow(.82f, wheel), .25f, 1f);
                    Log.Info("[Guns] Ironsight zoom target: " + (1f / _target).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x");
                }
            }
            _zoom = Mathf.Lerp(_zoom, _target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 16f));
            if (Mathf.Abs(_zoom - _target) < .0001f) _zoom = _target;
        }
        private static bool BeforeGetButton(GetButton action)
        {
            if (_ads == null || !ReferenceEquals(action.Fsm, _ads.Fsm) || !GunsModule.ToggleIronsights.Value) return true;
            if (action.buttonName.Value != "Aim Down Sights" || ApocasetterBridge.InputBlocked || !GameRefs.InGame || !Application.isFocused) return true;
            if (_buttonFrame != Time.frameCount)
            {
                _buttonFrame = Time.frameCount; _buttonValue = Aiming;
                if (Input.GetButtonDown(action.buttonName.Value)) _buttonValue = !_buttonValue;
            }
            action.storeResult.Value = _buttonValue;
            return false;
        }
        private static void PreCull(Camera cam)
        {
            if (cam != _camera || !GunsModule.IronsightWheelZoom.Value || ThirdPerson || !GameRefs.InPlay || Mathf.Abs(_zoom - 1f) < .0001f) return;
            Restore();
            _renderCamera = cam; _transparentProjection = cam.useJitteredProjectionMatrixForTransparentRendering;
            var projection = cam.projectionMatrix;
            projection.m00 /= _zoom; projection.m11 /= _zoom;
            cam.projectionMatrix = projection;
            cam.useJitteredProjectionMatrixForTransparentRendering = true;
            cam.ResetCullingMatrix();
        }
        private static void PostRender(Camera cam) { if (cam == _renderCamera) Restore(); }
        private static void Restore()
        {
            if (_renderCamera == null) return;
            _renderCamera.useJitteredProjectionMatrixForTransparentRendering = _transparentProjection;
            _renderCamera.ResetProjectionMatrix(); _renderCamera.ResetCullingMatrix(); _renderCamera = null;
        }
    }
}
