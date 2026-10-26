using Godot;
using System;

namespace HordeShooter.Tests;
public partial class WeaponGripCheck : Node
{
    public override void _Ready()
    {
        try
        {
            int count=0;
            foreach(var species in new[]{"goose","duck","raccoon","redpanda"})
            foreach(var id in new[]{"m4","m1911","r870","m249","mortar","alabong","frying_pan"})
            {
                var rig=CharacterRig.Create(CharacterCatalog.Player(species));AddChild(rig);rig.HideBuiltInWeapon();
                var gun=WeaponVisual.Build(ArmoryCatalog.Weapon(id),rig.HandColor);rig.Weapon.AddChild(gun.Root);rig.BindWeaponGrip(gun);
                Require(rig.Model.FindChild(species+"_leftArm",true,false) is Node3D{Visible:false} && rig.Model.FindChild(species+"_rightArm",true,false) is Node3D{Visible:false},"static wings hidden while weapon is held");count++;
                if(id=="m4")
                {
                    Require(gun.StockContact!=null&&rig.UsesSupportGrip&&gun.LeftGrip is {Visible:true},species+" shared shouldered rifle with support hand");count++;
                    rig.SetLocomotionPose(.2f,1,false);rig.Animate(.016f,1,0,0,Vector3.Zero);
                    Require(rig.IsRunningCarry&&rig.Weapon.Basis.Z.Y<-.3f&&rig.GripConnectionError<.002f,species+" run lowers barrel while retaining grip");count++;
                    rig.SetLocomotionPose(.016f,1,true);
                    Require(!rig.IsRunningCarry&&Math.Abs(rig.Weapon.Basis.Z.Y)<.001f&&rig.GripConnectionError<.002f,species+" firing lowers muzzle before shot update");count++;
                }
                for(int i=0;i<4;i++)
                {
                    rig.Animate(.016f,.8f,i*.25f,.2f,new Vector3(2,0,1));
                    if(i==2)rig.DodgePose(.5f);
                    Require(rig.GripConnectionError<.002f,$"{species}/{id} arm ends meet weapon grips during motion");count++;
                }
                gun.Root.Visible=false;rig.UpdateWeaponGrip();
                Require(!rig.WeaponArmsVisible,"hidden weapon does not leave floating arms");count++;
                rig.Free();
            }
            foreach(var id in new[]{"m1911","glock17"})
            {
                var rig=CharacterRig.Create(CharacterCatalog.Player("goose"));AddChild(rig);
                var gun=WeaponVisual.Build(ArmoryCatalog.Weapon(id).DualVariant!,rig.HandColor);rig.Weapon.AddChild(gun.Root);rig.BindWeaponGrip(gun);
                rig.Animate(.016f,1,1,0,Vector3.Zero);
                Require(gun.LeftGrip!=null&&gun.RightGrip.GlobalPosition.X>gun.LeftGrip.GlobalPosition.X&&rig.GripConnectionError<.002f,"dual hands follow separate pistol grips");count++;
                rig.Free();
            }
            var stage=new Stage{CharacterOverride="goose",DirectorEnabled=false};AddChild(stage);
            stage.Player.ManualControl=true;stage.Player.Invulnerable=true;stage.Player.SetPhysicsProcess(false);
            foreach(var id in new[]{"m16"})
            {
                var player=stage.Player;player.Weapons.Pickup(ArmoryCatalog.Weapon(id),null);
                player.Rig.SetLocomotionPose(1,1,false);
                int before=player.Weapons.ShotsFired;
                for(int i=0;i<30;i++)
                {
                    player.Rig.SetLocomotionPose(.04f,1,false);
                    int previous=player.Weapons.ShotsFired;
                    player.Weapons.Tick(.04f,true,i==0,i==0,false);
                    if(player.Weapons.ShotsFired>previous)
                    {Require(Math.Abs(player.Rig.Weapon.Basis.Z.Y)<.001f,id+" every queued burst shot lowers muzzle after trigger release");count++;}
                }
                Require(player.Weapons.ShotsFired-before==3,id+" tap completes three burst rounds");count++;
                Require(player.Rig.IsRunningCarry,id+" resumes running carry after aim hold expires");count++;
            }
            stage.Free();
            GD.Print("WEAPON_GRIP_PASS "+count);QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
}
