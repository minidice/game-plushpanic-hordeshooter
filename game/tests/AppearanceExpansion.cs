using Godot;
using System;
using System.Linq;

namespace HordeShooter.Tests;
public partial class AppearanceExpansion : Node
{
    public override void _Ready()
    {
        try
        {
            foreach(var id in new[]{"goose","duck","raccoon","redpanda"})
            {
                var def=CharacterCatalog.Player(id);
                Require(def.Id==id&&def.Model!=null,id+" playable GLB registered");
                var first=CharacterRig.Create(def);var second=CharacterRig.Create(def);AddChild(first);AddChild(second);
                var original=second.ChannelColor("body");
                var profile=new AppearanceProfile{Body="#70b9e8",Beak="#dd704a",Hat=2,Vest=false,Pack=false};
                first.ApplyAppearance(profile);
                var skin=first.Model.FindChild(id+"_torso_mesh",true,false) as MeshInstance3D;
                Require(skin!=null && skin.GetActiveMaterial(0) is StandardMaterial3D{VertexColorUseAsAlbedo:true},id+" skin markings use imported vertex colors");
                Require(first.ChannelColor("body").IsEqualApprox(new Color(profile.Body)*original),id+" body tint applied");
                Require(second.ChannelColor("body").IsEqualApprox(original),id+" shared model material remains unchanged");
                Require(first.Model.FindChild(id+"_beret",true,false) is Node3D{Visible:true},id+" beret available");
                Require(first.Model.FindChild(id+"_vest",true,false) is Node3D{Visible:false},id+" vest hidden");
                var colors=new AppearanceProfile{Wing="#397c71",Tail="#bad4aa",Feet="#d49b51",Eyes="#273e4b",Goggles=false,Headset=true,VestStyle=1};
                first.ApplyAppearance(colors);
                Require(first.ChannelColor("wing").IsEqualApprox(new Color(colors.Wing))&&first.HandColor.IsEqualApprox(new Color(colors.Wing)),id+" wing and gripping hand share selected color");
                Require(first.ChannelColor("tail").IsEqualApprox(second.ChannelColor("tail")*new Color(colors.Tail)),id+" tail tint preserves original markings");
                Require(first.ChannelColor("feet").IsEqualApprox(new Color(colors.Feet))&&first.ChannelColor("eyes").IsEqualApprox(new Color(colors.Eyes)),id+" feet and eyes independently colored");
                Require(first.Model.FindChild(id+"_goggles",true,false) is Node3D{Visible:false}&&first.Model.FindChild(id+"_headset",true,false) is Node3D{Visible:true},id+" goggles and headset independently selected");
                Require(first.Model.FindChild(id+"_vest",true,false) is Node3D{Visible:false}&&first.Model.FindChild(id+"_vestPadded",true,false) is Node3D{Visible:true},id+" padded vest replaces pouch vest");
                first.Animate(.016f,0,0,0,Vector3.Zero);first.DodgePose(.5f);
                Require(Math.Abs(first.Pose.Rotation.X)<1.1f&&first.Pose.Position.Y>.15f,id+" dodge is a low leap without full rotation");
                first.DodgePose(1);Require(first.Pose.Transform.IsEqualApprox(Transform3D.Identity),id+" landing restores pose");
                first.Free();second.Free();
            }
            foreach(var id in new[]{"m1911","glock17"})
            {
                var def=ArmoryCatalog.Weapon(id).DualVariant!;
                var built=WeaponVisual.Build(def,null);AddChild(built.Root);
                Require(built.Muzzles.Count==2 && built.Root.GetChildCount()==2,id+" dual models have two independent gun roots");
                Require(built.Muzzles.All(m=>m.Name=="Muzzle"&&m.GetParent()!=built.Root)&&built.Muzzles[0].GetParent()!=built.Muzzles[1].GetParent(),id+" imported dual muzzles remain independent");
                Require(built.Root.GetChild<Node3D>(0).Position.X<0&&built.Root.GetChild<Node3D>(1).Position.X>0,id+" dual guns use separate hand offsets");
                built.Root.Free();
            }
            var path="user://appearance-test.cfg";
            var p=new AppearanceProfile{Body="#abcdee",Hat=0,Vest=false,Wing="#397c71",Tail="#bad4aa",Feet="#d49b51",Eyes="#273e4b",Goggles=false,Headset=true,VestStyle=1};
            AppearanceStore.SaveProfile("goose",p,path);
            var loaded=AppearanceStore.ReadProfile("goose",path);
            Require(loaded.Body==p.Body&&loaded.Hat==0&&!loaded.Vest,"appearance save/load round trip");
            Require(loaded.Wing==p.Wing&&loaded.Tail==p.Tail&&loaded.Feet==p.Feet&&loaded.Eyes==p.Eyes&&!loaded.Goggles&&loaded.Headset&&loaded.VestStyle==1,"extended customization save/load round trip");
            Godot.DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            GD.Print("APPEARANCE_EXPANSION_PASS");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
    private static void Require(bool ok,string message){if(!ok)throw new Exception(message);GD.Print("PASS: "+message);}
}
