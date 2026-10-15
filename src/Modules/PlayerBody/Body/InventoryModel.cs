using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace WildWasteland.Modules.PlayerBody
{
    // Settings and TAB share editable RGBA PNGs. Native DXT1 has no alpha, and a GPU-only backup loses its pixels when
    // Low's masterTextureLimit changes. Bind the RawImages instead; CleanHUD mirrors their texture references.
    internal static class InventoryModel
    {
        private const string GameTexture = "player_character_2_UI";
        private static Texture2D _game, _settingsMale, _settingsFemale;
        private static bool _settingsPictureTried;
        private static readonly System.Collections.Generic.List<RawImage> _switched = new System.Collections.Generic.List<RawImage>();
        private static bool _applied;
        private static float _nextTry;
        private static int _tries;

        internal static void PrepareSettingsPictures()
        {
            if (_settingsPictureTried) return;
            _settingsPictureTried = true;
            _settingsMale = LoadPicture("Models/player_character_2_UI_male.png", "male");
            _settingsFemale = LoadPicture("Models/player_character_2_UI.png", "female");
        }

        private static Texture2D LoadPicture(string file, string gender)
        {
            string path = PlayerBodyModule.ModPath(file);
            Texture2D picture = null;
            try
            {
                if (!File.Exists(path) && gender == "male")
                    using (var source = typeof(InventoryModel).Assembly.GetManifestResourceStream("PlayerBody.Portraits.male.png"))
                        if (source != null)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                            using (var output = File.Create(path)) source.CopyTo(output);
                        }
                if (!File.Exists(path)) return null;
                picture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "Wild Wasteland " + gender + " TAB picture", hideFlags = HideFlags.HideAndDontSave };
                if (!ImageConversion.LoadImage(picture, File.ReadAllBytes(path), true)) throw new InvalidDataException("not a PNG/JPG");
                return picture;
            }
            catch (Exception e)
            {
                if (picture != null) UnityEngine.Object.Destroy(picture);
                PlayerBodyModule.Warn("Body picture " + path + ": " + e.Message);
                return null;
            }
        }

        internal static Texture SettingsPicture(bool female)
        {
            return female ? _settingsFemale : _settingsMale;
        }

        // Runner calls this every frame, but it only works until the swap is done for the loaded scene (a cheap bool check after that)
        public static void LateTick()
        {
            // (WW) module on/off is handled by PlayerBodyModule (Init resets, Shutdown calls Off; ReplaceDriver changes call Off / Reset)
            if (_applied || !PlayerBodyModule.Running || !PlayerBodyModule.ReplaceDriver.Value) return;
            if (Time.unscaledTime < _nextTry || _tries >= 15) return;   // the texture may load a moment after the scene: retry for ~30 s
            _nextTry = Time.unscaledTime + 2f; _tries++;
            Apply();
        }

        private static void Apply()
        {
            if (_game == null)
                foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>())
                    if (t != null && t.name == GameTexture) { _game = t; break; }
            if (_game == null) return;
            PrepareSettingsPictures();
            var mine = SettingsPicture(PlayerBodyModule.Female);
            if (mine == null) return;
            int n = 0;
            foreach (var r in Resources.FindObjectsOfTypeAll<RawImage>())
                if (r != null && r.gameObject.scene.IsValid() && r.texture == _game) { r.texture = mine; _switched.Add(r); n++; }
            _applied = n > 0;
            if (_applied) PlayerBodyModule.Info("TAB screen picture: " + n + " UI element(s) switched to " + PlayerBodyModule.Character.Value + " PNG");
        }

        public static void Off()
        {
            foreach (var r in _switched) if (r != null && _game != null) r.texture = _game;
            _switched.Clear();
            _applied = false; _tries = 0; _nextTry = 0f;
        }

        // Character switched: restore native references, then bind the other PNG on the next tick.
        public static void CharacterChanged()
        {
            Off();
        }

        // New scene: the native texture/UI may be new instances; the shared PNGs remain cached.
        public static void Reset()
        {
            Off(); _game = null;
        }
    }
}
