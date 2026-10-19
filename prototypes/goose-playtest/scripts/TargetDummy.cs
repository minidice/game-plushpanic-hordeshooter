using Godot;

public partial class TargetDummy : StaticBody3D
{
    public int Health {get;private set;}=3;
    private MeshInstance3D _plate=null!;
    private float _reset,_flash;
    private readonly StandardMaterial3D _paint=new(){AlbedoColor=new Color("c57248"),Roughness=.85f};
    public override void _Ready()
    {
        CollisionLayer=4;CollisionMask=0;
        AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=new Vector3(.85f,1.8f,.45f)},Position=new Vector3(0,.9f,0)});
        _plate=Field.Box(this,new Vector3(0,1,0),new Vector3(.85f,1.5f,.25f),_paint);
        Field.Box(this,new Vector3(0,.12f,0),new Vector3(1.1f,.24f,.9f),Field.Material("313e3e"));
        var ring=new MeshInstance3D{Mesh=new TorusMesh{InnerRadius=.19f,OuterRadius=.24f},Position=new Vector3(0,1.05f,.15f),RotationDegrees=new Vector3(90,0,0),MaterialOverride=Field.Material("e8d6ac")};AddChild(ring);
    }
    public void Hit(){if(Health<=0)return;Health--;_flash=.12f;if(Health==0)_reset=2.5f;}
    public override void _Process(double delta)
    {
        _flash=Mathf.Max(0,_flash-(float)delta);
        _paint.AlbedoColor=_flash>0?new Color("fff1b5"):Health>0?new Color("c57248"):new Color("414a47");
        _plate.RotationDegrees=new Vector3(Health==0?-55:0,0,0);
        if(_reset>0){_reset-=(float)delta;if(_reset<=0)Health=3;}
    }
}
