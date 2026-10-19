using Godot;
using System;

namespace HordeShooter.Tests;
public partial class ModularCustomization : Node
{
    private int _checks;
    private void Check(bool condition,string message)
    {if(!condition)throw new Exception(message);_checks++;GD.Print("PASS: "+message);}
    public override void _Ready()
    {
        try
        {
            foreach(var species in new[]{"goose","duck","raccoon","redpanda"})
            {
                var def=CharacterCatalog.Player(species);var rig=CharacterRig.Create(def);AddChild(rig);
                float radius=def.ColliderRadius,height=def.ColliderHeight;
                var p=AppearanceCatalog.CreatePreset(species,1);p.FaceStyle=2;p.BeakStyle=2;p.HairStyle=2;p.HairColor="#be684d";
                p.BodyWidth=1.12f;p.BodyHeight=1.10f;p.WingSize=1.25f;p.FeetSize=1.25f;p.TailSize=1.30f;p.EarSize=1.25f;
                rig.ApplyAppearance(p);
                Node3D Part(string suffix)=>(Node3D)(rig.Model.FindChild(species+"_"+suffix,true,false)??throw new Exception("missing "+suffix));
                Check(Part("leftEye").Scale.Y<Part("leftEye").Scale.X,species+" sleepy eye shape applied");
                Check(Part("crestSwept").Visible&&!Part("crestShort").Visible,species+" selected hair only");
                Check(rig.ChannelColor("hair").IsEqualApprox(new Color(p.HairColor)),species+" hair independently tinted");
                var eye=Part("leftEye").Transform;var tail=Part("tail").Transform;var scale=rig.Model.Scale;
                rig.ApplyAppearance(p);rig.Animate(.016f,1,1,0,Vector3.Zero);rig.DodgePose(.5f);
                Check(Part("leftEye").Transform.IsEqualApprox(eye)&&Part("tail").Transform.IsEqualApprox(tail)&&rig.Model.Scale.IsEqualApprox(scale),species+" shape edits survive animation without compounded scaling");
                Check(def.ColliderRadius==radius&&def.ColliderHeight==height,species+" cosmetic proportions retain collision baseline");
                var gun=WeaponVisual.Build(ArmoryCatalog.Weapon("m4"),rig.HandColor);rig.Weapon.AddChild(gun.Root);rig.BindWeaponGrip(gun);
                rig.SetLocomotionPose(.2f,1,false);rig.Animate(.016f,1,.5f,0,Vector3.Zero);
                Check(rig.GripConnectionError<.002f,species+" maximum proportions keep running weapon grip connected");
                rig.DodgePose(.5f);Check(rig.GripConnectionError<.002f,species+" maximum proportions retain leap grip");
                p.Hat=1;rig.ApplyAppearance(p);
                Check(!Part("crestSwept").Visible&&!Part("crestShort").Visible,species+" hat suppresses hair clipping");
                Check(!Part("leftArm").Visible&&!Part("rightArm").Visible,species+" editing shapes preserves equipped wing visibility");
                rig.SetPreviewVisibility(false,false);
                Check(Part("leftArm").Visible&&Part("rightArm").Visible&&!rig.WeaponArmsVisible&&!Part("helmet").Visible&&Part("crestSwept").Visible,species+" uncovered preview has arms and hair");
                Check(p.Hat==1&&p.HairStyle==2,species+" preview does not mutate selected equipment");
                rig.SetPreviewVisibility(true,true);
                Check(!Part("leftArm").Visible&&rig.WeaponArmsVisible&&Part("helmet").Visible&&!Part("crestSwept").Visible,species+" preview restores equipment and dynamic grips");
                rig.ApplyAppearance(new AppearanceProfile());
                Check(rig.Model.Scale.IsEqualApprox(Vector3.One*def.ModelScale),species+" resetting profile restores baseline proportions");
                rig.Free();
            }
            const string path="user://modular-custom-test.cfg";
            var original=AppearanceCatalog.CreatePreset("goose",1);original.HairColor="#b6584a";original.HairStyle=2;
            AppearanceStore.SaveProfile("goose",original,path);
            AppearanceStore.SaveProfile("raccoon",AppearanceCatalog.CreatePreset("raccoon",2),path);
            var read=AppearanceStore.ReadProfile("goose",path);
            foreach(var option in AppearanceCatalog.Scales)Check(Mathf.IsEqualApprox(read.ScaleFor(option.Key),original.ScaleFor(option.Key)),option.Key+" stored per species");
            Check(read.HairStyle==2&&read.HairColor==original.HairColor&&read.BeakStyle==original.BeakStyle,"part styles and hair color persist");
            Check(AppearanceStore.ReadProfile("raccoon",path).FaceStyle==1,"other species retains independent preset");
            var legacy=new ConfigFile();legacy.SetValue("duck","body","#aabbcc");legacy.Save(path);
            var migrated=AppearanceStore.ReadProfile("duck",path);
            Check(migrated.Body=="#aabbcc"&&migrated.BodyWidth==1&&migrated.HairStyle==0,"old color-only profiles retain defaults for new parts");
            var invalid=new AppearanceProfile{BodyWidth=float.NaN,EyeSize=99,FeetSize=-3,HairStyle=99};invalid.Normalize();
            Check(invalid.BodyWidth==1&&invalid.EyeSize==1.45f&&invalid.FeetSize==.8f&&invalid.HairStyle==2,"out-of-range profiles clamped to safe cosmetic limits");
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
            GD.Print("MODULAR_CUSTOMIZATION_PASS "+_checks);QuitHygiene.Collect();GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
