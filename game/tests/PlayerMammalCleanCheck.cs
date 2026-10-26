using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Player customization and rigid hand regression, plus rendered Stage 2 movement/fire.</summary>
public abstract partial class PlayerMammalCleanCheck : Node
{
    protected abstract string Species { get; }
    private string OutputRoot => "res://output/"+Species+"-clean";
    private int _checks;
    private Stage? _stage;
    private void Check(bool ok,string label) { if(!ok)throw new Exception(label);_checks++; }
    private async Task Frames(int count) { for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
    private static Vector3[] Vertices(MeshInstance3D mesh)=>Enumerable.Range(0,mesh.Mesh.GetSurfaceCount()).SelectMany(i=>mesh.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex].AsVector3Array()).ToArray();
    public override async void _Ready()
    {
        int exit=0;
        try {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(OutputRoot));
            foreach(var weapon in new[]{"m4","m1911","r870","m249","mortar","alabong","frying_pan"}) Verify(weapon);
            if(!OS.GetCmdlineUserArgs().Contains("--rig-only"))await Play();
            GD.Print(Species.ToUpperInvariant()+"_CLEAN_PASS "+_checks);
        } catch(Exception e) {GD.PushError(Species.ToUpperInvariant()+"_CLEAN_FAIL "+e);exit=1;}
        if(IsInstanceValid(_stage))_stage!.Free();
        QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private void Verify(string weapon)
    {
        var def=CharacterCatalog.Player(Species);var rig=CharacterRig.Create(def);AddChild(rig);
        try {
            Node3D Part(string suffix)=>(Node3D)(rig.Model.FindChild(Species+"_"+suffix,true,false)??throw new Exception("missing "+Species+"_"+suffix));
            Check(rig.Model.FindChild(Species+"_leftPaw_mesh",true,false) is MeshInstance3D,"new authored rigid palm exists");
            var profile=new AppearanceProfile{Hat=0,Vest=false,Pack=false,Goggles=false};rig.ApplyAppearance(profile);
            var ear=(MeshInstance3D)Part("leftEar_mesh");var earMesh=ear.Mesh;
            var snout=Part("snout");var snoutHome=snout.Transform;
            var gun=WeaponVisual.Build(ArmoryCatalog.Weapon(weapon),rig.HandColor);rig.HideBuiltInWeapon();rig.Weapon.AddChild(gun.Root);rig.BindWeaponGrip(gun);
            var paws=new[]{(MeshInstance3D)rig.Model.FindChild("RightForearm",true,false),(MeshInstance3D)rig.Model.FindChild("LeftForearm",true,false)};
            Check(paws.All(p=>p is CharacterLionPaw),"player uses rigid hand and separate forearm");
            float size=1;
            void Sample() {
                Check(rig.GripConnectionError<.002f,weapon+" actual grip contact");
                for(int k=0;k<2;k++) {
                    var paw=paws[k];var source=(MeshInstance3D)Part(k==0?"rightPaw_mesh":"leftPaw_mesh");
                    Check(paw.Mesh==source.Mesh&&!source.IsVisibleInTree(),"single authored hand surface");
                    var points=Vertices(paw);
                    for(int i=1;i<points.Length;i+=Math.Max(1,points.Length/10)) {
                        float actual=(paw.Transform*points[i]).DistanceTo(paw.Transform*points[0]);
                        Check(Mathf.Abs(actual-points[i].DistanceTo(points[0])*size)<.0002f,"hand vertex distances remain rigid");
                    }
                    Check(paw.Transform.Basis.IsFinite()&&paw.GetNode<Node3D>("Forearm").Transform.Basis.IsFinite(),"finite hand/forearm pose");
                    var forearm=paw.GetNode<MeshInstance3D>("Forearm");
                    // Inspect real vertices: a correct grip marker alone misses a stretched shoulder spike.
                    var shoulder=rig.Model.ToLocal(Part(k==0?"rightArm":"leftArm").GetParentNode3D().ToGlobal(new Vector3(k==0?.440f:-.440f,1.125f,.158f)));
                    var wrist=rig.Model.ToLocal(paw.GetNode<Node3D>("Wrist").GlobalPosition);
                    var elbow=rig.Model.ToLocal(paw.GetNode<Node3D>("Elbow").GlobalPosition);
                    // Each real surface stays near its own anatomical endpoints; the whole arm now bends.
                    foreach(var (section,from,to) in new[]{(paw.GetNode<MeshInstance3D>("UpperArm"),shoulder,elbow),(forearm,elbow,wrist)}) {
                        var direction=to-from;float length=direction.Length();
                        var distances=Vertices(section).Select(v=>(rig.Model.ToLocal(section.ToGlobal(v))-from).Dot(direction.Normalized())).ToArray();
                        Check(distances.Min()>-.06f*size&&distances.Max()<length+.06f*size,$"arm section stays between its joints: {weapon}/{k} size={size} min={distances.Min()} max={distances.Max()} length={length}");
                    }
                    Check(paw.GetActiveMaterial(0)==source.GetActiveMaterial(0),"owned source material follows live customization");
                }
            }
            foreach(float scale in new[]{.8f,1.25f,1f}) {
                size=scale;profile.WingSize=scale;profile.FeetSize=scale;
                profile.BodyWidth=scale<1?.88f:1.12f;profile.BodyHeight=scale<1?1.1f:.9f;
                rig.ApplyAppearance(profile);
                for(int i=0;i<5;i++){rig.SetLocomotionPose(.05f,1,i%2==0);rig.Animate(.016f,1,.5f,.2f,new Vector3(2,0,1));Sample();}
                rig.DodgePose(.5f);Sample();
            }
            profile.BodySecondary="#327a9c";profile.BodyMarking="#b46d34";rig.ApplyAppearance(profile);
            // Raccoon's dark hands follow its eye-mask tone; red panda's hands follow the dark belly tone.
            string handTone=Species=="raccoon"?profile.BodyMarking:profile.BodySecondary;
            Check(paws[0].GetActiveMaterial(0) is StandardMaterial3D mat&&mat.AlbedoColor.IsEqualApprox(new Color(handTone)),"equipped hand recolors with species palette tone");
            rig.ResetPose();size=1;profile=new AppearanceProfile{Hat=0};rig.ApplyAppearance(profile);Sample();
            foreach(int style in new[]{1,2,3,0}) {
                profile.EarStyle=style;profile.EarSize=style%2==0?.75f:1.25f;profile.EarAngle=style%2==0?-20:40;rig.ApplyAppearance(profile);
                Check(rig.EarStyleBuilt==style&&rig.EarPivots.All(p=>p.Transform.Basis.IsFinite()),"ear styles/size/angle remain supported");
            }
            Check(ear.Mesh==earMesh,"default restores authored cupped ear");
            foreach(int style in new[]{1,2,0}) {profile.BeakStyle=style;profile.BeakSize=style==0?1:.75f;profile.FaceStyle=style;rig.ApplyAppearance(profile);}
            Check(snout.Transform.IsEqualApprox(snoutHome),"snout customization returns to new authored baseline");
            foreach(int style in new[]{1,2,3,4,0}) {profile.TailStyle=style;profile.TailSize=style%2==0?.65f:1.3f;rig.ApplyAppearance(profile);Check(Part("tail").Transform.Basis.IsFinite(),"tail style/size finite");}
            var state=new WeaponState(ArmoryCatalog.Weapon(weapon),0,30);state.StartReload();
            for(int i=0;i<12;i++){state.Tick(.15f,false);rig.SetReloadPose(.15f,state);rig.Animate(.016f,.4f,0,0,Vector3.Zero);Sample();}
            rig.ResetPose();rig.ZiplinePose(false,.5f,.8f);Check(paws.All(p=>p.IsVisibleInTree()&&p.Transform.Basis.IsFinite()),"hanging shows finite palms");
            rig.ZiplinePose(true,.6f,.4f);rig.EndZiplinePose();rig.ResetPose();Sample();
            foreach(int hat in new[]{1,2,0}) {profile.Hat=hat;rig.ApplyAppearance(profile);Check(rig.EarPivots.All(p=>p.Transform.Basis.IsFinite()),"hat folds ears without invalid pose");}
            rig.SetPreviewVisibility(false,false);Check(!rig.WeaponArmsVisible&&Part("leftPaw_mesh").IsVisibleInTree(),"unarmed preview restores resting hands");
            rig.SetPreviewVisibility(true,true);Check(rig.WeaponArmsVisible&&!Part("leftPaw_mesh").IsVisibleInTree(),"equipped preview restores single active hands");
            var status=StatusVisualController.Attach(rig);status.SetStates(new[]{new StatusVisualState("burn",3)});
            foreach(var mesh in paws.SelectMany(p=>new[]{p,p.GetNode<MeshInstance3D>("Forearm"),p.GetNode<MeshInstance3D>("UpperArm"),p.GetNode<MeshInstance3D>("RestArm")}))Check(mesh.GetNode<MeshInstance3D>("StatusSurface").Mesh==mesh.Mesh,"status follows true hand and forearm");
            rig.FlashDeath();Check(paws.All(p=>p.MaterialOverlay!=null),"hit flash covers palms");
            rig.ShowAs(VisionSystem.Visibility.Hidden);Check(paws.All(p=>!p.IsVisibleInTree()),"hidden character has no orphan hands");
            rig.ShowAs(VisionSystem.Visibility.Visible);rig.ApplyAppearance(new AppearanceProfile());
            Check(def.ColliderRadius==.48f&&def.ColliderHeight==2.06f&&def.MoveSpeed==4.4f,"cosmetic changes keep collision/speed");
        } finally {rig.Free();}
    }
    private async Task Shot(string name)
    {
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        using var image=GetViewport().GetTexture().GetImage();Check(image.SavePng(ProjectSettings.GlobalizePath(OutputRoot+"/"+name+".png"))==Error.Ok,"saved "+name);
    }
    private async Task Play()
    {
        _stage=new Stage{MapOverride=new LayoutMap(),CharacterOverride=Species,DirectorEnabled=false,ModeOverride=GameMode.Scenario,SquadOverride=0,ArrivalOverride=false,NextStageOverride=false};AddChild(_stage);await Frames(5);
        var player=_stage.Player;player.ManualControl=true;player.Invulnerable=true;
        var start=new Vector3(11,_stage.Tiers.GroundY(new Vector3(11,0,-2))+.05f,-2);player.TeleportTo(start);player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"),null);
        player.SetControls(Vector2.Right,new Vector3(20,1,-2),false);await Frames(35);
        Check(player.GlobalPosition.DistanceTo(start)>1,"actual Stage2 controlled movement");await Shot("run-gameplay");
        int shots=player.Weapons.ShotsFired;player.SetControls(Vector2.Zero,player.GlobalPosition+new Vector3(8,1,0),true);await Frames(20);
        Check(player.Weapons.ShotsFired>shots,"actual player weapon fire");await Shot("fire-gameplay");player.SetControls(Vector2.Zero,Vector3.Zero,false);
        Check(_stage.Ladders.Count>0,"Stage2 ladder available");
        var ladder=_stage.Ladders[0];player.TeleportTo(ladder.Bottom+Vector3.Up*.05f);
        // Teleport spans the map; frame the ladder directly instead of capturing the camera's travel.
        _stage.Cam.SetProcess(false);
        var ladderFocus=ladder.Bottom.Lerp(ladder.Top,.5f);
        _stage.Cam.GlobalPosition=ladderFocus+new Vector3(6,4,8);_stage.Cam.LookAt(ladderFocus);_stage.Cam.Size=8;
        Check(player.ClimbLadder(ladder,true),"actual "+Species+" climbs ladder");await Frames(12);
        Check(player.Rig.WeaponArmsVisible&&player.Rig.GripConnectionError<.002f,"climbing preserves player hand connection");await Shot("ladder-gameplay");
        for(int i=0;i<600&&player.OnLadder;i++)await Frames(1);
        Check(!player.OnLadder,"ladder traversal completes");player.TeleportTo(start);await Frames(2);
        player.SetPhysicsProcess(false);_stage.Cam.SetProcess(false);
        foreach(bool gear in new[]{false,true}) {
            var focus=player.GlobalPosition+Vector3.Up;_stage.Cam.GlobalPosition=focus+new Vector3(2.5f,1.8f,4);_stage.Cam.LookAt(focus);_stage.Cam.Size=3.8f;
            player.Rig.ApplyAppearance(new AppearanceProfile{Hat=gear?1:0,Vest=gear,Pack=gear,Goggles=false});player.Rig.Face(_stage.Cam.GlobalPosition-player.GlobalPosition);player.Rig.SetPreviewVisibility(gear,gear);
            await Frames(3);await Shot(gear?"equipped-close":"base-close");
        }
    }
}
