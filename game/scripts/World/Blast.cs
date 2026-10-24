using Godot;

namespace HordeShooter;

/// <summary>Shared explosion rules for barrels, rockets, mortar shells, grenades and the lure.</summary>
public static class Blast
{
    /// <summary>Share of <c>damage</c> the player takes at the center (slapstick, not lethal on its own).</summary>
    public const float PlayerShare = .2f;

    /// <param name="damage">Zero disables all damage and barrel chains. Positive damage keeps the existing monster/object
    /// formula: (damage * falloff + 15) * outgoing multiplier, including the 15-point floor at the edge.
    /// Barrels within 1.4 times the radius use that floor even outside the damage radius.</param>
    /// <param name="knockbackPower">Zero disables all impulses. Positive monster push falls from this center power to
    /// min(40, power) at the edge, divided by monster mass. Positive player/prop impulses keep their existing fixed formulas.</param>
    /// <param name="by">The survivor whose weapon this was: the damage takes their damage multiplier (cards, glass cannon) and counts for them.</param>
    public static void Explode(Stage stage, Vector3 at, float radius, float damage, Color color, Node? source = null, float selfShare = PlayerShare,
        float knockbackPower = 150f, Player? by = null, string effectSource = "blast", Vector3 effectDirection = default)
    {
        float scale = by != null && GodotObject.IsInstanceValid(by) ? by.OutgoingDamage : 1f;
        CombatBurstEffect.Play(stage.Effects, CombatEffectCatalog.Current.ForSource(effectSource), at, radius, effectDirection, stage.Tiers.GroundY(at));
        stage.Cam.AddShake(.55f);
        stage.Sfx.PlayAt("explosion", at + Vector3.Up, radius > 5 ? 2 : 0);
        stage.Effects.PaintSplat(at, 2f);
        // Corpses are local cosmetic physics on every peer; only the host can hurt/launch living actors or chain barrels.
        if (!stage.Authoritative)
        {
            stage.Effects.BlastCorpses(at, radius, knockbackPower, source);
            return;
        }
        stage.Director.MakeNoise(at, stage.Director.Rules.NoiseExplosion);
        foreach (var car in stage.AlarmCars)
            if (car.GlobalPosition.DistanceTo(at) < radius + 2f) car.Trigger();

        // Height tiers (L-15, elevation-tiers.md 6장): a blast reaches one tier up or down, not further.
        var tiers = stage.Tiers.Any ? stage.Tiers : null;
        // The tier whose floor the blast is on or above: a rocket bursting at chest height is still on that floor.
        int blastTier = TierMap.FloorTier(at.Y);
        foreach (var enemy in stage.Enemies)
        {
            if (damage <= 0 && knockbackPower <= 0) continue;
            if (enemy.IsDead) continue;
            if (tiers != null && Mathf.Abs(tiers.TierOf(enemy) - blastTier) > 1) continue;
            var away = enemy.GlobalPosition - at;
            float d = away.Length();
            if (d > radius) continue;
            float falloff = 1 - d / radius;
            float edgePower = Mathf.Min(40f, Mathf.Max(0, knockbackPower));
            float power = knockbackPower > 0 ? edgePower + (knockbackPower - edgePower) * falloff : 0;
            enemy.Shove(away, power, damage > 0 ? (damage * falloff + 15f) * scale : 0, by);
        }

        // Breakable objects take the blast like a monster would (C-29); explosive-only ones only from this.
        foreach (var placed in System.Linq.Enumerable.ToList(stage.Breakables))
        {
            if (damage <= 0) continue;
            if (!GodotObject.IsInstanceValid(placed)) continue;
            if (tiers != null && Mathf.Abs(TierMap.FloorTier(placed.GlobalPosition.Y) - blastTier) > 1) continue;
            float d = (placed.GlobalPosition - at).Length();
            if (d > radius) continue;
            placed.TakeDamage((damage * (1 - d / radius) + 15f) * scale, ObjectDamage.Explosion, by);
        }

        // Include corpses created by the damage above, even before the next physics broadphase.
        stage.Effects.BlastCorpses(at, radius, knockbackPower, source);

        foreach (var player in System.Linq.Enumerable.ToList(stage.AlivePlayers))
        {
            if (tiers != null && Mathf.Abs(tiers.TierOf(player) - blastTier) > 1) continue;
            var toPlayer = player.GlobalPosition - at;
            float pd = toPlayer.Length();
            if (pd >= radius) continue;
            float falloff = 1 - pd / radius;
            if (damage > 0) player.TakeDamage(damage * selfShare * falloff, toPlayer, 0);
            if (knockbackPower <= 0) continue;
            toPlayer.Y = 0;
            var flat = toPlayer.LengthSquared() > .0001f ? toPlayer.Normalized() : -player.AimDirection;
            player.Launch(flat * 8f * falloff + Vector3.Up * (3f + 5f * falloff), .6f + .6f * falloff);
        }

        foreach (var prop in System.Linq.Enumerable.ToList(stage.Props))
        {
            if (prop == source || !GodotObject.IsInstanceValid(prop)) continue;
            var away = prop.GlobalPosition - at;
            float d = away.Length();
            if (d > radius * 1.4f) continue;
            if (prop is ExplosiveBarrel barrel)
            {
                if (damage > 0) barrel.TakeDamage((damage * Mathf.Max(0, 1 - d / radius) + 15f) * scale, ObjectDamage.Explosion, by);
            }
            else if (knockbackPower > 0) prop.ApplyCentralImpulse(away.Normalized() * 6f + Vector3.Up * 4f);
        }
    }
}
