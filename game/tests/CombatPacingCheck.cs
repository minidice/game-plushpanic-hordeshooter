using Godot;
using System;

namespace HordeShooter.Tests;

public partial class CombatPacingCheck : Node3D
{
    private int _checks;
    private void Check(bool value,string message) { if(!value)throw new Exception(message);_checks++;GD.Print("PASS: "+message); }
    public override void _Ready()
    {
        try
        {
            Check(WeaponDatabase.Current.Errors.Count==0,"updated weapon CSV resolves without errors");
            foreach(var (id,mag,seconds) in new[]{("m4",50,2.2f),("mp5",50,2.2f),("m16",60,2.4f),("ak47",40,2.3f),("m1911",15,1.5f)})
            {
                var def=ArmoryCatalog.Weapon(id);
                Check(def.MagSize==mag&&Mathf.IsEqualApprox(def.ReloadTime,seconds),id+" pacing target");
                var state=new WeaponState(def,mag-3);
                int total=state.TotalAmmo;
                Check(state.StartReload(),id+" starts partial reload");
                state.Tick(seconds-.01f,false);Check(state.Mag==mag-3,id+" never refills early");
                state.Tick(.02f,false);Check(state.Mag==mag&&!state.Reloading,id+" finishes on schedule");
                if(!def.InfiniteReserve)Check(state.TotalAmmo==total,id+" conserves ammunition");
            }
            foreach(var id in new[]{"r870","benelli_m4"})
            {
                var def=ArmoryCatalog.Weapon(id);
                var s=new WeaponState(def,0,5);
                Check(s.StartReload(),id+" empty reload starts");
                s.Tick(def.ReloadTime-.01f,false);Check(s.Mag==0,id+" startup has no instant shell");
                s.Tick(.02f,false);Check(s.Mag==1&&s.Reserve==4&&s.Reloading,id+" first shell usable before full reload");
                Check(s.Trigger(true,true)&&s.Mag==0&&!s.Reloading,id+" fire interrupts shell reload");
                s.Tick(1,false);s.StartReload();s.Tick(10,false);
                Check(s.Mag==4&&s.Reserve==0&&!s.Reloading,id+" long frame inserts remaining ammo only");
                s=new WeaponState(def,3,6);s.StartReload();s.Tick(def.ReloadTime+.01f,false);s.Holster();
                int loaded=s.Mag;s.Tick(10,false);Check(s.Mag==loaded&&s.TotalAmmo==9&&!s.Reloading,id+" switching preserves inserted shells and cancels remaining");
                s=new WeaponState(def,def.MagSize-1,3);s.StartReload();s.Tick(10,false);
                Check(s.Mag==def.MagSize&&s.Reserve==2,id+" topping off cannot overfill");
            }
            var doubleBarrel=new WeaponState(ArmoryCatalog.Weapon("double_barrel"),0,2);
            doubleBarrel.StartReload();doubleBarrel.Tick(.6f,false);
            Check(doubleBarrel.Mag==0&&!doubleBarrel.Trigger(true,true),"double barrel remains a two-shell batch reload");
            doubleBarrel.Tick(.71f,false);Check(doubleBarrel.Mag==2&&!doubleBarrel.Reloading,"double barrel reloads both shells in 1.3 seconds");
            var camera=new CameraRig();AddChild(camera);camera.SnapTo(Vector3.Zero);
            var projected=camera.UnprojectPosition(Vector3.Right*10)-camera.UnprojectPosition(Vector3.Zero);
            Check(projected.X>0&&projected.Y<0,"Stage 1 main +X route projects upper-right");
            float angle=Mathf.RadToDeg(Mathf.Atan2(-projected.Y,projected.X));
            Check(angle>=15&&angle<=75,"progression stays inside level design direction cone");
            Check(camera.GroundMovement(Vector2.Right).Dot(Vector3.Right)>0,"screen-relative movement follows camera");
            camera.Free();GD.Print("COMBAT_PACING_PASS "+_checks);QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
