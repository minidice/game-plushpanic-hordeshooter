using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Actual Stage 2 draft barrel damage and wall collapse, photographed from one fixed camera.</summary>
public partial class DestructionCapture : Node
{
    private Stage _stage = null!;
    private Label _caption = null!;
    private readonly List<Image> _shots = new();

    public override async void _Ready()
    {
        int exit = 0;
        try
        {
            if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("DestructionCapture requires a real renderer.");
            await Run();
            GD.Print("DESTRUCTION_CAPTURE_PASS: actual map, barrel damage, animated wall, unchanged plain panel, three rendered states");
        }
        catch (Exception e) { GD.PushError(e.ToString()); exit = 1; }
        foreach (var shot in _shots) shot.Dispose();
        foreach (Node child in GetChildren()) child.Free();
        QuitHygiene.Collect(); GetTree().Quit(exit);
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task Shot(string state, string caption)
    {
        _caption.Text = caption;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        string path = ProjectSettings.GlobalizePath("res://output/destruction-map-" + state + ".png");
        if (image.SavePng(path) != Error.Ok) throw new InvalidOperationException("Could not save " + path);
        _shots.Add(image); GD.Print("SHOT: " + path);
    }

    private async Task Run()
    {
        OS.LowProcessorUsageMode = false;
        Engine.MaxFps = 60;
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario,
            MapOverride = new LayoutMap(), DirectorEnabled = false, SquadOverride = 0 };
        AddChild(_stage);
        await Frames(4);
        foreach (var enemy in _stage.AllEnemies.ToArray()) if (IsInstanceValid(enemy)) enemy.QueueFree();
        _stage.Player.ManualControl = true; _stage.Player.Invulnerable = true;
        var placement = DestructionPlacements.Load("stage3").Single(p => p.Id == "yard_west");
        if (placement.At.DistanceTo(new Vector3(288, 1.2f, -58)) > .01f) throw new InvalidOperationException("Capture placement changed.");
        var wall = _stage.Breakables.Single(b => b.HasMeta("destruction_placement") && b.GetMeta("destruction_placement").AsString() == placement.Id);
        var barrel = _stage.Props.OfType<ExplosiveBarrel>().Single(b => b.HasMeta("destruction_placement") && b.GetMeta("destruction_placement").AsString() == placement.Id);
        var visual = wall.GetChildren().OfType<BreakableWallVisual>().Single();
        var plain = _stage.GetNode<Node3D>("ConcretePanel_" + placement.Id);
        _stage.Player.TeleportTo(placement.At + new Vector3(-3.7f, .05f, 4.2f));
        _stage.Player.SetControls(Vector2.Zero, placement.Barrel + Vector3.Up, false);
        foreach (var layer in _stage.FindChildren("*", nameof(CanvasLayer), true, false).OfType<CanvasLayer>()) layer.Visible = false;
        _stage.Cam.SetProcess(false); _stage.Cam.SetPhysicsProcess(false); _stage.Cam.Current = false;
        var target = placement.At + new Vector3(2.15f, .9f, .35f);
        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 9.2f,
            Position = target + new Vector3(4.5f, 9.5f, 14), Far = 100, Current = true };
        AddChild(camera); camera.LookAt(target);
        var overlay = new CanvasLayer(); AddChild(overlay);
        _caption = new Label { Position = new Vector2(28, 24) };
        _caption.AddThemeFontSizeOverride("font_size", 28);
        _caption.AddThemeColorOverride("font_color", new Color("f2ede1"));
        _caption.AddThemeColorOverride("font_shadow_color", new Color("18201d"));
        _caption.AddThemeConstantOverride("shadow_offset_x", 2); _caption.AddThemeConstantOverride("shadow_offset_y", 2);
        overlay.AddChild(_caption);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        await Frames(20);
        await Shot("before", "STAGE 2 / BEFORE - cracked concrete and explosive barrel");

        // Real damage entry point: the runtime fuse, Blast and ObjectBody own every subsequent effect.
        barrel.TakeDamage(barrel.Health, ObjectDamage.Gun, _stage.Player);
        int ticks = 0;
        while (IsInstanceValid(wall) && !wall.Broken && ticks++ < 120) await Frames(1);
        if (IsInstanceValid(wall) && !wall.Broken) throw new InvalidOperationException("Actual barrel failed to break the wall.");
        await ToSignal(GetTree().CreateTimer(.21), SceneTreeTimer.SignalName.Timeout);
        if (!visual.IsProcessing()) throw new InvalidOperationException("Wall collapse finished before the intermediate capture.");
        await Shot("breaking", "STAGE 2 / BREAKING - actual barrel explosion and falling panels");
        await ToSignal(GetTree().CreateTimer(.9), SceneTreeTimer.SignalName.Timeout);
        if (IsInstanceValid(wall) || IsInstanceValid(barrel) || !IsInstanceValid(plain) || visual.IsProcessing())
            throw new InvalidOperationException("Unexpected settled destruction state.");
        await Shot("after", "STAGE 2 / AFTER - open passage, permanent posts and intact neighbouring wall");

        using var sheet = Image.CreateEmpty(2400, 450, false, Image.Format.Rgb8);
        for (int i = 0; i < _shots.Count; i++)
        {
            using var thumb = (Image)_shots[i].Duplicate(); thumb.Resize(800, 450, Image.Interpolation.Lanczos);
            thumb.Convert(Image.Format.Rgb8); sheet.BlitRect(thumb, new Rect2I(0, 0, 800, 450), new Vector2I(i * 800, 0));
        }
        string sheetPath = ProjectSettings.GlobalizePath("res://output/destruction-map-contact-sheet.png");
        if (sheet.SavePng(sheetPath) != Error.Ok) throw new InvalidOperationException("Could not save contact sheet.");
        GD.Print("SHOT: " + sheetPath);
    }
}
