using Godot;

namespace HordeShooter;

/// <summary>
/// Starts a two-player match (doc/multiplayer-sync-design.md 6장): the menu's "Create Room" / "Join", or the command line
///   Godot --path . -- --host            → listens on <see cref="Port"/> and starts Stage 1 as the host
///   Godot --path . -- --join=127.0.0.1  → connects, then starts Stage 1 as a client
/// The stage reads <see cref="Pending"/> when it is the current scene. Back at the menu everything is reset (I-73).
/// </summary>
public static class NetLaunch
{
    public const int Port = 24680;
    /// <summary>Two-player MVP: the host takes one client.</summary>
    public const int MaxClients = 1;
    public static NetRole Pending { get; private set; } = NetRole.Offline;
    private static bool _argsUsed;
    private static System.Action? _cancelJoin;

    /// <summary>Called by the main menu when it boots; true when it took over. Command-line arguments are used once.</summary>
    public static bool TryStartFromArgs(SceneTree tree)
    {
        if (_argsUsed) return false;
        _argsUsed = true;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--host") return Host(tree);
            if (arg.StartsWith("--join")) { Join(tree, arg.Contains('=') ? arg[(arg.IndexOf('=') + 1)..] : "127.0.0.1"); return true; }
        }
        return false;
    }

    /// <summary>Back to single player: close the link, forget the role and any join in progress.</summary>
    public static void Reset(SceneTree tree)
    {
        _cancelJoin?.Invoke();
        _cancelJoin = null;
        var api = tree.GetMultiplayer();
        if (api.MultiplayerPeer is ENetMultiplayerPeer peer) peer.Close();
        api.MultiplayerPeer = null;
        Pending = NetRole.Offline;
    }

    /// <summary>False when the port is taken (another game already hosts on this PC).</summary>
    public static bool Host(SceneTree tree)
    {
        Reset(tree);
        var peer = new ENetMultiplayerPeer();
        var error = peer.CreateServer(Port, MaxClients);
        if (error != Error.Ok) { GD.PushWarning($"[net] could not listen on UDP {Port}: {error}"); return false; }
        tree.GetMultiplayer().MultiplayerPeer = peer;
        Pending = NetRole.Host;
        GD.Print($"[net] hosting on UDP {Port}");
        // Deferred: the menu may call this from its own _Ready, while the tree is still adding it.
        Callable.From(() => tree.ChangeSceneToFile("res://scenes/Stage1.tscn")).CallDeferred();
        return true;
    }

    /// <param name="done">Called once: true when connected, false when the host could not be reached.</param>
    public static void Join(SceneTree tree, string address, System.Action<bool>? done = null)
    {
        Reset(tree);
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
            done?.Invoke(true);
            Pending = NetRole.Client;
            Callable.From(() => tree.ChangeSceneToFile("res://scenes/Stage1.tscn")).CallDeferred();
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
}
