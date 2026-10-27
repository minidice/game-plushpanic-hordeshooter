using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class BodyFluidCheck : Node
{
    private int _checks;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private bool Windowed => DisplayServer.GetName() != "headless";
    private async Task Shot(string name)
    {
        if (!Windowed) return;
        await Frames(4);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath($"res://output/body-fluid-{name}.png")) == Error.Ok, name + " render saved");
    }
    private static void Freeze(Node node)
    {
        node.SetProcess(false); node.SetPhysicsProcess(false);
        foreach (var child in node.GetChildren()) Freeze(child);
    }
    public override async void _Ready()
    {
        bool failed = false;
        try
        {
            var stage = new Stage { DirectorEnabled = false, CharacterOverride = "goose" };
            AddChild(stage); await Frames(5);
            stage.Player.ManualControl = true;
            stage.Player.Invulnerable = true;
            stage.LeaveSafeRoom();
            stage.Player.GlobalPosition = new Vector3(21, 0, .5f);
            stage.Player.SetControls(Vector2.Zero, stage.Player.GlobalPosition + new Vector3(5, 1, -1), false);
            stage.Cam.SnapTo(stage.Player.GlobalPosition);
            await Frames(20); Freeze(stage);
            var feet = stage.Player.GlobalPosition;
            var enemy = stage.SpawnCommon(feet + new Vector3(3, 0, 0), false);
            enemy.Die(Vector3.Right, 4);
            var residue = stage.Effects.GetChildren().OfType<MeshInstance3D>().Last();
            var color = residue.MaterialOverride is StandardMaterial3D mat ? mat.AlbedoColor : ((ShaderMaterial)residue.MaterialOverride).GetShaderParameter("fluid_color").AsColor();
            Check(Mathf.Max(color.R, Mathf.Max(color.G, color.B)) < .5f, "death leaves a deep muted residue instead of bright candy paint");
            var first = (BodyFluidSplat)residue;
            Check(first.IsFluid && first.Tint == BodyFluidSplat.Olive, "common death uses olive body fluid");
            var bull = stage.Spawn(MonsterDatabase.Get("bull"), feet + new Vector3(5, 0, -2), false);
            bull.Die(Vector3.Left, 4);
            Check(stage.Effects.GetChildren().OfType<BodyFluidSplat>().Last().Tint == BodyFluidSplat.Blue, "bull death uses consistent deep blue");
            int beforeBalloon = stage.Effects.SplatCount;
            var balloon = stage.Spawn(MonsterDatabase.Get("balloon"), feet + new Vector3(6, 0, 2), false);
            balloon.Die(Vector3.Up, 0);
            Check(stage.Effects.SplatCount == beforeBalloon + 4 && stage.Effects.GetChildren().OfType<BodyFluidSplat>().TakeLast(4).All(s => s.Tint == BodyFluidSplat.Ochre), "balloon keeps four stains, all ochre rather than random candy colors");
            await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
            await Frames(2);
            await Shot("stage-wet");
            Check(!stage.Effects.GetChildren().OfType<CpuParticles3D>().Any(p => p.Mesh is SphereMesh), "short fluid droplets expire after the death burst");
            first._Process(BodyFluidSplat.DrySeconds);
            Check(first.Age == BodyFluidSplat.DrySeconds && !first.IsProcessing() && IsInstanceValid(first), "drying ends updates but retains the harmless floor stain");
            int beforeBlast = stage.Effects.SplatCount;
            Blast.Explode(stage, feet + new Vector3(4, 0, 0), 1, 0, Colors.White, knockbackPower: 0);
            var scorch = stage.Effects.GetChildren().OfType<BodyFluidSplat>().Last();
            Check(stage.Effects.SplatCount == beforeBlast + 1 && !scorch.IsFluid && scorch.Tint == BodyFluidSplat.Scorch && !scorch.IsProcessing(), "explosion keeps one dry neutral mark, never body fluid");
            stage.Tiers.PaintTier(feet.X + 8, feet.X + 12, feet.Z - 2, feet.Z + 2, 1);
            stage.Effects.DeathSplat(feet + new Vector3(10, TierMap.Height, 0), MonsterDatabase.Get("spitter"), Vector3.Up);
            var high = stage.Effects.GetChildren().OfType<BodyFluidSplat>().Last();
            Check(high.GlobalPosition.Y > TierMap.Height && high.GlobalPosition.Y < TierMap.Height + .03f, "residue follows the raised floor instead of being buried at world zero");
            for (int i = 0; i < Effects.MaxSplats + 5; i++) stage.Effects.PaintSplat(feet);
            await Frames(4);
            Check(stage.Effects.SplatCount == Effects.MaxSplats && stage.Effects.GetChildren().OfType<BodyFluidSplat>().Count() == Effects.MaxSplats && !IsInstanceValid(first), "160-residue cap removes the oldest nodes and keeps counts bounded");
            var remaining = stage.Effects.GetChildren().OfType<BodyFluidSplat>().Last();
            Check(IsInstanceValid(remaining) && !remaining.IsQueuedForDeletion(), "cleanup sample is alive before the stage is removed");
            stage.QueueFree(); await Frames(3);
            Check(!IsInstanceValid(remaining), "stage cleanup frees a previously live residue node");
            if (Windowed) await Gallery();
            GD.Print("BODY_FLUID_PASS " + _checks);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("BODY_FLUID_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect(); GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Gallery()
    {
        var gallery = new Node3D(); AddChild(gallery);
        var environment = new WorldEnvironment { Environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("151b23"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = .7f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
        } };
        gallery.AddChild(environment);
        gallery.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -25, 0), LightEnergy = 1.15f });
        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 11.8f, Position = new Vector3(0, 10, 10), Current = true };
        gallery.AddChild(camera); camera.LookAt(new Vector3(0, 0, 1), Vector3.Up);
        var colors = new[] { BodyFluidSplat.Olive, BodyFluidSplat.Blue, BodyFluidSplat.Ochre };
        var labels = new[] { "OLIVE", "DEEP BLUE", "OCHRE" };
        for (int row = 0; row < 2; row++)
        {
            float z = row * 4 - 1;
            Mat.Box(gallery, new Vector3(0, -.12f, z), new Vector3(11, .2f, 3.7f), Mat.Solid(row == 0 ? "333c46" : "c2b79e"));
            for (int i = 0; i < 3; i++)
            {
                var mark = BodyFluidSplat.Create(gallery, new Vector3((i - 1) * 3.5f, .015f, z), 1.55f, colors[i], 2 + i * 17);
                mark._Process(.8); mark.SetProcess(false);
                gallery.AddChild(new Label3D { Text = labels[i], Position = new Vector3((i - 1) * 3.5f, .1f, z + 1.5f),
                    FontSize = 36, PixelSize = .008f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    Modulate = row == 0 ? new Color("b4bbc2") : new Color("474b50"), OutlineSize = 0 });
            }
        }
        await Shot("palette-wet");
        foreach (var mark in gallery.GetChildren().OfType<BodyFluidSplat>()) mark._Process(BodyFluidSplat.DrySeconds);
        await Shot("palette-dry");
        await Frames(15); gallery.QueueFree(); await Frames(3);
    }
}
