using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Every card Onslaught can offer, by key (decisions.md M-27): "w|&lt;weapon&gt;|&lt;slot&gt;" arming weapons,
/// "c|&lt;LabCard&gt;[|sniper|shotgun]" level-up effect cards, "u|&lt;CardUpgrade&gt;|&lt;level&gt;" their upgrades (plan
/// 13.2 card tree), "s|&lt;LabSpecial&gt;" boss specials and "r|&lt;id&gt;" repeatable cards that stack (more damage, a
/// heal, a medkit). The host draws offers and applies picks; a client rebuilds the same cards from the keys it is sent,
/// only to show them. The effect cards act through <see cref="LabCards"/> on the hits of the survivor who took them.
/// </summary>
public static class CardCatalog
{
    public static readonly string[] Repeatables = { "damage", "heal", "medkit" };
    public const float DamageStep = .1f, HealShare = .4f;
    public const int MaxMedkits = 3;

    /// <summary>
    /// A level-up effect card; picked, it is the picker's own (<see cref="LabCards.Set(LabCard, bool, Player)"/>).
    /// Billiards is a sniper or shotgun card with its own name per kind (<paramref name="kind"/>, plan 13.2).
    /// </summary>
    public static CardChoice Lab(LabCard card, LabCards? effects, string kind = "")
    {
        string id = LabCards.Key("", card) + (kind == "" ? "" : "_" + kind);
        string tag = kind == "" ? Loc.Text("tag_card_common") : Loc.Text("tag_card_" + kind);
        return new(card.ToString(), Loc.Text("name_card_" + id), tag, Loc.Text("desc_card_" + id),
            CardTarget.Common, null, who => effects?.Set(card, true, who), "c|" + card + (kind == "" ? "" : "|" + kind));
    }

    /// <summary>The next level of a taken card's upgrade (<paramref name="level"/> is the level it brings).</summary>
    public static CardChoice Upgrade(CardUpgrade upgrade, int level, LabCards? effects) =>
        new(upgrade.ToString(), Loc.Format("fmt_card_upgrade_name", Loc.Text(LabCards.Key("name_card_", upgrade)), level),
            Loc.Format("fmt_card_upgrade_tag", Loc.Text(LabCards.Key("name_card_", LabCards.BaseOf(upgrade)))), Loc.Text(LabCards.Key("desc_card_", upgrade)),
            CardTarget.Common, null, who => effects?.Upgrade(upgrade, who), $"u|{upgrade}|{level}");

    /// <summary>The weapon kind a survivor's slot 1 or 2 gun gives billiards ("sniper", "shotgun"), or "" when neither.</summary>
    public static string BilliardsKind(Player? who)
    {
        if (who == null) return "";
        var types = new[] { who.Loadout.Primary?.Def.TypeId, who.Loadout.Secondary.Def.TypeId };
        return types.Any(t => LabCards.SniperTypes.Contains(t)) ? "sniper" : types.Any(t => LabCards.ShotgunTypes.Contains(t)) ? "shotgun" : "";
    }

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
            case "c" when parts.Length is 2 or 3 && Enum.TryParse<LabCard>(parts[1], out var card):
                return Lab(card, effects, parts.Length == 3 ? parts[2] : "");
            case "u" when parts.Length == 3 && Enum.TryParse<CardUpgrade>(parts[1], out var upgrade) && int.TryParse(parts[2], out int level):
                return Upgrade(upgrade, level, effects);
            case "s" when parts.Length == 2 && Enum.TryParse<LabSpecial>(parts[1], out var special):
                return Special(special, effects);
            case "r" when parts.Length == 2 && Repeatables.Contains(parts[1]):
                return Repeat(parts[1]);
        }
        return null;
    }

    /// <summary>
    /// <paramref name="who"/>'s level-up (plan 13.2 card tree): up to two of the effect cards they have not taken (billiards
    /// only with a sniper or shotgun in slot 1 or 2) and the next level of the ones they have, filled up with repeatable
    /// cards, three different ones.
    /// </summary>
    public static CardOffer LevelUp(LabCards effects, RandomNumberGenerator rng, Player who, int count = 3)
    {
        string billiards = BilliardsKind(who);
        var fresh = LabCards.LevelUpCards
            .Where(c => !effects.Owns(who, c) && (c != LabCard.Billiards || billiards != ""))
            .Select(c => Lab(c, effects, c == LabCard.Billiards ? billiards : ""));
        var upgrades = LabCards.LevelUpCards.Where(c => effects.Owns(who, c)).SelectMany(LabCards.UpgradesOf)
            .Where(u => effects.LevelOf(who, u) < LabCards.MaxLevel(u))
            .Select(u => Upgrade(u, effects.LevelOf(who, u) + 1, effects));
        var cards = fresh.Concat(upgrades).OrderBy(_ => rng.Randf());
        var repeat = Repeatables.OrderBy(_ => rng.Randf()).Select(Repeat);
        var choices = cards.Take(Math.Min(2, count)).Concat(repeat).Take(count).OrderBy(_ => rng.Randf()).ToList();
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
