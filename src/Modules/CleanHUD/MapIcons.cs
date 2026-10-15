using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class MapIcons
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        internal static Texture2D Get(string key) { Texture2D t; return key != null && Textures.TryGetValue(key, out t) ? t : null; }
        internal static void Preload()
        {
            Make("cave", new[] { "................", "......####......", "....########....", "...##########...", "..############..", "..############..", ".#####....#####.", ".####......####.", "#####......#####", "####........####", "####........####", "###..........###", "###..........###", "###..........###", ".##..........##.", "................" });
            Make("wreck", new[] { "................", "................", "..........#.....", "..#......#......", "...#..###.......", "...#####.#......", "..###..##.#.....", ".###....#####...", "#####..#######..", "###############.", "#######.########", ".##############.", "..###......###..", "..###......###..", "................", "................" });
            Make("unknown", new[] { "................", ".....######.....", "...##########...", "...###....###...", "...###....###...", "..........###...", ".........###....", "........###.....", ".......###......", ".......###......", "................", ".......###......", ".......###......", "................", "................", "................" });
        }
        private static void Make(string key, string[] rows)
        {
            if (Textures.ContainsKey(key)) return;
            var t = new Texture2D(16, 16, TextureFormat.RGBA32, false) { name = "CleanHUD." + key, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var pixels = new Color32[256];
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) if (rows[y][x] == '#') pixels[(15 - y) * 16 + x] = new Color32(255, 255, 255, 255);
            t.SetPixels32(pixels); t.Apply(false, true); Textures[key] = t;
        }
        internal static void Dispose() { foreach (var t in Textures.Values) if (t != null) Object.Destroy(t); Textures.Clear(); }
    }
}
