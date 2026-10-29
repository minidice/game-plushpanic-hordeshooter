using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Worn gear in real shots (decisions.md C-32 · C-34 · C-36, equipment-implementation-plan.md Task 2): the crit roll, crit
/// damage with the focus-only bonus kept off chance crits, aiming-only options, one shared roll for a shotgun's pellets,
/// spread and recoil scaled per shooter, and a remote person's focus judged on the host. Headless.
/// </summary>
public partial class EquipmentCombatCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private Player _player = null!;
    private Enemy _mark = null!;
    private readonly List<(int Shot, bool Crit, float Damage)> _hits = new();

    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("EQUIPMENT_COMBAT_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("EQUIPMENT_COMBAT_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private static GearItem Gear(string id) => EquipmentDatabase.Current.Create(id, new RandomNumberGenerator { Seed = 1 }) ?? throw new Exception(id);

    /// <summary>A gear piece outside the data, for an exact chance (100% or 50%).</summary>
    private static GearItem Custom(EquipmentSlot slot, string stat, string condition, float value)
    {
        var db = EquipmentDatabase.Current;
        var option = new OptionDefinition($"test_{stat}_{value}", db.Stats[stat], db.Conditions[condition], null, value, 1);
        return new GearItem(new EquipmentDefinition($"test_{slot}", slot, "name_gear_keen_cap", "", "test"), option);
    }

    private async Task Run()
    {
        Pure();
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 1 };
        AddChild(_stage);
        await Frames(3);
        _player = _stage.Player;
        _player.ManualControl = true;
        _player.Invulnerable = true;
        var m4 = ArmoryCatalog.Weapon("m4");
        _player.Loadout.SetSlot(2, m4);
        _player.Weapons.SelectSlot(2);
        var near = _player.GlobalPosition + new Vector3(3.5f, 1, .5f);
        _mark = _stage.SpawnCommon(near with { Y = 0 }, awake: false);
        _mark.ScaleHealth(1000);
        await Frames(3);
        _mark.Stun(600);
        _stage.EnemyHit += (e, h) => { if (e == _mark && h.Shooter == _player) _hits.Add((_player.Weapons.ShotsFired, h.Crit, h.Damage)); };

        await CritDamage(m4);
        await Pellets();
        await SpreadAndRecoil(m4);
        await RemoteFocus();
    }

    private void Pure()
    {
        Check(EquipmentCombat.IsCritical(false, .03f, .029f) && !EquipmentCombat.IsCritical(false, .03f, .031f), "a 3% chance crits on a roll of 0.029, not 0.031");
        Check(EquipmentCombat.IsCritical(true, 0, .999f) && !EquipmentCombat.IsCritical(false, 0, 0), "a focused shot always crits; no chance never does");
        var m4 = ArmoryCatalog.Weapon("m4");
        var plain = new WeaponRecoil();
        var braced = new WeaponRecoil();
        plain.Kick(m4, new RandomNumberGenerator { Seed = 7 });
        braced.Kick(m4, new RandomNumberGenerator { Seed = 7 }, .75f);
        Check(braced.Offset.IsEqualApprox(plain.Offset * .75f), $"recoil scaled to 0.75 kicks 0.75 as far ({braced.Offset} vs {plain.Offset})");
    }

    /// <summary>One shot at the mark; <paramref name="focused"/> lines up the 0.5 s focus first, otherwise any focus is spent.</summary>
    private async Task<(bool Crit, float Damage)> Shoot(bool focused, bool aiming)
    {
        var at = _mark.GlobalPosition + Vector3.Up;
        _player.Gun.FillUp();
        _player.HoldAim(aiming);
        await Frames(30);
        if (focused)
        {
            for (int i = 0; i < 90 && !(_player.CritReady && _player.FocusTarget == _mark); i++) { _player.SetControls(Vector2.Zero, at, false); await Frames(1); }
            if (!_player.CritReady) throw new Exception("could not line up a focus");
        }
        else
        {
            _player.SetControls(Vector2.Zero, at, false);
            await Frames(1);
            _player.SpendFocus();
        }
        int before = _hits.Count;
        _player.SetControls(Vector2.Zero, at, true);
        await Frames(1);
        _player.SetControls(Vector2.Zero, at, false);
        await Frames(1);
        if (_hits.Count != before + 1) throw new Exception($"expected one hit, got {_hits.Count - before}");
        return (_hits[^1].Crit, _hits[^1].Damage);
    }

    private async Task CritDamage(WeaponDefinition m4)
    {
        float mult = m4.CritMultiplier;
        var plain = await Shoot(false, false);
        var focus = await Shoot(true, false);
        Check(!plain.Crit && focus.Crit && Mathf.IsEqualApprox(focus.Damage / plain.Damage, mult), $"without gear: plain {plain.Damage:0.#}, focused crit ×{mult}");

        _player.Gear.Equip(Gear("focus_band"));
        var bandPlain = await Shoot(false, false);
        var bandFocus = await Shoot(true, false);
        Check(!bandPlain.Crit && Mathf.IsEqualApprox(bandPlain.Damage, plain.Damage), "the focus band leaves a plain shot alone");
        Check(bandFocus.Crit && Mathf.IsEqualApprox(bandFocus.Damage / plain.Damage, mult * 1.3f), $"the focus band makes the focused shot ×{mult} × 1.3 ({bandFocus.Damage / plain.Damage:0.###})");

        _player.Gear.Equip(Custom(EquipmentSlot.Armor, "crit_chance", "always", 100));
        var lucky = await Shoot(false, false);
        Check(lucky.Crit && Mathf.IsEqualApprox(lucky.Damage / plain.Damage, mult), "a chance crit gets the weapon's crit damage but not the focus band's bonus");
        var luckyFocus = await Shoot(true, false);
        Check(luckyFocus.Crit && Mathf.IsEqualApprox(luckyFocus.Damage / plain.Damage, mult * 1.3f), "the focused shot still gets the band's bonus");

        _player.Gear.Equip(Gear("hunter_hat"));
        var hip = await Shoot(false, false);
        var aimed = await Shoot(false, true);
        Check(hip.Crit && Mathf.IsEqualApprox(hip.Damage / plain.Damage, mult), "the hunter hat does nothing from the hip");
        var aimedPlainRatio = aimed.Damage / plain.Damage;
        Check(aimed.Crit && Mathf.IsEqualApprox(aimedPlainRatio, mult * 1.2f), $"aimed, the hunter hat adds 20% crit damage ({aimedPlainRatio:0.###})");

        _player.Gear.Equip(Custom(EquipmentSlot.Armor, "crit_chance", "aiming", 100));
        var hipNoChance = await Shoot(false, false);
        var aimedChance = await Shoot(false, true);
        Check(!hipNoChance.Crit && aimedChance.Crit, "an aiming-only chance stops when the aim is let go");
        _player.HoldAim(false);
    }

    private async Task Pellets()
    {
        var r870 = ArmoryCatalog.Weapon("r870");
        _player.Loadout.SetSlot(2, r870);
        _player.Weapons.SelectSlot(2);
        _player.Gear.Equip(Custom(EquipmentSlot.Armor, "crit_chance", "always", 50));
        await Frames(5);
        var at = _mark.GlobalPosition + Vector3.Up;
        var shots = new Dictionary<int, List<bool>>();
        for (int s = 0; s < 16; s++)
        {
            _player.Gun.FillUp();
            _player.SetControls(Vector2.Zero, at, false);
            await Frames(Mathf.CeilToInt(r870.FireInterval * Engine.PhysicsTicksPerSecond) + 2);
            _player.SpendFocus();
            int before = _hits.Count;
            _player.SetControls(Vector2.Zero, at, true);
            await Frames(1);
            _player.SetControls(Vector2.Zero, at, false);
            foreach (var hit in _hits.Skip(before)) (shots.TryGetValue(hit.Shot, out var l) ? l : shots[hit.Shot] = new List<bool>()).Add(hit.Crit);
        }
        int multi = shots.Values.Count(p => p.Count > 1);
        Check(shots.Count >= 12 && multi >= 10, $"the shotgun puts several pellets into the mark ({multi} of {shots.Count} shots: {string.Join(" ", shots.Values.Select(p => p.Count))})");
        Check(shots.Values.All(p => p.All(c => c == p[0])), "one roll per shot: a shot's pellets all crit or none do");
        Check(shots.Values.Any(p => p[0]) && shots.Values.Any(p => !p[0]),
            $"at 50% some shots crit and some do not ({shots.Values.Count(p => p[0])}/{shots.Count})");
        _player.Gear.Equip(Custom(EquipmentSlot.Armor, "crit_chance", "always", 0));
    }

    private async Task SpreadAndRecoil(WeaponDefinition m4)
    {
        _player.Loadout.SetSlot(2, m4);
        _player.Weapons.SelectSlot(2);
        var bot = _stage.Players.First(p => p.IsBot);
        bot.Loadout.SetSlot(2, m4);
        bot.Weapons.SelectSlot(2);
        _player.Gear.Equip(Gear("steady_vest"));
        _player.HoldAim(false);
        await Frames(40);
        var gun = _player.Gun;
        Check(Mathf.IsEqualApprox(_player.Weapons.ShotCone(gun), gun.Spread * .9f), $"the steady vest narrows the cone to 90% ({_player.Weapons.ShotCone(gun):0.##}° of {gun.Spread:0.##}°)");
        Check(Mathf.IsEqualApprox(bot.Weapons.ShotCone(bot.Gun), bot.Gun.Spread), "a squadmate with the same gun keeps its own cone");
        _player.Gear.Equip(Gear("marksman_vest"));
        Check(Mathf.IsEqualApprox(_player.Weapons.ShotCone(gun), gun.Spread), "the marksman vest does nothing from the hip");
        Check(Mathf.IsEqualApprox(_player.Weapons.ShotCone(gun, fullyAimed: true), gun.AimedSpread * .85f), "the marksman vest narrows the aimed cone to 85%");
        _player.HoldAim(true);
        await Frames(Mathf.CeilToInt(m4.AimTime * Engine.PhysicsTicksPerSecond) + 4);
        Check(_player.IsAiming && Mathf.IsEqualApprox(_player.Weapons.ShotCone(gun), gun.Spread * .85f), "aimed, the marksman vest narrows the live cone too");

        _player.Gear.Equip(Gear("shoulder_harness"));
        await Frames(60);
        _player.Recoil.Clear();
        var at = _mark.GlobalPosition + Vector3.Up;
        _player.Gun.FillUp();
        _player.SetControls(Vector2.Zero, at, true);
        await Frames(1);
        _player.SetControls(Vector2.Zero, at, false);
        Check(Mathf.IsEqualApprox(_player.Recoil.Offset.Y, -m4.RecoilUp * .75f), $"aimed, the shoulder harness kicks the aim point 75% as high ({_player.Recoil.Offset.Y:0.#} px)");
        _player.HoldAim(false);
        await Frames(60);
        _player.Recoil.Clear();
        _player.Gun.FillUp();
        _player.SetControls(Vector2.Zero, at, true);
        await Frames(1);
        _player.SetControls(Vector2.Zero, at, false);
        Check(Mathf.IsEqualApprox(_player.Recoil.Offset.Y, -m4.RecoilUp), "from the hip the harness does nothing");
    }

    private async Task RemoteFocus()
    {
        var at = _mark.GlobalPosition + Vector3.Up;
        var remote = _stage.AddSurvivor(CharacterCatalog.Player("goose"), _player.GlobalPosition + new Vector3(0, 0, 2), false, "remote");
        remote.IsRemote = true;
        remote.ManualControl = true;
        remote.Invulnerable = true;
        var bot = _stage.Players.First(p => p.IsBot);
        // The host's vision would mark the monster seen again every frame; hold it out of the host's sight.
        _stage.Vision.ProcessMode = ProcessModeEnum.Disabled;
        _mark.IsSeen = false;
        await Frames(2);
        Check(!_mark.IsSeen, "the monster stays out of the host's sight for this part");
        for (int i = 0; i < 90 && !remote.CritReady; i++)
        {
            remote.SetControls(Vector2.Zero, at, false);
            _player.SetControls(Vector2.Zero, at, false);
            await Frames(1);
        }
        Check(remote.CritReady && remote.FocusTarget == _mark, "on the host a remote person lines up a focus from their own aim, not the host's view");
        Check(_player.FocusTarget != _mark, "the host's own focus still needs the monster in sight");
        Check(!bot.CritReady, "squadmates never get a focus crit");
        _mark.IsSeen = true;
    }
}
