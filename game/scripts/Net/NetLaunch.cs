using Godot;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Opens and closes the link for a match (doc/multiplayer-sync-design.md 6장, menu-multiplayer-plan.md 2장).
/// The menu's "Create Room" / "Join" open a <see cref="NetLobby"/>; the host starts the match from there.
/// The command line skips the lobby for quick tests:
///   Godot --path . -- --host            → listens on <see cref="Port"/> and starts Stage 1 at once
///   Godot --path . -- --join=127.0.0.1  → the menu joins that room (straight into a running match)
/// The stage reads <see cref="Pending"/> when it is the current scene. Back at the menu everything is reset (I-73).
/// </summary>
public static class NetLaunch
{
    public const int Port = 24680;
    /// <summary>Two-player MVP: the host takes one client.</summary>
    public const int MaxClients = 1;
    public static int MaxPlayers => MaxClients + 1;
    public static NetRole Pending { get; private set; } = NetRole.Offline;
    /// <summary>The open room on this PC (null when playing offline).</summary>
    public static NetLobby? Lobby { get; private set; }
    /// <summary>The address joined last (saved with the settings and shown again in the menu).</summary>
    public static string LastAddress { get; set; } = "";
    private static bool _argsUsed;
    private static System.Action? _cancelJoin;

    /// <summary>
    /// Called by the main menu when it boots; arguments are used once. True for --host (the menu is skipped).
    /// For --join the menu still builds and joins with <paramref name="join"/>.
    /// </summary>
    public static bool TryStartFromArgs(SceneTree tree, out string? join)
    {
        join = null;
        if (_argsUsed) return false;
        _argsUsed = true;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--host")
            {
                // Deferred: the menu calls this from its own _Ready, while the root is still adding it.
                Callable.From(() => { if (Host(tree)) Lobby!.Start(); }).CallDeferred();
                return true;
            }
            if (arg.StartsWith("--join")) join = arg.Contains('=') ? arg[(arg.IndexOf('=') + 1)..] : "127.0.0.1";
        }
        return false;
    }

    /// <summary>Back to single player: close the link and the room, forget the role and any join in progress.</summary>
    public static void Reset(SceneTree tree)
    {
        _cancelJoin?.Invoke();
        _cancelJoin = null;
        if (Lobby != null && GodotObject.IsInstanceValid(Lobby))
        {
            // Out of the tree now, so a new room can take the same name (and RPC path) this frame.
            Lobby.GetParent()?.RemoveChild(Lobby);
            Lobby.QueueFree();
        }
        Lobby = null;
        var api = tree.GetMultiplayer();
        if (api.MultiplayerPeer is ENetMultiplayerPeer peer) peer.Close();
        api.MultiplayerPeer = null;
        Pending = NetRole.Offline;
    }

    /// <summary>
    /// This PC's addresses a friend on the same router can join: private IPv4 only (192.168.x.x, 10.x.x.x, 172.16–31.x.x).
    /// Over the internet the public address and an open port are needed (room codes, step ③).
    /// </summary>
    public static string[] LanAddresses() => IP.GetLocalAddresses()
        .Where(a => a.Count(c => c == '.') == 3 && (a.StartsWith("192.168.") || a.StartsWith("10.") ||
                    (a.StartsWith("172.") && int.TryParse(a.Split('.')[1], out int b) && b is >= 16 and <= 31)))
        .Distinct().ToArray();

    /// <summary>127.x.x.x always means this PC: typed for another PC it can never connect (I-76).</summary>
    public static bool IsLoopback(string address) => address.StartsWith("127.") || address == "localhost";

    /// <summary>Opens the room. False when the port is taken (another game already hosts on this PC).</summary>
    public static bool Host(SceneTree tree)
    {
        Reset(tree);
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateServer(Port, MaxClients);
        if (error != Error.Ok) { GD.PushWarning($"[net] could not listen on UDP {Port}: {error}"); return false; }
        tree.GetMultiplayer().MultiplayerPeer = peer;
        Pending = NetRole.Host;
        GD.Print($"[net] hosting on UDP {Port}");
        OpenLobby(tree, NetRole.Host);
        return true;
    }

    /// <param name="done">Called once: true when connected (the room is open), false when the host could not be reached.</param>
    public static void Join(SceneTree tree, string address, System.Action<bool>? done = null)
    {
        Reset(tree);
        LastAddress = address;
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateClient(address, Port);
        if (error != Error.Ok) { GD.PushWarning($"[net] could not connect to {address}: {error}"); done?.Invoke(false); return; }
        var api = tree.GetMultiplayer();
        api.MultiplayerPeer = peer;
        GD.Print($"[net] joining {address}:{Port}");
        // One attempt, one answer: both handlers are removed as soon as either fires (or the attempt is cancelled).
        System.Action ok = null!, fail = null!;
        void Detach() { api.ConnectedToServer -= ok; api.ConnectionFailed -= fail; }
        ok = () =>
        {
            Detach();
            _cancelJoin = null;
            Pending = NetRole.Client;
            OpenLobby(tree, NetRole.Client);
            done?.Invoke(true);
        };
        fail = () =>
        {
            Detach();
            _cancelJoin = null;
            GD.PushWarning($"[net] could not reach {address}:{Port}");
            api.MultiplayerPeer = null;
            done?.Invoke(false);
        };
        api.ConnectedToServer += ok;
        api.ConnectionFailed += fail;
        _cancelJoin = Detach;
    }

    private static void OpenLobby(SceneTree tree, NetRole role)
    {
        Lobby = NetLobby.Create(role, GameSession.SelectedCharacterId);
        tree.Root.AddChild(Lobby);
    }
}
