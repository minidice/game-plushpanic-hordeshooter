using Godot;
using System;
using System.Linq;

namespace HordeShooter.Tests;

public partial class PetModelCheck : Node3D
{
    private int _checks;
    private void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++; GD.Print($"PASS: {message}");
    }
    public override void _Ready()
    {
        try
        {
            foreach(var quality in Enum.GetValues<PetModelQuality>())
            foreach (string species in new[] { "turtle", "dog", "rabbit" })
            {
                var pet = PetModel.Create(species,quality); AddChild(pet);
                Check(pet.Quality==quality,$"{species}: requested {quality} geometry loaded");
                var socket = pet.BackSocket;
                Check(pet.Weapon == null && socket.GetChildCount() == 0, $"{species}: base is completely unarmed");
                if (species == "dog")
                {
                    var torso=(MeshInstance3D)pet.FindChild("BodyMesh",true,false);
                    var material=(StandardMaterial3D)torso.GetActiveMaterial(0);
                    var colors=torso.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Color].AsColorArray();
                    Check(material.VertexColorUseAsAlbedo && colors.Length>0 && colors.Max(c=>c.R)-colors.Min(c=>c.R)>.2f,
                        "dog: imported coat uses its dark-to-tan surface colors");
                }
                var back=(MeshInstance3D)pet.FindChild("BodyMesh",true,false);
                var backVertices=Enumerable.Range(0,back.Mesh.GetSurfaceCount())
                    .SelectMany(i=>back.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                    .Select(v=>pet.ToLocal(back.ToGlobal(v)));
                float backHeight=backVertices.Where(v=>Mathf.Abs(v.X)<.265f && v.Z>-.515f && v.Z<.055f).Max(v=>v.Y);
                Check(Mathf.Abs(socket.Position.Y-backHeight)<.025f,$"{species}/{quality}: mounting plate touches actual back surface");
                Check(socket.GlobalBasis.IsEqualApprox(Basis.Identity), $"{species}: socket uses shared +Z forward / +Y up axes");
                var bounds = Bounds(pet, pet);
                Check(Mathf.Abs(bounds.Position.Y) < .025f && bounds.End.Y > .8f && bounds.End.Y < 2, $"{species}: soles sit on floor and model has pet scale");
                foreach (string weapon in new[] { "twin_blaster", "rocket_pod", "twin_blaster" })
                {
                    var previous = pet.Weapon;
                    pet.SetWeapon(weapon);
                    Check(socket.GetChildCount() == 1 && pet.Weapon!.GetParent() == socket && pet.Weapon.Transform.IsEqualApprox(Transform3D.Identity), $"{species}/{weapon}: replaces at same socket without species offsets");
                    Check(previous == null || !GodotObject.IsInstanceValid(previous), $"{species}/{weapon}: replaced model is released");
                    var muzzle = pet.Weapon!.FindChild("Muzzle", true, false) as Node3D;
                    Check(muzzle != null && muzzle.GlobalPosition.Y > socket.GlobalPosition.Y && muzzle.GlobalPosition.Z > socket.GlobalPosition.Z + .5f, $"{species}/{weapon}: muzzle is above back and points forward");
                    var moduleBounds = Bounds(pet.Weapon, pet);
                    Check(moduleBounds.Position.Y >= socket.Position.Y - .005f, $"{species}/{weapon}: mounting plate never extends into back");
                    var front = (Node3D)pet.FindChild("FrontLeft",true,false);
                    var rest = front.Transform;
                    pet.Pose(.23f,1);
                    Check(!rest.IsEqualApprox(front.Transform) && pet.Weapon.GetParent() == socket, $"{species}/{weapon}: articulated walk keeps weapon attached");
                    pet.Pose(.23f,0);
                    Check(rest.IsEqualApprox(front.Transform), $"{species}/{weapon}: idle restores foot pose");
                }
                var kept = pet.Weapon;
                try { pet.SetWeapon("not_a_weapon"); throw new Exception("Invalid ID accepted"); }
                catch (ArgumentException) { }
                Check(pet.Weapon == kept && socket.GetChildCount() == 1, $"{species}: invalid weapon leaves current loadout intact");
                pet.SetWeapon(null);
                Check(pet.Weapon == null && socket.GetChildCount() == 0, $"{species}: unequip restores bare base immediately");
                pet.SetWeapon("rocket_pod");var module=pet.Weapon;
                pet.Position=new Vector3(2,.5f,-3);pet.RotationDegrees=new Vector3(0,37,0);
                pet.Pose(.3f,1);
                var world=pet.GlobalTransform;var muzzleBefore=((Node3D)module!.FindChild("Muzzle",true,false)).GlobalPosition;
                var footBefore=((Node3D)pet.FindChild("FrontLeft",true,false)).Transform;
                pet.SetQuality(quality==PetModelQuality.High ? PetModelQuality.Low : PetModelQuality.High);
                Check(pet.Weapon==module && pet.BackSocket.GetChildCount()==1,$"{species}: quality switch retains the same weapon instance");
                Check(pet.GlobalTransform.IsEqualApprox(world) && ((Node3D)module.FindChild("Muzzle",true,false)).GlobalPosition.DistanceTo(muzzleBefore)<.001f,$"{species}: quality switch keeps placement and muzzle");
                Check(((Node3D)pet.FindChild("FrontLeft",true,false)).Transform.IsEqualApprox(footBefore),$"{species}: quality switch keeps walk pose");
                pet.Pose(0,0);
                pet.BackSocket.RemoveChild(module);
                Check((Bounds(pet.FindChild(species,true,false),pet).Size-bounds.Size).Length()<.10f,$"{species}: quality tiers preserve silhouette bounds");
                pet.BackSocket.AddChild(module);
                var retainedQuality=pet.Quality;
                try {pet.SetQuality((PetModelQuality)99);throw new Exception("Invalid quality accepted");} catch(ArgumentException) { }
                Check(pet.Quality==retainedQuality && pet.Weapon==module,$"{species}: invalid quality leaves model and weapon intact");
                pet.Free();
            }
            GameSettings.PetQuality=PetModelQuality.Low;
            var configured=PetModel.Create("rabbit");
            Check(configured.Quality==PetModelQuality.Low,"creation without override uses saved pet quality");
            GameSettings.PetQuality=PetModelQuality.High;
            AddChild(configured);
            Check(configured.Quality==PetModelQuality.High,"entering tree catches settings changed after creation");
            GameSettings.PetQuality=PetModelQuality.Low;
            configured.SetWeapon("twin_blaster");var configuredWeapon=configured.Weapon;
            GameSettings.PetQuality=PetModelQuality.High;
            Check(configured.Quality==PetModelQuality.High && configured.Weapon==configuredWeapon,"settings change updates active pets without replacing their weapons");
            configured.Free();GameSettings.PetQuality=PetModelQuality.Low;GameSettings.PetQuality=PetModelQuality.High;
            GD.Print($"PET_MODEL_PASS {_checks}"); GetTree().Quit();
        }
        catch(Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    public static Aabb Bounds(Node node, Node3D reference)
    {
        Aabb? result = null;
        void Visit(Node n)
        {
            if (n is MeshInstance3D mesh)
            {
                var box = reference.GlobalTransform.AffineInverse() * mesh.GlobalTransform * mesh.GetAabb();
                result = result.HasValue ? result.Value.Merge(box) : box;
            }
            foreach(var child in n.GetChildren()) Visit(child);
        }
        Visit(node); return result ?? new Aabb();
    }
}
