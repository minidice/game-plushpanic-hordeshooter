using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Textured GPU cards with soft intersections; no ground paint. Flame roots stay in place.</summary>
public partial class AreaSurfaceVisual : Node3D
{
    private readonly List<(GpuParticles3D particles, StandardMaterial3D material, float alpha)> _layers = new();
    private readonly List<ShaderMaterial> _timelines = new();
    private CombatEffectDefinition _def = null!;
    private float _radius;
    private float _lifetime;
    public const uint ParticleSeed = 6517;

    public static AreaSurfaceVisual Create(Node3D parent, CombatEffectDefinition def, float radius, float lifetime)
    {
        var visual = new AreaSurfaceVisual { Name = "AreaLayers", _def = def, _radius = radius, _lifetime = lifetime };
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
            Layer("ThinSmoke", false, Math.Max(4, _def.Lobes * 2 / 3), _def.Opacity * .46f, _radius * 1.05f, _radius * .65f, 1.8f, 2.2f);
            if (_def.Particles > 0) Layer("Embers", true, _def.Particles, .7f, .045f, .09f, 1.1f, 1.2f, true);
        }
        else Layer("LowSmoke", false, Math.Max(1, _def.Lobes), _def.Opacity * .9f, _radius * 1.05f, _radius * .65f, 2.3f, 3.2f);
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
        if (!ember)
        {
            var timeline = new ShaderMaterial { Shader = GD.Load<Shader>(flame
                ? "res://shaders/area_flame_particles.gdshader" : "res://shaders/area_smoke_particles.gdshader") };
            // Flame roots must reach the damaging edge; spread still controls decorative smoke/embers.
            timeline.SetShaderParameter("root_radius", _radius * (flame ? .94f : _def.Spread));
            timeline.SetShaderParameter("root_count", amount);
            timeline.SetShaderParameter("card_height", height);
            timeline.SetShaderParameter("active_lifetime", _lifetime);
            timeline.SetShaderParameter("tail_seconds", _def.ResidueSeconds);
            if (!flame)
            {
                // A broad, low volume connects the flame roots before the outer flames finish igniting.
                bool fireFill = _def.Family == "fire";
                timeline.SetShaderParameter("spread_seconds", fireFill ? .38f : .95f);
                timeline.SetShaderParameter("appear_seconds", fireFill ? .24f : .55f);
                timeline.SetShaderParameter("radial_delay", fireFill ? .12f : .45f);
            }
            _timelines.Add(timeline);
            motion = timeline;
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
        _layers.Add((particles, material, alpha));
    }

    // Explicit time keeps runtime pause and Studio's manual frame stepping equivalent.
    public void Advance(float delta, float age, bool active)
    {
        foreach (var timeline in _timelines) timeline.SetShaderParameter("clock", age);
        foreach (var (particles, material, alpha) in _layers)
        {
            bool shaped = particles.ProcessMaterial is ShaderMaterial;
            // Analytic slots keep processing through their individual fade, without a global cutoff.
            if (delta > 0) particles.RequestParticlesProcess(active || shaped ? delta : 0, active || shaped ? 0 : delta);
            float visibility = shaped ? 1 : Mathf.SmoothStep(0, .6f, age)
                * (1 - Mathf.SmoothStep(Math.Max(0, _lifetime - .35f), _lifetime + Math.Min(.35f, _def.ResidueSeconds), age));
            var color = material.AlbedoColor;
            if (_def.Family == "fire" && particles.Name == "ThinSmoke")
            {
                // Warm translucent combustion volume cools into ordinary smoke at hazard expiry.
                float cool = Mathf.SmoothStep(Math.Max(0, _lifetime - .35f), _lifetime + Math.Min(.35f, _def.ResidueSeconds), age);
                color = _def.Color.Lerp(_def.Accent, .38f).Lerp(_def.SmokeColor, cool);
            }
            color.A = alpha * visibility;
            material.AlbedoColor = color;
        }
    }
}
