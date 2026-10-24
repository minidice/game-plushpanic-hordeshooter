using Godot;
using System;
using System.Linq;

namespace HordeShooter.Tests;

/// <summary>Isolated geometry/timeline checks and real renderer contact sheet for the wall art.</summary>
public partial class BreakableWallArtCheck : Node3D
{
    private int _checks;
    private void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++; GD.Print("PASS: " + message);
    }

    public override async void _Ready()
    {
        bool failed = false;
        try
        {
            var wall = BreakableWallArt.Create(this);
            Check(!wall.IsProcessing(), "new visual is a static snapshot");
            Check(wall.FindChildren("*", nameof(CollisionObject3D), true, false).Count == 0, "art creates no collision or damage bodies");
            var meshes = wall.FindChildren("*", nameof(MeshInstance3D), true, false).Cast<MeshInstance3D>().ToArray();
            int triangles = meshes.Sum(m => Enumerable.Range(0, m.Mesh.GetSurfaceCount()).Sum(s =>
            {
                var arrays = m.Mesh.SurfaceGetArrays(s);
                int indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array().Length;
                return (indices > 0 ? indices : arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length) / 3;
            }));
            Check(triangles < 10000, "all timeline meshes stay below 10,000 triangles: " + triangles);
            wall.SetProgress(.35f);
            var snapshot = meshes.Select(m => m.GlobalTransform).ToArray();
            wall.SetProgress(1); wall.SetProgress(.35f);
            Check(meshes.Select((m, i) => m.GlobalTransform.IsEqualApprox(snapshot[i])).All(ok => ok), "scrubbing back to .35 reproduces the same geometry transforms");
            wall.SetProgress(1);
            var bounds = new Aabb(); bool first = true;
            int corridorViolations = 0;
            foreach (var mesh in meshes.Where(m => m.IsVisibleInTree()))
            {
                var box = wall.GlobalTransform.AffineInverse() * mesh.GlobalTransform * mesh.GetAabb();
                bounds = first ? box : bounds.Merge(box); first = false;
                // Test vertices, since batched meshes can contain both disconnected side posts.
                for (int s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
                    foreach (var local in mesh.Mesh.SurfaceGetArrays(s)[(int)Mesh.ArrayType.Vertex].AsVector3Array())
                    {
                        var p = wall.GlobalTransform.AffineInverse() * mesh.GlobalTransform * local;
                        if (Mathf.Abs(p.X) < BreakableWallArt.OpeningWidth / 2 - .001f && p.Y > .065f) corridorViolations++;
                    }
            }
            Check(corridorViolations == 0, "broken corridor contains only gravel below 6.5 cm");
            GD.Print("BROKEN_WALL_BOUNDS " + bounds);
            wall.SetProgress(0); wall.PlayBreak();
            Check(wall.IsProcessing(), "PlayBreak enables visual playback");
            wall._Process(.8);
            Check(!wall.IsProcessing(), "playback settles and stops after .8 seconds");
            wall.Free();

            if (DisplayServer.GetName() != "headless")
            {
                AddChild(new WorldEnvironment { Environment = new Godot.Environment {
                    BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("303d42"),
                    AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color("c8d0d0"), AmbientLightEnergy = .65f } });
                AddChild(new DirectionalLight3D { RotationDegrees = new(-50, -35, 0), LightEnergy = 1.15f, ShadowEnabled = true });
                Mat.Mesh(this, new PlaneMesh { Size = new Vector2(18, 8) }, new(0, -.03f, 0), Mat.Solid("766e5e"));
                for (int i = 0; i < 3; i++)
                {
                    var model = BreakableWallArt.Create(this); model.Position = new((i - 1) * 5.5f, 0, 0);
                    model.SetProgress(new[] { 0f, .35f, 1f }[i]);
                    AddChild(new Label3D { Text = new[] { "INTACT / CRACKED", "BREAKING / 0.35", "BROKEN / CLEAR PASSAGE" }[i],
                        Position = model.Position + new Vector3(0, .12f, 1.8f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 42, PixelSize = .005f, NoDepthTest = true });
                }
                var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 11.6f, Position = new(3.5f, 9, 15), Current = true };
                AddChild(camera); camera.LookAt(new Vector3(0, .8f, 0));
                for (int i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/breakable-wall-art.png"));
            }
            GD.Print("BREAKABLE_WALL_ART_CHECK_PASS " + _checks);
        }
        catch (Exception e) { failed = true; GD.PushError(e.ToString()); }
        foreach (Node child in GetChildren()) child.Free();
        QuitHygiene.Collect(); GetTree().Quit(failed ? 1 : 0);
    }
}
