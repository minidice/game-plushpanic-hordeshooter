using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Small ids for data that both PCs load from the same files: weapons, throwables and monsters sorted by id.
/// A mismatch between builds is caught by the version check before a match starts.
/// </summary>
public static class NetIds
{
    private static WeaponDefinition[]? _weapons;
    private static Dictionary<string, int>? _weaponIndex;
    private static string[]? _throwables, _monsters;

    // Dual variants ("m1911_dual") are made from their base weapon and are not in the weapon table: include them (I-73).
    private static WeaponDefinition[] Weapons => _weapons ??= ArmoryCatalog.Weapons.Values
        .SelectMany(d => d.DualVariant is { } dual ? new[] { d, dual } : new[] { d })
        .GroupBy(d => d.Id).Select(g => g.First())
        .OrderBy(d => d.Id, System.StringComparer.Ordinal).ToArray();
    private static Dictionary<string, int> WeaponIndex => _weaponIndex ??= Weapons.Select((d, i) => (d.Id, i)).ToDictionary(t => t.Id, t => t.i);
    private static string[] Throwables => _throwables ??= ArmoryCatalog.Throwables.Keys.OrderBy(k => k, System.StringComparer.Ordinal).ToArray();
    private static string[] Monsters => _monsters ??= MonsterDatabase.Current.Monsters.Keys.OrderBy(k => k, System.StringComparer.Ordinal).ToArray();

    /// <summary>Fails loudly on the host instead of sending an index the other side cannot read.</summary>
    public static int Weapon(string id) => WeaponIndex.TryGetValue(id, out int i) ? i : throw new System.InvalidOperationException($"weapon '{id}' has no network id");
    public static WeaponDefinition WeaponDef(int i) => Weapons[i];
    public static string WeaponAt(int i) => Weapons[i].Id;
    public static int Throwable(string id) => Index(Throwables, id, "throwable");
    public static string ThrowableAt(int i) => Throwables[i];
    public static int Monster(string id) => Index(Monsters, id, "monster");

    /// <summary>An item's id as a number for its kind (weapon, caliber, throwable; medkits are 0).</summary>
    public static int Item(ItemKind kind, string id) => kind switch
    {
        ItemKind.Weapon => Weapon(id),
        ItemKind.Ammo => Index(CaliberDatabase.Current.Order.ToArray(), id, "caliber"),
        ItemKind.Throwable => Throwable(id),
        _ => 0,
    };

    public static string ItemAt(ItemKind kind, int index) => kind switch
    {
        ItemKind.Weapon => WeaponAt(index),
        ItemKind.Ammo => CaliberDatabase.Current.Order[index],
        ItemKind.Throwable => ThrowableAt(index),
        _ => InvItem.MedkitId,
    };
    public static string MonsterAt(int i) => Monsters[i];

    private static int Index(string[] list, string id, string what)
    {
        int i = System.Array.IndexOf(list, id);
        return i >= 0 ? i : throw new System.InvalidOperationException($"{what} '{id}' has no network id");
    }

    /// <summary>A short fingerprint of the data both sides must agree on; a client with another one is refused.</summary>
    public static int DataHash()
    {
        unchecked
        {
            int h = 17;
            foreach (var s in Weapons.Select(w => w.Id).Concat(Throwables).Concat(Monsters).Concat(CaliberDatabase.Current.Order))
                foreach (char c in s) h = h * 31 + c;
            return h;
        }
    }
    public static void Clear() { _weapons = null; _weaponIndex = null; _throwables = _monsters = null; }
}

/// <summary>Buttons a client is holding right now.</summary>
[System.Flags]
public enum NetHeld { None = 0, Fire = 1, Revive = 2, Aim = 4 }

/// <summary>
/// One-shot buttons. The client counts every press and always sends the counts, so a lost or late input message
/// never loses a press: the host acts once for each count it has not seen yet (I-73).
/// </summary>
public enum NetPress { Fire, Reload, Heal, Throw, Cancel, Roll, CycleUp, CycleDown, Count }

/// <summary>One client input message (client → host, every physics frame).</summary>
public struct NetInput
{
    public int Seq;
    public Vector3 Aim;
    public NetHeld Held;
    public byte[] Presses;
    /// <summary>The last slot asked for, and how many slot presses so far.</summary>
    public int Slot, SlotPresses;
    /// <summary>Where the client's own simulation put its player (own movement, decided 2026-10-20).</summary>
    public Vector3 Position;

