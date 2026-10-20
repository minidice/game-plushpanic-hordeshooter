using Godot;
using System.Collections.Generic;

namespace HordeShooter;

public enum WeaponSlot
{
    /// <summary>Key 1: melee or pistols. Never runs out of reserve ammo.</summary>
    Secondary = 1,
    /// <summary>Key 2: everything else. Limited ammo.</summary>
    Primary = 2,
}

public enum WeaponCategory
{
    Melee,
    Pistol,
    Smg,
    Rifle,
    Lmg,
    Shotgun,
    Sniper,
    Heavy,
}

public enum FireMode
{
    /// <summary>Arc swing that hits everything in front (trait melee_arc).</summary>
    Melee,
    /// <summary>Instant rays (bullets, pellets).</summary>
    Hitscan,
    /// <summary>Straight flying projectile (trait projectile), usually with explode_on_hit.</summary>
    Projectile,
    /// <summary>Lobbed shell that lands on the aim point after a delay, over walls (trait lob).</summary>
    Lob,
}

/// <summary>
/// One weapon after data resolution: a row of content/data/weapons.csv, with empty cells filled from its
/// type in weapon_types.csv and trait JSON files applied. Built by <see cref="WeaponDatabase"/>; never
/// edited at runtime. Times are seconds and distances metres here (the CSV uses ms and m).
/// </summary>
public sealed class WeaponDefinition
{
    public string Id { get; set; } = "";
    /// <summary>Weapon row this came from (differs from Id only for derived variants such as dual pistols).</summary>
    public string BaseId { get; set; } = "";
    public string TypeId { get; set; } = "";
    /// <summary>Localization key (content/lan/*/weapon.csv), e.g. name_weapon_ak47.</summary>
    public string NameKey { get; set; } = "";
    /// <summary>Format key for the dual version's name ("쌍 {0}" / "Dual {0}").</summary>
    public string DualFormatKey { get; set; } = "fmt_weapon_dual";
    /// <summary>Name in the current language.</summary>
    public string DisplayName => Dual ? Loc.Format(DualFormatKey, Loc.Text(NameKey)) : Loc.Text(NameKey);
    public WeaponSlot Slot { get; set; } = WeaponSlot.Primary;
    public WeaponCategory Category { get; set; } = WeaponCategory.Rifle;
    public FireMode Mode { get; set; } = FireMode.Hitscan;
    /// <summary>Hold to fire. False = one shot (or one burst) per click.</summary>
    public bool Automatic { get; set; } = true;

    // ---------- damage & range ----------
    /// <summary>Per shot. With pellets this is the total if every pellet hits.</summary>
    public float Damage { get; set; } = 10;
    /// <summary>Bullets stop here. For melee: reach.</summary>
    public float MaxRange { get; set; } = 40;
    /// <summary>Full damage up to here, then linear falloff to <see cref="FalloffMin"/> at <see cref="MaxRange"/>.</summary>
    public float EffectiveRange { get; set; } = 30;
    public float FalloffMin { get; set; } = .6f;
    public int Pierce { get; set; } = 1;
    /// <summary>Knockback power applied to the shooter per shot (see <see cref="Knockback"/>).</summary>
    public float RecoilPower { get; set; }
    /// <summary>Knockback power applied to enemies per hit (split across pellets).</summary>
    public float HitPower { get; set; } = 20;

    // ---------- rate & ammo ----------
    public float FireInterval { get; set; } = .1f;
    public float ReloadTime { get; set; } = 2f;
    /// <summary>Zero: replace magazine. Positive: insert one shell per interval after ReloadTime startup.</summary>
    public float ShellReloadTime { get; set; }
    public int MagSize { get; set; } = 30;
    public int ReserveStart { get; set; }
    public int ReserveMax { get; set; }
    public bool InfiniteReserve { get; set; }
    public bool Refillable { get; set; } = true;

    // ---------- spread (degrees) ----------
    public float BaseSpread { get; set; } = 1.5f;
    public float SpreadPerShot { get; set; } = 1.7f;
    public float MoveSpread { get; set; } = 4f;
    public float MaxSpread { get; set; } = 12f;
    public float SpreadRecovery { get; set; } = 18f;

