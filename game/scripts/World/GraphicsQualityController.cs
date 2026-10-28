using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>Local rendering settings; never changes physics, weather, map lighting colours or network state.</summary>
public partial class GraphicsQualityController : Node3D
{
    private const int Capacity=256;
    private Stage _stage=null!;
    private Godot.Environment _environment=null!;
    private bool _authoredSsao;
    private bool _low;
    private readonly Dictionary<Light3D,bool> _lights=new();
    private MultiMeshInstance3D _blobs=null!;
    private readonly List<(Vector3 at,float radius,bool tree)> _scenery=new();
    private readonly Dictionary<Node3D,(Vector3 at,Vector3 ground,Vector3 normal)> _contacts=new();
    private double _contactTimer;
    private Enemy[] _nearEnemies=System.Array.Empty<Enemy>();

    public override void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;
        _stage=GetParent<Stage>();
        _environment=_stage.GetChildren().OfType<WorldEnvironment>().Single().Environment;
        _authoredSsao=_environment.SsaoEnabled;
        _blobs=new MultiMeshInstance3D{Name="SimpleShadows",CastShadow=GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride=new ShaderMaterial{Shader=GD.Load<Shader>("res://shaders/simple_shadow.gdshader")},
            Multimesh=new MultiMesh{TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,UseColors=true,
                Mesh=new PlaneMesh{Size=Vector2.One},InstanceCount=Capacity,VisibleInstanceCount=0}};
        AddChild(_blobs);
        if(_stage.Map is LayoutMap map)
            foreach(var holder in map.KitProps)
            {
                var asset=holder.GetChildren().OfType<Node3D>().FirstOrDefault(n=>n.SceneFilePath.EndsWith(".glb"));
                if(asset==null)continue;
                string id=System.IO.Path.GetFileNameWithoutExtension(asset.SceneFilePath);
                bool tree=id.Contains("pine")||id.Contains("broadleaf")||id.Contains("forest_oval")||id.Contains("forest_fan");
                if(!(tree||id.Contains("boulder")||id=="rock_cluster"))continue;
                var info=PropLibrary.Find(id);
                if(tree)
                {
                    // Use the placed crown, including model scale and rotation, not the trunk collider
                    // or a capped manifest size. Leave enough penumbra beyond the canopy to read from above.
                    var meshes=asset.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>().ToArray();
                    if(meshes.Length==0)continue;
                    var bounds=meshes[0].GlobalTransform*meshes[0].GetAabb();
                    foreach(var mesh in meshes.Skip(1))bounds=bounds.Merge(mesh.GlobalTransform*mesh.GetAabb());
                    var at=bounds.GetCenter();at.Y=holder.GlobalPosition.Y;
                    _scenery.Add((at,Mathf.Max(bounds.Size.X,bounds.Size.Z)*.8f,true));
                }
                else if(info!=null)_scenery.Add((holder.GlobalPosition,Mathf.Clamp(Mathf.Max(info.Dimensions.X,info.Dimensions.Z)*.38f,.45f,1.5f),false));
            }
        GameSettings.GraphicsQualityChanged+=Apply;
        GetTree().NodeAdded+=Added;
        GetTree().NodeRemoved+=Removed;
        Apply();
    }
    private void Added(Node node)
    {
        if(node is Light3D light)Callable.From(()=>
        {
            if(_low && IsInsideTree() && IsInstanceValid(light) && _stage.IsAncestorOf(light))RememberAndDisable(light);
        }).CallDeferred();
    }
    private void Removed(Node node)
    {
        if(node is Light3D light && _lights.Remove(light,out bool enabled) && IsInstanceValid(light))light.ShadowEnabled=enabled;
        if(node is Node3D actor)_contacts.Remove(actor);
    }
    private void RememberAndDisable(Light3D light)
    {
        if(!_lights.ContainsKey(light))_lights.Add(light,light.ShadowEnabled);
        light.ShadowEnabled=false;
    }
    private void Apply()
    {
        _low=GameSettings.ShadowQuality==GraphicsQualityLevel.Low;
        GameSettings.ApplySurfaceQuality();
        if(_low)
        {
            foreach(var light in _stage.FindChildren("*",nameof(Light3D),true,false).OfType<Light3D>())RememberAndDisable(light);
            _environment.SsaoEnabled=false;
        }
        else
        {
            foreach(var (light,enabled) in _lights)if(IsInstanceValid(light))light.ShadowEnabled=enabled;
            _lights.Clear();_environment.SsaoEnabled=_authoredSsao;
        }
        _blobs.Visible=_low;_contacts.Clear();_contactTimer=1;
    }
    public override void _PhysicsProcess(double delta)
    {
        if(!_low)return;
        _contactTimer+=delta;bool refresh=_contactTimer>=.10;
        if(refresh)
        {
            _contactTimer=0;
            foreach(var actor in _contacts.Keys.Where(n=>!IsInstanceValid(n)||n.IsQueuedForDeletion()).ToArray())_contacts.Remove(actor);
            var cameraAt=_stage.Cam.GlobalPosition;
            _nearEnemies=_stage.AllEnemies.Where(e=>IsInstanceValid(e)&&!e.IsDead&&e.GlobalPosition.DistanceSquaredTo(cameraAt)<55*55)
                .OrderBy(e=>e.GlobalPosition.DistanceSquaredTo(_stage.Player.GlobalPosition)).Take(192).ToArray();
        }
        int queries=0;
        int count=0;var eye=_stage.Cam.GlobalPosition;
        void Draw(Vector3 at,Vector3 normal,float radius,float opacity,float aspect=.825f,float core=.38f)
        {
            if(count>=Capacity)return;
            var x=Vector3.Right.Slide(normal).Normalized();if(x.LengthSquared()<.1f)x=Vector3.Forward.Slide(normal).Normalized();
            var basis=new Basis(x*radius*2,normal,x.Cross(normal)*radius*2*aspect);
            _blobs.Multimesh.SetInstanceTransform(count,new Transform3D(basis,at+normal*.022f));
            // Red carries the opaque core radius; alpha carries strength. Shared shader, one draw batch.
            _blobs.Multimesh.SetInstanceColor(count,new Color(core,1,1,opacity));count++;
        }
        void Actor(Node3D actor,float radius)
        {
            if(count>=192 || actor.GlobalPosition.DistanceSquaredTo(eye)>55*55)return;
            var at=actor.GlobalPosition;
            bool known=_contacts.TryGetValue(actor,out var contact);
            if(queries<32 && (!known || (refresh && (contact.normal==Vector3.Zero || contact.at.DistanceSquaredTo(at)>.01f))))
            {
                queries++;
                var hit=GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(at+Vector3.Up*.12f,at-Vector3.Up*6,Layers.World));
                if(hit.Count==0){_contacts[actor]=(at,Vector3.Zero,Vector3.Zero);return;}
                contact=(at,hit["position"].AsVector3(),hit["normal"].AsVector3());_contacts[actor]=contact;
            }
            if(contact.normal==Vector3.Zero)return;
            var ground=contact.ground;
            // Follow horizontal motion between physics contact samples on the sampled support plane.
            var shift=at-contact.at;ground.X+=shift.X;ground.Z+=shift.Z;
            if(contact.normal.Y>.2f)ground.Y-=(shift.X*contact.normal.X+shift.Z*contact.normal.Z)/contact.normal.Y;
            float height=Mathf.Max(0,at.Y-ground.Y);
            Draw(ground,contact.normal,radius*(1+Mathf.Min(height,2)*.12f),.25f*Mathf.Clamp(1-height/6,.15f,1));
        }
        foreach(var p in _stage.AlivePlayers)Actor(p,.43f);
        foreach(var enemy in _nearEnemies)if(IsInstanceValid(enemy)&&!enemy.IsDead)Actor(enemy,.42f);
        foreach(var (at,radius,tree) in _scenery)
            if(at.DistanceSquaredTo(eye)<45*45)Draw(at,Vector3.Up,radius,tree?.46f:.20f,tree?1:.825f,tree?.65f:.38f);
        _blobs.Multimesh.VisibleInstanceCount=count;
    }
    public override void _ExitTree()
    {
        GameSettings.GraphicsQualityChanged-=Apply;
        GetTree().NodeAdded-=Added;
        GetTree().NodeRemoved-=Removed;
        foreach(var (light,enabled) in _lights)if(IsInstanceValid(light))light.ShadowEnabled=enabled;
        _lights.Clear();_contacts.Clear();
        if(_environment!=null)_environment.SsaoEnabled=_authoredSsao;
    }
}
