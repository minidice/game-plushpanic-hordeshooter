using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>Visual burst assembled from shared meshes. Never rolls gameplay RNG or creates collision.</summary>
public partial class CombatBurstEffect : Node3D
{
    public const int MaxBursts = 24, MaxLights = 4;
    private const string BurstGroup = "combat_visual_burst", LightGroup = "combat_visual_light";
    internal static readonly SphereMesh Blob = new() { Radius = .5f, Height = 1, RadialSegments = 12, Rings = 6 };
    internal static readonly BoxMesh Shard = new() { Size = Vector3.One };
    private sealed record Part(MeshInstance3D Mesh, Vector3 Start, Vector3 Velocity, float Size, float Delay, int Kind, int Index);
    private readonly List<Part> _parts = new();
    private CombatEffectDefinition _def = null!;
    private ShaderMaterial _hot = null!, _smoke = null!;
    private ShaderMaterial? _ring;
    private OmniLight3D? _light;
    private BarrelBurstVisual? _fuel;
    private ImpactBurstVisual? _impact;
    private float _age, _radius;
    public string EffectId => _def.Id;
    public float Age => _age;
    public int VisualParts => _parts.Count + (_fuel?.VisualParts ?? 0) + (_impact?.VisualParts ?? 0);

    internal static ShaderMaterial Volume(Color color, Color accent, float heat)
    {
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/combat_volume.gdshader") };
        mat.SetShaderParameter("tint", color); mat.SetShaderParameter("accent", accent); mat.SetShaderParameter("heat", heat);
        return mat;
    }
    internal static ShaderMaterial Ground(CombatEffectDefinition def, int mode)
    {
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/combat_ground.gdshader") };
        mat.SetShaderParameter("tint", def.Color); mat.SetShaderParameter("accent", def.Accent); mat.SetShaderParameter("mode", mode);
        return mat;
    }
    internal static MeshInstance3D Mesh(Node3D parent, Mesh mesh, Material material)
    {
        var node = new MeshInstance3D { Mesh = mesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        parent.AddChild(node); return node;
    }

    public static CombatBurstEffect Play(Node3D parent, CombatEffectDefinition def, Vector3 at, float radius, Vector3 direction = default, float? floorY = null)
    {
        if (def.Kind != "burst" || !float.IsFinite(radius) || radius <= 0) throw new ArgumentException("Invalid burst input");
        var effect = new CombatBurstEffect { Name = "CombatBurst_" + def.Id, _def = def, _radius = radius, Position = at };
        parent.AddChild(effect);
        var active = parent.GetTree().GetNodesInGroup(BurstGroup).OfType<CombatBurstEffect>().Where(n => !n.IsQueuedForDeletion()).ToArray();
        // Decoration alone is capped. Gameplay explosions have already happened independently.
        foreach (var old in active.Take(Math.Max(0, active.Length - MaxBursts + 1))) { old.Visible = false; old.QueueFree(); }
        effect.AddToGroup(BurstGroup);
        effect.Build(direction, (floorY ?? at.Y) - at.Y);
        effect.UpdateVisual();
        return effect;
    }

    private void Build(Vector3 direction, float floorOffset)
    {
        if (_def.Id == "barrel")
        {
            _fuel = BarrelBurstVisual.Create(this, _def, _radius);
            BuildLight();
            return;
        }
        if (_def.Family == "blast")
        {
            _impact = ImpactBurstVisual.Create(this, _def, _radius, direction, floorOffset);
            BuildLight();
            return;
        }
        bool poison = _def.Family == "poison";
        var random = new RandomNumberGenerator { Seed = 7417 };
        _hot = Volume(_def.Color, _def.Accent, poison ? .12f : 1.4f);
        _smoke = Volume(_def.SmokeColor, _def.SmokeColor.Lightened(.18f), 0);
        if (_def.Family != "fire")
        {
            _ring = Ground(_def, 0);
            var ring = Mesh(this, new PlaneMesh { Size = Vector2.One * _radius * 2 }, _ring);
            ring.Position = new(0, floorOffset + .055f, 0);
        }
        Vector3 directed = direction.LengthSquared() > .001f ? direction.Normalized() : Vector3.Zero;
        for (int i = 0; i < _def.Lobes; i++)
        {
            float angle = i * Mathf.Tau / Math.Max(1, _def.Lobes) + .17f;
            var outward = new Vector3(Mathf.Cos(angle), .18f + random.Randf() * .3f, Mathf.Sin(angle));
            var velocity = outward * _radius * _def.Spread * .65f + directed * _radius * .35f;
            float size = _radius * random.RandfRange(.28f, .43f);
            AddPart(Blob, _hot, new(0, .22f, 0), velocity, size, i * .009f, 0, i);
            if (!poison && _def.Family != "fire")
                AddPart(Blob, _smoke, outward * _radius * .12f + Vector3.Up * .3f,
                    outward * _radius * .4f, size * 1.12f, .12f + i * .013f, 1, i);
        }
        var spark = Mat.Glow(poison ? _def.Accent : _def.Accent.Lightened(.25f), poison ? .3f : 1.2f);
        if (!poison)
            for (int i = 0; i < 7; i++)
                AddPart(Shard, spark, Vector3.Up * .4f, Vector3.Right.Rotated(Vector3.Up, i * Mathf.Tau / 7), _radius, 0, 3, i);
        for (int i = 0; i < _def.Particles; i++)
        {
            float angle = random.RandfRange(0, Mathf.Tau);
            var velocity = new Vector3(Mathf.Cos(angle), random.RandfRange(.25f, .85f), Mathf.Sin(angle)) * random.RandfRange(2.5f, 6.5f);
            velocity += directed * 2;
            AddPart(poison ? Blob : Shard, spark, Vector3.Up * .25f, velocity, random.RandfRange(.045f, .085f), 0, 2, i);
        }
        BuildLight();
    }
    private void BuildLight()
    {
        if (_def.LightEnergy > 0 && GetTree().GetNodesInGroup(LightGroup).Count(n => !n.IsQueuedForDeletion()) < MaxLights)
        {
            _light = new OmniLight3D { Position = Vector3.Up * 1.2f, OmniRange = _radius * 2, LightColor = _def.Color,
                LightEnergy = _def.LightEnergy, ShadowEnabled = false };
            AddChild(_light); _light.AddToGroup(LightGroup);
        }
    }
    private void AddPart(Mesh mesh, Material mat, Vector3 start, Vector3 velocity, float size, float delay, int kind, int index)
    {
        var node = Mesh(this, mesh, mat);
        _parts.Add(new(node, start, velocity, size, delay, kind, index));
    }
    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= _def.Duration) { QueueFree(); return; }
        UpdateVisual();
    }
    private void UpdateVisual()
    {
        if (_light != null) _light.LightEnergy = _def.LightEnergy * Mathf.Pow(Mathf.Max(0, 1 - _age / .22f), 2);
        if (_fuel != null) { _fuel.Advance(_age); return; }
        if (_impact != null) { _impact.Advance(_age); return; }
        _hot.SetShaderParameter("clock", _age); _smoke.SetShaderParameter("clock", _age);
        _ring?.SetShaderParameter("clock", _age);
        foreach (var part in _parts)
        {
            float elapsed = _age - part.Delay;
            float life = part.Kind == 3 ? .17f : part.Kind == 0 ? _def.Duration * .52f : _def.Duration - part.Delay;
            float t = Mathf.Clamp(elapsed / life, 0, 1);
            part.Mesh.Visible = elapsed >= 0 && t < 1;
            if (!part.Mesh.Visible) continue;
            if (part.Kind == 3)
            {
                part.Mesh.Position = part.Start + part.Velocity * _radius * (.1f + t*.35f);
                part.Mesh.Scale = new Vector3(_radius*(.25f+t*.25f), .035f*(1-t), .06f*(1-t));
                part.Mesh.Rotation = new(0, -part.Index*Mathf.Tau/7, .15f);
            }
            else if (part.Kind == 2)
            {
                part.Mesh.Position = part.Start + part.Velocity * elapsed + Vector3.Down * (5 * elapsed * elapsed);
                if (part.Mesh.Position.Y < .04f) part.Mesh.Position = new(part.Mesh.Position.X, .04f, part.Mesh.Position.Z);
                part.Mesh.Scale = new Vector3(1, _def.Family == "poison" ? 1.4f : 3.4f, .65f) * part.Size * (1 - t * .65f);
                part.Mesh.Rotation = new(elapsed * 7 + part.Index, elapsed * 4, part.Index * .8f);
            }
            else
            {
                float expand = 1 - Mathf.Pow(1 - t, 3);
                float size = part.Size * (part.Kind == 0 ? .15f + .95f * expand : .45f + t * .9f);
                part.Mesh.Position = part.Start + part.Velocity * expand * .65f + Vector3.Up * (_def.Rise * t * (part.Kind == 1 ? .85f : .35f));
                part.Mesh.Scale = new Vector3(1, part.Kind == 1 ? .85f : .8f + _def.Rise * .25f, 1) * size;
            }
            // Smoke never becomes an opaque wall; the hot mass is gone early in the burst.
            float alpha = part.Kind == 1 ? .32f * Mathf.Sin(Mathf.Pi * t) : (1 - t) * (part.Kind == 0 ? .9f : 1);
            if (part.Kind is 0 or 1) part.Mesh.SetInstanceShaderParameter("visibility_alpha", alpha);
            else part.Mesh.Transparency = 1 - alpha;
        }
    }
}
