using System;
using UnityEngine;
using UnityEngine.Rendering;
using WildWasteland.Core;

namespace WildWasteland.Modules.Weather
{
    // Bounds only: all pixels integrate continuous dust, including from inside the eye.
    internal sealed class WeatherVortexSmoke : IDisposable
    {
        private readonly Mesh _bounds;
        private readonly Material _volume;
        private readonly WeatherSmoke _clouds;
        private readonly Material _cloudMaterial;
        private readonly MaterialPropertyBlock _cloudProperties = new MaterialPropertyBlock();
        private Camera _camera;
        private static readonly int Profile = Shader.PropertyToID("_SpiralProfile"), Steps = Shader.PropertyToID("_Steps");
        internal const float TopRadius = 3200f;
        internal static float Width(int slot) { switch (slot%8) { case 0:return 1; case 1:return .10f; case 2:return .72f; case 3:return .18f; case 4:return .92f; case 5:return .065f; case 6:return .56f; default:return .25f; } }
        internal static float TopWidth(int slot) { return Mathf.Max(.45f,Width(slot)); }
        internal WeatherVortexSmoke(Material material, Material smoke)
        {
            _volume=new Material(material); _volume.renderQueue=3001;
            _cloudMaterial=new Material(smoke); _cloudMaterial.renderQueue=3002;
            _clouds=new WeatherSmoke(_cloudMaterial,true);
            _bounds=new Mesh { name="WW.Weather.VortexSmoke.Bounds" };
            _bounds.vertices=new[] {
                new Vector3(-2500,-30,-2500),new Vector3(2500,-30,-2500),new Vector3(2500,-30,2500),new Vector3(-2500,-30,2500),
                new Vector3(-2500,2100,-2500),new Vector3(2500,2100,-2500),new Vector3(2500,2100,2500),new Vector3(-2500,2100,2500) };
            _bounds.triangles=new[] {0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0,0,1,2,0,2,3,4,7,6,4,6,5};
            _bounds.RecalculateBounds(); _bounds.UploadMeshData(true);
        }
        internal void Begin(Camera camera) { _camera=camera; _clouds.Begin(camera); }
        internal void Draw(int slot, Vector3 position, Quaternion rotation, WeatherVisualDetail detail, MaterialPropertyBlock properties)
        {
            float width=Width(slot);
            properties.SetFloat(Steps,detail == WeatherVisualDetail.Low ? 64 : detail == WeatherVisualDetail.Max ? 128 : 96);
            properties.SetVector(Profile,new Vector4(width,slot*1.731f,.9f+slot%3*.07f,0));
            Graphics.DrawMesh(_bounds,Matrix4x4.TRS(position,rotation,new Vector3(width,1,width)),_volume,
                2,_camera,0,properties,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);
            _cloudProperties.Clear();
            _cloudProperties.SetColor("_Tint",properties.GetColor("_Tint"));
            _cloudProperties.SetColor("_CloudTint",properties.GetColor("_CloudTint"));
            _cloudProperties.SetFloat("_Life",properties.GetFloat("_Life"));
            _cloudProperties.SetFloat("_Critical",1); _cloudProperties.SetFloat("_VortexCloud",1);
            _cloudProperties.SetFloat("_FrameBlend",detail == WeatherVisualDetail.Low ? 0 : 1);
            _cloudProperties.SetVector("_CloudOrigin",new Vector4(position.x,position.y,position.z,width));
            _cloudProperties.SetVector("_RegionAxisZ",rotation*Vector3.forward);
            _clouds.Draw(slot,position,rotation,width,TopWidth(slot),detail,false,_cloudProperties);
        }
        internal void Suspend() { _camera=null; _clouds.Suspend(); }
        public void Dispose() { Suspend(); _clouds.Dispose(); UnityEngine.Object.Destroy(_bounds); UnityEngine.Object.Destroy(_volume); UnityEngine.Object.Destroy(_cloudMaterial); }
    }
}

