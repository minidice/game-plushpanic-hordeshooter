using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Short impact, translucent dust and fragments, advanced only by the owning burst.</summary>
public partial class ImpactBurstVisual : Node3D
{
    private enum Layer { Impact, Smoke, Fragment, Flash }
    private sealed record Card(MeshInstance3D Mesh, StandardMaterial3D Material, Vector3 Travel,
        float Size, float Delay, float Life, float Phase, Layer Layer);
    private sealed record Profile(float HotLife, float HotSize, float Direction, float Loft, float FragmentStretch);
    private readonly List<Card> _cards = new();
    private CombatEffectDefinition _def = null!;
    private Profile _profile = null!;
    private float _radius, _floorOffset;
    public int VisualParts => _cards.Count;

    public static ImpactBurstVisual Create(Node3D parent, CombatEffectDefinition def, float radius, Vector3 direction, float floorOffset)
    {
        var visual = new ImpactBurstVisual { Name = "ImpactBurst", _def = def, _radius = radius, _floorOffset = floorOffset,
            _profile = def.Id switch {
                "grenade" => new(.20f, .23f, 0, .4f, 3.8f),
                "rocket" => new(.30f, .34f, .25f, .55f, 3f),
                "mortar" => new(.24f, .28f, 0, 1.05f, 2.8f),
                "lure" => new(.22f, .32f, 0, .45f, 1.5f),
                _ => new(.25f, .30f, 0, .55f, 2.5f),
            } };
        parent.AddChild(visual);
        visual.Build(direction.LengthSquared() > .001f ? direction.Normalized() : Vector3.Zero);
        return visual;
    }

    private void Build(Vector3 direction)
    {
        var random = new RandomNumberGenerator { Seed = 7417 };
        for (int i = 0; i < _def.Lobes; i++)
        {
            float phase = random.Randf();
            float angle = i * 2.39996f;
            var radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var travel = radial * _radius * _def.Spread * (.25f + .55f * phase)
                + direction * _radius * _profile.Direction;
            Add(Layer.Impact, travel, _radius * _profile.HotSize * (.8f + .4f * phase),
                i * .006f, _profile.HotLife + .08f * phase, phase);
            Add(Layer.Smoke, travel * .8f, _radius * (.34f + .16f * phase),
                .07f + .05f * phase, .64f + .18f * phase, phase);
        }
        for (int i = 0; i < _def.Particles; i++)
        {
            float angle = random.RandfRange(0, Mathf.Tau);
            var travel = new Vector3(Mathf.Cos(angle), random.RandfRange(.4f, 1.05f), Mathf.Sin(angle))
                * _radius * random.RandfRange(.55f, 1.05f);
            travel += direction * _radius * _profile.Direction;
            Add(Layer.Fragment, travel, random.RandfRange(.035f, .065f), 0, random.RandfRange(.55f, .88f), random.Randf());
        }
        Add(Layer.Flash, Vector3.Zero, _radius * .55f, 0, .12f, 0);
    }

    private void Add(Layer layer, Vector3 travel, float size, float delay, float life, float phase)
    {
        int tile = (int)(phase * 3.99f);
        var material = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, BillboardKeepScale = true,
            AlbedoTexture = GD.Load<Texture2D>(AssetPaths.File("vfx.area_smoke")),
            Uv1Scale = new Vector3(.5f, .5f, 1), Uv1Offset = new Vector3(tile % 2 * .5f, tile / 2 * .5f, 0),
            ProximityFadeEnabled = true, ProximityFadeDistance = layer == Layer.Smoke ? .35f : .1f,
            EmissionEnabled = layer != Layer.Smoke,
        };
        var mesh = CombatBurstEffect.Mesh(this, new QuadMesh { Size = Vector2.One }, material);
        mesh.Name = layer.ToString();
        _cards.Add(new(mesh, material, travel, size, delay * _def.Duration, life * _def.Duration, phase, layer));
    }

    public void Advance(float age)
    {
        foreach (var card in _cards)
        {
            float elapsed = age - card.Delay;
            float t = Mathf.Clamp(elapsed / card.Life, 0, 1);
            card.Mesh.Visible = elapsed >= 0 && t < 1;
            if (!card.Mesh.Visible) continue;
            bool smoke = card.Layer == Layer.Smoke;
            float expansion = 1 - Mathf.Pow(1 - t, smoke ? 2 : 4);
            float appear = Mathf.SmoothStep(0, smoke ? .18f : .035f, t);
            float fade = 1 - Mathf.SmoothStep(smoke ? .25f : .08f, 1, t);
            float size = card.Size * (.2f + expansion * .8f);
            var at = card.Travel * expansion + Vector3.Up * .18f;
            Color color = _def.Accent.Lerp(_def.Color, Mathf.SmoothStep(0, .5f, t));
            float alpha = .78f * appear * fade;
            float stretch = 1;
            if (smoke)
            {
                size = card.Size * (.45f + expansion * .9f);
                at.Y += _def.Rise * _profile.Loft * (.15f + t);
                color = _def.SmokeColor.Lightened(.12f + .1f * card.Phase);
                alpha = .32f * appear * fade;
            }
            else if (card.Layer == Layer.Fragment)
            {
                at = card.Travel * t + Vector3.Up * (.25f - _radius * .8f * t * t);
                // The source can be above its floor (rocket collision or mid-air grenade).
                at.Y = Mathf.Max(_floorOffset + .06f, at.Y);
                size = card.Size * (.35f + .65f * fade);
                stretch = _profile.FragmentStretch;
                alpha = appear * fade;
            }
            else if (card.Layer == Layer.Flash)
            {
                color = _def.Accent.Lightened(.35f);
                alpha = .8f * appear * fade;
            }
            else at.Y += _def.Rise * .18f * expansion;
            card.Mesh.Position = at;
            card.Mesh.Scale = new(size, size * stretch, 1);
            color.A = alpha;
            card.Material.AlbedoColor = color;
            if (!smoke)
            {
                card.Material.Emission = new Color(color.R, color.G, color.B);
                card.Material.EmissionEnergyMultiplier = card.Layer == Layer.Flash ? 1.5f : .8f * fade;
            }
        }
    }
}
