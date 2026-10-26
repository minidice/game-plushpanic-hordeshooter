using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Fuel ignition, rolling smoke and dying embers; decoration driven by the parent's clock.</summary>
public partial class BarrelBurstVisual : Node3D
{
    private sealed record Card(MeshInstance3D Mesh, StandardMaterial3D Material, Vector3 Travel,
        float Size, float Delay, float Life, float Phase, int Kind);
    private readonly List<Card> _cards = new();
    private CombatEffectDefinition _def = null!;
    private float _radius, _pace;
    public int VisualParts => _cards.Count;

    public static BarrelBurstVisual Create(Node3D parent, CombatEffectDefinition def, float radius)
    {
        var visual = new BarrelBurstVisual { Name = "FuelBurst", _def = def, _radius = radius, _pace = def.Duration / 2.4f };
        parent.AddChild(visual);
        visual.Build();
        return visual;
    }

    private void Build()
    {
        var random = new RandomNumberGenerator { Seed = 7417 };
        for (int i = 0; i < _def.Lobes; i++)
        {
            float phase = random.Randf();
            float angle = i * 2.39996f;
            var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var travel = radial * _radius * _def.Spread * (.35f + .55f * phase);
            Add(0, travel, _radius * (.57f + .2f * phase), i * .007f, .46f + .16f * phase, phase);
            Add(1, travel * .85f, _radius * (.65f + .22f * phase), .08f + .09f * phase,
                1.8f + .35f * phase, phase);
            if (i % 2 == 0)
                Add(2, travel * .75f, _radius * (.24f + .1f * phase), .18f + .12f * phase,
                    .7f + .35f * phase, phase);
        }
        for (int i = 0; i < _def.Particles; i++)
        {
            float angle = random.RandfRange(0, Mathf.Tau);
            var travel = new Vector3(Mathf.Cos(angle), random.RandfRange(.7f, 1.4f), Mathf.Sin(angle))
                * _radius * random.RandfRange(.4f, .95f);
            Add(3, travel, random.RandfRange(.035f, .065f), 0, random.RandfRange(.65f, 1.45f), random.Randf());
        }
        Add(4, Vector3.Zero, _radius * .85f, 0, .14f, 0);
    }

    private void Add(int kind, Vector3 travel, float size, float delay, float life, float phase)
    {
        bool flame = kind == 2;
        int grid = flame ? 4 : 2;
        var material = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, BillboardKeepScale = true,
            AlbedoTexture = GD.Load<Texture2D>(AssetPaths.File(flame ? "vfx.area_flame" : "vfx.area_smoke")),
            Uv1Scale = new Vector3(1f / grid, 1f / grid, 1),
            ProximityFadeEnabled = true, ProximityFadeDistance = kind == 1 ? .45f : .15f,
            EmissionEnabled = kind != 1,
        };
        int tile = (int)(phase * 3.99f);
        material.Uv1Offset = new Vector3(tile % grid / (float)grid, tile / grid / (float)grid, 0);
        var mesh = CombatBurstEffect.Mesh(this, new QuadMesh { Size = Vector2.One }, material);
        mesh.Name = kind switch { 0 => "Fireball", 1 => "Smoke", 2 => "ResidualFlame", 3 => "Ember", _ => "Flash" };
        _cards.Add(new(mesh, material, travel, size, delay * _pace, life * _pace, phase, kind));
    }

    public void Advance(float age)
    {
        foreach (var card in _cards)
        {
            float elapsed = age - card.Delay;
            float t = Mathf.Clamp(elapsed / card.Life, 0, 1);
            card.Mesh.Visible = elapsed >= 0 && t < 1;
            if (!card.Mesh.Visible) continue;
            float expansion = 1 - Mathf.Pow(1 - t, card.Kind == 1 ? 2 : 4);
            float fade = 1 - Mathf.SmoothStep(card.Kind == 1 ? .3f : .12f, 1, t);
            float appear = Mathf.SmoothStep(0, card.Kind == 1 ? .2f : .06f, t);
            float size = card.Size;
            Vector3 at = card.Travel * expansion + Vector3.Up * .3f;
            Color color;
            float alpha;
            switch (card.Kind)
            {
                case 0:
                    size *= .2f + expansion * .9f;
                    at.Y += _def.Rise * .28f * expansion;
                    color = _def.Accent.Lerp(_def.Color, Mathf.SmoothStep(0, .55f, t));
                    alpha = .85f * appear * fade;
                    break;
                case 1:
                    size *= .4f + .85f * expansion;
                    at.Y += _def.Rise * (.15f + .85f * t);
                    at.X += Mathf.Sin(t * 4 + card.Phase * 6) * _radius * .035f * t;
                    color = _def.SmokeColor.Lightened(.12f + .12f * card.Phase);
                    alpha = .46f * appear * fade;
                    break;
                case 2:
                    at = card.Travel + Vector3.Up * (.2f + size * .25f * fade);
                    size *= .25f + .75f * fade;
                    color = Colors.White;
                    alpha = .65f * appear * fade;
                    int tile = (int)(card.Phase * 16 + elapsed / _pace * 18) % 16;
                    card.Material.Uv1Offset = new Vector3(tile % 4 / 4f, tile / 4 / 4f, 0);
                    break;
                case 3:
                    at = card.Travel * t + Vector3.Up * (.35f - _radius * .6f * t * t);
                    at.Y = Mathf.Max(.08f, at.Y);
                    size *= .4f + .6f * fade;
                    color = _def.Accent.Lerp(_def.Color, t);
                    alpha = appear * fade;
                    break;
                default:
                    size *= .25f + .85f * expansion;
                    color = _def.Accent.Lightened(.55f);
                    alpha = .85f * appear * fade;
                    break;
            }
            card.Mesh.Position = at;
            card.Mesh.Scale = new Vector3(size, size * (card.Kind == 2 ? 1.3f : 1), 1);
            color.A = alpha;
            card.Material.AlbedoColor = color;
            if (card.Kind != 1)
            {
                card.Material.Emission = new Color(color.R, color.G, color.B);
                card.Material.EmissionEnergyMultiplier = card.Kind == 4 ? 2 : card.Kind == 0 ? 1.2f * fade : .6f;
            }
        }
    }
}
