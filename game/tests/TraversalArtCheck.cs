using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Bundle A in the real Stage 2 draft, including rendered boarding and arrival.</summary>
public partial class TraversalArtCheck : Node
{
    private Stage _stage = null!;
    private int _checks;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        _checks++;
        GD.Print("PASS: " + message);
    }
    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private static Node3D[] Models(Node parent, string id) => parent.FindChildren("*", "Node3D", true, false)
        .OfType<Node3D>().Where(n => n.SceneFilePath.EndsWith("/" + id + ".glb")).ToArray();
    private static MeshInstance3D[] Meshes(Node parent) => parent.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToArray();
    private async Task Shot(string name)
    {
        if (!Windowed) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = ProjectSettings.GlobalizePath("res://output/traversal-" + name + ".png");
        Check(GetViewport().GetTexture().GetImage().SavePng(path) == Error.Ok, "render " + name);
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
        Check(((player.GlobalPosition - to) with { Y = 0 }).Length() <= .12f, label + " reaches its target through actual player controls");
        Check(Mathf.Abs(player.GlobalPosition.Y - to.Y) < .15f, label + " stays on the floor");
    }
    private void CheckDoorways(Node3D cabin)
    {
        var inverse = cabin.GlobalTransform.AffineInverse();
        int intrusions = 0;
        foreach (var mesh in Meshes(cabin))
        {
            var faces = mesh.Mesh.GetFaces();
            for (int i = 0; i < faces.Length; i += 3)
            {
                var box = new Aabb(inverse * mesh.ToGlobal(faces[i]), Vector3.Zero)
                    .Expand(inverse * mesh.ToGlobal(faces[i + 1])).Expand(inverse * mesh.ToGlobal(faces[i + 2]));
                foreach (float x in new[] { -2.92f, 2.92f })
                    if (box.Intersects(new Aabb(new Vector3(x - .12f, .15f, -1.15f), new Vector3(.24f, 1.65f, 2.3f)))) intrusions++;
            }
        }
        Check(intrusions == 0, "both cabin entrances are clear in the actual GLB triangles (not only collision-free)");
    }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        bool failed = false;
        try { await Run(); GD.Print("TRAVERSAL_ART_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("TRAVERSAL_ART_FAIL " + e.Message); failed = true; }
        GetTree().Paused = false;
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }
    private async Task Run()
    {
        foreach (string id in new[] { "ladder_section", "ladder_top", "zipline_anchor", "gondola_cabin", "gondola_station" })
            Check(PropLibrary.Find(id) is { Collision.Kind: "none" }, id + " is registered as visual-only art");
        _stage = new Stage { MapOverride = new LayoutMap(), ModeOverride = GameMode.Scenario,
            CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 0 };
        AddChild(_stage);
        await Frames(4);
        _stage.Player.ManualControl = true;
        _stage.Player.Invulnerable = true;
        foreach (var enemy in _stage.AllEnemies.ToList()) if (IsInstanceValid(enemy)) enemy.QueueFree();
        await Frames(2);
        foreach (var ladder in _stage.Ladders)
        {
            var sections = Models(ladder, "ladder_section").OrderBy(n => n.GlobalPosition.Y).ToArray();
            Check(sections.Length == ladder.Tiers, "ladder has one GLB for every tier");
            for (int i = 0; i < sections.Length; i++)
                Check(sections[i].GlobalPosition.DistanceTo(ladder.Foot + Vector3.Up * (i * 1.2f)) < .01f, "sections stack without height gaps");
            var top = Models(ladder, "ladder_top").Single();
            Check(Mathf.Abs(top.GlobalPosition.Y - ladder.Rim.Y) < .01f, "top handle pivot matches the upper floor");
            var intoTier = ((ladder.Top - ladder.Bottom) with { Y = 0 }).Normalized();
            Check((-top.GlobalBasis.Z).Dot(intoTier) > .999f, "top handles bend into the upper tier in this ladder orientation");
        }
        var line = _stage.Ziplines.Single();
        var anchors = Models(line, "zipline_anchor");
        Check(anchors.Length == 2, "zipline has two imported anchors");
        Check(Mathf.Abs(line.RidePoint(0).Y + Zipline.Hang - line.Start.Y - 3f) < .01f,
            "zipline rider hangs 2.1 m below the 3 m pulley");
        for (int i = 0; i < anchors.Length; i++)
            Check(anchors[i].ToGlobal(new Vector3(0, 3, -.4f)).DistanceTo(line.CablePoint(i)) < .01f, "cable ends at the rotated model pulley");
        for (int i = 0; i <= 24; i++)
            Check((line.CablePoint(i / 24f) - line.RidePoint(i / 24f)).DistanceTo(Vector3.Up * 2.1f) < .01f, "cable and rider use identical sag");
        var p = _stage.Player;
        var climb = _stage.Ladders.OrderByDescending(l => l.Tiers).First();
        p.TeleportTo(climb.Bottom + Vector3.Up * .05f);
        _stage.Cam.Close = true;
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(20);
        await Shot("ladder-bottom");
        Check(p.ClimbLadder(climb, true), "player starts climbing the imported ladder");
        await Frames(40);
        await Shot("ladder-climb");
        for (int i = 0; i < 360 && p.OnLadder; i++) await Frames(1);
        Check(!p.OnLadder && p.GlobalPosition.DistanceTo(climb.Top) < .2f, "player steps off at the handle's floor height");
        await Shot("ladder-top");
        p.TeleportTo(line.Start + Vector3.Up * .05f);
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(20);
        await Shot("zipline-start");
        Check(p.RideZipline(line), "player starts riding the modeled zipline");
        await Frames(90);
        await Shot("zipline-middle");
        for (int i = 0; i < 600 && p.OnZipline; i++) await Frames(1);
        Check(!p.OnZipline && p.GlobalPosition.DistanceTo(line.Landing) < .2f, "zipline arrives beside the end post on the lower ground");
        await Frames(20);
        await Shot("zipline-end");
        var gondola = _stage.Gondolas.Single();
        var cabin = Models(gondola, "gondola_cabin").Single();
        Check(Models(gondola, "gondola_station").Length == 2, "departure and arrival stations both exist");
        CheckDoorways(cabin);
        var center = gondola.GlobalPosition;
        p.TeleportTo(center + Vector3.Up * 1.2f);
        Check(!gondola.Aboard(p), "standing one tier above the platform does not count as aboard");
        p.TeleportTo(center + Vector3.Right * 3.2f);
        Check(!gondola.Aboard(p), "standing outside the cabin does not count as aboard");
        p.TeleportTo(center + Vector3.Forward * 5 + Vector3.Up * .05f);
        p.SetControls(Vector2.Zero, p.GlobalPosition + Vector3.Right * 2 + Vector3.Up, false);
        _stage.Cam.SnapTo(p.GlobalPosition + Vector3.Up * .8f);
        await Frames(45);
        var departure = Models(gondola, "gondola_station").OrderBy(s => s.GlobalPosition.DistanceTo(center)).First();
        Check(Meshes(departure).Any(m => m.Name.ToString().StartsWith("Occlude_") && m.Transparency > .7f),
            "station tower fades when the ordinary camera sees a survivor behind it");
        await Shot("gondola-tower-occlusion");
        p.TeleportTo(center + Vector3.Left * 3);
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(20);
        await Shot("gondola-approach");
        await Walk(p, center, "boarding through the west doorway");
        Check(gondola.Aboard(p), "walking into the cabin counts as aboard");
        await Shot("gondola-boarded");
        var passengers = new List<Player> { p };
        foreach (string id in new[] { "duck", "raccoon", "redpanda", "goose", "duck" })
        {
            var other = _stage.AddSurvivor(CharacterCatalog.Player(id), center + Vector3.Left * 4, true, "Rider" + passengers.Count);
            other.Invulnerable = true;
            passengers.Add(other);
        }
        Check(!gondola.TryDepart() && !_stage.AllStopped, "departure waits for all six survivors");
        for (int i = 0; i < passengers.Count; i++)
        {
            var seat = center + new Vector3((i % 3 - 1) * 1.1f, .05f, (i / 3 - .5f) * 1.1f);
            // Everyone goes through the opening, then fans out inside the cabin.
            passengers[i].TeleportTo(center + Vector3.Left * 3 + Vector3.Up * .05f);
            await Walk(passengers[i], center, "rider " + i + " enters");
            await Walk(passengers[i], seat with { Y = center.Y }, "rider " + i + " takes a place");
        }
        _stage.Cam.SnapTo(center + Vector3.Up);
        await Frames(35);
        Check(passengers.All(gondola.Aboard), "all six capsules fit on the cabin floor");
        Check(Meshes(cabin).Where(m => m.Name.ToString().StartsWith("Keep")).All(m => m.Transparency == 0), "floor and low walls stay opaque");
        Check(Meshes(cabin).Any(m => m.Name.ToString().StartsWith("Occlude_") && m.Transparency > .7f), "upper cabin frame fades for the actual camera and riders");
        await Shot("gondola-six-close");
        _stage.Cam.Close = false;
        await Frames(35);
        await Shot("gondola-six-wide");
        var before = passengers.Select(r => r.GlobalPosition).ToArray();
        var terrain = _stage.GetChildren().OfType<MeshInstance3D>().First(m => m.Visible);
        var worldLamps=_stage.GetChildren().OfType<OmniLight3D>().Where(l=>l.Visible).ToArray();
        var alreadyHidden = new Node3D { Name = "AlreadyHidden", Visible = false };
        _stage.AddChild(alreadyHidden);
        Check(gondola.TryDepart() && _stage.AllStopped, "all six depart together and the world pauses");
        await Frames(100);
        Check(gondola.Riding && gondola.BackdropTravel > 0, "fake background moves during the ride");
        var backdrop=gondola.GetNode<Node3D>("GondolaBackdrop");
        Check(backdrop.FindChildren("*",nameof(MultiMeshInstance3D),true,false).Count>0,
            "ride backdrop reuses woodland meshes instead of graybox blocks");
        Check(passengers.Select((r, i) => r.GlobalPosition.DistanceTo(before[i]) < .01f).All(v => v), "all six riders stay fixed during the ride");
        Check(!terrain.IsVisibleInTree(), "real terrain no longer covers the fake gorge during the ride");
        Check(worldLamps.All(l=>!l.IsVisibleInTree()),"real lamp fixtures do not float over the gorge backdrop");
        await Shot("gondola-riding");
        for (int i = 0; i < 1800 && gondola.Riding; i++) await Frames(1);
        Check(gondola.Completed && !_stage.AllStopped, "arrival resumes the world");
        Check(cabin.GlobalPosition.DistanceTo(gondola.Arrival) < .02f, "cabin is at the arrival station with its passengers");
        Check(terrain.IsVisibleInTree(), "arrival restores the real terrain");
        Check(worldLamps.All(l=>l.Visible),"arrival restores original world lamps");
        Check(!alreadyHidden.Visible, "arrival leaves originally hidden roots hidden");
        Check(passengers.All(r => r.GlobalPosition.DistanceTo(gondola.Arrival) < 2.5f), "all six arrive inside the destination cabin");
        Check(!gondola.TryDepart(), "a completed one-way ride cannot depart from the old station again");
        await Frames(30);
        await Shot("gondola-arrived");
        for (int i = 0; i < passengers.Count; i++)
        {
            await Walk(passengers[i], gondola.Arrival, "rider " + i + " reaches the exit aisle");
            await Walk(passengers[i], gondola.Arrival + Vector3.Right * 4, "rider " + i + " disembarks through the east doorway");
            await Walk(passengers[i], gondola.Arrival + new Vector3(6 + i, 0, 3), "rider " + i + " clears the exit");
        }
        await Frames(50);
        Check(Meshes(cabin).Where(m => m.Name.ToString().StartsWith("Occlude_")).All(m => m.Transparency < .05f),
            "cabin frame returns to opaque when everyone has disembarked");
        await Shot("gondola-disembarked");
        // Removing a ride (for example while leaving a scene) must not strand the shared tree in pause.
        var arrival = gondola.Arrival;
        var interrupted = Gondola.Create(_stage, new Rect2(arrival.X - 3, arrival.Z - 2.5f, 6, 5), arrival.Y,
            arrival + Vector3.Right * 10, Vector3.Right, 12);
        for (int i = 0; i < passengers.Count; i++)
            passengers[i].TeleportTo(arrival + new Vector3((i % 3 - 1) * 1.1f, .05f, (i / 3 - .5f) * 1.1f));
        Check(interrupted.TryDepart() && _stage.AllStopped && !terrain.Visible, "interruption fixture starts a real ride");
        interrupted.Free();
        Check(!_stage.AllStopped && !GetTree().Paused && terrain.Visible && !alreadyHidden.Visible,
            "removing a ride restores world visibility and pause state without revealing hidden roots");
        Check(PropLibrary.Errors.Count == 0, "no missing prop resources");
    }
}
