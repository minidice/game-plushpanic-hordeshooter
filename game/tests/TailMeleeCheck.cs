using Godot;
using System;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class TailMeleeCheck : Node
{
    private int _checks;
    private void Check(bool value,string message)
    { if(!value)throw new Exception(message);_checks++; }

    public override async void _Ready()
    {
        try
        {
            foreach(var species in new[]{"goose","duck","raccoon","redpanda"})
            {
                var rig=CharacterRig.Create(CharacterCatalog.Player(species));AddChild(rig);
                var tail=(Node3D)rig.Model.FindChild(species+"_tail",true,false)!;
                CheckTailSurface((MeshInstance3D)tail.FindChild(species+"_tail_mesh",true,false)!,species);
                var anchor=(Node3D)tail.GetParent();
                Check(anchor.Name=="TailMotion",species+" has a rear attachment pivot independent of size");
                var root=anchor.Position;var rest=tail.Transform;
                foreach(float size in new[]{.7f,1.3f,1f})
                {
                    var profile=new AppearanceProfile{TailSize=size,Tail="#bad4aa"};
                    rig.ApplyAppearance(profile);var shape=tail.Transform;
                    for(int i=0;i<90;i++)rig.Animate(.016f,.8f,0,0,Vector3.Zero);
                    rig.ApplyAppearance(profile);
                    Check(anchor.Position.IsEqualApprox(root)&&tail.Position.IsZeroApprox(),species+" tail size keeps embedded rear root");
                    Check(tail.Transform.IsEqualApprox(shape),species+" tail scale never compounds while animating");
                    Check(!anchor.Basis.IsEqualApprox(Basis.Identity),species+" tail has restrained secondary motion");
                }
                rig.ApplyAppearance(new AppearanceProfile());
                Check(tail.Transform.IsEqualApprox(rest),species+" tail reset restores stable home");
                var built=WeaponVisual.Build(ArmoryCatalog.Weapon("frying_pan"),rig.HandColor);
                rig.Weapon.AddChild(built.Root);rig.BindWeaponGrip(built);
                var idle=rig.Weapon.Transform;
                foreach(float at in new[]{0f,.055f,.11f,.17f,.23f,.33f,.42f})
                {
                    rig.SetMeleePose(at);rig.Animate(.016f,.6f,0,0,Vector3.Zero);
                    Check(rig.GripConnectionError<.002f&&!rig.UsesSupportGrip,species+" one-hand grip stays connected throughout swing");
                }
                rig.SetMeleePose(-1);rig.Animate(.016f,0,0,0,Vector3.Zero);
                Check(rig.Weapon.Transform.IsEqualApprox(idle),species+" melee recovery returns exactly to carry pose");
                rig.Free();
            }
            CheckZombieTails();
            await CheckTimingAndCancellation();
            GD.Print("TAIL_MELEE_PASS "+_checks);QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }

    private void CheckZombieTails()
    {
        foreach(var species in new[]{"goose","duck","raccoon","redpanda"})
        {
            var definition=CharacterDefinition.MakeZombie(CharacterCatalog.Player(species),CharacterCatalog.Enemy("plush"));
            var rig=CharacterRig.Create(definition);AddChild(rig);
            Check(definition.Id=="plush_"+species,"zombie fixture has a composite definition ID");
            var tail=(Node3D)rig.Model.FindChild(species+"_tail",true,false)!;
            var mesh=(MeshInstance3D)tail.FindChild(species+"_tail_mesh",true,false)!;
            var anchor=(Node3D)tail.GetParent();
            Check(anchor.Name=="TailMotion",species+" zombie resolves its original tail attachment");
            CheckTailSurface(mesh,species);
            Check(mesh.MaterialOverlay==definition.ZombieOverlay,species+" repaired zombie tail retains infection tint");
            var root=anchor.Position;var home=tail.Transform;
            for(int i=0;i<90;i++)rig.Animate(.016f,.8f,0,0,Vector3.Zero);
            Check(anchor.Position.IsEqualApprox(root)&&tail.Transform.IsEqualApprox(home),species+" zombie tail motion keeps a stable rear root and shape");
            Check(!anchor.Basis.IsEqualApprox(Basis.Identity),species+" zombie tail has secondary motion");
            rig.ResetPose();
            Check(anchor.Basis.IsEqualApprox(Basis.Identity)&&tail.Transform.IsEqualApprox(home),species+" zombie corpse reset restores tail home");
            rig.Free();
        }
    }

    private void CheckTailSurface(MeshInstance3D tail,string species)
    {
        var mesh=tail.Mesh;
        Check(mesh.GetSurfaceCount()==(AppearanceCatalog.IsBird(species)?1:2),species+" preserves tail palette bands");
        Check(mesh.GetAabb().Size.Z>(AppearanceCatalog.IsBird(species)?.3f:.8f),species+" tail keeps a visible rearward silhouette");
        for(int surface=0;surface<mesh.GetSurfaceCount();surface++)
        {
            var arrays=mesh.SurfaceGetArrays(surface);
            var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals=arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var indices=arrays[(int)Mesh.ArrayType.Index].VariantType==Variant.Type.PackedInt32Array?arrays[(int)Mesh.ArrayType.Index].AsInt32Array():Array.Empty<int>();
            int count=indices.Length>0?indices.Length:vertices.Length;
            for(int i=0;i<count;i+=3)
            {
                int a=indices.Length>0?indices[i]:i,b=indices.Length>0?indices[i+1]:i+1,c=indices.Length>0?indices[i+2]:i+2;
                var face=(vertices[b]-vertices[a]).Cross(vertices[c]-vertices[a]);
                // Clockwise front faces must oppose the mathematical cross product.
                Check(face.LengthSquared()>1e-18f&&face.Normalized().Dot((normals[a]+normals[b]+normals[c]).Normalized())<-.01f,species+" tail triangle has consistent outward facing");
            }
        }
    }

    private async Task CheckTimingAndCancellation()
    {
        foreach(var interrupt in new[]{"none","blocked","stun","switch","throw","death","out_of_range"})
        {
            var stage=new Stage{CharacterOverride="goose",DirectorEnabled=false};AddChild(stage);
            var player=stage.Player;player.ManualControl=true;player.SetPhysicsProcess(false);
            player.GlobalPosition=new Vector3(4,0,0);
            player.SetControls(Vector2.Zero,new Vector3(9,1,0),false);player.Rig.Face(Vector3.Right);
            var victim=stage.SpawnEnemy<Enemy>(MonsterDatabase.Get("plush"),new Vector3(5.3f,0,0),awake:false);
            victim.SetPhysicsProcess(false);
            // SpawnEnemy joins the crowd at the next Stage physics tick. Physics also
            // needs a frame to register colliders for the impact's line-of-sight test.
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
            Check(stage.Enemies.Contains(victim),"melee fixture is registered in the live crowd");
            float health=victim.Health;
            var weapons=player.Weapons;
            player.Loadout.AddThrowable(ArmoryCatalog.Throwable("frag"));
            weapons.Tick(.016f,false,true,true,false);
            Check(victim.Health==health,"melee cannot damage during initial windup");
            weapons.Tick(.10f,false,false,false,false);
            Check(victim.Health==health,"melee keeps its hit queued before impact");
            if(interrupt=="switch")weapons.Pickup(ArmoryCatalog.Weapon("m4"),null);
            if(interrupt=="throw")Check(weapons.BeginThrowAim(),"throw aim fixture has a throwable");
            if(interrupt=="death")player.TakeDamage(9999,Vector3.Left);
            if(interrupt=="stun")player.Launch(Vector3.Zero,.5f);
            if(interrupt=="out_of_range")victim.GlobalPosition+=Vector3.Right*10;
            weapons.Tick(.10f,false,false,false,interrupt=="blocked");
            Check(interrupt=="none"?victim.Health<health:victim.Health==health,interrupt+" resolves or cancels queued melee damage");
            float after=victim.Health;
            weapons.Tick(.30f,false,false,false,false);
            Check(victim.Health==after,"melee impact is emitted once");
            stage.Free();
        }
    }
}
