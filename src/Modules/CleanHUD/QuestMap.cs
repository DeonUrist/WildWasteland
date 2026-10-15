using System;
using System.Collections.Generic;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.UI;
using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    internal sealed class QuestMap : IMapMarkers, IDisposable
    {
        private sealed class Entry
        {
            internal int Id; internal string Owner, Icon; internal Color Color; internal Func<Vector3> Position;
            internal RectTransform Root; internal RawImage Image; internal Texture IconTexture;
        }
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly HashSet<int> _ids = new HashSet<int>();
        private int _next;
        private PlayMakerFSM _fsm;
        private Camera _camera;
        private RectTransform _map, _root;
        private bool _captured, _nativeEnabled, _cameraNative, _fsmOn;
        private Camera _owned;
        private float _nextLookup;
        private readonly List<RawImage> _consumers = new List<RawImage>();
        public MapDetail Markers { get { return CleanHudModule.MapMarkers.Value; } }
        public MapDetail Routes { get { return CleanHudModule.MapRoutes.Value; } }
        public int Add(string owner, Func<Vector3> position, string icon, Color color, string label)
        { var e = new Entry { Id = ++_next, Owner = owner, Position = position, Icon = icon, Color = color }; _entries.Add(e); _ids.Add(e.Id); return e.Id; }
        public bool Exists(int id) { return _ids.Contains(id); }
        public bool Hidden(Vector3 scene) { return MapFog.Hides(scene); }
        public void Remove(int id)
        { for (int i = _entries.Count - 1; i >= 0; i--) if (_entries[i].Id == id) { Destroy(_entries[i]); _ids.Remove(id); _entries.RemoveAt(i); } }
        public void RemoveAll(string owner)
        { for (int i = _entries.Count - 1; i >= 0; i--) if (_entries[i].Owner == owner) { Destroy(_entries[i]); _ids.Remove(_entries[i].Id); _entries.RemoveAt(i); } }
        private static void Destroy(Entry e) { if (e.Root != null) UnityEngine.Object.Destroy(e.Root.gameObject); e.Root = null; }
        internal void FindMap() { FindMap(false); }
        // GameObject.Find and the FSM action walk back off to 5 s after a miss; opening the map forces a lookup.
        private void FindMap(bool force)
        {
            if (_fsm == null) _fsm = FsmUtil.Find(GameRefs.GameManager, "MapCheat");
            if (_fsm != null && !_captured) { _captured = true; _nativeEnabled = _fsm.enabled; force = true; }
            ApplyEnabled();
            if ((_map == null || _camera == null) && _fsm != null && (force || Time.unscaledTime >= _nextLookup))
            {
                foreach (var state in _fsm.FsmStates) foreach (var action in state.Actions)
                {
                    var activate = action as ActivateGameObject; if (activate == null) continue;
                    var go = _fsm.Fsm.GetOwnerDefaultTarget(activate.gameObject); if (go == null) continue;
                    // MapCheat_Camera may sit under an inactive object while the map is closed; GameObject.Find misses it then.
                    if (_camera == null) foreach (var c in go.GetComponentsInChildren<Camera>(true)) if (c.name == "MapCheat_Camera") { _camera = c; break; }
                    if (_map == null && go.name == "MapCheatUI")
                        foreach (var t in go.GetComponentsInChildren<RectTransform>(true)) if (t.name == "RawImage_Map") { _map = t; break; }
                }
                if (_camera == null)
                {
                    var go = GameObject.Find("MapCheat_Camera"); if (go != null) _camera = go.GetComponent<Camera>();
                }
                if (_map == null || _camera == null) _nextLookup = Time.unscaledTime + 5f;
            }
            if (_map != null && _root == null)
            {
                _root = DialogueWidgets.Rect(_map, "CleanHUD.QuestMarkers", 0, 0, 0, 0);
                _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one; _root.offsetMin = _root.offsetMax = Vector2.zero;
                _root.gameObject.AddComponent<RectMask2D>();
            }
            MapFrame.Sync(_map);
        }
        // The native map camera renders a 1024x1024 target every frame even while the map is closed: it only renders while
        // the map is shown (or while another UI shows its target). Projection helpers keep working on a disabled camera.
        private void DriveCamera(bool shown)
        {
            if (!ReferenceEquals(_camera, _owned))
            {
                RestoreCamera();
                if (_camera == null) return;
                _owned = _camera; _cameraNative = _camera.enabled; FindConsumers();
            }
            if (_owned == null) return;
            bool want = shown;
            for (int i = 0; i < _consumers.Count && !want; i++) want = _consumers[i] != null && _consumers[i].isActiveAndEnabled;
            if (_owned.enabled == want) return;
            _owned.enabled = want;
            if (want && _owned.gameObject.activeInHierarchy) _owned.Render();
        }
        private void FindConsumers()
        {
            _consumers.Clear();
            var target = _owned.targetTexture; if (target == null) return;
            foreach (var image in UnityEngine.Object.FindObjectsOfType<RawImage>(true))
                if (image.texture == target && image.name != "RawImage_Map") _consumers.Add(image);
            if (_consumers.Count > 0) CleanHudModule.Log("Map camera target is also shown by " + _consumers.Count + " other image(s); the camera renders while any is visible");
        }
        private void RestoreCamera()
        {
            if (_owned != null && _owned.enabled != _cameraNative) _owned.enabled = _cameraNative;
            _owned = null; _consumers.Clear();
        }
        internal void Tick()
        {
            bool fsmOn = _fsm != null && _fsm.isActiveAndEnabled && _fsm.ActiveStateName == "on";
            if (fsmOn && !_fsmOn && (_map == null || _camera == null)) FindMap(true);
            _fsmOn = fsmOn;
            bool mapShown = fsmOn && (_map == null || _map.gameObject.activeInHierarchy);
            DriveCamera(mapShown);
            bool play = GameRefs.Menu != null && GameRefs.Menu.ActiveStateName == "play";
            MapFog.Tick(_map, _camera, CleanHudModule.MapEnabled.Value && play && mapShown && _map != null);
            if (_root == null) return;
            bool open = CleanHudModule.Enabled.Value && CleanHudModule.MapEnabled.Value && Markers != MapDetail.Off && play
                && mapShown && _camera != null;
            if (_root.gameObject.activeSelf != open) _root.gameObject.SetActive(open);
            if (!open) return;
            foreach (var e in _entries)
            {
                if (e.Root == null)
                {
                    e.Root = DialogueWidgets.Rect(_root, "quest", 0, 0, 28, 28);
                    e.Image = e.Root.gameObject.AddComponent<RawImage>(); e.Image.raycastTarget = false;
                    e.IconTexture = MapIcons.Get(e.Icon) ?? Assets.Tex("g_" + e.Icon);
                }
                bool ambiguous = Markers == MapDetail.Ambiguous && !(e.Owner == LocationMarkers.Owner && LocationMarkers.Known(e.Position));
                e.Image.texture = ambiguous ? MapIcons.Get("unknown") : e.IconTexture;
                e.Image.color = ambiguous ? Color.white : e.Color;
                var scene = e.Position(); var p = _camera.WorldToViewportPoint(scene);
                bool visible = p.z > 0 && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1 && !(e.Owner == LocationMarkers.Owner && MapFog.Hides(scene));
                if (e.Root.gameObject.activeSelf != visible) e.Root.gameObject.SetActive(visible);
                if (visible) e.Root.anchoredPosition = new Vector2(p.x * _map.rect.width - 14, -(1 - p.y) * _map.rect.height + 14);
            }
        }
        internal void ApplyEnabled()
        {
            if (_fsm == null || !_captured) return;
            bool enabled = CleanHudModule.MapEnabled.Value;
            if (!enabled && _fsm.enabled && _fsm.ActiveStateName == "on") _fsm.SendEvent("back");
            if (_fsm.enabled != enabled) _fsm.enabled = enabled;
        }
        internal void SceneLoaded()
        {
            RestoreNative(); RestoreCamera(); _fsmOn = false; _nextLookup = 0; MapFrame.Restore(); MapFog.SceneLoaded();
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            foreach (var e in _entries) e.Root = null;
            _root = _map = null; _camera = null; _fsm = null;
        }
        private void RestoreNative()
        {
            if (_captured && _fsm != null)
            {
                if (!_nativeEnabled && _fsm.enabled && _fsm.ActiveStateName == "on") _fsm.SendEvent("back");
                _fsm.enabled = _nativeEnabled;
            }
            _captured = false;
        }
        public void Dispose() { SceneLoaded(); _entries.Clear(); _ids.Clear(); }
    }
}
