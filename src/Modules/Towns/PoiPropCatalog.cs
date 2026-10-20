using System;
using System.Collections.Generic;
using ES3Internal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WildWasteland.Modules.Towns
{
    // Catalog work runs once in slices. Placing a prop never clones its native scripts, tags or Rigidbody.
    internal sealed class PoiPropCatalog : IDisposable
    {
        internal sealed class Shape
        {
            internal int Kind, Direction;
            internal Vector3 Center, Size;
            internal float Radius, Height;
            internal Mesh Mesh;
        }
        internal sealed class Asset
        {
            internal long Id;
            internal string Label, MeshName;
            internal Mesh Mesh;
            internal Material[] Materials;
            internal Vector3 NativeScale, Offset;
            internal Bounds Box;
            internal UnityEngine.Rendering.ShadowCastingMode Shadows;
            internal bool ReceiveShadows;
            internal readonly List<Shape> Shapes = new List<Shape>();
            internal GameObject Visual, Solid;
        }
        private readonly Dictionary<long, Asset> _assets = new Dictionary<long, Asset>();
        private readonly HashSet<ulong> _listed = new HashSet<ulong>();
        internal readonly List<Asset> All = new List<Asset>();
        private IEnumerator<KeyValuePair<long, Object>> _scan;
        private GameObject _templates;
        internal bool Started, Complete;
        internal bool Busy { get { return _scan != null; } }
        internal void Start()
        {
            if (Started) return;
            var manager = ES3ReferenceMgrBase.Current; if (manager == null) return;
            // ES3 registers new objects while the world runs. Take a stable reference snapshot only for the editor catalog.
            var snapshot = new List<KeyValuePair<long, Object>>(manager.idRef);
            _scan = snapshot.GetEnumerator(); Started = true;
        }
        internal bool Step()
        {
            if (_scan == null) return false;
            if (!_scan.MoveNext())
            {
                _scan.Dispose(); _scan = null; Complete = true;
                All.Sort(Compare); return false;
            }
            var pair = _scan.Current; var filter = pair.Value as MeshFilter;
            if (filter == null || filter.gameObject.scene.IsValid()) return true;
            Asset asset = Capture(pair.Key, filter); if (asset == null) return true;
            ulong key = unchecked((ulong)(uint)asset.Mesh.GetInstanceID() * 1099511628211UL);
            foreach (var material in asset.Materials) key = unchecked((key ^ (uint)(material == null ? 0 : material.GetInstanceID())) * 1099511628211UL);
            if (_listed.Add(key)) All.Add(asset);
            return true;
        }
        private static int Compare(Asset a, Asset b) { return string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase); }
        internal Asset Resolve(PoiDressProp prop)
        {
            Asset asset;
            if (_assets.TryGetValue(prop.Asset, out asset)) return asset.MeshName == prop.Mesh ? asset : null;
            var manager = ES3ReferenceMgrBase.Current;
            var filter = manager == null ? null : manager.Get(prop.Asset, true) as MeshFilter;
            if (filter == null || filter.gameObject.scene.IsValid() || filter.sharedMesh == null || filter.sharedMesh.name != prop.Mesh) return null;
            return Capture(prop.Asset, filter);
        }
        private Asset Capture(long id, MeshFilter filter)
        {
            Asset asset; if (_assets.TryGetValue(id, out asset)) return asset;
            var mesh = filter.sharedMesh; var renderer = filter.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null || mesh.vertexCount == 0 || mesh.vertexCount > 100000) return null;
            var scale = filter.transform.lossyScale;
            if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0) return null;
            var size = Vector3.Scale(mesh.bounds.size, scale);
            if (Mathf.Max(size.x, size.y, size.z) > 80 || size.sqrMagnitude < .0001f) return null;
            var root = filter.transform; while (root.parent != null) root = root.parent;
            asset = new Asset { Id = id, Mesh = mesh, MeshName = mesh.name,
                Label = filter.name + " · " + mesh.name + " · " + root.name, Materials = renderer.sharedMaterials,
                NativeScale = scale, Offset = -Vector3.Scale(mesh.bounds.center - Vector3.up * mesh.bounds.extents.y, scale),
                Box = new Bounds(Vector3.up * size.y * .5f, size), Shadows = renderer.shadowCastingMode, ReceiveShadows = renderer.receiveShadows };
            foreach (var collider in filter.GetComponents<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                var box = collider as BoxCollider; var sphere = collider as SphereCollider; var capsule = collider as CapsuleCollider; var surface = collider as MeshCollider;
                if (box != null) asset.Shapes.Add(new Shape { Kind = 0, Center = box.center, Size = box.size });
                else if (sphere != null) asset.Shapes.Add(new Shape { Kind = 1, Center = sphere.center, Radius = sphere.radius });
                else if (capsule != null) asset.Shapes.Add(new Shape { Kind = 2, Center = capsule.center, Radius = capsule.radius, Height = capsule.height, Direction = capsule.direction });
                else if (surface != null && surface.sharedMesh != null) asset.Shapes.Add(new Shape { Kind = 3, Mesh = surface.sharedMesh });
            }
            _assets.Add(id, asset); return asset;
        }
        internal GameObject Create(Asset asset, Transform parent, PoiDressProp prop, bool solid)
        {
            if (_templates == null) { _templates = new GameObject("POI dressing static templates"); _templates.SetActive(false); }
            var template = solid ? asset.Solid : asset.Visual;
            if (template == null)
            {
                template = new GameObject("POI prop " + asset.MeshName); template.transform.SetParent(_templates.transform, false);
                var geometry = new GameObject("Geometry"); geometry.transform.SetParent(template.transform, false);
                geometry.transform.localPosition = asset.Offset; geometry.transform.localScale = asset.NativeScale;
                geometry.AddComponent<MeshFilter>().sharedMesh = asset.Mesh;
                var renderer = geometry.AddComponent<MeshRenderer>(); renderer.sharedMaterials = asset.Materials;
                renderer.shadowCastingMode = asset.Shadows; renderer.receiveShadows = asset.ReceiveShadows;
                if (solid)
                {
                    foreach (var shape in asset.Shapes)
                    {
                        if (shape.Kind == 0) { var c = geometry.AddComponent<BoxCollider>(); c.center = shape.Center; c.size = shape.Size; }
                        else if (shape.Kind == 1) { var c = geometry.AddComponent<SphereCollider>(); c.center = shape.Center; c.radius = shape.Radius; }
                        else if (shape.Kind == 2) { var c = geometry.AddComponent<CapsuleCollider>(); c.center = shape.Center; c.radius = shape.Radius; c.height = shape.Height; c.direction = shape.Direction; }
                        else { var c = geometry.AddComponent<MeshCollider>(); c.sharedMesh = shape.Mesh; c.convex = false; }
                    }
                    if (asset.Shapes.Count == 0) { var c = geometry.AddComponent<MeshCollider>(); c.sharedMesh = asset.Mesh; c.convex = false; }
                }
                if (solid) asset.Solid = template; else asset.Visual = template;
            }
            var go = Object.Instantiate(template, parent, false); Pose(go.transform, prop); return go;
        }
        internal static void Pose(Transform transform, PoiDressProp prop)
        { transform.localPosition = prop.Position; transform.localRotation = Quaternion.Euler(prop.Rotation); transform.localScale = prop.Scale; }
        public void Dispose()
        {
            if (_scan != null) _scan.Dispose(); _scan = null;
            if (_templates != null) { _templates.SetActive(false); Object.Destroy(_templates); } _templates = null;
            _assets.Clear(); All.Clear(); _listed.Clear(); Started = Complete = false;
        }
    }
}
