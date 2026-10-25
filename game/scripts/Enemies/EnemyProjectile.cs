using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Swept segment collision prevents fast enemy shots from passing through walls.</summary>
public partial class EnemyProjectile : Node3D
{
    public string WeaponId { get; private set; } = "";
    private Stage _stage = null!;
    private CombatProjectileEffect? _visual;
    private Vector3 _direction;
    private RangedTrait _shot = null!;
    private float _remaining, _damage;
    // Height tiers (L-15): a gunshot fired down a tier flies level at the shooter's height and finds survivors below on
    // the floor plan; vomit thrown between tiers is lobbed over the edge (the lob exception).
    private bool _flat, _lob;
    private int _tier;
    private Vector3 _from, _to;
    private float _total, _travelled;
    /// <summary>Low cover right in front of the shooter is fired over (C-29): where it stood and how wide it is.</summary>
    private Vector3 _shooter;
    private float _shooterRadius;
    private List<Rid>? _overCover;

    /// <param name="shooterRadius">The shooter's body radius: cover within a body width of it does not stop the shot.</param>
    public void Setup(Stage stage, Vector3 from, Vector3 target, RangedTrait shot, float damage, float shooterRadius = .4f, string weaponId = "")
    {
        WeaponId = weaponId;
        _stage = stage; Position = from; _shot = shot; _damage = damage;
        _shooter = from; _shooterRadius = shooterRadius;
        _direction = (target - from).Normalized(); _remaining = from.DistanceTo(target);
        if (!stage.Tiers.Any) return;
        int fromTier = TierMap.FloorTier(from.Y - .8f), toTier = TierMap.FloorTier(target.Y - .8f);
        if (shot.HazardSeconds > 0 && fromTier != toTier)
        {
            _lob = true;
            _from = from;
            _to = target;
            _total = Mathf.Max(.1f, from.DistanceTo(target));
        }
        else if (shot.HazardSeconds <= 0 && toTier < fromTier)
        {
            _flat = true;
            _tier = fromTier;
            var flat = new Vector3(target.X - from.X, 0, target.Z - from.Z);
            _direction = flat.Normalized();
            _remaining = flat.Length() + 2f;
        }
    }
    /// <summary>Gunshots are a thin streak trailing behind the bullet's head (I-66).</summary>
    public const float TracerLength = .5f, TracerRadius = .025f;

    public override void _Ready()
    {
        // Vomit is a slow blob you are meant to watch and step away from.
        if (_shot.HazardSeconds > 0)
        {
            _visual = CombatProjectileEffect.Create(this, CombatEffectCatalog.Current.ForSource("poison_flight"), .22f);
            _visual.Face(_direction);
            return;
        }
        // Gunshots: the windup is the warning (C-13). The bullet itself is fast and should not glow like a ball.
        if (Mathf.Abs(_direction.Dot(Vector3.Up)) < .99f) LookAt(GlobalPosition + _direction, Vector3.Up);
        if (WeaponId == "woodbow")
        {
            Mat.Box(this, new Vector3(0, 0, .28f), new Vector3(.025f, .025f, .56f), Mat.Solid(new Color("a67a48")));
            Mat.Box(this, new Vector3(0, 0, -.02f), new Vector3(.09f, .025f, .09f), Mat.Solid(new Color("bbc3bf")));
            return;
        }
        var streak = Mat.Mesh(this, new CylinderMesh { TopRadius = TracerRadius, BottomRadius = TracerRadius * .4f, Height = TracerLength, RadialSegments = 6, Rings = 1 },
            new Vector3(0, 0, TracerLength * .5f), Mat.Glow(new Color("f2dcb0"), 1.1f));
        streak.RotationDegrees = new Vector3(-90, 0, 0);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_stage.NearestAlivePlayer(GlobalPosition) == null) { QueueFree(); return; }
        if (_lob) { Lob((float)delta); return; }
        float travel = Mathf.Min(_remaining, _shot.ProjectileSpeed * (float)delta);
        var next = GlobalPosition + _direction * travel;
        var hit = Rays.Cast(this, GlobalPosition, next, Layers.EnemyShotMask | Layers.Player, _overCover);
        // Cover the shooter fires over is passed for the rest of the flight; any other cover stops the shot.
        while (hit is { } over && CoverRule.IsCover(over.Collider) && CoverRule.ShootsOver(_shooter, _shooterRadius, over.Position))
        {
            (_overCover ??= new()).Add(over.Rid);
            hit = Rays.Cast(this, GlobalPosition, next, Layers.EnemyShotMask | Layers.Player, _overCover);
        }
        if (_flat)
        {
            var tiers = _stage.Tiers;
            float block = hit is { } h ? GlobalPosition.DistanceTo(h.Position) : travel;
            var below = TierMap.Along(_stage.AlivePlayers, p => p.Definition.ColliderRadius, p => tiers.TierOf(p) < _tier, GlobalPosition, _direction, block);
            if (below.Count > 0)
            {
                var victim = below[0].body;
                victim.TakeDamage(_damage, _direction, .5f);
                Impact(victim.GlobalPosition + Vector3.Up * .8f);
                return;
            }
        }
        if (hit is { } collision)
        {
            if (collision.Collider is Player player) player.TakeDamage(_damage, _direction, .5f);
            else if (collision.Collider is BaseCore core && _stage.Authoritative) core.TakeDamage(_damage, _shooter);
            else if (collision.Collider is ExplosiveBarrel barrel) barrel.TakeDamage(_damage, ObjectDamage.Gun);
            else if (ObjectBody.From(collision.Collider) is { } placed) placed.TakeDamage(_damage, ObjectDamage.Gun);
            Impact(collision.Position + collision.Normal * .16f);
            return;
        }
        _visual?.Face(next - GlobalPosition);
        GlobalPosition = next;
        _remaining -= travel;
        if (_remaining <= .001f) Impact(next);
    }
    /// <summary>An arc over whatever edge lies between, landing on the target's floor; only a survivor in the way stops it.</summary>
    private void Lob(float dt)
    {
        _travelled += _shot.ProjectileSpeed * dt;
        float t = Mathf.Clamp(_travelled / _total, 0, 1);
        float apex = 1.5f + Mathf.Abs(_to.Y - _from.Y);
        var next = _from.Lerp(_to, t) + Vector3.Up * (4 * apex * t * (1 - t));
        if (Rays.Cast(this, GlobalPosition, next, Layers.Player) is { Collider: Player player } hit)
        {
            player.TakeDamage(_damage, _direction, .5f);
            Impact(hit.Position);
            return;
        }
        _visual?.Face(next - GlobalPosition);
        GlobalPosition = next;
        if (t >= 1) Impact(_to);
    }

    private void Impact(Vector3 point)
    {
        if (_shot.HazardSeconds > 0)
        {
            CombatBurstEffect.Play(_stage.Effects, CombatEffectCatalog.Current.ForSource("poison_impact"), point, _shot.HazardRadius, _direction, _stage.Tiers.GroundY(point));
            var hazard = new VomitHazard();
            hazard.Setup(_stage, new Vector3(point.X, _stage.Tiers.GroundY(point) + .06f, point.Z), _shot.HazardSeconds, _shot.HazardRadius, _damage * .5f);
            _stage.AddChild(hazard);
        }
        else _stage.Effects.Impact(point, -_direction);
        QueueFree();
    }
}
