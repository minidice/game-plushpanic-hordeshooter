using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter.Tests;

/// <summary>
/// Network messages survive packing and unpacking (multiplayer-sync-design.md 7장): input, players with inventory,
/// enemy snapshots at 0/70/200 enemies and the edges of the map, and the interpolation track. No network involved.
/// </summary>
public partial class NetCodecCheck : Node
{
    private int _checks;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }

    public override void _Ready()
    {
        bool failed = false;
        try { Run(); GD.Print("NET_CODEC_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("NET_CODEC_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private static bool Near(Vector3 a, Vector3 b, float tolerance = .006f) => a.DistanceTo(b) <= tolerance;

    private void Run()
    {
        foreach (float x in new[] { -8192.25f, -327.69f, 327.68f, 633.25f, 8192.25f })
        {
            var position = new Vector3(x, 400.25f, -x);
            var coordinate = new NetWriter(); coordinate.Pos(position);
            Check(Near(new NetReader(coordinate.ToArray()).Pos(), position), $"large signed coordinates survive: {position}");
        }
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, 100000f, -100000f })
        {
            bool rejected = false;
            try { new NetWriter().Pos(new Vector3(invalid, 0, 0)); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, $"invalid coordinate is rejected instead of clipped: {invalid}");
        }
        var presses = new byte[(int)NetPress.Count];
        presses[(int)NetPress.Roll] = 3;
        presses[(int)NetPress.CycleDown] = 255;
        var input = new NetInput { Seq = 123456, Aim = new Vector3(127.5f, 1.2f, -11.9f), Held = NetHeld.Fire | NetHeld.Revive, Presses = presses, Slot = 2, SlotPresses = 7, Position = new Vector3(-1.99f, .04f, 11.99f) };
        var w = new NetWriter();
        input.Write(w);
        var back = NetInput.Read(new NetReader(w.ToArray()));
        Check(back.Seq == input.Seq && Near(back.Aim, input.Aim) && back.Held == input.Held && back.Presses.SequenceEqual(presses) && back.Slot == 2 && back.SlotPresses == 7 &&
              Near(back.Position, input.Position), $"input with press counts survives the round trip ({w.Length} bytes)");

        // Dual pistols are made from their base weapon: they need an id too, or every shot with them breaks the client (I-73).
        var dual = ArmoryCatalog.Weapon("m1911").DualVariant!;
        Check(NetIds.WeaponDef(NetIds.Weapon(dual.Id)) == dual, $"the dual variant {dual.Id} has a network id");
        bool threw = false;
        try { NetIds.Weapon("no_such_gun"); } catch (InvalidOperationException) { threw = true; }
        Check(threw, "an unknown weapon fails on the sending side instead of sending a bad index");

        // Events carry their length: a client can skip one it cannot read and still apply the next.
        var batch = new NetEventBatch();
        batch.Begin(NetEvent.EnemyHit); batch.W.U16(5); batch.W.Pos(Vector3.One); batch.W.U8(9);
        batch.Begin(NetEvent.BarrelExplode); batch.W.U16(42);
        var reader = new NetReader(batch.Take());
        int eventCount = reader.U8() | reader.U8() << 8;
        var first = (NetEvent)reader.U8(); reader.Slice(reader.U16()); // skipped without reading it
        var second = (NetEvent)reader.U8(); var body = reader.Slice(reader.U16());
        Check(eventCount == 2 && first == NetEvent.EnemyHit && second == NetEvent.BarrelExplode && body.U16() == 42 && !reader.More,
            "an event can be skipped by its length and the next one still reads right");

        var player = new NetPlayerState
        {
            Peer = 2, Position = new Vector3(64.37f, 0, -3.12f), Aim = new Vector3(70, 1, 0), Health = 87, Medkits = 1, BleedLeft = 12.5f,
            Flags = NetPlayerFlags.Downed | NetPlayerFlags.HostMoves, ActiveSlot = 2, TapStacks = 7,
            Secondary = NetIds.Weapon("m1911"), SecondaryMag = 15, SecondaryReserve = 0,
            Primary = NetIds.Weapon("m4"), PrimaryMag = 37, PrimaryReserve = 250,
            Special = NetIds.Weapon("railgun"), SpecialMag = 1, SpecialReserve = 3,
            Throwables = new List<(int, int)> { (NetIds.Throwable("frag"), 2), (NetIds.Throwable("molotov"), 1) },
            Backpack = new List<(int, int, int, int, int)>
            {
                (0, (int)ItemKind.Ammo, NetIds.Item(ItemKind.Ammo, "762"), 40, 0),
                (19, (int)ItemKind.Weapon, NetIds.Item(ItemKind.Weapon, "ak47"), 1, 27),
                (7, (int)ItemKind.Medkit, 0, 3, 0),
            },
        };
        foreach (int count in new[] { 0, 70, 200 })
        {
            var snap = new NetSnapshot { Time = 1234.5678, Shutter = .5f, Players = { player } };
            if (count > 0)
                snap.Enemies = Enumerable.Range(1, count).Select(i => new NetEnemyState
                {
                    Id = i, Position = new Vector3(-2 + i * .63f, i % 2 * 3.45f, -12 + i * .11f), Facing = new Vector3(Mathf.Sin(i), 0, Mathf.Cos(i)),
                    Flags = (NetEnemyFlags)(i % 16),
                }).ToList();
            var bytes = snap.Encode();
            var got = NetSnapshot.Decode(bytes);
            var p = got.Players[0];
            bool players = p.Peer == 2 && Near(p.Position, player.Position) && p.Health == 87 && p.Medkits == 1 && Mathf.IsEqualApprox(p.BleedLeft, 12.5f) &&
                           p.Flags == player.Flags && p.ActiveSlot == 2 && p.TapStacks == 7 && NetIds.WeaponAt(p.Primary) == "m4" && p.PrimaryMag == 37 && p.PrimaryReserve == 250 &&
                           NetIds.WeaponAt(p.Special) == "railgun" && p.SpecialMag == 1 && p.SpecialReserve == 3 &&
                           NetIds.WeaponAt(p.Secondary) == "m1911" && p.Throwables.Count == 2 && NetIds.ThrowableAt(p.Throwables[0].Kind) == "frag" && p.Throwables[0].Count == 2 &&
                           p.Backpack is { Count: 3 } bag && bag[0] == player.Backpack![0] && bag[1] == player.Backpack[1] && bag[2] == player.Backpack[2] &&
                           NetIds.ItemAt(ItemKind.Ammo, bag[0].Id) == "762" && NetIds.ItemAt(ItemKind.Weapon, bag[1].Id) == "ak47";
            bool enemies = count == 0 ? got.Enemies == null
                : got.Enemies!.Count == count && got.Enemies.Zip(snap.Enemies!).All(t =>
                    t.First.Id == t.Second.Id && Near(t.First.Position, t.Second.Position) && t.First.Flags == t.Second.Flags &&
                    t.First.Facing.AngleTo(t.Second.Facing) < .03f);
            Check(players && enemies && Mathf.Abs((float)got.Time - 1234.5678f) < .001f && Mathf.Abs(got.Shutter - .5f) < .01f,
                $"a snapshot with {count} enemies survives the round trip ({bytes.Length} bytes)");
            if (count == 0)
            {
                var plain = new NetSnapshot { Players = { new NetPlayerState { Peer = 3, Throwables = new() } } };
                Check(NetSnapshot.Decode(plain.Encode()).Players[0].Backpack == null, "a survivor without a backpack (outside Outpost) stays without one");
            }
            if (count == 70) Check(bytes.Length < 1200 - 0, $"70 enemies fit one 1,200-byte packet ({bytes.Length} bytes)");
        }
        Check(NetIds.WeaponAt(NetIds.Weapon("ak47")) == "ak47" && NetIds.MonsterAt(NetIds.Monster("spitter")) == "spitter", "data ids map both ways");
        var crowd = new NetSnapshot { Time = 10, Players = Enumerable.Range(1, 6).Select(id => { var p = player; p.Peer = id; return p; }).ToList(),
            Enemies = Enumerable.Range(1, 200).Select(id => new NetEnemyState { Id = id, Position = new Vector3(600 + id, 400, -900) }).ToList() };
        var packets = crowd.EncodePackets().ToArray(); var pieces = packets.Select(NetSnapshot.Decode).ToArray();
        Check(packets.Length > 1 && packets.All(p => p.Length <= NetSnapshot.MaxPacketBytes), "six survivors and 200 enemies stay below the datagram budget");
        Check(pieces.SelectMany(p => p.Players).Select(p => p.Peer).SequenceEqual(Enumerable.Range(1, 6)) &&
            pieces.SelectMany(p => p.Enemies ?? new()).Select(e => e.Id).SequenceEqual(Enumerable.Range(1, 200)), "splitting never loses or duplicates a survivor or enemy");

        // The client draws the host's world 100 ms in the past, blending the samples around that moment.
        var track = new NetTrack();
        track.Push(1.0, new Vector3(0, 0, 0), Vector3.Back, 1);
        track.Push(1.1, new Vector3(1, 0, 0), Vector3.Back, 3);
        track.Push(1.05, new Vector3(9, 9, 9), Vector3.Back, 0); // late and out of order: ignored
        track.Sample(1.05, out var mid, out _);
        Check(Near(mid, new Vector3(.5f, 0, 0)) && track.Flags == 3, "the track blends between samples and ignores late ones");
        track.Sample(5, out var held, out _);
        Check(Near(held, new Vector3(1, 0, 0)), "when updates stop it holds the newest sample");
    }
}
