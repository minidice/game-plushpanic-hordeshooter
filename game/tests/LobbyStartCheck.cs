using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Run as two processes with --role=host/client --port=26789. Exercises the real menu-to-stage transition.</summary>
public partial class LobbyStartCheck : Node
{
    private string _role = "";
    private async Task Until(Func<bool> done, string what, int seconds = 35)
    {
        ulong end = Time.GetTicksMsec() + (ulong)seconds * 1000;
        while (!done() && Time.GetTicksMsec() < end) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!done()) throw new Exception(what);
        GD.Print("PASS: " + what);
    }
    private Button Button(MainMenu menu, string name) => (Button)(menu.FindChild(name, true, false) ?? throw new Exception(name));
    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("LOBBY_START_PASS " + _role); }
        catch (Exception e) { failed = true; GD.PushError(e.ToString()); }
        if (GetTree().CurrentScene is { } scene) { GetTree().CurrentScene = null; scene.Free(); }
        NetLaunch.Reset(GetTree());
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }
    private async Task Run()
    {
        var args = OS.GetCmdlineUserArgs();
        _role = args.First(a => a.StartsWith("--role="))[7..];
        int port = int.Parse(args.First(a => a.StartsWith("--port="))[7..]);
        GameSettings.Path = "user://lobby-start-check-" + _role + ".cfg";
        GameSettings.Load();
        RoomDirectory.Url = "";
        GameSession.Mode = GameMode.Scenario;
        GameSession.Campaign = "campaign";
        GetTree().GetMultiplayer().ServerDisconnected += () => GD.Print($"[lobby-check] disconnected at {Time.GetTicksMsec()}ms");
        // Keep this driver alive while the real current scene is replaced by Menu and Stage.
        GetTree().CurrentScene = null;
        GetTree().ChangeSceneToFile("res://scenes/Menu.tscn");
        GetTree().SceneChanged += () => GD.Print("[lobby-check] scene=" + GetTree().CurrentScene?.Name);
        await Until(() => GetTree().CurrentScene is MainMenu, "real menu opened");
        var menu = (MainMenu)GetTree().CurrentScene;
        if (_role == "host")
        {
            // A separate test port avoids interfering with a user's open game on 24680.
            var peer = new ENetMultiplayerPeer();
            if (peer.CreateServer(port, NetLaunch.MaxClients) != Error.Ok) throw new Exception("test port busy");
            GetTree().GetMultiplayer().MultiplayerPeer = peer;
            var lobby = NetLobby.Create(NetRole.Host, "goose");
            typeof(NetLaunch).GetProperty(nameof(NetLaunch.Lobby))!.SetValue(null, lobby);
            typeof(NetLaunch).GetProperty(nameof(NetLaunch.Pending))!.SetValue(null, NetRole.Host);
            GetTree().Root.AddChild(lobby);
            typeof(MainMenu).GetMethod("AttachLobby", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(menu, null);
            menu.ShowPage(MenuPage.Lobby);
        }
        else
        {
            menu.JoinRoom("127.0.0.1", port);
            await Until(() => NetLaunch.Lobby?.Joined == true, "joined the waiting lobby");
        }
        for (int round = 1; round <= 2; round++)
        {
            menu = (MainMenu)GetTree().CurrentScene;
            if (_role == "host")
            {
                await Until(() => NetLaunch.Lobby!.Members.Count == 2 && NetLaunch.Lobby.CanStart, $"round {round}: both players ready in lobby");
                GD.Print($"[lobby-check] start at {Time.GetTicksMsec()}ms");
                Button(menu, "Lobby_Start").EmitSignal(BaseButton.SignalName.Pressed);
            }
            else Button(menu, "Lobby_Ready").ButtonPressed = true;
            await Until(() => GetTree().CurrentScene is Stage, $"round {round}: entered real stage");
            var stage = (Stage)GetTree().CurrentScene;
            await Until(() => stage.Net is { Started: true } && stage.Net.Peers.Count == 2, $"round {round}: both survivors synchronized");
            ulong until = Time.GetTicksMsec() + (_role == "host" ? 12000UL : 10000UL);
            while (Time.GetTicksMsec() < until)
            {
                if (!IsInstanceValid(stage) || GetTree().CurrentScene != stage) throw new Exception("returned to menu after lobby start");
                if (stage.Net!.ApplyErrors != 0) throw new Exception("network apply error after lobby start");
                if (Time.GetTicksMsec() + 4000 < until && stage.Net.Peers.Count != 2) throw new Exception("peer left immediately after lobby start");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            if (round == 2) break;
            if (_role == "host") NetLaunch.Lobby!.ReturnToLobby();
            await Until(() => GetTree().CurrentScene is MainMenu { Page: MenuPage.Lobby }, "returned to lobby together without disconnecting");
        }
    }
}
