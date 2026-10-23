using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Bundle C in the Stage 2 draft: imported geometry, interaction and rendered visibility.</summary>
public partial class LandmarkCArtCheck : Node
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
        Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/landmark-c-" + name + ".png")) == Error.Ok, "render " + name);
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
        try { await Run(); GD.Print("LANDMARK_C_ART_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("LANDMARK_C_ART_FAIL " + e.Message); failed = true; }
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }
    private async Task Run()
    {
        foreach (string id in new[] { "watchtower", "crane", "alarm_truck", "bunker_gate" })
            Check(PropLibrary.Find(id) != null, id + " is registered");
        var map = new LayoutMap();
        _stage = new Stage { MapOverride = map, ModeOverride = GameMode.Scenario,
            CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 1 };
        AddChild(_stage);
        await Frames(4);
        var p = _stage.Player;
        p.ManualControl = true;
        p.Invulnerable = true;
        var bot = _stage.Squad[0].Bot;
        _stage.Squad[0].ProcessMode = ProcessModeEnum.Disabled;
        bot.Invulnerable = true;
        foreach (var enemy in _stage.AllEnemies.ToList()) if (IsInstanceValid(enemy)) enemy.QueueFree();
        await Frames(2);
        foreach (string id in new[] { "watchtower", "crane", "alarm_truck", "bunker_gate" })
            Check(Models(_stage, id).Length == 1, id + " is placed once in the draft");

        var tower = Models(_stage, "watchtower").Single();
        Check(tower.GlobalPosition.DistanceTo(new Vector3(120, 0, -75)) < .01f, "tower pivot is ground center");
        var decks = _stage.GetChildren().OfType<MeshInstance3D>().Where(m => m.Name.ToString().StartsWith("WatchtowerDeck")).ToArray();
        Check(decks.Length > 0 && decks.All(m => Mathf.Abs(m.GetAabb().Size.Y - .3f) < .001f), "tower uses a thin visible deck");
        var ladder = _stage.Ladders.OrderBy(l => l.Top.DistanceTo(new Vector3(120, 4.8f, -75))).First();
        p.TeleportTo(ladder.Bottom + Vector3.Up * .05f);
        _stage.Cam.Close = false;
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(30);
        await Shot("tower-approach");
        Check(p.ClimbLadder(ladder, true), "tower ladder starts climbing");
        await Frames(40);
        await Shot("tower-climb");
        for (int i = 0; i < 360 && p.OnLadder; i++) await Frames(1);
        Check(!p.OnLadder && Mathf.Abs(p.GlobalPosition.Y - 4.8f) < .15f, "ladder exits onto 4.8 m deck");
        await Walk(p, new Vector3(120, 4.8f, -75), "tower reward area");
        await Frames(35);
        Check(Meshes(tower).Where(m => m.Name.ToString().StartsWith("Keep")).All(m => m.Transparency == 0), "tower foundations stay opaque");
        Check(Meshes(tower).Any(m => m.Name.ToString().StartsWith("Occlude_") && m.Transparency > .7f), "tower upper structure fades over survivor");
        await Shot("tower-interior");
        var line = _stage.Ziplines.Single();
        Check(Enumerable.Range(0, 17).All(i => ClearVolume(tower,
            new Aabb(line.RidePoint(i * .005f) - new Vector3(.48f, 0, .48f), new Vector3(.96f, 2.18f, .96f)))),
            "actual tower triangles clear the full goose body along early zipline departure");
        Check(ClearVolume(tower, new Aabb(tower.GlobalPosition + new Vector3(-6.4f, 6.85f, -4.4f), new Vector3(12.8f, .15f, 4))),
            "both eaves clear standing goose heads");
        await Walk(p, line.Start, "tower zipline exit");
        await Frames(20);
        await Shot("tower-zipline");
        Check(p.RideZipline(line), "zipline starts from modeled tower");
        await Frames(10);
        await Shot("tower-roof-clearance");
        await Frames(50);
        await Shot("tower-departure");
        for (int i = 0; i < 600 && p.OnZipline; i++) await Frames(1);
        Check(!p.OnZipline && p.GlobalPosition.DistanceTo(line.Landing) < .2f &&
            Mathf.Abs(p.GlobalPosition.Y-_stage.Tiers.GroundY(line.End))<.15f,
            "tower zipline lands beside the end post on the lower tier");

        var crane = Models(_stage, "crane").Single();
        Check(Mathf.Abs(crane.GlobalPosition.Y - 1.2f) < .01f, "crane base matches yard tier");
        p.TeleportTo(crane.GlobalPosition + Vector3.Back * 5 + Vector3.Up * .05f);
        p.SetControls(Vector2.Zero, crane.GlobalPosition + Vector3.Up, false);
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(35);
        await Shot("crane-approach");
        // Inspection overview only; ordinary-camera checks above/below keep the map's view.
        var ordinaryView = _stage.Cam.View;
        _stage.Cam.View = ordinaryView with { WideSize = 36 };
        await Frames(80);
        await Shot("crane-overview");
        _stage.Cam.View = ordinaryView;
        await Frames(60);
        p.SetControls(Vector2.Up, crane.GlobalPosition + Vector3.Up, false);
        await Frames(90);
        p.SetControls(Vector2.Zero, crane.GlobalPosition + Vector3.Up, false);
        Check(p.GlobalPosition.Z > crane.GlobalPosition.Z + 1.2f && p.GlobalPosition.Z < crane.GlobalPosition.Z + 2, "crane base physically blocks walking");
        p.TeleportTo(crane.GlobalPosition + Vector3.Forward * 3 + Vector3.Up * .05f);
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(40);
        Check(Meshes(crane).Any(m => m.Name.ToString().StartsWith("Occlude_") && m.Transparency > .7f), "crane mast fades behind survivor");
        Check(Meshes(crane).Where(m => m.Name.ToString().StartsWith("Keep")).All(m => m.Transparency == 0), "crane base stays opaque");
        await Shot("crane-occlusion");

        var gate = Models(_stage, "bunker_gate").Single();
        Check(Mathf.Abs(gate.GlobalPosition.Y - 4.8f) < .01f, "bunker gate rests on plateau tier");
        p.TeleportTo(gate.GlobalPosition + Vector3.Back * 4 + Vector3.Up * .05f);
        p.SetControls(Vector2.Zero, gate.GlobalPosition + Vector3.Up, false);
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(35);
        await Shot("bunker-closed");
        p.SetControls(Vector2.Up, gate.GlobalPosition + Vector3.Up, false);
        await Frames(90);
        p.SetControls(Vector2.Zero, gate.GlobalPosition + Vector3.Up, false);
        Check(p.GlobalPosition.Z > gate.GlobalPosition.Z + .7f && p.GlobalPosition.Z < gate.GlobalPosition.Z + 1.5f, "closed bunker door physically blocks entry");
        await Walk(p, gate.GlobalPosition + new Vector3(6, 0, 1.2f), "bunker east bypass");
        await Walk(p, gate.GlobalPosition + new Vector3(6, 0, -3), "around bunker frame");
        var gondola = _stage.Gondolas.Single();
        await Walk(p, gondola.GlobalPosition + Vector3.Left * 3, "gondola approach beyond bunker");
        await Walk(p, gondola.GlobalPosition, "gondola boarding beyond bunker");
        Check(gondola.Aboard(p), "bunker leaves gondola boarding accessible");
        await Shot("bunker-gondola");
        p.TeleportTo(gate.GlobalPosition + Vector3.Forward * 2 + Vector3.Up * .05f);
        p.SetControls(Vector2.Zero, p.GlobalPosition + Vector3.Right * 2, false);
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(40);
        Check(Meshes(gate).Any(m => m.Name.ToString().StartsWith("Occlude_") && m.Transparency > .7f), "gate upper wall fades behind survivor");
        await Shot("bunker-occlusion");

        var car = map.AlarmDevice!;
        Check(_stage.AlarmCars.Contains(car) && !car.Triggered, "draft exposes the modeled alarm truck");
        var truck = Models(car, "alarm_truck").Single();
        Check(Mathf.Abs(truck.GlobalPosition.Y - 1.2f) < .01f, "truck ground pivot matches yard floor");
        var shape = car.GetChildren().OfType<CollisionShape3D>().Single();
        Check(shape.Shape is BoxShape3D b && b.Size.DistanceTo(new Vector3(5.2f, 2, 2.2f)) < .001f, "truck collision matches brief");
        var lights = Meshes(truck).SelectMany(m => Enumerable.Range(0, m.Mesh.GetSurfaceCount()).Select(i => m.GetSurfaceOverrideMaterial(i)))
            .OfType<StandardMaterial3D>().Distinct().ToArray();
        Check(lights.Length == 2 && lights.All(m => m.EmissionEnabled), "truck binds red and amber model lights");
        p.TeleportTo(truck.GlobalPosition + Vector3.Left * 8 + Vector3.Up * .05f);
        bot.TeleportTo(truck.GlobalPosition + Vector3.Back * 5 + Vector3.Up * .05f);
        bot.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        var aim = car.GlobalPosition + Vector3.Up * .4f;
        await Frames(2);
        Check(Rays.Cast(bot, bot.GlobalPosition + Vector3.Up * Player.AimHeight, aim, Layers.World)?.Collider == car, "AI has unobstructed shot at truck");
        for (int i = 0; i < 30; i++) { bot.SetControls(Vector2.Zero, aim, true); await Frames(1); }
        bot.SetControls(Vector2.Zero, aim, false);
        Check(bot.ShotsFired > 0 && !car.Triggered, "AI bullets leave truck quiet");
        bot.TeleportTo(new Vector3(10, 0, -10));
        p.TeleportTo(truck.GlobalPosition + Vector3.Back * 5 + Vector3.Up * .05f);
        p.SetControls(Vector2.Zero, aim, false);
        _stage.Cam.Close = true;
        _stage.Cam.SnapTo(p.GlobalPosition);
        await Frames(35);
        await Shot("truck-idle");
        p.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        p.SetControls(Vector2.Zero, aim, true);
        for (int i = 0; i < 30 && !car.Triggered; i++) await Frames(1);
        p.SetControls(Vector2.Zero, aim, false);
        await Frames(5);
        Check(car.Triggered && car.Sounding && _stage.Director.AlarmRemaining > 0, "player shot starts truck alarm and director event");
        Check(_stage.Sfx.Played.GetValueOrDefault("alarm_loop") > 0, "truck starts real siren stream");
        Check(lights.Any(m => m.EmissionEnergyMultiplier > 0), "model lights flash during alarm");
        await Shot("truck-alarm");
        int mobs = _stage.Director.MobsSpawned;
        _stage.DirectorEnabled = true;
        await Frames(120);
        Check(_stage.Director.MobsSpawned > mobs && _stage.AllEnemies.Any(e => e.Reaction == Reaction.Hunter), "draft truck alarm summons hunter wave");
        _stage.DirectorEnabled = false;
        foreach (var enemy in _stage.AllEnemies.ToList()) if (IsInstanceValid(enemy)) enemy.QueueFree();
        var second = AlarmCar.Create(_stage, car.GlobalPosition + Vector3.Right * 10, new Vector3(5.2f, 2, 2.2f), "c7a35c", PropLibrary.Instance("alarm_truck"));
        var secondLights = Meshes(second).SelectMany(m => Enumerable.Range(0, m.Mesh.GetSurfaceCount()).Select(i => m.GetSurfaceOverrideMaterial(i))).OfType<StandardMaterial3D>().ToArray();
        Check(secondLights.All(m => !lights.Contains(m)) && !second.Triggered, "truck instances own independent light materials");
        Blast.Explode(_stage, second.GlobalPosition, 2.5f, 0, new Color("ff8844"));
        await Frames(2);
        Check(second.Triggered && second.Sounding, "explosion triggers a modeled truck");
        await Frames((int)(60 * _stage.Director.Rules.AlarmTime) + 10);
        Check(!car.Sounding && !second.Sounding && _stage.Director.AlarmRemaining <= 0, "alarms finish after configured duration");
        Check(lights.All(m => m.EmissionEnergyMultiplier == 0), "truck lights switch off at alarm end");
        car.Trigger();
        await Frames(2);
        Check(!car.Sounding, "truck alarm cannot be triggered twice");
        await Shot("truck-spent");
        Check(new[] { tower, crane, gate }.All(n => Meshes(n).Where(m => m.Name.ToString().StartsWith("Occlude_")).All(m => m.Transparency < .05f)), "all landmark upper parts return opaque after leaving");
        Check(new[] { tower, crane, gate }.All(n => Meshes(n).Where(m => m.Name.ToString().StartsWith("Keep")).All(m => m.Transparency == 0)), "all landmark foundations remain opaque");
        Check(PropLibrary.Errors.Count == 0, "no missing prop resources");
    }
}
