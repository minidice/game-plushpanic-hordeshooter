using Godot;
using System.Linq;

namespace HordeShooter;

public partial class ForestStudyLight : Node
{
    public Stage Stage { get; set; } = null!;
    public bool Expanded { get; init; }
    public float Weight { get; private set; }
    private DirectionalLight3D _sun=null!;
    private Godot.Environment _environment=null!;
    private Color _sunColor,_ambientColor;
    private Vector3 _sunRotation;
    private float _sunEnergy,_ambientEnergy;
    public override void _Ready()
    {
        _sun=Stage.GetChildren().OfType<DirectionalLight3D>().Single();
        _environment=Stage.GetChildren().OfType<WorldEnvironment>().Single().Environment;
        _sunColor=_sun.LightColor;_ambientColor=_environment.AmbientLightColor;
        _sunRotation=_sun.RotationDegrees;
        _sunEnergy=_sun.LightEnergy;_ambientEnergy=_environment.AmbientLightEnergy;
    }
    public override void _Process(double delta)
    {
        if(Stage.Cam?.Target is not Node3D target)return;
        var point=new Vector2(target.GlobalPosition.X,target.GlobalPosition.Z);
        Weight=Mathf.MoveToward(Weight,LayoutMap.ForestWeight(point,Expanded),(float)delta*1.5f);
        _sun.LightColor=_sunColor.Lerp(new Color("fff2c7"),Weight);
        _sun.RotationDegrees=_sunRotation.Lerp(new Vector3(-42,-105,0),Weight);
        _sun.LightEnergy=Mathf.Lerp(_sunEnergy,1.35f,Weight);
        _environment.AmbientLightColor=_ambientColor.Lerp(new Color("bcc6a2"),Weight);
        _environment.AmbientLightEnergy=Mathf.Lerp(_ambientEnergy,.38f,Weight);
    }
    public override void _ExitTree()
    {
        if(IsInstanceValid(_sun)){_sun.LightColor=_sunColor;_sun.LightEnergy=_sunEnergy;_sun.RotationDegrees=_sunRotation;}
        if(_environment!=null){_environment.AmbientLightColor=_ambientColor;_environment.AmbientLightEnergy=_ambientEnergy;}
    }
}
