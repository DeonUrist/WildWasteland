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
            internal int Id; internal string Owner, Icon, Label; internal Color Color; internal Func<Vector3> Position;
            internal RectTransform Root; internal RawImage Image;
        }
        private readonly List<Entry> _entries = new List<Entry>();
        private int _next;
        private PlayMakerFSM _fsm;
        private Camera _camera;
        private RectTransform _map, _root;
        public int Add(string owner, Func<Vector3> position, string icon, Color color, string label)
        { var e = new Entry { Id = ++_next, Owner = owner, Position = position, Icon = icon, Color = color, Label = label }; _entries.Add(e); return e.Id; }
        public bool Exists(int id) { return _entries.Exists(e => e.Id == id); }
        public void Remove(int id)
        { for (int i = _entries.Count - 1; i >= 0; i--) if (_entries[i].Id == id) { Destroy(_entries[i]); _entries.RemoveAt(i); } }
        public void RemoveAll(string owner)
        { for (int i = _entries.Count - 1; i >= 0; i--) if (_entries[i].Owner == owner) { Destroy(_entries[i]); _entries.RemoveAt(i); } }
        private static void Destroy(Entry e) { if (e.Root != null) UnityEngine.Object.Destroy(e.Root.gameObject); e.Root = null; }
        internal void FindMap()
        {
            if (_entries.Count == 0) return;
            if (_fsm == null) _fsm = FsmUtil.Find(GameRefs.GameManager, "MapCheat");
            if (_map == null && _fsm != null)
                foreach (var state in _fsm.FsmStates) foreach (var action in state.Actions)
                {
                    var activate = action as ActivateGameObject; if (activate == null) continue;
                    var go = _fsm.Fsm.GetOwnerDefaultTarget(activate.gameObject); if (go == null || go.name != "MapCheatUI") continue;
                    foreach (var t in go.GetComponentsInChildren<RectTransform>(true)) if (t.name == "RawImage_Map") { _map = t; break; }
                }
            if (_camera == null)
            {
                var go = GameObject.Find("MapCheat_Camera"); if (go != null) _camera = go.GetComponent<Camera>();
            }
            if (_map != null && _root == null)
            {
                _root = DialogueWidgets.Rect(_map, "CleanHUD.QuestMarkers", 0, 0, 0, 0);
                _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one; _root.offsetMin = _root.offsetMax = Vector2.zero;
                _root.gameObject.AddComponent<RectMask2D>();
            }
        }
        internal void Tick()
        {
            if (_root == null) return;
            bool open = CleanHudModule.Enabled.Value && GameRefs.Menu != null && GameRefs.Menu.ActiveStateName == "play"
                && _fsm != null && _fsm.isActiveAndEnabled && _fsm.ActiveStateName == "on" && _camera != null && _map.gameObject.activeInHierarchy;
            if (_root.gameObject.activeSelf != open) _root.gameObject.SetActive(open);
            if (!open) return;
            foreach (var e in _entries)
            {
                if (e.Root == null)
                {
                    e.Root = DialogueWidgets.Rect(_root, "quest", 0, 0, 28, 28);
                    e.Image = e.Root.gameObject.AddComponent<RawImage>(); e.Image.texture = Assets.Tex("g_" + e.Icon); e.Image.color = e.Color; e.Image.raycastTarget = false;
                    DialogueWidgets.Text(e.Root, "label", 32, 0, 480, 28, 19, e.Color, e.Label);
                }
                var p = _camera.WorldToViewportPoint(e.Position());
                bool visible = p.z > 0 && p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1;
                if (e.Root.gameObject.activeSelf != visible) e.Root.gameObject.SetActive(visible);
                if (visible) e.Root.anchoredPosition = new Vector2(p.x * _map.rect.width - 14, -(1 - p.y) * _map.rect.height + 14);
            }
        }
        internal void SceneLoaded()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            foreach (var e in _entries) e.Root = null;
            _root = _map = null; _camera = null; _fsm = null;
        }
        public void Dispose() { SceneLoaded(); _entries.Clear(); }
    }
}
