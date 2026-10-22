using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// The party table (decisions.md M-5, M-31; content/data/party_scaling.csv): the rows load, only people count (not AI
/// squadmates), commons come in fewer or more on the start fill and in waves, health and damage are set at birth by tier
/// (commons by the common columns, named and bosses by the named ones), a boss's full bar includes it, and test scenes
/// keep the data's own numbers unless they ask.
/// </summary>
public partial class PartyScaleCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("PARTY_SCALE_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("PARTY_SCALE_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task<Stage> NewStage(int? party, int squad = 0)
    {
        if (_stage != null && IsInstanceValid(_stage)) { _stage.QueueFree(); await Frames(2); }
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", PartyOverride = party, SquadOverride = squad };
        AddChild(_stage);
        await Frames(3);
        _stage.Player.ManualControl = true;
        _stage.Player.Invulnerable = true;
        return _stage;
    }

    private int Commons => _stage.AllEnemies.Count(e => IsInstanceValid(e) && !e.IsDead && !e.IsSpecial);

    private async Task Clear()
    {
        foreach (var e in _stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
        await Frames(2);
    }

    private async Task Run()
    {
        // ---------- the table ----------
        var rows = PartyScaling.Rows;
        Check(PartyScaling.Errors.Count == 0 && rows.Select(r => r.Players).SequenceEqual(new[] { 1, 2, 3, 4, 5, 6 }),
            "party_scaling.csv loads: one row for each of 1 to 6 people");
        var one = PartyScaling.For(1);
        var six = PartyScaling.For(6);
        Check(Mathf.IsEqualApprox(PartyScaling.For(4).CommonCount, 1),
            "4 people is the data as written (100% everywhere)");
        Check(Mathf.IsEqualApprox(one.CommonCount, .5f) && Mathf.IsEqualApprox(one.CommonHp, .8f) && Mathf.IsEqualApprox(one.NamedHp, .5f) &&
              Mathf.IsEqualApprox(six.CommonCount, 1.4f) && Mathf.IsEqualApprox(six.NamedHp, 1.6f) && Mathf.IsEqualApprox(six.NamedDamage, 1.2f),
            "alone: half the commons at 80% health, named at half; six: 140% commons, named 160% health and 120% damage");
        Check(PartyScaling.For(0) == one && PartyScaling.For(9) == six, "fewer than one counts as one, more than six as six");
        var broken = new System.Collections.Generic.List<string>();
        PartyScaling.FromText("players,common_count_pct\n1,50\n", broken);
        Check(broken.Count > 0, $"a table missing columns is reported, not thrown ({broken[0]})");

        // ---------- who counts ----------
        var stage = await NewStage(null, squad: 1);
        Check(stage.Players.Count == 2 && stage.PartySize == 1, "an AI squadmate is not a person: alone with one is a party of 1");
        Check(stage.Party == PartyScale.Baseline, "a test scene keeps the data's numbers unless it sets a party");
        stage.PartyOverride = stage.PartySize;
        Check(stage.Party == one, "counted, that is the 1-person row (the running game scene counts the same way)");
        stage.PartyOverride = null;
        stage.AddRemoteSurvivor(CharacterCatalog.Player("duck"), stage.Map.PlayerStart + new Vector3(1.6f, 0, 0), 7);
        Check(stage.PartySize == 2, "another person's survivor counts");

        // ---------- the start fill ----------
        var baseline = await NewStage(4);
        int fullFill = Commons;
        int named = baseline.AllEnemies.Count(e => IsInstanceValid(e) && e.IsSpecial);
        var groups = SpawnDatabase.Current.Stage1.Groups.Where(g => g.Special < 0 && g.Area is "saferoom" or "street").Sum(g => g.Count);
        Check(fullFill > 0 && fullFill <= groups, $"4 people: the start fill places the groups as written ({fullFill} of {groups})");
        var alone = await NewStage(1);
        int halfFill = Commons;
        Check(Mathf.Abs(halfFill - groups * .5f) <= 1.5f && halfFill < fullFill, $"alone: about half the waiting commons ({halfFill} for {groups})");
        Check(alone.AllEnemies.Count(e => IsInstanceValid(e) && e.IsSpecial) == named, $"named in the fill stay as the map places them ({named})");
        var crowd = await NewStage(6);
        int bigFill = Commons;
        Check(bigFill > fullFill, $"six: more waiting commons ({bigFill} against {fullFill})");

        // ---------- waves ----------
        await NewStage(1);
        await Clear();
        var door = new SpawnEntry { Kind = EntryKind.Hidden, At = _stage.Map.PlayerStart + new Vector3(8, 0, 0) };
        int sent = 0;
        for (int i = 0; i < 4; i++) sent += _stage.Director.SpawnWave(door, 6);
        Check(sent >= 11 && sent <= 13, $"alone: four waves of 6 bring about 12 ({sent})");
        Check(_stage.ScaleCommons(1) + _stage.ScaleCommons(1) + _stage.ScaleCommons(1) + _stage.ScaleCommons(1) == 2,
            "small waves carry the fraction: four singles at 50% make two");
        _stage.PartyOverride = 6;
        await Clear();
        sent = 0;
        for (int i = 0; i < 4; i++) sent += _stage.Director.SpawnWave(door, 5);
        Check(sent >= 27 && sent <= 29, $"six: four waves of 5 bring about 28 ({sent})");

        // ---------- health and damage at birth ----------
        var spot = _stage.Map.PlayerStart + new Vector3(6, 0, 0);
        _stage.PartyOverride = 1;
        var common = _stage.SpawnCommon(spot, false);
        var bull = _stage.Spawn(MonsterDatabase.Get("bull"), spot + new Vector3(2, 0, 0), false);
        Check(Mathf.IsEqualApprox(common.Health, common.Monster.Hp * .8f) && Mathf.IsEqualApprox(common.DamageScale, 1),
            $"alone: a common is born at 80% health ({common.Health:0.#} of {common.Monster.Hp}), same damage");
        Check(Mathf.IsEqualApprox(bull.Health, bull.Monster.Hp * .5f), $"alone: a named one is born at half health ({bull.Health:0.#} of {bull.Monster.Hp})");
        _stage.PartyOverride = 6;
        var tough = _stage.SpawnCommon(spot + new Vector3(0, 0, 2), false);
        var bigBull = _stage.Spawn(MonsterDatabase.Get("bull"), spot + new Vector3(2, 0, 2), false);
        Check(Mathf.IsEqualApprox(tough.Health, tough.Monster.Hp * 1.1f) && Mathf.IsEqualApprox(tough.DamageScale, 1.1f),
            "six: a common at 110% health and damage");
        Check(Mathf.IsEqualApprox(bigBull.Health, bigBull.Monster.Hp * 1.6f) && Mathf.IsEqualApprox(bigBull.DamageScale, 1.2f),
            "six: a named one at 160% health and 120% damage (its charge too)");
        Check(Mathf.IsEqualApprox(common.Health, common.Monster.Hp * .8f), "a monster already out keeps what it was born with");
        var boss = (BossEnemy)_stage.Spawn(MonsterDatabase.Get(BossEnemy.MonsterId), spot + new Vector3(-4, 0, 4), false);
        Check(Mathf.IsEqualApprox(boss.MaxHealth, boss.Monster.Hp * 1.6f) && Mathf.IsEqualApprox(boss.Health, boss.MaxHealth),
            "six: the boss's full bar is 160% (its bar still starts full)");
        boss.SetHealthScale(.5f);
        Check(Mathf.IsEqualApprox(boss.Health, boss.Monster.Hp * .8f), "a lighter mid-run boss scales on top of the party (50% × 160%)");

        // ---------- test scenes ----------
        await NewStage(null);
        var plain = _stage.SpawnCommon(_stage.Map.PlayerStart + new Vector3(6, 0, 0), false);
        Check(Mathf.IsEqualApprox(plain.Health, plain.Monster.Hp) && _stage.ScaleCommons(7) == 7, "without a party set, a test scene changes nothing");
    }
}
