using System.Collections.Generic;
using Den.Tools;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class LocationMarkers
    {
        private const string Owner = "WildWasteland.Locations";
        private sealed class Location
        {
            internal int Marker, MapMarker; internal Transform Root; internal Renderer Native; internal bool ForceOff;
            internal string Icon, Label; internal Color Color; internal IMapMarkers Map;
            internal Vector3 Position() { return Root != null ? Root.position : Vector3.zero; }
        }
        private static readonly Dictionary<Transform, Location> Locations = new Dictionary<Transform, Location>();
        private static readonly List<Transform> Dead = new List<Transform>();

        internal static void Init()
        {
            Clear();
            TerrainTile.OnTileApplied -= Applied;
            TerrainTile.OnTileApplied += Applied;
        }
        private static void Applied(TerrainTile tile, TileData data, StopToken stop)
        {
            if (!CleanHudModule.Running || tile == null) return;
            SceneCache.QueueRoot(tile.transform);
        }
        internal static void Observe(Transform icon)
        {
            var root = icon != null ? icon.parent : null;
            if (root == null || Locations.ContainsKey(root)) return;
            string label = root.name.Split('(')[0].Trim(), glyph = "diamond"; Color color = Color.white;
            if (label.StartsWith("Cave_", System.StringComparison.OrdinalIgnoreCase)) glyph = "cave";
            else if (label.StartsWith("Wreck_", System.StringComparison.OrdinalIgnoreCase)) glyph = "wreck";
            else if (label.StartsWith("Camp_", System.StringComparison.OrdinalIgnoreCase))
            {
                glyph = "camp"; int number;
                if (int.TryParse(label.Substring(5), out number)) color = number == 10 || number == 15 || number == 16 ? new Color(.31f, .86f, .39f) : new Color(1f, .31f, .31f);
            }
            var renderer = icon.GetComponent<Renderer>();
            Locations.Add(root, new Location { Root = root, Icon = glyph, Color = color, Label = label, Native = renderer, ForceOff = renderer != null && renderer.forceRenderingOff });
        }
        internal static void Tick()
        {
            var player = Core.GameRefs.Player;
            bool hud = CleanHudModule.Running && CleanHudModule.Enabled.Value;
            bool visible = hud && CleanHudModule.ShowCompass.Value && CleanHudModule.ShowLocations.Value && player != null;
            var camera = Core.GameRefs.MainCamera;
            var origin = camera != null ? camera.transform.position : player != null ? player.transform.position : Vector3.zero;
            Dead.Clear();
            foreach (var pair in Locations)
            {
                var location = pair.Value;
                if (location.Native != null) location.Native.forceRenderingOff = hud && CleanHudModule.MapEnabled.Value ? true : location.ForceOff;
                if (location.Map != Services.Map) { if (location.Map != null) location.Map.Remove(location.MapMarker); location.Map = Services.Map; location.MapMarker = 0; }
                bool onMap = pair.Key != null && hud && CleanHudModule.MapEnabled.Value && location.Map != null && location.Map.Markers != MapDetail.Off;
                if (!onMap) { if (location.Map != null) location.Map.Remove(location.MapMarker); location.MapMarker = 0; }
                else if (!location.Map.Exists(location.MapMarker)) location.MapMarker = location.Map.Add(Owner, location.Position, location.Icon, location.Color, location.Label);
                if (pair.Key == null || !visible || (pair.Key.position - origin).sqrMagnitude > 1000000f)
                {
                    if (location.Marker != 0) Api.Compass.Remove(location.Marker);
                    location.Marker = 0;
                    if (pair.Key == null) Dead.Add(pair.Key);
                }
                else if (location.Marker == 0)
                    location.Marker = Api.Compass.Add(Owner, pair.Key, new Api.Marker { Icon = location.Icon, Color = location.Color, ShowDistance = true, MaxDistance = 1000f });
            }
            foreach (var root in Dead) Locations.Remove(root);
        }
        internal static void Clear()
        {
            foreach (var location in Locations.Values)
            {
                if (location.Map != null) location.Map.Remove(location.MapMarker);
                if (location.Native != null) location.Native.forceRenderingOff = location.ForceOff;
            }
            Api.Compass.RemoveAll(Owner); Locations.Clear(); Dead.Clear();
        }
        internal static void Shutdown()
        {
            TerrainTile.OnTileApplied -= Applied;
            Clear();
        }
    }
}
