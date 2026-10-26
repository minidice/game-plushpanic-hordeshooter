using Godot;
using System.Collections.Generic;

namespace HordeShooter;

public partial class CharacterRig
{
    private Node3D? _tailMotion;
    private string _tailSpecies="";
    private Transform3D _tailMotionHome;
    private float _tailClock, _tailMove;
    private MeshInstance3D? _tailMesh;
    private int _tailStyleBuilt;
    private Mesh? _defaultTailMesh;
    private Transform3D _defaultTailTransform;
    private CharacterTailSkin? _tailSkin;

    private void PrepareTail()
    {
        // Zombie definitions use composite IDs; the imported model retains its species node names.
        Node3D? tail=null;
        foreach(var species in new[]{"goose","duck","raccoon","redpanda"})
            if(Model.FindChild(species+"_tail",true,false) is Node3D found)
            {tail=found;_tailSpecies=species;break;}
        if(tail==null)return;
        // GLBs already embed this pivot in the rump (.55/.60 high, -.30 rearward).
        // Keep that authored attachment; resize and rotate the mesh around it, never the model origin.
        var parent=tail.GetParent();
        _tailMotion=new Node3D{Name="TailMotion",Transform=tail.Transform};
        // Imported descendants belong to the model scene. Suspend ownership while the
        // subtree is detached, then restore it once every owner is an ancestor again.
        var owners=new List<(Node Part,Node? Owner)>{(tail,tail.Owner)};
        foreach(var child in tail.FindChildren("*","",true,false))owners.Add((child,child.Owner));
        foreach(var (part,_) in owners)part.Owner=null;
        parent.RemoveChild(tail);parent.AddChild(_tailMotion);_tailMotion.AddChild(tail);
        _tailMotion.Owner=owners[0].Owner;
        foreach(var (part,owner) in owners)part.Owner=owner;
        tail.Transform=Transform3D.Identity;
        if(tail.FindChild(_tailSpecies+"_tail_mesh",true,false) is MeshInstance3D mesh && mesh.Mesh!=null)
        {
            // Canonical default also replaces legacy mammal variants, retaining their palette.
            var materials=new Material[mesh.Mesh.GetSurfaceCount()];
            for(int i=0;i<materials.Length;i++)materials[i]=mesh.GetActiveMaterial(i);
            mesh.Mesh=CharacterTailMesh.Build(_tailSpecies,materials);
            mesh.Transform=Transform3D.Identity;
            _defaultTailMesh=mesh.Mesh;_defaultTailTransform=mesh.Transform;
            _tailMesh=mesh;
            if(!AppearanceCatalog.IsBird(_tailSpecies))
            {_tailSkin=new CharacterTailSkin(mesh);_tailSkin.Configure(_tailSpecies,0);}
        }
        _tailMotionHome=_tailMotion.Transform;
    }

    /// <summary>Rebuilds the tail surface for a workshop style; per-character material overrides carry over.</summary>
    private void ApplyTailStyle(int style)
    {
        if(_tailMesh?.Mesh==null||style==_tailStyleBuilt)return;
        var materials=new Material[_tailMesh.Mesh.GetSurfaceCount()];
        var overrides=new Material?[materials.Length];
        for(int i=0;i<materials.Length;i++){materials[i]=_tailMesh.Mesh.SurfaceGetMaterial(i);overrides[i]=_tailMesh.GetSurfaceOverrideMaterial(i);}
        _tailMesh.Mesh=style==0?_defaultTailMesh:CharacterTailMesh.Build(_tailSpecies,materials,style);
        _tailMesh.Transform=style==0?_defaultTailTransform:Transform3D.Identity;
        for(int i=0;i<overrides.Length;i++)_tailMesh.SetSurfaceOverrideMaterial(i,overrides[i]);
        _tailStyleBuilt=style;
        _tailSkin?.Configure(_tailSpecies,style);
        // Configure clears only skin history. Rigid tail phase/smoothing survive live style changes.
    }

    private void UpdateTail(float dt,float movement)
    {
        if(_tailMotion==null)return;
        _tailClock+=Mathf.Max(0,dt);
        _tailMove=Mathf.Lerp(_tailMove,Mathf.Clamp(movement,0,1),1-Mathf.Exp(-Mathf.Max(0,dt)*8));
        bool bird=AppearanceCatalog.IsBird(_tailSpecies);
        bool skinned=_tailSkin!=null&&_tailStyleBuilt!=3;
        if(skinned)
        {
            _tailSkin!.Step(dt,Mathf.Clamp(movement,0,1),_clock,Rotation.Y,GlobalPosition);
            if(dt<=0){_tailMotion.Transform=_tailMotionHome;_tailClock=_tailMove=0;return;}
        }
        float yaw=Mathf.Sin(_tailClock*1.9f)*.018f+Mathf.Sin(_clock-.9f)*_tailMove*(bird?.035f:.075f);
        float pitch=Mathf.Cos(_clock-.9f)*_tailMove*(bird?.018f:.03f);
        if(skinned){yaw=Mathf.Sin(_clock-.9f)*_tailMove*.015f;pitch*=.35f;}
        _tailMotion.Transform=_tailMotionHome;
        _tailMotion.RotateObjectLocal(Vector3.Up,yaw);
        _tailMotion.RotateObjectLocal(Vector3.Right,pitch);
    }

    private void ResetTail()
    {
        if(_tailMotion!=null)_tailMotion.Transform=_tailMotionHome;
        _tailClock=_tailMove=0;
        _tailSkin?.Reset();
    }
}
