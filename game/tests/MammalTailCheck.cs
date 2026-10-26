using Godot;
using System;
using System.Linq;
using System.IO;
using System.Security.Cryptography;

namespace HordeShooter.Tests;

/// <summary>Checks rendered-tail inputs, actual section geometry, and per-rig deformation/state contracts.</summary>
public partial class MammalTailCheck : Node
{
    private int _checks;
    private void Check(bool value,string message) { if(!value)throw new Exception(message);_checks++; }
    private static MeshInstance3D Tail(CharacterRig rig,string species)=>(MeshInstance3D)rig.Model.FindChild(species+"_tail_mesh",true,false)!;
    public override void _Ready()
    {
        try {
            LegacySurfaces();
            MovingStyleSwitches();
            foreach(string species in new[]{"raccoon","redpanda"}) {
                Geometry(species);
                Runtime(species);
            }
            foreach(string species in new[]{"goose","duck"}) {
                var rig=CharacterRig.Create(CharacterCatalog.Player(species));AddChild(rig);
                Check(Tail(rig,species).Skin==null,"birds retain rigid tails");rig.Free();
            }
            GD.Print("MAMMAL_TAIL_PASS "+_checks);QuitHygiene.Collect();GetTree().Quit();
        } catch(Exception e) {GD.PushError("MAMMAL_TAIL_FAIL "+e);GetTree().Quit(1);}
    }
    private void MovingStyleSwitches()
    {
        foreach(string species in new[]{"goose","duck","raccoon","redpanda"}) {
            bool bird=AppearanceCatalog.IsBird(species);
            var changed=CharacterRig.Create(CharacterCatalog.Player(species));AddChild(changed);
            var control=CharacterRig.Create(CharacterCatalog.Player(species));AddChild(control);
            changed.ApplyAppearance(new AppearanceProfile{TailStyle=bird?0:3});
            control.ApplyAppearance(new AppearanceProfile{TailStyle=bird?0:3});
            for(int i=0;i<45;i++){changed.Animate(1f/60,.8f,0,0,Vector3.Zero);control.Animate(1f/60,.8f,0,0,Vector3.Zero);}
            changed.ApplyAppearance(new AppearanceProfile{TailStyle=1});
            if(!bird)changed.ApplyAppearance(new AppearanceProfile{TailStyle=3});
            changed.Animate(1f/60,.8f,0,0,Vector3.Zero);control.Animate(1f/60,.8f,0,0,Vector3.Zero);
            var actual=Tail(changed,species).GetParentNode3D().GetParentNode3D().Transform;
            var expected=Tail(control,species).GetParentNode3D().GetParentNode3D().Transform;
            Check(actual.IsEqualApprox(expected),species+" moving style switches preserve rigid tail phase and smoothing");
            changed.Free();control.Free();
        }
    }
    private void LegacySurfaces()
    {
        // Captured by executing HEAD's original builder. The request expressly excludes these surfaces.
        var cases=new[]{
            ("goose",0,"2E97D4026F17EA7CBB3C563BF5AD518D474182E1358FAC63FE88DC4A0B53DCD8"),
            ("goose",1,"B73F15965F2E98FE67DAE4AC5159E1089CB960AC6789CAC423DC38DBCE9F60F1"),
            ("goose",2,"D4408EF71F90F67857CF92D08596B82585BE297E1616280A286B3CF5BF659FC4"),
            ("goose",3,"947168329BAF05DD40B3DBA17DBF30BBC073D959BFABDE93D86F90D145FD9655"),
            ("goose",4,"0EA615D7518E9F1A9D7CA391A29292A100D20577C1EF729AD554F9FFBC491C75"),
            ("duck",0,"4378DDD0A5676E9EB2F1514EDE0E864FE6F79274C82ABDF6690A6CD388D663A9"),
            ("duck",1,"E406339B884539C96BDA7DC23043EF0248B51F7402203F0928E9A401C236211D"),
            ("duck",2,"91A856345A2E45BB0FB378B06F9C09791506CDA985E728AD2F81926567ABDC1D"),
            ("duck",3,"814C33BB20C15A4116A290C547506444E4B88FA0EBA366EE74DDB8E5BFB3AB95"),
            ("duck",4,"CC52C71D6C078BC768410D001028CD7AFE459FB335F78C912754AADEE14A5CC2"),
            ("raccoon",3,"FD5851A74C272AF67735A50523B55AA777F1DB2840B1B7884C56B18371EB548D"),
            ("redpanda",3,"15B4BDB58E883236670BDF684BEDAEDCF893E4EECA2F9AEB894CAEA7D58A6EF9")};
        using var light=new StandardMaterial3D();using var dark=new StandardMaterial3D();
        foreach(var (species,style,expected) in cases) {
            using var mesh=CharacterTailMesh.Build(species,new Material[]{light,dark},style);
            using var bytes=new MemoryStream();using var writer=new BinaryWriter(bytes);
            for(int s=0;s<mesh.GetSurfaceCount();s++)foreach(var kind in new[]{Mesh.ArrayType.Vertex,Mesh.ArrayType.Normal})
                foreach(var v in mesh.SurfaceGetArrays(s)[(int)kind].AsVector3Array()){writer.Write(v.X);writer.Write(v.Y);writer.Write(v.Z);}
            Check(Convert.ToHexString(SHA256.HashData(bytes.ToArray()))==expected,$"{species}/{style} excluded surface remains byte-identical");
        }
    }
    private void Geometry(string species)
    {
        var materials=new Material[]{new StandardMaterial3D{AlbedoColor=new Color("a89b85")},new StandardMaterial3D{AlbedoColor=new Color("3d3029")}};
        foreach(int style in new[]{0,1,2,4}) {
            var mesh=CharacterTailMesh.Build(species,materials,style);
            var tip=CharacterTailMesh.Center(species,CharacterTailMesh.For(style),1);
            Check(mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array().Any(v=>v.DistanceTo(tip)<.00001f),"long tail preserves original light tip channel");
            Check(!mesh.SurfaceGetArrays(1)[(int)Mesh.ArrayType.Vertex].AsVector3Array().Any(v=>v.DistanceTo(tip)<.00001f),"dark stripe never covers terminal cap");
            float width=style==1?1.35f:style==2?.72f:1;
            float radius=(species=="raccoon"?.145f:.18f)*width;
            // Every first triangle vertex around the root ring is a real surface point.
            var vertices=mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            for(int side=0;side<24;side++)
                Check(Mathf.Abs(vertices[side*6].Length()-radius)<.0001f,$"{species}/{style}: root has full cylindrical girth");
            // Gather the first 6 complete rings from surface0; a bulb or taper here violates uniform long girth.
            for(int ring=0;ring<6;ring++) {
                var section=Enumerable.Range(0,24).Select(s=>vertices[ring*144+s*6]).ToArray();
                var center=section.Aggregate(Vector3.Zero,(a,b)=>a+b)/24;
                Check(section.All(v=>Mathf.Abs(v.DistanceTo(center)-radius)<.0001f),"early cross sections stay round and uniform");
            }
            var offsets=new int[2];
            for(int ring=0;ring<36;ring++) {
                int band=Math.Min(6,ring*7/36)%2;
                var arrays=mesh.SurfaceGetArrays(band);
                var points=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var section=Enumerable.Range(0,24).Select(s=>points[offsets[band]+s*(ring==35?3:6)]).ToArray();
                var center=section.Aggregate(Vector3.Zero,(a,b)=>a+b)/24;
                if(ring<=28)Check(section.All(v=>Mathf.Abs(v.DistanceTo(center)-radius)<.0001f),"full first 80 percent retains uniform girth");
                else Check(section.All(v=>v.DistanceTo(center)<radius),"terminal cap rounds toward tip");
                if(ring==18&&style==0)Check(center.IsEqualApprox(new Vector3(.05f,-.05f,species=="raccoon"?-.475f:-.575f)),"default midpoint uses gentle low curve");
                var bones=arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
                var weights=arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
                for(int side=0;side<24;side++) {
                    int at=(offsets[band]+side*(ring==35?3:6))*4;
                    Check(Mathf.Abs(weights.Skip(at).Take(4).Sum()-1)<.00004f,"actual vertex weights normalize within GPU 16-bit quantization");
                    if(ring==0)Check(bones[at]==0&&weights[at]==1,"actual root vertices stay on stationary bone");
                }
                offsets[band]+=ring==35?72:144;
            }
            Check(mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Bones].AsInt32Array().Length==vertices.Length*4,"long tail carries GPU bone indices");
            mesh.Dispose();
        }
        var shortMesh=CharacterTailMesh.Build(species,materials,3);
        Check(shortMesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Bones].VariantType==Variant.Type.Nil,"short style retains old rigid mesh");
        shortMesh.Dispose();
        foreach(var material in materials)material.Dispose();
    }
    private void Runtime(string species)
    {
        var rig=CharacterRig.Create(CharacterCatalog.Player(species));AddChild(rig);
        var other=CharacterRig.Create(CharacterCatalog.Player(species));AddChild(other);
        var mesh=Tail(rig,species);var otherMesh=Tail(other,species);var home=mesh.Mesh;
        var skeleton=mesh.GetNodeOrNull<Skeleton3D>(mesh.Skeleton);
        Check(skeleton!=null&&mesh.Skin!=null&&skeleton.GetBoneCount()==4,"default and legacy mammals bind a tail-only four-bone skin");
        Check(mesh.Skin!=otherMesh.Skin,"each rig owns its skin and deformation state");
        var anchor=mesh.GetParentNode3D().GetParentNode3D();var root=anchor.Position;
        rig.Animate(0,0,0,0,Vector3.Zero);
        rig.Face(Vector3.Right);rig.Animate(1f/60,0,0,0,Vector3.Zero);
        var first=skeleton!.GetBonePoseRotation(1).GetEuler();
        var last=skeleton.GetBonePoseRotation(3).GetEuler();
        Check(first.Length()>.001f&&last.Length()<first.Length()*.5f,"turn enters near bone before delayed tip");
        Check(skeleton.GetBonePoseRotation(0).IsEqualApprox(Quaternion.Identity),"root bone never bends");
        Check(otherMesh.GetNode<Skeleton3D>(otherMesh.Skeleton).GetBonePoseRotation(1).IsEqualApprox(Quaternion.Identity),"second rig stays neutral");
        for(int i=0;i<60;i++)rig.Animate(1f/60,0,0,0,Vector3.Zero);
        Check(Enumerable.Range(1,3).All(i=>skeleton.GetBonePoseRotation(i).GetEuler().Length()<.003f),"stop settles within one second");
        foreach(float dt in new[]{.001f,.016f,.1f,1f}) {
            rig.Face(Vector3.Back);rig.GlobalPosition+=Vector3.Right*100;
            rig.Animate(dt,1,0,0,Vector3.Right*400);
            Check(Enumerable.Range(1,3).All(i=>skeleton.GetBonePoseRotation(i).GetEuler().Length()<.17f),"teleport and dt spikes remain bounded");
        }
        Check(anchor.Position.IsEqualApprox(root),"tail motion never shifts attachment");
        rig.Animate(0,0,0,0,Vector3.Zero);
        Check(Enumerable.Range(0,4).All(i=>skeleton.GetBonePoseRotation(i).IsEqualApprox(Quaternion.Identity)),"dt zero preview is neutral");
        var status=StatusVisualController.Attach(rig);status.SetStates(new[]{new StatusVisualState("burn",3)});
        var layer=mesh.GetNode<MeshInstance3D>("StatusSurface");
        Check(layer.Skin==mesh.Skin&&layer.GetNodeOrNull<Skeleton3D>(layer.Skeleton)==skeleton,"status surface follows same skeleton");
        foreach(int style in new[]{1,2,3,4,0}) {
            rig.ApplyAppearance(new AppearanceProfile{TailStyle=style,TailSize=.65f,Tail="#83b29a"});
            status._Process(.3);
            Check(layer.Mesh==mesh.Mesh&&layer.Skin==mesh.Skin,"live style also updates status geometry and skin");
            Check(mesh.GetActiveMaterial(0)!=otherMesh.GetActiveMaterial(0),"custom colors remain per character");
            Check(mesh.GetParentNode3D().Scale.IsEqualApprox(Vector3.One*.65f),"tail size is applied once");
            Check(style!=3||mesh.Skin==null,"short style disables skinning");
        }
        Check(mesh.Mesh==home,"default restores shared canonical per-rig baseline");
        rig.ResetPose();Check(skeleton.GetBonePoseRotation(3).IsEqualApprox(Quaternion.Identity),"corpse reset clears tail history");
        rig.FlashDeath();Check(mesh.MaterialOverlay!=null,"hit flash covers tail");
        status._Process(.016);Check(!layer.Visible,"hit flash suppresses duplicate status surface");
        rig.ShowAs(VisionSystem.Visibility.Hidden);Check(!mesh.IsVisibleInTree()&&!layer.IsVisibleInTree(),"hide cannot leave ghost tail");
        rig.Free();other.Free();
        var zombie=CharacterRig.Create(CharacterDefinition.MakeZombie(CharacterCatalog.Player(species),CharacterCatalog.Enemy("plush")));AddChild(zombie);
        var infected=Tail(zombie,species);
        Check(infected.Skin!=null&&infected.MaterialOverlay==zombie.Definition.ZombieOverlay,"infected variants keep deformation and infection overlay");zombie.Free();
    }
}
