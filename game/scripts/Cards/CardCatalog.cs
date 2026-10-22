using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Every card Onslaught can offer, by key (decisions.md M-27): "w|&lt;weapon&gt;|&lt;slot&gt;" arming weapons,
/// "c|&lt;LabCard&gt;[|sniper|shotgun]" level-up effect cards, "u|&lt;CardUpgrade&gt;|&lt;level&gt;" their upgrades (plan
/// 13.2 card tree), "s|&lt;LabSpecial&gt;" boss specials, "r|&lt;id&gt;" repeatable cards that stack (more damage, a
/// heal, a medkit) and "q|&lt;key&gt;" a "?" card hiding the card of that key. The host draws offers and applies picks;
/// a client rebuilds the same cards from the keys it is sent, only to show them. The effect cards act through
/// <see cref="LabCards"/> on the hits of the survivor who took them. Every card has a grade (M-29 plan 16.2): repeatable
/// 1, an upgrade level 2 (its last level 4), an effect card 3, normal "?" 4; specials 5–6, special "?" 7. A window draws
/// one grade by the mode's weights and fills from it, then from the nearest grades when it runs short.
/// </summary>
public static class CardCatalog
{
    public static readonly string[] Repeatables = { "damage", "heal", "medkit" };
    public const int NormalLow = 1, NormalHigh = 4, SpecialLow = 5, SpecialHigh = 7;
    /// <summary>What a "?" card can turn into: grades 3–4 (normal), 6–7 (special).</summary>
    public const int MysteryNormalLow = 3, MysterySpecialLow = 6;

