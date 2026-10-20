using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// The room before a match (doc/menu-multiplayer-plan.md 2장) on one PC: a host lobby and a client lobby, each in its
/// own SubViewport with its own multiplayer API, joined over ENet on 127.0.0.1. Checks the roster, ready, the host's
/// start button, the client following the host into the match, the way back to the room, and a member leaving.
/// Loading the stage is replaced by counters (NetSyncCheck covers the match itself).
/// </summary>
public partial class LobbyCheck : Node
{
    private int _checks;
    private int _hostEntered, _clientEntered, _hostLeft, _clientLeft;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    private async Task Until(Func<bool> done, string message)
    {
        for (int i = 0; i < 300 && !done(); i++) await Frames(1);
        Check(done(), message);
    }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("LOBBY_CHECK_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("LOBBY_CHECK_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private SubViewport View(string name)
    {
        var view = new SubViewport { Name = name, Size = new Vector2I(64, 64) };
        AddChild(view);
        return view;
    }

    private async Task Run()
    {
        GameSession.Mode = GameMode.Scenario;
        int port = 26000 + (int)(Time.GetTicksMsec() % 2000);
        var hostView = View("HostView");
        var clientView = View("ClientView");
        var hostApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(hostApi, hostView.GetPath());
        var hostPeer = new ENetMultiplayerPeer();
        Check(hostPeer.CreateServer(port, 4) == Error.Ok, $"the host listens on UDP {port}");
        hostApi.MultiplayerPeer = hostPeer;
        var host = NetLobby.Create(NetRole.Host, "goose");
        host.EnterMatch = _ => _hostEntered++;
        host.LeaveMatch = _ => _hostLeft++;
        hostView.AddChild(host);
        Check(host.Members.Count == 1 && host.CanStart, "a new room has only the host, who may start alone");

        var clientApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(clientApi, clientView.GetPath());
        var clientPeer = new ENetMultiplayerPeer();
        Check(clientPeer.CreateClient("127.0.0.1", port) == Error.Ok, "the client starts connecting");
        clientApi.MultiplayerPeer = clientPeer;
        await Until(() => clientPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected, "the client connects");
        var client = NetLobby.Create(NetRole.Client, "duck");
        client.EnterMatch = _ => _clientEntered++;
        client.LeaveMatch = _ => _clientLeft++;
        clientView.AddChild(client);
        int clientChanges = 0;
        client.Changed += () => clientChanges++;

        // ---------- roster ----------
        await Until(() => client.Joined && host.Members.Count == 2, "the host answers the client's hello");
        Check(client.Members.Count == 2 && client.Members[0].Peer == NetLobby.HostPeer && client.Members[0].Character == "goose" &&
              client.Members[1].Character == "duck" && client.Mine == client.Members[1], "both sides list the host first, then the member, with their characters");
        Check(!host.CanStart && !client.Members[1].Ready, "a new member is not ready, so the host cannot start");
        Check(client.Mode == host.Mode && clientChanges > 0, "the client shows the host's mode");

        // ---------- ready ----------
        client.SetReady(true);
        await Until(() => host.CanStart, "the member's ready reaches the host and opens [Start]");
        await Until(() => client.Mine?.Ready == true, "the client sees its own ready come back");
        client.SetReady(false);
        await Until(() => !host.CanStart, "cancelling ready closes [Start] again");
        Check(!client.Start() && !client.CanStart, "a member cannot start the match");
        host.SetMode(GameMode.Survival);
        Check(host.Mode == GameMode.Scenario, "a mode this build cannot play is ignored");
        client.SetReady(true);
        await Until(() => host.CanStart, "ready again");

        // ---------- start: the client follows once the host's stage is live ----------
        Check(host.Start() && host.InMatch && _hostEntered == 1, "the host starts and loads the match");
        await Frames(20);
        Check(_clientEntered == 0 && client.InMatch, "the client waits (shown as loading) until the host's stage can take it");
        host.HostLive();
        await Until(() => _clientEntered == 1, "the client loads the match once the host's stage runs");
        Check(!host.Start(), "start does nothing during a match");
        client.SetReady(false);
        await Frames(10);
        Check(host.Members[1].Ready, "ready cannot change during a match");

        // ---------- back to the room ----------
        host.ReturnToLobby();
        await Until(() => _clientLeft == 1 && !client.InMatch, "the host takes everyone back to the room");
        Check(_hostLeft == 1 && !host.InMatch && !host.Live && !host.CanStart && client.Mine?.Ready == false,
            "back in the room nobody is ready, so [Start] waits for them again");

        // ---------- a member leaves (like NetLaunch.Reset: the room node goes first, then the link) ----------
        bool closed = false;
        client.Closed += _ => closed = true;
        clientView.RemoveChild(client);
        client.QueueFree();
        clientPeer.Close();
        await Until(() => host.Members.Count == 1, "a member who leaves is taken off the host's list");
        Check(host.CanStart && !closed, "the host may start alone again; leaving on purpose is not 'the room closed'");

        // ---------- the host closes the room while a member waits in it ----------
        var again = new ENetMultiplayerPeer();
        Check(again.CreateClient("127.0.0.1", port) == Error.Ok, "a member comes back");
        clientApi.MultiplayerPeer = again;
        await Until(() => again.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected, "the member reconnects");
        var back = NetLobby.Create(NetRole.Client, "raccoon");
        string? reason = null;
        back.Closed += r => reason = r;
        clientView.AddChild(back);
        await Until(() => back.Joined && host.Members.Count == 2, "the room takes the member again");
        hostView.RemoveChild(host);
        host.QueueFree();
        hostPeer.Close();
        await Until(() => reason != null, "the waiting member is told the room is gone");
        Check(reason == "msg_net_host_left", "the reason is 'lost the host'");
        clientView.RemoveChild(back); // before the views drop their multiplayer API below
        back.QueueFree();
        again.Close();

        hostPeer.Close();
        GetTree().SetMultiplayer(null, hostView.GetPath());
        GetTree().SetMultiplayer(null, clientView.GetPath());
    }
}
