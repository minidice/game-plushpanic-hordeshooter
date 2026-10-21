using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class WeaponEffectsCheck : Node
{
    private Stage _stage = null!;
    private int _checks;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        _checks++; GD.Print("PASS: " + message);
    }
    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private IEnumerable<Node> Descendants(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private MeshInstance3D[] Meshes(string name) => Descendants(_stage.Effects).OfType<MeshInstance3D>()
        .Where(n => n.Name == name).ToArray();
    private async Task Clear()
    {
        foreach (var n in _stage.Effects.GetChildren()) n.QueueFree();
        await Frames(2);
    }
    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("WEAPON_EFFECTS_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); failed = true; }
        QuitHygiene.Collect(); GetTree().Quit(failed ? 1 : 0);
    }
    private async Task Run()
    {
        _stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false, SquadOverride = 0 };
        AddChild(_stage);
        await Frames(3);
        foreach (var e in _stage.AllEnemies.ToList()) e.QueueFree();
        var player = _stage.Player;
        player.ManualControl = true;
        player.TeleportTo(new Vector3(14, 0, 0));
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
        await Frames(3);
        _stage.ProcessMode = ProcessModeEnum.Disabled;
        var sizes = new Dictionary<string, float>();
        foreach (var def in WeaponDatabase.Current.Weapons.Values.Where(d => d.Mode == FireMode.Hitscan))
        {
            player.Weapons.PlaceInSlot((int)def.Slot, def);
            player.Rig.Face(Vector3.Right);
            var from = player.Weapons.MuzzlePosition;
            var ends = Enumerable.Range(0, Math.Max(1, def.Pellets))
                .Select(i => from + Vector3.Right.Rotated(Vector3.Up,
                    Mathf.DegToRad(def.Pellets > 1 ? ((i * .618034f) % 1 - .5f) * def.PelletSpread : 0)) * 12).ToArray();
            int ammo = player.Gun.Mag;
            player.Weapons.ShowNetShot(def, from, ends[0], ends);
            var flashes = Meshes("MuzzleFlash");
            var tracers = Meshes("Tracer");
            Check(flashes.Length == 1, def.Id + " emits one muzzle flash per shot, including buckshot");
            Check(Descendants(_stage.Effects).OfType<OmniLight3D>().Count() == 1, def.Id + " uses only one short muzzle light");
            Check(tracers.Length == ends.Length, def.Id + " displays all supplied rays");
            Check(player.Gun.Mag == ammo, def.Id + " received effects do not spend ammo");
            sizes[def.Id] = flashes[0].Scale.X;
            if (def.ChargeTime > 0)
                Check(tracers[0].Scale.X > 11, "the railgun retains an instantaneous beam instead of a traveling bullet");
            else
                Check(tracers.All(t => t.Scale.X < 4), def.Id + " starts with short traveling streaks");
            _stage.Cam.SnapTo(player.GlobalPosition + Vector3.Right * 3 + Vector3.Up * .8f);
            await Shot(def.Id + "-flash");
            foreach (var n in _stage.Effects.GetChildren()) n._Process(.035);
            await Shot(def.Id + "-travel");
            foreach (var n in _stage.Effects.GetChildren()) n._Process(2);
            await Frames(2);
            Check(Meshes("MuzzleFlash").Length == 0 && Meshes("Tracer").Length == 0, def.Id + " removes its flash and streaks");
            await Clear();
        }
        Check(sizes["m1911"] < sizes["m4"] && sizes["r870"] > sizes["m4"] && sizes["awm"] > sizes["m4"],
            "pistol, rifle, shotgun and sniper have distinct flash sizes");

        var shotgun = ArmoryCatalog.Weapon("double_barrel");
        player.Weapons.PlaceInSlot(2, shotgun);
        int shells = player.Gun.Mag;
        player.Weapons.Tick(.01f, false, true, true, false);
        Check(player.Gun.Mag == shells - 1 && Meshes("Tracer").Length == shotgun.Pellets,
            "a local shotgun spends one shell and displays every pellet");
        Check(Meshes("MuzzleFlash").Length == 1 && Descendants(_stage.Effects).OfType<OmniLight3D>().Count() == 1,
            "a local shotgun also has one flash and one light");
        await Clear();
        player.Weapons.PlaceInSlot(2, ArmoryCatalog.Weapon("m16"));
        int burstAmmo = player.Gun.Mag, shots = player.Weapons.ShotsFired;
        player.Weapons.Tick(.01f, false, true, true, false);
        for (int i = 0; i < 3; i++) player.Weapons.Tick(.1f, false, false, false, false);
        Check(player.Gun.Mag == burstAmmo - 3 && player.Weapons.ShotsFired == shots + 3 && Meshes("MuzzleFlash").Length == 3,
            "M16 burst timing emits three shots and three flashes after releasing the trigger");
        await Clear();

        var dual = ArmoryCatalog.Weapon("m1911").DualVariant!;
        player.Weapons.PlaceInSlot(1, dual);
        var muzzles = new List<Vector3>();
        for (int i = 0; i < 2; i++)
        {
            var muzzle = player.Weapons.MuzzlePosition;
            player.Weapons.Tick(.5f, false, true, true, false);
            var light = Descendants(_stage.Effects).OfType<OmniLight3D>().Single();
            Check(light.GlobalPosition.DistanceTo(muzzle) < .01f, "dual pistol flash stays on the muzzle that just fired");
            muzzles.Add(light.GlobalPosition);
            await Clear();
        }
        Check(muzzles[0].DistanceTo(muzzles[1]) > .1f, "dual pistol flashes alternate between the two barrels");

        player.Weapons.PlaceInSlot(1, ArmoryCatalog.Weapon("frying_pan"));
        player.Weapons.Tick(.01f, false, true, true, false);
        Check(Meshes("MeleeSweep").Length == 0, "melee windup does not flash early");
        player.Weapons.Tick(.18f, false, false, false, false);
        var sweep = Meshes("MeleeSweep").Single();
        var vertices = sweep.Mesh.SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        Check(vertices.All(v => v.Length() > .5f), "melee is a crescent with an open center, not a filled fan");
        await Shot("melee");
        foreach (var n in _stage.Effects.GetChildren()) n._Process(1);
        await Frames(2);
        Check(Meshes("MeleeSweep").Length == 0, "the melee crescent fades and is released");

        foreach (var id in new[] { "alabong", "mortar" })
        {
            var def = ArmoryCatalog.Weapon(id);
            player.Weapons.PlaceInSlot(3, def);
            var from = player.Weapons.MuzzlePosition;
            var end = from + Vector3.Right * (id == "mortar" ? 12 : 1);
            player.Weapons.ShowNetShot(def, from, end, new[] { end });
            Check(Meshes("MuzzleFlash").Length == 1 && Meshes("Tracer").Length == 0, id + " flashes without a fake hitscan tracer");
            Check(_stage.Effects.GetChildren().Any(n => n is Rocket or MortarShell), id + " keeps its real projectile");
            await Shot(id);
            await Clear();
        }
        foreach (var id in new[] { "honk", "rubber_duck" })
        {
            var def = ArmoryCatalog.Weapon(id);
            player.Weapons.ShowNetShot(def, player.GlobalPosition + Vector3.Up, player.AimPoint, Array.Empty<Vector3>());
            Check(Meshes("MuzzleFlash").Length == 0 && Meshes("Tracer").Length == 0, id + " has no firearm flash or bullet streak");
            await Clear();
        }
    }
    private async Task Shot(string name)
    {
        if (!Windowed) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/weapon-fx-{name}.png"));
    }
}
