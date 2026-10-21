using Godot;
using System;
using System.Linq;

namespace HordeShooter.Tests;

public partial class CompanionModelCheck : Node3D
{
    private int _checks;
    private void Check(bool ok,string message)
    {
        if(!ok) throw new Exception(message);
        _checks++;GD.Print("PASS: "+message);
    }
    public override void _Ready()
    {
        try
        {
            foreach(var quality in Enum.GetValues<PetModelQuality>())
            foreach(var id in DroneModel.BodyIds)
            {
                var drone=DroneModel.Create(id,quality);AddChild(drone);
                Check(drone.Quality==quality && drone.Equipment==null && drone.EquipmentSocket.GetChildCount()==0,id+" bare requested tier");
                Check(drone.EquipmentSocket.Position.IsEqualApprox(new Vector3(0,-.22f,0)) && drone.EquipmentSocket.Basis.IsEqualApprox(Basis.Identity),id+" common belly mount");
                var bounds=PetModelCheck.Bounds(drone,drone);
                foreach(var module in DroneModel.EquipmentIds.Concat(new[]{"light"}))
                {
                    var old=drone.Equipment;drone.SetEquipment(module);
                    Check(drone.EquipmentSocket.GetChildCount()==1 && drone.Equipment!.Transform.IsEqualApprox(Transform3D.Identity),id+"/"+module+" no offsets");
                    Check(old==null || !GodotObject.IsInstanceValid(old),id+" previous module released");
                    var moduleBounds=PetModelCheck.Bounds(drone.Equipment!,drone);
                    Check(Math.Abs(moduleBounds.End.Y+.22f)<.005f,id+" module top contacts belly");
                    Check(drone.Equipment!.FindChild("Muzzle",true,false) is Node3D,id+" module provides output marker");
                }
                var kept=drone.Equipment;
                try {drone.SetEquipment("missing");throw new Exception("bad equipment accepted");}catch(ArgumentException){}
                Check(drone.Equipment==kept,id+" invalid equipment preserves current module");
                drone.Position=new Vector3(2,3,-1);drone.RotationDegrees=new Vector3(0,30,0);drone.Pose(.24f,1);
                var at=drone.GlobalTransform;var socket=drone.EquipmentSocket.GlobalTransform;
                ((Node3D)kept!.FindChild("Yaw",true,false)).RotationDegrees=new Vector3(0,17,0);
                var muzzle=((Node3D)kept.FindChild("Muzzle",true,false)).GlobalPosition;
                drone.SetQuality(quality==PetModelQuality.High?PetModelQuality.Low:PetModelQuality.High);
                Check(drone.Equipment==kept && drone.GlobalTransform.IsEqualApprox(at) && drone.EquipmentSocket.GlobalTransform.IsEqualApprox(socket),id+" tier switch retains instance, placement and hover pose");
                Check(((Node3D)kept.FindChild("Muzzle",true,false)).GlobalPosition.DistanceTo(muzzle)<.001f,id+" aimed module survives tier switch");
                drone.Pose(0,0);drone.SetEquipment(null);
                Check(drone.Equipment==null && drone.EquipmentSocket.GetChildCount()==0,id+" unequip leaves bare chassis");
                Check((PetModelCheck.Bounds(drone,drone).Size-bounds.Size).Length()<.10f,id+" low silhouette preserved");
                var retained=drone.Quality;
                try {drone.SetQuality((PetModelQuality)8);throw new Exception("bad tier accepted");}catch(ArgumentException){}
                Check(drone.Quality==retained,id+" invalid tier keeps model");drone.Free();
            }
            foreach(var preset in SidekickModel.PresetIds)
            {
                var sidekick=SidekickModel.Create(preset,PetModelQuality.High);AddChild(sidekick);
                var source=CharacterCatalog.Player(sidekick.Species);
                Check(sidekick.Rig.Definition==source && sidekick.Rig.Model.Scale.IsEqualApprox(Vector3.One*source.ModelScale),preset+" reuses original definition without shrinking source");
                Check(Mathf.IsEqualApprox(sidekick.VisualScale,.6f) && Mathf.IsEqualApprox(sidekick.ColliderHeight,source.ColliderHeight*.6f),preset+" scaled collider reference");
                Check(sidekick.Rig.WeaponArmsVisible && sidekick.Rig.GripConnectionError<.002f,preset+" weapon grips align");
                var original=CharacterRig.Create(source);AddChild(original);original.HideBuiltInWeapon();
                var full=WeaponVisual.Build(ArmoryCatalog.Weapon(sidekick.WeaponId),original.HandColor);original.Weapon.AddChild(full.Root);original.BindWeaponGrip(full);
                Check((sidekick.ToLocal(sidekick.Weapon.Muzzles[0].GlobalPosition)-original.ToLocal(full.Muzzles[0].GlobalPosition)*.6f).Length()<.002f,preset+" muzzle scales with body and weapon");
                original.Free();
                sidekick.Animate(.016f,1);var weapon=sidekick.Weapon;
                sidekick.SetQuality(PetModelQuality.Low);
                Check(sidekick.Weapon==weapon && sidekick.Rig.GripConnectionError<.002f,preset+" quality preserves weapon and grip");
                Check(sidekick.FindChildren("*",nameof(MeshInstance3D),true,false).Cast<MeshInstance3D>().All(m=>Mathf.IsEqualApprox(m.LodBias,.5f)),preset+" low uses source mesh LOD bias");
                sidekick.Free();
            }
            var first=SidekickModel.Create("cadet",PetModelQuality.High);AddChild(first);
            var second=SidekickModel.Create("cadet",PetModelQuality.High);AddChild(second);
            var originalRig=CharacterRig.Create(CharacterCatalog.Player("goose"));AddChild(originalRig);
            var sourceHat=originalRig.ChannelColor("hat");var presetHat=second.Rig.ChannelColor("hat");
            first.Rig.ApplyAppearance(new AppearanceProfile{HatColor="#ff00ff"});
            Check(second.Rig.ChannelColor("hat").IsEqualApprox(presetHat),"changing one sidekick does not recolor another instance");
            var laterRig=CharacterRig.Create(CharacterCatalog.Player("goose"));AddChild(laterRig);
            Check(originalRig.ChannelColor("hat").IsEqualApprox(sourceHat) && laterRig.ChannelColor("hat").IsEqualApprox(sourceHat),"sidekick appearance does not mutate existing or future player materials");
            GameSettings.PetQuality=PetModelQuality.Low;
            Check(first.Quality==PetModelQuality.High && second.Quality==PetModelQuality.High,"explicit quality override ignores global setting");
            first.Free();second.Free();originalRig.Free();laterRig.Free();
            var live=DroneModel.Create("bee");var buddy=SidekickModel.Create("cadet");
            GameSettings.PetQuality=PetModelQuality.High;AddChild(live);AddChild(buddy);
            Check(live.Quality==PetModelQuality.High && buddy.Quality==PetModelQuality.High,"tree entry catches settings changed since creation");
            live.SetEquipment("support");var payload=live.Equipment;
            GameSettings.PetQuality=PetModelQuality.Low;
            Check(live.Quality==PetModelQuality.Low && buddy.Quality==PetModelQuality.Low && live.Equipment==payload,"shared setting updates both companion types");
            live.Free();buddy.Free();GameSettings.PetQuality=PetModelQuality.High;
            try{DroneModel.Create("missing");throw new Exception("bad body accepted");}catch(ArgumentException){}
            try{SidekickModel.Create("missing");throw new Exception("bad preset accepted");}catch(ArgumentException){}
            GD.Print($"COMPANION_MODEL_PASS {_checks}");QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
