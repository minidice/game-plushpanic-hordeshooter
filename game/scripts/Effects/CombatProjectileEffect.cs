using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Local tail aligned to actual travel. A projectile's parent owns flight and removal.</summary>
public partial class CombatProjectileEffect : Node3D
{
    private readonly List<MeshInstance3D> _blobs = new();
    private ShaderMaterial _material = null!;
    private float _age, _radius;
    public string EffectId { get; private set; } = "";
    public static CombatProjectileEffect Create(Node3D parent, CombatEffectDefinition def, float radius)
    {
        if (def.Kind != "projectile" || radius <= 0 || !float.IsFinite(radius)) throw new ArgumentException("Invalid projectile input");
        var effect = new CombatProjectileEffect { Name = "CombatProjectile_" + def.Id, EffectId = def.Id, _radius = radius };
        parent.AddChild(effect);
        effect._material = CombatBurstEffect.Volume(def.Color, def.Accent, .2f);
        for (int i = 0; i <= def.Particles; i++) effect._blobs.Add(CombatBurstEffect.Mesh(effect, CombatBurstEffect.Blob, effect._material));
        effect.UpdateVisual(); return effect;
    }
    public void Face(Vector3 direction)
    {
        if (direction.LengthSquared() > .0001f)
            Basis = Basis.LookingAt(direction.Normalized(), Mathf.Abs(direction.Normalized().Dot(Vector3.Up)) > .98f ? Vector3.Right : Vector3.Up);
    }
    public override void _Process(double delta) { _age += (float)delta; UpdateVisual(); }
    private void UpdateVisual()
    {
        _material.SetShaderParameter("clock", _age);
        for (int i = 0; i < _blobs.Count; i++)
        {
            float size = _radius * (i == 0 ? 1.5f : .8f / (1 + i * .25f));
            _blobs[i].Scale = new Vector3(1, .9f, i == 0 ? 1.6f : 1.2f) * size;
            _blobs[i].Position = i == 0 ? Vector3.Zero : new Vector3(Mathf.Sin(_age * 14 - i) * .025f, -.018f * i + Mathf.Sin(_age * 9 - i) * .025f, i * _radius * .7f);
            _blobs[i].SetInstanceShaderParameter("visibility_alpha", i == 0 ? 1f : Mathf.Max(.05f, 1 - i * .09f));
        }
    }
}
