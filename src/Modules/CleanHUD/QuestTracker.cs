using System;
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
        private static bool _owned;
        private readonly RectTransform _root;
        private readonly Font _font;
        private readonly Color _gold, _white;
        private readonly List<Text> _rows = new List<Text>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install(CleanHudModule owner)
        {
            Clear(); PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("local.apocalypterquests", out info) || ReferenceEquals(info.Instance, null) || info.Metadata.Version != new Version(1, 8, 0)) return;
            var type = info.Instance.GetType();
            _lines = Get<List<string>>(type, "trackerLines"); _enabled = Get<ConfigEntry<bool>>(type, "showTracker");
            _ready = Get<bool>(type, "ready"); _log = Get<bool>(type, "showLog"); _style = Get<GUIStyle>(type, "trackStyle");
            if (Core.Patches.Patch(owner, AccessTools.Method(type, "Shadowed"), prefix: AccessTools.Method(typeof(QuestTracker), "BeforeShadowed"))) _plugin = info.Instance;
        }
        private static Func<object, T> Get<T>(Type type, string name)
        {
            var p = Expression.Parameter(typeof(object), "p");
            return Expression.Lambda<Func<object, T>>(Expression.Convert(Expression.Field(Expression.Convert(p, type), AccessTools.Field(type, name)), typeof(T)), p).Compile();
        }
        private static bool BeforeShadowed(GUIStyle __2)
        { return !_owned || !CleanHudModule.Running || !CleanHudModule.Enabled.Value || _plugin == null || !ReferenceEquals(__2, _style(_plugin)); }
        internal static void Clear() { _owned = false; _plugin = null; _lines = null; _enabled = null; _ready = _log = null; _style = null; }

        internal QuestTracker(Transform parent, Font font, Color gold, Color white)
        {
            _root = DialogueWidgets.Rect(parent, "quest tracker", 0, 0, 640, 0);
            _root.pivot = new Vector2(1, 1); _font = font; _gold = gold; _white = white; _owned = true;
        }
        internal void Tick(RectTransform insanity)
        {
            bool visible = _plugin != null && _ready(_plugin) && _enabled(_plugin).Value && !_log(_plugin)
                && Core.GameRefs.InGame && !(Cursor.visible && Cursor.lockState != CursorLockMode.Locked);
            if (_root.gameObject.activeSelf != visible) _root.gameObject.SetActive(visible);
            if (!visible) return;
            _root.anchorMin = insanity.anchorMin; _root.anchorMax = insanity.anchorMax; _root.localScale = insanity.localScale;
            _root.anchoredPosition = insanity.anchoredPosition + new Vector2((1 - insanity.pivot.x) * insanity.rect.width * insanity.localScale.x,
                ((1 - insanity.pivot.y) * insanity.rect.height - (insanity.gameObject.activeSelf ? insanity.rect.height + 12 : 0)) * insanity.localScale.y);
            var lines = _lines(_plugin); float y = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i == _rows.Count)
                {
                    var text = DialogueWidgets.Rect(_root, "quest", 0, 0, 620, 32).gameObject.AddComponent<Text>();
                    text.font = _font; text.fontSize = 22; text.color = _white; text.alignment = TextAnchor.UpperRight;
                    text.supportRichText = false; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
                    var shadow = text.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .95f); shadow.effectDistance = new Vector2(0, -1.5f);
                    var bar = DialogueWidgets.Rect(text.transform, "bar", 634, 4, 6, 20).gameObject.AddComponent<Image>(); bar.color = _gold; bar.raycastTarget = false;
                    _rows.Add(text);
                }
                var row = _rows[i]; if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);
                if (row.text != lines[i]) { row.text = lines[i]; row.rectTransform.sizeDelta = new Vector2(620, Mathf.Max(32, row.preferredHeight)); }
                row.rectTransform.anchoredPosition = new Vector2(0, -y); y += row.rectTransform.sizeDelta.y + 8;
            }
            for (int i = lines.Count; i < _rows.Count; i++) if (_rows[i].gameObject.activeSelf) _rows[i].gameObject.SetActive(false);
        }
        internal void Dispose() { _owned = false; }
    }
}
