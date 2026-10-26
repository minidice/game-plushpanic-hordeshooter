using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
namespace HordeShooter.Tests;

/// <summary>Actual Stage2 AI firing and projectile origins, plus clearly separated inspection poses.</summary>
public partial class MammalArmStageCheck : Node
{
    private Stage _stage=null!;
    private int _checks;
    private void Check(bool ok,string label){if(!ok)throw new Exception(label);_checks++;}
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);}
    private Vector3 At(float x)=>new(x,_stage.Tiers.GroundY(new Vector3(x,0,-2))+.05f,-2);
    private async Task Shot(string name){
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var img=GetViewport().GetTexture().GetImage();
        Check(img.SavePng(ProjectSettings.GlobalizePath("res://output/mammal-arms/"+name+".png"))==Error.Ok,"capture "+name);
    }
    public override async void _Ready(){
        int exit=0;
        try {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output/mammal-arms"));
            _stage=new Stage{MapOverride=new LayoutMap(),CharacterOverride="raccoon",ThemeOverride="animals",DirectorEnabled=false,ModeOverride=GameMode.Scenario,SquadOverride=0,ArrivalOverride=false,NextStageOverride=false};
            AddChild(_stage);await Frames(5);_stage.Player.ManualControl=true;_stage.Player.Invulnerable=true;
            foreach(string weapon in new[]{"m4","mp5","ak47"}) {
                foreach(var e in _stage.AllEnemies.ToArray())e.QueueFree();await Frames(3);
                _stage.Player.TeleportTo(At(16));_stage.Cam.SnapTo(At(13)+Vector3.Up);
                var enemy=(RangedEnemy)_stage.SpawnCommonIdentity(new("armed","tiger","ochre",weapon,0,CommonGear.Vest),At(10),true);
                _stage.Player.SetControls(Vector2.Zero,enemy.GlobalPosition+Vector3.Up,false);
                float maxOriginError=0;int projectiles=0;
                void Entered(Node node){if(node is EnemyProjectile shot&&shot.WeaponId==weapon){
                    var muzzle=(Node3D)enemy.Rig.Weapon.FindChild("Weapon_"+weapon,true,false).FindChild("Muzzle",true,false);
                    maxOriginError=Mathf.Max(maxOriginError,shot.GlobalPosition.DistanceTo(muzzle.GlobalPosition));projectiles++;
                }}
                _stage.ChildEnteredTree+=Entered;
                for(int i=0;i<360&&enemy.ProjectilesFired==0;i++) {await Frames(1);Check(enemy.Rig.GripConnectionError<.002f,"active AI grip contact");}
                _stage.ChildEnteredTree-=Entered;
                Check(enemy.ProjectilesFired>0&&projectiles>0,weapon+" actual AI projectile");
                Check(maxOriginError<.002f,weapon+" projectile starts at actual moved barrel");
                await Shot("stage2-"+weapon+"-fire");
                enemy.SetPhysicsProcess(false);enemy.Rig.ResetPose();
                var cam=_stage.Cam;cam.SetProcess(false);var saved=cam.Transform;float savedSize=cam.Size;
                var focus=enemy.GlobalPosition+Vector3.Up;
                enemy.Rig.Face(new Vector3(.15f,0,1));cam.GlobalPosition=focus+new Vector3(2,2.5f,4);cam.LookAt(focus);cam.Size=4.5f;
                await Frames(3);await Shot("stage2-"+weapon+"-close");
                var state=new WeaponState(ArmoryCatalog.Weapon(weapon),0,30);state.StartReload();
                state.Tick(state.Def.ReloadTime*.45f,false);enemy.Rig.SetReloadPose(.2f,state);enemy.Rig.Animate(.016f,0,0,0,Vector3.Zero);
                Check(enemy.Rig.GripConnectionError<.002f,"inspection reload grip");await Frames(2);await Shot("stage2-"+weapon+"-reload");
                cam.Transform=saved;cam.Size=savedSize;cam.SetProcess(true);
            }
            GD.Print("MAMMAL_ARM_STAGE_PASS "+_checks);
        }catch(Exception e){GD.PushError("MAMMAL_ARM_STAGE_FAIL "+e);exit=1;}
        _stage?.QueueFree();await Frames(3);QuitHygiene.Collect();GetTree().Quit(exit);
    }
}
