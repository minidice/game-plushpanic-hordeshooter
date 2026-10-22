using Godot;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// One row of content/data/party_scaling.csv (menu-multiplayer-plan.md 4장, decisions.md M-5): how a party of this many
/// people changes the monsters. Commons change in number, health and damage; named and bosses only in health and damage.
/// </summary>
public sealed record PartyScale(int Players, float CommonCount, float CommonHp, float CommonDamage, float NamedHp, float NamedDamage)
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

    /// <summary>The row for <paramref name="people"/> (clamped to the table: 1 below, the largest above).</summary>
    public static PartyScale For(int people)
    {
        var rows = Rows;
        if (rows.Count == 0) return PartyScale.Baseline;
        people = Mathf.Clamp(people, rows[0].Players, rows[^1].Players);
        return rows.LastOrDefault(r => r.Players <= people) ?? rows[0];
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
