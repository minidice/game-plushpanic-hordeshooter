using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// The room before a match (doc/menu-multiplayer-plan.md 2장): who is here, who is ready, and the host's mode.
/// One node named <see cref="NodeName"/> under the multiplayer root on every PC, alive as long as the link is open, so a
/// friend who joins during a match is still answered and sent straight into it. The host decides; a client only asks.
/// </summary>
public partial class NetLobby : Node
{
    public const string NodeName = "NetLobby";
    public const int HostPeer = 1;
    /// <summary>Bump when lobby or match messages change shape: older builds then show as another version.</summary>
    public const int Protocol = 17;
    /// <summary>Two PCs can play together only on the same version: messages and data ids (multiplayer-sync-design.md 6장).</summary>
    public static string Version => $"p{Protocol}-{(uint)NetIds.DataHash():x8}";
    /// <summary>Seconds between the host's refreshes of its room on the public list (it expires after 45 s).</summary>
    public const double AdInterval = 15;

    public sealed class Member
    {
        public int Peer;
        public string Character = "";
        public bool Ready;
    }

    public NetRole Role { get; private set; }
    /// <summary>The host first, then everyone else in the order they came in.</summary>
    public List<Member> Members { get; } = new();
    /// <summary>The host's game setup (mode, card, difficulty — M-39 · M-40); guests get it with the roster.</summary>
    public PlaySetup Setup { get; private set; } = new(GameMode.Scenario, GameSession.Campaigns[0], Difficulty.Normal);
    public GameMode Mode => Setup.Mode;
    /// <summary>The host pressed start (it may still be loading the stage).</summary>
    public bool InMatch { get; private set; }
    /// <summary>Host: its stage is running, so newcomers go straight in.</summary>
    public bool Live { get; private set; }
    /// <summary>Client: the host has answered our hello.</summary>
    public bool Joined { get; private set; }
    public int Me => Role == NetRole.Host ? HostPeer : Multiplayer.GetUniqueId();
    public Member? Mine => Members.FirstOrDefault(m => m.Peer == Me);
    /// <summary>Everyone but the host is ready. The host alone may start too.</summary>
    public bool CanStart => Role == NetRole.Host && !InMatch && Members.Where(m => m.Peer != HostPeer).All(m => m.Ready);
    /// <summary>Host: put this room on the public list (RoomDirectory). Off in tests.</summary>
    public bool Advertise { get; set; }
    /// <summary>Host: the list has this room right now.</summary>
    public bool Listed => _listing != null;
    /// <summary>Host: the list server could not be reached on the last try.</summary>
    public bool ListFailed { get; private set; }

    /// <summary>The roster, mode or match state changed.</summary>
    public event Action? Changed;
    /// <summary>The room is gone for this PC (the host left, or refused us): a Loc key saying why.</summary>
    public event Action<string>? Closed;
    /// <summary>Loads the match on this PC. The game opens Stage 1; tests replace it.</summary>
    public Action<NetLobby> EnterMatch { get; set; } = lobby => lobby.GetTree().ChangeSceneToFile("res://scenes/Stage1.tscn");
    /// <summary>
    /// Leaves the match for the room on this PC: the stage stops talking at once, the menu (which opens this room's page)
    /// comes a moment later so packets already on their way still find the stage. Tests replace it.
    /// </summary>
    public Action<NetLobby> LeaveMatch { get; set; } = lobby =>
    {
        var tree = lobby.GetTree();
        if (tree.CurrentScene is Stage { Net: { } net }) net.Ending = true;
        tree.CreateTimer(.3).Timeout += () => tree.ChangeSceneToFile("res://scenes/Menu.tscn");
    };

    /// <summary>The host starts on the mode last picked in the menu, if this build can play it.</summary>
    public static NetLobby Create(NetRole role, string character) => new()
    {
        Name = NodeName, Role = role, _character = character,
        Setup = GameSession.PlayableModes.Contains(GameSession.Mode) ? PlaySetup.FromSession() : PlaySetup.FromSession() with { Mode = GameMode.Scenario },
    };

    private string _character = "";
    private RoomDirectory.Listing? _listing;
    private double _adTimer;
    private bool _adBusy, _adDirty;

