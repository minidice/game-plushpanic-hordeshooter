using Godot;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Rules for the card lab (tests/CardLab.tscn, decisions.md M-27 `[임시]`): Onslaught's open field and unlimited
/// reserves, but no arming, no clock and no boss. Monsters come only on request (a ring, a named one) or as a steady
/// trickle when it is switched on. Owns the <see cref="LabCards"/>.
/// </summary>
public partial class CardLabRules : ModeRules
{
    public override GameMode Mode => GameMode.Survivor;
    public override string ObjectiveText => Loc.Text("ui_cardlab_objective");
    public LabCards Cards { get; private set; } = null!;
    /// <summary>The local survivor's waiting card offers (arming after a cheat reset, test level-ups).</summary>
    public CardQueue Queue { get; } = new();
    /// <summary>A group of commons every few seconds around the survivor.</summary>
    public bool Trickle { get; set; }
    private float _trickleTimer;

    public override void _Ready()
    {
        base._Ready();
        Cards = new LabCards { Name = "LabCards", Stage = Stage };
        AddChild(Cards);
    }

    protected override void Tick(float dt)
    {
        foreach (var p in Stage.Players)
            if (IsInstanceValid(p)) p.Loadout.UseOnslaughtAmmo(Tuning.Survivor.SpecialCooldown);
    }

    protected override void Step(float dt)
    {
        if (!Trickle) return;
        _trickleTimer -= dt;
        if (_trickleTimer > 0) return;
        _trickleTimer = 2.5f;
        SpawnRing(6, 12, 16);
    }

    /// <summary>Up to <paramref name="count"/> awake commons on open ground between the two distances from the survivor.</summary>
    public int SpawnRing(int count, float min, float max)
    {
        var center = Stage.Player.GlobalPosition;
        var spots = Stage.Flow.OpenCellsNear(center, min, max).OrderBy(_ => Rng.Randf()).Take(count).ToList();
        foreach (var at in spots) Stage.SpawnCommon(at, awake: true);
        return spots.Count;
    }

    /// <summary>One monster of <paramref name="id"/> a little way ahead of the survivor.</summary>
    public Enemy? SpawnMonster(string id, float distance = 8)
    {
        var center = Stage.Player.GlobalPosition;
        var at = Stage.Flow.OpenCellsNear(center, distance, distance + 3).OrderBy(_ => Rng.Randf()).FirstOrDefault();
        return at == default ? null : Stage.Spawn(MonsterDatabase.Get(id), at, awake: true);
    }

    /// <summary>
    /// Cheat reset: no monsters, no cards, a frying pan only, then the three arming offers (slot 1, 2, 3) one after
    /// another, as an Onslaught match will start (decisions.md M-27 plan 13.1).
    /// </summary>
    public void CheatReset()
    {
        Trickle = false;
        ClearEnemies();
        foreach (var c in LabCards.All) Cards.Set(c, false);
        foreach (var s in LabCards.Specials) Cards.Set(s, false);
        Queue.Clear();
        var loadout = Stage.Player.Loadout;
        loadout.SetSlot(1, ArmoryCatalog.Weapon(ArmoryCatalog.StartingWeapon));
        loadout.Unequip(2);
        loadout.Unequip(3);
        Stage.Player.Weapons.RefreshVisual();
        foreach (var offer in ArmingCards.Draw(Tuning.Survivor.WeaponPool, Rng)) Queue.Add(offer);
    }

    /// <summary>A test level-up: three lab cards that are still off, as a normal card offer.</summary>
    public void TestLevelUp()
    {
        var off = LabCards.All.Where(c => !Cards.IsOn(c)).OrderBy(_ => Rng.Randf()).Take(3).ToList();
        if (off.Count == 0) return;
        Queue.Add(new CardOffer
        {
            Kind = CardKind.Normal,
            Title = Loc.Text("ui_card_levelup_title"),
            Choices = off.Select(c => new CardChoice(c.ToString(), Loc.Text(LabCards.Key("name_card_", c)), Loc.Text("tag_card_common"),
                Loc.Text(LabCards.Key("desc_card_", c)), CardTarget.Common, null, _ => Cards.Set(c, true))).ToList(),
        });
    }

    /// <summary>A boss-reward cheat: the stand-in special cards that are still off, as a special (gold) offer.</summary>
    public void TestSpecial()
    {
        var off = LabCards.Specials.Where(s => !Cards.IsOn(s)).ToList();
        if (off.Count == 0) return;
        Queue.Add(new CardOffer
        {
            Kind = CardKind.Special,
            Title = Loc.Text("ui_card_special_title"),
            Choices = off.Select(s => new CardChoice(s.ToString(), Loc.Text(LabCards.Key("name_card_", s)), Loc.Text("tag_card_special"),
                Loc.Text(LabCards.Key("desc_card_", s)), s == LabSpecial.GlassCannon ? CardTarget.Self : CardTarget.Common, null,
                _ => Cards.Set(s, true))).ToList(),
        });
    }

    /// <summary>A training log at <paramref name="at"/> (it never dies and stays put).</summary>
    public TrainingDummy SpawnDummy(Vector3 at) => Stage.SpawnEnemy<TrainingDummy>(MonsterDatabase.Get("plush"), new Vector3(at.X, 0, at.Z), awake: false);

    public void RemoveDummies()
    {
        foreach (var d in Stage.AllEnemies.OfType<TrainingDummy>().ToList()) d.QueueFree();
    }

    /// <summary>Removes the monsters; training logs stay.</summary>
    public void ClearEnemies()
    {
        foreach (var e in Stage.Enemies.ToList())
            if (IsInstanceValid(e) && !e.IsDead && e is not TrainingDummy) e.QueueFree();
    }
}