    public void Write(NetWriter w)
    {
        w.I32(Seq);
        w.Pos(Aim);
        w.U8((int)Held);
        for (int i = 0; i < (int)NetPress.Count; i++) w.U8(Presses?[i] ?? 0);
        w.U8(Slot);
        w.U8(SlotPresses);
        w.Pos(Position);
    }

    public static NetInput Read(NetReader r)
    {
        var input = new NetInput { Seq = r.I32(), Aim = r.Pos(), Held = (NetHeld)r.U8(), Presses = new byte[(int)NetPress.Count] };
        for (int i = 0; i < (int)NetPress.Count; i++) input.Presses[i] = (byte)r.U8();
        input.Slot = r.U8();
        input.SlotPresses = r.U8();
        input.Position = r.Pos();
        return input;
    }
}

[System.Flags]
public enum NetPlayerFlags
{
    None = 0, Rolling = 1, Reloading = 2, Healing = 4, Downed = 8, Dead = 16, Restrained = 32, HostMoves = 64, Stunned = 128,
}

/// <summary>A survivor in a snapshot. The owner's inventory rides along so its HUD matches the host.</summary>
public struct NetPlayerState
{
    public int Peer;
    public Vector3 Position, Aim;
    public int Health, Medkits;
    public float BleedLeft;
    public NetPlayerFlags Flags;
    public int ActiveSlot;
    /// <summary>Tap dance steps (M-29): the client walks its own survivor, so it needs them.</summary>
    public int TapStacks;
    /// <summary>Mad scientist size (0 none, 1 big, 2 small) + its level × 4.</summary>
    public int Mad;
    /// <summary>Honk beam daze and weakness left (tenths of a second); static fluff pole (0 none, 1 plus, 2 minus).</summary>
    public int Daze, Weak, Pole;
    public int Secondary, SecondaryMag, SecondaryReserve;
    public int Primary, PrimaryMag, PrimaryReserve; // Primary −1 = none
    public int Special, SpecialMag, SpecialReserve; // Special −1 = none
    /// <summary>Outpost backpack: (cell, kind, id, count, magazine); null outside Outpost.</summary>
    public List<(int Cell, int Kind, int Id, int Count, int Mag)>? Backpack;
    public List<(int Kind, int Count)> Throwables;

    public void Write(NetWriter w)
    {
        w.I32(Peer);
        w.Pos(Position);
        w.Pos(Aim);
        w.U8(Health);
        w.U8(Medkits);
        w.U8(Mathf.Clamp(Mathf.RoundToInt(BleedLeft * 4), 0, 255));
        w.U8((int)Flags);
        w.U8(ActiveSlot);
        w.U8(TapStacks);
        w.U8(Mad);
        w.U8(Daze);
        w.U8(Weak);
        w.U8(Pole);
        w.U16(Secondary); w.U16(SecondaryMag); w.U16(SecondaryReserve);
        w.S16(Primary); w.U16(PrimaryMag); w.U16(PrimaryReserve);
        w.S16(Special); w.U16(SpecialMag); w.U16(SpecialReserve);
        w.U8(Throwables?.Count ?? 0);
        if (Throwables != null) foreach (var (kind, count) in Throwables) { w.U8(kind); w.U8(count); }
        w.U8(Backpack == null ? 0xFF : Backpack.Count);
        if (Backpack != null) foreach (var (cell, kind, id, count, mag) in Backpack) { w.U8(cell); w.U8(kind); w.U16(id); w.U16(count); w.U16(mag); }
    }

    public static NetPlayerState Read(NetReader r)
    {
        var s = new NetPlayerState
        {
            Peer = r.I32(), Position = r.Pos(), Aim = r.Pos(), Health = r.U8(), Medkits = r.U8(), BleedLeft = r.U8() / 4f,
            Flags = (NetPlayerFlags)r.U8(), ActiveSlot = r.U8(), TapStacks = r.U8(), Mad = r.U8(), Daze = r.U8(), Weak = r.U8(), Pole = r.U8(),
            Secondary = r.U16(), SecondaryMag = r.U16(), SecondaryReserve = r.U16(),
            Primary = r.S16(), PrimaryMag = r.U16(), PrimaryReserve = r.U16(),
            Special = r.S16(), SpecialMag = r.U16(), SpecialReserve = r.U16(),
            Throwables = new List<(int, int)>(),
        };
        int n = r.U8();
        for (int i = 0; i < n; i++) s.Throwables.Add((r.U8(), r.U8()));
        int items = r.U8();
        if (items != 0xFF)
        {
            s.Backpack = new List<(int, int, int, int, int)>(items);
            for (int i = 0; i < items; i++) s.Backpack.Add((r.U8(), r.U8(), r.U16(), r.U16(), r.U16()));
        }
        return s;
    }
}

