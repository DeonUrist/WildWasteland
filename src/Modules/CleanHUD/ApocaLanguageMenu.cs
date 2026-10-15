using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class ApocaLanguageMenu
    {
        internal sealed class Entry
        {
            internal string Code, Name;
            internal Font Font;
            internal bool Selected;
            internal Action Choose;
        }
        internal static Action<Component> MarkOwn;
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static List<Entry> Read()
        {
            var entries = new List<Entry>(); BepInEx.PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue("com.denis.apocalypter.apocalanguage", out info) || ReferenceEquals(info.Instance, null)) return entries;
            var assembly = info.Instance.GetType().Assembly;
            var plugin = assembly.GetType("ApocaLanguage.Plugin", true);
            var translator = assembly.GetType("ApocaLanguage.Translator", true);
            var language = assembly.GetType("ApocaLanguage.LanguageInfo", true);
            var codeField = language.GetField("Code"); var nameField = language.GetField("DisplayName");
            var font = assembly.GetType("ApocaLanguage.Fonts", true).GetMethod("For", new[] { language });
            var select = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), plugin.GetMethod("SelectLanguage"));
            MarkOwn = (Action<Component>)Delegate.CreateDelegate(typeof(Action<Component>), assembly.GetType("ApocaLanguage.Texts", true).GetMethod("MarkOwn"));
            string current = ((ConfigEntry<string>)plugin.GetField("LanguageEntry").GetValue(null)).Value;
            foreach (var item in (IEnumerable)translator.GetField("Languages").GetValue(null))
            {
                string code = (string)codeField.GetValue(item);
                entries.Add(new Entry { Code = code, Name = (string)nameField.GetValue(item), Selected = code == current,
                    Font = (Font)font.Invoke(null, new[] { item }), Choose = () => select(code) });
            }
            return entries;
        }
    }
}
