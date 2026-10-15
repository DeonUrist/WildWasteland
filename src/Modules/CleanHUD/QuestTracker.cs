using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Modules.CleanHUD
{
    internal sealed class QuestTracker
    {
        private static object _plugin;
        private static Func<object, List<string>> _lines;
        private static Func<object, ConfigEntry<bool>> _enabled;
        private static Func<object, bool> _ready, _log;
        private static Func<object, GUIStyle> _style;
        private static Func<object, object> _book;
        private static Func<object, IList> _active;
        private static Func<object, string> _id;
        private static readonly List<Color> Colors = new List<Color>();
        private static bool _owned;
        private readonly RectTransform _root;
        private readonly Font _font;
        private readonly Color _gold, _white;
        private readonly List<Text> _rows = new List<Text>();
        private readonly List<Image> _bars = new List<Image>();
        // Inputs of the last placement; the root is only moved when one of them changes.
        private Vector2 _inMin, _inMax, _inPos, _inPivot, _inSize; private Vector3 _inScale, _inCompass; private float _inLossy; private bool _inActive, _placed;

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install(CleanHudModule owner)
        {
            Clear(); PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("local.apocalypterquests", out info) || ReferenceEquals(info.Instance, null)) return;
            var type = info.Instance.GetType();
            _lines = Get<List<string>>(type, "trackerLines"); _enabled = Get<ConfigEntry<bool>>(type, "showTracker");
            _ready = Get<bool>(type, "ready"); _log = Get<bool>(type, "showLog"); _style = Get<GUIStyle>(type, "trackStyle");
            _book = Get<object>(type, "book"); _active = Get<IList>(type.Assembly.GetType("ApocalypterQuests.QuestBook", true), "Active");
            _id = Get<string>(type.Assembly.GetType("ApocalypterQuests.Quest", true), "Id");
            if (Core.Patches.Patch(owner, AccessTools.Method(type, "Shadowed"), prefix: AccessTools.Method(typeof(QuestTracker), "BeforeShadowed"))
                && Core.Patches.Patch(owner, AccessTools.Method(type, "RefreshLines"), postfix: AccessTools.Method(typeof(QuestTracker), "LinesChanged"))) _plugin = info.Instance;
        }
        private static Func<object, T> Get<T>(Type type, string name)
        {
            var p = Expression.Parameter(typeof(object), "p");
            return Expression.Lambda<Func<object, T>>(Expression.Convert(Expression.Field(Expression.Convert(p, type), AccessTools.Field(type, name)), typeof(T)), p).Compile();
        }
        private static bool BeforeShadowed(GUIStyle __2)
        { return !_owned || !CleanHudModule.Running || !CleanHudModule.Enabled.Value || _plugin == null || !ReferenceEquals(__2, _style(_plugin)); }
        internal static void Clear() { _owned = false; _plugin = null; _lines = null; _enabled = null; _ready = _log = null; _style = null; _book = null; _active = null; _id = null; Colors.Clear(); }
        private static void LinesChanged()
        {
            Colors.Clear(); if (_plugin == null) return;
            foreach (var q in _active(_book(_plugin))) Colors.Add(QuestColor(_id(q)));
        }
        private static Color QuestColor(string id)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < id.Length; i++) hash = unchecked((hash ^ id[i]) * 16777619u);
            return Color.HSVToRGB((hash % 360u) / 360f, .6f, 1f);
        }

        internal QuestTracker(Transform parent, Font font, Color gold, Color white)
        {
            _root = DialogueWidgets.Rect(parent, "quest tracker", 0, 0, 640, 0);
            _root.pivot = new Vector2(1, 1); _font = font; _gold = gold; _white = white; _owned = true;
        }
        internal void Tick(RectTransform insanity, RectTransform compass)
        {
            bool visible = _plugin != null && _ready(_plugin) && _enabled(_plugin).Value && !_log(_plugin)
                && Core.GameRefs.InGame && !(Cursor.visible && Cursor.lockState != CursorLockMode.Locked);
            if (_root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
            if (!visible) return;
            Vector2 inMin = insanity.anchorMin, inMax = insanity.anchorMax, inPos = insanity.anchoredPosition, inPivot = insanity.pivot;
            var rect = insanity.rect; Vector2 inSize = rect.size; Vector3 inScale = insanity.localScale; bool inActive = insanity.gameObject.activeSelf;
            var compassBottom = compass.TransformPoint(new Vector3(0, -compass.pivot.y * compass.rect.height, 0));
            float lossy = _root.lossyScale.y;
            if (!_placed || inMin != _inMin || inMax != _inMax || inPos != _inPos || inPivot != _inPivot || inSize != _inSize || inScale != _inScale
                || inActive != _inActive || compassBottom != _inCompass || lossy != _inLossy)
            {
                _placed = true; _inMin = inMin; _inMax = inMax; _inPos = inPos; _inPivot = inPivot; _inSize = inSize; _inScale = inScale; _inActive = inActive; _inCompass = compassBottom;
                _root.anchorMin = inMin; _root.anchorMax = inMax; _root.localScale = inScale;
                _root.anchoredPosition = inPos + new Vector2((1 - inPivot.x) * rect.width * inScale.x,
                    ((1 - inPivot.y) * rect.height - (inActive ? rect.height + 12 : 0)) * inScale.y);
                lossy = _root.lossyScale.y; _inLossy = lossy;
                var position = _root.position;
                position.y = Mathf.Min(position.y, compassBottom.y - 24f * lossy);
                _root.position = position;
            }
            var lines = _lines(_plugin); float y = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i == _rows.Count)
                {
                    var text = DialogueWidgets.Rect(_root, "quest", 0, 0, 620, 32).gameObject.AddAccent<Text>();
                    text.font = _font; text.fontSize = 22; text.color = _white; text.alignment = TextAnchor.UpperRight;
                    text.supportRichText = false; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
                    var shadow = text.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .95f); shadow.effectDistance = new Vector2(0, -1.5f);
                    var bar = DialogueWidgets.Rect(text.transform, "bar", 634, 4, 6, 20).gameObject.AddAccent<Image>(); bar.color = _gold; bar.raycastTarget = false;
                    _rows.Add(text); _bars.Add(bar);
                }
                var row = _rows[i]; if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);
                var color = i < Colors.Count ? Colors[i] : _gold; if (_bars[i].color != color) _bars[i].color = color;
                if (row.text != lines[i]) { row.text = lines[i]; row.rectTransform.sizeDelta = new Vector2(620, Mathf.Max(32, row.preferredHeight)); }
                var at = new Vector2(0, -y); if (row.rectTransform.anchoredPosition != at) row.rectTransform.anchoredPosition = at;
                y += row.rectTransform.sizeDelta.y + 8;
            }
            for (int i = lines.Count; i < _rows.Count; i++) if (_rows[i].gameObject.activeSelf) _rows[i].gameObject.SetActive(false);
        }
        internal void Dispose() { _owned = false; }
    }
}
