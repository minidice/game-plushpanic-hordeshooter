using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>Visual-only drone; equipment and flight motion do not implement combat or following.</summary>
public partial class DroneModel : Node3D
{
    public static readonly IReadOnlyList<string> BodyIds=Array.AsReadOnly(new[]{"quadcopter","ladybug","bee","ufo"});
    public static readonly IReadOnlyList<string> EquipmentIds=Array.AsReadOnly(new[]{"light","laser","blaster","support"});
    public string BodyId {get;private set;}="";
    public PetModelQuality Quality {get;private set;}
    public Node3D EquipmentSocket {get;private set;}=null!;
    public Node3D? Equipment {get;private set;}
    private Node3D? _model;
    private Node3D _body=null!;
    private readonly List<(Node3D Node,Transform3D Bind)> _parts=new();
    private bool _followSettings;
    private float _time,_moving;

    public static DroneModel Create(string id,PetModelQuality? quality=null)
    {
        if(!BodyIds.Contains(id))throw new ArgumentException("Unknown drone",nameof(id));
        if(quality.HasValue && !Enum.IsDefined(quality.Value))throw new ArgumentException("Unknown quality",nameof(quality));
        var drone=new DroneModel{Name="Drone_"+id,BodyId=id,_followSettings=quality==null};
        drone.SetQuality(quality??GameSettings.PetQuality);return drone;
    }
    public override void _EnterTree()
    {
        if(!_followSettings)return;
        SetQuality(GameSettings.PetQuality);GameSettings.PetQualityChanged+=SetQuality;
    }
    public override void _ExitTree(){if(_followSettings)GameSettings.PetQualityChanged-=SetQuality;}

    public void SetQuality(PetModelQuality quality)
    {
        if(!Enum.IsDefined(quality))throw new ArgumentException("Unknown quality",nameof(quality));
        if(_model!=null && Quality==quality)return;
        var suffix=quality==PetModelQuality.Low?"_low":"";
        var next=GD.Load<PackedScene>(AssetPaths.Pattern("drone_model", BodyId + suffix)).Instantiate<Node3D>();
        Equipment?.GetParent()?.RemoveChild(Equipment);_model?.Free();
        _model=next;AddChild(next);Quality=quality;
        _body=(Node3D)next.FindChild("Body",true,false);
        EquipmentSocket=(Node3D)next.FindChild("EquipmentSocket",true,false);
        _parts.Clear();
        foreach(var node in next.FindChildren("*","Node3D",true,false).Cast<Node3D>())
            if(node.Name.ToString().StartsWith("Rotor") && node is not MeshInstance3D || node.Name=="WingLeft" || node.Name=="WingRight" || node.Name=="RimSpin")
                _parts.Add((node,node.Transform));
        foreach(var node in next.FindChildren("*",nameof(MeshInstance3D),true,false))
            ((MeshInstance3D)node).LodBias=quality==PetModelQuality.High?4:1;
        if(Equipment!=null)EquipmentSocket.AddChild(Equipment);
        Pose(_time,_moving);
    }
    public void SetEquipment(string? id)
    {
        if(id!=null && !EquipmentIds.Contains(id))throw new ArgumentException("Unknown drone equipment",nameof(id));
        var next=id==null?null:GD.Load<PackedScene>(AssetPaths.Pattern("drone_model", id)).Instantiate<Node3D>();
        Equipment?.Free();Equipment=next;
        if(next!=null)EquipmentSocket.AddChild(next);
    }
    public void Pose(float time,float moving)
    {
        _time=time;_moving=Mathf.Clamp(moving,0,1);
        _body.Position=new Vector3(0,Mathf.Sin(time*2.8f)*.045f,0);
        _body.Rotation=new Vector3(_moving*Mathf.Sin(time*2)*.09f,0,Mathf.Sin(time*1.7f)*.025f);
        foreach(var (node,bind) in _parts)
        {
            node.Transform=bind;
            string name=node.Name;
            if(name.StartsWith("Rotor"))node.RotateY(time*18);
            else if(name=="RimSpin")node.RotateY(time*.8f);
            else node.RotateZ(Mathf.Sin(time*30)*.55f*(name=="WingLeft"?-1:1));
        }
    }
}
