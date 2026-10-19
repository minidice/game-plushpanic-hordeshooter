using Godot;
using System;
using System.Collections.Generic;

public partial class Integration : Node
{
    private Field _field=null!;
    private Goose _goose=null!;
    private int _tick;
    private Vector3 _start;
    private readonly List<string> _passed=new();
    private float _peak;
    public override void _Ready()
    {
        try
        {
            _field=GD.Load<PackedScene>("res://scenes/Field.tscn").Instantiate<Field>();
            AddChild(_field);_goose=_field.Player;_goose.ManualControl=true;
            Check(ProjectSettings.GetSetting("physics/3d/physics_engine").AsString()=="Jolt Physics","Jolt configured");
            Check(_goose.Visual.FindChild("Muzzle",true,false)!=null,"GLB muzzle loaded");
            Check(Goose.MoveVector(new Vector2(1,1)).Length()<=1.001f,"diagonal movement normalized");
        }
        catch(Exception e){Fail(e);}
    }
    private void Check(bool ok,string name){if(!ok)throw new Exception(name);_passed.Add(name);GD.Print("PASS: "+name);}
    public override void _PhysicsProcess(double delta)
    {
        try
        {
            _tick++;
            if(_tick==30){_start=_goose.Position;_goose.SetControls(Vector2.Right,new Vector3(8,0,0),false);}
            if(_tick==150)
            {
                Check(_goose.Position.X>_start.X+.5f && _goose.Position.X<2.3f,"moves and stops at cover wall");
                _goose.SetControls(Vector2.Zero,Vector3.Zero,false);_goose.Position=Vector3.Zero;_goose.Velocity=Vector3.Zero;
            }
            if(_tick==180)
            {
                _goose.SetControls(Vector2.Zero,new Vector3(6,0,0),false);
                Check(_goose.FireAt(new Vector3(6,0,0)),"weapon fires");
                Check(_field.CoverTarget.Health==3,"cover blocks target damage");
                Check(!_goose.FireAt(new Vector3(6,0,0)),"fire cooldown enforced");
            }
            if(_tick==210)
            {
                _goose.FireAt(_field.OpenTarget.Position);
                Check(_field.OpenTarget.Health==2,"unobstructed ray damages target");
                _goose.TakeDamage(25,Vector3.Right);
                Check(_goose.Health==75&&!_goose.IsDead,"nonlethal hit keeps control");
            }
            if(_tick==250)
            {
                _goose.TakeDamage(100,Vector3.Right);
                Check(_goose.IsDead&&_goose.Health==0&&_goose.Corpse!=null,"lethal hit creates rigid corpse");
                Check(_goose.CollisionLayer==0,"living collider disabled on death");
                Check(!_goose.FireAt(Vector3.Zero),"dead goose cannot fire");
                _start=_goose.Position;_goose.SetControls(Vector2.One,Vector3.Zero,true);
            }
            if(_tick>250&&_tick<600)
            {
                var corpse=_goose.Corpse!;
                _peak=Math.Max(_peak,corpse.Position.Y);
                if(!corpse.Position.IsFinite()||corpse.Position.Y<-.5f)throw new Exception("corpse invalid or below ground");
            }
            if(_tick==600)
            {
                Check(_peak>1.8f,"death impulse launches capsule");
                Check(_goose.Position.DistanceTo(_start)<.01f,"dead movement blocked");
                Check(_goose.Corpse!.LinearVelocity.Length()<.6f,"corpse settles on world");
                _goose.Respawn(Vector3.Zero);_goose.SetControls(Vector2.Zero,Vector3.Zero,false);
                Check(!_goose.IsDead&&_goose.Health==100&&_goose.Corpse==null,"respawn restores control and removes corpse");
            }
            if(_tick==630)
            {
                Check(_goose.Visual.GetParent()==_goose,"visual reattached after respawn");
                var report=System.Text.Json.JsonSerializer.Serialize(new{passed=_passed.Count,checks=_passed,engine=Engine.GetVersionInfo()["string"].AsString(),peak_height=_peak});
                System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://output"));
                System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://output/integration-report.json"),report);
                GD.Print("GOOSE_INTEGRATION_PASS "+report);GetTree().Quit();
            }
        }
        catch(Exception e){Fail(e);}
    }
    private void Fail(Exception e){GD.PushError("GOOSE_INTEGRATION_FAIL "+e);SetPhysicsProcess(false);GetTree().Quit(1);}
}
