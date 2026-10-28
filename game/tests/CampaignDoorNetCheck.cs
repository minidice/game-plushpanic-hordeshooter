using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class CampaignDoorNetCheck : Node
{
    private int _checks;
    private Stage _host = null!, _client = null!;
    private SubViewport _hostView = null!, _clientView = null!;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("CAMPAIGN_DOOR_NET_PASS " + _checks); }
        catch (Exception e) { failed = true; GD.PushError(e.ToString()); }
        foreach (var view in new[] { _hostView, _clientView }.Where(v => v != null))
        {
            var api = view.GetMultiplayer();
            if (api.MultiplayerPeer is ENetMultiplayerPeer peer) peer.Close();
            GetTree().SetMultiplayer(null, view.GetPath());
            view.Free();
        }
        QuitHygiene.Collect(); GetTree().Quit(failed ? 1 : 0);
    }
    private SubViewport View(string name)
    {
        var view = new SubViewport { Name = name, OwnWorld3D = true, Size = new Vector2I(1600, 900), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(view); GetTree().SetMultiplayer(MultiplayerApi.CreateDefaultInterface(), view.GetPath()); return view;
    }
    private async Task Cross(Stage stage, Door door, string label)
    {
        stage.Player.TeleportTo(door.GlobalPosition + Vector3.Left * 2 + Vector3.Up * .05f);
        await Frames(12);
        for (int i = 0; i < 90; i++)
        {
            stage.Player.SetControls(Vector2.Right, door.GlobalPosition + Vector3.Right * 8, false);
            await Frames(1);
        }
        stage.Player.SetControls(Vector2.Zero, stage.Player.AimPoint, false);
        Check(stage.Player.GlobalPosition.X > door.GlobalPosition.X + 1, label + $" crosses C1 exit at {stage.Player.GlobalPosition}");
    }
    private async Task Run()
    {
        int port = 28000 + (int)(Time.GetTicksMsec() % 1000);
        _hostView = View("HostView"); _clientView = View("ClientView");
        var hostPeer = new ENetMultiplayerPeer();
        Check(hostPeer.CreateServer(port, 4) == Error.Ok, "host listens");
        _hostView.GetMultiplayer().MultiplayerPeer = hostPeer;
        var tallest = CharacterCatalog.LoadAll(CharacterCatalog.PlayerDir).Where(c => !c.Id.StartsWith("test_")).OrderByDescending(c => c.ColliderHeight).First();
        _host = new Stage { Name = "Stage", Role = NetRole.Host, ModeOverride = GameMode.Scenario, MapOverride = LayoutMap.ForStage(1), CharacterOverride = tallest.Id, DirectorEnabled = false, SquadOverride = 0 };
        _hostView.AddChild(_host); _host.Player.ManualControl = true; _host.Player.Invulnerable = true;
        foreach (var enemy in _host.AllEnemies.ToArray()) enemy.Die(Vector3.Zero, 0);
        var clientPeer = new ENetMultiplayerPeer();
        Check(clientPeer.CreateClient("127.0.0.1", port) == Error.Ok, "client connects");
        _clientView.GetMultiplayer().MultiplayerPeer = clientPeer;
        for (int i = 0; i < 300 && clientPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected; i++) await Frames(1);
        _client = new Stage { Name = "Stage", Role = NetRole.Client, ModeOverride = GameMode.Scenario, MapOverride = LayoutMap.ForStage(1), CharacterOverride = "duck", DirectorEnabled = false, SquadOverride = 0 };
        _clientView.AddChild(_client); _client.Player.ManualControl = true;
        for (int i = 0; i < 300 && !(_client.Net!.Started && _host.Net!.Peers.Count == 2); i++) await Frames(1);
        Check(_client.Net!.Started && _host.Net!.Peers.Count == 2, "two survivors synchronized on large campaign");
        var doors = _host.Map.Doors;
        Check(doors.Count > 8 && doors.Count == _client.Map.Doors.Count, "campaign has more than eight matching doors");
        var exitSwitch = ((LayoutMap)_host.Map).BaseExits[2];
        var c1Exit = doors.OrderBy(d => d.GlobalPosition.DistanceTo(exitSwitch.GlobalPosition)).First();
        int index = doors.ToList().IndexOf(c1Exit);
        Check(index >= 8, $"C1 exit is door index {index}");
        exitSwitch.Use(_host.Player);
        await Cross(_host, c1Exit, $"tallest standard survivor ({tallest.Id}, {tallest.ColliderHeight}m)");
        _host.Player.TeleportTo(c1Exit.GlobalPosition + new Vector3(5, 0, 3));
        await Frames(20);
        Check(_client.Map.Doors[index].IsOpen, "C1 exit open state reaches the duck's client");
        await Cross(_client, _client.Map.Doors[index], "network client duck (2.06m)");
        if (DisplayServer.GetName() != "headless")
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            _clientView.GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/campaign-door-client-passed.png"));
        }
        for (int i = 0; i < doors.Count; i++) doors[i].SetOpen(i % 2 == 0, 0);
        await Frames(20);
        Check(doors.Select((d, i) => d.IsOpen == _client.Map.Doors[i].IsOpen).All(v => v), "alternating door states above and below index eight remain independent");
        foreach (var door in doors) door.SetOpen(false, 0);
        await Frames(20);
        Check(_client.Map.Doors.All(d => !d.IsOpen), "all door closures synchronize");
        Check(_host.Map.AlarmDevices.Count > 0, "large campaign has alarms after its doors");
        _host.Map.AlarmDevices[0].Sound();
        await Frames(20);
        Check(_client.Map.AlarmDevices[0].Triggered, "alarm state synchronizes independently of door count");
        Check(_client.Net.ApplyErrors == 0, "new world state produces no decoding errors");
    }
}
