using Godot;

namespace HordeShooter;

/// <summary>Shared explosion rules for barrels, rockets, mortar shells, grenades and the lure.</summary>
public static class Blast
{
    /// <summary>Share of <c>damage</c> the player takes at the center (slapstick, not lethal on its own).</summary>
    public const float PlayerShare = .2f;

    /// <param name="knockbackPower">Push at the center (divided by each monster's mass, falls off to a small shove at the edge).</param>
    public static void Explode(Stage stage, Vector3 at, float radius, float damage, Color color, Node? source = null, float selfShare = PlayerShare,
        float knockbackPower = 150f)
    {
        stage.Effects.Explosion(at, radius, color);
        stage.Sfx.PlayAt("explosion", at + Vector3.Up, radius > 5 ? 2 : 0);
        stage.Effects.PaintSplat(at, 2f);
        stage.Director.MakeNoise(at, stage.Director.Rules.NoiseExplosion);
        foreach (var car in stage.AlarmCars)
            if (car.GlobalPosition.DistanceTo(at) < radius + 2f) car.Trigger();

        foreach (var enemy in stage.Enemies)
        {
            if (enemy.IsDead) continue;
            var away = enemy.GlobalPosition - at;
            float d = away.Length();
            if (d > radius) continue;
            float falloff = 1 - d / radius;
            enemy.Shove(away, 40f + (knockbackPower - 40f) * falloff, damage * falloff + 15f);
        }

        foreach (var player in System.Linq.Enumerable.ToList(stage.AlivePlayers))
        {
            var toPlayer = player.GlobalPosition - at;
            float pd = toPlayer.Length();
            if (pd >= radius) continue;
            float falloff = 1 - pd / radius;
            player.TakeDamage(damage * selfShare * falloff, toPlayer, 0);
            toPlayer.Y = 0;
            var flat = toPlayer.LengthSquared() > .0001f ? toPlayer.Normalized() : -player.AimDirection;
            player.Launch(flat * 8f * falloff + Vector3.Up * (3f + 5f * falloff), .6f + .6f * falloff);
        }

        foreach (var prop in stage.Props)
        {
            if (prop == source || !GodotObject.IsInstanceValid(prop)) continue;
            var away = prop.GlobalPosition - at;
            float d = away.Length();
            if (d > radius * 1.4f) continue;
            if (prop is ExplosiveBarrel barrel) barrel.Ignite(.18f + d * .03f);
            else prop.ApplyCentralImpulse(away.Normalized() * 6f + Vector3.Up * 4f);
        }
    }
}
