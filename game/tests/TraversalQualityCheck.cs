using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class TraversalQualityCheck : Node
{
    private Stage _stage = null!;
    private int _checks;
    private void Check(bool ok, string text) { if (!ok) throw new Exception(text); GD.Print("PASS: " + text); _checks++; }
    private async Task Frames(int n) { for (int i=0;i<n;i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Shot(string name)
    {
        if (DisplayServer.GetName()=="headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/quality-"+name+".png"))==Error.Ok,"render "+name);
        if(name.EndsWith("-zip-hang") || name.EndsWith("-zip-release"))
        {
            // Inspect hands/feet from the front as well as the normal gameplay camera.
            var rider=(Player)_stage.Cam.Target!;
            var close=new Camera3D { Fov=42,Position=rider.GlobalPosition+rider.Rig.Forward*3-rider.Rig.GlobalBasis.X*3+Vector3.Up*3.5f };
            AddChild(close); close.LookAt(rider.GlobalPosition+Vector3.Up*1.1f); close.Current=true;
            await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
            Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/quality-"+name+"-detail.png"))==Error.Ok,"render "+name+" detail");
            close.Free(); _stage.Cam.Current=true;
        }
    }
    public override async void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;
        bool failed=false;
        try { await Run(); GD.Print("TRAVERSAL_QUALITY_PASS "+_checks); }
        catch(Exception e) { GD.PushError(e.ToString()); GD.Print("TRAVERSAL_QUALITY_FAIL "+e.Message); failed=true; }
        GetTree().Paused=false;
        if (_stage!=null && IsInstanceValid(_stage)) _stage.Free();
        QuitHygiene.Collect(); GetTree().Quit(failed?1:0);
    }
    private async Task Run()
    {
        _stage=new Stage { MapOverride=new LayoutMap(), ModeOverride=GameMode.Scenario, CharacterOverride="goose", ThemeOverride="zombie", DirectorEnabled=false, SquadOverride=0 };
        AddChild(_stage); await Frames(5);
        var p=_stage.Player; p.ManualControl=true; p.Invulnerable=true;
        foreach(var enemy in _stage.AllEnemies.ToList()) enemy.QueueFree();
        var ladder=_stage.Ladders.OrderByDescending(l=>l.Tiers).First();
        var into=((ladder.Top-ladder.Bottom) with {Y=0}).Normalized();
        p.TeleportTo(ladder.Top+Vector3.Up*.05f);
        p.Weapons.Pickup(ArmoryCatalog.Weapon("m4"),null); p.HoldAim(true);
        await Frames(20);
        Check(p.AimBlend>.9f,"fixture enters traversal while aiming");
        _stage.Cam.Close=true; _stage.Cam.SnapTo(p.GlobalPosition);
        Check(p.ClimbLadder(ladder,false),"descending begins");
        Check(p.AimBlend==0&&!p.IsAiming,"ladder entry clears aim zoom immediately"); p.HoldAim(false);
        await Frames(12);
        Check(p.Rig.Forward.Dot(into)>.99f,"descending faces the ladder, just like ascending");
        await Shot("ladder-descend");
        while(p.OnLadder) await Frames(1);
        Check(p.GlobalPosition.DistanceTo(ladder.Bottom)<.2f,"descending lands at the lower entrance");
        p.SetControls(Vector2.Zero,p.GlobalPosition+into*5,false);
        Check(p.ClimbLadder(ladder,true),"ascending begins");
        await Frames(30);
        Check(p.Rig.Forward.Dot(into)>.99f,"ascending also faces the ladder");
        await Shot("ladder-ascend");
        while(p.OnLadder) await Frames(1);
        var line=_stage.Ziplines.Single();
        Check(line.Landing.DistanceTo(line.End)>1 && !_stage.Flow.IsBlocked(line.Landing),"landing clears the end post on an open floor cell");
        Check(line.SpeedAt(0)==5 && line.SpeedAt(.5f)>line.SpeedAt(.1f) && line.SpeedAt(20)==24,"zipline accelerates from 5 to a capped 24 m/s");
        Check(line.ProgressAt(0)==0 && Mathf.Abs(line.ProgressAt(line.TravelSeconds)-1)<.001f,"integrated speed reaches release at the advertised time");
        Check(line.Seconds<line.Start.DistanceTo(line.End)/12f,"long zipline including dismount is faster than the old constant ride");
        var passengers=new System.Collections.Generic.List<Player>{p};
        foreach(string id in new[]{"goose","duck","raccoon","redpanda"})
        {
            var rider=id=="goose"?p:_stage.AddSurvivor(CharacterCatalog.Player(id),line.Start,true,"Quality"+id);
            if(rider!=p) passengers.Add(rider);
            rider.ManualControl=true; rider.Invulnerable=true;
            rider.TeleportTo(line.Start+Vector3.Up*.05f);
            rider.SetControls(Vector2.Zero,line.End+Vector3.Up,false);
            _stage.Cam.Target=rider; _stage.Cam.Player=rider; _stage.Cam.SnapTo(rider.GlobalPosition);
            Check(rider.RideZipline(line),id+" hooks onto the cable");
            int mag=rider.Gun.Mag;
            await Frames(10);
            Check(rider.Rig.ZiplinePoseActive && !rider.Rig.Weapon.Visible && rider.Rig.WeaponArmsVisible,id+" hangs with raised arms and weapon stowed");
            var first=rider.GlobalPosition;
            await Frames(20); var second=rider.GlobalPosition;
            await Shot(id+"-zip-hang");
            await Frames(20); var third=rider.GlobalPosition;
            Check(second.DistanceTo(third)>first.DistanceTo(second)*1.2f,id+" actually moves farther in the next equal time interval");
            rider.PressRoll(); rider.SetControls(Vector2.One,line.End,true);
            for(int i=0;i<300 && !rider.Rig.ZiplineReleasing;i++) await Frames(1);
            Check(rider.OnZipline && rider.Rig.ZiplineReleasing && !rider.IsRolling && rider.Gun.Mag==mag,id+" releases into the jump with combat and rolling still blocked");
            await Frames(8); await Shot(id+"-zip-release");
            rider.SetControls(Vector2.Zero,line.End+Vector3.Right*5,false);
            for(int i=0;i<100 && rider.OnZipline;i++) await Frames(1);
            Check(!rider.OnZipline && rider.GlobalPosition.DistanceTo(line.Landing)<.2f,id+" lands beside the destination post");
            Check(!rider.Rig.ZiplinePoseActive && rider.Rig.Weapon.Visible && rider.Rig.Pose.Transform.IsEqualApprox(Transform3D.Identity),id+" restores weapon and normal body pose");
            await Frames(4); await Shot(id+"-zip-land");
            rider.TeleportTo(line.Start);
            Check(rider.RideZipline(line),id+" can ride again"); await Frames(3);
            rider.TeleportTo(line.End);
            Check(!rider.OnZipline && !rider.Rig.ZiplinePoseActive && rider.Rig.Weapon.Visible,id+" teleport cancels the hanging pose");
            rider.TeleportTo(line.End+Vector3.Back*8+Vector3.Right*passengers.Count*2);
        }
        var gondola=_stage.Gondolas.Single();
        Check(gondola.Platform.Size==new Vector2(6,5),"six-person cabin has a 6 by 5 metre floor");
        foreach(string id in new[]{"goose","duck"}) passengers.Add(_stage.AddSurvivor(CharacterCatalog.Player(id),gondola.GlobalPosition,true,"Extra"+id));
        for(int i=0;i<passengers.Count;i++)
        {
            var rider=passengers[i]; rider.ManualControl=true; rider.Invulnerable=true;
            rider.TeleportTo(gondola.GlobalPosition+new Vector3((i%3-1)*1.55f,.05f,(i/3-.5f)*1.6f));
            rider.SetControls(Vector2.Zero,rider.GlobalPosition+Vector3.Right*5,false);
            rider.ProcessMode=ProcessModeEnum.Always;
        }
        _stage.Cam.Target=p; _stage.Cam.Player=p; _stage.Cam.SnapTo(gondola.GlobalPosition+Vector3.Up);
        await Frames(5); await Shot("gondola-six-space");
        p.SetControls(Vector2.Right,p.GlobalPosition+Vector3.Right*5,false); p.PressRoll(); await Frames(2);
        Check(p.IsRolling,"regression fixture starts rolling before departure");
        var positions=passengers.Select(r=>r.GlobalPosition).ToArray();
        Check(gondola.TryDepart(),"six survivors depart even when one started a roll");
        Check(passengers.All(r=>r.OnGondola&&!r.IsRolling),"departure explicitly locks every passenger and cancels an active roll");
        Check(!p.ClimbLadder(ladder,true)&&!p.RideZipline(line),"other traversal cannot take over a gondola passenger");
        for(int tick=0;tick<120;tick++)
        {
            foreach(var rider in passengers) { rider.SetControls(Vector2.One,rider.GlobalPosition+Vector3.Right*8,true); rider.PressRoll(); rider.NetMotion=rider.GlobalPosition+Vector3.Right*20; }
            await Frames(1);
        }
        Check(passengers.Select((r,i)=>r.GlobalPosition.DistanceTo(positions[i])<.001f&&!r.IsRolling).All(v=>v),"repeated roll, movement and network positions cannot escape even with Always processing");
        await Shot("gondola-roll-blocked");
        while(gondola.Riding) await Frames(1);
        await Frames(3);
        Check(passengers.All(r=>!r.OnGondola&&!r.IsRolling),"arrival unlocks everybody without replaying queued rolls");
        await Shot("gondola-arrived");
        await Frames((int)Mathf.Ceil(p.RollCooldownLeft*60)+2);
        var before=p.GlobalPosition;
        p.SetControls(Vector2.Right,p.GlobalPosition+Vector3.Right*5,false); p.PressRoll(); await Frames(3);
        Check(p.IsRolling&&p.GlobalPosition.DistanceTo(before)>.1f,"a fresh roll works after arrival");
        await Frames(40); p.SetControls(Vector2.Zero,p.GlobalPosition+Vector3.Right*5,false);
        var interrupted=Gondola.Create(_stage,new Rect2(gondola.Arrival.X-3,gondola.Arrival.Z-2.5f,6,5),gondola.Arrival.Y,gondola.Arrival+Vector3.Right*10,Vector3.Right,12);
        for(int i=0;i<passengers.Count;i++) passengers[i].TeleportTo(gondola.Arrival+new Vector3((i%3-1)*1.55f,.05f,(i/3-.5f)*1.6f));
        _stage.SetReviveForTest(true);
        var downed=passengers.Last(); downed.Invulnerable=false; downed.TakeDamage(9999,Vector3.Zero,0); await Frames(2);
        Check(downed.IsDowned,"fixture has a downed but living passenger");
        var downPose=downed.Rig.Pose.Transform;
        Check(interrupted.TryDepart(),"interruption fixture departs");
        Check(downed.Rig.Pose.Transform.IsEqualApprox(downPose),"departure preserves the downed passenger pose");
        await Shot("gondola-downed");
        interrupted.Free();
        Check(passengers.All(r=>!r.OnGondola)&&!GetTree().Paused&&!_stage.AllStopped,"removing the ride releases all passenger locks and pause");
    }
}
