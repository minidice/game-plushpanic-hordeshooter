using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace HordeShooter.Tests;

/// <summary>Actual physics attacks, cancellation, misses and positional cues; windowed runs capture gameplay.</summary>
public partial class HunterCombatCheck : Node
{
    private Stage _stage = null!;
    private int _checks;
    private bool ParrotReview => OS.GetCmdlineUserArgs().Contains("--parrot-stage2");
    private bool LionReview => OS.GetCmdlineUserArgs().Contains("--lion-stage2");
    private bool ReindeerReview => OS.GetCmdlineUserArgs().Contains("--reindeer-stage2");
    private bool Stage2Review => ReindeerReview || ParrotReview || LionReview || OS.GetCmdlineUserArgs().Contains("--eagle-stage2");
    private string ReviewSpecies => ReindeerReview ? "reindeer" : LionReview ? "lion" : ParrotReview ? "parrot" : "eagle";
    private Vector3 TestPoint(float x) => Stage2Review ? new(x+8, _stage.Tiers.GroundY(new(x+8,0,-2))+.05f, -2) : new(x,.05f,0);
    private sealed class Field : StageMap
    {
        public override Rect2 Area => new(-20,-20,40,40);
        protected override void BuildMap() { PlayerStart=new(4,0,0); Floor(-20,20,-20,20,"777857"); }
    }
    private async Task Frames(int n) { for(int i=0;i<n;i++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
    private void Check(bool ok,string label) { if(!ok)throw new Exception(label);_checks++;GD.Print("PASS "+label); }
    private async Task Until(Func<bool> condition,string label)
    { for(int i=0;i<200&&!condition();i++)await Frames(1);Check(condition(),label); }
    private int Heard(string id) => _stage.Sfx.Played.GetValueOrDefault(id);
    private async Task Shot(string label)
    {
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://output/hunter-combat-"+(Stage2Review?ReviewSpecies+"-stage2-":"")+label+".png"))==Error.Ok,"capture "+label);
    }
    private async Task<Enemy> Spawn(string weapon)
    {
        foreach(var e in _stage.AllEnemies.ToArray())e.QueueFree();
        foreach(var p in _stage.GetChildren().OfType<EnemyProjectile>().ToArray())p.QueueFree();
        await Frames(3);
        var player=_stage.Player;player.Heal(999);player.Invulnerable=false;
        player.TeleportTo(TestPoint(4.05f));player.SetControls(Vector2.Zero,TestPoint(3)+Vector3.Up,false);
        _stage.Cam.SnapTo(TestPoint(3)+Vector3.Up);
        return _stage.SpawnCommonIdentity(new("hunter",ReviewSpecies,"red",weapon),TestPoint(weapon=="woodbow"?-2:3),true);
    }
    public override async void _Ready()
    {
        int exit=0;
        try
        {
            _stage=new Stage{MapOverride=Stage2Review?new LayoutMap():new Field(),CharacterOverride="goose",ThemeOverride="animals",DirectorEnabled=false,
                ModeOverride=GameMode.Scenario,SquadOverride=0,ArrivalOverride=false,NextStageOverride=false};
            AddChild(_stage);await Frames(4);_stage.Player.ManualControl=true;
            if (Stage2Review) await ReviewApproach();
            if (Stage2Review && OS.GetCmdlineUserArgs().Contains("--brow-review")) await ReviewBrows();
            foreach(string weapon in new[]{"club","knife"}) await Melee(weapon);
            await Bow();GD.Print("HUNTER_COMBAT_PASS "+_checks);
        }
        catch(Exception e){GD.PushError("HUNTER_COMBAT_FAIL "+e);exit=1;}
        _stage?.QueueFree();await Frames(3);QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private async Task ReviewApproach()
    {
        var enemy=await Spawn("club");
        _stage.Player.TeleportTo(TestPoint(9));
        _stage.Player.SetControls(Vector2.Zero,enemy.GlobalPosition+Vector3.Up,false);
        float before=enemy.GlobalPosition.DistanceTo(_stage.Player.GlobalPosition);
        await Frames(36);
        Check(enemy.GlobalPosition.DistanceTo(_stage.Player.GlobalPosition)<before-1,ReviewSpecies+" approaches through actual Stage2 physics");
        await Shot("walk");
        enemy.SetPhysicsProcess(false);enemy.Rig.ResetPose();
        await Frames(2);await Shot("stance");
    }
    private async Task ReviewBrows()
    {
        // Facial inspection in the actual map lighting. Close views deliberately use an inspection camera.
        foreach (var e in _stage.AllEnemies.ToArray()) e.QueueFree();
        await Frames(3);
        var camera = _stage.Cam; var saved = camera.Transform;
        camera.SetProcess(false);
        try
        {
            foreach (int variant in new[] { 0, 3, 9 })
            {
                var enemy = _stage.SpawnCommonIdentity(new("hunter",ReviewSpecies,"ochre","club",variant),TestPoint(3),true);
                enemy.SetPhysicsProcess(false);
                var focus = enemy.GlobalPosition + Vector3.Up * 1.1f;
                camera.Transform = saved;
                enemy.Rig.Face(camera.GlobalPosition - enemy.GlobalPosition);
                if (variant == 0) { await Frames(2); await Shot("brows-game-distance"); }
                camera.GlobalPosition = focus + (camera.GlobalPosition-focus).Normalized()*5;
                camera.LookAt(focus); await Frames(2); await Shot("brows-close-"+variant);
                enemy.QueueFree(); await Frames(2);
            }
        }
        finally { camera.Transform=saved; camera.SetProcess(true); }
    }
    private async Task Melee(string weapon)
    {
        string prefix="hunter_"+weapon;
        var enemy=await Spawn(weapon);int hp=_stage.Player.Health;
        int ready=Heard(prefix+(weapon=="club"?"_raise":"_ready")),swing=Heard(prefix+"_swing"),hit=Heard(prefix+"_hit");
        await Until(()=>enemy.IsMeleeAttacking,weapon+" starts actual windup");
        await Frames(3);Check(_stage.Player.Health==hp,weapon+" early pose causes no damage");
        await Shot(weapon+"-ready");
        await Until(()=>_stage.Player.Health<hp,weapon+" actual contact applies damage");
        Check(Heard(prefix+"_swing")==swing+1&&Heard(prefix+"_hit")==hit+1,weapon+" one swing and one confirmed contact cue");
        Check(Heard(prefix+(weapon=="club"?"_raise":"_ready"))==ready+1,weapon+" one dedicated ready cue");
        await Shot(weapon+"-impact");
        await Frames(12);Check(Heard(prefix+"_hit")==hit+1,weapon+" recovery does not repeat contact sound");
        enemy=await Spawn(weapon);await Until(()=>enemy.IsMeleeAttacking,weapon+" miss windup");
        hit=Heard(prefix+"_hit");swing=Heard(prefix+"_swing");_stage.Player.TeleportTo(TestPoint(9));
        await Frames(50);Check(Heard(prefix+"_hit")==hit&&Heard(prefix+"_swing")==swing+1,weapon+" miss has swing without impact cue");
        enemy=await Spawn(weapon);await Until(()=>enemy.IsMeleeAttacking,weapon+" cancel windup");
        hp=_stage.Player.Health;hit=Heard(prefix+"_hit");swing=Heard(prefix+"_swing");enemy.Stun(2);
        await Frames(45);Check(!enemy.IsMeleeAttacking&&_stage.Player.Health==hp&&Heard(prefix+"_hit")==hit&&Heard(prefix+"_swing")==swing,weapon+" stun cancels damage and future swing sound");
    }
    private async Task Bow()
    {
        var bow=(RangedEnemy)await Spawn("woodbow");
        int snort=Heard("bull_snort"),oldHit=Heard("zombie_hit"),draw=Heard("hunter_bow_draw"),release=Heard("hunter_bow_release");
        bow.StartWindup();await Frames(30);await Shot("bow-draw");
        Check(bow.WindingUp&&Heard("hunter_bow_draw")==draw+1&&Heard("bull_snort")==snort,"bow draws with dedicated cue");
        Check(bow.Rig.Weapon.FindChild("BowNockedArrow",true,false) is Node3D{Visible:true},"arrow visible while drawing");
        await Until(()=>bow.ProjectilesFired==1,"bow emits actual projectile");
        Check(Heard("hunter_bow_release")==release+1&&Heard("zombie_hit")==oldHit,"bow release has dedicated sound once");
        Check(bow.Rig.Weapon.FindChild("BowNockedArrow",true,false) is Node3D{Visible:false},"nocked arrow hidden on actual release");
        await Shot("bow-release");await Frames(30);
        Check(bow.Rig.Weapon.FindChild("BowNockedArrow",true,false) is Node3D{Visible:true},"bow renocks after recovery");
        bow=(RangedEnemy)await Spawn("woodbow");bow.StartWindup();await Frames(15);release=Heard("hunter_bow_release");bow.Stun(2);
        Check(!bow.GetChildren().OfType<AudioStreamPlayer3D>().Any(p=>p.Playing),"interrupted draw emitter stops immediately");
        await Frames(80);Check(bow.ProjectilesFired==0&&Heard("hunter_bow_release")==release&&!bow.WindingUp,"stun aborts bow without phantom release");
        Check(bow.Rig.Weapon.FindChild("BowNockedArrow",true,false) is Node3D{Visible:true},"interruption restores idle arrow");
    }
}
