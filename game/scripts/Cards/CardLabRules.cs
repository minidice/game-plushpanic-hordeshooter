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
    public override LabCards? CardEffects => Cards;
    /// <summary>The local survivor's waiting card offers (arming after a cheat reset, test level-ups).</summary>
    public CardQueue Queue { get; } = new();
    /// <summary>A group of commons every few seconds around the survivor.</summary>
    public bool Trickle { get; set; }
    /// <summary>The survivor's companions (C adds the next kind, Shift+C clears).</summary>
    public CompanionSquad Companions { get; private set; } = null!;
    private float _trickleTimer;
    private int _nextCompanion;

    /// <summary>What C cycles through: each pet, drone and sidekick once, with differing gear.</summary>
    public static readonly string[] CompanionCycle =
    {
        "pet:dog:twin_blaster", "drone:bee:laser", "sidekick:cadet", "pet:turtle:rocket_pod", "drone:quadcopter:blaster",
        "sidekick:scout", "pet:rabbit:twin_blaster", "drone:ladybug:support", "sidekick:engineer", "drone:ufo:light", "sidekick:guard",
    };

    public override void _Ready()
    {
        base._Ready();
        Cards = new LabCards
        {
            Name = "LabCards", Stage = Stage, HonkChargeMinCooldown = Tuning.Survivor.CooldownCardMin,
            DestinyPool = Tuning.Survivor.WeaponPool.Where(id => ArmoryCatalog.Weapon(id).Slot == WeaponSlot.Special).ToList(),
        };
        AddChild(Cards);
        Companions = new CompanionSquad { Name = "Companions", Stage = Stage };
        AddChild(Companions);
    }

    /// <summary>The next companion of <see cref="CompanionCycle"/> joins the survivor.</summary>
    public Companion? AddNextCompanion() => Companions.Add(Stage.Player, CompanionCycle[_nextCompanion++ % CompanionCycle.Length]);

    protected override void Tick(float dt)
    {
        foreach (var p in Stage.Players)
            if (IsInstanceValid(p)) p.Loadout.UseOnslaughtAmmo(Tuning.Survivor.SpecialCooldown, Tuning.Survivor.SpecialCooldowns);
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
        Cards.ClearAll();
        Companions.Clear();
        Queue.Clear();
        var loadout = Stage.Player.Loadout;
        loadout.SetSlot(1, ArmoryCatalog.Weapon(ArmoryCatalog.StartingWeapon));
        loadout.Unequip(2);
        loadout.Unequip(3);
        Stage.Player.Weapons.RefreshVisual();
        foreach (var offer in ArmingCards.Draw(Tuning.Survivor.WeaponPool, Rng)) Queue.Add(offer);
        Queue.Add(Companions.Offer(Rng));
    }

    /// <summary>A test level-up: the same offer an Onslaught level-up gives this survivor (new cards, upgrades of taken ones, repeatables).</summary>
    public void TestLevelUp() => Queue.Add(CardCatalog.LevelUp(Cards, Rng, Stage.Player));

    /// <summary>A boss-reward cheat: the stand-in special cards that are still off, as a special (gold) offer.</summary>
    public void TestSpecial()
    {
        // Three of those not on yet, glass cannon (the one with a price) first while it is off.
        var off = LabCards.Specials.Where(s => !Cards.IsOn(s)).OrderBy(s => s == LabSpecial.GlassCannon ? 0 : 1).Take(3).ToList();
        if (off.Count == 0) return;
        Queue.Add(new CardOffer { Kind = CardKind.Special, TitleKey = "ui_card_special_title", Choices = off.Select(s => CardCatalog.Special(s, Cards)).ToList() });
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
