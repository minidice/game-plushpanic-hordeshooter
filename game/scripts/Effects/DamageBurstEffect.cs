using Godot;

namespace HordeShooter;

/// <summary>One depth-tested billboard: crisp impact rays or a wider death pop. No gameplay or light.</summary>
public partial class DamageBurstEffect : Node3D
{
    public static readonly Color CriticalColor = new("ffd34a");
    private static readonly QuadMesh Quad = new() { Size = Vector2.One };
    private static readonly Shader BurstShader = GD.Load<Shader>("res://shaders/damage_burst.gdshader");
    private MeshInstance3D _surface = null!;
    private ShaderMaterial _material = null!;
    private float _age, _diameter, _frontOffset;
    private bool _death, _critical;

    public static DamageBurstEffect Play(Node3D parent, Vector3 at, float height, float radius, bool death, bool critical = false)
    {
        var effect = new DamageBurstEffect
        {
            Name = death ? "DeathBurst" : "HitBurst",
            _death = death,
            _critical = critical,
            _diameter = death ? Mathf.Clamp(height * 1.65f, 2.2f, 4f) : Mathf.Clamp(height * 1.45f, 1.8f, 3.1f),
            _frontOffset = Mathf.Clamp(radius, .2f, 1f),
        };
        parent.AddChild(effect);
        effect.GlobalPosition = at;
        effect.UpdateLook();
        return effect;
    }

    public override void _Ready()
    {
        _material = new ShaderMaterial { Shader = BurstShader };
        _material.SetShaderParameter("death", _death);
        _surface = new MeshInstance3D
        {
            Name = "DamageRays", Mesh = Quad, MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_surface);
    }

    // Frequent shotgun pellets and burn ticks refresh one surface instead of stacking lights/particles.
    public void Restart(bool critical) { _age = 0; _critical = critical; UpdateLook(); }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= (_death ? .32f : .16f)) { QueueFree(); return; }
        UpdateLook();
    }

    private void UpdateLook()
    {
        _material.SetShaderParameter("progress", _age / (_death ? .32f : .16f));
        _material.SetShaderParameter("tint", _critical ? CriticalColor : Colors.White);
        var view = GetViewport().GetCamera3D()?.GlobalBasis.Orthonormalized() ?? Basis.Identity;
        // Bring the plane just in front of the body's centre while retaining wall depth occlusion.
        _surface.GlobalTransform = new Transform3D(view.Scaled(Vector3.One * _diameter), GlobalPosition + view.Z * _frontOffset);
    }
}
