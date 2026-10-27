using Godot;

namespace HordeShooter;

/// <summary>A depth-tested floor marker. Footprint is the indicated boundary; the padded plane only carries its halo.</summary>
public partial class GroundMarker : MeshInstance3D
{
    public static readonly Color Danger = new("ff5546");
    private const float Padding = .18f;
    private static readonly Shader SurfaceShader = new() { Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;
uniform vec4 tint : source_color = vec4(1.0);
uniform vec2 half_size = vec2(1.0);
uniform bool circle = true;
uniform float line_width = 0.045;
uniform float fill_alpha = 0.04;
uniform float opacity = 1.0;
uniform float progress = -1.0;
varying vec2 position_on_floor;
void vertex() { position_on_floor = VERTEX.xz; }
void fragment() {
    vec2 p = position_on_floor;
    vec2 q = abs(p) - half_size;
    float d = circle ? length(p) - half_size.x : length(max(q, vec2(0.0))) + min(max(q.x, q.y), 0.0);
    float aa = max(fwidth(d), 0.003);
    float inside = 1.0 - smoothstep(-aa, aa, d);
    // The bright edge sits inside the actual boundary. The outside halo never becomes a filled danger area.
    float edge_distance = abs(d + line_width * 0.5);
    float core = 1.0 - smoothstep(line_width * 0.25, line_width * 0.5 + aa, edge_distance);
    float halo = exp(-edge_distance / 0.055) * 0.32;
    float fill = inside * fill_alpha;
    if (progress >= 0.0) {
        float elapsed = circle ? length(p) / half_size.x : (half_size.y - p.y) / (half_size.y * 2.0);
        float charged = 1.0 - smoothstep(progress - 0.012, progress + 0.012, elapsed);
        fill += inside * charged * 0.16;
    }
    ALBEDO = mix(tint.rgb, vec3(0.96, 0.98, 1.0), core * 0.72);
    ALPHA = clamp(core * 0.88 + halo + fill, 0.0, 1.0) * opacity * tint.a;
}" };
    private readonly PlaneMesh _plane = new();
    private readonly ShaderMaterial _surface = new() { Shader = SurfaceShader };
    public Vector2 Footprint { get; private set; }

    public GroundMarker()
    {
        Mesh = _plane;
        MaterialOverride = _surface;
        CastShadow = ShadowCastingSetting.Off;
    }

    public static GroundMarker Circle(Node parent, Vector3 at, float radius, Color tint, float fill = .04f, float opacity = 1, float width = .045f)
    {
        var marker = Create(parent, at, tint, fill, opacity, width);
        marker.SetRadius(radius);
        return marker;
    }

    public static GroundMarker Lane(Node parent, Vector3 from, Vector3 to, float width, Color tint, float fill = .07f)
    {
        var marker = Create(parent, (from + to) * .5f, tint, fill, 1, Mathf.Min(.045f, width * .22f));
        marker._surface.SetShaderParameter("circle", false);
        marker.SetSize(new Vector2(width, Mathf.Max(.01f, from.DistanceTo(to))));
        if (from.DistanceSquaredTo(to) > .0001f) marker.LookAt(to, Vector3.Up);
        return marker;
    }

    private static GroundMarker Create(Node parent, Vector3 at, Color tint, float fill, float opacity, float width)
    {
        var marker = new GroundMarker { Position = at };
        marker.SetTint(tint);
        marker.SetOpacity(opacity);
        marker._surface.SetShaderParameter("fill_alpha", fill);
        marker._surface.SetShaderParameter("line_width", width);
        parent.AddChild(marker);
        return marker;
    }

    public void SetRadius(float radius) => SetSize(Vector2.One * Mathf.Max(.01f, radius) * 2);

    private void SetSize(Vector2 size)
    {
        if (Footprint.IsEqualApprox(size)) return;
        Footprint = size;
        _plane.Size = size + Vector2.One * (Padding * 2);
        _surface.SetShaderParameter("half_size", size * .5f);
    }

    public void SetTint(Color color) => _surface.SetShaderParameter("tint", color);
    public void SetOpacity(float opacity) => _surface.SetShaderParameter("opacity", Mathf.Clamp(opacity, 0, 1));
    public void SetProgress(float progress) => _surface.SetShaderParameter("progress", Mathf.Clamp(progress, 0, 1));
}
