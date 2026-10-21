using Godot;

namespace HordeShooter;

/// <param name="Power">Knockback power (see <see cref="Knockback"/>); 20 is a normal rifle bullet.</param>
public readonly record struct HitInfo(float Damage, Vector3 Direction, Vector3 Point, bool Crit = false, float Power = 20f,
    Player? Shooter = null, string WeaponType = "");
