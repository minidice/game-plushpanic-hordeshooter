using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Every companion of a match (Onslaught and the card lab, decisions.md M-27): who has which, the companion arming
/// card, and the copy a client keeps. The deciding PC adds them; each client is sent the list (owner, key, fallen)
/// with its card sync and builds its own copies, so it sees everyone's companions move and shoot.
/// </summary>
public partial class CompanionSquad : Node3D
{
    public Stage Stage { get; set; } = null!;
    private readonly List<Companion> _all = new();
    public IReadOnlyList<Companion> All => _all;
    /// <summary>Goes up when one is added or removed (the host re-sends the list).</summary>
    public int Version { get; private set; }

    public IEnumerable<Companion> Of(Player who) => _all.Where(c => c.Survivor == who);

    public Companion? Add(Player owner, string key)
    {
        if (CompanionSpec.Parse(key) is not { } spec || !IsInstanceValid(owner)) return null;
        var companion = new Companion { Name = $"Companion_{spec.Kind}_{_all.Count}", Stage = Stage, Survivor = owner, Spec = spec, Slot = Of(owner).Count() };
        AddChild(companion);
        _all.Add(companion);
        Version++;
        return companion;
    }

    public void Clear()
    {
        foreach (var c in _all) c.QueueFree();
        _all.Clear();
        Version++;
    }

    public override void _PhysicsProcess(double delta)
    {
        // A survivor that left takes its companions along.
        for (int i = _all.Count - 1; i >= 0; i--)
            if (!IsInstanceValid(_all[i].Survivor) || !_all[i].Survivor.IsInsideTree())
            {
                _all[i].QueueFree();
                _all.RemoveAt(i);
                Version++;
            }
    }

    // ---------- the companion arming card ----------

    /// <summary>Three companion cards: a pet, a drone and a sidekick, each with a random look and gear.</summary>
    public CardOffer Offer(RandomNumberGenerator rng)
    {
        string Pick(IReadOnlyList<string> from) => from[rng.RandiRange(0, from.Count - 1)];
        var keys = new[]
        {
            new CompanionSpec(CompanionKind.Pet, Pick(CompanionSpec.PetSpecies), Pick(CompanionSpec.PetGear)).Key,
            new CompanionSpec(CompanionKind.Drone, Pick(DroneModel.BodyIds), Pick(CompanionSpec.DroneGear)).Key,
            new CompanionSpec(CompanionKind.Sidekick, Pick(SidekickModel.PresetIds), "").Key,
        };
        return new CardOffer { Kind = CardKind.Arming, TitleKey = "ui_card_companion_title", Choices = keys.Select(k => Card(k, this)).ToList() };
    }

    /// <summary>A companion as a card; picked, it joins the picker (<paramref name="squad"/> null = a client's copy, display only).</summary>
    public static CardChoice Card(string key, CompanionSquad? squad)
    {
        var spec = CompanionSpec.Parse(key)!;
        string kind = spec.Kind.ToString().ToLowerInvariant();
        var desc = Loc.Text(spec.Kind == CompanionKind.Sidekick ? "desc_companion_sidekick_gun" :"desc_companion_gear_" + spec.Gear) + "\n" +
                   Loc.Text("desc_companion_" + kind);
        return new CardChoice(key, spec.DisplayName, Loc.Text("tag_companion_" + kind), desc, CardTarget.Summon, null,
            who => squad?.Add(who, key), "f|" + key);
    }

    // ---------- network ----------

    public void Write(NetWriter w)
    {
        w.U8(_all.Count);
        foreach (var c in _all)
        {
            w.I32(c.Survivor.PeerId);
            w.Str(c.Spec.Key);
            w.U8(c.Down ? 1 : 0);
        }
    }

    /// <summary>Client: rebuilds its copies when the list changed, otherwise only who is down.</summary>
    public void Read(NetReader r)
    {
        int count = r.U8();
        var list = new List<(int Peer, string Key, bool Down)>();
        for (int i = 0; i < count; i++) list.Add((r.I32(), r.Str(), r.U8() == 1));
        // Survivors this PC does not have yet are skipped (their companions come with a later sync).
        list.RemoveAll(e => Stage.Players.All(p => p.PeerId != e.Peer));
        bool same = list.Count == _all.Count && list.Select((e, i) => _all[i].Survivor.PeerId == e.Peer && _all[i].Spec.Key == e.Key).All(x => x);
        if (!same)
        {
            Clear();
            foreach (var (peer, key, _) in list)
                if (Stage.Players.FirstOrDefault(p => p.PeerId == peer) is { } owner) Add(owner, key);
        }
        for (int i = 0; i < list.Count && i < _all.Count; i++) _all[i].SetDown(list[i].Down);
    }
}
