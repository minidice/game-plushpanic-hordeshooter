using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Duck model, customization and rendered Stage 2 motion regression.</summary>
public partial class DuckCleanCheck : Node
{
    private int _checks;
    private Stage? _stage;
    private const string OutputRoot = "res://output/duck-clean";
    private void Check(bool ok,string label) { if(!ok)throw new Exception(label);_checks++; }
    private async Task Frames(int count) { for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
    private static Vector3[] Vertices(MeshInstance3D mesh)=>Enumerable.Range(0,mesh.Mesh.GetSurfaceCount()).SelectMany(i=>mesh.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex].AsVector3Array()).ToArray();

    public override async void _Ready()
    {
        int exit=0;
        try {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(OutputRoot));
            VerifyRig();
            if(!OS.GetCmdlineUserArgs().Contains("--rig-only"))await Play();
            GD.Print("DUCK_CLEAN_PASS "+_checks);
        } catch(Exception e) { GD.PushError("DUCK_CLEAN_FAIL "+e);exit=1; }
        if(IsInstanceValid(_stage))_stage!.Free();
        QuitHygiene.Collect();GetTree().Quit(exit);
    }

    private void VerifyRig()
    {
        var definition=CharacterCatalog.Player("duck");
        Check(definition.Model?.ResourcePath.EndsWith("duck-clean.glb")==true,"clean model connected");
        Check(definition.ColliderRadius==.48f&&definition.ColliderHeight==2.06f&&definition.MoveSpeed==4.4f,"gameplay values unchanged");
        var rig=CharacterRig.Create(definition);AddChild(rig);
        try {
            MeshInstance3D Part(string suffix)=>(MeshInstance3D)(rig.Model.FindChild("duck_"+suffix,true,false)??throw new Exception("missing "+suffix));
            var torso=Part("torso_mesh");var beak=Part("beak_mesh");
            var torsoVertices=Vertices(torso);var beakVertices=Vertices(beak);
            Check(torsoVertices.Length>3000&&beakVertices.Length>2000,"authored surfaces present");
            Check(Part("leftFoot_mesh").Mesh.GetSurfaceCount()==2,"webbed foot palette channels");
            var defaultProfile=new AppearanceProfile{Hat=0,Vest=false,Pack=false,Goggles=false};
            rig.ApplyAppearance(defaultProfile);
            var home=beak.Transform;
            foreach(int style in new[]{1,2,0}) {
                defaultProfile.BeakStyle=style;defaultProfile.FaceStyle=style;
                defaultProfile.BeakSize=style==0?1:.75f;
                defaultProfile.EyeSize=style==0?1:.7f;
                rig.ApplyAppearance(defaultProfile);
                Check(beak.Transform.Basis.IsFinite(),"bill style finite");
            }
            Check(beak.Transform.IsEqualApprox(home),"default bill restores authored pose");
            foreach(int style in new[]{1,2,3,4,0}) {
                defaultProfile.TailStyle=style;defaultProfile.TailSize=style==0?1:1.3f;
                rig.ApplyAppearance(defaultProfile);
                Check(Part("tail_mesh").Transform.Basis.IsFinite(),"tail style finite");
            }
            foreach(float size in new[]{.8f,1.25f,1f}) {
                defaultProfile.WingSize=size;defaultProfile.FeetSize=size;
                defaultProfile.BodyWidth=size<1?.88f:1.12f;defaultProfile.BodyHeight=size<1?1.1f:.9f;
                rig.ApplyAppearance(defaultProfile);
                Check(Part("leftArm_mesh").Transform.Basis.IsFinite()&&Part("leftFoot_mesh").Transform.Basis.IsFinite(),"size extremes finite");
            }
            rig.ApplyAppearance(new AppearanceProfile{Body="#3576a0",Beak="#e7a850",Feet="#315f71",Hat=1,Vest=true,Pack=true,Goggles=true});
            Check(rig.Model.FindChild("duck_helmet",true,false)!=null&&rig.Model.FindChild("duck_goggles",true,false)!=null,"player gear retained");
            foreach(string weapon in new[]{"m4","m1911","r870","m249","mortar","alabong","frying_pan"}) {
                var built=WeaponVisual.Build(ArmoryCatalog.Weapon(weapon),rig.HandColor);
                rig.HideBuiltInWeapon();rig.Weapon.AddChild(built.Root);rig.BindWeaponGrip(built);
                Check(rig.WeaponArmsVisible&&rig.GripConnectionError<.002f,weapon+" wing tip grip");
                for(int i=0;i<4;i++) { rig.SetLocomotionPose(.05f,1,i%2==0);rig.Animate(.016f,1,.5f,.2f,new Vector3(2,0,1));Check(rig.GripConnectionError<.002f,weapon+" moving grip"); }
                built.Root.Free();
            }
            rig.ApplyAppearance(new AppearanceProfile());
            Check(rig.Model.FindChild("duck_leftArm_mesh",true,false)!=null,"resting wing retained");
        } finally {rig.Free();}
    }

    private async Task Shot(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath(OutputRoot+"/"+name+".png"))==Error.Ok,"saved "+name);
    }

    private async Task Play()
    {
        _stage=new Stage{MapOverride=new LayoutMap(),CharacterOverride="duck",DirectorEnabled=false,ModeOverride=GameMode.Scenario,SquadOverride=0,ArrivalOverride=false,NextStageOverride=false};AddChild(_stage);await Frames(5);
        var player=_stage.Player;player.ManualControl=true;player.Invulnerable=true;
        var start=new Vector3(11,_stage.Tiers.GroundY(new Vector3(11,0,-2))+.05f,-2);player.TeleportTo(start);player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"),null);
        player.SetControls(Vector2.Right,new Vector3(20,1,-2),false);await Frames(35);
        Check(player.GlobalPosition.DistanceTo(start)>1,"Stage 2 controlled movement");await Shot("run-gameplay");
        int shots=player.Weapons.ShotsFired;player.SetControls(Vector2.Zero,player.GlobalPosition+new Vector3(8,1,0),true);await Frames(20);
        Check(player.Weapons.ShotsFired>shots,"Stage 2 firing");await Shot("fire-gameplay");player.SetControls(Vector2.Zero,Vector3.Zero,false);
        player.SetPhysicsProcess(false);_stage.Cam.SetProcess(false);
        foreach(bool gear in new[]{false,true}) {
            var focus=player.GlobalPosition+Vector3.Up;_stage.Cam.GlobalPosition=focus+new Vector3(2.5f,1.8f,4);_stage.Cam.LookAt(focus);_stage.Cam.Size=3.8f;
            player.Rig.ApplyAppearance(new AppearanceProfile{Hat=gear?1:0,Vest=gear,Pack=gear,Goggles=gear});player.Rig.Face(_stage.Cam.GlobalPosition-player.GlobalPosition);player.Rig.SetPreviewVisibility(gear,gear);
            await Frames(3);await Shot(gear?"equipped-close":"base-close");
        }
    }
}
