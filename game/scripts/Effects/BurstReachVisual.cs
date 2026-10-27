using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Brief, omnidirectional volume at the supplied damage radius; never a ground decal.</summary>
public partial class BurstReachVisual : Node3D
{
    private readonly List<(MeshInstance3D mesh, StandardMaterial3D material, Vector3 reach, float size, float life)> _puffs = new();
    private float _spreadTime;
    public int VisualParts => _puffs.Count;

    public static BurstReachVisual Create(Node3D parent, CombatEffectDefinition def, float radius)
    {
        var visual = new BurstReachVisual { Name = "DamageReach", _spreadTime = Math.Min(.07f, def.Duration * .12f) };
        parent.AddChild(visual);
        if (def.Lobes == 0) return visual;
        // Keep the accepted fuel/fire impact art unchanged. The shorter blasts need a lower,
        // wider distribution so elevated billboards do not leave the near-side damage edge bare.
        bool compactBlast = def.Id is "blast" or "grenade" or "rocket" or "mortar" or "lure";
        var random = new RandomNumberGenerator { Seed = 17621 };
        // Fixed angular coverage: directional core art must not hide the other side of a spherical blast.
        const int count = 28;
        for (int i = 0; i < count; i++)
        {
            bool edge = i < 16;
            float angle = edge ? Mathf.Tau * i / 16 + random.RandfRange(-.055f, .055f) : i * 2.39996f;
            float distance = radius * (edge ? random.RandfRange(compactBlast ? .84f : .72f, compactBlast ? .90f : .79f)
                : Mathf.Sqrt((i - 16 + .5f) / 12) * .58f);
            int tile = i % 4;
            var color = def.Color.Lerp(def.Accent, .35f);
            var material = new StandardMaterial3D {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, BillboardKeepScale = true,
                AlbedoTexture = GD.Load<Texture2D>(AssetPaths.File("vfx.area_smoke")),
                Uv1Scale = new(.5f,.5f,1), Uv1Offset = new(tile%2*.5f,tile/2*.5f,0),
                AlbedoColor = color, EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = .45f,
                ProximityFadeEnabled = true, ProximityFadeDistance = compactBlast ? .3f : .15f,
            };
            var mesh = CombatBurstEffect.Mesh(visual, new QuadMesh { Size = Vector2.One }, material);
            mesh.Name = "ReachPuff" + i;
            visual._puffs.Add((mesh, material, new Vector3(Mathf.Cos(angle)*distance, radius*(compactBlast ? .09f : .12f), Mathf.Sin(angle)*distance),
                radius * (edge ? random.RandfRange(compactBlast ? .52f : .66f,compactBlast ? .60f : .74f) : random.RandfRange(.9f,1.1f)),
                Math.Min(.48f,def.Duration*.7f)*random.RandfRange(.8f,1)));
        }
        return visual;
    }

    public void Advance(float age)
    {
        float expansion = 1-Mathf.Pow(1-Mathf.Clamp(age/_spreadTime,0,1),3);
        foreach (var (mesh, material, reach, size, life) in _puffs)
        {
            mesh.Visible = age < life;
            if (!mesh.Visible) continue;
            float alpha = .48f * Mathf.SmoothStep(0,_spreadTime*.3f,age)
                * (1-Mathf.SmoothStep(life*.22f,life,age));
            mesh.Position = reach * expansion;
            mesh.Scale = new Vector3(size, size*.8f,1) * (.45f + .55f * expansion);
            var color = material.AlbedoColor; color.A=alpha; material.AlbedoColor=color;
        }
    }
}
