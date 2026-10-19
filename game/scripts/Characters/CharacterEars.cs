using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Mammal ears: rebuilt cupped shells (CharacterEarMesh), a style per profile, size and outward tilt.</summary>
public partial class CharacterRig
{
    private readonly List<(Node3D Pivot,MeshInstance3D Mesh,float Side)> _ears=new();
    private string _earSpecies="";
    private readonly Dictionary<Node3D,Transform3D> _earHomes=new();
    private int _earStyleBuilt;

    private void PrepareEars()
    {
        foreach(var species in new[]{"raccoon","redpanda"})
        {
            foreach(var (suffix,side) in new[]{("leftEar",-1f),("rightEar",1f)})
                if(Model.FindChild(species+"_"+suffix,true,false) is Node3D pivot
                   && pivot.FindChild(species+"_"+suffix+"_mesh",true,false) is MeshInstance3D { Mesh: not null } mesh)
                {
                    var materials=new Material[mesh.Mesh.GetSurfaceCount()];
                    for(int i=0;i<materials.Length;i++)materials[i]=mesh.GetActiveMaterial(i);
                    mesh.Mesh=CharacterEarMesh.Get(species,0,materials);
                    _earSpecies=species;
                    mesh.Transform=Transform3D.Identity;
                    _ears.Add((pivot,mesh,side));
                    _earHomes[pivot]=pivot.Transform;
                }
            if(_ears.Count>0)return;
        }
    }

    /// <summary>Restores each ear, applies style, tilt and size; a hat folds them flat to the sides.</summary>
    private void ApplyEars(AppearanceProfile p,bool hatOn)
    {
        if(p.EarStyle!=_earStyleBuilt)
        {
            foreach(var (_,mesh,_) in _ears)
            {
                var materials=new Material[mesh.Mesh.GetSurfaceCount()];
                var overrides=new Material?[materials.Length];
                for(int i=0;i<materials.Length;i++){materials[i]=mesh.Mesh.SurfaceGetMaterial(i);overrides[i]=mesh.GetSurfaceOverrideMaterial(i);}
                mesh.Mesh=CharacterEarMesh.Get(_earSpecies,p.EarStyle,materials);
                for(int i=0;i<overrides.Length;i++)mesh.SetSurfaceOverrideMaterial(i,overrides[i]);
            }
            _earStyleBuilt=p.EarStyle;
        }
        foreach(var (ear,_,side) in _ears)
        {
            ear.Transform=_earHomes[ear];
            if(hatOn){ear.Position=new Vector3(side*.43f,.16f,-.06f);ear.Rotation=new Vector3(0,0,-side*.55f);ear.Scale=new Vector3(.72f,.60f,.80f);}
            else ear.Rotation+=new Vector3(0,0,-side*Mathf.DegToRad(p.EarAngle));
            ear.Scale*=p.EarSize;
        }
    }

    public int EarStyleBuilt=>_earStyleBuilt;
    public int TailStyleBuilt=>_tailStyleBuilt;
    public IEnumerable<Node3D> EarPivots{get{foreach(var e in _ears)yield return e.Pivot;}}
}
