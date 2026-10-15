using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Modules.CleanHUD
{
    // The game's forcedSquare font baked into an atlas (Resources/font_atlas.png + font_meta.json, 96 px).
    // The title scene does not load the game's Font asset, so the menus draw text from this atlas everywhere.
    internal sealed class GlyphFont
    {
        internal struct G { internal float X, Y, W, H, Ox, Top, Adv; }
        internal Texture2D Atlas;
        internal float Size, Ascent, Descent, AtlasW, AtlasH;
        internal readonly Dictionary<char, G> Map = new Dictionary<char, G>();

        private static GlyphFont _inst;
        internal static void Reset() { _inst = null; }
        internal static GlyphFont Get()
        {
            if (_inst != null && _inst.Atlas != null) return _inst;
            var f = new GlyphFont();
            f.Atlas = Assets.Tex("font_atlas");
            var asm = Assembly.GetExecutingAssembly();
            string json;
            using (var s = asm.GetManifestResourceStream("CleanHUD.font_meta.json"))
            using (var r = new StreamReader(s)) json = r.ReadToEnd();
            var d = (Dictionary<string, object>)Json.Parse(json);
            f.Size = Json.Num(d, "size", 96); f.Ascent = Json.Num(d, "ascent", 58); f.Descent = Json.Num(d, "descent", 17);
            f.AtlasW = Json.Num(d, "w", 1024); f.AtlasH = Json.Num(d, "h", 512);
            var gl = Json.Obj(d, "glyphs");
            foreach (var kv in gl)
            {
                var a = (List<object>)kv.Value;
                if (kv.Key.Length != 1 || a.Count < 7) continue;
                f.Map[kv.Key[0]] = new G { X = F(a[0]), Y = F(a[1]), W = F(a[2]), H = F(a[3]), Ox = F(a[4]), Top = F(a[5]), Adv = F(a[6]) };
            }
            _inst = f;
            return f;
        }
        private static float F(object o) { return o is double ? (float)(double)o : 0f; }

        internal float Measure(string text, float size, float spacing)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float s = size / Size, w = 0f; G g;
            for (int i = 0; i < text.Length; i++)
            {
                if (!Map.TryGetValue(text[i], out g) && !Map.TryGetValue(' ', out g)) continue;
                w += g.Adv * s + (i < text.Length - 1 ? spacing : 0f);
            }
            return w;
        }

        // The same atlas in IMGUI windows, sharing the menu's text metrics and assets.
        internal static void Draw(Rect rect, string text, float size, Color color, TextAnchor align)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
            var font = Get(); if (font == null || font.Atlas == null) return;
            float width = font.Measure(text, size, 0);
            if (width > rect.width && width > 0) { size *= rect.width / width; width = rect.width; }
            float scale = size / font.Size, x = rect.x;
            if (align == TextAnchor.MiddleCenter || align == TextAnchor.UpperCenter || align == TextAnchor.LowerCenter) x = rect.center.x - width / 2;
            else if (align == TextAnchor.MiddleRight || align == TextAnchor.UpperRight || align == TextAnchor.LowerRight) x = rect.xMax - width;
            float baseline = rect.center.y + (font.Ascent - font.Descent) * scale / 2;
            var previous = GUI.color; GUI.color = color;
            foreach (char ch in text)
            {
                G glyph;
                if (!font.Map.TryGetValue(ch, out glyph) && !font.Map.TryGetValue('?', out glyph)) continue;
                if (ch != ' ' && glyph.W > 1 && glyph.H > 1)
                    GUI.DrawTextureWithTexCoords(new Rect(x + glyph.Ox * scale, baseline - glyph.Top * scale, glyph.W * scale, glyph.H * scale), font.Atlas,
                        new Rect(glyph.X / font.AtlasW, 1f - (glyph.Y + glyph.H) / font.AtlasH, glyph.W / font.AtlasW, glyph.H / font.AtlasH));
                x += glyph.Adv * scale;
            }
            GUI.color = previous;
        }
    }

    // Single-line text drawn from the GlyphFont atlas. Works with Shadow/Outline (they modify the mesh).
    public sealed class GlyphText : MaskableGraphic
    {
        [NonSerialized] public string Text = "";
        [NonSerialized] public float Size = 24f;
        [NonSerialized] public float Spacing = 0f;
        [NonSerialized] public TextAnchor Align = TextAnchor.MiddleLeft;

        public override Texture mainTexture { get { var f = GlyphFont.Get(); return f != null ? f.Atlas : null; } }

        public void Set(string t)
        {
            t = t ?? "";
            if (t == Text) return;
            Text = t;
            SetVerticesDirty();
        }

        public float Width { get { return GlyphFont.Get().Measure(Text, Size, Spacing); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (string.IsNullOrEmpty(Text)) return;
            var f = GlyphFont.Get();
            if (f == null || f.Atlas == null) return;
            float s = Size / f.Size;
            var r = GetPixelAdjustedRect();
            float w = f.Measure(Text, Size, Spacing);
            float x;
            switch (Align)
            {
                case TextAnchor.UpperCenter: case TextAnchor.MiddleCenter: case TextAnchor.LowerCenter: x = r.center.x - w / 2f; break;
                case TextAnchor.UpperRight: case TextAnchor.MiddleRight: case TextAnchor.LowerRight: x = r.xMax - w; break;
                default: x = r.xMin; break;
            }
            float baseline;
            switch (Align)
            {
                case TextAnchor.UpperLeft: case TextAnchor.UpperCenter: case TextAnchor.UpperRight: baseline = r.yMax - f.Ascent * s; break;
                case TextAnchor.LowerLeft: case TextAnchor.LowerCenter: case TextAnchor.LowerRight: baseline = r.yMin + f.Descent * s; break;
                default: baseline = r.center.y + (f.Descent - f.Ascent) * s / 2f; break;
            }
            Color32 c = color;
            int v = 0;
            for (int i = 0; i < Text.Length; i++)
            {
                GlyphFont.G g;
                if (!f.Map.TryGetValue(Text[i], out g)) { if (f.Map.TryGetValue(' ', out g)) x += g.Adv * s + Spacing; continue; }
                if (g.W > 1f && g.H > 1f && Text[i] != ' ')
                {
                    float l = x + g.Ox * s, t = baseline + g.Top * s, rr = l + g.W * s, b = t - g.H * s;
                    float u0 = g.X / f.AtlasW, u1 = (g.X + g.W) / f.AtlasW, v1 = 1f - g.Y / f.AtlasH, v0 = 1f - (g.Y + g.H) / f.AtlasH;
                    vh.AddVert(new Vector3(l, b), c, new Vector2(u0, v0));
                    vh.AddVert(new Vector3(l, t), c, new Vector2(u0, v1));
                    vh.AddVert(new Vector3(rr, t), c, new Vector2(u1, v1));
                    vh.AddVert(new Vector3(rr, b), c, new Vector2(u1, v0));
                    vh.AddTriangle(v, v + 1, v + 2); vh.AddTriangle(v + 2, v + 3, v);
                    v += 4;
                }
                x += g.Adv * s + Spacing;
            }
        }
    }
}
