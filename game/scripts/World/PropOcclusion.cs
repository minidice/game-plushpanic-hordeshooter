using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Only the marked upper parts of traversal props fade; floors and low walls keep their material.</summary>
public partial class PropOcclusion : Node
{
    private Stage _stage = null!;
    private readonly List<MeshInstance3D> _parts = new();

    public static void Attach(Stage stage, Node3D model)
    {
        var fade = new PropOcclusion { Name = "PropOcclusion", _stage = stage,
            ProcessMode = ProcessModeEnum.Always, ProcessPriority = 100 };
        foreach (var part in model.FindChildren("Occlude_*", "MeshInstance3D", true, false))
            fade._parts.Add((MeshInstance3D)part);
        model.AddChild(fade);
    }

    public override void _Process(double delta)
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;
        foreach (var mesh in _parts)
        {
            if (!mesh.IsVisibleInTree()) continue;
            var inverse = mesh.GlobalTransform.AffineInverse();
            var eye = inverse * camera.GlobalPosition;
            bool obscured = false;
            // AABB is deliberately conservative: a whole named frame fades together, without alpha flicker per bar.
            var bounds = mesh.GetAabb().Grow(.08f);
            foreach (var player in _stage.AlivePlayers)
            {
                if (camera.IsPositionBehind(player.GlobalPosition)) continue;
                obscured = bounds.IntersectsSegment(eye, inverse * (player.GlobalPosition + Vector3.Up * .65f))
                    || bounds.IntersectsSegment(eye, inverse * (player.GlobalPosition + Vector3.Up * 1.25f));
                if (obscured) break;
            }
            mesh.Transparency = Mathf.Lerp(mesh.Transparency, obscured ? .85f : 0f, 1 - Mathf.Exp(-(float)delta * 12f));
        }
    }
}
