using System.Collections.Generic;
using MapMagic.Terrains;
using UnityEngine;
using WildWasteland.Core;

namespace WildWasteland.Modules.Towns
{
    // Keep the exact applied town footprint, rather than repeatedly blending its feather into edited ground.
    // Only town-owned samples are restored; Vistas retains the surrounding terrain and road centreline.
    internal sealed class TownVistasTerrain
    {
        private sealed class Patch
        {
            internal TownPlan Plan;
            internal int X, Z;
            internal float[,] Heights;
            internal bool[,] Owned;
            internal float[,] EndWeight;
            internal float EndHeight;
            internal bool AdjustEnd;
        }
        private readonly TerrainData _data;
        private readonly Rect _world;
        private readonly Vector3 _size;
        private readonly int _resolution;
        private readonly List<Patch> _patches = new List<Patch>();
        internal TownVistasTerrain(TerrainTile tile, List<TownPlan> plans)
        {
            _data = tile.main.terrain.terrainData; _world = tile.WorldRect;
            _size = _data.size; _resolution = _data.heightmapResolution;
            int res = _data.heightmapResolution;
            float sx = _data.size.x / (res - 1), sz = _data.size.z / (res - 1);
            foreach (var plan in plans)
            {
                float outer = plan.Radius + plan.Feather;
                var min = plan.WorldCenter - new Vector3(outer, 0, outer); var max = plan.WorldCenter + new Vector3(outer, 0, outer);
                if (plan.GradeAccess) foreach (var local in plan.AccessPath)
                { var p = plan.World(local); var margin = new Vector3(plan.AccessWidth * .5f + 4, 0, plan.AccessWidth * .5f + 4); min = Vector3.Min(min, p - margin); max = Vector3.Max(max, p + margin); }
                int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - _world.x) / sx), 0, res - 1), z0 = Mathf.Clamp(Mathf.FloorToInt((min.z - _world.y) / sz), 0, res - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt((max.x - _world.x) / sx), 0, res - 1), z1 = Mathf.Clamp(Mathf.CeilToInt((max.z - _world.y) / sz), 0, res - 1);
                var patch = new Patch { Plan = plan, X = x0, Z = z0, Heights = _data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1),
                    Owned = new bool[z1 - z0 + 1, x1 - x0 + 1], EndWeight = new float[z1 - z0 + 1, x1 - x0 + 1] };
                var end = plan.AccessPath.Length > 1 ? plan.World(plan.AccessPath[plan.AccessPath.Length - 1]) : plan.WorldCenter;
                patch.AdjustEnd = plan.GradeAccess && plan.AccessPath.Length > 1 && new Vector2(end.x - plan.WorldCenter.x, end.z - plan.WorldCenter.z).magnitude > outer + 10;
                patch.EndHeight = tile.main.terrain.SampleHeight(tile.mapMagic.transform.TransformPoint(end)) / _data.size.y;
                var lengths = new float[plan.AccessPath.Length];
                for (int s = 1; s < lengths.Length; s++)
                { var d = plan.AccessPath[s] - plan.AccessPath[s - 1]; d.y = 0; lengths[s] = lengths[s - 1] + d.magnitude; }
                float length = lengths.Length > 0 ? Mathf.Max(.001f, lengths[lengths.Length - 1]) : 1;
                for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector3(_world.x + x * sx, 0, _world.y + z * sz);
                    float distance = new Vector2(p.x - plan.WorldCenter.x, p.z - plan.WorldCenter.z).magnitude;
                    bool owned = distance < outer;
                    float best = 0, endWeight = 0;
                    if (plan.GradeAccess && distance > plan.Radius) for (int s = 1; s < plan.AccessPath.Length; s++)
                    {
                        var a = plan.World(plan.AccessPath[s - 1]); var b = plan.World(plan.AccessPath[s]); a.y = b.y = 0;
                        var d = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, d) / Mathf.Max(.001f, d.sqrMagnitude));
                        float weight = Mathf.Clamp01((plan.AccessWidth * .5f + 4 - (p - a - d * t).magnitude) / 4);
                        weight = weight * weight * (3 - 2 * weight);
                        if (weight <= best) continue;
                        best = weight; endWeight = weight * Mathf.Lerp(lengths[s - 1], lengths[s], t) / length;
                    }
                    // Leave the road mouth with Vistas, including the height used to adjust this profile on later edits.
                    if (new Vector2(p.x - end.x, p.z - end.z).magnitude < Mathf.Max(10, plan.AccessWidth * .5f + 4)) best = 0;
                    patch.Owned[z - z0, x - x0] = owned || best > 0;
                    patch.EndWeight[z - z0, x - x0] = patch.AdjustEnd ? endWeight : 0;
                }
                _patches.Add(patch);
            }
        }
        internal void Restore(TerrainTile tile)
        {
            if (tile.main == null || tile.main.terrain == null || tile.main.terrain.terrainData != _data || tile.WorldRect != _world) return;
            if (_data.heightmapResolution != _resolution || _data.size != _size) return;
            var terrain = tile.main.terrain; int changed = 0;
            foreach (var patch in _patches)
            {
                var plan = patch.Plan; float endDelta = 0;
                if (patch.AdjustEnd)
                {
                    var end = tile.mapMagic.transform.TransformPoint(plan.World(plan.AccessPath[plan.AccessPath.Length - 1]));
                    endDelta = terrain.SampleHeight(end) / _data.size.y - patch.EndHeight;
                }
                int h = patch.Heights.GetLength(0), w = patch.Heights.GetLength(1);
                var live = _data.GetHeights(patch.X, patch.Z, w, h); bool dirty = false;
                for (int z = 0; z < h; z++) for (int x = 0; x < w; x++)
                {
                    if (!patch.Owned[z, x]) continue;
                    float target = Mathf.Clamp01(patch.Heights[z, x] + endDelta * patch.EndWeight[z, x]);
                    if (Mathf.Abs(live[z, x] - target) * _data.size.y < .003f) continue;
                    live[z, x] = target; dirty = true; changed++;
                }
                if (dirty) _data.SetHeights(patch.X, patch.Z, live);
            }
            if (changed > 0) Log.Info("[Towns] Vistas terrain restored: " + tile.coord + "; " + changed + " pad/access samples; buildings retained.");
        }
    }
}
