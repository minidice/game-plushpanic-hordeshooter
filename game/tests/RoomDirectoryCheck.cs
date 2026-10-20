using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// The public room list against a running lobby server (Minidice lobby/, docs/LOBBY_SPEC.md). Needs one:
///   node lobby/app.js   (PORT=3125)  then   Godot --headless --path . res://tests/RoomDirectoryCheck.tscn -- --lobby=http://127.0.0.1:3125/hordeshooter
/// A host room goes on the list with this build's version, follows the player count and match state, and leaves the
/// list when the room closes. A room of another version is listed as such.
/// </summary>
public partial class RoomDirectoryCheck : Node
{
    private int _checks;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    private async Task<RoomDirectory.Room?> Until(Func<RoomDirectory.Room?, bool> done, string id)
    {
        RoomDirectory.Room? room = null;
        for (int i = 0; i < 40; i++)
        {
            room = (await RoomDirectory.List())?.FirstOrDefault(r => r.Id == id);
            if (done(room)) return room;
            await ToSignal(GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
        }
        return room;
    }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("ROOM_DIRECTORY_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("ROOM_DIRECTORY_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        RoomDirectory.Url = "";
        RoomDirectory.ReadArgs();
        Check(RoomDirectory.Enabled, "a lobby server was given with --lobby=URL");
        Check(await RoomDirectory.List() != null, $"the lobby server answers at {RoomDirectory.Url}");

        int port = 27000 + (int)(Time.GetTicksMsec() % 2000);
        var hostView = new SubViewport { Name = "HostView", Size = new Vector2I(64, 64) };
        var clientView = new SubViewport { Name = "ClientView", Size = new Vector2I(64, 64) };
        AddChild(hostView);
        AddChild(clientView);
        var hostApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(hostApi, hostView.GetPath());
        var hostPeer = new ENetMultiplayerPeer();
        Check(hostPeer.CreateServer(port, 4) == Error.Ok, $"the host listens on UDP {port}");
        hostApi.MultiplayerPeer = hostPeer;
        var host = NetLobby.Create(NetRole.Host, "goose");
        host.Advertise = true;
        host.ListName = "RoomDirectoryCheck";
        host.EnterMatch = _ => { };
        hostView.AddChild(host);

        for (int i = 0; i < 200 && !host.Listed; i++) await Frames(1);
        Check(host.Listed && !host.ListFailed, "the host's room goes on the list");
        var mine = (await RoomDirectory.List())!.FirstOrDefault(r => r.Name == "RoomDirectoryCheck");
        Check(mine is { Players: 1, State: "lobby", SameVersion: true } && mine.Version == NetLobby.Version && mine.Port == NetLaunch.Port,
            $"listed with this build's version {NetLobby.Version}, one player, waiting");
        Check(mine!.SameNetwork && mine.Lan.SequenceEqual(NetLaunch.LanAddresses().Take(4)), "a player behind the same address gets the host's LAN addresses");

        // A friend comes in: the count follows within a couple of seconds, not at the next 15 s refresh.
        var clientApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(clientApi, clientView.GetPath());
        var clientPeer = new ENetMultiplayerPeer();
        Check(clientPeer.CreateClient("127.0.0.1", port) == Error.Ok, "a client connects");
        clientApi.MultiplayerPeer = clientPeer;
        for (int i = 0; i < 300 && clientPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected; i++) await Frames(1);
        var client = NetLobby.Create(NetRole.Client, "duck");
        clientView.AddChild(client);
        Check((await Until(r => r?.Players == 2, mine.Id))?.Players == 2, "the list shows two players soon after the friend joins");

        client.SetReady(true);
        for (int i = 0; i < 120 && !host.CanStart; i++) await Frames(1);
        Check(host.Start(), "the host starts");
        Check((await Until(r => r?.State == "playing", mine.Id))?.State == "playing", "the list shows the room as playing");

        // Another build's room: listed, marked as another version.
        var other = await RoomDirectory.Register(new RoomDirectory.Ad("p0-00000000", "OtherVersionCheck", 1, 2, "lobby", 24680, Array.Empty<string>()));
        Check(other != null, "a room of another version can be listed");
        var seen = (await RoomDirectory.List())!.First(r => r.Id == other!.Id);
        Check(!seen.SameVersion && seen.Version == "p0-00000000", "it reads as another version (the menu locks it)");
        await RoomDirectory.Remove(other!);

        // The room closes (NetLaunch.Reset): off the list at once.
        clientView.RemoveChild(client);
        client.QueueFree();
        clientPeer.Close();
        hostView.RemoveChild(host);
        host.QueueFree();
        hostPeer.Close();
        Check(await Until(r => r == null, mine.Id) == null, "a closed room leaves the list");
        GetTree().SetMultiplayer(null, hostView.GetPath());
        GetTree().SetMultiplayer(null, clientView.GetPath());
    }
}
