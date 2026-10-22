using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>Where a special card vote stands (decisions.md M-29, plan 16.5).</summary>
public enum VotePhase { None, Voting, Spinning, Result, Countdown }

/// <summary>
/// The team's special card vote (decisions.md M-29, plan 16.5). A middle boss down stops everyone
/// (<see cref="Stage.SetAllStop"/>) and three special cards come up; each person stamps one, anonymously — every PC
/// sees only how many stamps each card has and how many have voted. A stamp cannot be moved. The vote settles early
/// once the leader cannot be caught (first &gt; second + not yet voted), when everyone has voted, or when the time
/// runs out (no stamps at all: any of the three). A tie spins between the tied cards. The winner goes to the whole
/// team, then 3 · 2 · 1 and play goes on. Downed people vote too; someone who leaves loses their stamp and stops
/// counting; someone who joins mid-vote waits for the next one. The host decides; clients send their stamp and mirror
/// the host's state. Runs while the tree is paused.
/// </summary>
public partial class SpecialVote : Node
{
    public const float SpinSeconds = 1.6f, ShowSeconds = .8f, ResultSeconds = 1.4f, CountdownSeconds = 3f, SyncEvery = .25f;

    public Stage Stage { get; set; } = null!;
    /// <summary>Seconds to vote (mode_rules.json survivor.special_vote_s).</summary>
    public float Limit { get; set; } = 60;
    /// <summary>Host: applies the winning card (it is the team's).</summary>
    public Action<CardChoice>? Won { get; set; }
    /// <summary>Client: sends this PC's stamp to the host.</summary>
    public Action<int, int>? SendStamp { get; set; }
    /// <summary>A stamp landed, a vote settled or the phase moved on (the window listens).</summary>
    public event Action? Changed;

    public VotePhase Phase { get; private set; }
    /// <summary>Numbers the votes of a match; a client's stamp names the vote it answers.</summary>
    public int Serial { get; private set; }
    public CardOffer? Offer { get; private set; }
    /// <summary>Seconds left in this phase (the voting gauge, the spin, the countdown).</summary>
    public float Left { get; private set; }
    /// <summary>Stamps on each card (who stamped which is never kept on a client).</summary>
    public int[] Stamps { get; private set; } = new int[3];
    public int Voters { get; private set; }
    public int Voted { get; private set; }
    /// <summary>The winning card once settled (-1 before).</summary>
    public int Winner { get; private set; } = -1;
    /// <summary>The cards the winner was drawn from: one when it was clear, several for a tie.</summary>
    public IReadOnlyList<int> Tied { get; private set; } = Array.Empty<int>();
    /// <summary>This PC's own stamp (-1 = none).</summary>
    public int Mine { get; private set; } = -1;
    /// <summary>Whether this PC's survivor is one of this vote's voters (joiners and AI squadmates are not).</summary>
    public bool IsVoter { get; private set; }
    public bool Active => Phase != VotePhase.None;
    public bool CanStamp => Phase == VotePhase.Voting && IsVoter && Mine < 0;

