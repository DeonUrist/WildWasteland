using System.Collections.Generic;
using Den.Tools;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;

namespace WildWasteland.Modules.CleanHUD
{
    internal static class LocationMarkers
    {
        private const string Owner = "WildWasteland.Locations";
        private sealed class Location { internal int Marker; }
        private static readonly Dictionary<Transform, Location> Locations = new Dictionary<Transform, Location>();
        private static readonly List<Transform> Dead = new List<Transform>();
        private static readonly Api.Marker Style = new Api.Marker { Icon = "camp", ShowDistance = true, MaxDistance = 1000f };

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
        internal static void Observe(Transform root)
        {
            if (root != null && !Locations.ContainsKey(root)) Locations.Add(root, new Location());
        }
        internal static void Tick()
        {
            var player = Core.GameRefs.Player;
            bool visible = CleanHudModule.Running && CleanHudModule.Enabled.Value && CleanHudModule.ShowLocations.Value && player != null;
            var camera = Core.GameRefs.MainCamera;
            var origin = camera != null ? camera.transform.position : player != null ? player.transform.position : Vector3.zero;
            Dead.Clear();
            foreach (var pair in Locations)
            {
                var location = pair.Value;
                if (pair.Key == null || !visible || (pair.Key.position - origin).sqrMagnitude > 1000000f)
                {
                    if (location.Marker != 0) Api.Compass.Remove(location.Marker);
                    location.Marker = 0;
                    if (pair.Key == null) Dead.Add(pair.Key);
                }
                else if (location.Marker == 0)
                    location.Marker = Api.Compass.Add(Owner, pair.Key, Style);
            }
            foreach (var root in Dead) Locations.Remove(root);
        }
        internal static void Clear()
        {
            Api.Compass.RemoveAll(Owner); Locations.Clear(); Dead.Clear();
        }
        internal static void Shutdown()
        {
            TerrainTile.OnTileApplied -= Applied;
            Clear();
        }
    }
}
