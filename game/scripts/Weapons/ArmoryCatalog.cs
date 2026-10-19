using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>Weapons and throwables both come from the CSV/JSON data (<see cref="WeaponDatabase"/>, <see cref="ThrowableDatabase"/>).</summary>
public static class ArmoryCatalog
{
    public const string StartingWeapon = "frying_pan";
    public const string WeaponDataDir = "res://content/data";

    public static IReadOnlyDictionary<string, WeaponDefinition> Weapons => WeaponDatabase.Current.Weapons;
    public static IReadOnlyDictionary<string, ThrowableDefinition> Throwables => ThrowableDatabase.Current.Throwables;

    public static WeaponDefinition Weapon(string id) =>
        Weapons.TryGetValue(id, out var def) ? def : throw new KeyNotFoundException($"weapon '{id}' not in {WeaponDataDir}/weapons.csv");

    public static WeaponDefinition? FindWeapon(string id) => Weapons.GetValueOrDefault(id);

    public static ThrowableDefinition Throwable(string id) =>
        Throwables.TryGetValue(id, out var def) ? def : throw new KeyNotFoundException($"throwable '{id}' not in {ThrowableDatabase.TablePath}");

    public static Loadout NewLoadout() => new(Weapon(StartingWeapon));
}