[System.Flags]
public enum NetEnemyFlags { None = 0, Awake = 1, Stunned = 2, Charging = 4, Winding = 8 }

/// <summary>An enemy in a snapshot: 10 bytes.</summary>
public struct NetEnemyState
{
    public int Id;
    public Vector3 Position, Facing;
    public NetEnemyFlags Flags;

    public void Write(NetWriter w)
    {
        w.U16(Id);
        w.FlatPos(Position);
        w.S16(Mathf.RoundToInt(Position.Y * 100));
        w.Yaw(Facing);
        w.U8((int)Flags);
    }

    public static NetEnemyState Read(NetReader r)
    {
        int id = r.U16();
        var flat = r.FlatPos();
        float y = r.S16() / 100f;
        return new NetEnemyState { Id = id, Position = new Vector3(flat.X, y, flat.Z), Facing = r.Yaw(), Flags = (NetEnemyFlags)r.U8() };
    }
}

/// <summary>Host → client, 20 per second; enemies ride in every other one (10 per second, decided 2026-10-20).</summary>
public sealed class NetSnapshot
{
    public double Time;
    public float Shutter;
    public List<NetPlayerState> Players = new();
    public List<NetEnemyState>? Enemies;

    public byte[] Encode()
    {
        var w = new NetWriter();
        w.F32((float)Time);
        w.U8(Mathf.RoundToInt(Mathf.Clamp(Shutter, 0, 1) * 255));
        w.U8(Players.Count);
        foreach (var p in Players) p.Write(w);
        w.U16(Enemies?.Count ?? 0xFFFF);
        if (Enemies != null) foreach (var e in Enemies) e.Write(w);
        return w.ToArray();
    }

    public static NetSnapshot Decode(byte[] data)
    {
        var r = new NetReader(data);
        var s = new NetSnapshot { Time = r.F32(), Shutter = r.U8() / 255f };
        int players = r.U8();
        for (int i = 0; i < players; i++) s.Players.Add(NetPlayerState.Read(r));
        int enemies = r.U16();
        if (enemies != 0xFFFF)
        {
            s.Enemies = new List<NetEnemyState>(enemies);
            for (int i = 0; i < enemies; i++) s.Enemies.Add(NetEnemyState.Read(r));
        }
        return s;
    }
}

/// <summary>Host → client events (reliable, batched once per physics frame). See multiplayer-sync-design.md 4.3.</summary>
public enum NetEvent
{
    EnemySpawn = 1, EnemyDeath, EnemyHit, EnemyAttack, EnemyAttackEnd, EnemyProjectile,
    PlayerHit, Shot, Throw, BarrelExplode, World, PickupSpawn, Notice, StageEnd, LootSpawn, CardProp, EnemyBurn, EnemyWeak, Roster,
}

/// <summary>Enemy attack kinds for <see cref="NetEvent.EnemyAttack"/>.</summary>
public enum NetAttack { Melee = 1, Ranged, Capture, Charge }

/// <summary>
/// Events of one frame, each with its length, so a client can skip one it cannot read and still apply the rest.
/// </summary>
public sealed class NetEventBatch
{
    public NetWriter W { get; private set; } = new();
    public int Count { get; private set; }
    private int _open = -1;

    public void Begin(NetEvent type)
    {
        Close();
        W.U8((int)type);
        _open = W.ReserveU16();
        Count++;
    }

    private void Close()
    {
        if (_open >= 0) W.PatchU16(_open, W.Length - _open - 2);
        _open = -1;
    }

    /// <summary>[count lo, count hi, events...] — and starts a fresh batch.</summary>
    public byte[] Take()
    {
        Close();
        var body = W.ToArray();
        var packet = new byte[body.Length + 2];
        packet[0] = (byte)(Count & 0xFF);
        packet[1] = (byte)(Count >> 8);
        body.CopyTo(packet, 2);
        W = new NetWriter();
        Count = 0;
        return packet;
    }
}
