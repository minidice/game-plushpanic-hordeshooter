using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

/// <summary>What a supply point is for (scenario-level-design.md 5.3).</summary>
public enum SupplyKind
{
    /// <summary>Saferoom kit at the start; outside the stage's budget.</summary>
    Start,
    /// <summary>Supplies the route needs, found on the main path.</summary>
    Main,
    /// <summary>A reward for looking around; the stage must be finishable without it.</summary>
    Optional,
    /// <summary>A reward tied to the stage's big event (heavy weapons).</summary>
    Event,
    /// <summary>The safehouse at the end; counted in the next stage's budget.</summary>
    Arrival,
}

public enum SupplyItemKind { Weapon, Throwable, Ammo, Medkit }

/// <summary>
/// <see cref="Infinite"/>: a shared weapon on a table (decisions.md C-33). <see cref="PerPerson"/>: laid out once for each
/// person in the party (no AI squadmates, M-35), side by side from <see cref="At"/> along +X.
/// </summary>
public sealed record SupplyItem(SupplyItemKind Kind, string Id, Vector3 At, bool Infinite = false, bool PerPerson = false);

public sealed class SupplyPoint
{
    public string Id { get; init; } = "";
    public SupplyKind Kind { get; init; }
    public string Area { get; init; } = "";
    public List<SupplyItem> Items { get; } = new();
    /// <summary>Median X and Z of the items: one stray item does not drag the center away from the group.</summary>
    public Vector3 Center => Items.Count == 0 ? Vector3.Zero : new Vector3(Median(Items.Select(i => i.At.X)), 0, Median(Items.Select(i => i.At.Z)));

    private static float Median(IEnumerable<float> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}

/// <summary>
/// Stage supply points from content/data/stages/&lt;stage&gt;_supplies.json. Items are grouped where supplies make sense
/// instead of scattered along the route (decisions.md L-12). Errors are collected, never thrown.
/// </summary>
public sealed class StageSupplies
{
    public const string Stage1Path = "res://content/data/stages/stage1_supplies.json";
    public const string Stage2Path = "res://content/data/stages/stage2_supplies.json";
    /// <summary>Every item of a point lies within this distance of the point's center.</summary>
    public const float ClusterRadius = 3f;
    /// <summary>Items closer than this would overlap on the floor.</summary>
    public const float MinSpacing = 1f;

    private readonly List<string> _errors = new();
    public List<SupplyPoint> Points { get; } = new();
    public IReadOnlyList<string> Errors => _errors;

    private static StageSupplies? _stage1;
    public static StageSupplies Stage1
    {
        get
        {
            if (_stage1 != null) return _stage1;
            _stage1 = FromText(FileAccess.FileExists(Stage1Path) ? FileAccess.GetFileAsString(Stage1Path) : "", "stage1_supplies.json");
            foreach (var error in _stage1.Errors) GD.PushError("[supplies] " + error);
            return _stage1;
        }
    }
    private static StageSupplies? _stage2;
    /// <summary>Scenario Stage 2 (decisions.md L-14).</summary>
    public static StageSupplies Stage2
    {
        get
        {
            if (_stage2 != null) return _stage2;
            _stage2 = FromText(FileAccess.FileExists(Stage2Path) ? FileAccess.GetFileAsString(Stage2Path) : "", "stage2_supplies.json");
            foreach (var error in _stage2.Errors) GD.PushError("[supplies] " + error);
            return _stage2;
        }
    }
    private static readonly Dictionary<string, StageSupplies> _byPath = new();
    /// <summary>Any stage's supplies by file (the size draft's, decisions.md L-29), read once.</summary>
    public static StageSupplies Load(string path)
    {
        if (_byPath.TryGetValue(path, out var db)) return db;
        db = FromText(FileAccess.FileExists(path) ? FileAccess.GetFileAsString(path) : "", path.GetFile());
        foreach (var error in db.Errors) GD.PushError("[supplies] " + error);
        return _byPath[path] = db;
    }
    public static void Reload() { _stage1 = null; _stage2 = null; _byPath.Clear(); }

    public IEnumerable<SupplyItem> ItemsOf(params SupplyKind[] kinds) => Points.Where(p => kinds.Contains(p.Kind)).SelectMany(p => p.Items);

