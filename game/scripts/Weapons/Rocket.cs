using Godot;

namespace HordeShooter;

/// <summary>Straight-flying rocket. Ray-steps each frame so it never tunnels through a zombie.</summary>
public partial class Rocket : Node3D
{
    private Stage _stage = null!;
    private Vector3 _velocity;
    private WeaponDefinition _def = null!;
    private float _life;
    private Rid _shooter;
    private Player? _by;
    /// <summary>The tier it was fired from (L-15): it flies over lower tiers and finds targets there on the floor plan.</summary>
    private int _tier;

    /// <param name="by">Who fired it: the hit and the warhead count for them and take their damage multiplier.</param>
    public static Rocket Launch(Stage stage, Vector3 from, Vector3 direction, WeaponDefinition def, Rid shooter, Player? by = null)
    {
        var rocket = new Rocket
        {
            Name = "Rocket",
            _stage = stage,
            _velocity = direction.Normalized() * def.ProjectileSpeed,
            _def = def,
            _life = def.ProjectileLifetime,
            _shooter = shooter,
            _by = by,
            _tier = by != null && stage.Tiers.Any ? stage.Tiers.TierOf(by) : TierMap.FloorTier(from.Y - Player.AimHeight),
        };
        stage.Effects.AddChild(rocket);
        rocket.GlobalPosition = from;
        rocket.LookAt(from + direction, Vector3.Up);
        var body = Mat.Mesh(rocket, new CylinderMesh { TopRadius = .07f, BottomRadius = .07f, Height = .45f }, Vector3.Zero, Mat.Solid("c9a23a", .4f));
        body.RotationDegrees = new Vector3(90, 0, 0);
        Mat.Mesh(rocket, new SphereMesh { Radius = .1f, Height = .2f }, new Vector3(0, 0, .25f), Mat.Glow(new Color("ffb347"), 4));
        rocket.AddChild(new OmniLight3D { OmniRange = 4, LightEnergy = 2, LightColor = new Color("ffb347"), Position = new Vector3(0, 0, .3f) });
        rocket.AddChild(new CpuParticles3D
        {
            Amount = 40,
            Lifetime = .5f,
            Mesh = new SphereMesh { Radius = .08f, Height = .16f, Material = new StandardMaterial3D { AlbedoColor = new Color(.7f, .7f, .7f, .6f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha } },
            Gravity = Vector3.Up * .5f,
            ScaleAmountMin = .6f,
            ScaleAmountMax = 1.6f,
            Position = new Vector3(0, 0, .3f),
            LocalCoords = false,
        });
        return rocket;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _life -= dt;
        var from = GlobalPosition;
        var to = from + _velocity * dt;
        var hitOrNull = Rays.Cast(this, from, to, Layers.BulletMask, new[] { _shooter });
        if (_stage.Tiers.Any)
        {
            var tiers = _stage.Tiers;
            float block = hitOrNull is { } h ? from.DistanceTo(h.Position) : from.DistanceTo(to);
            var below = TierMap.Along(_stage.Enemies, e => e.Radius, e => !e.IsDead && tiers.TierOf(e) < _tier, from, _velocity, block);
            if (below.Count > 0)
            {
                var enemy = below[0].body;
                var at = enemy.GlobalPosition + Vector3.Up * .5f;
                enemy.TakeHit(new HitInfo(_def.Damage * (_by != null && IsInstanceValid(_by) ? _by.OutgoingDamage : 1f), _velocity.Normalized(), at, false,
                    _def.HitPower, _by != null && IsInstanceValid(_by) ? _by : null, _def.TypeId));
                Detonate(at);
                return;
            }
        }
        if (hitOrNull is { } hit)
        {
            var at = hit.Position;
            // Direct hit: the target takes the rocket's own damage, then everyone takes the warhead.
            if (hit.Collider is Enemy enemy && !enemy.IsDead)
                enemy.TakeHit(new HitInfo(_def.Damage * (_by != null && IsInstanceValid(_by) ? _by.OutgoingDamage : 1f), _velocity.Normalized(), at, false,
                    _def.HitPower, _by != null && IsInstanceValid(_by) ? _by : null, _def.TypeId));
            Detonate(at);
            return;
        }
        GlobalPosition = to;
        if (_life <= 0) Detonate(to);
    }

    private void Detonate(Vector3 at)
    {
        if (_def.Explodes) Blast.Explode(_stage, at, _def.ExplosionRadius, _def.ExplosionDamage, new Color("ff8a3c"), selfShare: _def.ExplosionSelfShare,
            knockbackPower: _def.ExplosionKnockback, by: _by != null && IsInstanceValid(_by) ? _by : null);
        else _stage.Effects.Impact(at, -_velocity.Normalized());
        QueueFree();
    }
}
