using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Textured GPU cards with soft intersections; no ground paint. Flame roots stay in place.</summary>
public partial class AreaSurfaceVisual : Node3D
{
    private readonly List<(GpuParticles3D particles, StandardMaterial3D material, float alpha, bool flame)> _layers = new();
    private ShaderMaterial? _flameMotion;
    private CombatEffectDefinition _def = null!;
    private float _radius;
    public const uint ParticleSeed = 6517;

    public static AreaSurfaceVisual Create(Node3D parent, CombatEffectDefinition def, float radius)
    {
        var visual = new AreaSurfaceVisual { Name = "AreaLayers", _def = def, _radius = radius };
        parent.AddChild(visual);
        visual.Build();
        return visual;
    }

    private static Texture2D Texture(string key) => GD.Load<Texture2D>(AssetPaths.File("vfx.area_" + key))
        ?? throw new InvalidOperationException("Missing area texture: " + key);

    private void Build()
    {
        bool fire = _def.Family == "fire";
        if (fire)
        {
            Layer("Flames", true, Math.Max(1, _def.Lobes), _def.Opacity, _radius * .31f, Math.Max(.45f, _def.Rise * 1.65f), .75f, 1.25f);
            Layer("ThinSmoke", false, Math.Max(4, _def.Lobes / 2), _def.Opacity * .22f, _radius * .65f, _radius * .43f, 1.8f, 2.2f);
            if (_def.Particles > 0) Layer("Embers", true, _def.Particles, .7f, .045f, .09f, 1.1f, 1.2f, true);
        }
        else Layer("LowSmoke", false, Math.Max(1, _def.Lobes), _def.Opacity, _radius * .85f, _radius * .65f, 2.3f, 3.2f);
    }

    private void Layer(string name, bool flame, int amount, float alpha, float width, float height, float life, float warmup, bool ember = false)
    {
        var gradient = new Gradient();
        gradient.Offsets = new[] { 0f, .16f, .65f, 1f };
        gradient.Colors = new[] { new Color(1,1,1,0), Colors.White, new Color(1,1,1,.72f), new Color(1,1,1,0) };
        var process = new ParticleProcessMaterial {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
            EmissionRingAxis = Vector3.Up, EmissionRingHeight = .02f,
            EmissionRingRadius = _radius * _def.Spread, EmissionRingInnerRadius = 0,
            Direction = flame ? Vector3.Up : new Vector3(.35f,.18f,.1f), Spread = flame ? 8 : 35,
            Gravity = Vector3.Zero, InitialVelocityMin = flame ? .04f : .08f,
            InitialVelocityMax = ember ? 1.2f : flame ? .12f : Math.Max(.08f, _def.Rise),
            ScaleMin = .7f, ScaleMax = 1.2f,
            AngleMin = flame ? 0 : -35, AngleMax = flame ? 0 : 35,
            AngularVelocityMin = flame ? 0 : -6, AngularVelocityMax = flame ? 0 : 6,
            AnimOffsetMin = 0, AnimOffsetMax = 1,
            AnimSpeedMin = flame ? .65f : 0, AnimSpeedMax = flame ? 1f : 0,
            ColorRamp = new GradientTexture1D { Gradient = gradient },
        };
        if (ember) process.InitialVelocityMin = .6f;
        Material motion = process;
        if (flame && !ember)
        {
            _flameMotion = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/area_flame_particles.gdshader") };
            _flameMotion.SetShaderParameter("root_radius", _radius * _def.Spread);
            _flameMotion.SetShaderParameter("root_count", amount);
            _flameMotion.SetShaderParameter("card_height", height);
            motion = _flameMotion;
        }
        var material = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            BillboardKeepScale = true, VertexColorUseAsAlbedo = true,
            AlbedoTexture = Texture(flame ? "flame" : "smoke"),
            AlbedoColor = flame ? Colors.White : _def.SmokeColor,
            ParticlesAnimHFrames = flame ? 4 : 2, ParticlesAnimVFrames = flame ? 4 : 2,
            ParticlesAnimLoop = flame,
            ProximityFadeEnabled = true, ProximityFadeDistance = flame ? .16f : .55f,
        };
        var particles = new GpuParticles3D {
            Name = name, Amount = amount, Lifetime = life, LocalCoords = true,
            ProcessMaterial = motion, DrawPass1 = new QuadMesh { Size = new Vector2(width,height), Material = material },
            Position = Vector3.Up * (flame ? height * .42f : height * .28f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            SpeedScale = 0, UseFixedSeed = true, Seed = ParticleSeed, FixedFps = 60,
            VisibilityAabb = new Aabb(new Vector3(-_radius*1.6f,-1,-_radius*1.6f),new Vector3(_radius*3.2f,_radius*2+2,_radius*3.2f)),
        };
        AddChild(particles);
        particles.RequestParticlesProcess(warmup);
        _layers.Add((particles, material, alpha, flame));
    }

    // Explicit time keeps runtime pause and Studio's manual frame stepping equivalent.
    public void Advance(float delta, float age, bool active, float remaining, float endFade = 1)
    {
        float grow = Mathf.SmoothStep(0, .32f, age);
        // With no painted ground cue, fire must already read at hazard activation.
        if (_def.Family == "fire") grow = .35f + .65f * grow;
        _flameMotion?.SetShaderParameter("clock", age);
        foreach (var (particles, material, alpha, flame) in _layers)
        {
            if (delta > 0) particles.RequestParticlesProcess(active ? delta : 0, active ? 0 : delta);
            float visibility = active ? grow * endFade : flame ? 0 : remaining;
            var color = material.AlbedoColor;
            color.A = alpha * visibility;
            material.AlbedoColor = color;
        }
    }
}
