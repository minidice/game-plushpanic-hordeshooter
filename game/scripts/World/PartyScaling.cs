using Godot;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// One row of content/data/party_scaling.csv (menu-multiplayer-plan.md 4장, decisions.md M-5): how a party of this many
/// people changes the monsters. Commons change in number, health and damage; named and bosses only in health and damage.
/// <see cref="Players"/> can be a fraction: an AI squadmate counts as half a person (M-34).
/// </summary>
public sealed record PartyScale(float Players, float CommonCount, float CommonHp, float CommonDamage, float NamedHp, float NamedDamage)
{
    public static readonly PartyScale Baseline = new(4, 1, 1, 1, 1, 1);
    public float Hp(MonsterTier tier) => tier == MonsterTier.Common ? CommonHp : NamedHp;
    public float Damage(MonsterTier tier) => tier == MonsterTier.Common ? CommonDamage : NamedDamage;
}

/// <summary>Loads the party table once. Errors are collected with the row's line, never thrown; a missing row is 100%.</summary>
public static class PartyScaling
{
    public const string TablePath = "res://content/data/party_scaling.csv";
    private static readonly string[] Columns = { "players", "common_count_pct", "common_hp_pct", "common_damage_pct", "named_hp_pct", "named_damage_pct" };
    private static List<PartyScale>? _rows;
    public static List<string> Errors { get; } = new();

    public static IReadOnlyList<PartyScale> Rows
    {
        get
        {
            if (_rows != null) return _rows;
            _rows = FromText(FileAccess.GetFileAsString(TablePath), Errors);
            foreach (var error in Errors) GD.PushError("[party] " + error);
            return _rows;
        }
    }

    /// <summary>
    /// The row for <paramref name="people"/> (clamped to the table: 1 below, the largest above). Between two rows (1.5 = one
    /// person and an AI squadmate, M-34) every percentage is interpolated.
    /// </summary>
    public static PartyScale For(float people)
    {
        var rows = Rows;
        if (rows.Count == 0) return PartyScale.Baseline;
        people = Mathf.Clamp(people, rows[0].Players, rows[^1].Players);
        var low = rows.LastOrDefault(r => r.Players <= people) ?? rows[0];
        var high = rows.FirstOrDefault(r => r.Players >= people) ?? rows[^1];
        if (high.Players <= low.Players) return low;
        float t = (people - low.Players) / (high.Players - low.Players);
        return new PartyScale(people, Mathf.Lerp(low.CommonCount, high.CommonCount, t), Mathf.Lerp(low.CommonHp, high.CommonHp, t),
            Mathf.Lerp(low.CommonDamage, high.CommonDamage, t), Mathf.Lerp(low.NamedHp, high.NamedHp, t), Mathf.Lerp(low.NamedDamage, high.NamedDamage, t));
    }

    public static List<PartyScale> FromText(string text, List<string> errors)
    {
        var (header, rows) = Csv.Parse(text);
        var result = new List<PartyScale>();
        foreach (var column in Columns.Where(c => !header.Contains(c))) errors.Add($"party_scaling.csv: missing column '{column}'");
        if (errors.Count > 0) return result;
        foreach (var row in rows)
        {
            var v = new float[Columns.Length];
            bool ok = true;
            for (int i = 0; i < Columns.Length; i++)
            {
                if (float.TryParse(row.Cells[Columns[i]], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]) && v[i] >= 0) continue;
                errors.Add($"party_scaling.csv line {row.Line}: '{Columns[i]}' is not a number ≥ 0");
                ok = false;
            }
            if (ok) result.Add(new PartyScale((int)v[0], v[1] / 100, v[2] / 100, v[3] / 100, v[4] / 100, v[5] / 100));
        }
        result.Sort((a, b) => a.Players.CompareTo(b.Players));
        return result;
    }
}
