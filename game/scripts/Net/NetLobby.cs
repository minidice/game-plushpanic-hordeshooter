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

    public sealed class Member
    {
        public int Peer;
        public string Character = "";
        public bool Ready;
    }

    public NetRole Role { get; private set; }
    /// <summary>The host first, then everyone else in the order they came in.</summary>
    public List<Member> Members { get; } = new();
    public GameMode Mode { get; private set; } = GameMode.Scenario;
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
        Mode = GameSession.PlayableModes.Contains(GameSession.Mode) ? GameSession.Mode : GameMode.Scenario,
    };

    private string _character = "";

    public override void _Ready()
    {
        if (Role == NetRole.Host)
        {
            Members.Add(new Member { Peer = HostPeer, Character = _character, Ready = true });
            Multiplayer.PeerDisconnected += OnPeerLeft;
        }
        else
        {
            Multiplayer.ServerDisconnected += OnHostLeft;
            RpcId(HostPeer, MethodName.Hello, _character, NetIds.DataHash());
        }
    }

    public override void _ExitTree()
    {
        // The multiplayer API outlives this node: leave nothing pointing at it.
        if (Role == NetRole.Host) Multiplayer.PeerDisconnected -= OnPeerLeft;
        else Multiplayer.ServerDisconnected -= OnHostLeft;
    }

    // =====================================================================
    // Host
    // =====================================================================

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Hello(string character, int dataHash)
    {
        if (Role != NetRole.Host) return;
        int peer = Multiplayer.GetRemoteSenderId();
        // Another build or other data would read our ids differently (multiplayer-sync-design.md 6장).
        if (dataHash != NetIds.DataHash())
        {
            GD.PushWarning($"[lobby] refused peer {peer}: different data");
            RpcId(peer, MethodName.Refused, "msg_net_peer_refused");
            return;
        }
        if (Members.FirstOrDefault(m => m.Peer == peer) is { } known) known.Character = character;
        else Members.Add(new Member { Peer = peer, Character = character });
        GD.Print($"[lobby] peer {peer} came in as {character}");
        Broadcast();
        // A match is already running: join it (the stage gives late survivors a place beside the host).
        if (Live) RpcId(peer, MethodName.MatchStarted, (int)Mode);
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
    public void SetMode(GameMode mode)
    {
        if (Role != NetRole.Host || InMatch || mode == Mode || !GameSession.PlayableModes.Contains(mode)) return;
        Mode = mode;
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
        GameSession.Mode = Mode;
        EnterMatch(this);
        return true;
    }

    /// <summary>Host: the stage can take survivors now. Everyone in the room loads it.</summary>
    public void HostLive()
    {
        if (Role != NetRole.Host || !InMatch || Live) return;
        Live = true;
        foreach (var m in Members.Where(m => m.Peer != HostPeer)) RpcId(m.Peer, MethodName.MatchStarted, (int)Mode);
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
        w.U8(InMatch ? 1 : 0);
        w.U8(Members.Count);
        foreach (var m in Members) { w.I32(m.Peer); w.Str(m.Character); w.U8(m.Ready ? 1 : 0); }
        var bytes = w.ToArray();
        foreach (var m in Members.Where(m => m.Peer != HostPeer)) RpcId(m.Peer, MethodName.Roster, bytes);
        Changed?.Invoke();
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
        Mode = (GameMode)r.U8();
        InMatch = r.U8() != 0;
        int count = r.U8();
        Members.Clear();
        for (int i = 0; i < count; i++) Members.Add(new Member { Peer = r.I32(), Character = r.Str(), Ready = r.U8() != 0 });
        Joined = true;
        Changed?.Invoke();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void MatchStarted(int mode)
    {
        if (Role != NetRole.Client) return;
        InMatch = true;
        Mode = (GameMode)mode;
        GameSession.Mode = Mode;
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
