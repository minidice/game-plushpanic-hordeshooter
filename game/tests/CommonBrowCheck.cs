using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
namespace HordeShooter.Tests;
public partial class CommonBrowCheck : Node
{
    private int _checks;
    private void Check(bool ok,string message) { if(!ok) throw new Exception(message); _checks++; }
    private async Task Frames(int n) { for(int i=0;i<n;i++) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    public override async void _Ready()
    {
        int exit=0;
        try { await Run(); GD.Print("COMMON_BROW_CHECK_PASS "+_checks); }
        catch(Exception e) { GD.PushError("COMMON_BROW_CHECK_FAIL "+e); exit=1; }
        foreach(var child in GetChildren()) child.QueueFree(); await Frames(3); QuitHygiene.Collect(); GetTree().Quit(exit);
    }
    private MeshInstance3D[] Brows(CharacterRig rig) => rig.Head!.GetChildren().OfType<MeshInstance3D>().Where(m=>m.Name.ToString().StartsWith("common_brow_")).ToArray();
    private async Task Run()
    {
        var c=CommonBrowCatalog.Current; Check(c.Errors.Count==0,string.Join(";",c.Errors));
        Check(c.Presets.Count==10 && c.Presets.Distinct().Count()==10,"ten distinct presets");
        string text=FileAccess.GetFileAsString(CommonBrowCatalog.DataPath);
        void Bad(Action<JsonObject> mutate) { var j=JsonNode.Parse(text)!.AsObject();mutate(j);Check(CommonBrowCatalog.FromText(j.ToJsonString()).Errors.Count>0,"invalid data rejected"); }
        Bad(j=>j["version"]=2); Bad(j=>j["extra"]=true); Bad(j=>j["presets"]![0]!["width"]=1);
        Bad(j=>j["presets"]![0]!["shape"]="capsule"); Bad(j=>j["presets"]![0]!["label"]="");
        Bad(j=>j["presets"]![0]!.AsObject().Remove("angle")); Bad(j=>j["species"]![0]!["left"]![1]=9);
        Bad(j=>j["species"]![0]!["id"]="human"); Bad(j=>j["species"]![0]!.AsObject().Remove("yaw"));
        Check(CommonBrowCatalog.FromText("{\"version\":1,\"version\":1}").Errors.Count>0,"duplicate property rejected");
        foreach(var species in new[]{"parrot","eagle","lion"})
        foreach(var palette in CommonRoster.Current.Palettes.Keys)
        foreach(int variant in Enumerable.Range(0,10))
        {
            var id=new CommonIdentity("hunter",species,palette,"club",variant);
            var rig=CharacterRig.Create(CommonRoster.Current.LookFor(id));AddChild(rig);CommonRoster.Current.Apply(rig,id);
            var brows=Brows(rig);Check(brows.Length==2,"two brows for each palette and variant");
            var before=brows.Select(b=>b.Transform).ToArray(); c.Apply(rig,id);
            Check(Brows(rig).Length==2&&brows.Select(b=>b.Transform).SequenceEqual(before),"idempotent stable identity");
            Check(brows[0].Mesh==brows[1].Mesh,"shared pair geometry");
            rig.QueueFree();
        }
        foreach(string s in new[]{"human","tiger","reindeer","rhino"})
        {
            var id=new CommonIdentity(s=="human"?"zombie":"hunter",s,"ochre",s=="human"?"unarmed":"club");
            var rig=CharacterRig.Create(CommonRoster.Current.LookFor(id));AddChild(rig);CommonRoster.Current.Apply(rig,id);Check(Brows(rig).Length==0,"unsupported absent");rig.QueueFree();
        }
        var sample=new CommonIdentity("hunter","parrot","red","club");
        var a=CharacterRig.Create(CommonRoster.Current.LookFor(sample));var b=CharacterRig.Create(CommonRoster.Current.LookFor(sample));AddChild(a);AddChild(b);
        CommonRoster.Current.Apply(a,sample);CommonRoster.Current.Apply(b,sample);
        var left=Brows(a)[0];var other=Brows(b)[0];Check(left.Mesh==other.Mesh,"cross-rig shared mesh");var otherTransform=other.Transform;
        var local=left.Transform;var global=left.GlobalPosition;a.Head!.Position+=new Vector3(.01f,.02f,.03f);a.Head.Rotation+=new Vector3(.1f,.1f,.1f);
        Check(left.Transform==local&&left.GlobalPosition.DistanceTo(global)>.01f,"head motion follows without rebuilding");Check(other.Transform==otherTransform,"independent rigs");
        var material=left.MaterialOverride;var fx=StatusVisualController.Attach(a);fx.SetStates(new[]{new StatusVisualState("burn",2)});fx._Process(0);
        Check(left.GetChildren().OfType<MeshInstance3D>().Any(m=>m.HasMeta(StatusVisualController.InternalMeta)),"status scans brow");
        a.FlashHit();fx._Process(0);Check(left.GetChildren().OfType<MeshInstance3D>().All(m=>!m.Visible),"flash hides status layer");
        a.ShowAs(VisionSystem.Visibility.Hidden);fx._Process(0);Check(!left.IsVisibleInTree()&&!fx.Displayed,"hide conceals brows and status");
        a.ShowAs(VisionSystem.Visibility.Visible);await Frames(40);fx.Clear();await Frames(3);
        Check(left.MaterialOverride==material&&!left.GetChildren().Any(m=>m.HasMeta(StatusVisualController.InternalMeta)),"reset preserves brow material");
        var previous=CommonBrowCatalog.Current;CommonBrowCatalog.Reload();Check(CommonBrowCatalog.Current!=previous&&CommonBrowCatalog.Current.Errors.Count==0,"reload reconstructs catalog");
    }
}
