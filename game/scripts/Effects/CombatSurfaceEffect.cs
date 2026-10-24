using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Radius and duration are supplied by the hazard. No damage, sound, lights or collision here.</summary>
public partial class CombatSurfaceEffect : Node3D
{
    private CombatEffectDefinition _def = null!;
    private ShaderMaterial _ground = null!, _body = null!;
    private readonly List<(MeshInstance3D mesh, Vector3 at, float phase)> _pieces = new();
    private readonly List<(MeshInstance3D mesh, Vector3 at, float phase)> _motes = new();
    private float _age, _life;
    public float Radius { get; private set; }
    public string EffectId => _def.Id;
    public float Lifetime => _life;
    public static CombatSurfaceEffect Create(Node3D parent, CombatEffectDefinition def, float radius, float lifetime)
    {
        if (def.Kind != "surface" || !float.IsFinite(radius) || radius <= 0 || !float.IsFinite(lifetime) || lifetime <= 0)
            throw new ArgumentException("Invalid surface input");
        var effect = new CombatSurfaceEffect { Name = "CombatSurface_" + def.Id, _def = def, Radius = radius, _life = lifetime };
        parent.AddChild(effect); effect.Build(); effect.UpdateVisual(); return effect;
    }
    private void Build()
    {
        bool fire = _def.Family == "fire";
        _ground = CombatBurstEffect.Ground(_def, fire ? 1 : 2);
        var plane = CombatBurstEffect.Mesh(this, new PlaneMesh { Size = Vector2.One * Radius * 2 }, _ground);
        plane.Position = Vector3.Up * .035f;
        _body = CombatBurstEffect.Volume(_def.Color, _def.Accent, fire ? 1.5f : .1f);
        _body.SetShaderParameter("flame", fire);
        var random = new RandomNumberGenerator { Seed = 6517 };
        for (int i = 0; i < _def.Lobes; i++)
        {
            float angle = i * 2.399963f, distance = Mathf.Sqrt((i + .5f) / Math.Max(1, _def.Lobes)) * Radius * _def.Spread;
            var at = new Vector3(Mathf.Cos(angle) * distance, .06f, Mathf.Sin(angle) * distance);
            var mesh = CombatBurstEffect.Mesh(this, CombatBurstEffect.Blob, _body);
            _pieces.Add((mesh, at, random.RandfRange(0, Mathf.Tau)));
        }
        var moteMaterial = Mat.Glow(_def.Accent, fire ? 1 : .15f);
        for (int i = 0; i < _def.Particles; i++)
        {
            float angle = random.RandfRange(0, Mathf.Tau), distance = Mathf.Sqrt(random.Randf()) * Radius * (_def.Spread + .03f);
            var mesh = CombatBurstEffect.Mesh(this, CombatBurstEffect.Blob, moteMaterial);
            _motes.Add((mesh, new(Mathf.Cos(angle)*distance, .08f, Mathf.Sin(angle)*distance), random.Randf()));
        }
    }
    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= _life) { QueueFree(); return; }
        UpdateVisual();
    }
    private void UpdateVisual()
    {
        bool fire = _def.Family == "fire";
        float fade = Mathf.Clamp((_life - _age) / .3f, 0, 1);
        _ground.SetShaderParameter("clock", _age); _ground.SetShaderParameter("fade", fade);
        _body.SetShaderParameter("clock", _age);
        foreach (var (mesh, at, phase) in _pieces)
        {
            float pulse = .5f + .5f * Mathf.Sin(_age * (fire ? 9 : 3) + phase);
            float size = Mathf.Min(Radius * .18f, fire ? .38f : .21f);
            mesh.Scale = fire ? new Vector3(size * (.7f + .3f * pulse), _def.Rise * (.45f + .65f * pulse), size * .7f)
                : Vector3.One * size * (.4f + pulse * .6f);
            mesh.Position = at + Vector3.Up * (fire ? mesh.Scale.Y * .4f : pulse * _def.Rise);
            mesh.Rotation = fire ? new Vector3(.12f * Mathf.Sin(_age * 6 + phase), phase, .15f * Mathf.Sin(_age * 5 + phase)) : Vector3.Zero;
            mesh.SetInstanceShaderParameter("visibility_alpha", fade * (fire ? .82f : .55f + pulse * .25f));
        }
        foreach (var (mesh, at, phase) in _motes)
        {
            float t = Mathf.PosMod(_age * (fire ? .9f : .55f) + phase, 1);
            mesh.Position = at + new Vector3(Mathf.Sin(phase*12+t*3)*.025f, t * (fire ? .85f : .35f), 0);
            mesh.Scale = Vector3.One * Mathf.Min(Radius*.025f,fire ? .045f : .06f) * (1-t*.5f);
            mesh.Transparency = 1 - Mathf.Sin(t*Mathf.Pi)*fade*.7f;
        }
    }
}