    public override void _Ready()
    {
        // The room keeps refreshing its listing while the stage (or its pause menu) runs.
        ProcessMode = ProcessModeEnum.Always;
        if (Role == NetRole.Host)
        {
            Members.Add(new Member { Peer = HostPeer, Character = _character, Ready = true });
            Multiplayer.PeerDisconnected += OnPeerLeft;
        }
        else
        {
            Multiplayer.ServerDisconnected += OnHostLeft;
            RpcId(HostPeer, MethodName.Hello, _character, Version);
        }
    }

    public override void _ExitTree()
    {
        // Off the list at once rather than after the 45 s expiry.
        if (_listing != null) _ = RoomDirectory.Remove(_listing);
        _listing = null;
        // The multiplayer API outlives this node: leave nothing pointing at it.
        if (Role == NetRole.Host) Multiplayer.PeerDisconnected -= OnPeerLeft;
        else Multiplayer.ServerDisconnected -= OnHostLeft;
    }

    // =====================================================================
    // Host
    // =====================================================================

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Hello(string character, string version)
    {
        if (Role != NetRole.Host) return;
        int peer = Multiplayer.GetRemoteSenderId();
        // Another build or other data would read our messages and ids differently (multiplayer-sync-design.md 6장).
        if (version != Version)
        {
            GD.PushWarning($"[lobby] refused peer {peer}: version {version}, ours {Version}");
            RpcId(peer, MethodName.Refused, "msg_net_peer_refused");
            return;
        }
        if (Members.FirstOrDefault(m => m.Peer == peer) is { } known) known.Character = character;
        else Members.Add(new Member { Peer = peer, Character = character });
        GD.Print($"[lobby] peer {peer} came in as {character}");
        Broadcast();
        // A match is already running: join it (the stage gives late survivors a place beside the host).
        if (Live) RpcId(peer, MethodName.MatchStarted, (int)Mode, Setup.Choice, (int)Setup.Difficulty, GameSession.MapSeed);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void AskReady(bool ready)
    {
        if (Role != NetRole.Host || InMatch) return;
        var member = Members.FirstOrDefault(m => m.Peer == Multiplayer.GetRemoteSenderId());
        if (member == null || member.Ready == ready) return;
        member.Ready = ready;
        Broadcast();
    }

    /// <summary>Host: a new mode clears everyone's ready (they agreed to the old one).</summary>
    public void SetMode(GameMode mode) => SetSetup(Setup with { Mode = mode });

    /// <summary>Host: a new setup (mode, card or difficulty) clears everyone's ready, as a new mode did.</summary>
    public void SetSetup(PlaySetup setup)
    {
        setup = setup.Valid();
        if (Role != NetRole.Host || InMatch || setup == Setup || !GameSession.PlayableModes.Contains(setup.Mode)) return;
        Setup = setup;
        foreach (var m in Members) m.Ready = m.Peer == HostPeer;
        Broadcast();
    }

    /// <summary>Host: load the match. Friends follow once the host's stage is running (<see cref="HostLive"/>).</summary>
    public bool Start()
    {
        if (!CanStart) return false;
        InMatch = true;
        GD.Print($"[lobby] starting {Mode} with {Members.Count} player(s)");
        Broadcast();
        Setup.Apply();
        // One seed for the whole room: a scattered map comes out the same on every PC.
        GameSession.MapSeed = (int)(GD.Randi() % 1000000) + 1;
        GameSession.StartCampaign();
        EnterMatch(this);
        return true;
    }

    /// <summary>Host: the stage can take survivors now. Everyone in the room loads it.</summary>
    public void HostLive()
    {
        if (Role != NetRole.Host || !InMatch || Live) return;
        Live = true;
        foreach (var m in Members.Where(m => m.Peer != HostPeer)) RpcId(m.Peer, MethodName.MatchStarted, (int)Mode, Setup.Choice, (int)Setup.Difficulty, GameSession.MapSeed);
    }

    /// <summary>
    /// Host: the match is over (restart after a wipe, a clear, or from the pause menu): everyone goes back to this room,
    /// still connected, and has to ready up again (menu-multiplayer-plan.md 2장 "결과 → 대기").
    /// </summary>
    public void ReturnToLobby()
    {
        if (Role != NetRole.Host || !InMatch) return;
        InMatch = Live = false;
        foreach (var m in Members) m.Ready = m.Peer == HostPeer;
        GD.Print("[lobby] back to the room");
        Broadcast();
        foreach (var m in Members.Where(m => m.Peer != HostPeer)) RpcId(m.Peer, MethodName.BackToRoom);
        LeaveMatch(this);
    }

    private void OnPeerLeft(long id)
    {
        if (Members.RemoveAll(m => m.Peer == (int)id) == 0) return;
        GD.Print($"[lobby] peer {id} left");
        Broadcast();
    }

    private void Broadcast()
    {
        var w = new NetWriter();
        w.U8((int)Mode);
        w.Str(Setup.Choice);
        w.U8((int)Setup.Difficulty);
        w.U8(InMatch ? 1 : 0);
        w.U8(Members.Count);
        foreach (var m in Members) { w.I32(m.Peer); w.Str(m.Character); w.U8(m.Ready ? 1 : 0); }
        var bytes = w.ToArray();
        foreach (var m in Members.Where(m => m.Peer != HostPeer)) RpcId(m.Peer, MethodName.Roster, bytes);
        _adDirty = true;
        Changed?.Invoke();
    }

    // ----- the public room list -----

    public override void _Process(double delta)
    {
        if (!Advertise || Role != NetRole.Host || _adBusy) return;
        _adTimer -= delta;
        // Player count and state go out soon after they change, but no more than once a second.
        if (_adTimer > 0 && !(_adDirty && _adTimer < AdInterval - 1)) return;
        _adDirty = false;
        _adTimer = AdInterval;
        UpdateListing();
    }

    private RoomDirectory.Ad Ad() => new(Version, ListName, Members.Count, NetLaunch.MaxPlayers, InMatch ? "playing" : "lobby",
        NetLaunch.Port, NetLaunch.LanAddresses().Take(4).ToArray());

    /// <summary>The host's character in this PC's language (the list shows it as the room name).</summary>
    public string ListName { get; set; } = "";

    private async void UpdateListing()
    {
        _adBusy = true;
        bool wasListed = Listed, wasFailed = ListFailed;
        try
        {
            var ad = Ad();
            bool? kept = _listing == null ? false : await RoomDirectory.Refresh(_listing, ad);
            if (!IsInsideTree()) return;
            if (kept == false)
            {
                // New room, or the list forgot it (server restart, long pause): register again.
                var listing = await RoomDirectory.Register(ad);
                if (!IsInsideTree()) { if (listing != null) _ = RoomDirectory.Remove(listing); return; }
                _listing = listing;
                kept = listing != null ? true : null;
                if (listing != null) GD.Print($"[rooms] listed as {listing.Id} ({Version})");
            }
            ListFailed = kept == null;
            if (ListFailed != wasFailed || Listed != wasListed) Changed?.Invoke();
        }
        finally { _adBusy = false; }
    }

    // =====================================================================
    // Client
    // =====================================================================

    /// <summary>Client: ask the host to mark us ready (or not). The roster coming back is the answer.</summary>
    public void SetReady(bool ready)
    {
        if (Role == NetRole.Client && Joined && !InMatch) RpcId(HostPeer, MethodName.AskReady, ready);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Roster(byte[] data)
    {
        if (Role != NetRole.Client) return;
        var r = new NetReader(data);
        Setup = new PlaySetup((GameMode)r.U8(), r.Str(), (Difficulty)r.U8());
        InMatch = r.U8() != 0;
        int count = r.U8();
        Members.Clear();
        for (int i = 0; i < count; i++) Members.Add(new Member { Peer = r.I32(), Character = r.Str(), Ready = r.U8() != 0 });
        Joined = true;
        Changed?.Invoke();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void MatchStarted(int mode, string choice, int difficulty, int seed)
    {
        if (Role != NetRole.Client) return;
        InMatch = true;
        Setup = new PlaySetup((GameMode)mode, choice, (Difficulty)difficulty).Valid();
        Setup.Apply();
        GameSession.MapSeed = seed;
        GameSession.StartCampaign();
        Changed?.Invoke();
        EnterMatch(this);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void BackToRoom()
    {
        if (Role != NetRole.Client) return;
        InMatch = false;
        Changed?.Invoke();
        LeaveMatch(this);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Refused(string reason) => Closed?.Invoke(reason);

    private void OnHostLeft()
    {
        // During a match the stage says so and goes back to the menu itself (StageNet.LeaveMatch).
        if (!InMatch) Closed?.Invoke("msg_net_host_left");
    }
}
