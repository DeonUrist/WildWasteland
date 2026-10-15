using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class MapIcons
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        internal static Texture2D Get(string key) { Texture2D t; return key != null && Textures.TryGetValue(key, out t) ? t : null; }
        internal static void Preload()
        {
            string dir = Path.Combine(Core.Assets.ModuleDir("CleanHUD"), "Icons");
            if (!Directory.Exists(dir)) { CleanHudModule.Warn("Map/compass icon folder missing: " + dir); return; }
            foreach (string path in Directory.GetFiles(dir, "*.png"))
            {
                string key = Path.GetFileNameWithoutExtension(path); if (Textures.ContainsKey(key)) continue;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "CleanHUD." + key, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
                try
                {
                    if (!texture.LoadImage(File.ReadAllBytes(path), true)) throw new InvalidDataException("PNG could not be decoded");
                    Textures[key] = texture; texture = null;
                }
                catch (Exception e) { CleanHudModule.Warn("Map/compass icon " + key + ": " + e.Message); }
                finally { if (texture != null) UnityEngine.Object.Destroy(texture); }
            }
            CleanHudModule.Log("Map/compass icons loaded from disk: " + Textures.Count + " (" + dir + ")");
        }
        internal static void Dispose() { foreach (var t in Textures.Values) if (t != null) UnityEngine.Object.Destroy(t); Textures.Clear(); }
    }
}
