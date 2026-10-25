using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

public sealed record CommonEncounterProfile(string DefaultPack, IReadOnlyDictionary<string, string> Areas);

public partial class Stage
{
    private readonly int[] _commonAppearanceBag = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
    private int _commonAppearanceNext = 10;
    private bool _commonAppearanceSeeded;

    private CommonIdentity RandomCommonAppearance(CommonIdentity identity)
    {
        if (!Authoritative) throw new InvalidOperationException("Common appearances are chosen only by the host");
        if (!_commonAppearanceSeeded) { _rng.Randomize(); _commonAppearanceSeeded = true; }
        // The host shuffles a ten-entry bag; every preset appears before the next refill.
        // Weapon slots remain in authored order. Clients receive the resulting birth value.
        if (_commonAppearanceNext == 10)
        {
            for (int i = 9; i > 0; i--)
            {
                int j = _rng.RandiRange(0, i);
                (_commonAppearanceBag[i], _commonAppearanceBag[j]) = (_commonAppearanceBag[j], _commonAppearanceBag[i]);
            }
            _commonAppearanceNext = 0;
        }
        return identity with { Variant = _commonAppearanceBag[_commonAppearanceNext++], Gear = identity.Faction == "armed"
            ? (_rng.Randf() < .35f ? CommonGear.Vest : CommonGear.None) | (_rng.Randf() < .25f ? CommonGear.Glasses : CommonGear.None) : CommonGear.None };
    }
    public Enemy SpawnCommonIdentity(CommonIdentity identity, Vector3 at, bool awake) => Spawn(CommonRoster.Current.Compose(identity), at, awake);

    public string CommonPackAt(Vector3 at, string? explicitArea = null)
    {
        var encounters = Director?.Encounters;
        if (encounters != null && encounters.CommonProfiles.TryGetValue(ThemeId, out var profile))
        {
            string area = explicitArea ?? (encounters.Areas.Count == 0 ? "" : encounters.Areas[encounters.AreaIndex(at.X)].Id);
            return profile.Areas.TryGetValue(area, out var pack) ? pack : profile.DefaultPack;
        }
        return DefaultCommonPack(ThemeId);
    }
    public static string DefaultCommonPack(string theme) => StageTheme.Normalize(theme) switch
    { "animals" => "hunter_eagle", "armed" => "armed_rhino", _ => "zombie_street" };

    public CommonIdentity[] CommonPack(Vector3 at, int count, string? area = null)
    {
        var identities = CommonRoster.Current.CreatePack(CommonPackAt(at, area), count, _commonIndex);
        for (int i = 0; i < identities.Length; i++) identities[i] = RandomCommonAppearance(identities[i]);
        _commonIndex = (_commonIndex + count) % 10;
        return identities;
    }
}
