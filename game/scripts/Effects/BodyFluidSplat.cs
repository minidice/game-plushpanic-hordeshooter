using Godot;

namespace HordeShooter;

/// <summary>Harmless, bounded floor residue. One plane carries the pooled fluid and nearby droplets.</summary>
public partial class BodyFluidSplat : MeshInstance3D
{
    public const float DrySeconds = 25;
    public static readonly Color Olive = new("465124"), Blue = new("243e55"), Ochre = new("685025"), Scorch = new("28251f");
    private static readonly PlaneMesh Surface = new() { Size = Vector2.One * 2 };
    private static readonly Shader FluidShader = new() { Code = @"
shader_type spatial;
render_mode cull_disabled, depth_draw_never, shadows_disabled;
uniform vec4 fluid_color : source_color = vec4(0.2, 0.25, 0.1, 1.0);
uniform float seed = 0.0;
uniform float age = 0.0;
uniform bool fluid = true;
float hash(float v) { return fract(sin(v * 127.1 + seed * 311.7) * 43758.5453); }
float soft_union(float a, float b, float k) {
    float h = max(k - abs(a - b), 0.0) / k;
    return min(a, b) - h * h * k * 0.25;
}
float pool(vec2 p) {
    float d = length(p * vec2(1.0, 1.28)) - 0.51;
    for (int i = 0; i < 6; i++) {
        float f = float(i);
        float a = f * 1.0472 + hash(f + 1.0) * 0.65;
        vec2 center = vec2(cos(a), sin(a)) * (0.30 + hash(f + 8.0) * 0.12);
        float r = 0.18 + hash(f + 16.0) * 0.15;
        d = soft_union(d, length(p - center) - r, 0.14);
    }
    // A short dragged tongue, still joined to the central pool rather than a distant watery spray.
    d = soft_union(d, (length((p - vec2(0.05, 0.49)) / vec2(0.16, 0.32)) - 1.0) * 0.16, 0.09);
    d += sin(p.x * 19.0 + seed) * sin(p.y * 16.0 - seed) * 0.009;
    for (int i = 0; i < 5; i++) {
        float f = float(i);
        float a = f * 1.2566 + hash(f + 24.0) * 0.6;
        vec2 center = vec2(cos(a), sin(a)) * (0.78 + hash(f + 32.0) * 0.08);
        vec2 q = (p - center) * vec2(1.0, 1.2);
        d = min(d, length(q) - (0.026 + hash(f + 40.0) * 0.042));
    }
    return d;
}
void fragment() {
    float spread = fluid ? mix(0.82, 1.0, smoothstep(0.0, 0.4, age)) : 1.0;
    vec2 p = (UV - vec2(0.5)) * 2.0 / spread;
    float d = pool(p);
    float aa = max(fwidth(d), 0.002);
    float coverage = 1.0 - smoothstep(-aa, aa, d);
    if (coverage < 0.01) discard;
    float wet = fluid ? 1.0 - smoothstep(2.0, 25.0, age) : 0.0;
    float depth = 1.0 - smoothstep(0.1, 0.7, length(p * vec2(1.0, 1.25)));
    float variation = sin(p.x * 9.0 + seed) * sin(p.y * 11.0 + seed) * 0.02;
    float lip = exp(-pow((d + 0.024) / 0.016, 2.0));
    vec3 base = fluid_color.rgb * (mix(0.88, 0.64, depth) + variation - lip * 0.18);
    // A small broad sheen, never a luminous outline or a full glossy disc.
    float sheen = exp(-dot((p - vec2(-0.18, -0.14)) * vec2(5.0, 15.0), (p - vec2(-0.18, -0.14)) * vec2(5.0, 15.0)));
    float rim_light = lip * smoothstep(0.10, 0.45, -p.x - p.y);
    ALBEDO = base * mix(0.68, 1.0, wet) + mix(fluid_color.rgb, vec3(0.35), 0.35) * (sheen * 0.55 + rim_light * 0.20) * wet;
    NORMAL_MAP = vec3(0.5 + p.x * lip * 0.12 * wet, 0.5 - p.y * lip * 0.12 * wet, 1.0);
    ROUGHNESS = mix(0.96, 0.30, wet);
    SPECULAR = mix(0.10, 0.65, wet);
    ALPHA = coverage * (fluid ? 0.96 : 0.55);
}" };
    private readonly ShaderMaterial _material = new() { Shader = FluidShader };
    public float Age { get; private set; }
    public bool IsFluid { get; private set; }
    public Color Tint { get; private set; }

    public static Color ColorFor(MonsterDefinition monster) => monster.Behavior switch
    {
        MonsterBehavior.Balloon => Ochre,
        MonsterBehavior.Bull or MonsterBehavior.Boss or MonsterBehavior.Leaper or MonsterBehavior.Tether or MonsterBehavior.Shooter => Blue,
        _ => Olive,
    };

    public static BodyFluidSplat Create(Node parent, Vector3 at, float radius, Color color, float seed, bool fluid = true)
    {
        var splat = new BodyFluidSplat { Mesh = Surface, Scale = Vector3.One * radius, Position = at,
            CastShadow = ShadowCastingSetting.Off, IsFluid = fluid, Tint = color };
        splat._material.SetShaderParameter("fluid_color", color);
        splat._material.SetShaderParameter("seed", seed);
        splat._material.SetShaderParameter("fluid", fluid);
        splat.MaterialOverride = splat._material;
        parent.AddChild(splat);
        splat.SetProcess(fluid);
        return splat;
    }

    public override void _Process(double delta)
    {
        Age = Mathf.Min(DrySeconds, Age + (float)delta);
        _material.SetShaderParameter("age", Age);
        if (Age >= DrySeconds) SetProcess(false);
    }
}
