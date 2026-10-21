using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Every card Onslaught can offer, by key (decisions.md M-27): "w|&lt;weapon&gt;|&lt;slot&gt;" arming weapons,
/// "c|&lt;LabCard&gt;" level-up effect cards, "s|&lt;LabSpecial&gt;" boss specials and "r|&lt;id&gt;" repeatable cards
/// that stack (more damage, a heal, a medkit). The host draws offers and applies picks; a client rebuilds the same cards
/// from the keys it is sent, only to show them. The effect cards act through <see cref="LabCards"/> and count for the
/// whole team for now (I-85).
/// </summary>
public static class CardCatalog
{
    public static readonly string[] Repeatables = { "damage", "heal", "medkit" };
    public const float DamageStep = .1f, HealShare = .4f;
    public const int MaxMedkits = 3;

    public static CardChoice Lab(LabCard card, LabCards? effects) =>
        new(card.ToString(), Loc.Text(LabCards.Key("name_card_", card)), Loc.Text("tag_card_common"), Loc.Text(LabCards.Key("desc_card_", card)),
            CardTarget.Common, null, _ => effects?.Set(card, true), "c|" + card);

    public static CardChoice Special(LabSpecial card, LabCards? effects) =>
        new(card.ToString(), Loc.Text(LabCards.Key("name_card_", card)), Loc.Text("tag_card_special"), Loc.Text(LabCards.Key("desc_card_", card)),
            card == LabSpecial.GlassCannon ? CardTarget.Self : CardTarget.Common, null, who => effects?.Set(card, true, who), "s|" + card);

    /// <summary>A card that can be taken again and again: it only changes the survivor who picks it.</summary>
    public static CardChoice Repeat(string id) => new(id, Loc.Text("name_card_r_" + id), Loc.Text("tag_card_self"), Loc.Text("desc_card_r_" + id),
        CardTarget.Self, null, who =>
        {
            switch (id)
            {
                case "damage": who.DamageMultiplier += DamageStep; break;
                case "heal": who.Heal(Mathf.CeilToInt(who.MaxHealth * HealShare)); break;
                case "medkit": who.Medkits = Math.Min(MaxMedkits, who.Medkits + 1); break;
            }
        }, "r|" + id);

    /// <summary>The card a key names, or null when the key is unknown (a newer host, a typo).</summary>
    public static CardChoice? FromKey(string key, LabCards? effects)
    {
        var parts = key.Split('|');
        switch (parts[0])
        {
            case "w" when parts.Length == 3 && int.TryParse(parts[2], out int slot) && ArmoryCatalog.Weapons.ContainsKey(parts[1]):
                return ArmingCards.WeaponCard(slot, ArmoryCatalog.Weapon(parts[1]));
            case "c" when parts.Length == 2 && Enum.TryParse<LabCard>(parts[1], out var card):
                return Lab(card, effects);
            case "s" when parts.Length == 2 && Enum.TryParse<LabSpecial>(parts[1], out var special):
                return Special(special, effects);
            case "r" when parts.Length == 2 && Repeatables.Contains(parts[1]):
                return Repeat(parts[1]);
        }
        return null;
    }

    /// <summary>A level-up: effect cards nobody has yet first, filled up with repeatable cards, three different ones.</summary>
    public static CardOffer LevelUp(LabCards effects, RandomNumberGenerator rng, int count = 3)
    {
        var fresh = LabCards.All.Where(c => !effects.IsOn(c)).OrderBy(_ => rng.Randf()).Select(c => Lab(c, effects));
        var repeat = Repeatables.OrderBy(_ => rng.Randf()).Select(Repeat);
        var choices = fresh.Take(Math.Min(2, count)).Concat(repeat).Take(count).OrderBy(_ => rng.Randf()).ToList();
        return new CardOffer { Kind = CardKind.Normal, TitleKey = "ui_card_levelup_title", Choices = choices };
    }

    /// <summary>A boss reward: the special cards nobody has yet; when all are taken, strong repeatables.</summary>
    public static CardOffer BossReward(LabCards effects, RandomNumberGenerator rng, int count = 3)
    {
        var fresh = LabCards.Specials.Where(s => !effects.IsOn(s)).OrderBy(_ => rng.Randf()).Select(s => Special(s, effects));
        var choices = fresh.Concat(Repeatables.Select(Repeat)).Take(count).ToList();
        return new CardOffer { Kind = CardKind.Special, TitleKey = "ui_card_special_title", Choices = choices };
    }

    // ---------- network ----------

    public static void Write(NetWriter w, IReadOnlyList<CardOffer> offers)
    {
        w.U8(offers.Count);
        foreach (var offer in offers)
        {
            w.U16(offer.Serial);
            w.U8((int)offer.Kind);
            w.Str(offer.TitleKey);
            w.S16(offer.TitleArg);
            w.U8(offer.Choices.Count);
            foreach (var choice in offer.Choices) w.Str(choice.Key);
        }
    }

    /// <summary>Offers as a client sees them: the same cards, but picking them only asks the host.</summary>
    public static List<CardOffer> Read(NetReader r)
    {
        var offers = new List<CardOffer>();
        int count = r.U8();
        for (int i = 0; i < count; i++)
        {
            int serial = r.U16();
            var kind = (CardKind)r.U8();
            string title = r.Str();
            int arg = r.S16();
            int choices = r.U8();
            var list = new List<CardChoice>();
            for (int c = 0; c < choices; c++)
                if (FromKey(r.Str(), null) is { } choice) list.Add(choice);
            offers.Add(new CardOffer { Serial = serial, Kind = kind, TitleKey = title, TitleArg = arg, Choices = list });
        }
        return offers;
    }
}
