using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Burning pool from a molotov: hurts every zombie inside each frame, and you too if you stand in it.</summary>
public partial class FireZone : Node3D
{
    private Stage _stage = null!;
    private float _radius, _dps, _playerDps, _life;
    private readonly System.Collections.Generic.Dictionary<Player, float> _playerDebts = new();
    private readonly Dictionary<Enemy, float> _ticks = new();

    public float Radius => _radius;

    public static FireZone Create(Stage stage, Vector3 at, BurnPoolEffect burn)
    {
        var zone = new FireZone { Name = "Fire", _stage = stage, _radius = burn.RadiusM, _dps = burn.EnemyDps, _playerDps = burn.PlayerDps, _life = burn.Duration, Position = at };
        stage.Effects.AddChild(zone);
        zone.AddToGroup(SquadGroups.Hazard);
        zone.SetMeta("radius", burn.RadiusM);
        // Edge ring + flames only (no filled disc): the burning zombies inside must stay readable.
        Mat.Mesh(zone, new TorusMesh { InnerRadius = burn.RadiusM * .94f, OuterRadius = burn.RadiusM, Rings = 32, RingSegments = 4 },
            Vector3.Up * .04f, Mat.Glow(new Color("ff7a2a"), 1.5f));
        var flameMat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        zone.AddChild(new CpuParticles3D
        {
            Amount = 80,
            Lifetime = .7f,
            Mesh = new SphereMesh { Radius = .18f, Height = .36f, Material = flameMat },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(burn.RadiusM * .75f, .05f, burn.RadiusM * .75f),
            Direction = Vector3.Up,
            Spread = 15,
            Gravity = Vector3.Up * 3,
            InitialVelocityMin = .5f,
            InitialVelocityMax = 1.5f,
            ScaleAmountMin = .6f,
            ScaleAmountMax = 1.5f,
            Color = new Color(1f, .5f, .12f, .85f),
            HueVariationMin = -.03f,
            HueVariationMax = .05f,
        });
        var light = new OmniLight3D { OmniRange = burn.RadiusM * 2.2f, LightEnergy = 1.4f, LightColor = new Color("ff7a2a"), Position = Vector3.Up * 1.6f };
        light.AddToGroup(VisionSystem.LightGroup);
        zone.AddChild(light);
        stage.FlickerLights.Add(light);
        light.SetMeta("base_energy", 1.4f);
        stage.Director.MakeNoise(at, stage.Director.Rules.NoiseFire);
        stage.Sfx.Attach("fire_loop", zone);
        return zone;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _life -= dt;
        if (_life <= 0)
        {
            QueueFree();
            return;
        }
        float r2 = _radius * _radius;
        foreach (var enemy in _stage.Enemies)
        {
            if (enemy.IsDead) continue;
            var d = enemy.GlobalPosition - GlobalPosition;
            d.Y = 0;
            if (d.LengthSquared() > r2) continue;
            enemy.Burn(_dps * dt);
            // Occasional sparks so burning zombies read as burning.
            float t = _ticks.GetValueOrDefault(enemy) - dt;
            if (t <= 0)
            {
                t = .35f;
                _stage.Effects.Stuffing(enemy.GlobalPosition + Vector3.Up, Vector3.Up, new Color("ff9a3c"), 3);
            }
            _ticks[enemy] = t;
        }

        foreach (var player in System.Linq.Enumerable.ToList(_stage.AlivePlayers))
        {
            var p = player.GlobalPosition - GlobalPosition;
            p.Y = 0;
            if (p.LengthSquared() > r2) continue;
            // Whole points only, so each survivor carries their own fraction between frames.
            float debt = _playerDebts.GetValueOrDefault(player) + _playerDps * dt;
            int whole = (int)debt;
            _playerDebts[player] = debt - whole;
            if (whole > 0) player.TakeDamage(whole, Vector3.Zero, 0);
        }
    }

    public override void _ExitTree() => _stage.FlickerLights.RemoveAll(l => !IsInstanceValid(l) || l.GetParent() == this);
}
