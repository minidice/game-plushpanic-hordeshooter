using Godot;
using System;

namespace HordeShooter.Tests;
public partial class ReloadPoseCheck : Node
{
    private int _checks;
    private void Check(bool ok,string message){if(!ok)throw new Exception(message);_checks++;}
    public override void _Ready()
    {
        try
        {
            foreach(var species in new[]{"goose","duck","raccoon","redpanda"})
            foreach(var id in new[]{"m4","m1911","r870","benelli_m4"})
            foreach(int profile in new[]{0,1,2})
            {
                var rig=CharacterRig.Create(CharacterCatalog.Player(species));AddChild(rig);
                rig.ApplyAppearance(new AppearanceProfile{Hat=profile==0?0:1,Vest=profile!=0,Pack=profile!=0,VestStyle=profile==2?1:0,
                    BodyWidth=profile==1?1.12f:profile==2?.88f:1,BodyHeight=profile==1?.90f:profile==2?1.10f:1,WingSize=profile==1?.8f:profile==2?1.25f:1});
                rig.HideBuiltInWeapon();
                var weapon=ArmoryCatalog.Weapon(id);var built=WeaponVisual.Build(weapon,rig.HandColor);
                rig.Weapon.AddChild(built.Root);rig.BindWeaponGrip(built);
                var idle=rig.Weapon.Transform;var leftHome=built.LeftGrip!.Transform;
                var state=new WeaponState(weapon,0,10);state.StartReload();
                Vector3 pouch=Vector3.Zero,port=Vector3.Zero;float error=0;
                for(int frame=0;frame<100;frame++)
                {
                    state.Tick(weapon.ReloadTime/100,false);
                    rig.SetLocomotionPose(.016f,1,false);
                    rig.SetReloadPose(.016f,state);rig.Animate(.016f,.7f,0,.1f,Vector3.Zero);
                    error=Mathf.Max(error,rig.GripConnectionError);
                    if(frame==40)pouch=rig.Model.ToLocal(built.LeftGrip.GlobalPosition);
                    if(frame==78)port=rig.Model.ToLocal(built.LeftGrip.GlobalPosition);
                }
                Check(error<.002f,$"{species}/{id}/{profile}: both wrists remain connected during movement and recoil deformation");
                Check(pouch.DistanceTo(port)>.15f,$"{species}/{id}/{profile}: hand travels between pouch and gun");
                int ammo=state.Mag;state.CancelReload();
                rig.SetLocomotionPose(0,0,true);rig.SetReloadPose(.2f,null);rig.Animate(0,0,0,0,Vector3.Zero);
                Check(!rig.ReloadPoseActive && rig.Weapon.Transform.IsEqualApprox(idle) && built.LeftGrip.Transform.IsEqualApprox(leftHome) && !built.LeftGrip.Visible,
                    $"{species}/{id}/{profile}: cancellation restores idle and hidden spare palm without drift");
                Check(state.Mag==ammo,$"{species}/{id}/{profile}: pose never changes ammunition");
                if(weapon.ShellReloadTime>0)
                {
                    state=new WeaponState(weapon,0,10);state.StartReload();state.Tick(weapon.ReloadTime+.001f,false);
                    rig.SetReloadPose(.2f,state);var start=built.LeftGrip.Position;
                    state.Tick(weapon.ShellReloadTime*.40f,false);rig.SetReloadPose(.016f,state);var middle=built.LeftGrip.Position;
                    state.Tick(weapon.ShellReloadTime*.58f,false);rig.SetReloadPose(.016f,state);var end=built.LeftGrip.Position;
                    Check(start.DistanceTo(end)<.04f && middle.DistanceTo(end)>.15f,"shell cycle returns continuously to port at each insertion");
                }
                rig.ResetPose();Check(!rig.ReloadPoseActive,"death/reset clears reload pose");rig.Free();
            }
            var stage=new Stage{CharacterOverride="goose",DirectorEnabled=false};AddChild(stage);
            var player=stage.Player;player.ManualControl=true;player.SetPhysicsProcess(false);
            var def=ArmoryCatalog.Weapon("m4");
            player.Weapons.Pickup(def,new WeaponState(def,0,10));
            var rest=player.Rig.Weapon.Transform;
            Check(player.Gun.StartReload(),"reload starts");
            player.Weapons.Tick(def.ReloadTime*.45f,false,false,false,false);
            player.Rig.Animate(0,0,0,0,Vector3.Zero);
            Check(player.Rig.Weapon.Position.DistanceTo(rest.Origin)>.1f,"magazine reload visibly brings gun forward instead of keeping idle pose");
            Check(player.Rig.GripConnectionError<.002f,"holding hand stays on weapon during reload");
            player.Weapons.Tick(def.ReloadTime,false,false,false,false);
            Check(!player.Gun.Reloading&&!player.Rig.ReloadPoseActive&&player.Gun.Mag==10,"completion restores pose and uses available reserve only");
            var shell=ArmoryCatalog.Weapon("r870");
            player.Weapons.Pickup(shell,new WeaponState(shell,0,5));
            player.Weapons.Tick(.01f,false,true,true,false);
            Check(player.Gun.Reloading,"empty trigger starts automatic reload");
            player.Weapons.Tick(shell.ReloadTime+.01f,false,false,false,false);
            Check(player.Gun.Mag==1&&player.Rig.ReloadPoseActive,"inserted shell available while animation continues");
            int shots=player.Weapons.ShotsFired;
            player.Weapons.Tick(.01f,false,true,true,false);
            Check(player.Weapons.ShotsFired==shots+1&&!player.Gun.Reloading&&!player.Rig.ReloadPoseActive&&Mathf.Abs(player.Rig.Weapon.Basis.Z.Y)<.001f,
                "shell interrupt restores horizontal muzzle before firing");
            player.Gun.StartReload();player.Weapons.Tick(.2f,false,false,false,false);
            Check(player.Rig.ReloadPoseActive,"reload restarts after shot");
            player.Weapons.Tick(.2f,false,false,false,true);
            Check(!player.Rig.ReloadPoseActive,"blocked action suppresses reload motion");
            player.Weapons.Tick(.1f,false,false,false,false);
            Check(player.Rig.ReloadPoseActive,"unblocked ongoing reload resumes at actual state time");
            player.Weapons.SelectSlot(1);
            Check(!player.Rig.ReloadPoseActive,"slot switch clears old reload pose immediately");
            player.Weapons.SelectSlot(2);player.Gun.StartReload();player.Weapons.Tick(.2f,false,false,false,false);
            player.Loadout.AddThrowable(ArmoryCatalog.Throwable("frag"));
            Check(player.Weapons.BeginThrowAim()&&!player.Rig.ReloadPoseActive,"throw aiming clears reload immediately");
            player.Weapons.EndThrowAim();
            foreach(var id in new[]{"double_barrel","alabong","mortar"})
            {
                var unsupported=ArmoryCatalog.Weapon(id);player.Weapons.Pickup(unsupported,new WeaponState(unsupported,0,3));
                player.Gun.StartReload();player.Weapons.Tick(.2f,false,false,false,false);
                Check(!player.Rig.ReloadPoseActive,id+" keeps original pose until dedicated reload is authored");
            }
            stage.Free();
            GD.Print("RELOAD_POSE_PASS "+_checks);QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
