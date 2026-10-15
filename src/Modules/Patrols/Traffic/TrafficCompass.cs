using System.Collections.Generic;
using UnityEngine;

namespace WildWasteland.Modules.Patrols
{
    internal static class TrafficCompass
    {
        private const string Owner = "WildWasteland.VehicleGroups";
        private sealed class Tracked
        {
            internal int Id, Kind = -1;
            internal Transform Target;
            internal Vector3 Anchor;
            internal Vector3 Position() { return Target != null ? Target.position : TrafficSpace.Scene(Anchor); }
        }
        private static readonly Color[] Colors = { new Color32(255,220,50,255), new Color32(80,220,100,255), new Color32(255,80,80,255), new Color32(255,150,40,255) };
        private static readonly Dictionary<object, Tracked> Markers = new Dictionary<object, Tracked>();
        private static readonly HashSet<object> Seen = new HashSet<object>();
        private static readonly HashSet<string> Journeys = new HashSet<string>();
        private static readonly List<object> Dead = new List<object>();
        private static Core.ICompassMarkers _api;

        internal static void Clear()
        {
            if (_api != null && Markers.Count > 0) _api.RemoveAll(Owner);
            _api = null; Markers.Clear(); Seen.Clear(); Journeys.Clear(); Dead.Clear();
        }
        internal static void Tick()
        {
            var api = Core.Services.Compass;
            if (!ReferenceEquals(api, _api)) { Clear(); _api = api; }
            if (api == null || !api.VehiclesEnabled) { Clear(); return; }
            if (!PatrolsModule.Running || !Core.GameRefs.InPlay) return;
            Seen.Clear(); Journeys.Clear(); Dead.Clear();
            var journeys = WorldTraffic.CompassJourneys;
            if (PatrolsModule.WorldTrafficEnabled.Value && journeys != null)
                foreach (var journey in journeys)
                {
                    if (journey.foot) continue;   // foot patrols: never on the compass (map dot only, with Map Routes On)
                    bool alive = false;
                    foreach (var vehicle in journey.vehicles) if (!vehicle.retired) { alive = true; break; }
                    if (!alive) continue;
                    Journeys.Add(journey.id);
                    Track(journey, WorldTraffic.CompassTarget(journey), journey.position, Kind(journey.merchant, journey.faction, journey.convoy));
                }
            // Include parked/manual merchants and editor-spawned groups, without duplicating traffic members.
            foreach (var merchant in MerchantHost.All)
            {
                if (merchant == null || merchant.Data == null) continue;
                var marker = merchant.GetComponent<PatrolMarker>();
                if (marker != null && (marker.PlayerEntered || marker.Exploded || Journeys.Contains(marker.TrafficId))) continue;
                var encounter = merchant.GetComponent<EncounterCar>();
                object key = encounter != null && !string.IsNullOrEmpty(encounter.Event) ? (object)encounter.Event : merchant;
                Track(key, merchant.transform, Vector3.zero, 0);
            }
            foreach (var marker in PatrolMarker.All)
            {
                if (marker == null || marker.PlayerEntered || marker.Exploded || marker.SetupOnly || marker.Driver == null || Journeys.Contains(marker.TrafficId)) continue;
                var encounter = marker.GetComponent<EncounterCar>();
                if (encounter != null && encounter.Building) continue;
                object key = encounter != null && !string.IsNullOrEmpty(encounter.Event) ? (object)encounter.Event : marker;
                Track(key, marker.transform, Vector3.zero, Kind(false, marker.Faction, encounter != null && encounter.Convoy));
            }
            foreach (var pair in Markers) if (!Seen.Contains(pair.Key)) { api.Remove(pair.Value.Id); Dead.Add(pair.Key); }
            foreach (var key in Dead) Markers.Remove(key);
        }
        private static int Kind(bool merchant, string faction, bool convoy)
        { return merchant ? 0 : TrafficFactions.Normalize(faction) == "Coyotes" ? 1 : convoy ? 3 : 2; }
        private static void Track(object key, Transform target, Vector3 anchor, int kind)
        {
            if (!Seen.Add(key)) return;
            Tracked marker;
            if (!Markers.TryGetValue(key, out marker)) Markers.Add(key, marker = new Tracked());
            marker.Target = target; marker.Anchor = anchor;
            if (marker.Kind != kind) { if (marker.Id != 0) _api.Remove(marker.Id); marker.Id = 0; marker.Kind = kind; }
            if (marker.Id == 0 || !_api.Exists(marker.Id)) marker.Id = _api.Add(Owner, marker.Position, "car", Colors[kind]);
        }
    }

    internal sealed partial class WorldTraffic
    {
        internal static IList<TrafficJourney> CompassJourneys { get { return _instance != null ? _instance._journeys : null; } }
        internal static Transform CompassTarget(TrafficJourney journey)
        {
            Live live;
            if (_instance != null && journey.foot) return _instance.FootLeader(journey);   // leader walker, else the virtual position
            if (_instance == null || !_instance._live.TryGetValue(journey.id, out live)) return null;
            Transform target = null; int slot = int.MaxValue; bool truck = false;
            foreach (var pair in live.Cars)
            {
                var car = pair.Value; if (car == null) continue;
                var marker = car.GetComponent<PatrolMarker>();
                if (marker == null || marker.PlayerEntered || marker.Exploded) continue;
                bool cargo = pair.Key >= 0 && pair.Key < journey.vehicles.Length && journey.vehicles[pair.Key].truck;
                if (target != null && (!cargo && truck || cargo == truck && pair.Key >= slot)) continue;
                target = car.transform; slot = pair.Key; truck = cargo;
            }
            return target;
        }
    }
}
