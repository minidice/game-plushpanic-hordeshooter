using Godot;
namespace HordeShooter;

/// <summary>Rigid authored paw with a separate shoulder-to-wrist forearm. Mesh resources remain immutable.</summary>
internal partial class CharacterLionPaw : MeshInstance3D
{
    private Transform3D _rest, _forearmRest;
    private Vector3 _shoulder, _wrist, _grip;
    private MeshInstance3D _forearm=null!;
    private Node3D _wristMarker=null!;
    private Vector3 _meshGrip;
    private float _side;
    public static CharacterLionPaw Create(Node3D parent,MeshInstance3D paw,Transform3D rest,MeshInstance3D forearm,Transform3D forearmRest,Vector3 shoulder,Vector3 wrist,Vector3 grip,string name,float side)
    {
        var result=new CharacterLionPaw{Name=name,Mesh=paw.Mesh,_rest=rest,_forearmRest=forearmRest,_shoulder=shoulder,_wrist=wrist,_grip=grip,_side=side,Transform=rest};
        CopyMaterials(paw,result);parent.AddChild(result);
        result._forearm=new MeshInstance3D{Name="Forearm",Mesh=forearm.Mesh};CopyMaterials(forearm,result._forearm);result.AddChild(result._forearm);
        result._wristMarker=new Node3D{Name="Wrist",Position=rest.AffineInverse()*wrist};result.AddChild(result._wristMarker);
        result._meshGrip=rest.AffineInverse()*grip;
        result.PosePaw(Vector3.Zero,false,1,0);return result;
    }
    private static void CopyMaterials(MeshInstance3D source,MeshInstance3D target)
    {for(int i=0;i<source.Mesh.GetSurfaceCount();i++)target.SetSurfaceOverrideMaterial(i,source.GetActiveMaterial(i));}
    public void PosePaw(Vector3 target,bool holding,float size,float running)
    {
        var axis=(_grip-_wrist).Normalized();
        Basis pose;
        if(holding && target.DistanceSquaredTo(_shoulder)>.000001f)
            pose=new Basis(new Quaternion(axis,(target-_shoulder).Normalized())).Scaled(Vector3.One*size);
        else pose=new Basis(Vector3.Back,-_side*running*.35f).Scaled(Vector3.One*size);
        var offset=holding?target-pose*_grip:_shoulder-pose*_shoulder;
        var pawPose=new Transform3D(pose,offset);
        Transform=pawPose*_rest;
        var wrist=pawPose*_wrist;
        var restDirection=_wrist-_shoulder;
        float restLength=restDirection.Length();
        var direction=wrist-_shoulder;
        var forearmAxis=restDirection/restLength;
        float stretch=direction.Length()/restLength-size;
        var axial=new Basis(Vector3.Right*size+forearmAxis*(forearmAxis.X*stretch),Vector3.Up*size+forearmAxis*(forearmAxis.Y*stretch),Vector3.Back*size+forearmAxis*(forearmAxis.Z*stretch));
        var forearmPose=new Basis(new Quaternion(forearmAxis,direction.Normalized()))*axial;
        var desired=new Transform3D(forearmPose,_shoulder-forearmPose*_shoulder)*_forearmRest;
        _forearm.Transform=Transform.AffineInverse()*desired;
    }
    public Vector3 Grip=>GlobalTransform*_meshGrip;
}
