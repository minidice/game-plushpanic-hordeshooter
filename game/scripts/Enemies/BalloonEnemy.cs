using Godot;

namespace HordeShooter;

/// <summary>
/// Balloon parasite (boomer slot). Waddles up, inflates, and pops. Anyone within range of the pop —
/// even if it was shot — is dosed with helium (narrow vision, squeaky screen) and a mob is summoned.
/// Shoot it from far away.
/// </summary>
public partial class BalloonEnemy : Enemy
{
    private BalloonPopTrait PopTrait => Monster.BalloonPop!;
    public bool Popped { get; private set; }
    private float _inflate = -1;
    private Vector3 _modelScale;

    public override void _Ready()
    {
        base._Ready();
        AttackReach = -99;
        _modelScale = Rig.Model.Scale;
    }

    protected override Vector3 Think(float dt)
    {
        if (_inflate >= 0)
        {
            _inflate += dt;
            float wobble = 1 + _inflate * .6f + Mathf.Sin(_inflate * 40) * .05f;
            Rig.Model.Scale = _modelScale * wobble;
            if (_inflate >= PopTrait.InflateTime) Pop();
            return Vector3.Zero;
        }
        var player = Stage.NearestAlivePlayer(GlobalPosition);
        var toPlayer = player != null ? player.GlobalPosition - GlobalPosition : Vector3.Inf;
        toPlayer.Y = 0;
        if (Awake && player != null && toPlayer.Length() < PopTrait.TriggerRange)
        {
            _inflate = 0;
            Stage.Sfx.PlayAt("balloon_inflate", GlobalPosition + Vector3.Up * 1.5f);
            return Vector3.Zero;
        }
        return base.Think(dt);
    }

    public override void Die(Vector3 direction, float force) => Pop();

    public void Pop()
    {
        if (IsDead) return;
        IsDead = true;
        Popped = true;
        Stage.OnEnemyDied(this);
        Stage.RaiseEnemyKilled(this, Vector3.Up, 0);
        CollisionLayer = 0;
        CollisionMask = 0;
        Shape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);

        var at = GlobalPosition;
        var pop = PopTrait;
        var pink = Definition.PrimaryColor;
        Stage.Effects.Explosion(at, pop.Radius * .7f, pink);
        Stage.Sfx.PlayAt("balloon_pop", at + Vector3.Up * 1.5f);
        Stage.Effects.Stuffing(at + Vector3.Up * 1.5f, Vector3.Up, Definition.StuffingColor, 30);
        for (int i = 0; i < 4; i++)
            Stage.Effects.PaintSplat(at + new Vector3(Rng.RandfRange(-1.5f, 1.5f), 0, Rng.RandfRange(-1.5f, 1.5f)), 1.4f);
        Stage.Director.MakeNoise(at, Stage.Director.Rules.NoiseBalloon);

        foreach (var other in Stage.Enemies)
        {
            if (other == this || other.IsDead) continue;
            var away = other.GlobalPosition - at;
            if (away.Length() < pop.Radius * .8f) other.Shove(away, pop.KnockbackPower, 0);
        }

        bool summoned = false;
        foreach (var player in System.Linq.Enumerable.ToList(Stage.AlivePlayers))
        {
            var toPlayer = player.GlobalPosition - at;
            toPlayer.Y = 0;
            if (toPlayer.Length() >= pop.Radius) continue;
            player.ApplyHelium(pop.HeliumTime);
            player.Launch(toPlayer.Normalized() * 5f + Vector3.Up * 4f, .5f);
            Stage.Effects.FloatingText(player.GlobalPosition + Vector3.Up * 2.8f, "HELIUM!", new Color("ff9ccc"));
            // One summon per pop, around the first survivor caught in it.
            if (!summoned) Stage.Director.SummonAround(player.GlobalPosition, pop.SummonCount);
            summoned = true;
        }

        if (Rig.Head != null) Rig.Head.Visible = false;
        Rig.Model.Scale = _modelScale;
        SpawnCorpse(Vector3.Up, 3f, Definition.ColliderRadius * .6f, Definition.ColliderHeight * .5f);
        QueueFree();
    }
}
