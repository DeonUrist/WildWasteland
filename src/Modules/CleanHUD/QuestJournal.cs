using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class QuestJournal
    {
        private static object _plugin;
        private static Func<object, bool> _ready, _open;
        private static Func<object, string> _text;
        private static Func<object, object> _book;
        private static Func<object, int> _done;
        private static Func<object, ConfigEntry<KeyCode>> _key;
        private static GameObject _root;
        private static GlyphText _title, _hint;
        private static RectTransform _content;
        private static ScrollRect _scroll;
        private static string _shownText;
        private static int _shownDone = -1;
        private static KeyCode _shownKey = KeyCode.None;
        private static readonly List<GlyphText> Rows = new List<GlyphText>();

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Install(CleanHudModule owner)
        {
            Clear(); PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("local.apocalypterquests", out info) || ReferenceEquals(info.Instance, null)) return;
            var type = info.Instance.GetType();
            _ready = Get<bool>(type, "ready"); _open = Get<bool>(type, "showLog"); _text = Get<string>(type, "logText");
            _book = Get<object>(type, "book"); _done = Get<int>(type.Assembly.GetType("ApocalypterQuests.QuestBook", true), "TotalDone");
            _key = Get<ConfigEntry<KeyCode>>(type, "logKey");
            if (!Core.Patches.Patch(owner, AccessTools.Method(type, "OnGUI"), transpiler: AccessTools.Method(typeof(QuestJournal), "JournalDrawing"))) return;
            _plugin = info.Instance;
            CleanHudModule.Log("Quest journal bridge ready; CleanHUD off restores the original J window");
        }
        private static Func<object, T> Get<T>(Type type, string name)
        {
            var p = Expression.Parameter(typeof(object), "p");
            return Expression.Lambda<Func<object, T>>(Expression.Convert(Expression.Field(Expression.Convert(p, type), AccessTools.Field(type, name)), typeof(T)), p).Compile();
        }
        private static IEnumerable<CodeInstruction> JournalDrawing(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions); int reads = 0;
            var field = AccessTools.Field(AccessTools.TypeByName("ApocalypterQuests.Plugin"), "showLog");
            for (int i = 0; i < code.Count; i++)
                if (code[i].LoadsField(field) && ++reads == 2)
                    code.Insert(++i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(QuestJournal), "NativeJournal")));
            if (reads != 2) throw new InvalidOperationException("Quest journal drawing changed; retaining the original window");
            return code;
        }
        private static bool NativeJournal(bool open)
        { return open && !(CleanHudModule.Running && CleanHudModule.Enabled.Value && _root != null && _root.activeSelf); }
        private static void Build()
        {
            _root = new GameObject("CleanHUD.Journal", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _root.hideFlags = HideFlags.HideAndDontSave; UnityEngine.Object.DontDestroyOnLoad(_root);
            _root.SetActive(false);
            var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scale = _root.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1920, 1080); scale.matchWidthOrHeight = 1;
            var panel = DialogueWidgets.Rect(_root.transform, "window", 0, 0, 1200, 900);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            var plate = panel.gameObject.AddComponent<Image>(); plate.sprite = Menu.PlateSprite(); plate.type = Image.Type.Sliced; plate.raycastTarget = false;
            _title = DialogueWidgets.Text(panel, "title", 50, 35, 1100, 60, 42, DialogueWidgets.Gold, "");
            _scroll = DialogueWidgets.Scroll(panel, "journal", 50, 115, 1100, 650, out _content); _scroll.inertia = false;
            _hint = DialogueWidgets.Text(panel, "hint", 50, 800, 1100, 50, 24, DialogueWidgets.Gold, "");
        }
        internal static void Tick()
        {
            if (_plugin == null) return;
            if (!CleanHudModule.Enabled.Value || !_ready(_plugin) || !_open(_plugin) || !Core.GameRefs.InGame
                || Core.UI.EditorSession.Busy || (Cursor.visible && Cursor.lockState != CursorLockMode.Locked)) { Hide(); return; }
            try
            {
                if (_root == null) Build();
                bool opening = !_root.activeSelf;
                string text = _text(_plugin) ?? "";
                if (_shownText != text) { Refresh(text); _shownText = text; }
                int done = _done(_book(_plugin));
                if (_shownDone != done) { _shownDone = done; _title.Set("QUESTS   (" + done.ToString(CultureInfo.InvariantCulture) + " COMPLETED)"); }
                var key = _key(_plugin).Value;
                if (_shownKey != key || opening) { _shownKey = key; _hint.Set("MOUSE WHEEL TO SCROLL   /   " + key.ToString().ToUpperInvariant() + " TO CLOSE"); }
                if (opening) { _root.SetActive(true); _scroll.verticalNormalizedPosition = 1; }
                float overflow = _content.sizeDelta.y - 650;
                if (overflow > 0 && Input.mouseScrollDelta.y != 0)
                    _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition + Input.mouseScrollDelta.y * 80 / overflow);
            }
            catch { Hide(); throw; }
        }
        private static void Refresh(string text)
        {
            float y = 0; int row = 0;
            foreach (string paragraph in text.Replace("\r", "").Split('\n'))
            {
                bool heading = paragraph.StartsWith("<b>", StringComparison.Ordinal);
                foreach (string line in TalkDialogue.Wrap(paragraph.Replace("<b>", "").Replace("</b>", ""), 1080))
                {
                    if (row == Rows.Count) Rows.Add(DialogueWidgets.Text(_content, "line", 10, y, 1080, 32, 26, Color.white, ""));
                    var label = Rows[row++]; label.rectTransform.anchoredPosition = new Vector2(10, -y);
                    label.color = heading ? DialogueWidgets.Gold : Color.white; label.Set(line); label.gameObject.SetActive(true); y += 32;
                }
            }
            for (int i = row; i < Rows.Count; i++) Rows[i].gameObject.SetActive(false);
            _content.sizeDelta = new Vector2(1100, y);
        }
        internal static void Hide() { if (_root != null) _root.SetActive(false); }
        internal static void Clear()
        {
            Hide(); if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null; _title = _hint = null; _content = null; _scroll = null; Rows.Clear(); _shownText = null; _shownDone = -1; _shownKey = KeyCode.None;
            _plugin = null; _ready = _open = null; _text = null; _book = null; _done = null; _key = null;
        }
    }
}
