using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Bundle B in the Stage 2 draft: imported geometry, interaction and rendered visibility.</summary>
public partial class LandmarkArtCheck : Node
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
    private async Task Shot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/landmark-" + name + ".png")) == Error.Ok, "render " + name);
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
        bool failed = false;
        try { await Run(); GD.Print("LANDMARK_ART_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("LANDMARK_ART_FAIL " + e.Message); failed = true; }
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }
    private async Task Run()
    {
        foreach (string id in new[] { "bridge_truss", "bridge_leaf", "pump_house", "watch_post" })
            Check(PropLibrary.Find(id) is { Collision.Kind: "none" }, id + " is registered as visual-only art");
        _stage = new Stage { MapOverride = new LayoutMap(), ModeOverride = GameMode.Scenario,
            CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 0 };
        AddChild(_stage);
        await Frames(4);
        var map = (LayoutMap)_stage.Map;
        var p = _stage.Player;
        p.ManualControl = true;
        p.Invulnerable = true;
        foreach (var enemy in _stage.AllEnemies.ToList()) if (IsInstanceValid(enemy)) enemy.QueueFree();
        _stage.Cam.Close = true;
        var trusses = Models(_stage, "bridge_truss").OrderBy(n => n.GlobalPosition.X).ToArray();
        Check(trusses.Length == 5, "30 m bridge contains five 6 m trusses");
        for (int i = 0; i < trusses.Length; i++)
        {
            Check(trusses[i].GlobalPosition.DistanceTo(new Vector3(188 + i * 6, 2.4f, -45)) < .01f, "truss pivot matches deck top");
            var points = Meshes(trusses[i]).SelectMany(m => m.Mesh.GetFaces().Select(m.ToGlobal)).ToArray();
            Check(Mathf.Abs(points.Min(v => v.Y) - .7f) < .02f, "truss reaches 1.7 m below deck");
        }
        var decks = _stage.GetChildren().OfType<MeshInstance3D>().Where(m => m.Name.ToString().StartsWith("BridgeDeck")).ToArray();
        Check(decks.Length > 0 && decks.All(m => Mathf.Abs(m.GetAabb().Size.Y - .3f) < .001f), "bridge has thin visible deck instead of a full-height gray box");
        var gate = map.Shutter!;
        var leaf = Models(gate, "bridge_leaf").Single();
        Check(leaf.FindChild("Keep", true, false) == null && leaf.FindChild("Occlude_Leaf", true, false) != null, "leaf is entirely occludable per brief");
        Check(leaf.GlobalPosition.DistanceTo(gate.GlobalPosition) < .001f, "leaf hinge sits on gate floor pivot");
        Check(Mathf.Abs(leaf.GlobalBasis.X.Dot(Vector3.Back)) > .999f, "leaf width crosses the bridge");
        Check(leaf.GlobalBasis.Z.Dot(Vector3.Left) > .999f, "leaf warning face points toward west approach");
        var blocker = gate.FindChildren("*", "CollisionShape3D", true, false).OfType<CollisionShape3D>().Single();
        Check(!gate.IsOpen && !blocker.Disabled, "raised bridge physically blocks the route");
        p.TeleportTo(new Vector3(196, 2.45f, -45));
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(35);
        await Shot("bridge-closed");
        p.SetControls(Vector2.Right, gate.GlobalPosition + Vector3.Up, false);
        await Frames(90);
        p.SetControls(Vector2.Zero, gate.GlobalPosition + Vector3.Up, false);
        Check(p.GlobalPosition.X > 198 && p.GlobalPosition.X < 199.1f, "actual player cannot walk through raised leaf");
        p.TeleportTo(new Vector3(196, 2.45f, -45));
        _stage.Cam.SnapTo(p.GlobalPosition);
        gate.SetProgress(.5f);
        Check(Mathf.Abs(leaf.Position.Y + .6825f) < .001f && !blocker.Disabled, "partial motion lowers the visual but keeps collision");
        await Frames(25);
        await Shot("bridge-partial");
        gate.SetProgress(0);

        var pump = Models(_stage, "pump_house").Single();
        var panel = map.ShutterPanel!;
        Check(panel.GlobalPosition.DistanceTo(pump.ToGlobal(new Vector3(0, 1.1f, 1.6f))) < .001f, "interaction point is the modeled red valve");
        ClearOpening(pump, new Aabb(new Vector3(-1.4f,.05f,2.2f),new Vector3(2.8f,1.8f,.3f)), "pump front entrance");
        p.TeleportTo(pump.GlobalPosition + new Vector3(0,.05f,5));
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(35);
        await Shot("pump-approach");
        await Walk(p, pump.GlobalPosition + new Vector3(0,0,2), "entering pump opening");
        await Frames(35);
        Check(_stage.FocusedInteractable == panel, "walking to valve selects actual E prompt");
        Check(Meshes(pump).Where(m => m.Name.ToString().StartsWith("Keep")).All(m => m.Transparency == 0), "pump and red valve stay opaque");
        Check(Meshes(pump).Any(m => m.Name.ToString().StartsWith("Occlude_") && m.Transparency > .7f), "pump roof fades for player inside");
        await Shot("pump-valve");
        p.ManualControl = false;
        Input.ParseInputEvent(new InputEventAction { Action = GameInput.Interact, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventAction { Action = GameInput.Interact, Pressed = false });
        p.ManualControl = true;
        Check(_stage.Objective == Objective.HoldOut && _stage.Director.CrescendoActive, "real interaction action starts pump mission");
        await Shot("pump-running");
        await Walk(p, pump.GlobalPosition + new Vector3(0,0,5), "leaving pump opening");
        _stage.Director.EndCrescendoForTest();
        await Frames(3);
        _stage.Director.Stop();
        foreach (var enemy in _stage.AllEnemies.ToList()) if (IsInstanceValid(enemy)) enemy.QueueFree();
        Check(gate.IsOpen && blocker.Disabled && _stage.Objective == Objective.ReachSafehouse, "mission completion opens physical bridge");
        p.TeleportTo(new Vector3(196,2.45f,-45));
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(100);
        Check(!leaf.Visible && Mathf.Abs(leaf.Position.Y + 3.9f) < .001f, "fully lowered leaf hides below thin deck");
        await Shot("bridge-open");
        await Walk(p, new Vector3(204,2.4f,-45), "crossing the opened bridge");
        await Shot("bridge-crossed");
        gate.SetOpen(false, .5f);
        await Frames(45);
        Check(leaf.Visible && leaf.Position.Length() < .001f && !blocker.Disabled, "closing restores leaf and collision");
        gate.SetOpen(true, .5f);
        await Frames(5);
        gate.SetOpen(false, 0);
        await Frames(40);
        Check(leaf.Visible && leaf.Position.Length() < .001f, "interrupted open cannot hide a reclosed leaf");
        gate.SetOpen(true, 0);
        var hut = Models(_stage, "watch_post").Single();
        foreach (float x in new[] {-5.8f,5.8f})
            ClearOpening(hut, new Aabb(new Vector3(x-.19f,.05f,-1.4f),new Vector3(.38f,1.9f,2.8f)), "watch post doorway");
        var center = hut.GlobalPosition;
        p.TeleportTo(center + new Vector3(-8,.05f,0));
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(40);
        await Shot("watch-approach");
        await Walk(p, center, "west doorway entry");
        await Frames(35);
        Check(Meshes(hut).Where(m => m.Name.ToString().StartsWith("Keep")).All(m => m.Transparency == 0), "watch low walls stay opaque");
        Check(Meshes(hut).Any(m => m.Name.ToString().StartsWith("Occlude_") && m.Transparency > .7f), "watch upper walls and half-roof fade inside");
        await Shot("watch-inside");
        var supplies = _stage.Interactables.Where(i => i.Pickup is PickupKind.Ammo or PickupKind.Medkit)
            .Where(i => ((i.GlobalPosition-center) with {Y=0}).Length() < 5).ToArray();
        Check(supplies.Length == 2, "watch post retains both ammo and medkit supplies");
        foreach (var item in supplies)
        {
            await Walk(p, item.GlobalPosition with {Y=center.Y}, "reaching watch supply");
            Check(p.GlobalPosition.DistanceTo(item.GlobalPosition) < item.Radius, "watch supply remains in interaction reach");
        }
        await Shot("watch-supplies");
        await Walk(p, center, "returning to watch aisle");
        await Walk(p, center + Vector3.Right * 8, "east doorway exit");
        await Walk(p, center, "east doorway re-entry");
        await Walk(p, center + Vector3.Left * 8, "west doorway exit");
        await Frames(45);
        Check(Meshes(hut).Where(m => m.Name.ToString().StartsWith("Occlude_")).All(m => m.Transparency < .05f), "watch upper walls become opaque after leaving");
        Check(Meshes(pump).Where(m => m.Name.ToString().StartsWith("Occlude_")).All(m => m.Transparency < .05f), "distant pump roof becomes opaque");
        await Shot("watch-exited");
        Check(PropLibrary.Errors.Count == 0, "no missing prop resources");
    }

    private void ClearOpening(Node3D model, Aabb opening, string label)
    {
        var inverse = model.GlobalTransform.AffineInverse();
        int hits = 0;
        foreach (var mesh in Meshes(model))
        {
            var faces = mesh.Mesh.GetFaces();
            for (int i=0;i<faces.Length;i+=3)
            {
                var box = new Aabb(inverse * mesh.ToGlobal(faces[i]), Vector3.Zero)
                    .Expand(inverse * mesh.ToGlobal(faces[i+1])).Expand(inverse * mesh.ToGlobal(faces[i+2]));
                if (box.Intersects(opening)) hits++;
            }
        }
        Check(hits == 0, label + " has no imported triangles in walking clearance");
    }
}