    public static StageSupplies FromText(string json, string file)
    {
        var db = new StageSupplies();
        if (json.Length == 0) { db._errors.Add($"{file}: missing"); return db; }
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("points", out var points) || points.ValueKind != JsonValueKind.Array)
            {
                db._errors.Add($"{file}: points must be a list");
                return db;
            }
            int i = 0;
            foreach (var p in points.EnumerateArray())
            {
                string where = $"{file} points[{i++}]";
                var j = new JsonFields(p, where, db._errors);
                string id = j.String("id", "");
                if (id == "" || db.Points.Any(x => x.Id == id)) { db._errors.Add($"{where}: id is empty or duplicated"); continue; }
                if (!System.Enum.TryParse<SupplyKind>(j.String("kind", ""), true, out var kind))
                {
                    db._errors.Add($"{where}: kind must be start, main, optional, event or arrival");
                    continue;
                }
                var point = new SupplyPoint { Id = id, Kind = kind, Area = j.String("area", "") };
                if (p.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    int n = 0;
                    foreach (var it in items.EnumerateArray())
                        if (db.ReadItem(it, $"{where} ({id}) items[{n++}]") is { } item) point.Items.Add(item);
                }
                if (point.Items.Count == 0) { db._errors.Add($"{where} ({id}): no items"); continue; }
                db.Points.Add(point);
            }
            db.CheckLayout();
        }
        catch (System.Exception e) { db._errors.Add($"{file}: {e.Message}"); }
        return db;
    }

    private SupplyItem? ReadItem(JsonElement it, string where)
    {
        var j = new JsonFields(it, where, _errors);
        if (!System.Enum.TryParse<SupplyItemKind>(j.String("item", ""), true, out var kind))
        {
            _errors.Add($"{where}: item must be weapon, throwable, ammo or medkit");
            return null;
        }
        string id = j.String("id", "");
        if (kind == SupplyItemKind.Weapon && ArmoryCatalog.FindWeapon(id) == null) { _errors.Add($"{where}: weapon '{id}' is not in weapons.csv"); return null; }
        if (kind == SupplyItemKind.Throwable && !ArmoryCatalog.Throwables.ContainsKey(id)) { _errors.Add($"{where}: throwable '{id}' is not in throwables.csv"); return null; }
        if (!it.TryGetProperty("at", out var at) || at.ValueKind != JsonValueKind.Array || at.GetArrayLength() != 2 ||
            at.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.Number))
        {
            _errors.Add($"{where}: at must be [x, z]");
            return null;
        }
        bool infinite = it.TryGetProperty("infinite", out var inf) && inf.ValueKind == JsonValueKind.True;
        bool perPerson = it.TryGetProperty("per_person", out var pp) && pp.ValueKind == JsonValueKind.True;
        if (infinite && kind != SupplyItemKind.Weapon) { _errors.Add($"{where}: only weapons can be infinite (ammo piles always are)"); return null; }
        if (infinite && perPerson) { _errors.Add($"{where}: an infinite weapon is already for everyone"); return null; }
        return new SupplyItem(kind, id, new Vector3(at[0].GetSingle(), Mathf.Max(0, j.Float("height", 0)), at[1].GetSingle()), infinite, perPerson);
    }

    /// <summary>Points stay grouped, and no two items share a spot (scenario-level-design.md 5.4).</summary>
    private void CheckLayout()
    {
        // The start kit fills the whole saferoom: a wide prep room is the stated exception (scenario-level-design.md 5.4).
        foreach (var p in Points.Where(p => p.Kind != SupplyKind.Start))
        {
            var center = p.Center;
            foreach (var item in p.Items)
                if (Flat(item.At - center).Length() > ClusterRadius)
                    _errors.Add($"{p.Id}: {item.Kind} {item.Id} is more than {ClusterRadius} m from the rest of the point");
        }
        var all = Points.SelectMany(p => p.Items).ToList();
        for (int a = 0; a < all.Count; a++)
            for (int b = a + 1; b < all.Count; b++)
                if (Flat(all[a].At - all[b].At).Length() < MinSpacing)
                    _errors.Add($"items {all[a].Kind} {all[a].Id} and {all[b].Kind} {all[b].Id} are closer than {MinSpacing} m");
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
