using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Modeled Stage 2 draft safehouse: entry, door, close interaction and visibility.</summary>
public partial class SafehouseArtCheck : Node
{
    private Stage _stage = null!;
    private int _checks;
    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        _checks++;
        GD.Print("PASS: " + message);
    }
    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private static Node3D[] Models(Node parent, string id) => parent.FindChildren("*", "Node3D", true, false)
        .OfType<Node3D>().Where(n => n.SceneFilePath.EndsWith("/" + id + ".glb")).ToArray();
    private static MeshInstance3D[] Meshes(Node parent) => parent.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
    private static bool ClearVolume(Node3D model, Aabb volume) => Meshes(model).All(mesh =>
    {
        var faces = mesh.Mesh.GetFaces();
        for (int i = 0; i < faces.Length; i += 3)
        {
            var bounds = new Aabb(mesh.ToGlobal(faces[i]), Vector3.Zero)
                .Expand(mesh.ToGlobal(faces[i + 1])).Expand(mesh.ToGlobal(faces[i + 2]));
            if (bounds.Intersects(volume)) return false;
        }
        return true;
    });
    private async Task Shot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/safehouse-" + name + ".png")) == Error.Ok, "render " + name);
    }
    private async Task Walk(Player player, Vector3 to, string label)
    {
        int ticks = 0;
        while (((player.GlobalPosition - to) with { Y = 0 }).Length() > .12f && ticks++ < 480)
        {
            var dir = ((to - player.GlobalPosition) with { Y = 0 }).Normalized();
            player.SetControls(new Vector2(dir.X, dir.Z), to + dir * 2 + Vector3.Up, false);
            await Frames(1);
        }
        player.SetControls(Vector2.Zero, to + Vector3.Right * 3 + Vector3.Up, false);
        Check(((player.GlobalPosition - to) with { Y = 0 }).Length() <= .12f, label + " actual walk reaches target");
        Check(Mathf.Abs(player.GlobalPosition.Y - to.Y) < .15f, label + " stays on floor");
    }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        bool failed = false;
        try { await Run(); GD.Print("SAFEHOUSE_ART_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("SAFEHOUSE_ART_FAIL " + e.Message); failed = true; }
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }
    private async Task Run()
    {
        foreach (string id in new[] { "safehouse_b", "safehouse_door" })
            Check(PropLibrary.Find(id) != null, id + " is registered");
        var map = new LayoutMap();
        _stage = new Stage { MapOverride = map, ModeOverride = GameMode.Scenario,
            CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 0 };
        AddChild(_stage);
        await Frames(4);
        var p = _stage.Player;
        p.ManualControl = true;
        p.Invulnerable = true;
        _stage.StartCrescendo();
        _stage.Director.EndCrescendoForTest();
        await Frames(4);
        _stage.Director.Stop();
        foreach (var enemy in _stage.AllEnemies.ToList()) if (IsInstanceValid(enemy)) enemy.QueueFree();
        await Frames(2);
        Check(_stage.Objective == Objective.ReachSafehouse, "existing bridge objective advances to safehouse");
        var house = Models(_stage, "safehouse_b").Single();
        var door = map.SafehouseDoor!;
        var leaf = Models(door, "safehouse_door").Single();
        var at = house.GlobalPosition;
        var blocker = door.FindChildren("*", "CollisionShape3D", true, false).OfType<CollisionShape3D>().Single();
        Check(Mathf.Abs(at.Y - 4.8f) < .01f, "safehouse pivot matches arrival plateau");
        Check(PropLibrary.Find("safehouse_b")!.Collision.Kind == "none" && PropLibrary.Find("safehouse_door")!.Collision.Kind == "none", "game retains all collision ownership");
        Check(door.Size.DistanceTo(new Vector3(.3f, 2.4f, 2.6f)) < .001f, "door retains existing collider dimensions");
        Check(door.IsOpen && blocker.Disabled && !leaf.Visible, "initial door is open and hidden in wall pocket");
        Check(ClearVolume(house, new Aabb(at + new Vector3(-7.3f, .03f, -1.27f), new Vector3(1, 2.36f, 2.54f))), "actual GLB doorway remains clear below lintel");
        Check(ClearVolume(house, new Aabb(at + new Vector3(-6.35f, .03f, -5.35f), new Vector3(12.7f, 2.22f, 10.7f))), "actual GLB interior clears standing survivor bodies");
        // L-32: safehouse_b moved the button from the west wall (screen lower-left, its back to the camera) to the north wall.
        Check(map.SafehouseSwitch!.GlobalPosition.DistanceTo(at + LayoutMap.SafehouseSwitchLocal) < .001f, "close interaction is on the modeled red button on the north wall");
        p.TeleportTo(door.GlobalPosition + Vector3.Left * 4 + Vector3.Up * .05f);
        p.SetControls(Vector2.Zero, at + Vector3.Up, false);
        _stage.Cam.Close = false;
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(40);
        await Shot("approach");
        door.SetOpen(false, 0);
        await Frames(20);
        Check(leaf.Visible && leaf.Position.Length() < .001f && !blocker.Disabled, "closed door restores modeled leaf and collision");
        await Shot("closed-exterior");
        p.SetControls(Vector2.Right, at + Vector3.Up, false);
        await Frames(90);
        p.SetControls(Vector2.Zero, at + Vector3.Up, false);
        Check(p.GlobalPosition.X < door.GlobalPosition.X - .5f && p.GlobalPosition.X > door.GlobalPosition.X - .9f, "actual walking cannot pass the closed door");
        Check(_stage.Flow.IsBlocked(door.GlobalPosition), "closed door blocks flow field");
        door.SetOpen(true, .5f);
        await Frames(45);
        Check(!leaf.Visible && blocker.Disabled && Mathf.Abs(leaf.Position.Z - 2.5f) < .001f, "opening slides leaf into wall and hides it");
        await Walk(p, door.GlobalPosition + Vector3.Right * 2, "west doorway entry");
        await Walk(p, at, "safehouse center");
        await Frames(35);
        Check(map.InSafehouse(p.GlobalPosition) && _stage.Objective == Objective.CloseSafehouse, "entry enables existing close-to-clear objective");
        Check(Meshes(house).Where(m => m.Name.ToString().StartsWith("Keep")).All(m => m.Transparency == 0), "low walls and control button remain opaque");
        Check(Meshes(house).Any(m => m.Name.ToString().StartsWith("Occlude_") && m.Transparency > .7f), "upper walls and half-roof fade for ordinary camera");
        await Shot("inside-wide");
        _stage.Cam.Close = true;
        await Frames(40);
        await Shot("inside-close");
        var ammo = _stage.Interactables.Single(i => i.Pickup == PickupKind.Ammo && map.InSafehouse(i.GlobalPosition));
        await Walk(p, ammo.GlobalPosition with { Y = at.Y }, "arrival ammunition");
        Check(p.GlobalPosition.DistanceTo(ammo.GlobalPosition) < ammo.Radius, "arrival ammo stays within interaction reach");
        await Shot("ammo");
        await Walk(p, at + Vector3.Back * 4, "near south wall");
        p.SetControls(Vector2.Down, at + Vector3.Back * 8, false);
        await Frames(75);
        p.SetControls(Vector2.Zero, at + Vector3.Up, false);
        Check(p.GlobalPosition.Z < at.Z + 5.2f && p.GlobalPosition.Z > at.Z + 4.8f, "original room wall prevents walking through faded art");
        await Walk(p, at, "return to room center");
        await Walk(p, door.GlobalPosition + Vector3.Right * 2, "return to doorway");
        await Walk(p, door.GlobalPosition + Vector3.Left * 4, "leave safehouse");
        p.SetControls(Vector2.Zero, p.GlobalPosition + Vector3.Left * 3, false);
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(45);
        Check(_stage.Objective == Objective.ReachSafehouse && !map.SafehouseSwitch.Enabled, "leaving disables close action");
        Check(Meshes(house).Where(m => m.Name.ToString().StartsWith("Occlude_")).All(m => m.Transparency < .05f), "upper house returns opaque after leaving");
        await Shot("exited");
        var other = _stage.AddSurvivor(CharacterCatalog.Player("duck"), door.GlobalPosition + Vector3.Left * 3, false, "OtherHuman");
        other.ManualControl = true;
        other.Invulnerable = true;
        await Walk(p, door.GlobalPosition + Vector3.Right * 2, "enter while another human is outside");
        await Walk(p, at, "wait inside");
        await Frames(5);
        Check(_stage.Objective == Objective.ReachSafehouse && !map.SafehouseSwitch.Enabled && !_stage.TryCloseSafehouse(), "all living humans must enter before closing");
        await Walk(other, door.GlobalPosition + Vector3.Right * 2, "second human enters");
        await Walk(other, at + Vector3.Right * 3, "second human clears entrance");
        await Frames(5);
        Check(_stage.Objective == Objective.CloseSafehouse && map.SafehouseSwitch.Enabled, "all humans inside enables close action");
        var enemyInside = _stage.SpawnCommon(at + Vector3.Forward * 3, false);
        enemyInside.ProcessMode = ProcessModeEnum.Disabled;
        await Frames(2);
        Check(!_stage.TryCloseSafehouse() && door.IsOpen && _stage.State == StageState.Playing, "enemy inside still prevents closing and clear");
        enemyInside.QueueFree();
        await Frames(2);
        // In front of the north-wall button (L-32), on the floor.
        await Walk(p, at + new Vector3(LayoutMap.SafehouseSwitchLocal.X, 0, LayoutMap.SafehouseSwitchLocal.Z + .6f), "modeled close button");
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(35);
        Check(_stage.FocusedInteractable == map.SafehouseSwitch, "standing by red button selects real E action");
        await Shot("button");
        // Exercise reversible door motion before the actual close action ends the stage.
        door.SetOpen(false, .8f);
        await Frames(20);
        Check(leaf.Visible && !blocker.Disabled && leaf.Position.Z > 0 && leaf.Position.Z < 2.5f, "partial closing shows moving leaf while collision is active");
        await Shot("closing");
        door.SetOpen(true, .5f);
        await Frames(5);
        door.SetOpen(false, 0);
        await Frames(40);
        Check(leaf.Visible && leaf.Position.Length() < .001f && !blocker.Disabled, "interrupted opening restores closed leaf and collider");
        door.SetOpen(true, 0);
        await Frames(5);
        p.ManualControl = false;
        Input.ParseInputEvent(new InputEventAction { Action = GameInput.Interact, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventAction { Action = GameInput.Interact, Pressed = false });
        p.ManualControl = true;
        await Frames(45);
        Check(_stage.State == StageState.Cleared && !door.IsOpen && !blocker.Disabled, "real E button closes modeled door and clears stage");
        Check(leaf.Visible && leaf.Position.Length() < .001f && !map.SafehouseSwitch.Enabled, "final door is shut and close action disabled");
        await Shot("cleared");
        Check(PropLibrary.Errors.Count == 0, "no missing model resources");
    }
}
