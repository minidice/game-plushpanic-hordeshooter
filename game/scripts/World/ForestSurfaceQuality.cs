using Godot;

namespace HordeShooter;

/// <summary>First surface-quality study in the original forest (now Stage 3).
/// Keep bounds/fade synchronized with forest_patch.gdshaderinc and forest_grass.gdshader.</summary>
public static class ForestSurfaceQuality
{
    public static readonly Rect2 Bounds = new(64, -42, 44, 40);
    public const float Fade = 4;

    public static float Weight(Vector2 point) =>
        Mathf.SmoothStep(Bounds.Position.X, Bounds.Position.X + Fade, point.X) *
        (1 - Mathf.SmoothStep(Bounds.End.X - Fade, Bounds.End.X, point.X)) *
        Mathf.SmoothStep(Bounds.Position.Y, Bounds.Position.Y + Fade, point.Y) *
        (1 - Mathf.SmoothStep(Bounds.End.Y - Fade, Bounds.End.Y, point.Y));
}
