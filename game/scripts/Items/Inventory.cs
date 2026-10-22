using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

public enum ItemKind { Weapon, Ammo, Throwable, Medkit }

/// <summary>
/// One backpack cell's contents (Outpost, decisions.md M-25): a weapon (with its own magazine), a stack of rounds
/// of one caliber, a stack of throwables, or medkits.
/// </summary>
public sealed class InvItem
{
    public const string MedkitId = "medkit";
    public const int MedkitStack = 3;

    public ItemKind Kind { get; init; }
    /// <summary>Weapon id, caliber id, throwable id, or <see cref="MedkitId"/>.</summary>
    public string Id { get; init; } = "";
    public int Count { get; set; } = 1;
    /// <summary>A carried weapon keeps its magazine (its reserve lives in the backpack as ammo).</summary>
    public WeaponState? State { get; set; }

    public static InvItem Ammo(string caliber, int rounds) => new() { Kind = ItemKind.Ammo, Id = caliber, Count = rounds };
    public static InvItem Medkit(int count = 1) => new() { Kind = ItemKind.Medkit, Id = MedkitId, Count = count };
    public static InvItem Throwable(string id, int count = 1) => new() { Kind = ItemKind.Throwable, Id = id, Count = count };
    public static InvItem Weapon(WeaponState state) => new() { Kind = ItemKind.Weapon, Id = state.Def.Id, Count = 1, State = state };

    /// <summary>How many fit in one cell.</summary>
    public int StackLimit => Kind switch
    {
        ItemKind.Ammo => CaliberDatabase.Get(Id)?.Stack ?? 30,
        ItemKind.Throwable => ArmoryCatalog.Throwables.TryGetValue(Id, out var t) ? Math.Max(1, t.MaxCarry) : 1,
        ItemKind.Medkit => MedkitStack,
        _ => 1,
    };

    public bool Stacks(InvItem other) => Kind != ItemKind.Weapon && Kind == other.Kind && Id == other.Id;

    public string DisplayName => Kind switch
    {
        ItemKind.Weapon => State?.Def.DisplayName ?? Loc.Text("name_weapon_" + Id),
        ItemKind.Ammo => CaliberDatabase.Get(Id)?.DisplayName ?? Id,
        ItemKind.Throwable => ArmoryCatalog.Throwables.TryGetValue(Id, out var t) ? t.DisplayName : Id,
        _ => Loc.Text("hud_medkit"),
    };

    public InvItem Copy() => new() { Kind = Kind, Id = Id, Count = Count, State = State };
}

/// <summary>
/// Outpost's backpack: 4 × 5 = 20 cells, one stack each (decisions.md M-25). Pure data, so the host can keep every
/// person's and send it in the snapshot; the UI and the network both change it through <see cref="InventoryOps"/>.
/// </summary>
public sealed class Backpack
{
    public const int Columns = 4, Rows = 5, Size = Columns * Rows;
    private readonly InvItem?[] _cells = new InvItem?[Size];

    public InvItem? this[int cell] => cell is >= 0 and < Size ? _cells[cell] : null;
    public IEnumerable<InvItem> Items => _cells.Where(c => c != null)!;
    public int FreeCells => _cells.Count(c => c == null);

    public void Set(int cell, InvItem? item) => _cells[cell] = item is { Count: > 0 } ? item : null;

    /// <summary>Rounds of <paramref name="caliber"/> carried.</summary>
    public int Count(string caliber) => Items.Where(i => i.Kind == ItemKind.Ammo && i.Id == caliber).Sum(i => i.Count);

    public int CountOf(ItemKind kind, string id) => Items.Where(i => i.Kind == kind && i.Id == id).Sum(i => i.Count);

    /// <summary>Adds into matching stacks first, then empty cells. Returns how many did not fit (0 = all in).</summary>
    public int Add(InvItem item)
    {
        int left = item.Count;
        if (item.Kind != ItemKind.Weapon)
            for (int i = 0; i < Size && left > 0; i++)
                if (_cells[i] is { } stack && stack.Stacks(item))
                {
                    int room = stack.StackLimit - stack.Count;
                    int put = Math.Min(room, left);
                    stack.Count += put;
                    left -= put;
                }
        for (int i = 0; i < Size && left > 0; i++)
        {
            if (_cells[i] != null) continue;
            var copy = item.Copy();
            copy.Count = Math.Min(left, item.StackLimit);
            _cells[i] = copy;
            left -= copy.Count;
        }
        return left;
    }

    /// <summary>Takes up to <paramref name="amount"/> rounds of a caliber (smallest stacks first). Returns how many.</summary>
    public int Take(ItemKind kind, string id, int amount)
    {
        int taken = 0;
        foreach (int i in Enumerable.Range(0, Size).Where(i => _cells[i]?.Kind == kind && _cells[i]!.Id == id).OrderBy(i => _cells[i]!.Count).ToList())
        {
            if (taken >= amount) break;
            var stack = _cells[i]!;
            int take = Math.Min(stack.Count, amount - taken);
            stack.Count -= take;
            taken += take;
            if (stack.Count <= 0) _cells[i] = null;
        }
        return taken;
    }

    /// <summary>Drag inside the backpack: merge into a matching stack, else swap.</summary>
    public void Move(int from, int to)
    {
        if (from == to || this[from] is not { } item) return;
        if (_cells[to] is { } target && target.Stacks(item))
        {
            int put = Math.Min(target.StackLimit - target.Count, item.Count);
            target.Count += put;
            item.Count -= put;
            if (item.Count <= 0) _cells[from] = null;
            return;
        }
        (_cells[from], _cells[to]) = (_cells[to], item);
    }

    public void Clear() => Array.Fill(_cells, null);
}
