using Godot;

namespace HordeShooter;

/// <summary>Small tail-only GPU rig. Cascaded bounded filters delay the tip without simulation or mesh rebuilding.</summary>
internal sealed class CharacterTailSkin
{
    private readonly Skeleton3D _skeleton;
    private readonly MeshInstance3D _mesh;
    private readonly Vector2[] _bend=new Vector2[3];
    private bool _history;
    private float _heading;
    private Vector3 _position;
    public CharacterTailSkin(MeshInstance3D mesh)
    {
        _mesh=mesh;
        _skeleton=new Skeleton3D{Name="TailSkeleton"};
        mesh.GetParent().AddChild(_skeleton);
        for(int i=0;i<4;i++)
        {
            _skeleton.AddBone("tail_"+i);
            if(i>0)_skeleton.SetBoneParent(i,i-1);
        }
    }
    public void Configure(string species,int style)
    {
        Reset();
        if(style==3){_mesh.Skin=null;_mesh.Skeleton=new NodePath("");return;}
        var shape=CharacterTailMesh.For(style);
        var skin=new Skin();
        var previous=Vector3.Zero;
        for(int i=0;i<4;i++)
        {
            var at=CharacterTailMesh.Center(species,shape,i*.25f);
            _skeleton.SetBoneRest(i,new Transform3D(Basis.Identity,at-previous));
            skin.AddBind(i,new Transform3D(Basis.Identity,-at));
            previous=at;
        }
        _skeleton.ResetBonePoses();
        _mesh.Skin=skin;_mesh.Skeleton=_mesh.GetPathTo(_skeleton);
        // Rest AABB plus bounded bend allowance keeps the skinned tip/shadow in view.
        _mesh.CustomAabb=_mesh.Mesh.GetAabb().Grow(.25f);
    }
    public void Step(float dt,float movement,float clock,float heading,Vector3 position)
    {
        if(dt<=0||!float.IsFinite(dt)||dt>.2f||(_history&&position.DistanceSquaredTo(_position)>9))
        {Reset();_heading=heading;_position=position;_history=true;return;}
        float turn=_history?Mathf.AngleDifference(_heading,heading)/dt:0;
        _heading=heading;_position=position;_history=true;
        var target=new Vector2(Mathf.Cos(clock-.9f)*movement*.012f,
            Mathf.Clamp(-turn*.025f,-.11f,.11f)+Mathf.Sin(clock-.9f)*movement*.025f);
        // Update from tip toward root so each region consumes the previous frame's predecessor.
        for(int i=2;i>=0;i--)
        {
            var wanted=i==0?target:_bend[i-1];
            _bend[i]=_bend[i].Lerp(wanted,1-Mathf.Exp(-dt*(22-i*3.5f)));
            _skeleton.SetBonePoseRotation(i+1,Quaternion.FromEuler(new Vector3(_bend[i].X,_bend[i].Y,0)));
        }
    }
    public void Reset()
    {
        _history=false;
        for(int i=0;i<3;i++)_bend[i]=Vector2.Zero;
        _skeleton.ResetBonePoses();
    }
}
