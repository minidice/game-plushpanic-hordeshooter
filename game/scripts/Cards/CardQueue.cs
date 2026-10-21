using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>What gives a card offer (decisions.md M-27): picking the weapons at the start, a level-up, a boss.</summary>
public enum CardKind { Arming, Normal, Special }

/// <summary>Who a card changes; drawn large on the card so nobody has to pick a target afterwards (plan 5.1).</summary>
public enum CardTarget { Slot1, Slot2, Slot3, Self, Summon, Common }

/// <summary>One of the three cards on offer.</summary>
public sealed record CardChoice(string Id, string Name, string Tag, string Description, CardTarget Target, Texture2D? Icon, Action<Player> Apply);

/// <summary>Three cards to pick one from. The cards are drawn when the offer is made and never change (plan 3장).</summary>
public sealed class CardOffer
{
    public CardKind Kind { get; init; }
    public string Title { get; init; } = "";
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

    public int Count => _offers.Count;
    public CardOffer? Front => _offers.Count > 0 ? _offers[0] : null;
    public IReadOnlyList<CardOffer> Offers => _offers;
    /// <summary>Raised when an offer is added, picked or the queue is cleared.</summary>
    public event Action? Changed;
    public int Picked { get; private set; }

    public void Add(CardOffer offer)
    {
        _offers.Add(offer);
        Changed?.Invoke();
    }

    /// <summary>Takes card <paramref name="index"/> of the front offer and applies it to <paramref name="who"/>.</summary>
    public CardChoice? Choose(Player who, int index)
    {
        if (Front is not { } offer || index < 0 || index >= offer.Choices.Count) return null;
        var choice = offer.Choices[index];
        _offers.RemoveAt(0);
        Picked++;
        choice.Apply(who);
        Changed?.Invoke();
        return choice;
    }

    public void Clear()
    {
        _offers.Clear();
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
        Title = Loc.Format("fmt_card_arming_title", slot),
        Choices = defs.Select(d => WeaponCard(slot, d)).ToList(),
    };

    /// <summary>A weapon as a card: what it is, its round and the numbers that tell guns apart.</summary>
    public static CardChoice WeaponCard(int slot, WeaponDefinition def)
    {
        var caliber = CaliberDatabase.Get(def.Caliber)?.DisplayName ?? "—";
        var tag = Loc.Format("fmt_card_weapon_tag", slot, Loc.Text("name_weapontype_" + def.TypeId), caliber);
        var desc = Loc.Format("fmt_card_weapon_desc", def.Damage, def.MagSize, Mathf.RoundToInt(60f / Mathf.Max(def.FireInterval, .01f)), def.MaxRange);
        var target = slot switch { 1 => CardTarget.Slot1, 2 => CardTarget.Slot2, _ => CardTarget.Slot3 };
        return new CardChoice(def.Id, def.DisplayName, tag, desc, target, def.LoadIcon(), who =>
        {
            if (slot == 3) who.Loadout.Equip(3, new WeaponState(def));
            else who.Loadout.SetSlot(slot, def);
            who.Weapons.RefreshVisual();
        });
    }
}
