using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Scripted play-space review through actual Player movement and the tracking camera.</summary>
public partial class WorkshopWalkthrough : Node
{
    private Stage _stage = null!;
    private int _checks;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok,string why)
    {
        if(!ok)throw new Exception(why);
        _checks++;GD.Print("PASS: "+why);
    }
    private async Task Frames(int count)
    {
        for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
    }
    private static Vector3 Ground(Vector3 v)=>v with { Y=0 };
    private async Task Walk(Vector3 to,string name)
    {
        var player=_stage.Player;
        int steps=0;
        while(Ground(player.GlobalPosition).DistanceTo(to)>.12f&&steps++<600)
        {
            var direction=(to-Ground(player.GlobalPosition)).Normalized();
            player.SetControls(new Vector2(direction.X,direction.Z),to+direction*4,false);
            await Frames(1);
        }
        player.SetControls(Vector2.Zero,to+Vector3.Right*4,false);
        Check(Ground(player.GlobalPosition).DistanceTo(to)<=.12f,$"{name}: actual player reaches {to} in {steps} ticks (at {player.GlobalPosition})");
        Check(Mathf.Abs(player.GlobalPosition.Y)<.15f,name+": feet remain on the floor");
    }
    private async Task Capture(string name)
    {
        if(!Windowed)return;
        foreach(bool close in new[]{false,true})
        {
            _stage.Cam.Close=close;
            // Exercise ordinary following and aim offset; never manually reposition the camera.
            foreach(var (label,direction) in new[]{("forward",Vector3.Forward),("side",Vector3.Right)})
            {
                _stage.Player.SetControls(Vector2.Zero,_stage.Player.GlobalPosition+direction*8,false);
                await Frames(35);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                string path=$"res://output/walkthrough-{name}-{(close?"close":"wide")}-{label}.png";
                Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath(path))==Error.Ok,"capture "+path);
            }
        }
        _stage.Cam.Close=false;
    }
    private async Task Circuit(string name,Vector3[] points)
    {
        // Teleport only to the starting point; all subsequent points and return travel use controls.
        _stage.Player.TeleportTo(points[0]);
        await Frames(5);
        for(int i=1;i<points.Length;i++)
        {
            await Walk(points[i],name+" outward "+i);
            if(i==1||i==2||i==points.Length-1)await Capture(name+"-"+i);
        }
        for(int i=points.Length-2;i>=0;i--)await Walk(points[i],name+" return "+i);
    }
    public override async void _Ready()
    {
        bool failed=false;
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
            foreach(int number in new[]{1,2})
            {
                _stage=new Stage { MapOverride=number==1?new StageOneLayout():new StageTwoMap(),
                    ModeOverride=GameMode.Scenario,CharacterOverride="goose",ThemeOverride="zombie",DirectorEnabled=false };
                AddChild(_stage);
                _stage.Player.ManualControl=true;
                _stage.Player.Invulnerable=true;
                foreach(var door in _stage.Map.Doors)door.SetOpen(true,0);
                _stage.Map.ApplyFlow();
                await Frames(3);
                if(number==1)
                    await Circuit("workbench",new[]{new Vector3(66.8f,0,-5.6f),new Vector3(66.8f,0,-8.9f),
                        new Vector3(70,0,-8.9f),new Vector3(73.5f,0,-8.9f),new Vector3(73.5f,0,-5.6f),new Vector3(66.8f,0,-5.6f)});
                else
                {
                    await Circuit("generator",new[]{new Vector3(78.4f,0,-9.3f),new Vector3(78.4f,0,-7.1f),
                        new Vector3(80.5f,0,-5.1f),new Vector3(82.1f,0,-3.3f),new Vector3(82.1f,0,-.5f)});
                    await Circuit("pipes",new[]{new Vector3(86.3f,0,-5.9f),new Vector3(86.3f,0,-8.9f),
                        new Vector3(88.4f,0,-8.9f),new Vector3(90.5f,0,-8.9f),new Vector3(90.5f,0,-5.6f)});
                    // Walk back from the rack to the actual supply row, then verify the existing interaction focus.
                    await Walk(new Vector3(90.5f,0,-3.7f),"leave material bay");
                    await Walk(new Vector3(85.2f,0,-3.7f),"approach supplies from route");
                    var supplies=StageSupplies.Stage2.Points.Single(p=>p.Id=="yard_shed");
                    foreach(var supply in supplies.Items.OrderByDescending(i=>i.At.X).ThenBy(i=>i.At.Z))
                    {
                        await Walk(supply.At,"approach "+supply.Kind);
                        await Frames(2);
                        Check(_stage.FocusedInteractable is {} focus&&Ground(focus.GlobalPosition).DistanceTo(supply.At)<.15f,
                            supply.Kind+" can be selected beside the new equipment");
                    }
                    await Capture("supplies");
                }
                _stage.Free();await Frames(2);
            }
            GD.Print("WORKSHOP_WALKTHROUGH_PASS "+_checks);
        }
        catch(Exception e){GD.PushError(e.ToString());failed=true;}
        if(IsInstanceValid(_stage))_stage.Free();
        QuitHygiene.Collect();GetTree().Quit(failed?1:0);
    }
}
