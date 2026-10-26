using Godot;
namespace HordeShooter;

/// <summary>Rigid authored paw and two closed Blender arm sections. Shared meshes remain immutable.</summary>
internal partial class CharacterLionPaw : MeshInstance3D
{
    private Transform3D _rest, _forearmRest;
    private Vector3 _shoulder, _wrist, _grip;
    private MeshInstance3D _forearm=null!;
    private MeshInstance3D _upperArm=null!;
    private MeshInstance3D _restArm=null!;
    private Node3D _elbowMarker=null!;
    private Node3D _wristMarker=null!;
    private Vector3 _meshGrip;
    private float _side;
    private float _radius;
    private float _armLength;
    public Vector3 PosedShoulder { get; private set; }
    public float ArmLength => _armLength;
    public static CharacterLionPaw Create(Node3D parent,MeshInstance3D paw,Transform3D rest,MeshInstance3D forearm,Transform3D forearmRest,Vector3 shoulder,Vector3 wrist,Vector3 grip,string name,float side)
    {
        var result=new CharacterLionPaw{Name=name,Mesh=paw.Mesh,_rest=rest,_forearmRest=forearmRest,_shoulder=shoulder,_wrist=wrist,_grip=grip,_side=side,Transform=rest};
        CopyMaterials(paw,result);parent.AddChild(result);
        var template=GD.Load<PackedScene>("res://assets/characters/shared/mammal-arm-segments.glb").Instantiate<Node3D>();
        result._upperArm=Section("UpperArm");result._forearm=Section("Forearm");
        MeshInstance3D Section(string section) {
            var mesh=new MeshInstance3D{Name=section,Mesh=((MeshInstance3D)template.FindChild(section,true,false)).Mesh};
            mesh.SetSurfaceOverrideMaterial(0,forearm.GetActiveMaterial(0));result.AddChild(mesh);return mesh;
        }
        template.Free();
        result._restArm=new MeshInstance3D{Name="RestArm",Mesh=forearm.Mesh};CopyMaterials(forearm,result._restArm);result.AddChild(result._restArm);
        result._radius=Mathf.Clamp((forearmRest*forearm.Mesh.GetAabb()).Size.X*.5f,.065f,.12f);
        result._armLength=shoulder.DistanceTo(wrist)*1.10f;
        result.PosedShoulder=shoulder;
        result._elbowMarker=new Node3D{Name="Elbow"};result.AddChild(result._elbowMarker);
        result._wristMarker=new Node3D{Name="Wrist",Position=rest.AffineInverse()*wrist};result.AddChild(result._wristMarker);
        result._meshGrip=rest.AffineInverse()*grip;
        result.PosePaw(Vector3.Zero,false,1,0);return result;
    }
    private static void CopyMaterials(MeshInstance3D source,MeshInstance3D target)
    {for(int i=0;i<source.Mesh.GetSurfaceCount();i++)target.SetSurfaceOverrideMaterial(i,source.GetActiveMaterial(i));}
    public void PosePaw(Vector3 target,bool holding,float size,float running,bool compact=false)
    {
        var shoulder=compact&&holding?CompactShoulder(target,size):_shoulder;
        PosedShoulder=shoulder;
        var axis=(_grip-_wrist).Normalized();
        Basis pose;
        Vector3 elbow;
        if(holding && target.DistanceSquaredTo(shoulder)>.000001f) {
            var reach=target-shoulder;float distance=reach.Length();var direction=reach/distance;
            float hand=(_grip-_wrist).Length()*size;
            float upper=_armLength*.52f*size,lower=_armLength*.48f*size+hand;
            // Dedicated traversal/reload can still reach extreme targets. Standard firearm poses
            // preserve authored proportions and must fit the weapon to these bounded limbs.
            if(compact){distance=Mathf.Min(distance,(upper+lower)*.96f);target=shoulder+direction*distance;}
            else {float extension=Mathf.Max(1,distance/((upper+lower)*.98f));upper*=extension;lower*=extension;}
            float along=Mathf.Clamp((distance*distance+upper*upper-lower*lower)/(2*distance),-upper,upper);
            var pole=new Vector3(_side*.8f,-1,-.1f);
            pole-=direction*pole.Dot(direction);
            if(pole.LengthSquared()<.0001f)pole=direction.Cross(Vector3.Forward);
            elbow=shoulder+direction*along+pole.Normalized()*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            pose=new Basis(new Quaternion(axis,(target-elbow).Normalized())).Scaled(Vector3.One*size);
        }
        else {
            pose=new Basis(Vector3.Back,-_side*running*.35f).Scaled(Vector3.One*size);
            elbow=_shoulder+pose*((_wrist-_shoulder)*.5f)+new Vector3(_side*.012f,0,-.018f)*size;
        }
        var offset=holding?target-pose*_grip:_shoulder-pose*_shoulder;
        var pawPose=new Transform3D(pose,offset);
        Transform=pawPose*_rest;
        _upperArm.Visible=_forearm.Visible=holding;_restArm.Visible=!holding;
        _restArm.Transform=Transform.AffineInverse()*pawPose*_forearmRest;
        var wrist=pawPose*_wrist;
        _elbowMarker.Position=Transform.AffineInverse()*elbow;
        var upperDirection=(elbow-shoulder).Normalized();var lowerDirection=(wrist-elbow).Normalized();
        Place(_upperArm,shoulder-upperDirection*.04f*size,elbow+upperDirection*.025f*size,size);
        Place(_forearm,elbow-lowerDirection*.025f*size,wrist+lowerDirection*.012f*size,size);
    }
    // Roll the shoulder around the torso surface toward the support position, without translating
    // the arm root away from the body. Use the smallest protraction that leaves a bent elbow.
    private Vector3 CompactShoulder(Vector3 target,float size)
    {
        float radius=new Vector2(_shoulder.X,_shoulder.Z).Length();
        float start=Mathf.Atan2(_shoulder.Z,Mathf.Abs(_shoulder.X));
        float end=Mathf.Min(start+Mathf.DegToRad(55),Mathf.DegToRad(82));
        float desired=Mathf.Clamp(Mathf.Atan2(target.Z,_side*target.X),start,end);
        float reach=(_armLength+(_grip-_wrist).Length())*size*.92f;
        Vector3 At(float angle)=>new(_side*radius*Mathf.Cos(angle),_shoulder.Y,radius*Mathf.Sin(angle));
        if(_shoulder.DistanceTo(target)<=reach)return _shoulder;
        float low=start,high=desired;
        for(int i=0;i<12;i++){float mid=(low+high)*.5f;if(At(mid).DistanceTo(target)>reach)low=mid;else high=mid;}
        return At(high);
    }
    private void Place(MeshInstance3D section,Vector3 from,Vector3 to,float size) {
        var delta=to-from;float length=Mathf.Max(delta.Length(),.0001f);
        var rotation=delta.LengthSquared()>.00000001f?new Basis(new Quaternion(Vector3.Up,delta/length)):Basis.Identity;
        var basis=rotation*Basis.FromScale(new Vector3(_radius*size,length,_radius*size));
        section.Transform=Transform.AffineInverse()*new Transform3D(basis,from);
    }
    public Vector3 Grip=>GlobalTransform*_meshGrip;
}