    private readonly HashSet<Player> _voters = new();
    private readonly Dictionary<Player, int> _stamps = new();
    private readonly RandomNumberGenerator _rng = new();
    private float _syncTimer;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _rng.Randomize();
    }

    // ---------- the rule ----------

    /// <summary>
    /// The cards the winner is drawn from once the vote is settled, or null while it is still open. Settled early when
    /// the leader cannot be caught (<c>first &gt; second + notYet</c>), or when nobody is left to vote, or at time up:
    /// then the cards with the most stamps (all of them when there are none).
    /// </summary>
    public static IReadOnlyList<int>? Settle(IReadOnlyList<int> stamps, int notYet, bool timeUp)
    {
        var order = stamps.OrderByDescending(s => s).ToList();
        int first = order[0], second = order.Count > 1 ? order[1] : 0;
        if (notYet > 0 && !timeUp && first <= second + notYet) return null;
        if (first == 0) return Enumerable.Range(0, stamps.Count).ToList();
        return Enumerable.Range(0, stamps.Count).Where(i => stamps[i] == first).ToList();
    }

    // ---------- host ----------

    /// <summary>Host: stops everyone and opens the vote on <paramref name="offer"/> for <paramref name="voters"/>.</summary>
    public void Begin(CardOffer offer, IEnumerable<Player> voters)
    {
        Serial++;
        Offer = offer;
        _voters.Clear();
        foreach (var p in voters) _voters.Add(p);
        _stamps.Clear();
        Stamps = new int[offer.Choices.Count];
        Mine = -1;
        Winner = -1;
        Tied = Array.Empty<int>();
        IsVoter = _voters.Contains(Stage.Player);
        Phase = VotePhase.Voting;
        Left = Limit;
        _syncTimer = 0;
        Stage.SetAllStop(true);
        Recount();
        Changed?.Invoke();
    }

    /// <summary>Host: <paramref name="who"/> stamps card <paramref name="index"/> of vote <paramref name="serial"/> (once; later stamps are ignored).</summary>
    public bool Stamp(Player who, int serial, int index)
    {
        if (Phase != VotePhase.Voting || serial != Serial || !_voters.Contains(who) || _stamps.ContainsKey(who) || index < 0 || index >= Stamps.Length)
            return false;
        _stamps[who] = index;
        if (who == Stage.Player) Mine = index;
        Recount();
        Changed?.Invoke();
        return true;
    }

    /// <summary>This PC's survivor stamps (a client sends it to the host and shows it at once).</summary>
    public void StampLocal(int index)
    {
        if (!CanStamp || Offer == null || index < 0 || index >= Offer.Choices.Count) return;
        if (Stage.Authoritative) { Stamp(Stage.Player, Serial, index); return; }
        Mine = index;
        SendStamp?.Invoke(Serial, index);
        Changed?.Invoke();
    }

    private void Recount()
    {
        // Someone who left takes their stamp with them and stops counting.
        _voters.RemoveWhere(p => !IsInstanceValid(p) || !Stage.Players.Contains(p));
        foreach (var gone in _stamps.Keys.Where(p => !_voters.Contains(p)).ToList()) _stamps.Remove(gone);
        Stamps = new int[Stamps.Length];
        foreach (var index in _stamps.Values) Stamps[index]++;
        Voters = _voters.Count;
        Voted = _stamps.Count;
        if (Phase == VotePhase.Voting && Settle(Stamps, Voters - Voted, Left <= 0) is { } tied) Decide(tied);
    }

    private void Decide(IReadOnlyList<int> tied)
    {
        Tied = tied;
        Winner = tied[_rng.RandiRange(0, tied.Count - 1)];
        Phase = VotePhase.Spinning;
        Left = tied.Count > 1 ? SpinSeconds : ShowSeconds;
    }

    public override void _Process(double delta)
    {
        if (!Active) return;
        float dt = (float)delta;
        Left = Mathf.Max(0, Left - dt);
        if (!Stage.Authoritative) return;
        var was = Phase;
        if (Phase == VotePhase.Voting) Recount();
        else if (Left <= 0) Advance();
        if (Phase != was) Changed?.Invoke();
        _syncTimer -= dt;
        if (_syncTimer <= 0 || Phase != was) Sync();
    }

    private void Advance()
    {
        switch (Phase)
        {
            case VotePhase.Spinning:
                if (Offer != null && Winner >= 0) Won?.Invoke(Offer.Choices[Winner]);
                Phase = VotePhase.Result;
                Left = ResultSeconds;
                break;
            case VotePhase.Result:
                Phase = VotePhase.Countdown;
                Left = CountdownSeconds;
                break;
            case VotePhase.Countdown:
                Phase = VotePhase.None;
                Stage.SetAllStop(false);
                break;
        }
    }

    /// <summary>Host: every client gets the vote as it stands (its own stamp too, never anyone else's).</summary>
    public void Sync()
    {
        _syncTimer = SyncEvery;
        if (Stage.Net is not { Role: NetRole.Host } net) return;
        foreach (var p in Stage.Players.Where(p => p.IsRemote && IsInstanceValid(p)))
            net.SendVote(p, Encode(p));
    }

    // ---------- network ----------

    public byte[] Encode(Player who)
    {
        var w = new NetWriter();
        w.U16(Serial);
        w.U8((int)Phase);
        if (Phase == VotePhase.None || Offer == null) return w.ToArray();
        w.F32(Left);
        w.U8(Voters);
        w.U8(Voted);
        w.U8(Stamps.Length);
        foreach (var s in Stamps) w.U8(s);
        w.U8(Winner < 0 ? 255 : Winner);
        w.U8(Tied.Count);
        foreach (var t in Tied) w.U8(t);
        w.U8(_voters.Contains(who) ? 1 : 0);
        w.U8(_stamps.TryGetValue(who, out int mine) ? mine : 255);
        CardCatalog.Write(w, new[] { Offer });
        return w.ToArray();
    }

    /// <summary>Client: the host's vote, stopping or freeing this PC with it.</summary>
    public void Apply(byte[] data)
    {
        var r = new NetReader(data);
        int serial = r.U16();
        var phase = (VotePhase)r.U8();
        if (serial != Serial) Mine = -1;
        Serial = serial;
        Phase = phase;
        if (phase == VotePhase.None)
        {
            Offer = null;
            Stage.SetAllStop(false);
            Changed?.Invoke();
            return;
        }
        Left = r.F32();
        Voters = r.U8();
        Voted = r.U8();
        var stamps = new int[r.U8()];
        for (int i = 0; i < stamps.Length; i++) stamps[i] = r.U8();
        Stamps = stamps;
        int winner = r.U8();
        Winner = winner == 255 ? -1 : winner;
        var tied = new int[r.U8()];
        for (int i = 0; i < tied.Length; i++) tied[i] = r.U8();
        Tied = tied;
        IsVoter = r.U8() == 1;
        int mine = r.U8();
        // A stamp sent but not counted yet stays shown.
        if (mine != 255) Mine = mine;
        Offer = CardCatalog.Read(r).FirstOrDefault();
        Stage.SetAllStop(true);
        Changed?.Invoke();
    }

    // ---------- checks ----------

    /// <summary>Checks: the stamps by card as the host keeps them (who stamped which).</summary>
    public IReadOnlyDictionary<Player, int> StampsBy => _stamps;
}
