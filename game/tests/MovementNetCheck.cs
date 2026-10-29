using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Isolated real ENet movement checks; no live enemies can mask the deliberately applied hit.</summary>
public partial class MovementNetCheck : Node
{
    private int _checks;
    private ENetMultiplayerPeer? _server, _connection;
    private void Check(bool ok, string message)
    { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n)
    { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Until(Func<bool> condition)
    { for (int i = 0; i < 180 && !condition(); i++) await Frames(1); }
    private SubViewport View(string name)
    {
        var view = new SubViewport { Name = name, OwnWorld3D = true, Size = new Vector2I(640, 360) };
        AddChild(view); return view;
    }
    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("MOVEMENT_NET_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("MOVEMENT_NET_FAIL " + e.Message); failed = true; }
        _connection?.Close(); _server?.Close();
        QuitHygiene.Collect(); GetTree().Quit(failed ? 1 : 0);
    }
    private async Task Run()
    {
        int port = 27000 + (int)(Time.GetTicksMsec() % 1000);
        var hostView = View("Host"); var clientView = View("Client");
        var hostApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(hostApi, hostView.GetPath());
        _server = new ENetMultiplayerPeer();
        Check(_server.CreateServer(port, 4) == Error.Ok, "movement host starts");
        hostApi.MultiplayerPeer = _server;
        var host = new Stage { Name = "Stage", CharacterOverride = "goose", Role = NetRole.Host, DirectorEnabled = false };
        hostView.AddChild(host); host.Player.ManualControl = true;
        await Frames(3);
        var clientApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(clientApi, clientView.GetPath());
        _connection = new ENetMultiplayerPeer();
        Check(_connection.CreateClient("127.0.0.1", port) == Error.Ok, "movement client starts");
        clientApi.MultiplayerPeer = _connection;
        await Until(() => _connection.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected);
        var client = new Stage { Name = "Stage", CharacterOverride = "duck", Role = NetRole.Client, DirectorEnabled = false };
        clientView.AddChild(client); client.Player.ManualControl = true;
        await Until(() => client.Net!.Started && host.Players.Count == 2);
        Check(client.Net!.Started && host.Players.Count == 2, "movement peers finish their handshake");
        foreach (var enemy in host.AllEnemies.ToArray()) enemy.Die(Vector3.Zero, 0);
        var owner = client.Player; var authority = host.Players.Single(p => p.IsRemote);
        owner.SetControls(Vector2.Zero, owner.AimPoint, false);
        host.Player.SetControls(Vector2.Zero, host.Player.AimPoint, false);
        await Frames(140);
        Check(owner.IsAutoRunning, "quiet owner becomes ready to run");
        int expectedHealth = authority.Health - 3;
        authority.TakeDamage(3, Vector3.Zero);
        await Until(() => owner.Health == expectedHealth);
        Check(owner.Health == expectedHealth && !owner.IsAutoRunning && owner.AutoRunDelayLeft > 1,
            $"directionless host damage resets owner run delay (health {owner.Health}/{expectedHealth}, delay {owner.AutoRunDelayLeft:0.00})");
        authority.PressRoll(); await Frames(35);
        await Until(() => owner.RollCharges == 1);
        Check(authority.RollCharges == 1 && owner.RollCharges == 1, "owner receives the host's corrected available charge");
        Check(Mathf.Abs(authority.RollRechargeLeft - owner.RollRechargeLeft) < .3f, "charge recovery timers agree across ENet");
        owner.PressRoll(); await Frames(3);
        Check(owner.IsRolling && owner.RollCharges == 0, "owner predicts its second charge without waiting for network");
        await Frames(32);
        Check(authority.RollCharges == 0 && owner.RollCharges == 0, "host consumes the owner's second roll exactly once");
        var stale = owner.CaptureNetState();
        stale.RollPressSeen = (owner.TakeNetInput(0).Presses[(int)NetPress.Roll] - 1) & 255;
        stale.RollCharges = 2;
        owner.ApplyNetVitals(stale);
        Check(owner.RollCharges == 0, "an unacknowledged snapshot cannot refund a predicted roll");
        await Frames(340);
        Check(authority.RollCharges == 1 && owner.RollCharges == 1, "only one charge recovers first on both peers");
        Check(client.Net.ApplyErrors == 0, "movement snapshots apply without network errors");
    }
}
