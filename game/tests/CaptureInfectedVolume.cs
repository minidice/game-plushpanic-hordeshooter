using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Rendered front/side/rear comparison with an unmodified, unequipped animal reference.</summary>
public partial class CaptureInfectedVolume : Node3D
{
    public override async void _Ready()
    {
        try
        {
            GetWindow().Size = new Vector2I(1800, 600);
            AddChild(new WorldEnvironment { Environment = new Godot.Environment {
                BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("18242b"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("cbdde4"), AmbientLightEnergy = .65f,
                TonemapMode = Godot.Environment.ToneMapper.Agx } });
            AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-45, -35, 0), LightEnergy = 1.5f, ShadowEnabled = true });
            AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-20, 150, 0), LightEnergy = .5f });
            Mat.Box(this, new Vector3(0, -.12f, 0), new Vector3(16, .2f, 6), Mat.Solid("415652"));
            var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal,
                Size = 4.2f, Position = new Vector3(0, 1.05f, 16) };
            AddChild(camera);
            camera.LookAt(new Vector3(0, 1.05f, 0));
            var rigs = new List<CharacterRig>();
            int column = 0;
            foreach (var id in new[] { "raccoon", "humanoid", "crawler", "tether", "leaper", "spitter" })
            {
                var rig = CharacterRig.Create(id == "raccoon" ? CharacterCatalog.Player(id) : CharacterCatalog.Enemy(id));
                AddChild(rig);
                rig.Position = new Vector3(-5.25f + column++ * 2.1f, 0, 0);
                if (id == "raccoon") rig.ApplyAppearance(new AppearanceProfile { Hat = 0, Vest = false, Pack = false, Goggles = false });
                rig.HideBuiltInWeapon();
                rigs.Add(rig);
                AddChild(new Label3D { Text = id.ToUpperInvariant(), FontSize = 32, PixelSize = .006f,
                    Position = rig.Position + Vector3.Up * 2.35f,
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true });
            }
            string tag = "after";
            foreach (string arg in OS.GetCmdlineUserArgs())
                if (arg.StartsWith("--volume-tag=")) tag = arg["--volume-tag=".Length..];
            foreach (var (view, angle) in new[] { ("front", 0f), ("side", 90f), ("rear", 180f), ("quarter", 45f) })
            {
                foreach (var rig in rigs) rig.RotationDegrees = new Vector3(0, angle, 0);
                for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage();
                var error = image.SavePng(ProjectSettings.GlobalizePath($"res://output/infected-volume-{tag}-{view}.png"));
                if (error != Error.Ok) throw new Exception($"Saving {view} failed: {error}");
            }
            GD.Print("INFECTED_VOLUME_CAPTURE_PASS");
            QuitHygiene.Collect();
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
