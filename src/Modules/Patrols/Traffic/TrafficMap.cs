using UnityEngine;
namespace WildWasteland.Modules.Patrols
{
    internal sealed partial class WorldTraffic
    {
        private Camera _gameMap;
        private RectTransform _gameMapImage;
        private PlayMakerFSM _mapCheat;
        private float _nextMapScan,_gameInkAt;
        private readonly TrafficMapInk _gameInk=new TrafficMapInk();
        private struct MapLabel { internal Rect rect; internal Color color; internal string text; }
        private readonly System.Collections.Generic.List<MapLabel> _mapLabels=new System.Collections.Generic.List<MapLabel>();
        private readonly System.Collections.Generic.List<Rect> _mapBattles=new System.Collections.Generic.List<Rect>();
        private Vector2 _gameInkSize;
        private bool _inkNames,_inkHide,_mapWasOpen;
        private readonly Vector3[] _mapCorners=new Vector3[4];
        private Texture2D _skull;
        private Texture2D _mapDot;
        private GUIStyle _mapLabelStyle;
        private GUIStyle _rigNoticeStyle;
        private GUIStyle _rigXStyle;
        private Vector3 OffscreenTrafficPoint(Vector3 center,Vector3 direction)
        {
            // Route creation only; include the inactive native camera when M is closed.
            if(_gameMap==null)foreach(var camera in Resources.FindObjectsOfTypeAll<Camera>())
                if(camera.name=="MapCheat_Camera"&&camera.gameObject.scene.IsValid()){_gameMap=camera;break;}
            direction.y=0f;direction.Normalize();
            float reach=Mathf.Max(PatrolsModule.TrafficUnloadRange.Value,PatrolsModule.TrafficLoadRange.Value)+250f;
            if(_gameMap==null)return center+direction*(reach+100f);
            var plane=new Plane(Vector3.up,TrafficSpace.Scene(center));
            var corners=new Vector2[4];
            for(int corner=0;corner<4;corner++)
            {
                var ray=_gameMap.ViewportPointToRay(new Vector3(corner==1||corner==2?1f:0f,corner>=2?1f:0f,0f));
                if(!plane.Raycast(ray,out float distance))return center+direction*(reach+100f);
                var world=TrafficSpace.World(ray.GetPoint(distance));corners[corner]=new Vector2(world.x,world.z);
            }
            reach=MapExitDistance(new Vector2(center.x,center.z),new Vector2(direction.x,direction.z),corners);
            return center+direction*(reach+100f);
        }
        internal static float MapExitDistance(Vector2 origin,Vector2 direction,Vector2[] corners)
        {
            float exit=0f;
            for(int i=0;i<corners.Length;i++)
            {
                var a=corners[i]-origin;var edge=corners[(i+1)%corners.Length]-corners[i];
                float cross=direction.x*edge.y-direction.y*edge.x;if(Mathf.Abs(cross)<.0001f)continue;
                float t=(a.x*edge.y-a.y*edge.x)/cross,u=(a.x*direction.y-a.y*direction.x)/cross;
                if(t>=0f&&u>=0f&&u<=1f)exit=Mathf.Max(exit,t);
            }
            return exit;
        }
        // WW: was OnGUI (Gui job)
        internal void Gui()
        {
            if(Event.current.type!=EventType.Repaint)return;
            if(Core.GameRefs.InGame&&Time.unscaledTime<WarRig.WorldSpawning.NaturalNoticeUntil)
            {
                if(_rigNoticeStyle==null)_rigNoticeStyle=new GUIStyle(GUI.skin.box){fontSize=26,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.yellow}};
                GUI.Box(new Rect((Screen.width-420f)*.5f,40f,420f,55f),"WAR RIG SPAWNED",_rigNoticeStyle);
            }
            var rigs=WarRig.WorldSpawning.NaturalRigs;
            for(int i=rigs.Count-1;i>=0;i--)if(rigs[i]==null)rigs.RemoveAt(i);
            var mapApi=Core.Services.Map;
            bool traffic=PatrolsModule.WorldTrafficEnabled.Value&&(mapApi!=null?mapApi.Routes!=Core.MapDetail.Off:PatrolsModule.TrafficDebugMap.Value);
            if((!traffic&&rigs.Count==0)||!Core.GameRefs.InGame)
            {_gameInkAt=0f;_mapWasOpen=false;return;}
            bool mapOpen=_mapCheat!=null&&_mapCheat.isActiveAndEnabled&&_mapCheat.ActiveStateName=="on";
            if((_gameMap==null||_gameMapImage==null||_mapCheat==null)&&(Time.unscaledTime>=_nextMapScan||(mapOpen&&!_mapWasOpen)))
            {
                _nextMapScan=Time.unscaledTime+5f;
                if(_gameMap==null){var camera=GameObject.Find("MapCheat_Camera");_gameMap=camera!=null?camera.GetComponent<Camera>():null;}
                if(_mapCheat==null)_mapCheat=Core.FsmUtil.Find(Core.GameRefs.GameManager,"MapCheat");
                if(_gameMapImage==null&&_mapCheat!=null)_gameMapImage=FindMapImage();
            }
            _mapWasOpen=_mapCheat!=null&&_mapCheat.isActiveAndEnabled&&_mapCheat.ActiveStateName=="on";
            if(!_mapWasOpen||_gameMap==null||_gameMapImage==null||!_gameMapImage.gameObject.activeInHierarchy)
            {_gameInkAt=0f;return;}
            _gameMapImage.GetWorldCorners(_mapCorners);
            var rect=new Rect(_mapCorners[0].x,Screen.height-_mapCorners[1].y,_mapCorners[2].x-_mapCorners[0].x,_mapCorners[1].y-_mapCorners[0].y);
            if(rect.width<1f||rect.height<1f)return;
            var old=GUI.color;GUI.BeginGroup(rect);
            if(traffic&&(Time.unscaledTime>=_gameInkAt||_gameInkSize!=rect.size||_inkNames!=MapNames||_inkHide!=MapUnknown))
            {
                _gameInkAt=Time.unscaledTime+1f;_gameInkSize=rect.size;
                _inkNames=MapNames;_inkHide=MapUnknown;
                var canvas=new Rect(0,0,Mathf.Min(512f,rect.width),Mathf.Min(512f,rect.height));
                _gameInk.Begin(Mathf.RoundToInt(canvas.width),Mathf.RoundToInt(canvas.height));
                foreach(var journey in _journeys)for(int i=1;i<journey.route.Length;i++)
                    _gameInk.Segment(MapPoint(journey.route[i-1],canvas),MapPoint(journey.route[i],canvas),RouteColor(journey));
                _gameInk.End();
                _mapLabels.Clear();_mapBattles.Clear();
                foreach(var journey in _journeys)
                CacheMapLabel(journey.position,rect,RouteColor(journey),MapName(journey));
                foreach(var camp in TrafficLocations.Known.Values)if(camp.cleared&&mapApi==null)
                CacheMapLabel(camp.center,rect,Color.gray,camp.name+" [Cleared]");
                foreach(var battle in _battles)
                {var p=MapPoint(battle.position,rect);_mapBattles.Add(new Rect(p.x-9f,p.y-9f,18f,18f));}
                foreach(var merchant in MerchantHost.All)if(merchant!=null&&!Owns(merchant.GetComponent<PatrolMarker>()))
                CacheMapLabel(TrafficSpace.World(merchant.transform.position),rect,_inkHide?Color.gray:Color.yellow,_inkHide?"Unknown":_inkNames?merchant.Data?.group??"Coyote merchant":"Merchant");
            }
            if(traffic)
            {
                GUI.color=Color.white;GUI.DrawTexture(new Rect(0,0,rect.width,rect.height),_gameInk.Texture);
                foreach(var label in _mapLabels)DrawMapLabel(label);
                GUI.color=Color.white;foreach(var battle in _mapBattles)GUI.DrawTexture(battle,Skull());
            }
            foreach(var rig in rigs)
            {
                if(mapApi!=null&&mapApi.Markers==Core.MapDetail.Off)continue;
                var p=_gameMap.WorldToViewportPoint(rig.transform.position);
                if(p.z<=0f||p.x<0f||p.x>1f||p.y<0f||p.y>1f)continue;
                if(_rigXStyle==null)_rigXStyle=new GUIStyle(GUI.skin.label){fontSize=24,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.yellow}};
                bool unknown=mapApi!=null&&mapApi.Markers==Core.MapDetail.Ambiguous;
                _rigXStyle.normal.textColor=unknown?Color.white:Color.yellow;
                GUI.color=Color.white;GUI.Label(new Rect(p.x*rect.width-12f,(1f-p.y)*rect.height-12f,24f,24f),unknown?"?":"X",_rigXStyle);
            }
            GUI.EndGroup();GUI.color=old;
        }
        private RectTransform FindMapImage()
        {
            // The native FSM holds MapCheatUI even while its hierarchy is inactive.
            foreach(var state in _mapCheat.FsmStates)foreach(var action in state.Actions)
            {
                var activate=action as HutongGames.PlayMaker.Actions.ActivateGameObject;if(activate==null)continue;
                var root=_mapCheat.Fsm.GetOwnerDefaultTarget(activate.gameObject);if(root==null||root.name!="MapCheatUI")continue;
                foreach(var image in root.GetComponentsInChildren<RectTransform>(true))if(image.name=="RawImage_Map")return image;
            }
            return null;
        }
        private void CacheMapLabel(Vector3 world,Rect rect,Color color,string text)
        {var p=MapPoint(world,rect);_mapLabels.Add(new MapLabel{rect=new Rect(p.x+7f,p.y-13f,260f,26f),color=color,text=text});}
        private void DrawMapLabel(MapLabel label)
        {
            if(_mapLabelStyle==null)_mapLabelStyle=new GUIStyle(GUI.skin.label){fontSize=15,wordWrap=false,normal={textColor=Color.white}};
            if(_mapDot==null)
            {
                _mapDot=new Texture2D(9,9,TextureFormat.RGBA32,false);var pixels=new Color32[81];
                for(int y=0;y<9;y++)for(int x=0;x<9;x++)if((x-4)*(x-4)+(y-4)*(y-4)<=16)pixels[y*9+x]=new Color32(255,255,255,255);
                _mapDot.SetPixels32(pixels);_mapDot.Apply(false,false);
            }
            var r=label.rect;GUI.color=Color.black;GUI.DrawTexture(new Rect(r.x-11.5f,r.y+8.5f,9f,9f),_mapDot);
            GUI.Label(new Rect(r.x-1f,r.y,r.width,r.height),label.text,_mapLabelStyle);GUI.Label(new Rect(r.x+1f,r.y,r.width,r.height),label.text,_mapLabelStyle);
            GUI.Label(new Rect(r.x,r.y-1f,r.width,r.height),label.text,_mapLabelStyle);GUI.Label(new Rect(r.x,r.y+1f,r.width,r.height),label.text,_mapLabelStyle);
            GUI.color=label.color;GUI.DrawTexture(new Rect(r.x-9.5f,r.y+10.5f,5f,5f),_mapDot);GUI.Label(r,label.text,_mapLabelStyle);
        }
        private Vector2 MapPoint(Vector3 world,Rect rect)
        {var p=_gameMap.WorldToViewportPoint(TrafficSpace.Scene(world));return new Vector2(p.x*rect.width,(1f-p.y)*rect.height);}
        internal static string MapName(TrafficJourney journey)
        {return MapUnknown?"Unknown":MapNames&&!string.IsNullOrEmpty(journey.name)?journey.name:journey.merchant?"Merchant":journey.convoy?"Convoy":"Patrol";}
        private static bool MapUnknown {get{return Core.Services.Map!=null?Core.Services.Map.Routes==Core.MapDetail.Ambiguous:PatrolsModule.HideRouteType.Value;}}
        private static bool MapNames {get{return Core.Services.Map!=null?Core.Services.Map.Routes==Core.MapDetail.On:PatrolsModule.RealPatrolNames.Value;}}
        internal static Color32 RouteColor(TrafficJourney journey)
        {
            if(MapUnknown)return new Color32(150,150,150,255);
            if(journey.merchant)return new Color32(255,220,50,255);
            if(TrafficFactions.Normalize(journey.faction)=="Coyotes")return journey.convoy?new Color32(110,210,255,255):new Color32(80,220,100,255);
            return journey.convoy?new Color32(255,150,40,255):new Color32(255,80,80,255);
        }
        private Texture2D Skull()
        {
            if(_skull!=null)return _skull;
            string[] rows={"00011111111000","00111111111100","01111111111110","11111111111111","11100111100111","11100111100111","11111100111111","01111100111110","00111111111100","00011011011000","00011011011000"};
            _skull=new Texture2D(14,11,TextureFormat.RGBA32,false);var pixels=new Color32[154];for(int y=0;y<11;y++)for(int x=0;x<14;x++)pixels[(10-y)*14+x]=rows[y][x]=='1'?new Color32(230,230,230,255):new Color32(0,0,0,0);
            _skull.SetPixels32(pixels);_skull.filterMode=FilterMode.Point;_skull.Apply();return _skull;
        }
        private void OnDestroy(){_gameInk.Dispose();if(_skull!=null)Destroy(_skull);if(_mapDot!=null)Destroy(_mapDot);}
    }
}
