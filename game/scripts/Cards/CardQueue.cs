using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>What gives a card offer (decisions.md M-27): picking the weapons at the start, a level-up, a boss.</summary>
public enum CardKind { Arming, Normal, Special }

/// <summary>Who a card changes; drawn large on the card so nobody has to pick a target afterwards (plan 5.1).</summary>
public enum CardTarget { Slot1, Slot2, Slot3, Self, Summon, Common }

/// <summary>
/// One of the three cards on offer. <paramref name="Key"/> names the card for the network (<see cref="CardCatalog.FromKey"/>
/// rebuilds the same card on any PC); <paramref name="Apply"/> only runs on the deciding PC.
/// </summary>
public sealed record CardChoice(string Id, string Name, string Tag, string Description, CardTarget Target, Texture2D? Icon, Action<Player> Apply,
    string Key = "");

/// <summary>Three cards to pick one from. The cards are drawn when the offer is made and never change (plan 3장).</summary>
public sealed class CardOffer
{
    public CardKind Kind { get; init; }
    /// <summary>Numbers the offers of one queue (given when added); a client's pick names the offer it answers.</summary>
    public int Serial { get; set; }
    /// <summary>Language key of the title, with <see cref="TitleArg"/> filled in when it is 0 or more (slot numbers).</summary>
    public string TitleKey { get; init; } = "";
    public int TitleArg { get; init; } = -1;
    public string Title => TitleArg >= 0 ? Loc.Format(TitleKey, TitleArg) : Loc.Text(TitleKey);
    public IReadOnlyList<CardChoice> Choices { get; init; } = Array.Empty<CardChoice>();
    /// <summary>Arming cards must be picked; level-up and boss cards can be put off with X.</summary>
    public bool CanClose => Kind != CardKind.Arming;
}

/// <summary>
/// A survivor's waiting card offers, in the order they were given (decisions.md M-27: special and normal cards keep
/// their order, and an offer looked at once shows the same three cards when opened again). The window shows the front.
/// </summary>
public sealed class CardQueue
{
    private readonly List<CardOffer> _offers = new();
    private int _nextSerial;

    public int Count => _offers.Count;
    public CardOffer? Front => _offers.Count > 0 ? _offers[0] : null;
    public IReadOnlyList<CardOffer> Offers => _offers;
    /// <summary>Raised when an offer is added, picked or the queue is cleared.</summary>
    public event Action? Changed;
    public int Picked { get; private set; }
    /// <summary>Goes up on every change (the host sends a client its queue again when this moves).</summary>
    public int Version { get; private set; }

    public void Add(CardOffer offer)
    {
        if (offer.Serial == 0) offer.Serial = ++_nextSerial;
        _offers.Add(offer);
        Touch();
    }

    /// <summary>Takes card <paramref name="index"/> of the front offer and applies it to <paramref name="who"/>.</summary>
    public CardChoice? Choose(Player who, int index)
    {
        if (Front is not { } offer || index < 0 || index >= offer.Choices.Count) return null;
        var choice = offer.Choices[index];
        _offers.RemoveAt(0);
        Picked++;
        choice.Apply(who);
        Touch();
        return choice;
    }

    /// <summary>A client's pick on its way to the host: the front offer goes away here at once, the host applies it.</summary>
    public void DropFront()
    {
        if (_offers.Count == 0) return;
        _offers.RemoveAt(0);
        Touch();
    }

    /// <summary>A client takes the host's copy of its queue as it is.</summary>
    public void Replace(IEnumerable<CardOffer> offers)
    {
        _offers.Clear();
        _offers.AddRange(offers);
        Touch();
    }

    public void Clear()
    {
        _offers.Clear();
        Touch();
    }

    private void Touch()
    {
        Version++;
        Changed?.Invoke();
    }
}

/// <summary>
/// Onslaught's arming cards (decisions.md M-27 plan 13.1): slot 1 and slot 2 guns from the mode's weapon_pool, slot 3
/// from its special weapons. The two gun offers never share a weapon, so slot 2 cannot repeat slot 1 even though both
/// offers are drawn up front.
/// </summary>
public static class ArmingCards
{
    public static IEnumerable<CardOffer> Draw(IReadOnlyList<string> pool, RandomNumberGenerator rng, int perOffer = 3)
    {
        var defs = pool.Select(ArmoryCatalog.Weapon).ToList();
        var guns = defs.Where(d => d.Slot != WeaponSlot.Special && d.Mode != FireMode.Melee).OrderBy(_ => rng.Randf()).ToList();
        var specials = defs.Where(d => d.Slot == WeaponSlot.Special).OrderBy(_ => rng.Randf()).ToList();
        yield return Offer(1, guns.Take(perOffer));
        yield return Offer(2, guns.Skip(perOffer).Take(perOffer));
        if (specials.Count > 0) yield return Offer(3, specials.Take(perOffer));
    }

    private static CardOffer Offer(int slot, IEnumerable<WeaponDefinition> defs) => new()
    {
        Kind = CardKind.Arming,
        TitleKey = "fmt_card_arming_title",
        TitleArg = slot,
        Choices = defs.Select(d => WeaponCard(slot, d)).ToList(),
    };

    /// <summary>A weapon as a card: what it is, its round and the numbers that tell guns apart.</summary>
    public static CardChoice WeaponCard(int slot, WeaponDefinition def)
    {
        // The honk and the rubber duck are not guns: their own line and "cooldown" instead of numbers and a round.
        bool gadget = def.Mode is FireMode.Shout or FireMode.Throw;
        var caliber = gadget ? Loc.Text("ui_card_weapon_cooldown") : CaliberDatabase.Get(def.Caliber)?.DisplayName ?? "—";
        var tag = Loc.Format("fmt_card_weapon_tag", slot, Loc.Text("name_weapontype_" + def.TypeId), caliber);
        var desc = gadget ? Loc.Text("desc_weapon_" + def.Id)
            : Loc.Format("fmt_card_weapon_desc", def.Damage, def.MagSize, Mathf.RoundToInt(60f / Mathf.Max(def.FireInterval, .01f)), def.MaxRange);
        var target = slot switch { 1 => CardTarget.Slot1, 2 => CardTarget.Slot2, _ => CardTarget.Slot3 };
        return new CardChoice(def.Id, def.DisplayName, tag, desc, target, def.LoadIcon(), who =>
        {
            if (slot == 3)
            {
                who.Loadout.Equip(3, new WeaponState(def));
                who.Weapons.RefreshVisual();
            }
            else who.Weapons.PlaceInSlot(slot, def);
        }, $"w|{def.Id}|{slot}");
    }
}