    /// <summary>Border colour of a grade (1 grey → 4 purple, 5 gold → 7 red); 0 has none.</summary>
    public static Color GradeColor(int grade) => grade switch
    {
        1 => new Color("c9d1dc"),
        2 => new Color("7dff9a"),
        3 => new Color("6bb8ff"),
        4 => new Color("c08bff"),
        5 => new Color("ffcf5a"),
        6 => new Color("ff9b4a"),
        7 => new Color("ff5a7a"),
        _ => new Color("8c96a8"),
    };
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
            CardTarget.Common, null, who => effects?.Set(card, true, who), "c|" + card + (kind == "" ? "" : "|" + kind), 3);
    }

    /// <summary>The next level of a taken card's upgrade (<paramref name="level"/> is the level it brings).</summary>
    public static CardChoice Upgrade(CardUpgrade upgrade, int level, LabCards? effects) =>
        new(upgrade.ToString(), Loc.Format("fmt_card_upgrade_name", Loc.Text(LabCards.Key("name_card_", upgrade)), level),
            Loc.Format("fmt_card_upgrade_tag", Loc.Text(LabCards.Key("name_card_", LabCards.BaseOf(upgrade)))), Loc.Text(LabCards.Key("desc_card_", upgrade)),
            CardTarget.Common, null, who => effects?.Upgrade(upgrade, who), $"u|{upgrade}|{level}", level >= LabCards.MaxLevel(upgrade) ? 4 : 2);

    /// <summary>The weapon kind a survivor's slot 1 or 2 gun gives billiards ("sniper", "shotgun"), or "" when neither.</summary>
    public static string BilliardsKind(Player? who)
    {
        if (who == null) return "";
        var types = new[] { who.Loadout.Primary?.Def.TypeId, who.Loadout.Secondary.Def.TypeId };
        return types.Any(LabCards.IsSniperType) ? "sniper" : types.Any(LabCards.IsShotgunType) ? "shotgun" : "";
    }

    public static CardChoice Special(LabSpecial card, LabCards? effects) =>
        new(card.ToString(), Loc.Text(LabCards.Key("name_card_", card)), Loc.Text("tag_card_special"), Loc.Text(LabCards.Key("desc_card_", card)),
            CardTarget.Common, null, who => effects?.Set(card, true, who), "s|" + card, card == LabSpecial.GlassCannon ? 6 : 5);

    /// <summary>
    /// A "?" card: what it turns into (<paramref name="inner"/>) is decided when it is drawn, shown only when picked
    /// (plan 16.3). Normal "?" is grade 4 and hides a 3–4, special "?" is 7 and hides a 6–7.
    /// </summary>
    public static CardChoice Mystery(CardChoice inner)
    {
        bool special = inner.Grade >= SpecialLow;
        return new CardChoice("?", "?", Loc.Text(special ? "tag_card_mystery_special" : "tag_card_mystery"),
            Loc.Text(special ? "desc_card_mystery_special" : "desc_card_mystery"), inner.Target, null, who => inner.Apply(who),
            "q|" + inner.Key, special ? SpecialHigh : NormalHigh, inner);
    }

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
        }, "r|" + id, 1);

    /// <summary>The card a key names, or null when the key is unknown (a newer host, a typo).</summary>
    public static CardChoice? FromKey(string key, LabCards? effects)
    {
        if (key.StartsWith("q|")) return FromKey(key[2..], effects) is { } inner ? Mystery(inner) : null;
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
            case "f" when parts.Length == 2 && CompanionSpec.Parse(parts[1]) != null:
                return CompanionSquad.Card(parts[1], null);
        }
        return null;
    }

    /// <summary>
    /// The normal cards <paramref name="who"/> can get now (plan 13.2 card tree): the effect cards they have not taken
    /// (billiards only with a sniper or shotgun in slot 1 or 2), the next level of the ones they have, and the
    /// repeatable cards.
    /// </summary>
    public static List<CardChoice> NormalPool(LabCards effects, Player who)
    {
        string billiards = BilliardsKind(who);
        var fresh = LabCards.LevelUpCards
            .Where(c => !effects.Owns(who, c) && (c != LabCard.Billiards || billiards != ""))
            .Select(c => Lab(c, effects, c == LabCard.Billiards ? billiards : ""));
        var upgrades = LabCards.LevelUpCards.Where(c => effects.Owns(who, c)).SelectMany(LabCards.UpgradesOf)
            .Where(u => effects.LevelOf(who, u) < LabCards.MaxLevel(u))
            .Select(u => Upgrade(u, effects.LevelOf(who, u) + 1, effects));
        return fresh.Concat(upgrades).Concat(Repeatables.Select(Repeat)).ToList();
    }

    /// <summary>The special cards nobody has yet; repeatables (shown as grade 5) only fill a window that runs short.</summary>
    public static List<CardChoice> SpecialPool(LabCards effects) =>
        LabCards.Specials.Where(s => !effects.IsOn(s)).Select(s => Special(s, effects)).ToList();

    /// <summary>
    /// <paramref name="who"/>'s level-up: a window of one grade drawn by <paramref name="weights"/> (grades 1–4, the
    /// stretch's row; even when null), three different cards. Rerolls draw from the same pool at the window's grade.
    /// </summary>
    public static CardOffer LevelUp(LabCards effects, RandomNumberGenerator rng, Player who, IReadOnlyList<float>? weights = null, int count = 3)
    {
        int grade = DrawGrade(weights ?? new float[] { 1, 1, 1, 1 }, NormalLow, rng);
        var choices = Window(NormalPool(effects, who), grade, NormalLow, NormalHigh, MysteryNormalLow, count, rng, new HashSet<string>());
        return new CardOffer
        {
            Kind = CardKind.Normal, TitleKey = "ui_card_levelup_title", Choices = choices, Grade = grade,
            Redraw = avoid => Window(NormalPool(effects, who), grade, NormalLow, NormalHigh, MysteryNormalLow, 1, rng, avoid).FirstOrDefault(),
        };
    }

    /// <summary>A boss reward for the team vote: a window of special grades 5–7 drawn by <paramref name="weights"/>; no rerolls.</summary>
    public static CardOffer BossReward(LabCards effects, RandomNumberGenerator rng, IReadOnlyList<float>? weights = null, int count = 3)
    {
        int grade = DrawGrade(weights ?? new float[] { 1, 1, 1 }, SpecialLow, rng);
        var choices = Window(SpecialPool(effects), grade, SpecialLow, SpecialHigh, MysterySpecialLow, count, rng, new HashSet<string>());
        // When every special is on, strong repeatables fill the window.
        choices.AddRange(Repeatables.Select(Repeat).Where(r => choices.All(c => c.Key != r.Key)).Take(count - choices.Count));
        return new CardOffer { Kind = CardKind.Special, TitleKey = "ui_card_special_title", Choices = choices, Grade = grade };
    }

    /// <summary>Names a "?" card flicks through before it stops (every card of its band, plan 16.3).</summary>
    public static List<string> RevealNames(bool special)
    {
        var keys = special
            ? LabCards.Specials.Select(s => "s|" + s)
            : LabCards.LevelUpCards.Select(c => "c|" + c + (c == LabCard.Billiards ? "|sniper" : ""))
                .Concat(LabCards.LevelUpCards.SelectMany(LabCards.UpgradesOf).Select(u => $"u|{u}|{LabCards.MaxLevel(u)}"));
        return keys.Select(k => FromKey(k, null)?.Name).OfType<string>().Distinct().ToList();
    }

    /// <summary>A grade from <paramref name="lowest"/> up, by weight.</summary>
    public static int DrawGrade(IReadOnlyList<float> weights, int lowest, RandomNumberGenerator rng)
    {
        float roll = rng.Randf() * weights.Sum();
        for (int i = 0; i < weights.Count; i++)
        {
            roll -= weights[i];
            if (roll < 0) return lowest + i;
        }
        return lowest + weights.Count - 1;
    }

    /// <summary>
    /// Up to <paramref name="count"/> different cards from <paramref name="pool"/>, none of <paramref name="avoid"/>: at
    /// <paramref name="grade"/> first, then the nearest grades (lower first). The top grade also has a "?" card when a
    /// card of grades <paramref name="mysteryLow"/>–top is left to hide in it.
    /// </summary>
    public static List<CardChoice> Window(List<CardChoice> pool, int grade, int low, int high, int mysteryLow, int count, RandomNumberGenerator rng,
        IReadOnlyCollection<string> avoid)
    {
        var picked = new List<CardChoice>();
        var taken = new HashSet<string>(avoid);
        var order = Enumerable.Range(0, high - low + 1).SelectMany(d => new[] { grade - d, grade + d }).Distinct().Where(g => g >= low && g <= high);
        foreach (int g in order)
        {
            var here = pool.Where(c => c.Grade == g && !taken.Contains(c.Key)).OrderBy(_ => rng.Randf()).Cast<CardChoice?>().ToList();
            // The top grade can also show one "?" (null here), in a random place among its cards.
            if (g == high && !taken.Contains("?")) here.Insert(rng.RandiRange(0, here.Count), null);
            foreach (var card in here)
            {
                if (picked.Count >= count) break;
                if (card == null)
                {
                    // It hides a card no other card in the window is.
                    var inner = pool.Where(c => c.Grade >= mysteryLow && c.Grade <= high && !taken.Contains(c.Key)).OrderBy(_ => rng.Randf()).FirstOrDefault();
                    if (inner == null) continue;
                    picked.Add(Mystery(inner));
                    taken.Add(inner.Key);
                    taken.Add("?");
                    continue;
                }
                // A card the "?" took is not shown as well.
                if (taken.Contains(card.Key)) continue;
                picked.Add(card);
                taken.Add(card.Key);
            }
            if (picked.Count >= count) break;
        }
        return picked.OrderBy(_ => rng.Randf()).ToList();
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
            w.U8(offer.Grade);
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
            int grade = r.U8();
            int choices = r.U8();
            var list = new List<CardChoice>();
            for (int c = 0; c < choices; c++)
                if (FromKey(r.Str(), null) is { } choice) list.Add(choice);
            offers.Add(new CardOffer { Serial = serial, Kind = kind, TitleKey = title, TitleArg = arg, Choices = list, Grade = grade });
        }
        return offers;
    }
}
