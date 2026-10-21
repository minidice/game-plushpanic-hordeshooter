using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

public enum PetModelQuality { High, Low }

/// <summary>Visual-only quadruped rig. Mode AI and combat may drive this independently.</summary>
public partial class PetModel : Node3D
{
    public string Species { get; private set; } = "";
    public Node3D BackSocket { get; private set; } = null!;
    public Node3D? Weapon { get; private set; }
    public PetModelQuality Quality { get; private set; }
    private Node3D _model = null!;
    private float _poseTime, _poseMoving;
    private bool _followSettings;
    private Node3D _body = null!, _head = null!, _tail = null!;
    private readonly List<(Node3D Node, Transform3D BindPose, float Phase)> _legs = new();
    private Transform3D _bodyRest, _headRest, _tailRest;

    public static PetModel Create(string species, PetModelQuality? quality = null)
    {
        if (species is not ("turtle" or "dog" or "rabbit")) throw new ArgumentException("Unknown pet", nameof(species));
        var pet = new PetModel { Name = $"Pet_{species}", Species = species, _followSettings=quality==null };
        pet.SetQuality(quality ?? GameSettings.PetQuality);
        return pet;
    }

    public override void _EnterTree()
    {
        if (!_followSettings) return;
        SetQuality(GameSettings.PetQuality);
        GameSettings.PetQualityChanged += SetQuality;
    }
    public override void _ExitTree() { if(_followSettings) GameSettings.PetQualityChanged-=SetQuality; }

    /// <summary>Only geometry changes. The same weapon instance, pose and world transform survive.</summary>
    public void SetQuality(PetModelQuality quality)
    {
        if(!Enum.IsDefined(quality)) throw new ArgumentException("Unknown pet quality",nameof(quality));
        if(_model != null && Quality == quality) return;
        string suffix=quality == PetModelQuality.Low ? "_low" : "";
        var model=GD.Load<PackedScene>($"res://assets/pets/{Species}{suffix}.glb").Instantiate<Node3D>();
        Weapon?.GetParent()?.RemoveChild(Weapon);
        _model?.Free();
        _model=model; Quality=quality;AddChild(model);
        Node3D Part(string name) => model.FindChild(name,true,false) as Node3D
            ?? throw new InvalidOperationException($"{Species} missing {name}");
        _body=Part("Body");_head=Part("Head");_tail=Part("Tail");BackSocket=Part("BackSocket");
        _bodyRest=_body.Transform;_headRest=_head.Transform;_tailRest=_tail.Transform;
        _legs.Clear();
        foreach (var (name, phase) in new[] { ("FrontLeft",0f), ("FrontRight",Mathf.Pi), ("RearLeft",Mathf.Pi), ("RearRight",0f) })
        {
            var node = Part(name);
            float gaitPhase = Species == "rabbit" ? (name.StartsWith("Front") ? 0 : Mathf.Pi) : phase;
            _legs.Add((node,node.Transform,gaitPhase));
        }
        foreach(var node in model.FindChildren("*",nameof(MeshInstance3D),true,false))
            ((MeshInstance3D)node).LodBias=quality == PetModelQuality.High ? 4 : 1;
        if(Weapon != null) BackSocket.AddChild(Weapon);
        Pose(_poseTime,_poseMoving);
    }

    /// <summary>Null removes the complete module, including its mounting plate.</summary>
    public void SetWeapon(string? id)
    {
        if (id != null && id is not ("twin_blaster" or "rocket_pod")) throw new ArgumentException("Unknown pet weapon", nameof(id));
        var next = id == null ? null : GD.Load<PackedScene>($"res://assets/pets/{id}.glb").Instantiate<Node3D>();
        Weapon?.Free();
        Weapon = next;
        if (next != null) BackSocket.AddChild(next);
    }

    /// <summary>Absolute animation time and 0..1 motion blend; no locomotion or damage rules.</summary>
    public void Pose(float time, float moving)
    {
        moving = Mathf.Clamp(moving,0,1);
        _poseTime=time;_poseMoving=moving;
        float phase = time * (Species == "turtle" ? 5 : 9);
        _body.Transform = _bodyRest;
        _body.Position += Vector3.Up * (1 + Mathf.Sin(phase*2)) * .015f * moving;
        _head.Transform = _headRest;
        _head.RotateX(Mathf.Sin(phase)*.035f*moving);
        _tail.Transform = _tailRest;
        _tail.RotateY(Mathf.Sin(phase*1.4f) * (Species == "dog" ? .30f : .06f)*moving);
        foreach(var (node,rest,offset) in _legs)
        {
            float step = Mathf.Sin(phase+offset);
            node.Transform = rest;
            node.RotateX(step * (Species == "turtle" ? .13f : .30f)*moving);
            node.Position += Vector3.Up * Mathf.Max(0,step)*.05f*moving;
        }
    }
}