    // ---------- traits (filled from weapon_traits/*.json) ----------
    public int Pellets { get; set; } = 1;
    public float PelletSpread { get; set; }
    public int BurstCount { get; set; } = 1;
    public float BurstInterval { get; set; } = .065f;
    public float MeleeArc { get; set; } = 110;
    public int MeleeMaxTargets { get; set; } = 4;
    public float ProjectileSpeed { get; set; } = 24;
    public float ProjectileLifetime { get; set; } = 2.5f;
    public float FlightTime { get; set; } = 1.3f;
    public float MinRange { get; set; }
    public float ExplosionRadius { get; set; }
    public float ExplosionDamage { get; set; }
    public float ExplosionSelfShare { get; set; } = .2f;
    /// <summary>Knockback power at the center of the explosion (falls off with distance).</summary>
    public float ExplosionKnockback { get; set; } = 150;
    /// <summary>What this weapon becomes when the same one is picked up again (trait dual_wield).</summary>
    public WeaponDefinition? DualVariant { get; set; }
    public bool Dual { get; set; }
    public List<string> Traits { get; set; } = new();

    // ---------- presentation ----------
    public string FireSound { get; set; } = "";
    public string ModelPath { get; set; } = "";
    public string IconPath { get; set; } = "";
    public Color BodyColor { get; set; } = new("2f3330");
    public Color AccentColor { get; set; } = new("6b5a3f");
    public float Length { get; set; } = .75f;
    /// <summary>Metres around the shooter (or the swing) in which idle monsters wake (noise_m).</summary>
    public float Noise { get; set; } = 16f;
    public float Thickness { get; set; } = .12f;

    public float MeleeRange => MaxRange;
    public bool UsesAmmo => Mode != FireMode.Melee;
    public bool Explodes => ExplosionRadius > 0;

    /// <summary>Damage multiplier for a hit at <paramref name="distance"/> metres.</summary>
    public float FalloffAt(float distance)
    {
        if (distance <= EffectiveRange || MaxRange <= EffectiveRange) return 1;
        float t = Mathf.Clamp((distance - EffectiveRange) / (MaxRange - EffectiveRange), 0, 1);
        return Mathf.Lerp(1, FalloffMin, t);
    }

    public string ShotSound => FireSound != "" ? FireSound : Mode switch
    {
        FireMode.Melee => "melee_swing",
        FireMode.Projectile => "rocket_launch",
        FireMode.Lob => "mortar_fire",
        _ => Category switch
        {
            WeaponCategory.Pistol => "shot_pistol",
            WeaponCategory.Smg => "shot_smg",
            WeaponCategory.Shotgun => "shot_shotgun",
            WeaponCategory.Sniper => "shot_sniper",
            WeaponCategory.Lmg => "shot_lmg",
            _ => "shot_rifle",
        },
    };

    /// <summary>The model file if one is set (loaded on demand; ResourceLoader caches it).</summary>
    public PackedScene? LoadModel() => ModelPath != "" && ResourceLoader.Exists(ModelPath) ? ResourceLoader.Load<PackedScene>(ModelPath) : null;

    public Texture2D? LoadIcon() => IconPath != "" && ResourceLoader.Exists(IconPath) ? ResourceLoader.Load<Texture2D>(IconPath) : null;

    public WeaponDefinition Clone()
    {
        var copy = (WeaponDefinition)MemberwiseClone();
        copy.Traits = new List<string>(Traits);
        return copy;
    }
}

/// <summary>
/// Knockback power → speed. Powers in the data are unit-less (pistol ~3, shotgun ~60, rocket ~100) so a
/// future "reduce knockback" option only has to change <see cref="GameSettings.RecoilScale"/>.
/// </summary>
public static class Knockback
{
    /// <summary>Metres per second of push per point of power.</summary>
    public const float SpeedPerPower = .08f;

    /// <summary>Push speed for the shooter. Scaled by the player's option.</summary>
    public static float Recoil(float power) => power * SpeedPerPower * GameSettings.RecoilScale;

    /// <summary>Push speed for an enemy that was hit.</summary>
    public static float Hit(float power) => power * SpeedPerPower;
}

