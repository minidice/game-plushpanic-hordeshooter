using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace HordeShooter.Tests;

public partial class AreaEffectsCheck : Node
{
    private int _checks;
    private Node3D _world = null!;
    private readonly List<CombatSurfaceEffect> _samples = new();
    private IEnumerable<T> Nodes<T>(Node node) where T : Node
    { foreach (var child in node.GetChildren()) { if (child is T value) yield return value; foreach (var value2 in Nodes<T>(child)) yield return value2; } }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        _checks++;
        GD.Print("PASS: " + message);
    }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        int exit = 0;
        try
        {
            Check(CombatEffectCatalog.Current.All.Values.Any(d => d.Family == "smoke" && d.Kind == "surface"),
                "shared catalog exposes a visual-only smoke surface for Studio");
            ValidateData();
            await Visuals();
            GD.Print("AREA_EFFECTS_PASS " + _checks);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("AREA_EFFECTS_FAIL " + e.Message); exit = 1; }
        GetTree().Paused = false;
        if (_world != null && IsInstanceValid(_world)) _world.QueueFree();
        await Frame(); await Frame();
        QuitHygiene.Collect();
        GetTree().Quit(exit);
    }

    private void ValidateData()
    {
        string json = FileAccess.GetFileAsString(CombatEffectCatalog.DataPath);
        foreach (var change in new[] { "opacity", "residue_s", "smoke_residue", "smoke_burst" })
        {
            var doc = JsonNode.Parse(json)!;
            var fire = doc["effects"]!.AsArray().First(e => e!["id"]!.GetValue<string>() == "fire_pool")!;
            var smoke = doc["effects"]!.AsArray().First(e => e!["id"]!.GetValue<string>() == "smoke_cloud")!;
            if (change == "opacity") fire[change] = 1.5;
            if (change == "residue_s") fire[change] = 10;
            if (change == "smoke_residue") smoke["residue_s"] = 1;
            if (change == "smoke_burst") smoke["kind"] = "burst";
            bool rejected = false;
            try { CombatEffectCatalog.Parse(doc.ToJsonString()); } catch (FormatException) { rejected = true; }
            Check(rejected, "reject invalid area recipe " + change);
        }
        foreach (var key in new[] { "smoke", "flame", "flow", "detail", "scorch_0", "scorch_1", "scorch_2" })
            Check(ResourceLoader.Exists(AssetPaths.File("vfx.area_" + key)), "registered texture " + key);
    }

    private void Box(Vector3 at, Vector3 size, string color)
    {
        var mesh = new MeshInstance3D { Position = at, Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(color), Roughness = 1 } };
        _world.AddChild(mesh);
    }
    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://output/area-effects/" + name + ".png")) == Error.Ok, "render " + name);
    }
    private async Task Visuals()
    {
        System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://output/area-effects"));
        _world = new Node3D { ProcessMode = ProcessModeEnum.Pausable }; AddChild(_world);
        _world.AddChild(new WorldEnvironment { Environment = new Godot.Environment {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("242a31"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = .65f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic } });
        _world.AddChild(new DirectionalLight3D { RotationDegrees = new(-55,-30,0), LightEnergy = 1 });
        var camera = new Camera3D { Position = new(12,18,23), Projection = Camera3D.ProjectionType.Orthogonal, Size = 23 };
        _world.AddChild(camera); camera.LookAt(Vector3.Zero); camera.MakeCurrent();
        for (int x = -10; x < 10; x++) for (int z = -7; z < 7; z++)
            Box(new(x+.5f,-.1f,z+.5f), new(.99f,.2f,.99f), x < 0 ? ((x+z)%2==0 ? "a3a9ad":"8a9399") : ((x+z)%2==0 ? "313a42":"3c454e"));
        Box(new(0,.6f,-3.7f),new(15,1.2f,.35f),"6c767c");
        Box(new(6,.3f,-1),new(2,.6f,2),"535c66");
        var catalog = CombatEffectCatalog.Current;
        foreach (var (at, source) in new[] { (new Vector3(-4,0,1),"fire_pool"), (new Vector3(4,0,1),"smoke_preview") })
        {
            var holder = new Node3D { Position = at }; _world.AddChild(holder);
            var sample = CombatSurfaceEffect.Create(holder, catalog.ForSource(source), 3, 5);
            sample.SetProcess(false); _samples.Add(sample);
            Check(!Nodes<CollisionObject3D>(sample).Any() && !sample.IsInGroup(SquadGroups.Hazard), source + " is visual-only");
            Check(Nodes<GpuParticles3D>(sample).All(p => p.UseFixedSeed && p.Seed == AreaSurfaceVisual.ParticleSeed && p.SpeedScale == 0), source + " uses explicit seeded GPU time");
        }
        for (int i=0;i<4;i++) await Frame();
        await Capture("t000");
        for (int frame=1;frame<=432;frame++)
        {
            foreach (var sample in _samples) if (IsInstanceValid(sample) && !sample.IsQueuedForDeletion()) sample._Process(1.0/60);
            await Frame();
            if (frame is 15 or 60 or 180 or 294 or 312 or 420) await Capture("t" + frame.ToString("000"));
            if (frame == 180)
            {
                camera.Size = 32; await Capture("far"); camera.Size = 23;
                foreach (var sample in _samples) ((Node3D)sample.GetParent()).Position *= new Vector3(-1,1,1);
                await Capture("opposite-floor");
                foreach (var sample in _samples)
                {
                    var holder = (Node3D)sample.GetParent();
                    holder.Position = new(-holder.Position.X,0,-2.5f);
                }
                await Capture("wall-tier");
                foreach (var sample in _samples)
                {
                    var holder = (Node3D)sample.GetParent(); holder.Position = new(holder.Position.X,0,1);
                }
            }
            if (frame == 312)
            {
                Check(IsInstanceValid(_samples[0]) && _samples[0].IsResidue, "fire leaves a finite harmless tail");
                Check(!IsInstanceValid(_samples[1]), "smoke dissolves without scorch");
            }
        }
        Check(_samples.All(s => !IsInstanceValid(s)), "all area visuals finish including residue");
        // Early scene removal must not transfer anything to the root.
        var owner = new Node3D(); _world.AddChild(owner);
        var early = CombatSurfaceEffect.Create(owner, catalog.ForSource("fire_pool"), 2, 5);
        owner.QueueFree(); await Frame(); await Frame();
        Check(!IsInstanceValid(early), "aborted parent leaves no residue");
        await PauseAndResume();
        for (int i=0;i<30;i++)
        {
            var holder = new Node3D(); _world.AddChild(holder);
            var sample = CombatSurfaceEffect.Create(holder,catalog.ForSource("fire_pool"),1,1);
            sample.ReleaseResidue(_world); holder.QueueFree();
        }
        Check(_world.GetChildren().OfType<CombatSurfaceEffect>().Count(s => !s.IsQueuedForDeletion()) == CombatSurfaceEffect.MaxResidues,"residue count is bounded");
    }

    private async Task PauseAndResume()
    {
        var sample = CombatSurfaceEffect.Create(_world, CombatEffectCatalog.Current.ForSource("fire_pool"), 3, 1);
        var ground = Nodes<MeshInstance3D>(sample).Select(m => m.MaterialOverride).OfType<ShaderMaterial>()
            .Single(m => m.Shader.ResourcePath.EndsWith("area_ground.gdshader"));
        for (int i=0;i<15;i++) await Frame();
        GetTree().Paused = true;
        for (int i=0;i<3;i++) await Frame();
        float before = ground.GetShaderParameter("clock").AsSingle();
        await Capture("pause-before");
        byte[]? pixels = null;
        if (DisplayServer.GetName() != "headless")
        {
            using var image = GetViewport().GetTexture().GetImage(); pixels = image.GetData();
        }
        for (int i=0;i<35;i++) await Frame();
        Check(ground.GetShaderParameter("clock").AsSingle() == before && !sample.IsResidue, "pause freezes ground clock and active lifetime");
        await Capture("pause-after");
        if (pixels != null)
        {
            using var image = GetViewport().GetTexture().GetImage();
            Check(pixels.SequenceEqual(image.GetData()), "paused rendered GPU particles remain pixel-identical");
        }
        GetTree().Paused = false;
        for (int i=0;i<12;i++) await Frame();
        Check(ground.GetShaderParameter("clock").AsSingle() > before && !sample.IsResidue, "resume continues from frozen time");
        sample.QueueFree(); await Frame(); await Frame();
    }
}
